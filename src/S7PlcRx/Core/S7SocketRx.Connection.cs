// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Net.Sockets;
#if REACTIVE_SHIM
using S7PlcRx.Reactive.Enums;
#else
using S7PlcRx.Enums;
#endif

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Core;
#else
namespace S7PlcRx.Core;
#endif

/// <summary>Provides S7 socket connection functionality.</summary>
internal partial class S7SocketRx
{
    /// <summary>
    /// Initializes a new instance of the <see cref="S7SocketRx"/> class for communicating with a Siemens S7 PLC
    /// using the specified connection parameters.
    /// connection parameters.
    /// </summary>
    /// <remarks>This constructor configures the connection and initializes metrics reporting for the PLC
    /// communication session. The optimal data read length is set automatically based on the specified PLC
    /// type.</remarks>
    /// <param name="ip">The IP address of the target PLC. Cannot be null.</param>
    /// <param name="plcType">The type of PLC CPU to connect to.</param>
    /// <param name="rack">The rack number of the PLC to connect to.</param>
    /// <param name="slot">The slot number of the PLC to connect to.</param>
    /// <exception cref="ArgumentNullException">Thrown if ip is null.</exception>
    public S7SocketRx(string ip, CpuType plcType, short rack, short slot)
        : this(ip, plcType, rack, slot, TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="S7SocketRx"/> class for communicating with a Siemens S7 PLC
    /// using the specified connection parameters.
    /// connection parameters.
    /// </summary>
    /// <remarks>This constructor configures the connection and initializes metrics reporting for the PLC
    /// communication session. The optimal data read length is set automatically based on the specified PLC
    /// type.</remarks>
    /// <param name="ip">The IP address of the target PLC. Cannot be null.</param>
    /// <param name="plcType">The type of PLC CPU to connect to.</param>
    /// <param name="rack">The rack number of the PLC to connect to.</param>
    /// <param name="slot">The slot number of the PLC to connect to.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <exception cref="ArgumentNullException">Thrown if ip is null.</exception>
    public S7SocketRx(string ip, CpuType plcType, short rack, short slot, TimeProvider timeProvider)
    {
        IP = ip ?? throw new ArgumentNullException(nameof(ip));
        PLCType = plcType;
        Rack = rack;
        Slot = slot;
        _timeProvider = timeProvider;

        // Set optimized data read length based on PLC type capabilities
        DataReadLength = GetOptimalDataReadLength(plcType);

        // Initialize metrics reporting
        _metricsTimer = new(
            _ => ReportMetrics(),
            null,
            TimeSpan.FromSeconds(MetricsReportIntervalSeconds),
            TimeSpan.FromSeconds(MetricsReportIntervalSeconds));

        _disposable = Connect.Subscribe();
    }

    /// <summary>
    /// Gets an observable sequence that manages the connection to the device, emitting the connection status as a
    /// boolean value.
    /// </summary>
    /// <remarks>The observable attempts to establish and maintain a connection to the device, automatically
    /// retrying on failure with exponential backoff up to a maximum delay. The sequence emits <see langword="true"/>
    /// when the device is connected and available, and <see langword="false"/> otherwise. If the connection cannot be
    /// established or is lost, the observable signals an error. Subscribers receive updates on the connection status
    /// and are notified of errors such as device unavailability or socket exceptions. The connection is shared among
    /// all subscribers, and resources are released when there are no active subscriptions.</remarks>
    internal IObservable<bool> Connect =>
        Observable.Create<bool>(obs =>
        {
            if (_disposedValue)
            {
                obs.OnCompleted();
                return Disposable.Empty;
            }

            var dis = new CompositeDisposable();
            _initComplete = false;

            // Subject may have been disposed during teardown; recreate lazily.
            try
            {
                dis.Add(_socketExceptionSubject.Subscribe(ex =>
                {
                    if (ex is null)
                    {
                        return;
                    }

                    _metrics.RecordError();
                    LogError($"Socket exception: {ex.Message}", _timeProvider);
                    obs.OnError(ex);
                }));
            }
            catch (ObjectDisposedException)
            {
                _socketExceptionSubject = new();
                dis.Add(_socketExceptionSubject.Subscribe(ex =>
                {
                    if (ex is null)
                    {
                        return;
                    }

                    _metrics.RecordError();
                    LogError($"Socket exception: {ex.Message}", _timeProvider);
                    obs.OnError(ex);
                }));
            }

            dis.Add(IsConnected.Subscribe(
                deviceConnected =>
                {
                    var isAvail = _isAvailable == true;
                    obs.OnNext(isAvail && deviceConnected);
                    if (!_initComplete || deviceConnected)
                    {
                        return;
                    }

                    CloseSocketOptimized(_socket, _timeProvider);
                    _socket = null;
                    obs.OnError(new S7Exception(DeviceNotConnectedMessage));
                },
                ex =>
                {
                    CloseSocketOptimized(_socket, _timeProvider);
                    _socket = null;
                    obs.OnError(ex);
                }));

            dis.Add(IsAvailable.Subscribe(
                _ => ObserveAvailability(obs),
                ex =>
                {
                    CloseSocketOptimized(_socket, _timeProvider);
                    _socket = null;
                    obs.OnError(ex);
                }));

            return dis;
        })
        .RetryWithDelay(int.MaxValue, index =>
        {
            // Exponential backoff with cap: 1s, 2s, 4s, 8s, 16s, 30s, 30s...
            // Use bit shifting for better performance and prevent overflow
            var exponent = Math.Min(index, RetryBackoffMaximumExponent);
            var delayMilliseconds = Math.Min(
                InitialRetryDelayMilliseconds * (1 << exponent),
                MaxRetryDelayMilliseconds);

            // Log only first 5 attempts and then every 10th attempt to prevent log flooding
            if (index < InitialRetryAttemptsToLog || index % RetryLoggingInterval == 0)
            {
                LogWarning($"Connection attempt {index + 1} failed. Retrying in {delayMilliseconds}ms...", _timeProvider);
            }

            return TimeSpan.FromMilliseconds(delayMilliseconds);
        })
        .ReplayLastOnSubscribe(false);

    /// <summary>Gets the IP address associated with the current instance.</summary>
    internal string IP { get; }

    /// <summary>Gets the optimized data read length based on PLC type capabilities.</summary>
    internal ushort DataReadLength { get; private set; }

    /// <summary>Gets an observable sequence that indicates whether the resource is currently available.</summary>
    /// <remarks>The observable emits a value each time the availability status is checked, providing <see
    /// langword="true"/> if the resource is available; otherwise, <see langword="false"/>. The sequence uses a fast
    /// polling interval initially, then reduces frequency to minimize resource usage. Subscribers receive updates as
    /// the availability status changes. The sequence is shared among all subscribers and automatically manages its
    /// lifetime.</remarks>
    internal IObservable<bool> IsAvailable =>
        Observable.Create<bool>(obs =>
        {
            _isAvailable = null;
            var count = 0;

            // Fast probe (startup)
            SerialDisposable? timer = null;
            timer = new SerialDisposable
            {
                Disposable = Observable.Timer(
                    TimeSpan.Zero,
                    TimeSpan.FromMilliseconds(AvailabilityStartupProbeIntervalMilliseconds)).Subscribe(_ =>
                {
                    count++;
                    ProbeAvailabilityAndNotify(obs);

                    // After a few quick probes, back off to reduce ping noise.
                    if (count < AvailabilityStartupProbeCount)
                    {
                        return;
                    }

                    count = 0;
                    timer!.Disposable = Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(1)).Subscribe(
                        _ => ProbeAvailabilityAndNotify(obs));
                }),
            };

            return timer;
        }).OnErrorRetry().ReplayLastOnSubscribe(false);

