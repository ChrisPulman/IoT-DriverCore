// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.Serial.Reactive;
#else
namespace IoT.DriverCore.Serial;
#endif

/// <summary>Serial Port Rx.</summary>
/// <seealso cref="ISerialPortRx"/>
[DebuggerDisplay("PortName = {PortName}, IsOpen = {IsOpen}")]
public partial class SerialPortRx : ISerialPortRx, IReceiveBatchPortRx
{
    /// <summary>The initial capacity used by line parsing buffers.</summary>
    private const int DefaultLineBuilderCapacity = 256;

    /// <summary>The default serial receive polling interval in milliseconds.</summary>
    private const int DefaultReceivePollingIntervalMilliseconds = 10;

    /// <summary>The default port-name polling interval in milliseconds.</summary>
    private const int DefaultPortNamePollingIntervalMilliseconds = 500;

    /// <summary>The initial capacity used by delimiter parsing buffers.</summary>
    private const int DelimiterBuilderCapacity = 128;

    /// <summary>The delay used after opening the underlying serial port.</summary>
    private const int OpenStabilizationDelayMilliseconds = 50;

    /// <summary>The length of the optimized carriage-return/line-feed sequence.</summary>
    private const int TwoCharacterNewLineLength = 2;

    /// <summary>The error message published when the serial port closes during a read.</summary>
    private const string SerialPortNotOpenMessage = "Serial port is not open.";

    /// <summary>Publishes the current open state.</summary>
    private readonly ReplaySignal<bool> _isOpenValue = new(1);

    /// <summary>Publishes received characters.</summary>
    private readonly ReplaySignal<char> _dataReceived = new(0);

    /// <summary>Publishes received bytes.</summary>
    private readonly ReplaySignal<byte> _dataReceivedBytes = new(0);

    /// <summary>Publishes raw byte batches received from the serial port.</summary>
    private readonly ReplaySignal<byte[]> _dataReceivedBatches = new(0);

    /// <summary>Publishes serial port errors.</summary>
    private readonly ReplaySignal<Exception> _errors = new(0);

    /// <summary>Publishes byte-array write requests.</summary>
    private readonly ReplaySignal<(byte[] ByteArray, int Offset, int Count)> _writeByte = new(0);

    /// <summary>Publishes character-array write requests.</summary>
    private readonly ReplaySignal<(char[] CharArray, int Offset, int Count)> _writeChar = new(0);

    /// <summary>Publishes string write requests.</summary>
    private readonly ReplaySignal<string> _writeString = new(0);

    /// <summary>Publishes string-line write requests.</summary>
    private readonly ReplaySignal<string> _writeStringLine = new(0);

    /// <summary>Publishes discard-in-buffer requests.</summary>
    private readonly ReplaySignal<Unit> _discardInBuffer = new(0);

    /// <summary>Publishes discard-out-buffer requests.</summary>
    private readonly ReplaySignal<Unit> _discardOutBuffer = new(0);

    /// <summary>Publishes read requests.</summary>
    private readonly ReplaySignal<(byte[] Buffer, int Offset, int Count)> _readBytes = new(0);

    /// <summary>Publishes completed read lengths.</summary>
    private readonly ReplaySignal<int> _bytesRead = new(0);

    /// <summary>Publishes bytes received by read operations.</summary>
    private readonly ReplaySignal<int> _bytesReceived = new(0);

    /// <summary>Publishes serial pin change events.</summary>
    private readonly ReplaySignal<SerialPinChangedEventArgs> _pinChanged = new(0);

    /// <summary>Serializes read access.</summary>
    private readonly SemaphoreSlim _readLock = new(1, 1);

    /// <summary>Serializes automatic serial event callbacks that share a receive buffer.</summary>
#if NET9_0_OR_GREATER
    private readonly Lock _autoReceiveLock = new();
#else
    private readonly object _autoReceiveLock = new();
#endif

