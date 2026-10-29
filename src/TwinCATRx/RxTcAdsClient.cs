// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
#if REACTIVE_SHIM
using IoT.Driver.TwinCATRx.Core.Reactive;
#else
using IoT.Driver.TwinCATRx.Core;
#endif
using TwinCAT.Ads;

#if REACTIVE_SHIM
namespace IoT.Driver.TwinCATRx.Reactive;
#else
namespace IoT.Driver.TwinCATRx;
#endif

/// <summary>Observable TwinCAT ADS Client.</summary>
public partial class RxTcAdsClient : IRxTcAdsClient
{
    /// <summary>Stores the first TwinCAT 3 ADS port.</summary>
    private const int TwinCat3Port = 851;

    /// <summary>Stores the retry delay used when establishing a PLC connection.</summary>
    private const int ConnectionRetryDelaySeconds = 5;

    /// <summary>Publishes generated code snapshots.</summary>
    private readonly Signal<string[]> _codeSubject = new();

    /// <summary>Publishes data read from PLC variables.</summary>
    private readonly Signal<(string Variable, object? Data, string? Id)> _dataReceived = new();

    /// <summary>Publishes client errors.</summary>
    private readonly Signal<Exception> _errorReceived = new();

    /// <summary>Publishes write results.</summary>
    private readonly Signal<string?> _onWriteSubject = new();

    /// <summary>Publishes PLC initialization completion.</summary>
    private readonly ReplaySignal<Unit> _initCompleteSubject = new(1);

    /// <summary>Publishes pause state changes.</summary>
    private readonly ReplaySignal<bool> _isPausedSubject = new(1);

    /// <summary>Stores replaceable platform dependencies.</summary>
    private readonly IRxTcAdsPlatform _platform;

    /// <summary>Identifies generated assemblies owned by this client instance.</summary>
    private readonly string _generatedAssemblyIdentifier;

    /// <summary>Stores disposable resources owned by this client.</summary>
    private CompositeDisposable? _cleanup;

    /// <summary>Stores the active PLC initialization subscription.</summary>
    private IDisposable? _plcCleanup;

    /// <summary>Tracks disposal of the client independently of its connection.</summary>
    private int _disposed;

    /// <summary>Stores the most recently configured settings.</summary>
    private ISettings? _settings;

    /// <summary>Initializes a new instance of the <see cref="RxTcAdsClient"/> class.</summary>
    public RxTcAdsClient()
        : this(TimeProvider.System, RxTcAdsPlatform.CreateDefault())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RxTcAdsClient"/> class.</summary>
    /// <param name="timeProvider">The time provider.</param>
    public RxTcAdsClient(TimeProvider timeProvider)
        : this(timeProvider, RxTcAdsPlatform.CreateDefault())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RxTcAdsClient"/> class.</summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="platform">The replaceable platform dependencies.</param>
    internal RxTcAdsClient(TimeProvider timeProvider, IRxTcAdsPlatform platform)
    {
        _ = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _generatedAssemblyIdentifier = Guid.NewGuid().ToString("N");
        _platform = platform ?? throw new ArgumentNullException(nameof(platform));
    }

    /// <summary>Gets codes this instance.</summary>
    /// <returns>A Value.</returns>
    public IObservable<string[]> Code => _codeSubject.Retry(int.MaxValue).Publish().RefCount();

    /// <summary>Gets a value indicating whether this <see cref="RxTcAdsClient"/> is connected.</summary>
    /// <value><c>true</c> if connected; otherwise, <c>false</c>.</value>
    public bool Connected { get; internal set; }

    /// <summary>Gets the initialize complete. PLC is ready to read and write.</summary>
    /// <value>
    /// The initialize complete.
    /// </value>
    public IObservable<Unit> InitializeComplete => _initCompleteSubject.Retry(int.MaxValue).Publish().RefCount();

    /// <inheritdoc/>
    public IObservableAsync<Unit> InitializeCompleteAsync =>
        ObservableBridgeExtensions.ToAsyncObservable(InitializeComplete);

