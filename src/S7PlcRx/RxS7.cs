// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
#if REACTIVE_SHIM
using S7PlcRx.Reactive.Core;
using S7PlcRx.Reactive.Enums;
#else
using S7PlcRx.Core;
using S7PlcRx.Enums;
#endif

using DateTime = System.DateTime;

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive;
#else
namespace S7PlcRx;
#endif

/// <summary>
/// Provides an observable, reactive interface for reading from and writing to Siemens S7 PLCs, supporting tag-based
/// access, status monitoring, and asynchronous operations.
/// </summary>
/// <remarks>The RxS7 class enables integration with Siemens S7 programmable logic controllers (PLCs) using a
/// tag-based model and reactive programming patterns. It exposes observables for PLC data, connection status, errors,
/// and operational metrics, allowing clients to subscribe to real-time updates. The class supports both synchronous and
/// asynchronous read/write operations, as well as advanced features such as watchdog monitoring and batch variable
/// access. Thread safety is maintained for concurrent operations. Dispose the instance when no longer needed to release
/// resources and terminate background operations.</remarks>
public partial class RxS7 : IRxS7
{
    /// <summary>Diagnostic name for the primary synchronization semaphore.</summary>
    private const string LockName = "Lock";

    /// <summary>Maximum attempts used by direct value reads.</summary>
    private const int ValueReadMaxAttempts = 3;

    /// <summary>Delay between direct value-read attempts.</summary>
    private const int ValueReadRetryDelayMilliseconds = 20;

    /// <summary>SZL identifier containing CPU identity data.</summary>
    private const int CpuInformationSzlId = 28;

    /// <summary>SZL identifier containing the CPU order code.</summary>
    private const int CpuOrderCodeSzlId = 17;

    /// <summary>Delay before retrying an SZL read.</summary>
    private const int SzlRetryDelayMilliseconds = 10;

    /// <summary>Minimum CPU-information payload length.</summary>
    private const int CpuInformationMinimumLength = 204;

    /// <summary>Minimum order-code payload length.</summary>
    private const int CpuOrderCodeMinimumLength = 25;

    /// <summary>Offset of the AS name in the CPU-information payload.</summary>
    private const int CpuAsNameOffset = 2;

    /// <summary>Length of the AS name in the CPU-information payload.</summary>
    private const int CpuAsNameLength = 24;

    /// <summary>Offset of the module name in the CPU-information payload.</summary>
    private const int CpuModuleNameOffset = 36;

    /// <summary>Length of the module name in the CPU-information payload.</summary>
    private const int CpuModuleNameLength = 24;

    /// <summary>Offset of the copyright text in the CPU-information payload.</summary>
    private const int CpuCopyrightOffset = 104;

    /// <summary>Length of the copyright text in the CPU-information payload.</summary>
    private const int CpuCopyrightLength = 26;

    /// <summary>Offset of the serial number in the CPU-information payload.</summary>
    private const int CpuSerialNumberOffset = 138;

    /// <summary>Length of the serial number in the CPU-information payload.</summary>
    private const int CpuSerialNumberLength = 24;

    /// <summary>Offset of the module type in the CPU-information payload.</summary>
    private const int CpuModuleTypeOffset = 172;

    /// <summary>Length of the module type in the CPU-information payload.</summary>
    private const int CpuModuleTypeLength = 32;

    /// <summary>Offset of the order code in its SZL payload.</summary>
    private const int CpuOrderCodeOffset = 2;

    /// <summary>Length of the order code in its SZL payload.</summary>
    private const int CpuOrderCodeLength = 20;

    /// <summary>Distance from the payload end to the first version component.</summary>
    private const int CpuVersionMajorDistanceFromEnd = 3;

    /// <summary>Distance from the payload end to the second version component.</summary>
    private const int CpuVersionMinorDistanceFromEnd = 2;

    /// <summary>Additional receive capacity reserved for protocol framing.</summary>
    private const int ProtocolReceivePadding = 256;

    /// <summary>Transport-size code used for byte writes.</summary>
    private const byte ByteTransportSize = 2;