    /// <summary>Gets whether the remote endpoint is connected.</summary>
    /// <remarks>The observable emits a value whenever the connection status changes. Subscribers receive <see
    /// langword="true"/> when the connection is established and <see langword="false"/> when it is lost. The sequence
    /// emits the current status immediately upon subscription and continues to provide updates as the connection state
    /// changes. The observable is shared among all subscribers and only emits distinct consecutive values.</remarks>
    internal IObservable<bool> IsConnected =>
        Observable.Create<bool>(obs =>
        {
            _isConnected = null;

            // Faster startup: check frequently until connected, then slow down.
            SerialDisposable? timer = null;
            timer = new SerialDisposable
            {
                Disposable = Observable.Timer(
                    TimeSpan.Zero,
                    TimeSpan.FromMilliseconds(ConnectionStartupProbeIntervalMilliseconds)).Subscribe(_ =>
                {
                    _isConnected = EvaluateConnectionStateWithHysteresis();
                    var isConnectedNow = _initComplete && _isConnected == true;
                    obs.OnNext(isConnectedNow);

                    if (!isConnectedNow)
                    {
                        return;
                    }

                    // Switch to steady-state checks.
                    timer!.Disposable = Observable.Timer(
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(1)).Subscribe(__ =>
                    {
                        _isConnected = EvaluateConnectionStateWithHysteresis();
                        obs.OnNext(_initComplete && _isConnected == true);
                    });
                }),
            };

            return timer;
        }).OnErrorRetry().ReplayLastOnSubscribe(false).DistinctUntilChanged();