    /// <summary>Gets the data received.</summary>
    /// <value>The data received.</value>
    public IObservable<(string Variable, object? Data, string? Id)> DataReceived =>
        _dataReceived.Retry(int.MaxValue).Publish().RefCount();

    /// <inheritdoc/>
    public IObservableAsync<(string Variable, object? Data, string? Id)> DataReceivedAsync =>
        ObservableBridgeExtensions.ToAsyncObservable(DataReceived);

    /// <summary>Gets error received.</summary>
    /// <returns>A Value.</returns>
    public IObservable<Exception> ErrorReceived => _errorReceived;

    /// <inheritdoc/>
    public IObservableAsync<Exception> ErrorReceivedAsync =>
        ObservableBridgeExtensions.ToAsyncObservable(ErrorReceived);

    /// <summary>Gets a value indicating whether gets a value that indicates whether the object is disposed.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>Gets the on write.</summary>
    /// <value>The on write.</value>
    public IObservable<string?> OnWrite => _onWriteSubject.Retry(int.MaxValue).Publish().RefCount();

    /// <inheritdoc/>
    public IObservableAsync<string?> OnWriteAsync => ObservableBridgeExtensions.ToAsyncObservable(OnWrite);

    /// <summary>Gets the read write handle information.</summary>
    /// <value>The read write handle information.</value>
    public IDictionary<string, uint?> ReadWriteHandleInfo => ConnectionState.ReadWriteHandles;

    /// <summary>Gets the write handle information.</summary>
    /// <value>The write handle information.</value>
    public IDictionary<string, (uint? Handle, int ArrayLength)> WriteHandleInfo => ConnectionState.WriteHandles;

    /// <summary>Gets the settings.</summary>
    /// <value>
    /// The settings.
    /// </value>
    public ISettings? Settings
    {
        get => _executingConnection.Value?.Settings ?? _settings;
        private set => _settings = value;
    }

    /// <summary>Gets a value indicating whether this instance is paused.</summary>
    /// <value>
    ///   <c>true</c> if this instance is paused; otherwise, <c>false</c>.
    /// </value>
    public bool IsPaused { get; private set; }

    /// <summary>Gets the is paused observable.</summary>
    /// <value>
    /// The is paused observable.
    /// </value>
    public IObservable<bool> IsPausedObservable => _isPausedSubject.Retry(int.MaxValue).Publish().RefCount();

    /// <inheritdoc/>
    public IObservableAsync<bool> IsPausedObservableAsync =>
        ObservableBridgeExtensions.ToAsyncObservable(IsPausedObservable);

    /// <summary>Gets the signal for ADS client state changes.</summary>
    private Signal<AdsState> _clientState => ConnectionState.ClientState;

    /// <summary>Gets the queue of PLC read requests.</summary>
    private Signal<(uint? Handle, Type Type, int Length, string? Id)> _readPLC => ConnectionState.Reads;

    /// <summary>Gets the signal for TwinCAT service status updates.</summary>
    private ReplaySignal<ServiceStatus> _serviceStatus => ConnectionState.ServiceStatus;

    /// <summary>Gets the queue of PLC write requests.</summary>
    private Signal<(uint? Handle, object Value, int Length, string? Id)> _writePLC => ConnectionState.Writes;

    /// <summary>Gets generated code payloads.</summary>
    private List<string> _code => ConnectionState.Code;

    /// <summary>Gets resolved PLC variable types by variable name.</summary>
    private Dictionary<string, Type> _typeInfo => ConnectionState.TypeInfo;

    /// <summary>Gets read-write ADS handles mapped to variable names.</summary>
    private Dictionary<uint, string> _readWriteVariablesByHandle => ConnectionState.ReadWriteVariables;

    /// <summary>Gets write ADS handles mapped to variable names.</summary>
    private Dictionary<uint, string> _writeVariablesByHandle => ConnectionState.WriteVariables;

    /// <summary>Gets or sets the dynamic PLC code generator.</summary>
    private ICodeGenerator? _codeGenerator { get => ConnectionState.CodeGenerator; set => ConnectionState.CodeGenerator = value; }