    /// <summary>Minimum number of components in a data-block address.</summary>
    private const int DataBlockAddressComponentCount = 2;

    /// <summary>Length of the DB address prefix.</summary>
    private const int DataBlockPrefixLength = 2;

    /// <summary>Length of an S7 address type code such as DBB.</summary>
    private const int AddressTypeCodeLength = 3;

    /// <summary>Index containing a bit offset in a split DBX address.</summary>
    private const int BitAddressComponentIndex = 2;

    /// <summary>Length of an area address code such as EB or MW.</summary>
    private const int AreaAddressCodeLength = 2;

    /// <summary>Number of bits represented by one byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>Size of one S7 read request item.</summary>
    private const int ReadRequestItemSize = 12;

    /// <summary>Transport-size code used by standard read request items.</summary>
    private const byte StandardReadTransportSize = 2;

    /// <summary>Size of the fixed read request header.</summary>
    private const int ReadRequestHeaderSize = 19;

    /// <summary>Fixed size preceding read request items in the parameter block.</summary>
    private const int ReadParameterBaseSize = 2;

    /// <summary>Number of bytes occupied by a word-like value.</summary>
    private const int WordByteLength = 2;

    /// <summary>Number of bytes occupied by a double-word value.</summary>
    private const int DoubleWordByteLength = 4;

    /// <summary>Number of bytes occupied by a long-real value.</summary>
    private const int LongRealByteLength = 8;

    /// <summary>Fixed size of an S7 write package before its payload.</summary>
    private const int WriteRequestBaseSize = 35;

    /// <summary>Size of the write request parameter block.</summary>
    private const int WriteParameterSize = 14;

    /// <summary>Fixed bytes added to the write payload length.</summary>
    private const int WriteDataLengthOverhead = 4;

    /// <summary>Receive buffer size used for write acknowledgements.</summary>
    private const int WriteResponseBufferSize = 1024;

    /// <summary>Offset containing the S7 response return code.</summary>
    private const int ResponseReturnCodeOffset = 21;

    /// <summary>Total size of a single-item S7 read package.</summary>
    private const int SingleReadPackageSize = 31;

    /// <summary>Minimum valid size of an S7 read response.</summary>
    private const int ReadResponseMinimumSize = 25;

    /// <summary>Offset at which read response data begins.</summary>
    private const int ReadResponseDataOffset = 25;

    /// <summary>Protocol overhead reserved when calculating a safe read chunk.</summary>
    private const int ReadChunkProtocolOverhead = 32;

    /// <summary>Maximum byte-array payload requested in one read.</summary>
    private const int MaximumByteArrayReadChunk = 480;

    /// <summary>Divisor used to halve a failed read chunk.</summary>
    private const int ReadChunkReductionDivisor = 2;

    /// <summary>Maximum attempts made for one read chunk.</summary>
    private const int ReadChunkMaxAttempts = 3;

    /// <summary>Grace period after connecting before polling begins.</summary>
    private const int ConnectionPollingGraceMilliseconds = 250;

    /// <summary>Delay while waiting for the PLC connection.</summary>
    private const int ConnectionWaitDelayMilliseconds = 10;

    /// <summary>Maximum payload written in one S7 request.</summary>
    private const int MaximumWriteChunkSize = 200;

    /// <summary>Fixed prefix of an S7 read request item.</summary>
    private static readonly byte[] ReadRequestItemPrefix = [18, 10, 16];

    /// <summary>TPKT prefix shared by S7 read and write requests.</summary>
    private static readonly byte[] TpktHeaderPrefix = [3, 0, 0];

    /// <summary>Fixed body of an S7 read request header.</summary>
    private static readonly byte[] ReadRequestHeaderBody = [2, 240, 128, 50, 1, 0, 0, 0, 0];

    /// <summary>Function bytes terminating an S7 read request header.</summary>
    private static readonly byte[] ReadRequestFunction = [0, 0, 4];

    /// <summary>Fixed body of an S7 write request header.</summary>
    private static readonly byte[] WriteRequestHeaderBody = [2, 240, 128, 50, 1, 0, 0];

