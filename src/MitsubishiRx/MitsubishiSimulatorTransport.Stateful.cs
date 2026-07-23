// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Provides stateful device and controller behavior for the simulator transport.</summary>
public sealed partial class MitsubishiSimulatorTransport
{
    /// <summary>The number of hexadecimal characters used to encode a byte.</summary>
    private const int HexByteCharacterCount = 2;

    /// <summary>The number of hexadecimal characters used to encode a word.</summary>
    private const int HexWordCharacterCount = 4;

    /// <summary>The maximum point count represented by a zero legacy count.</summary>
    private const int MaximumLegacyPointCount = 256;

    /// <summary>The number of characters before a 1C batch device field.</summary>
    private const int SerialOneCPrefixCharacterCount = 7;

    /// <summary>The number of characters in a 3C request header.</summary>
    private const int SerialThreeCHeaderCharacterCount = 10;

    /// <summary>The number of characters in a 4C request header.</summary>
    private const int SerialFourCHeaderCharacterCount = 16;

    /// <summary>The number of bytes before a binary 4C request body.</summary>
    private const int SerialBinaryEnvelopeByteCount = 4;

    /// <summary>The number of bytes in a binary serial request header.</summary>
    private const int SerialBinaryHeaderByteCount = 8;

    /// <summary>The number of bytes in a binary serial command.</summary>
    private const int SerialBinaryCommandByteCount = 4;

    /// <summary>The number of bytes in the binary 3E command prefix.</summary>
    private const int ThreeEBinaryCommandOffset = 11;

    /// <summary>The number of bytes in the binary 4E command prefix.</summary>
    private const int FourEBinaryCommandOffset = 15;

    /// <summary>The number of bytes in the ASCII 3E command prefix.</summary>
    private const int ThreeEAsciiCommandOffset = 22;

    /// <summary>The number of bytes in the ASCII 4E command prefix.</summary>
    private const int FourEAsciiCommandOffset = 30;

    /// <summary>The number of characters in a legacy ASCII device number.</summary>
    private const int LegacyAsciiDeviceNumberCharacterCount = 8;

    /// <summary>The number of characters in a modern ASCII device number.</summary>
    private const int ModernAsciiDeviceNumberCharacterCount = 6;

    /// <summary>The number of bytes in a legacy binary device field.</summary>
    private const int LegacyBinaryDeviceFieldByteCount = 6;

    /// <summary>The number of bytes in a modern binary device field.</summary>
    private const int ModernBinaryDeviceFieldByteCount = 4;

    /// <summary>The number of bytes in a modern binary device number.</summary>
    private const int ModernBinaryDeviceNumberByteCount = 3;

    /// <summary>The minimum number of bytes in an ASCII serial frame.</summary>
    private const int MinimumSerialAsciiFrameByteCount = 4;

    /// <summary>The number of framing bytes after an ASCII serial request body.</summary>
    private const int SerialAsciiSuffixByteCount = 3;

    /// <summary>The number of bits in a byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>The number of bits in two bytes.</summary>
    private const int BitsPerTwoBytes = 16;

    /// <summary>The number of bits in three bytes.</summary>
    private const int BitsPerThreeBytes = 24;

    /// <summary>The fixed protocol width of a controller model name.</summary>
    private const int ControllerModelNameCharacterCount = 16;

    /// <summary>The prefix used by serial word-read descriptions.</summary>
    private const string ReadWordsDescriptionPrefix = "Read words ";

    /// <summary>The prefix used by serial bit-read descriptions.</summary>
    private const string ReadBitsDescriptionPrefix = "Read bits ";

    /// <summary>The prefix used by serial word-write descriptions.</summary>
    private const string WriteWordsDescriptionPrefix = "Write words ";

    /// <summary>The prefix used by serial bit-write descriptions.</summary>
    private const string WriteBitsDescriptionPrefix = "Write bits ";

    /// <summary>Stores registered monitor devices.</summary>
    private readonly List<MitsubishiDeviceAddress> _monitorDevices = [];

    /// <summary>Stores controller buffer-memory words.</summary>
    private readonly Dictionary<ushort, ushort> _bufferMemory = [];

    /// <summary>Stores whether the simulated controller is running.</summary>
    private bool _isCpuRunning = true;

    /// <summary>Stores the current simulated controller error.</summary>
    private ushort _controllerError;

    /// <summary>Stores the simulated controller model name.</summary>
    private string _modelName = "MITSUBISHI SIMULATOR";

    /// <summary>Stores the simulated controller model code.</summary>
    private ushort _modelCode = 0x0001;

