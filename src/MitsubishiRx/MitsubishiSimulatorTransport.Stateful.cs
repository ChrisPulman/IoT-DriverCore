// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

#if REACTIVE_SHIM

namespace IoT.Driver.MitsubishiRx.Reactive;

#else

namespace IoT.Driver.MitsubishiRx;

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

        var words = new ushort[addresses.Length];
        for (var index = 0; index < addresses.Length; index++)
        {
            words[index] = Memory.ReadWords(addresses[index], 1)[0];
        }

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

        var bits = new bool[values.Length];
        for (var index = 0; index < values.Length; index++)
        {
            bits[index] = values[index] != 0;
        }

        Memory.WriteBits(address, bits);
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