    /// <summary>Gets or sets whether the current ADS connection completed initialization.</summary>
    private bool _initialized { get => ConnectionState.Initialized; set => ConnectionState.Initialized = value; }

    /// <summary>Connects the specified settings.</summary>
    /// <param name="settings">The settings.</param>
    /// <exception cref="Exception">An Exception.</exception>
    [RequiresUnreferencedCode("Invokes dynamic code generation and reflection to materialize PLC types.")]
    [RequiresDynamicCode("Invokes dynamic code generation and reflection to materialize PLC types.")]
    public void Connect(ISettings settings)
    {
        if (IsDisposed)
        {
            Publish(_errorReceived, new ObjectDisposedException(nameof(RxTcAdsClient)));
            return;
        }

        try
        {
            CompositeDisposable subscription;
            lock (_lifecycleGate)
            {
                if (IsDisposed || _plcCleanup is not null)
                {
                    return;
                }

                subscription = new();
                _plcCleanup = subscription;
                Settings = settings;
                Connected = false;
            }

            _ = ObservableBridgeExtensions.SubscribeTo(InitPLC(subscription)).DisposeWith(subscription);
        }
        catch (Exception ex)
        {
            Publish(_errorReceived, ex);
        }
    }

    /// <summary>Disconnects this instance.</summary>
    public void Disconnect()
    {
        IDisposable? subscription;
        ConnectionScope? resources;
        bool wasPaused;
        lock (_lifecycleGate)
        {
            subscription = _plcCleanup;
            _plcCleanup = null;
            resources = CloseConnectionLocked(_connection ?? _lastConnection);
            wasPaused = IsPaused;
            IsPaused = false;
            _pauseVersion++;
        }

        subscription?.Dispose();
        resources?.Dispose();
        if (!wasPaused)
        {
            return;
        }

        Publish(_isPausedSubject, false);
    }

    /// <summary>Releases unmanaged and - optionally - managed resources.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Reads the specified variable.</summary>
    /// <param name="variable">The data.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Parameters - Parameter 0 must be set to the size of the Array.
    /// </exception>
    public void Read(string variable) => Read(variable, null, null);

    /// <summary>Reads a variable with a correlation identifier.</summary>
    /// <param name="variable">The variable.</param>
    /// <param name="id">The correlation identifier.</param>
    public void Read(string variable, string? id) => Read(variable, null, id);

    /// <summary>Reads a variable with an explicit array length.</summary>
    /// <param name="variable">The variable.</param>
    /// <param name="arrayLength">The optional array length.</param>
    public void Read(string variable, int? arrayLength) => Read(variable, arrayLength, null);

    /// <summary>Reads a variable with an explicit array length and correlation identifier.</summary>
    /// <param name="variable">The variable.</param>
    /// <param name="arrayLength">The optional array length.</param>
    /// <param name="id">The correlation identifier.</param>
    public void Read(string variable, int? arrayLength, string? id)
        => QueueCurrentConnection(() => ReadConnectionVariable(variable, arrayLength, id));

    /// <summary>Writes the specified variable.</summary>
    /// <param name="variable">The variable.</param>
    /// <param name="value">The value.</param>
    public void Write(string variable, object value) => Write(variable, value, null);

    /// <summary>Writes a variable with a correlation identifier.</summary>
    /// <param name="variable">The variable.</param>
    /// <param name="value">The value.</param>
    /// <param name="id">The correlation identifier.</param>
    public void Write(string variable, object value, string? id)
        => QueueCurrentConnection(() => WriteConnectionVariable(variable, value, id));

