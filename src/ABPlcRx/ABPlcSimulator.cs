// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using SignalFactory = ReactiveUI.Primitives.Reactive.Signals.Signal;
#else
using SignalFactory = ReactiveUI.Primitives.Signals.Signal;
#endif
using IoT.DriverCore.Core;

#if REACTIVELIST_REACTIVE
namespace IoT.DriverCore.ABPlcRx.Reactive;
#else
namespace IoT.DriverCore.ABPlcRx;
#endif

/// <summary>Deterministic, in-memory Allen-Bradley controller simulator.</summary>
/// <remarks>
/// The simulator composes the same <see cref="ABPlcRx"/> and logical-tag pipeline as a native
/// controller. Device memory is shared by physical tag name, while every registered tag retains
/// its own staged handle buffer. Reads copy device memory to the handle and successful writes
/// commit the staged buffer back to device memory.
/// </remarks>
public sealed class ABPlcSimulator : IABPlcRx
{
    /// <summary>Default ControlLogix backplane path.</summary>
    private const string DefaultLogixPath = "1,0";

    /// <summary>Simulator-only gateway value used in generated tag URLs.</summary>
    private const string SimulatorGateway = "127.0.0.1";

    /// <summary>Default simulator scan interval.</summary>
    private static readonly TimeSpan DefaultScanInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>Default simulator operation timeout.</summary>
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(1);

    /// <summary>Publishes connection state changes.</summary>
    private readonly Signal<bool> _connectionChanged = new();

    /// <summary>The real controller facade composed over simulator-native storage.</summary>
    private readonly ABPlcRx _controller;

    /// <summary>The in-memory native adapter.</summary>
    private readonly SimulatedPlcTagNative _native;

