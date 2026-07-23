// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Net.Sockets;
using DateTime = System.DateTime;
using Timer = System.Threading.Timer;

#if REACTIVE_SHIM
namespace IoT.DriverCore.S7PlcRx.Reactive.Core;
#else
namespace IoT.DriverCore.S7PlcRx.Core;
#endif

/// <summary>
/// Provides an enhanced, observable-based socket communication layer for connecting to Siemens S7 PLCs with improved
/// connection management, error handling, and performance monitoring.
/// </summary>
/// <remarks>S7SocketRx manages the lifecycle of a TCP/IP connection to a Siemens S7 PLC, exposing connection and
/// availability status as observables for reactive monitoring. It supports automatic reconnection with exponential
/// backoff, optimized data transfer based on PLC type, and periodic metrics reporting. This class is intended for
/// internal use and is not thread-safe for concurrent operations beyond its designed observability and connection
/// management. Dispose the instance to release all resources and terminate background monitoring.</remarks>
internal partial class S7SocketRx : IDisposable
{
    /// <summary>Defines the f ai l e d value.</summary>
    private const string Failed = nameof(Failed);

    /// <summary>Defines the successful operation status.</summary>
    private const string Success = nameof(Success);

    /// <summary>Message emitted when an operation requires a connected device.</summary>
    private const string DeviceNotConnectedMessage = "Device not connected";

    /// <summary>Defines the d ef au lt ti me o u t value.</summary>
    private const int DefaultTimeout = 10_000;

    /// <summary>Defines the i ni ti al re tr yd el ay mi ll is ec on d s value.</summary>
    private const int InitialRetryDelayMilliseconds = 250;

    /// <summary>Defines the a xr et ry de la ym il li se co n d s value.</summary>
    private const int MaxRetryDelayMilliseconds = 5_000;

    /// <summary>Defines the p in gt im eo ut m s value.</summary>
    private const int PingTimeoutMs = 2_000;

    /// <summary>Defines the p or tp ro be ti me ou t m s value.</summary>
    private const int PortProbeTimeoutMs = 750;

    /// <summary>Defines the a va il ab il it yf ai lu re th re sh o l d value.</summary>
    private const int AvailabilityFailureThreshold = 3;

    /// <summary>Defines the c on ne ct io nf ai lu re th re sh o l d value.</summary>
    private const int ConnectionFailureThreshold = 3;

    /// <summary>Connection monitoring and retry policy.</summary>
    private const int MetricsReportIntervalSeconds = 30;

    /// <summary>Defines the Retry Backoff Maximum Exponent value.</summary>
    private const int RetryBackoffMaximumExponent = 4;

    /// <summary>Defines the Initial Retry Attempts To Log value.</summary>
    private const int InitialRetryAttemptsToLog = 5;

    /// <summary>Defines the Retry Logging Interval value.</summary>
    private const int RetryLoggingInterval = 10;

    /// <summary>Defines the Availability Startup Probe Interval Milliseconds value.</summary>
    private const int AvailabilityStartupProbeIntervalMilliseconds = 250;

    /// <summary>Defines the Availability Startup Probe Count value.</summary>
    private const int AvailabilityStartupProbeCount = 8;

    /// <summary>Defines the Connection Startup Probe Interval Milliseconds value.</summary>
    private const int ConnectionStartupProbeIntervalMilliseconds = 50;

    /// <summary>Defines the Socket Poll Microseconds value.</summary>
    private const int SocketPollMicroseconds = 1_000;

    /// <summary>Defines the Connection Idle Check Minutes value.</summary>
    private const int ConnectionIdleCheckMinutes = 2;

    /// <summary>Defines the Connection Restart Delay Milliseconds value.</summary>
    private const int ConnectionRestartDelayMilliseconds = 1_000;

    /// <summary>Maximum time allowed for tracked background work to stop during disposal.</summary>
    private const int BackgroundShutdownTimeoutMilliseconds = 3_000;

    /// <summary>Defines the Consecutive Error Restart Threshold value.</summary>
    private const int ConsecutiveErrorRestartThreshold = 6;

    /// <summary>ISO-on-TCP and S7 protocol framing.</summary>
    private const int S7TcpPort = 102;

    /// <summary>Defines the Tpkt Header Length value.</summary>
    private const int TpktHeaderLength = 4;

    /// <summary>Defines the Tpkt Length High Byte Offset value.</summary>
    private const int TpktLengthHighByteOffset = 2;

    /// <summary>Defines the Tpkt Length Low Byte Offset value.</summary>
    private const int TpktLengthLowByteOffset = 3;