    /// <summary>Pauses the specified time.</summary>
    /// <param name="time">The time.</param>
    public void Pause(TimeSpan time)
    {
        bool connected;
        lock (_lifecycleGate)
        {
            connected = IsCurrentConnection(_connection);
        }

        if (!connected)
        {
            PublishError(new ObjectDisposedException(nameof(RxTcAdsClient)));
            return;
        }

        if (time.TotalMilliseconds <= 0)
        {
            return;
        }

        QueueCurrentConnection(() =>
        {
            var cleanup = new CompositeDisposable();
            _ = cleanup.DisposeWith(ConnectionLifetime);
            lock (_lifecycleGate)
            {
                if (!IsCurrentConnection(_executingConnection.Value))
                {
                    return;
                }

                IsPaused = true;
                _pauseVersion++;
            }

            Publish(_isPausedSubject, true);

            _ = SubscribeConnection(Observable.Timer(time), _ =>
            {
                lock (_lifecycleGate)
                {
                    if (!IsCurrentConnection(_executingConnection.Value))
                    {
                        return;
                    }

                    IsPaused = false;
                    _pauseVersion++;
                }

                Publish(_isPausedSubject, false);
                cleanup.Dispose();
            }).DisposeWith(cleanup);
        });
    }

    /// <summary>Releases unmanaged and - optionally - managed resources.</summary>
    /// <param name="disposing">
    /// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only
    /// unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing || Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Disconnect();
        bool release;
        lock (_lifecycleGate)
        {
            release = _activePublications == 0;
        }

        if (!release)
        {
            return;
        }

        DisposeSignals();
    }

    /// <summary>Disposes the client-owned signals.</summary>
    private void DisposeSignals()
    {
        if (Interlocked.Exchange(ref _signalsDisposed, 1) != 0)
        {
            return;
        }

        _codeSubject.Dispose();
        _errorReceived.Dispose();
        _onWriteSubject.Dispose();
        _dataReceived.Dispose();
        _initCompleteSubject.Dispose();
        _isPausedSubject.Dispose();
    }

    /// <summary>Queues a read for a resolved variable.</summary>
    /// <param name="variable">The PLC variable name.</param>
    /// <param name="arrayLength">The optional array length.</param>
    /// <param name="id">The correlation identifier.</param>
    private void ReadConnectionVariable(string variable, int? arrayLength, string? id)
    {
        if (!TryGetReadTarget(variable, arrayLength, out var handle, out var type, out var readLength) || type is null)
        {
            return;
        }

        if (type.IsArray || type == typeof(string))
        {
            ReadArrayHandle(handle, type, readLength, id);
        }
        else
        {
            ReadHandle(handle, type, id);
        }
    }

    /// <summary>Queues a write for a resolved variable.</summary>
    /// <param name="variable">The PLC variable name.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="id">The correlation identifier.</param>
    private void WriteConnectionVariable(string variable, object value, string? id)
    {
        if (string.IsNullOrWhiteSpace(variable))
        {
            return;
        }

        if (ReadWriteHandleInfo.TryGetValue(variable, out var readWritehandle))
        {
            WriteHandle(readWritehandle, value, id: id);
            return;
        }

        if (!WriteHandleInfo.TryGetValue(variable, out var item))
        {
            return;
        }

        WriteHandle(item.Handle, value, item.ArrayLength, id: id);
    }

    /// <summary>Queues a PLC array read request.</summary>
    /// <param name="handle">The ADS variable handle.</param>
    /// <param name="type">The value type.</param>
    /// <param name="length">The array length.</param>
    /// <param name="id">The correlation identifier.</param>
    private void ReadArrayHandle(uint? handle, Type type, int length, string? id) =>
        QueueConnection(() => _readPLC.OnNext((handle, type, length, id)));

    /// <summary>Queues a PLC scalar read request.</summary>
    /// <param name="handle">The ADS variable handle.</param>
    /// <param name="type">The value type.</param>
    /// <param name="id">The correlation identifier.</param>
    private void ReadHandle(uint? handle, Type type, string? id) =>
        QueueConnection(() => _readPLC.OnNext((handle, type, -1, id)));

    /// <summary>Queues a PLC write request.</summary>
    /// <param name="handle">The ADS variable handle.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="length">The array length.</param>
    /// <param name="id">The correlation identifier.</param>
    private void WriteHandle(uint? handle, object value, int length = -1, string? id = null) =>
        QueueConnection(() => _writePLC.OnNext((handle, value, length, id)));
}