    /// <summary>Synchronizes lazy observable cache initialization.</summary>
#if NET9_0_OR_GREATER
    private readonly Lock _observableCacheLock = new();
#else
    private readonly object _observableCacheLock = new();
#endif

    /// <summary>The optional connection factory used by deterministic in-memory ports.</summary>
    private readonly Func<SerialPortRx, ISerialPortConnection>? _connectionFactory;

    /// <summary>The active port subscription collection.</summary>
    private CompositeDisposable _disposablePort = [];

    /// <summary>The cached data-received observable.</summary>
    private IObservable<char>? _cachedDataReceived;

    /// <summary>The cached byte-received observable.</summary>
    private IObservable<byte>? _cachedDataReceivedBytes;

    /// <summary>The cached raw-byte batch observable.</summary>
    private IObservable<byte[]>? _cachedDataReceivedBatches;

    /// <summary>The cached bytes-read observable.</summary>
    private IObservable<int>? _cachedBytesReceived;

    /// <summary>The cached error observable.</summary>
    private IObservable<Exception>? _cachedErrorReceived;

    /// <summary>The cached open-state observable.</summary>
    private IObservable<bool>? _cachedIsOpenObservable;

    /// <summary>The cached line observable.</summary>
    private IObservable<string>? _lines;

    /// <summary>The cached async data-received observable.</summary>
    private IObservableAsync<char>? _cachedDataReceivedAsync;

    /// <summary>The cached async byte-received observable.</summary>
    private IObservableAsync<byte>? _cachedDataReceivedBytesAsync;

    /// <summary>The cached async bytes-read observable.</summary>
    private IObservableAsync<int>? _cachedBytesReceivedAsync;

    /// <summary>The cached async error observable.</summary>
    private IObservableAsync<Exception>? _cachedErrorReceivedAsync;

    /// <summary>The cached async open-state observable.</summary>
    private IObservableAsync<bool>? _cachedIsOpenObservableAsync;

    /// <summary>The cached async line observable.</summary>
    private IObservableAsync<string>? _linesAsync;
#if HasWindows
    /// <summary>The cached serial pin change observable.</summary>
    private IObservable<SerialPinChangedEventArgs>? _cachedPinChanged;

    /// <summary>The cached async serial pin change observable.</summary>
    private IObservableAsync<SerialPinChangedEventArgs>? _cachedPinChangedAsync;
#endif

    /// <summary>The wrapped serial port connection.</summary>
    private ISerialPortConnection? _serialPort;

    /// <summary>The cached break-state setting.</summary>
    private bool _breakState;

    /// <summary>The cached discard-null setting.</summary>
    private bool _discardNull;

    /// <summary>The cached DTR-enable setting.</summary>
    private bool _dtrEnable;

    /// <summary>The cached parity replacement byte.</summary>
    private byte _parityReplace = 63;

    /// <summary>The cached read buffer size.</summary>
    private int _readBufferSize = 4096;

    /// <summary>The cached received-bytes threshold.</summary>
    private int _receivedBytesThreshold = 1;

    /// <summary>The cached RTS-enable setting.</summary>
    private bool _rtsEnable;

    /// <summary>The cached write buffer size.</summary>
    private int _writeBufferSize = 2048;

    /// <summary>Initializes a new instance of the <see cref="SerialPortRx"/> class.</summary>
    /// <param name="port">The port.</param>
    /// <param name="baudRate">The baud rate.</param>
    /// <param name="dataBits">The data bits.</param>
    /// <param name="parity">The parity.</param>
    /// <param name="stopBits">The stop bits.</param>
    /// <param name="handshake">The handshake.</param>
    public SerialPortRx(string port, int baudRate, int dataBits, Parity parity, StopBits stopBits, Handshake handshake)
    {
        PortName = port;
        BaudRate = baudRate;
        DataBits = dataBits;
        Parity = parity;
        StopBits = stopBits;
        Handshake = handshake;
    }