    /// <summary>Gets the type of PLC (Programmable Logic Controller) associated with this instance.</summary>
    internal CpuType PLCType { get; }

    /// <summary>Gets the rack number associated with the device or connection.</summary>
    internal short Rack { get; }

    /// <summary>Gets the slot number associated with this instance.</summary>
    internal short Slot { get; }

    /// <summary>Gets an observable sequence that provides real-time connection metrics.</summary>
    /// <remarks>Subscribers receive updates whenever new connection metrics are available. The sequence
    /// completes when the underlying connection is closed or disposed.</remarks>
    internal IObservable<ConnectionMetrics> Metrics => _metricsSubject;

    /// <summary>Gets the duration used to assess recent operation availability.</summary>
    private static TimeSpan RecentOperationAvailabilityWindow =>
        TimeSpan.FromSeconds(AvailabilityFailureThreshold);

    /// <summary>Receives data from the connected device and writes it into the specified buffer.</summary>
    /// <remarks>If the device is not connected or initialization is incomplete, the method returns -1 and no
    /// data is written to the buffer. Exceptions encountered during the receive operation are reported through the
    /// socket exception subject. The method is not thread-safe.</remarks>
    /// <param name="tag">The tag associated with the data to be received. Can be null if tag information is not
    /// required.</param>
    /// <param name="buffer">The buffer to store the received data. Must not be null and must have sufficient space to
    /// accommodate the data.</param>
    /// <param name="size">The maximum number of bytes to receive. Must be greater than zero and not exceed the
    /// available space in the
    /// buffer starting at the specified offset.</param>
    /// <param name="offset">The zero-based position in the buffer at which to begin storing the received data. Must be
    /// non-negative
    /// and
    /// within the bounds of the buffer.</param>
    /// <returns>The number of bytes received and written to the buffer, or -1 if the operation fails or the device
    /// is not
    /// connected.</returns>
    internal int Receive(Tag tag, byte[] buffer, int size, int offset = 0)
        => ReceiveCore(tag, buffer, size, offset, traceOperation: true);

    /// <summary>Sends data to the connected device using the specified tag and buffer.</summary>
    /// <remarks>If the device is not connected or an error occurs during the send operation, the method
    /// returns -1 and notifies subscribers of the exception. The method does not throw exceptions for connection or
    /// send failures.</remarks>
    /// <param name="tag">The tag associated with the data being sent. Can be null if no tag information is
    /// required.</param>
    /// <param name="buffer">The buffer containing the data to send. Cannot be null.</param>
    /// <param name="size">The number of bytes to send from the buffer. Must be less than or equal to the length
    /// of the buffer and
    /// greater
    /// than zero.</param>
    /// <returns>The number of bytes sent if the operation is successful; otherwise, -1 if the device is not connected
    /// or an
    /// error occurs.</returns>
    internal int Send(Tag tag, byte[] buffer, int size)
    {
        if (!_initComplete)
        {
            return -1;
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();

            if (_socket?.Connected == true)
            {
                var sent = _socket.Send(buffer, size, SocketFlags.None);

                stopwatch.Stop();
                RecordSuccessfulOperation(stopwatch.Elapsed, sent, isReceive: false);

                if (tag is not null && Debugger.IsAttached)
                {
                    var result = sent == size ? Success : Failed;
                    Debug.WriteLine(
                        $"{_timeProvider.GetUtcNow().LocalDateTime} Wrote Tag: {tag.Name} value: {tag.Value} {result} " +
                        $"({sent}/{size} bytes, {stopwatch.ElapsedMilliseconds}ms)");
                }

                return sent;
            }

            RecordError();
            _socketExceptionSubject.OnNext(new S7Exception(DeviceNotConnectedMessage));
        }
        catch (Exception ex)
        {
            RecordError();
            _socketExceptionSubject.OnNext(ex);
        }

        return -1;
    }

    /// <summary>Releases resources used by this instance.</summary>
    void IDisposable.Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