    /// <summary>Function and item prefix for an S7 write request.</summary>
    private static readonly byte[] WriteRequestItemPrefix = [5, 1, 18, 10, 16, 2];

    /// <summary>Data transport prefix for an S7 write request.</summary>
    private static readonly byte[] WriteDataTransportPrefix = [0, 4];

    /// <summary>Stores the s oc ke t r x used by this instance.</summary>
    private readonly S7SocketRx _socketRx;

    /// <summary>Stores the d at ar e a d used by this instance.</summary>
    private readonly Signal<Tag?> _dataRead = new();

    /// <summary>Stores the d is po sa bl e s used by this instance.</summary>
    private readonly CompositeDisposable _disposables = [];

    /// <summary>Stores the l as te rr o r used by this instance.</summary>
    private readonly Signal<string> _lastError = new();

    /// <summary>Stores the l as te rr or co d e used by this instance.</summary>
    private readonly Signal<ErrorCode> _lastErrorCode = new();

    /// <summary>Stores the p lc re qu es ts ub je c t used by this instance.</summary>
    private readonly Signal<PLCRequest> _plcRequestSubject = new();

    /// <summary>Stores the s ta t u s used by this instance.</summary>
    private readonly Signal<string> _status = new();

    /// <summary>Stores the r ea dt i m e used by this instance.</summary>
    private readonly Signal<long> _readTime = new();

    /// <summary>Stores the l o c k used by this instance.</summary>
    private readonly SemaphoreSlim _lock = new(1);

    /// <summary>Stores the l oc kt ag li s t used by this instance.</summary>
    private readonly SemaphoreSlim _lockTagList = new(1);

    /// <summary>Stores the lock used to serialize direct socket interactions.</summary>
    private readonly Lock _socketLock = new();

    /// <summary>Stores the s to pw at c h used by this instance.</summary>
    private readonly Stopwatch _stopwatch = new();

    /// <summary>Stores the p au s e d used by this instance.</summary>
    private readonly Signal<bool> _paused = new();

    /// <summary>Stores the time provider used by this instance.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>Stores the l as tc on ne ct ed at u t c used by this instance.</summary>
    private DateTime _lastConnectedAtUtc = DateTime.MinValue;

    /// <summary>Stores the p au s e used by this instance.</summary>
    private bool _pause;

    /// <summary>Initializes a new instance of the <see cref="RxS7"/> class from composed connection settings.</summary>
    /// <param name="options">The composed PLC connection settings.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="options"/> or its connection settings are null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the configured watchdog address is not a DBW address.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the configured watchdog interval is less than one second.
    /// </exception>
    public RxS7(RxS7Options options)
        : this(options, TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RxS7"/> class from composed connection settings.</summary>
    /// <param name="options">The composed PLC connection settings.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="options"/> or its connection settings are null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the configured watchdog address is not a DBW address.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the configured watchdog interval is less than one second.
    /// </exception>
    public RxS7(RxS7Options options, TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var connection = options.Connection ??
            throw new ArgumentNullException(nameof(options), "Connection options cannot be null.");
        var watchdog = options.Watchdog;
        if (watchdog is not null)
        {
            if (!watchdog.Address.Contains("DBW", StringComparison.Ordinal))
            {
                throw new ArgumentException("WatchDogAddress must be a DBW address.", nameof(options));
            }

            if (watchdog.IntervalSeconds < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "WatchDogInterval must be greater than 0.");
            }
        }

        PLCType = connection.CpuType;
        IP = connection.IpAddress;
        Rack = connection.Rack;
        Slot = connection.Slot;

        // Create an observable socket
        _socketRx = new(IP, PLCType, Rack, Slot, _timeProvider);

        IsConnected = _socketRx.IsConnected;

        // Get the PLC connection status
        _disposables.Add(IsConnected.Subscribe(x =>
        {
            IsConnectedValue = x;
            if (x)
            {
                _lastConnectedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
            }

            _status.OnNext($"{_timeProvider.GetUtcNow().LocalDateTime} - PLC Connected Status: {x}");
        }));

        if (watchdog is not null)
        {
            WatchDogAddress = watchdog.Address;
            WatchDogWritingTime = watchdog.IntervalSeconds;
            WatchDogValueToWrite = watchdog.ValueToWrite;
            _disposables.Add(WatchDogObservable().Subscribe());
        }

        _disposables.Add(TagReaderObservable(options.Polling.IntervalMilliseconds).Subscribe());

        _disposables.Add(_plcRequestSubject.Subscribe(request =>
        {
            if (request.Request == PlcRequestType.Write)
            {
                _ = WriteString(request.Tag);
            }

            GetTagValue(request.Tag);
            _dataRead.OnNext(request.Tag);
        }));
    }