    /// <summary>Initializes a new instance of the <see cref="SerialPortRx"/> class.</summary>
    /// <param name="port">The port.</param>
    /// <param name="baudRate">The baud rate.</param>
    /// <param name="dataBits">The data bits.</param>
    /// <param name="parity">The parity.</param>
    /// <param name="stopBits">The stop bits.</param>
    public SerialPortRx(string port, int baudRate, int dataBits, Parity parity, StopBits stopBits)
    {
        PortName = port;
        BaudRate = baudRate;
        DataBits = dataBits;
        Parity = parity;
        StopBits = stopBits;
    }

    /// <summary>Initializes a new instance of the <see cref="SerialPortRx"/> class.</summary>
    /// <param name="port">The port.</param>
    /// <param name="baudRate">The baud rate.</param>
    /// <param name="dataBits">The data bits.</param>
    /// <param name="parity">The parity.</param>
    public SerialPortRx(string port, int baudRate, int dataBits, Parity parity)
    {
        PortName = port;
        BaudRate = baudRate;
        DataBits = dataBits;
        Parity = parity;
    }

    /// <summary>Initializes a new instance of the <see cref="SerialPortRx"/> class.</summary>
    /// <param name="port">The port.</param>
    /// <param name="baudRate">The baud rate.</param>
    /// <param name="dataBits">The data bits.</param>
    public SerialPortRx(string port, int baudRate, int dataBits)
    {
        PortName = port;
        BaudRate = baudRate;
        DataBits = dataBits;
    }

    /// <summary>Initializes a new instance of the <see cref="SerialPortRx"/> class.</summary>
    /// <param name="port">The port.</param>
    /// <param name="baudRate">The baud rate.</param>
    public SerialPortRx(string port, int baudRate)
    {
        PortName = port;
        BaudRate = baudRate;
    }

    /// <summary>Initializes a new instance of the <see cref="SerialPortRx" /> class.</summary>
    /// <param name="port">The port.</param>
    public SerialPortRx(string port) => PortName = port;

    /// <summary>Initializes a new instance of the <see cref="SerialPortRx"/> class.</summary>
    public SerialPortRx()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SerialPortRx"/> class.</summary>
    /// <param name="connectionFactory">Creates a fresh connection for each open operation.</param>
    internal SerialPortRx(Func<SerialPortRx, ISerialPortConnection> connectionFactory)
    {
        ArgumentGuard.ThrowIfNull(connectionFactory, nameof(connectionFactory));
        _connectionFactory = connectionFactory;
    }

    /// <summary>Gets indicates that no timeout should occur.</summary>
    [Browsable(true)]
    [DefaultValue(-1)]
    [MonitoringDescription("InfiniteTimeout")]
    public int InfiniteTimeout => SerialPort.InfiniteTimeout;

    /// <summary>Gets or sets the baud rate.</summary>
    /// <value>The baud rate.</value>
    [Browsable(true)]
    [DefaultValue(9600)]
    [MonitoringDescription("BaudRate")]
    public int BaudRate { get; set; } = 9600;

    /// <summary>Gets or sets the data bits.</summary>
    /// <value>The data bits.</value>
    [Browsable(true)]
    [DefaultValue(8)]
    [MonitoringDescription("DataBits")]
    public int DataBits { get; set; } = 8;

    /// <summary>Gets the data received as characters.</summary>
    /// <value>The data received.</value>
    public IObservable<char> DataReceived => GetOrCreateCachedObservable(ref _cachedDataReceived, _dataReceived);

    /// <summary>Gets the data received as characters via an async observable.</summary>
    /// <value>The data received.</value>
    public IObservableAsync<char> DataReceivedAsync =>
        GetOrCreateCachedAsyncObservable(ref _cachedDataReceivedAsync, DataReceived);

    /// <summary>Gets the raw bytes received from the serial port.</summary>
    /// <value>The raw bytes received.</value>
    public IObservable<byte> DataReceivedBytes =>
        GetOrCreateCachedObservable(ref _cachedDataReceivedBytes, _dataReceivedBytes);