    /// <summary>Defines the Bits Per Byte value.</summary>
    private const int BitsPerByte = 8;

    /// <summary>Defines the Cotp Data Header Length value.</summary>
    private const int CotpDataHeaderLength = 3;

    /// <summary>Defines the Iso Data Header Length value.</summary>
    private const int IsoDataHeaderLength = 7;

    /// <summary>Defines the Minimum Iso Packet Length value.</summary>
    private const int MinimumIsoPacketLength = 16;

    /// <summary>Defines the Connection Response Length value.</summary>
    private const int ConnectionResponseLength = 22;

    /// <summary>Defines the Communication Setup Response Length value.</summary>
    private const int CommunicationSetupResponseLength = 27;

    /// <summary>Defines the Handshake Receive Buffer Size value.</summary>
    private const int HandshakeReceiveBufferSize = 256;

    /// <summary>Defines the Minimum Negotiated Pdu Length value.</summary>
    private const ushort MinimumNegotiatedPduLength = 240;

    /// <summary>Defines the Maximum Negotiated Pdu Length value.</summary>
    private const ushort MaximumNegotiatedPduLength = 4_096;

    /// <summary>Defines the Negotiated Pdu Length Offset value.</summary>
    private const int NegotiatedPduLengthOffset = 25;

    /// <summary>Defines the Socket Buffer Pdu Multiplier value.</summary>
    private const int SocketBufferPduMultiplier = 2;

    /// <summary>Defines the S7 Response Return Code Offset value.</summary>
    private const int S7ResponseReturnCodeOffset = 21;

    /// <summary>Supported controller PDU sizes.</summary>
    private const ushort LogoPduLength = 240;

    /// <summary>Defines the Standard Pdu Length value.</summary>
    private const ushort StandardPduLength = 480;

    /// <summary>Defines the Extended Pdu Length value.</summary>
    private const ushort ExtendedPduLength = 960;

    /// <summary>Defines the High Performance Pdu Length value.</summary>
    private const ushort HighPerformancePduLength = 1_440;

    /// <summary>SZL request and response layout.</summary>
    private const int SzlBufferSize = 1_024;

    /// <summary>Defines the Szl Request Sequence Offset value.</summary>
    private const int SzlRequestSequenceOffset = 11;

    /// <summary>Defines the Szl Area Offset value.</summary>
    private const int SzlAreaOffset = 29;

    /// <summary>Defines the Szl Index Offset value.</summary>
    private const int SzlIndexOffset = 31;

    /// <summary>Defines the Szl Continuation Sequence Offset value.</summary>
    private const int SzlContinuationSequenceOffset = 24;

    /// <summary>Defines the Minimum Szl Response Length value.</summary>
    private const int MinimumSzlResponseLength = 32;

    /// <summary>Defines the Szl Error Code Offset value.</summary>
    private const int SzlErrorCodeOffset = 27;

    /// <summary>Defines the Szl Return Code Offset value.</summary>
    private const int SzlReturnCodeOffset = 29;

    /// <summary>Defines the S7 Return Code Success value.</summary>
    private const byte S7ReturnCodeSuccess = 0xff;

    /// <summary>Defines the Szl First Payload Offset value.</summary>
    private const int SzlFirstPayloadOffset = 41;

    /// <summary>Defines the Szl Continuation Payload Offset value.</summary>
    private const int SzlContinuationPayloadOffset = 37;

    /// <summary>Defines the Szl Data Length Offset value.</summary>
    private const int SzlDataLengthOffset = 31;

    /// <summary>Defines the Szl First Packet Metadata Length value.</summary>
    private const int SzlFirstPacketMetadataLength = 8;

    /// <summary>Defines the Szl Last Data Unit Offset value.</summary>
    private const int SzlLastDataUnitOffset = 26;

    /// <summary>Defines the Szl Sequence Offset value.</summary>
    private const int SzlSequenceOffset = 24;

    /// <summary>Defines the Szl Total Length Offset value.</summary>
    private const int SzlTotalLengthOffset = 37;

    /// <summary>S7 telegram byte values.</summary>
    private const byte TpktVersion = 0x03;

    /// <summary>Defines the Szl Telegram Length value.</summary>
    private const byte SzlTelegramLength = 0x21;

    /// <summary>Defines the Connection Request Telegram Length value.</summary>
    private const byte ConnectionRequestTelegramLength = 0x16;

    /// <summary>Defines the Communication Setup Telegram Length value.</summary>
    private const byte CommunicationSetupTelegramLength = 0x19;

    /// <summary>Defines the Cotp Data Header Size value.</summary>
    private const byte CotpDataHeaderSize = 0x02;