    /// <summary>Gets an observable sequence that emits all tag updates as they occur.</summary>
    /// <remarks>Each observer receives tag updates in real time as they are published. The sequence is shared
    /// among all subscribers, and subscriptions are managed automatically. Observers may receive null values if a tag
    /// is removed or unavailable.</remarks>
    public IObservable<Tag?> ObserveAll =>
        _dataRead
            .Publish()
            .RefCount();

    /// <summary>Gets an observable sequence that indicates whether the operation is currently paused.</summary>
    /// <remarks>The returned observable emits a value of <see langword="true"/> when the operation enters a
    /// paused state, and <see langword="false"/> when it resumes. Subscribers receive updates only when the paused
    /// state changes. The sequence is shared among all subscribers.</remarks>
    public IObservable<bool> IsPaused => _paused.DistinctUntilChanged().Publish().RefCount();

    /// <summary>Gets the IP address associated with the current instance.</summary>
    public string IP { get; }

    /// <summary>Gets an observable sequence that indicates whether the connection is currently established.</summary>
    /// <remarks>Subscribers receive updates whenever the connection state changes. The sequence emits <see
    /// langword="true"/> when connected and <see langword="false"/> when disconnected.</remarks>
    public IObservable<bool> IsConnected { get; }

    /// <summary>Gets a value indicating whether the connection is currently established.</summary>
    public bool IsConnectedValue { get; private set; }

    /// <summary>Gets an observable sequence of the component's most recent error messages.</summary>
    /// <remarks>Subscribers receive error messages as they occur. The sequence is shared among all
    /// subscribers, and each subscriber receives messages from the point of subscription onward.</remarks>
    public IObservable<string> LastError => _lastError.Publish().RefCount();

    /// <summary>Gets an observable sequence that emits the most recent error code reported by the system.</summary>
    /// <remarks>Subscribers receive updates whenever a new error code is reported. The sequence is shared
    /// among all subscribers and only remains active while there is at least one active subscription.</remarks>
    public IObservable<ErrorCode> LastErrorCode => _lastErrorCode.Publish().RefCount();

    /// <summary>Gets the type of PLC (Programmable Logic Controller) associated with this instance.</summary>
    public CpuType PLCType { get; }

    /// <summary>Gets the rack number associated with the device or component.</summary>
    public short Rack { get; }

    /// <summary>Gets or sets a value indicating whether WatchDog writing output is displayed.</summary>
    public bool ShowWatchDogWriting { get; set; }

    /// <summary>Gets the slot number associated with this instance.</summary>
    public short Slot { get; }

    /// <summary>Gets an observable sequence that provides status updates as strings.</summary>
    /// <remarks>Subscribers receive status updates as they occur. The observable sequence is shared among all
    /// subscribers, and subscriptions are managed automatically. Status updates are pushed to observers in real
    /// time.</remarks>
    public IObservable<string> Status => _status.Publish().RefCount();

    /// <summary>Gets the collection of tags associated with the current instance.</summary>
    public Tags TagList { get; } = [];

    /// <summary>Gets the network address of the WatchDog service, if configured.</summary>
    public string? WatchDogAddress { get; }

    /// <summary>Gets or sets the value to be written to the watchdog timer.</summary>
    public ushort WatchDogValueToWrite { get; set; } = S7WatchdogOptions.DefaultValueToWrite;