    /// <summary>Gets raw byte batches received from the serial port.</summary>
    /// <value>The raw byte batches received.</value>
    public IObservable<byte[]> DataReceivedBatches =>
        GetOrCreateCachedObservable(ref _cachedDataReceivedBatches, _dataReceivedBatches);

    /// <summary>Gets the raw bytes received from the serial port via an async observable.</summary>
    /// <value>The raw bytes received.</value>
    public IObservableAsync<byte> DataReceivedBytesAsync =>
        GetOrCreateCachedAsyncObservable(ref _cachedDataReceivedBytesAsync, DataReceivedBytes);

    /// <summary>Gets the data received when executing ReadAsync.</summary>
    /// <value>The data received.</value>
    public IObservable<int> BytesReceived => GetOrCreateCachedObservable(ref _cachedBytesReceived, _bytesReceived);

    /// <summary>Gets the data received when executing ReadAsync via an async observable.</summary>
    /// <value>The data received.</value>
    public IObservableAsync<int> BytesReceivedAsync =>
        GetOrCreateCachedAsyncObservable(ref _cachedBytesReceivedAsync, BytesReceived);

    /// <summary>Gets or sets the encoding.</summary>
    /// <value>The encoding.</value>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [MonitoringDescription("Encoding")]
    public Encoding Encoding { get; set; } = Encoding.ASCII;

    /// <summary>Gets the error received.</summary>
    /// <value>The error received.</value>
    public IObservable<Exception> ErrorReceived => GetOrCreateCachedObservable(
        ref _cachedErrorReceived,
        CreateDistinctErrorObservable);

    /// <summary>Gets the error received via an async observable.</summary>
    /// <value>The error received.</value>
    public IObservableAsync<Exception> ErrorReceivedAsync =>
        GetOrCreateCachedAsyncObservable(ref _cachedErrorReceivedAsync, ErrorReceived);

    /// <summary>Gets or sets the handshake.</summary>
    /// <value>The handshake.</value>
    [Browsable(true)]
    [DefaultValue(Handshake.None)]
    [MonitoringDescription("Handshake")]
    public Handshake Handshake { get; set; } = Handshake.None;

    /// <summary>Gets a value indicating whether this instance is disposed.</summary>
    /// <value><c>true</c> if this instance is disposed; otherwise, <c>false</c>.</value>
    [Browsable(true)]
    [MonitoringDescription("IsDisposed")]
    public bool IsDisposed { get; private set; }

    /// <summary>Gets a value indicating whether gets the is open.</summary>
    /// <value>The is open.</value>
    [Browsable(true)]
    [MonitoringDescription("IsOpen")]
    public bool IsOpen => _serialPort?.IsOpen ?? false;

    /// <summary>Gets the is open observable.</summary>
    /// <value>The is open observable.</value>
    public IObservable<bool> IsOpenObservable => GetOrCreateCachedObservable(
        ref _cachedIsOpenObservable,
        () => _isOpenValue.DistinctUntilChanged());

    /// <summary>Gets the is open async observable.</summary>
    /// <value>The is open async observable.</value>
    public IObservableAsync<bool> IsOpenObservableAsync =>
        GetOrCreateCachedAsyncObservable(ref _cachedIsOpenObservableAsync, IsOpenObservable);

    /// <summary>Gets or sets the parity.</summary>
    /// <value>The parity.</value>
    [Browsable(true)]
    [DefaultValue(Parity.None)]
    [MonitoringDescription("Parity")]
    public Parity Parity { get; set; } = Parity.None;

    /// <summary>Gets or sets the port.</summary>
    /// <value>The port.</value>
    [Browsable(true)]
    [DefaultValue("COM1")]
    [MonitoringDescription("PortName")]
    public string PortName { get; set; } = "COM1";