    /// <summary>Defines the Cotp Data Pdu Type value.</summary>
    private const byte CotpDataPduType = 0xf0;

    /// <summary>Defines the Cotp End Of Transmission Unit value.</summary>
    private const byte CotpEndOfTransmissionUnit = 0x80;

    /// <summary>Defines the Cotp Connection Request Header Size value.</summary>
    private const byte CotpConnectionRequestHeaderSize = 0x11;

    /// <summary>Defines the Cotp Connection Request Pdu Type value.</summary>
    private const byte CotpConnectionRequestPduType = 0xe0;

    /// <summary>Defines the Cotp Source Reference Low Byte value.</summary>
    private const byte CotpSourceReferenceLowByte = 0x2e;

    /// <summary>Defines the Cotp Source Tsap Parameter Code value.</summary>
    private const byte CotpSourceTsapParameterCode = 0xc1;

    /// <summary>Defines the Cotp Destination Tsap Parameter Code value.</summary>
    private const byte CotpDestinationTsapParameterCode = 0xc2;

    /// <summary>Defines the Cotp Tpdu Size Parameter Code value.</summary>
    private const byte CotpTpduSizeParameterCode = 0xc0;

    /// <summary>Defines the Cotp Tsap Parameter Length value.</summary>
    private const byte CotpTsapParameterLength = 0x02;

    /// <summary>Defines the Cotp Tpdu Size Parameter Length value.</summary>
    private const byte CotpTpduSizeParameterLength = 0x01;

    /// <summary>Defines the Cotp Tpdu Size512 Bytes value.</summary>
    private const byte CotpTpduSize512Bytes = 0x09;

    /// <summary>Defines the S7 Protocol Identifier value.</summary>
    private const byte S7ProtocolIdentifier = 0x32;

    /// <summary>Defines the S7 User Data Message Type value.</summary>
    private const byte S7UserDataMessageType = 0x07;

    /// <summary>Defines the S7 Job Message Type value.</summary>
    private const byte S7JobMessageType = 0x01;

    /// <summary>Defines the Szl First Pdu Reference value.</summary>
    private const byte SzlFirstPduReference = 0x05;

    /// <summary>Defines the Szl Continuation Pdu Reference value.</summary>
    private const byte SzlContinuationPduReference = 0x06;

    /// <summary>Defines the Szl First Parameter Length value.</summary>
    private const byte SzlFirstParameterLength = 0x08;

    /// <summary>Defines the Szl Continuation Parameter Length value.</summary>
    private const byte SzlContinuationParameterLength = 0x0c;

    /// <summary>Defines the Szl First Data Length value.</summary>
    private const byte SzlFirstDataLength = 0x08;

    /// <summary>Defines the Szl Continuation Data Length value.</summary>
    private const byte SzlContinuationDataLength = 0x04;

    /// <summary>Defines the S7 User Data Parameter Head value.</summary>
    private const byte S7UserDataParameterHead = 0x12;

    /// <summary>Defines the Szl First Parameter Payload Length value.</summary>
    private const byte SzlFirstParameterPayloadLength = 0x04;

    /// <summary>Defines the Szl Continuation Parameter Payload Length value.</summary>
    private const byte SzlContinuationParameterPayloadLength = 0x08;

    /// <summary>Defines the Szl Read Request value.</summary>
    private const byte SzlReadRequest = 0x11;

    /// <summary>Defines the Szl Read Response value.</summary>
    private const byte SzlReadResponse = 0x12;

    /// <summary>Defines the Szl Function Group value.</summary>
    private const byte SzlFunctionGroup = 0x44;

    /// <summary>Defines the Szl Subfunction value.</summary>
    private const byte SzlSubfunction = 0x01;

    /// <summary>Defines the Szl Continuation Flag value.</summary>
    private const byte SzlContinuationFlag = 0x01;

    /// <summary>Defines the S7 Octet String Transport Size value.</summary>
    private const byte S7OctetStringTransportSize = 0x09;

    /// <summary>Defines the Szl Request Data Length value.</summary>
    private const byte SzlRequestDataLength = 0x04;

    /// <summary>Defines the Szl Continuation Data Bit Length value.</summary>
    private const byte SzlContinuationDataBitLength = 0x0a;

    /// <summary>Defines the Communication Setup Pdu Reference value.</summary>
    private const byte CommunicationSetupPduReference = 0x04;

    /// <summary>Defines the Communication Setup Parameter Length value.</summary>
    private const byte CommunicationSetupParameterLength = 0x08;

