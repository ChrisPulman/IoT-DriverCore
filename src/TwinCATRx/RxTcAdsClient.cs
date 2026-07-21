// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
#if REACTIVE_SHIM
using CP.TwinCatRx.Core.Reactive;
#else
using CP.TwinCatRx.Core;
#endif
using TwinCAT.Ads;

#if REACTIVE_SHIM
namespace CP.TwinCatRx.Reactive;
#else
namespace CP.TwinCatRx;
#endif

/// <summary>Observable TwinCAT ADS Client.</summary>
public partial class RxTcAdsClient : IRxTcAdsClient
{
    /// <summary>Stores the first TwinCAT 3 ADS port.</summary>
    private const int TwinCat3Port = 851;

    /// <summary>Stores the retry delay used when establishing a PLC connection.</summary>
    private const int ConnectionRetryDelaySeconds = 5;

    /// <summary>Publishes ADS client state changes.</summary>
    private readonly Signal<AdsState> _clientState = new();

    /// <summary>Publishes generated code snapshots.</summary>
    private readonly Signal<string[]> _codeSubject = new();

    /// <summary>Publishes data read from PLC variables.</summary>
    private readonly Signal<(string Variable, object? Data, string? Id)> _dataReceived = new();

    /// <summary>Publishes client errors.</summary>
    private readonly Signal<Exception> _errorReceived = new();

    /// <summary>Publishes write results.</summary>
    private readonly Signal<string?> _onWriteSubject = new();

    /// <summary>Queues PLC read requests.</summary>
    private readonly Signal<(uint? Handle, Type Type, int Length, string? Id)> _readPLC = new();

    /// <summary>Publishes TwinCAT service status updates.</summary>
    private readonly ReplaySignal<ServiceStatus> _serviceStatus = new(1);

    /// <summary>Queues PLC write requests.</summary>
    private readonly Signal<(uint? Handle, object Value, int Length, string? Id)> _writePLC = new();

    /// <summary>Publishes PLC initialization completion.</summary>
    private readonly ReplaySignal<Unit> _initCompleteSubject = new(1);

    /// <summary>Publishes pause state changes.</summary>
    private readonly ReplaySignal<bool> _isPausedSubject = new(1);

    /// <summary>Stores generated code payloads.</summary>
    private readonly List<string> _code = [];

    /// <summary>Stores resolved PLC variable types by variable name.</summary>
    private readonly Dictionary<string, Type> _typeInfo = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Maps read-write ADS handles to variable names.</summary>
    private readonly Dictionary<uint, string> _readWriteVariablesByHandle = [];

    /// <summary>Maps write ADS handles to variable names.</summary>
    private readonly Dictionary<uint, string> _writeVariablesByHandle = [];

    /// <summary>Stores the time provider used to obtain the current time.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>Stores disposable resources owned by this client.</summary>
    private CompositeDisposable? _cleanup;

    /// <summary>Stores the dynamic PLC code generator.</summary>
    private CodeGenerator? _codeGenerator;

    /// <summary>Stores the active PLC initialization subscription.</summary>
    private IDisposable? _plcCleanup;

    /// <summary>Stores whether the current ADS connection completed initialization.</summary>
    private bool _initialized;

    /// <summary>Initializes a new instance of the <see cref="RxTcAdsClient"/> class.</summary>
    public RxTcAdsClient()
    {
        _timeProvider = TimeProvider.System;
    }