    /// <summary>Gets the interval, in seconds, that the watchdog uses when writing status updates.</summary>
    public int WatchDogWritingTime { get; } = S7WatchdogOptions.DefaultIntervalSeconds;

    /// <summary>Gets a value indicating whether gets a value that indicates whether the object is disposed.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>Gets an observable sequence that emits each read operation's duration in ticks.</summary>
    /// <remarks>The observable sequence is shared among all subscribers. Each subscriber receives
    /// notifications when a read operation is performed, with the value representing the read time in ticks. The
    /// sequence completes when the underlying source completes.</remarks>
    public IObservable<long> ReadTime => _readTime.Publish().RefCount();

    /// <summary>
    /// Returns an observable sequence that emits the current and future values of the specified variable, cast to the
    /// specified type.
    /// </summary>
    /// <remarks>The returned observable is hot and shared among all subscribers. Subscribers receive the most
    /// recent value and all subsequent updates. If the variable does not exist or its value cannot be cast to the
    /// specified type, the observable emits null.</remarks>
    /// <typeparam name="T">The type to which the variable's value is cast in the observable sequence.</typeparam>
    /// <param name="tag">The typed logical tag key to observe.</param>
    /// <returns>An observable sequence of values of type T, or null if the variable value cannot be cast to
    /// T. The sequence emits a new value each time the variable changes.</returns>
    public IObservable<T?> Observe<T>(LogicalTagKey<T> tag)
    {
        Guard.NotNull(tag, nameof(tag));
        return ObserveAll
            .Where(t => TagValueIsValid<T>(t, tag.Name))
            .Select(t => (T?)t?.Value)
            .OnErrorRetry()
            .Publish()
            .RefCount();
    }

    /// <summary>Asynchronously retrieves the specified variable's value cast to the requested type.</summary>
    /// <remarks>If the variable's type is not known, it is set to the requested type <typeparamref name="T"/>
    /// before retrieving the value. The method waits for an internal pause condition to be met before
    /// proceeding.</remarks>
    /// <typeparam name="T">The type to which the variable's value is cast and returned.</typeparam>
    /// <param name="tag">The typed logical tag key to read.</param>
    /// <returns>A value of type <typeparamref name="T"/> if the variable exists and can be cast to the specified type;
    /// otherwise, <see langword="default"/>.</returns>
    public async Task<T?> ReadAsync<T>(LogicalTagKey<T> tag)
    {
        Guard.NotNull(tag, nameof(tag));
        _pause = true;
        try
        {
            _ = await _paused.Where(x => x).FirstAsync();
            var storedTag = TagList[tag.Name];
            if (storedTag?.Type == typeof(object))
            {
                storedTag.Type = typeof(T);
            }

            for (var attempt = 0; attempt < ValueReadMaxAttempts; attempt++)
            {
                GetTagValue(storedTag);
                if (TagValueIsValid<T>(storedTag))
                {
                    return (T?)storedTag?.Value;
                }

                await Task.Delay(ValueReadRetryDelayMilliseconds).ConfigureAwait(false);
            }

            return default;
        }
        finally
        {
            _pause = false;
        }
    }

    /// <summary>Sets a variable value when it exists and the value is compatible with its type.</summary>
    /// <remarks>If the variable does not exist or the value is null, this method does nothing. The value is
    /// only set if its type matches the variable's expected type or if the type parameter is object.</remarks>
    /// <typeparam name="T">The type of the value to assign to the variable.</typeparam>
    /// <param name="variable">The name of the variable whose value is to be set. Cannot be null.</param>
    /// <param name="value">The value to assign to the variable. Must be compatible with the variable's type.</param>
    public void Value<T>(string? variable, T? value)
    {
        if (variable is null)
        {
            return;
        }

        var tag = TagList[variable];
        if (tag is null || value is null || (typeof(object) != typeof(T) && tag.Type != typeof(T)))
        {
            return;
        }

        tag.NewValue = value;
        QueueWrite(tag);
    }
}