    /// <summary>Defines the S7 Setup Communication Function value.</summary>
    private const byte S7SetupCommunicationFunction = 0xf0;

    /// <summary>Defines the Default Requested Pdu Length Low Byte value.</summary>
    private const byte DefaultRequestedPduLengthLowByte = 0x1e;

    /// <summary>Defines the Communication Setup Pdu Length Offset value.</summary>
    private const int CommunicationSetupPduLengthOffset = 23;

    /// <summary>Defines the Connection Request Source Tsap High Offset value.</summary>
    private const int ConnectionRequestSourceTsapHighOffset = 13;

    /// <summary>Defines the Connection Request Source Tsap Low Offset value.</summary>
    private const int ConnectionRequestSourceTsapLowOffset = 14;

    /// <summary>Defines the Connection Request Destination Tsap High Offset value.</summary>
    private const int ConnectionRequestDestinationTsapHighOffset = 17;

    /// <summary>Defines the Connection Request Destination Tsap Low Offset value.</summary>
    private const int ConnectionRequestDestinationTsapLowOffset = 18;

    /// <summary>Defines the Programming Device Source Tsap High Byte value.</summary>
    private const byte ProgrammingDeviceSourceTsapHighByte = 0x01;

    /// <summary>Defines the Default Source Tsap Low Byte value.</summary>
    private const byte DefaultSourceTsapLowByte = 0x00;

    /// <summary>Defines the Default Destination Tsap High Byte value.</summary>
    private const byte DefaultDestinationTsapHighByte = 0x03;

    /// <summary>Stores the e tr ic ss ub je c t used by this instance.</summary>
    private readonly Signal<ConnectionMetrics> _metricsSubject = new();

    /// <summary>Stores the c on ne ct io nl o c k used by this instance.</summary>
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    /// <summary>Stores the shared byte-buffer pool used by this instance.</summary>
    private readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;

    /// <summary>Coordinates background work with the lifetime of this connection.</summary>
    private readonly CancellationTokenSource _lifetimeCancellation = new();

    /// <summary>Synchronizes creation and capture of tracked background tasks.</summary>
    private readonly object _lifecycleSync = new();

    /// <summary>Stores the TCP port used for S7 connection and availability probes.</summary>
    private readonly int _s7TcpPort = S7TcpPort;

    /// <summary>Stores the e tr i c s used by this instance.</summary>
    private readonly ConnectionMetrics _metrics = new();

    /// <summary>Stores the e tr ic st im e r used by this instance.</summary>
    private readonly Timer? _metricsTimer;

    /// <summary>Stores the time provider used by this instance.</summary>
    private TimeProvider _timeProvider = TimeProvider.System;

    /// <summary>Stores the s oc ke te xc ep ti on su bj e c t used by this instance.</summary>
    private Signal<Exception> _socketExceptionSubject = new();

    /// <summary>Stores the d is po sa b l e used by this instance.</summary>
    private IDisposable _disposable;

    /// <summary>Tracks the currently active availability probe.</summary>
    private Task _availabilityProbeTask = Task.CompletedTask;

    /// <summary>Tracks the currently active availability observation and connection initialization.</summary>
    private Task _availabilityObservationTask = Task.CompletedTask;

    /// <summary>Tracks the currently active connection restart.</summary>
    private Task _restartTask = Task.CompletedTask;

    /// <summary>Stores the d is po se dv al u e used by this instance.</summary>
    private bool _disposedValue;

    /// <summary>Stores the i ni tc om pl e t e used by this instance.</summary>
    private bool _initComplete;

    /// <summary>Stores the i sa va il ab l e used by this instance.</summary>
    private bool? _isAvailable;

    /// <summary>Stores the i sc on ne ct e d used by this instance.</summary>
    private bool? _isConnected;

    /// <summary>Stores the s oc k e t used by this instance.</summary>
    private Socket? _socket;

    /// <summary>Stores the socket owned by an in-progress connection attempt.</summary>
    private Socket? _connectionAttemptSocket;

    /// <summary>Stores the l as ts uc ce ss fu lo pe ra ti o n used by this instance.</summary>
    private DateTime _lastSuccessfulOperation = DateTime.MinValue;

    /// <summary>Stores the number of consecutive socket errors.</summary>
    private int _consecutiveErrors;

    /// <summary>Stores the number of consecutive availability-check failures.</summary>
    private int _consecutiveAvailabilityFailures;

    /// <summary>Stores the number of consecutive connection failures.</summary>
    private int _consecutiveConnectionFailures;

    /// <summary>Indicates whether a restart operation is in progress.</summary>
    private int _restartInProgress;
}