    /// <summary>Initializes a new instance of the <see cref="RxTcAdsClient"/> class.</summary>
    /// <param name="timeProvider">The time provider.</param>
    public RxTcAdsClient(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
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
    public IObservable<Exception> ErrorReceived => _errorReceived.Retry(int.MaxValue).Publish().RefCount();

    /// <inheritdoc/>
    public IObservableAsync<Exception> ErrorReceivedAsync =>
        ObservableBridgeExtensions.ToAsyncObservable(ErrorReceived);

    /// <summary>Gets a value indicating whether gets a value that indicates whether the object is disposed.</summary>
    public bool IsDisposed => _cleanup?.IsDisposed ?? false;

    /// <summary>Gets the on write.</summary>
    /// <value>The on write.</value>
    public IObservable<string?> OnWrite => _onWriteSubject.Retry(int.MaxValue).Publish().RefCount();

    /// <inheritdoc/>
    public IObservableAsync<string?> OnWriteAsync => ObservableBridgeExtensions.ToAsyncObservable(OnWrite);

    /// <summary>Gets the read write handle information.</summary>
    /// <value>The read write handle information.</value>
    public IDictionary<string, uint?> ReadWriteHandleInfo { get; } =
        new Dictionary<string, uint?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the write handle information.</summary>
    /// <value>The write handle information.</value>
    public IDictionary<string, (uint? Handle, int ArrayLength)> WriteHandleInfo { get; } =
        new Dictionary<string, (uint? Handle, int ArrayLength)>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the settings.</summary>
    /// <value>
    /// The settings.
    /// </value>
    public ISettings? Settings { get; private set; }

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

    /// <summary>Connects the specified settings.</summary>
    /// <param name="settings">The settings.</param>
    /// <exception cref="Exception">An Exception.</exception>
    [RequiresUnreferencedCode("Invokes dynamic code generation and reflection to materialize PLC types.")]
    [RequiresDynamicCode("Invokes dynamic code generation and reflection to materialize PLC types.")]
    public void Connect(ISettings settings)
    {
        if (_cleanup?.IsDisposed == true)
        {
            _errorReceived.OnNext(new ObjectDisposedException(nameof(RxTcAdsClient)));
            return;
        }

        try
        {
            if (_plcCleanup is null)
            {
                Settings = settings;
                Connected = false;
                _plcCleanup = ObservableBridgeExtensions.SubscribeTo(InitPLC());
            }
        }
        catch (Exception ex)
        {
            _errorReceived.OnNext(ex);
        }
    }

    /// <summary>Disconnects this instance.</summary>
    public void Disconnect()
    {
        _plcCleanup?.Dispose();
        _plcCleanup = null;
        Connected = false;
        if (!IsPaused)
        {
            return;
        }

        IsPaused = false;
        _isPausedSubject.OnNext(IsPaused);
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

    /// <summary>Writes the specified variable.</summary>
    /// <param name="variable">The variable.</param>
    /// <param name="value">The value.</param>
    public void Write(string variable, object value) => Write(variable, value, null);

    /// <summary>Writes a variable with a correlation identifier.</summary>
    /// <param name="variable">The variable.</param>
    /// <param name="value">The value.</param>
    /// <param name="id">The correlation identifier.</param>
    public void Write(string variable, object value, string? id)
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

    /// <summary>Pauses the specified time.</summary>
    /// <param name="time">The time.</param>
    public void Pause(TimeSpan time)
    {
        var ownerCleanup = _cleanup;
        if (ownerCleanup?.IsDisposed != false)
        {
            _errorReceived.OnNext(new ObjectDisposedException(nameof(RxTcAdsClient)));
            return;
        }

        if (time.TotalMilliseconds <= 0)
        {
            return;
        }

        var cleanup = new CompositeDisposable();
        _ = cleanup.DisposeWith(ownerCleanup);
        IsPaused = true;
        _isPausedSubject.OnNext(IsPaused);
        _ = ObservableBridgeExtensions.SubscribeTo(Observable.Timer(time), _ =>
        {
            IsPaused = false;
            _isPausedSubject.OnNext(IsPaused);
            cleanup.Dispose();
        }).DisposeWith(cleanup);
    }

    /// <summary>Releases unmanaged and - optionally - managed resources.</summary>
    /// <param name="disposing">
    /// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only
    /// unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (_cleanup?.IsDisposed != false || !disposing)
        {
            return;
        }

        _plcCleanup?.Dispose();
        _codeGenerator?.Dispose();
        _codeGenerator = null;
        _cleanup?.Dispose();
        _code.Clear();
        ReadWriteHandleInfo.Clear();
        _typeInfo.Clear();
        WriteHandleInfo.Clear();
        _clientState.Dispose();
        _codeSubject.Dispose();
        _errorReceived.Dispose();
        _onWriteSubject.Dispose();
        _serviceStatus.Dispose();
        _readPLC.Dispose();
        _writePLC.Dispose();
        _dataReceived.Dispose();
        _initCompleteSubject.Dispose();
        _isPausedSubject.Dispose();
    }

    /// <summary>Queues a PLC array read request.</summary>
    /// <param name="handle">The ADS variable handle.</param>
    /// <param name="type">The value type.</param>
    /// <param name="length">The array length.</param>
    /// <param name="id">The correlation identifier.</param>
    private void ReadArrayHandle(uint? handle, Type type, int length, string? id) =>
        _readPLC.OnNext((handle, type, length, id));

    /// <summary>Queues a PLC scalar read request.</summary>
    /// <param name="handle">The ADS variable handle.</param>
    /// <param name="type">The value type.</param>
    /// <param name="id">The correlation identifier.</param>
    private void ReadHandle(uint? handle, Type type, string? id) =>
        _readPLC.OnNext((handle, type, -1, id));

    /// <summary>Queues a PLC write request.</summary>
    /// <param name="handle">The ADS variable handle.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="length">The array length.</param>
    /// <param name="id">The correlation identifier.</param>
    private void WriteHandle(uint? handle, object value, int length = -1, string? id = null) =>
        _writePLC.OnNext((handle, value, length, id));
}