    /// <summary>Gets or sets the simulated controller model name.</summary>
    public string ModelName
    {
        get
        {
            lock (_stateGate)
            {
                return _modelName;
            }
        }

        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            lock (_stateGate)
            {
                ThrowIfDisposed();
                _modelName = value;
            }
        }
    }

    /// <summary>Gets or sets the simulated controller model code.</summary>
    public ushort ModelCode
    {
        get
        {
            lock (_stateGate)
            {
                return _modelCode;
            }
        }

        set
        {
            lock (_stateGate)
            {
                ThrowIfDisposed();
                _modelCode = value;
            }
        }
    }

    /// <summary>Gets whether the simulated controller is in the run state.</summary>
    public bool IsCpuRunning
    {
        get
        {
            lock (_stateGate)
            {
                return _isCpuRunning;
            }
        }
    }

    /// <summary>Gets the current simulated controller error code.</summary>
    public ushort ControllerError
    {
        get
        {
            lock (_stateGate)
            {
                return _controllerError;
            }
        }
    }

    /// <summary>Sets the current simulated controller error code.</summary>
    /// <param name="errorCode">The deterministic controller error code.</param>
    public void SetControllerError(ushort errorCode)
    {
        lock (_stateGate)
        {
            ThrowIfDisposed();
            _controllerError = errorCode;
        }
    }

    /// <summary>Reads consecutive simulated buffer-memory words.</summary>
    /// <param name="address">The first buffer-memory address.</param>
    /// <param name="length">The number of words to read.</param>
    /// <returns>A detached word snapshot.</returns>
    public ushort[] ReadBufferMemory(ushort address, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        var result = new ushort[length];
        lock (_stateGate)
        {
            ThrowIfDisposed();
            for (var offset = 0; offset < length; offset++)
            {
                _ = _bufferMemory.TryGetValue(
                    checked((ushort)(address + offset)),
                    out result[offset]);
            }
        }

        return result;
    }

    /// <summary>Writes consecutive simulated buffer-memory words.</summary>
    /// <param name="address">The first buffer-memory address.</param>
    /// <param name="values">The values to write.</param>
    public void WriteBufferMemory(ushort address, IReadOnlyList<ushort> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException("At least one value must be supplied.", nameof(values));
        }

        lock (_stateGate)
        {
            ThrowIfDisposed();
            for (var offset = 0; offset < values.Count; offset++)
            {
                _bufferMemory[checked((ushort)(address + offset))] = values[offset];
            }
        }
    }

    /// <summary>Creates a stateful response for a generated Mitsubishi request.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <returns>The operation result.</returns>
    private byte[] CreateStatefulResponse(
        MitsubishiClientOptions options,
        MitsubishiTransportRequest request)
    {
        if (options.TransportKind == MitsubishiTransportKind.Serial)
        {
            return CreateSerialStatefulResponse(options, request);
        }

        var decoded = DecodeMcRequest(options, request.Payload);
        var payload = ExecuteDecodedRequest(options, decoded);
        return CreateSuccessResponse(options, payload);
    }

    /// <summary>Executes one decoded MC request against simulator state.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <returns>The operation result.</returns>
    private byte[] ExecuteDecodedRequest(
        MitsubishiClientOptions options,
        DecodedSimulatorRequest request)
    {
        return request.Command switch
        {
            MitsubishiCommandCodes.DeviceRead => ExecuteDeviceRead(options, request),
            MitsubishiCommandCodes.DeviceWrite => ExecuteDeviceWrite(options, request),
            MitsubishiCommandCodes.RandomRead => ExecuteRandomRead(options, request),
            MitsubishiCommandCodes.RandomWrite => ExecuteRandomWrite(options, request),
            MitsubishiCommandCodes.BlockRead => ExecuteBlockRead(options, request),
            MitsubishiCommandCodes.BlockWrite => ExecuteBlockWrite(options, request),
            MitsubishiCommandCodes.EntryMonitorDevice => ExecuteMonitorRegistration(options, request),
            MitsubishiCommandCodes.ExecuteMonitor => ExecuteMonitor(),
            MitsubishiCommandCodes.MemoryRead or MitsubishiCommandCodes.ExtendUnitRead =>
                ExecuteBufferMemoryRead(options, request),
            MitsubishiCommandCodes.MemoryWrite or MitsubishiCommandCodes.ExtendUnitWrite =>
                ExecuteBufferMemoryWrite(request),
            MitsubishiCommandCodes.ReadTypeName => ExecuteReadTypeName(options),
            MitsubishiCommandCodes.RemoteRun => ExecuteCpuStateChange(isRunning: true),
            MitsubishiCommandCodes.RemoteStop
            or MitsubishiCommandCodes.RemotePause
            or MitsubishiCommandCodes.RemoteReset => ExecuteCpuStateChange(isRunning: false),
            MitsubishiCommandCodes.ClearError => ExecuteClearError(),
            MitsubishiCommandCodes.LoopbackTest => ExecuteLoopback(request),
            _ => [],
        };
    }

    /// <summary>Reads a consecutive device range.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <returns>The operation result.</returns>
    private byte[] ExecuteDeviceRead(
        MitsubishiClientOptions options,
        DecodedSimulatorRequest request)
    {
        var offset = 0;
        var address = ReadDeviceAddress(options, request, ref offset);
        var points = ReadPointCount(request, ref offset);
        var bitUnits = request.Subcommand == 0x0001;
        if (request.IsLegacy)
        {
            bitUnits = request.LegacyCommand == 0x00;
        }

        return bitUnits
            ? EncodeBits(Memory.ReadBits(address, points), options.DataCode)
            : EncodeWords(Memory.ReadWords(address, points), options.DataCode);
    }

    /// <summary>Writes a consecutive device range.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <returns>The operation result.</returns>
    private byte[] ExecuteDeviceWrite(
        MitsubishiClientOptions options,
        DecodedSimulatorRequest request)
    {
        var offset = 0;
        var address = ReadDeviceAddress(options, request, ref offset);
        var points = ReadPointCount(request, ref offset);
        var bitUnits = request.Subcommand == 0x0001;
        if (request.IsLegacy)
        {
            bitUnits = request.LegacyCommand == 0x02;
        }

        if (bitUnits)
        {
            Memory.WriteBits(address, ReadBitValues(request, ref offset, points));
        }
        else
        {
            Memory.WriteWords(address, ReadWordValues(request, ref offset, points));
        }

        return [];
    }

    /// <summary>Reads random word devices.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <returns>The operation result.</returns>
    private byte[] ExecuteRandomRead(
        MitsubishiClientOptions options,
        DecodedSimulatorRequest request)
    {
        var offset = 0;
        var count = ReadRandomDeviceCount(request, ref offset);
        var words = new ushort[count];
        for (var index = 0; index < count; index++)
        {
            var address = ReadDeviceAddress(options, request, ref offset);
            words[index] = Memory.ReadWords(address, 1)[0];
        }

        return EncodeWords(words, options.DataCode);
    }

    /// <summary>Writes random word devices.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <returns>The operation result.</returns>
    private byte[] ExecuteRandomWrite(
        MitsubishiClientOptions options,
        DecodedSimulatorRequest request)
    {
        var offset = 0;
        var count = ReadUInt16(request, ref offset);
        _ = ReadUInt16(request, ref offset);
        for (var index = 0; index < count; index++)
        {
            var address = ReadDeviceAddress(options, request, ref offset);
            Memory.WriteWords(address, [ReadUInt16(request, ref offset)]);
        }

        return [];
    }

    /// <summary>Reads word and bit blocks.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <returns>The operation result.</returns>
    private byte[] ExecuteBlockRead(
        MitsubishiClientOptions options,
        DecodedSimulatorRequest request)
    {
        var offset = 0;
        var wordBlockCount = ReadUInt16(request, ref offset);
        var bitBlockCount = ReadUInt16(request, ref offset);
        var result = new List<byte>();
        for (var index = 0; index < wordBlockCount; index++)
        {
            var address = ReadDeviceAddress(options, request, ref offset);
            var points = ReadUInt16(request, ref offset);
            result.AddRange(EncodeWords(Memory.ReadWords(address, points), options.DataCode));
        }

        for (var index = 0; index < bitBlockCount; index++)
        {
            var address = ReadDeviceAddress(options, request, ref offset);
            var points = ReadUInt16(request, ref offset);
            result.AddRange(EncodeBlockBits(Memory.ReadBits(address, points), options.DataCode));
        }

        return result.ToArray();
    }

    /// <summary>Writes word and bit blocks.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <returns>The operation result.</returns>
    private byte[] ExecuteBlockWrite(
        MitsubishiClientOptions options,
        DecodedSimulatorRequest request)
    {
        var offset = 0;
        var wordBlockCount = ReadUInt16(request, ref offset);
        var bitBlockCount = ReadUInt16(request, ref offset);
        for (var index = 0; index < wordBlockCount; index++)
        {
            var address = ReadDeviceAddress(options, request, ref offset);
            var points = ReadUInt16(request, ref offset);
            Memory.WriteWords(address, ReadWordValues(request, ref offset, points));
        }

        for (var index = 0; index < bitBlockCount; index++)
        {
            var address = ReadDeviceAddress(options, request, ref offset);
            var points = ReadUInt16(request, ref offset);
            Memory.WriteBits(address, ReadBlockBitValues(request, ref offset, points));
        }

        return [];
    }

    /// <summary>Registers monitor devices.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <returns>The operation result.</returns>
    private byte[] ExecuteMonitorRegistration(
        MitsubishiClientOptions options,
        DecodedSimulatorRequest request)
    {
        var offset = 0;
        var count = ReadRandomDeviceCount(request, ref offset);
        var addresses = new List<MitsubishiDeviceAddress>(count);
        for (var index = 0; index < count; index++)
        {
            addresses.Add(ReadDeviceAddress(options, request, ref offset));
        }

        lock (_stateGate)
        {
            _monitorDevices.Clear();
            _monitorDevices.AddRange(addresses);
        }

        return [];
    }

    /// <summary>Reads registered monitor devices.</summary>
    /// <returns>The operation result.</returns>
    private byte[] ExecuteMonitor()
    {
        MitsubishiClientOptions options;
        MitsubishiDeviceAddress[] addresses;
        lock (_stateGate)
        {
            options = _connectedOptions
                ?? throw new InvalidOperationException("The Mitsubishi simulator is not connected.");
            addresses = _monitorDevices.ToArray();
        }

        var words = addresses
            .Select(address => Memory.ReadWords(address, 1)[0])
            .ToArray();
        return EncodeWords(words, options.DataCode);
    }

    /// <summary>Reads controller buffer memory.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <returns>The operation result.</returns>
    private byte[] ExecuteBufferMemoryRead(
        MitsubishiClientOptions options,
        DecodedSimulatorRequest request)
    {
        var offset = 0;
        var address = ReadUInt16(request, ref offset);
        var length = ReadUInt16(request, ref offset);
        return EncodeWords(ReadBufferMemory(address, length), options.DataCode);
    }

    /// <summary>Writes controller buffer memory.</summary>
    /// <param name="request">The request parameter.</param>
    /// <returns>The operation result.</returns>
    private byte[] ExecuteBufferMemoryWrite(DecodedSimulatorRequest request)
    {
        var offset = 0;
        var address = ReadUInt16(request, ref offset);
        var length = ReadUInt16(request, ref offset);
        WriteBufferMemory(address, ReadWordValues(request, ref offset, length));
        return [];
    }

    /// <summary>Reads the simulated controller type name.</summary>
    /// <param name="options">The options parameter.</param>
    /// <returns>The operation result.</returns>
    private byte[] ExecuteReadTypeName(MitsubishiClientOptions options)
    {
        string name;
        ushort code;
        lock (_stateGate)
        {
            name = _modelName;
            code = _modelCode;
        }

        var fixedName = name.PadRight(ControllerModelNameCharacterCount);
        return options.DataCode == CommunicationDataCode.Ascii
            ? Encoding.ASCII.GetBytes(
                fixedName + code.ToString("X4", CultureInfo.InvariantCulture))
            : [
                .. Encoding.ASCII.GetBytes(fixedName),
                (byte)(code & 0xFF),
                (byte)(code >> 8),
            ];
    }

    /// <summary>Returns the echoed data from an MC loopback request.</summary>
    /// <param name="request">The decoded loopback request.</param>
    /// <returns>The loopback data without its request length prefix.</returns>
    private byte[] ExecuteLoopback(DecodedSimulatorRequest request)
    {
        if (!request.IsAscii)
        {
            EnsureAvailable(request.Body, 0, ProtocolWordByteCount);
            return request.Body[ProtocolWordByteCount..];
        }

        var lengthCharacterCount = request.IsLegacy
            ? HexByteCharacterCount
            : HexWordCharacterCount;
        EnsureAvailable(request.Body, 0, lengthCharacterCount);
        return request.Body[lengthCharacterCount..];
    }

    /// <summary>Changes the simulated controller run state.</summary>
    /// <param name="isRunning">The isRunning parameter.</param>
    /// <returns>The operation result.</returns>
    private byte[] ExecuteCpuStateChange(bool isRunning)
    {
        lock (_stateGate)
        {
            _isCpuRunning = isRunning;
        }

        return [];
    }

    /// <summary>Clears the simulated controller error.</summary>
    /// <returns>The operation result.</returns>
    private byte[] ExecuteClearError()
    {
        lock (_stateGate)
        {
            _controllerError = 0;
        }

        return [];
    }

    /// <summary>Creates stateful responses for supported serial batch requests.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <returns>The operation result.</returns>
    private byte[] CreateSerialStatefulResponse(
        MitsubishiClientOptions options,
        MitsubishiTransportRequest request)
    {
        return TryExecuteSerialBatch(options, request, out var payload)
            ? CreateSuccessResponse(options, payload)
            : CreateSuccessResponse(options, []);
    }

    /// <summary>Executes a serial batch operation when it can be decoded unambiguously.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <param name="payload">The payload parameter.</param>
    /// <returns>The operation result.</returns>
    private bool TryExecuteSerialBatch(
        MitsubishiClientOptions options,
        MitsubishiTransportRequest request,
        out byte[] payload)
    {
        if (!TryDecodeSerialBatchDescription(
                request.Description,
                out var isRead,
                out var isWord,
                out var addressText))
        {
            payload = [];
            return false;
        }

        var address = MitsubishiDeviceAddress.Parse(
            addressText,
            options.XyNotation);
        var decoded = DecodeSerialBatch(options, request.Payload, address, isRead, isWord);
        if (isRead)
        {
            payload = ReadSerialBatch(options, address, decoded.Values.Length, isWord);
            return true;
        }

        WriteSerialBatch(address, decoded.Values, isWord);
        payload = [];
        return true;
    }

    /// <summary>Decodes a generated serial batch description.</summary>
    /// <param name="description">The generated request description.</param>
    /// <param name="isRead">Whether the operation reads device memory.</param>
    /// <param name="isWord">Whether the operation uses word units.</param>
    /// <param name="address">The device address text.</param>
    /// <returns>Whether the description represents a supported batch operation.</returns>
    private bool TryDecodeSerialBatchDescription(
        string description,
        out bool isRead,
        out bool isWord,
        out string address)
    {
        ThrowIfDisposed();
        if (description.StartsWith(ReadWordsDescriptionPrefix, StringComparison.Ordinal))
        {
            (isRead, isWord, address) =
                (true, true, description[ReadWordsDescriptionPrefix.Length..]);
            return true;
        }

        if (description.StartsWith(ReadBitsDescriptionPrefix, StringComparison.Ordinal))
        {
            (isRead, isWord, address) =
                (true, false, description[ReadBitsDescriptionPrefix.Length..]);
            return true;
        }

        if (description.StartsWith(WriteWordsDescriptionPrefix, StringComparison.Ordinal))
        {
            (isRead, isWord, address) =
                (false, true, description[WriteWordsDescriptionPrefix.Length..]);
            return true;
        }

        if (description.StartsWith(WriteBitsDescriptionPrefix, StringComparison.Ordinal))
        {
            (isRead, isWord, address) =
                (false, false, description[WriteBitsDescriptionPrefix.Length..]);
            return true;
        }

        (isRead, isWord, address) = (false, false, string.Empty);
        return false;
    }

    /// <summary>Reads one decoded serial batch from device memory.</summary>
    /// <param name="options">The connected protocol options.</param>
    /// <param name="address">The first device address.</param>
    /// <param name="points">The point count.</param>
    /// <param name="isWord">Whether the operation uses word units.</param>
    /// <returns>The encoded response payload.</returns>
    private byte[] ReadSerialBatch(
        MitsubishiClientOptions options,
        MitsubishiDeviceAddress address,
        int points,
        bool isWord) =>
        isWord
            ? EncodeWords(Memory.ReadWords(address, points), options.DataCode)
            : EncodeBits(Memory.ReadBits(address, points), options.DataCode);

    /// <summary>Writes one decoded serial batch to device memory.</summary>
    /// <param name="address">The first device address.</param>
    /// <param name="values">The decoded device values.</param>
    /// <param name="isWord">Whether the operation uses word units.</param>
    private void WriteSerialBatch(
        MitsubishiDeviceAddress address,
        ushort[] values,
        bool isWord)
    {
        if (isWord)
        {
            Memory.WriteWords(address, values);
            return;
        }

        Memory.WriteBits(address, values.Select(static value => value != 0).ToArray());
    }

    /// <summary>Decodes one generated serial batch request.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="payload">The payload parameter.</param>
    /// <param name="address">The address parameter.</param>
    /// <param name="isRead">The isRead parameter.</param>
    /// <param name="isWord">The isWord parameter.</param>
    /// <returns>The operation result.</returns>
    private SerialBatchRequest DecodeSerialBatch(
        MitsubishiClientOptions options,
        byte[] payload,
        MitsubishiDeviceAddress address,
        bool isRead,
        bool isWord)
    {
        if (options.FrameType == MitsubishiFrameType.FourC
            && options.ResolvedSerial.MessageFormat == MitsubishiSerialMessageFormat.Format5)
        {
            return DecodeBinarySerialBatch(payload, isRead, isWord);
        }

        var text = NormalizeSerialAsciiRequest(payload);
        return options.FrameType == MitsubishiFrameType.OneC
            ? DecodeOneCSerialBatch(text, address, isRead, isWord)
            : DecodeModernAsciiSerialBatch(options.FrameType, text, isRead, isWord);
    }

    /// <summary>Decodes a generated binary 4C batch request.</summary>
    /// <param name="payload">The framed request bytes.</param>
    /// <param name="isRead">Whether the operation reads device memory.</param>
    /// <param name="isWord">Whether the operation uses word units.</param>
    /// <returns>The decoded batch request.</returns>
    private SerialBatchRequest DecodeBinarySerialBatch(
        byte[] payload,
        bool isRead,
        bool isWord)
    {
        var inner = payload.AsSpan(SerialBinaryEnvelopeByteCount);
        var offset = SerialBinaryHeaderByteCount
            + SerialBinaryCommandByteCount
            + ModernBinaryDeviceFieldByteCount;
        var points = ReadLittleEndianUInt16(inner, ref offset);
        var values = isRead
            ? new ushort[points]
            : ReadBinaryBatchValues(inner, ref offset, points, isWord);
        return new SerialBatchRequest(values);
    }

    /// <summary>Decodes a generated ASCII 1C batch request.</summary>
    /// <param name="payload">The normalized request text.</param>
    /// <param name="address">The decoded device address.</param>
    /// <param name="isRead">Whether the operation reads device memory.</param>
    /// <param name="isWord">Whether the operation uses word units.</param>
    /// <returns>The decoded batch request.</returns>
    private SerialBatchRequest DecodeOneCSerialBatch(
        string payload,
        MitsubishiDeviceAddress address,
        bool isRead,
        bool isWord)
    {
        var addressLength = address.Symbol.Length
            + (address.Symbol.Length > 1
                ? ModernBinaryDeviceNumberByteCount
                : ModernBinaryDeviceFieldByteCount);
        var offset = SerialOneCPrefixCharacterCount + addressLength;
        var encodedPoints = ParseHexByte(payload.AsSpan(offset, HexByteCharacterCount));
        offset += HexByteCharacterCount;
        var points = encodedPoints == 0 ? MaximumLegacyPointCount : encodedPoints;
        var values = isRead
            ? new ushort[points]
            : ReadAsciiBatchValues(payload, ref offset, points, isWord);
        return new SerialBatchRequest(values);
    }

    /// <summary>Decodes a generated ASCII 3C or 4C batch request.</summary>
    /// <param name="frameType">The serial frame type.</param>
    /// <param name="payload">The normalized request text.</param>
    /// <param name="isRead">Whether the operation reads device memory.</param>
    /// <param name="isWord">Whether the operation uses word units.</param>
    /// <returns>The decoded batch request.</returns>
    private SerialBatchRequest DecodeModernAsciiSerialBatch(
        MitsubishiFrameType frameType,
        string payload,
        bool isRead,
        bool isWord)
    {
        var headerLength = frameType == MitsubishiFrameType.ThreeC
            ? SerialThreeCHeaderCharacterCount
            : SerialFourCHeaderCharacterCount;
        var bodyOffset = headerLength + LegacyAsciiDeviceNumberCharacterCount;
        var pointsOffset = bodyOffset + LegacyAsciiDeviceNumberCharacterCount;
        int points = isRead
            ? ParseHexByte(payload.AsSpan(pointsOffset, HexByteCharacterCount))
            : ParseHexUInt16(payload.AsSpan(pointsOffset, HexWordCharacterCount));
        points = points == 0 ? MaximumLegacyPointCount : points;
        var valueOffset = pointsOffset
            + (isRead ? HexByteCharacterCount : HexWordCharacterCount);
        var values = isRead
            ? new ushort[points]
            : ReadAsciiBatchValues(payload, ref valueOffset, points, isWord);
        return new SerialBatchRequest(values);
    }

    /// <summary>Reads binary serial batch values.</summary>
    /// <param name="payload">The payload parameter.</param>
    /// <param name="offset">The offset parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="isWord">The isWord parameter.</param>
    /// <returns>The operation result.</returns>
    private ushort[] ReadBinaryBatchValues(
        ReadOnlySpan<byte> payload,
        ref int offset,
        int points,
        bool isWord)
    {
        var values = new ushort[points];
        for (var index = 0; index < points; index++)
        {
            if (isWord)
            {
                values[index] = ReadLittleEndianUInt16(payload, ref offset);
                continue;
            }

            values[index] = Convert.ToUInt16(payload[offset] != 0);
            offset++;
        }

        return values;
    }

    /// <summary>Reads ASCII serial batch values.</summary>
    /// <param name="payload">The payload parameter.</param>
    /// <param name="offset">The offset parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="isWord">The isWord parameter.</param>
    /// <returns>The operation result.</returns>
    private ushort[] ReadAsciiBatchValues(
        string payload,
        ref int offset,
        int points,
        bool isWord)
    {
        var values = new ushort[points];
        for (var index = 0; index < points; index++)
        {
            if (isWord)
            {
                values[index] = ParseHexUInt16(
                    payload.AsSpan(offset, HexWordCharacterCount));
                offset += HexWordCharacterCount;
            }
            else
            {
                values[index] = Convert.ToUInt16(payload[offset] != '0');
                offset++;
            }
        }

        return values;
    }

    /// <summary>Normalizes an ASCII serial request to its body text.</summary>
    /// <param name="payload">The payload parameter.</param>
    /// <returns>The operation result.</returns>
    private string NormalizeSerialAsciiRequest(byte[] payload)
    {
        var bytes = payload
            .Where(static value => value is not (byte)'\r' and not (byte)'\n')
            .ToArray();
        if (bytes.Length < MinimumSerialAsciiFrameByteCount || bytes[0] != 0x05)
        {
            throw new InvalidDataException("The serial simulator received an invalid ASCII request frame.");
        }

        return Encoding.ASCII.GetString(bytes, 1, bytes.Length - SerialAsciiSuffixByteCount);
    }

    /// <summary>Decodes an MC request frame.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="payload">The payload parameter.</param>
    /// <returns>The operation result.</returns>
    private DecodedSimulatorRequest DecodeMcRequest(
        MitsubishiClientOptions options,
        byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (options.FrameType == MitsubishiFrameType.OneE)
        {
            return DecodeLegacyMcRequest(options, payload);
        }

        var isAscii = options.DataCode == CommunicationDataCode.Ascii;
        var commandOffset = (options.FrameType, isAscii) switch
        {
            (MitsubishiFrameType.ThreeE, false) => ThreeEBinaryCommandOffset,
            (MitsubishiFrameType.FourE, false) => FourEBinaryCommandOffset,
            (MitsubishiFrameType.ThreeE, true) => ThreeEAsciiCommandOffset,
            (MitsubishiFrameType.FourE, true) => FourEAsciiCommandOffset,
            _ => throw new ArgumentOutOfRangeException(nameof(options.FrameType)),
        };
        EnsureAvailable(
            payload,
            commandOffset,
            isAscii
                ? HexWordCharacterCount * ProtocolWordByteCount
                : HexWordCharacterCount);
        ushort command;
        ushort subcommand;
        int bodyOffset;
        if (isAscii)
        {
            command = ParseHexUInt16(
                payload.AsSpan(commandOffset, HexWordCharacterCount));
            subcommand = ParseHexUInt16(
                payload.AsSpan(
                    commandOffset + HexWordCharacterCount,
                    HexWordCharacterCount));
            bodyOffset = commandOffset
                + (HexWordCharacterCount * ProtocolWordByteCount);
        }
        else
        {
            command = ReadLittleEndianUInt16(payload, commandOffset);
            subcommand = ReadLittleEndianUInt16(
                payload,
                commandOffset + ProtocolWordByteCount);
            bodyOffset = commandOffset + HexWordCharacterCount;
        }

        return new DecodedSimulatorRequest(
            command,
            subcommand,
            payload[bodyOffset..],
            isAscii,
            IsLegacy: false,
            LegacyCommand: null);
    }

    /// <summary>Decodes a legacy 1E MC request frame.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="payload">The payload parameter.</param>
    /// <returns>The operation result.</returns>
    private DecodedSimulatorRequest DecodeLegacyMcRequest(
        MitsubishiClientOptions options,
        byte[] payload)
    {
        var isAscii = options.DataCode == CommunicationDataCode.Ascii;
        EnsureAvailable(
            payload,
            0,
            isAscii
                ? HexWordCharacterCount * ProtocolWordByteCount
                : HexWordCharacterCount);
        var legacyCommand = isAscii
            ? ParseHexByte(payload.AsSpan(0, HexByteCharacterCount))
            : payload[0];
        var bodyOffset = isAscii
            ? HexWordCharacterCount * ProtocolWordByteCount
            : HexWordCharacterCount;
        var mapping = legacyCommand switch
        {
            0x00 => (MitsubishiCommandCodes.DeviceRead, (ushort)0x0000),
            0x01 => (MitsubishiCommandCodes.DeviceRead, (ushort)0x0001),
            0x02 => (MitsubishiCommandCodes.DeviceWrite, (ushort)0x0002),
            0x03 => (MitsubishiCommandCodes.DeviceWrite, (ushort)0x0003),
            0x06 => (MitsubishiCommandCodes.EntryMonitorDevice, (ushort)0x0000),
            0x08 => (MitsubishiCommandCodes.ExecuteMonitor, (ushort)0x0000),
            0x13 => (MitsubishiCommandCodes.RemoteRun, (ushort)0x0000),
            0x14 => (MitsubishiCommandCodes.RemoteStop, (ushort)0x0000),
            0x15 => (MitsubishiCommandCodes.ReadTypeName, (ushort)0x0000),
            0x16 => (MitsubishiCommandCodes.LoopbackTest, (ushort)0x0000),
            _ => ((ushort)0, (ushort)0),
        };
        return new DecodedSimulatorRequest(
            mapping.Item1,
            mapping.Item2,
            payload[bodyOffset..],
            isAscii,
            IsLegacy: true,
            legacyCommand);
    }

    /// <summary>Reads a device address from a decoded request body.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <param name="offset">The offset parameter.</param>
    /// <returns>The operation result.</returns>
    private MitsubishiDeviceAddress ReadDeviceAddress(
        MitsubishiClientOptions options,
        DecodedSimulatorRequest request,
        ref int offset)
    {
        if (request.IsAscii)
        {
            var numberLength = request.IsLegacy
                ? LegacyAsciiDeviceNumberCharacterCount
                : ModernAsciiDeviceNumberCharacterCount;
            EnsureAvailable(
                request.Body,
                offset,
                numberLength + HexByteCharacterCount);
            var number = int.Parse(
                Encoding.ASCII.GetString(request.Body, offset, numberLength),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture);
            offset += numberLength;
            var symbol = Encoding.ASCII
                .GetString(request.Body, offset, HexByteCharacterCount)
                .TrimEnd('*', ' ');
            offset += HexByteCharacterCount;
            return CreateAddress(options, symbol, number);
        }

        if (request.IsLegacy)
        {
            EnsureAvailable(request.Body, offset, LegacyBinaryDeviceFieldByteCount);
            var number = request.Body[offset]
                | (request.Body[offset + 1] << BitsPerByte)
                | (request.Body[offset + ProtocolWordByteCount] << BitsPerTwoBytes)
                | (request.Body[offset + ModernBinaryDeviceNumberByteCount] << BitsPerThreeBytes);
            offset += ModernBinaryDeviceFieldByteCount;
            var code = ReadLittleEndianUInt16(request.Body, ref offset);
            var metadata = MitsubishiDeviceAddress.Metadata.Values.FirstOrDefault(
                value => value.AsciiCode == code)
                ?? throw new InvalidDataException($"Unsupported legacy Mitsubishi device code 0x{code:X4}.");
            return CreateAddress(options, metadata.Symbol, number);
        }

        EnsureAvailable(request.Body, offset, ModernBinaryDeviceFieldByteCount);
        var modernNumber = request.Body[offset]
            | (request.Body[offset + 1] << BitsPerByte)
            | (request.Body[offset + ProtocolWordByteCount] << BitsPerTwoBytes);
        offset += ModernBinaryDeviceNumberByteCount;
        var binaryCode = request.Body[offset];
        offset++;
        var modernMetadata = MitsubishiDeviceAddress.Metadata.Values.FirstOrDefault(
            value => (byte)value.BinaryCode == binaryCode)
            ?? throw new InvalidDataException($"Unsupported Mitsubishi device code 0x{binaryCode:X2}.");
        return CreateAddress(options, modernMetadata.Symbol, modernNumber);
    }

    /// <summary>Creates a parsed-address equivalent from decoded protocol fields.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="symbol">The symbol parameter.</param>
    /// <param name="number">The number parameter.</param>
    /// <returns>The operation result.</returns>
    private MitsubishiDeviceAddress CreateAddress(
        MitsubishiClientOptions options,
        string symbol,
        int number) =>
        new(
            symbol,
            number,
            options.XyNotation,
            symbol + number.ToString(CultureInfo.InvariantCulture));

    /// <summary>Reads a batch point count.</summary>
    /// <param name="request">The request parameter.</param>
    /// <param name="offset">The offset parameter.</param>
    /// <returns>The operation result.</returns>
    private int ReadPointCount(
        DecodedSimulatorRequest request,
        ref int offset)
    {
        if (!request.IsLegacy)
        {
            return ReadUInt16(request, ref offset);
        }

        if (request.IsAscii)
        {
            EnsureAvailable(request.Body, offset, HexWordCharacterCount);
            var value = ParseHexByte(
                request.Body.AsSpan(offset, HexByteCharacterCount));
            offset += HexWordCharacterCount;
            return value == 0 ? MaximumLegacyPointCount : value;
        }

        EnsureAvailable(request.Body, offset, ProtocolWordByteCount);
        var points = request.Body[offset];
        offset += ProtocolWordByteCount;
        return points == 0 ? MaximumLegacyPointCount : points;
    }

    /// <summary>Reads a protocol UInt16.</summary>
    /// <param name="request">The request parameter.</param>
    /// <param name="offset">The offset parameter.</param>
    /// <returns>The operation result.</returns>
    private ushort ReadUInt16(DecodedSimulatorRequest request, ref int offset)
    {
        if (request.IsAscii)
        {
            EnsureAvailable(request.Body, offset, HexWordCharacterCount);
            var value = ParseHexUInt16(
                request.Body.AsSpan(offset, HexWordCharacterCount));
            offset += HexWordCharacterCount;
            return value;
        }

        return ReadLittleEndianUInt16(request.Body, ref offset);
    }

    /// <summary>Reads a random-device count and its reserved byte.</summary>
    /// <param name="request">The request parameter.</param>
    /// <param name="offset">The offset parameter.</param>
    /// <returns>The operation result.</returns>
    private int ReadRandomDeviceCount(DecodedSimulatorRequest request, ref int offset)
    {
        EnsureAvailable(
            request.Body,
            offset,
            request.IsAscii ? ProtocolWordByteCount * ProtocolWordByteCount : ProtocolWordByteCount);
        var count = request.IsAscii
            ? ParseHexByte(request.Body.AsSpan(offset, ProtocolWordByteCount))
            : request.Body[offset];
        offset += request.IsAscii
            ? ProtocolWordByteCount * ProtocolWordByteCount
            : ProtocolWordByteCount;
        return count;
    }

    /// <summary>Reads consecutive protocol word values.</summary>
    /// <param name="request">The request parameter.</param>
    /// <param name="offset">The offset parameter.</param>
    /// <param name="count">The count parameter.</param>
    /// <returns>The operation result.</returns>
    private ushort[] ReadWordValues(
        DecodedSimulatorRequest request,
        ref int offset,
        int count)
    {
        var values = new ushort[count];
        for (var index = 0; index < count; index++)
        {
            values[index] = ReadUInt16(request, ref offset);
        }

        return values;
    }

    /// <summary>Reads consecutive batch bit values.</summary>
    /// <param name="request">The request parameter.</param>
    /// <param name="offset">The offset parameter.</param>
    /// <param name="count">The count parameter.</param>
    /// <returns>The operation result.</returns>
    private bool[] ReadBitValues(
        DecodedSimulatorRequest request,
        ref int offset,
        int count)
    {
        var values = new bool[count];
        for (var index = 0; index < count; index++)
        {
            EnsureAvailable(request.Body, offset, 1);
            values[index] = request.IsAscii
                ? request.Body[offset] != (byte)'0'
                : request.Body[offset] != 0;
            offset++;
        }

        return values;
    }

    /// <summary>Reads consecutive block bit values.</summary>
    /// <param name="request">The request parameter.</param>
    /// <param name="offset">The offset parameter.</param>
    /// <param name="count">The count parameter.</param>
    /// <returns>The operation result.</returns>
    private bool[] ReadBlockBitValues(
        DecodedSimulatorRequest request,
        ref int offset,
        int count)
    {
        var values = new bool[count];
        for (var index = 0; index < count; index++)
        {
            if (request.IsAscii)
            {
                EnsureAvailable(request.Body, offset, HexByteCharacterCount);
                values[index] = request.Body[offset] != (byte)'0'
                    || request.Body[offset + 1] != (byte)'0';
                offset += HexByteCharacterCount;
            }
            else
            {
                EnsureAvailable(request.Body, offset, 1);
                values[index] = request.Body[offset] != 0;
                offset++;
            }
        }

        return values;
    }

    /// <summary>Encodes words for the connected data code.</summary>
    /// <param name="values">The values parameter.</param>
    /// <param name="dataCode">The dataCode parameter.</param>
    /// <returns>The operation result.</returns>
    private byte[] EncodeWords(
        ushort[] values,
        CommunicationDataCode dataCode)
    {
        if (dataCode == CommunicationDataCode.Ascii)
        {
            return Encoding.ASCII.GetBytes(
                string.Concat(values.Select(static value =>
                    value.ToString("X4", CultureInfo.InvariantCulture))));
        }

        var result = new byte[values.Length * ProtocolWordByteCount];
        for (var index = 0; index < values.Length; index++)
        {
            result[index * ProtocolWordByteCount] = (byte)(values[index] & 0xFF);
            result[(index * ProtocolWordByteCount) + 1] =
                (byte)(values[index] >> BitsPerByte);
        }

        return result;
    }

    /// <summary>Encodes packed batch bits for the connected data code.</summary>
    /// <param name="values">The values parameter.</param>
    /// <param name="dataCode">The dataCode parameter.</param>
    /// <returns>The operation result.</returns>
    private byte[] EncodeBits(
        bool[] values,
        CommunicationDataCode dataCode)
    {
        if (dataCode == CommunicationDataCode.Ascii)
        {
            var encoded = string.Concat(
                values.Select(static value => value ? '1' : '0'));
            if ((encoded.Length & 1) != 0)
            {
                encoded += '0';
            }

            return Encoding.ASCII.GetBytes(encoded);
        }

        var result = new byte[
            (values.Length + 1) / ProtocolWordByteCount];
        for (var index = 0; index < values.Length; index++)
        {
            if (values[index])
            {
                result[index / ProtocolWordByteCount] |=
                    (byte)(index % ProtocolWordByteCount == 0 ? 0x01 : 0x10);
            }
        }

        return result;
    }

    /// <summary>Encodes block bits for the connected data code.</summary>
    /// <param name="values">The values parameter.</param>
    /// <param name="dataCode">The dataCode parameter.</param>
    /// <returns>The operation result.</returns>
    private byte[] EncodeBlockBits(
        bool[] values,
        CommunicationDataCode dataCode) =>
        dataCode == CommunicationDataCode.Ascii
            ? Encoding.ASCII.GetBytes(
                string.Concat(values.Select(static value => value ? "10" : "00")))
            : values.Select(static value => value ? (byte)0x10 : (byte)0x00).ToArray();

    /// <summary>Reads a little-endian UInt16 and advances an offset.</summary>
    /// <param name="bytes">The bytes parameter.</param>
    /// <param name="offset">The offset parameter.</param>
    /// <returns>The operation result.</returns>
    private ushort ReadLittleEndianUInt16(ReadOnlySpan<byte> bytes, ref int offset)
    {
        EnsureAvailable(bytes, offset, ProtocolWordByteCount);
        var value = (ushort)(bytes[offset] | (bytes[offset + 1] << BitsPerByte));
        offset += ProtocolWordByteCount;
        return value;
    }

    /// <summary>Reads a little-endian UInt16 at a fixed offset.</summary>
    /// <param name="bytes">The bytes parameter.</param>
    /// <param name="offset">The offset parameter.</param>
    /// <returns>The operation result.</returns>
    private ushort ReadLittleEndianUInt16(ReadOnlySpan<byte> bytes, int offset)
    {
        EnsureAvailable(bytes, offset, ProtocolWordByteCount);
        return (ushort)(bytes[offset] | (bytes[offset + 1] << BitsPerByte));
    }

    /// <summary>Parses two hexadecimal ASCII characters.</summary>
    /// <param name="value">The value parameter.</param>
    /// <returns>The operation result.</returns>
    private byte ParseHexByte(ReadOnlySpan<byte> value) =>
        byte.Parse(Encoding.ASCII.GetString(value), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    /// <summary>Parses two hexadecimal characters.</summary>
    /// <param name="value">The value parameter.</param>
    /// <returns>The operation result.</returns>
    private byte ParseHexByte(ReadOnlySpan<char> value) =>
        byte.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    /// <summary>Parses four hexadecimal ASCII characters.</summary>
    /// <param name="value">The value parameter.</param>
    /// <returns>The operation result.</returns>
    private ushort ParseHexUInt16(ReadOnlySpan<byte> value) =>
        ushort.Parse(Encoding.ASCII.GetString(value), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    /// <summary>Parses four hexadecimal characters.</summary>
    /// <param name="value">The value parameter.</param>
    /// <returns>The operation result.</returns>
    private ushort ParseHexUInt16(ReadOnlySpan<char> value) =>
        ushort.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    /// <summary>Ensures a requested span is present in a protocol buffer.</summary>
    /// <param name="bytes">The bytes parameter.</param>
    /// <param name="offset">The offset parameter.</param>
    /// <param name="length">The length parameter.</param>
    private void EnsureAvailable(ReadOnlySpan<byte> bytes, int offset, int length)
    {
        if (offset >= 0 && length >= 0 && offset <= bytes.Length - length)
        {
            return;
        }

        throw new InvalidDataException("The Mitsubishi simulator received a truncated request frame.");
    }

    /// <summary>Represents one decoded protocol request.</summary>
    /// <param name="Command">The command code.</param>
    /// <param name="Subcommand">The subcommand code.</param>
    /// <param name="Body">The decoded request body.</param>
    /// <param name="IsAscii">Whether the request uses ASCII encoding.</param>
    /// <param name="IsLegacy">Whether the request uses legacy framing.</param>
    /// <param name="LegacyCommand">The optional legacy command byte.</param>
    private sealed record DecodedSimulatorRequest(
        ushort Command,
        ushort Subcommand,
        byte[] Body,
        bool IsAscii,
        bool IsLegacy,
        byte? LegacyCommand);

    /// <summary>Represents values decoded from one serial batch request.</summary>
    /// <param name="Values">The decoded values.</param>
    private sealed record SerialBatchRequest(ushort[] Values);
}