    /// <summary>Tracks simulator disposal.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="ABPlcSimulator"/> class.</summary>
    public ABPlcSimulator()
        : this(PlcType.LGX)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ABPlcSimulator"/> class.</summary>
    /// <param name="plcType">The processor family to emulate.</param>
    public ABPlcSimulator(PlcType plcType)
        : this(
            plcType,
            DefaultScanInterval,
            DefaultTimeout,
            plcType == PlcType.LGX ? DefaultLogixPath : null,
            TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ABPlcSimulator"/> class.</summary>
    /// <param name="plcType">The processor family to emulate.</param>
    /// <param name="scanInterval">The tag scan interval.</param>
    /// <param name="timeout">The operation timeout.</param>
    /// <param name="path">The optional route path.</param>
    /// <param name="timeProvider">The time provider used for results and operation logs.</param>
    public ABPlcSimulator(
        PlcType plcType,
        TimeSpan scanInterval,
        TimeSpan timeout,
        string? path,
        TimeProvider timeProvider)
    {
        ArgumentExceptionHelper.ThrowIfNull(timeProvider, nameof(timeProvider));
        _native = new(timeProvider);
        _controller = new(
            plcType,
            SimulatorGateway,
            scanInterval,
            timeout,
            plcType == PlcType.LGX && string.IsNullOrEmpty(path) ? DefaultLogixPath : path,
            _native,
            timeProvider);
    }

    /// <inheritdoc/>
    public bool IsDisposed => _disposed;

    /// <summary>Gets a value indicating whether simulated communications are connected.</summary>
    public bool IsConnected => !_disposed && _native.IsConnected;

    /// <summary>Gets connection-state changes. The current state is emitted on subscription.</summary>
    public IObservable<bool> ConnectionChanged =>
        _connectionChanged.StartWith(IsConnected).DistinctUntilChanged();

    /// <summary>Gets a stable snapshot of recorded simulator operations.</summary>
    public IReadOnlyList<ABPlcSimulatorLogEntry> OperationLog => _native.OperationLog;

    /// <summary>Gets exact native-operation counts without relying on wall-clock timings.</summary>
    public ABPlcSimulatorOperationMetrics OperationMetrics => _native.GetOperationMetrics();

    /// <summary>Gets the latest operation status for every physical PLC tag.</summary>
    public IReadOnlyDictionary<string, int> TagStatuses => _native.TagStatuses;

    /// <summary>Gets the number of live tag handles.</summary>
    public int ActiveHandleCount => _native.ActiveHandleCount;

    /// <inheritdoc/>
    public IObservable<IPlcTag?> ObserveAll => _controller.ObserveAll;

    /// <inheritdoc/>
    public IObservableAsync<IPlcTag?> ObserveAllAsyncObservable => _controller.ObserveAllAsyncObservable;

    /// <inheritdoc/>
    public bool ScanEnabled
    {
        get => _controller.ScanEnabled;
        set => _controller.ScanEnabled = value;
    }

    /// <inheritdoc/>
    public bool AutoWriteValue
    {
        get => _controller.AutoWriteValue;
        set => _controller.AutoWriteValue = value;
    }

    /// <summary>Creates a logical-tag client over this simulator.</summary>
    /// <returns>A logical-tag client that does not require physical hardware.</returns>
    public ABLogicalTagClient CreateLogicalTagClient()
    {
        ThrowIfDisposed();
        return new ABLogicalTagClient(this);
    }

    /// <summary>Disconnects simulated communications with <see cref="PlcTagStatus.ErrBadConnection"/>.</summary>
    public void Disconnect() => Disconnect(PlcTagStatus.ErrBadConnection);

    /// <summary>Disconnects simulated communications.</summary>
    /// <param name="statusCode">The status returned by IO while disconnected.</param>
    public void Disconnect(int statusCode)
    {
        ThrowIfDisposed();
        if (!_native.Disconnect(statusCode))
        {
            return;
        }

        _connectionChanged.OnNext(false);
    }

    /// <summary>Reconnects simulated communications without losing device memory or registrations.</summary>
    public void Reconnect()
    {
        ThrowIfDisposed();
        if (!_native.Reconnect())
        {
            return;
        }

        _connectionChanged.OnNext(true);
    }

    /// <summary>Queues a libplctag-compatible result for a future matching operation.</summary>
    /// <param name="operation">The operation to fault.</param>
    /// <param name="statusCode">The status to return.</param>
    public void QueueFault(ABPlcSimulatorOperation operation, int statusCode) =>
        QueueFault(operation, statusCode, 1, null);

    /// <summary>Queues a repeated libplctag-compatible result for future matching operations.</summary>
    /// <param name="operation">The operation to fault.</param>
    /// <param name="statusCode">The status to return.</param>
    /// <param name="repeatCount">The number of matching operations affected.</param>
    public void QueueFault(ABPlcSimulatorOperation operation, int statusCode, int repeatCount) =>
        QueueFault(operation, statusCode, repeatCount, null);

    /// <summary>Queues a libplctag-compatible result for a future matching operation.</summary>
    /// <param name="operation">The operation to fault.</param>
    /// <param name="statusCode">The status to return.</param>
    /// <param name="repeatCount">The number of matching operations affected.</param>
    /// <param name="tagName">Optional physical tag-name filter.</param>
    public void QueueFault(
        ABPlcSimulatorOperation operation,
        int statusCode,
        int repeatCount,
        string? tagName)
    {
        ThrowIfDisposed();
        _native.QueueFault(operation, statusCode, repeatCount, tagName);
    }

    /// <summary>Clears all unconsumed scripted operation results.</summary>
    public void ClearFaults()
    {
        ThrowIfDisposed();
        _native.ClearFaults();
    }

    /// <summary>Clears the operation log and restarts its sequence at one.</summary>
    public void ClearOperationLog()
    {
        ThrowIfDisposed();
        _native.ClearOperationLog();
    }

    /// <summary>Seeds or updates raw device memory for a physical PLC tag.</summary>
    /// <param name="tagName">The physical PLC tag name.</param>
    /// <param name="value">The raw tag bytes.</param>
    public void SetTagBytes(string tagName, IReadOnlyCollection<byte> value)
    {
        ThrowIfDisposed();
        _native.SetTagBytes(tagName, value);
    }

    /// <summary>Gets a copy of raw device memory for a physical PLC tag.</summary>
    /// <param name="tagName">The physical PLC tag name.</param>
    /// <returns>A copy of the raw tag bytes.</returns>
    public byte[] GetTagBytes(string tagName)
    {
        ThrowIfDisposed();
        return _native.GetTagBytes(tagName);
    }

    /// <summary>Seeds or updates a supported scalar value in device memory.</summary>
    /// <typeparam name="T">The scalar PLC value type.</typeparam>
    /// <param name="tagName">The physical PLC tag name.</param>
    /// <param name="value">The value to encode.</param>
    public void SetTagValue<T>(string tagName, T value) =>
        SetTagBytes(tagName, ABPlcSimulatorValueCodec.Encode(value));

    /// <summary>Reads a supported scalar value directly from device memory.</summary>
    /// <typeparam name="T">The scalar PLC value type.</typeparam>
    /// <param name="tagName">The physical PLC tag name.</param>
    /// <param name="typeWitness">A type witness for the scalar value.</param>
    /// <returns>The decoded value.</returns>
    public T GetTagValue<T>(string tagName, T? typeWitness)
    {
        _ = typeWitness;
        return ABPlcSimulatorValueCodec.Decode<T>(GetTagBytes(tagName));
    }

    /// <inheritdoc/>
    public void AddUpdateTagItem<T>(string tagName, T? typeWitness) =>
        _controller.AddUpdateTagItem(tagName, typeWitness);

    /// <inheritdoc/>
    public void AddUpdateTagItem<T>(string variable, string tagName, T? typeWitness) =>
        _controller.AddUpdateTagItem(variable, tagName, typeWitness);

    /// <inheritdoc/>
    public void AddUpdateTagItem<T>(string variable, string tagName, string tagGroup, T? typeWitness) =>
        _controller.AddUpdateTagItem(variable, tagName, tagGroup, typeWitness);

    /// <inheritdoc/>
    public bool RemoveTagItem(string variable) => _controller.RemoveTagItem(variable);

    /// <inheritdoc/>
    public IObservable<T?> Observe<T>(string? variable, T? typeWitness, int bit) =>
        _controller.Observe(variable, typeWitness, bit);

    /// <inheritdoc/>
    public IObservableAsync<T?> ObserveAsyncObservable<T>(string? variable, T? typeWitness, int bit) =>
        _controller.ObserveAsyncObservable(variable, typeWitness, bit);

    /// <inheritdoc/>
    public IObservable<IReadOnlyDictionary<string, object?>> ObserveMany(params string[] variables) =>
        _controller.ObserveMany(variables);

    /// <inheritdoc/>
    public IObservableAsync<IReadOnlyDictionary<string, object?>> ObserveManyAsyncObservable(
        params string[] variables) =>
        _controller.ObserveManyAsyncObservable(variables);

    /// <inheritdoc/>
    public IObservable<IPlcTag> ObserveGroup(string groupName) => _controller.ObserveGroup(groupName);

    /// <inheritdoc/>
    public IObservableAsync<IPlcTag> ObserveGroupAsyncObservable(string groupName) =>
        _controller.ObserveGroupAsyncObservable(groupName);

    /// <inheritdoc/>
    public IObserver<T> CreateWriter<T>(string variable, T? typeWitness, int bit) =>
        _controller.CreateWriter(variable, typeWitness, bit);

    /// <inheritdoc/>
    public IObservable<T?> ObserveSampled<T>(
        string variable,
        TimeSpan sampleInterval,
        T? typeWitness,
        int bit,
        ISequencer? scheduler) =>
        _controller.ObserveSampled(variable, sampleInterval, typeWitness, bit, scheduler);

    /// <inheritdoc/>
    public IObservableAsync<T?> ObserveSampledAsyncObservable<T>(
        string variable,
        TimeSpan sampleInterval,
        T? typeWitness,
        int bit,
        ISequencer? scheduler) =>
        _controller.ObserveSampledAsyncObservable(variable, sampleInterval, typeWitness, bit, scheduler);

    /// <inheritdoc/>
    public IObservable<PlcTagResult> ObserveErrors() => _controller.ObserveErrors();

    /// <inheritdoc/>
    public IObservableAsync<PlcTagResult> ObserveErrorsAsyncObservable() =>
        _controller.ObserveErrorsAsyncObservable();

    /// <inheritdoc/>
    public T? GetValue<T>(string? variable, T? typeWitness, int bit) =>
        _controller.GetValue(variable, typeWitness, bit);

    /// <inheritdoc/>
    public void Value<T>(string? variable, T? value, int bit) =>
        _controller.Value(variable, value, bit);

    /// <inheritdoc/>
    public IEnumerable<PlcTagResult> Write() => _controller.Write();

    /// <inheritdoc/>
    public PlcTagResult? Write(string? variable) => _controller.Write(variable);

    /// <inheritdoc/>
    public IEnumerable<PlcTagResult> Read() => _controller.Read();

    /// <inheritdoc/>
    public PlcTagResult? Read(string? variable) => _controller.Read(variable);

    /// <inheritdoc/>
    public Task<IReadOnlyList<PlcTagResult>> ReadManyAsync(
        IReadOnlyCollection<string> variables,
        CancellationToken cancellationToken) =>
        _controller.ReadManyAsync(variables, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<PlcTagResult>> WriteManyAsync(
        IReadOnlyDictionary<string, object?> values,
        CancellationToken cancellationToken) =>
        _controller.WriteManyAsync(values, cancellationToken);

    /// <inheritdoc/>
    public Task<TagOperationResult<T>> ReadValueAsync<T>(
        string variable,
        T? typeWitness,
        int bit,
        CancellationToken cancellationToken) =>
        _controller.ReadValueAsync(variable, typeWitness, bit, cancellationToken);

    /// <inheritdoc/>
    public Task<TagOperationResult<T>> WriteValueAsync<T>(
        string variable,
        T value,
        int bit,
        CancellationToken cancellationToken) =>
        _controller.WriteValueAsync(variable, value, bit, cancellationToken);

    /// <inheritdoc/>
    public bool Ping(bool echo)
    {
        ThrowIfDisposed();
        if (echo)
        {
            Console.Out.WriteLine($"Simulator connected: {IsConnected}");
        }

        return IsConnected;
    }

    /// <inheritdoc/>
    public Task<bool> PingAsync(bool echo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Ping(echo));
    }

    /// <inheritdoc/>
    public IObservable<bool> ObservePing(TimeSpan interval, bool echo, ISequencer? scheduler) =>
        SignalFactory.Timer(TimeSpan.Zero, interval, scheduler ?? TaskPoolSequencer.Default)
            .Select(_ => Ping(echo))
            .DistinctUntilChanged();

    /// <inheritdoc/>
    public IObservableAsync<bool> ObservePingAsyncObservable(
        TimeSpan interval,
        bool echo,
        ISequencer? scheduler) =>
        ObservableAsyncBridgeExtensions.ToAsyncObservable(ObservePing(interval, echo, scheduler));

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _controller.Dispose();
        ((IDisposable)_native).Dispose();
        _connectionChanged.OnNext(false);
        _connectionChanged.OnCompleted();
        _connectionChanged.Dispose();
    }

    /// <summary>Throws when this simulator has been disposed.</summary>
    private void ThrowIfDisposed() =>
        _ = !_disposed ? true : throw new ObjectDisposedException(nameof(ABPlcSimulator));
}