    /// <summary>Gets or sets the read timeout.</summary>
    /// <value>The read timeout.</value>
    [Browsable(true)]
    [DefaultValue(-1)]
    [MonitoringDescription("ReadTimeout")]
    public int ReadTimeout { get; set; } = -1;

    /// <summary>Gets or sets the stop bits.</summary>
    /// <value>The stop bits.</value>
    [Browsable(true)]
    [DefaultValue(StopBits.One)]
    [MonitoringDescription("StopBits")]
    public StopBits StopBits { get; set; } = StopBits.One;

    /// <summary>Gets or sets the write timeout.</summary>
    /// <value>The write timeout.</value>
    [Browsable(true)]
    [DefaultValue(-1)]
    [MonitoringDescription("WriteTimeout")]
    public int WriteTimeout { get; set; } = -1;

    /// <summary>Gets or sets creates new line.</summary>
    /// <value>
    /// The new line.
    /// </value>
    [Browsable(false)]
    [DefaultValue("\n")]
    [MonitoringDescription("NewLine")]
    public string NewLine { get; set; } = "\n";

    /// <summary>Gets or sets a value indicating whether break state.</summary>
    /// <value>The break state.</value>
    [Browsable(true)]
    [DefaultValue(false)]
    [MonitoringDescription("BreakState")]
    public bool BreakState
    {
        get => _serialPort?.BreakState ?? _breakState;
        set
        {
            _breakState = value;
            _serialPort?.BreakState = value;
        }
    }

    /// <summary>Gets the number of bytes of data in the receive buffer.</summary>
    /// <value>The bytes to read.</value>
    [Browsable(false)]
    [MonitoringDescription("BytesToRead")]
    public int BytesToRead => _serialPort?.BytesToRead ?? 0;

    /// <summary>Gets the number of bytes of data in the send buffer.</summary>
    /// <value>The bytes to write.</value>
    [Browsable(false)]
    [MonitoringDescription("BytesToWrite")]
    public int BytesToWrite => _serialPort?.BytesToWrite ?? 0;

    /// <summary>Gets a value indicating whether the Carrier Detect (CD) signal is on.</summary>
    /// <value>The CD holding.</value>
    [Browsable(false)]
    [MonitoringDescription("CDHolding")]
    public bool CDHolding => _serialPort?.CDHolding ?? false;

    /// <summary>Gets a value indicating whether the Clear-to-Send (CTS) signal is on.</summary>
    /// <value>The CTS holding.</value>
    [Browsable(false)]
    [MonitoringDescription("CtsHolding")]
    public bool CtsHolding => _serialPort?.CtsHolding ?? false;

    /// <summary>
    /// Gets or sets a value indicating whether null bytes are ignored when transmitted between the port and the receive
    /// buffer.
    /// </summary>
    /// <value>The discard null.</value>
    [Browsable(true)]
    [DefaultValue(false)]
    [MonitoringDescription("DiscardNull")]
    public bool DiscardNull
    {
        get => _serialPort?.DiscardNull ?? _discardNull;
        set
        {
            _discardNull = value;
            _serialPort?.DiscardNull = value;
        }
    }

    /// <summary>Gets a value indicating whether the Data Set Ready (DSR) signal is on.</summary>
    /// <value>The DSR holding.</value>
    [Browsable(false)]
    [MonitoringDescription("DsrHolding")]
    public bool DsrHolding => _serialPort?.DsrHolding ?? false;

    /// <summary>Gets or sets whether the Data Terminal Ready (DTR) signal is enabled.</summary>
    /// <value>The DTR enable.</value>
    [Browsable(true)]
    [DefaultValue(false)]
    [MonitoringDescription("DtrEnable")]
    public bool DtrEnable
    {
        get => _serialPort?.DtrEnable ?? _dtrEnable;
        set
        {
            _dtrEnable = value;
            _serialPort?.DtrEnable = value;
        }
    }

    /// <summary>Gets or sets the parity replace.</summary>
    /// <value>The parity replace.</value>
    [Browsable(true)]
    [DefaultValue((byte)63)]
    [MonitoringDescription("ParityReplace")]
    public byte ParityReplace
    {
        get => _serialPort?.ParityReplace ?? _parityReplace;
        set
        {
            _parityReplace = value;
            _serialPort?.ParityReplace = value;
        }
    }

    /// <summary>Gets or sets the size of the read buffer.</summary>
    /// <value>The size of the read buffer.</value>
    [Browsable(true)]
    [DefaultValue(4096)]
    [MonitoringDescription("ReadBufferSize")]
    public int ReadBufferSize
    {
        get => _serialPort?.ReadBufferSize ?? _readBufferSize;
        set
        {
            _readBufferSize = value;
            _serialPort?.ReadBufferSize = value;
        }
    }

    /// <summary>Gets or sets the byte threshold that raises DataReceived.</summary>
    /// <value>The received bytes threshold.</value>
    [Browsable(true)]
    [DefaultValue(1)]
    [MonitoringDescription("ReceivedBytesThreshold")]
    public int ReceivedBytesThreshold
    {
        get => _serialPort?.ReceivedBytesThreshold ?? _receivedBytesThreshold;
        set
        {
            _receivedBytesThreshold = value;
            _serialPort?.ReceivedBytesThreshold = value;
        }
    }

    /// <summary>Gets or sets whether the Request to Send (RTS) signal is enabled.</summary>
    /// <value>The RTS enable.</value>
    [Browsable(true)]
    [DefaultValue(false)]
    [MonitoringDescription("RtsEnable")]
    public bool RtsEnable
    {
        get => _serialPort?.RtsEnable ?? _rtsEnable;
        set
        {
            _rtsEnable = value;
            _serialPort?.RtsEnable = value;
        }
    }

    /// <summary>Gets or sets the size of the write buffer.</summary>
    /// <value>The size of the write buffer.</value>
    [Browsable(true)]
    [DefaultValue(2048)]
    [MonitoringDescription("WriteBufferSize")]
    public int WriteBufferSize
    {
        get => _serialPort?.WriteBufferSize ?? _writeBufferSize;
        set
        {
            _writeBufferSize = value;
            _serialPort?.WriteBufferSize = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether to automatically consume received data
    /// and feed it to the DataReceived and DataReceivedBytes observables.
    /// Set to false if you want to use synchronous Read methods instead.
    /// Must be set before calling Open().
    /// </summary>
    /// <value>True to enable automatic data reception (default), false to use sync reads.</value>
    [Browsable(true)]
    [DefaultValue(true)]
    [MonitoringDescription("EnableAutoDataReceive")]
    public bool EnableAutoDataReceive { get; set; } = true;

#if HasWindows
    /// <summary>Gets the pin changed.</summary>
    /// <value>
    /// The pin changed.
    /// </value>
    public IObservable<SerialPinChangedEventArgs> PinChanged =>
        GetOrCreateCachedObservable(ref _cachedPinChanged, _pinChanged);

    /// <summary>Gets the pin changed async observable.</summary>
    /// <value>
    /// The pin changed async observable.
    /// </value>
    public IObservableAsync<SerialPinChangedEventArgs> PinChangedAsync =>
        GetOrCreateCachedAsyncObservable(ref _cachedPinChangedAsync, PinChanged);
#endif

    /// <summary>Gets a lazily-created observable sequence of complete lines split by the NewLine sequence.</summary>
    public IObservable<string> Lines => _lines ??= CreateLinesObservable();

    /// <summary>Gets complete lines as an async observable.</summary>
    public IObservableAsync<string> LinesAsync =>
        _linesAsync ??= ObservableAsyncBridgeExtensions.ToAsyncObservable(Lines);

    /// <summary>Gets the placeholder returned when no serial ports are available.</summary>
    private static string[] NoPorts { get; } = ["NoPorts"];
}
