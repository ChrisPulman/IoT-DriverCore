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

/// <summary>Provides protocol helper operations for the Mitsubishi simulator transport.</summary>
public sealed partial class MitsubishiSimulatorTransport
{
    /// <summary>Decodes one generated serial batch request.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="payload">The payload parameter.</param>
    /// <param name="address">The address parameter.</param>
    /// <param name="isRead">The isRead parameter.</param>
    /// <param name="isWord">The isWord parameter.</param>
    /// <returns>The operation result.</returns>
    private static SerialBatchRequest DecodeSerialBatch(
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
    private static SerialBatchRequest DecodeBinarySerialBatch(
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
        return new(values);
    }

    /// <summary>Decodes a generated ASCII 1C batch request.</summary>
    /// <param name="payload">The normalized request text.</param>
    /// <param name="address">The decoded device address.</param>
    /// <param name="isRead">Whether the operation reads device memory.</param>
    /// <param name="isWord">Whether the operation uses word units.</param>
    /// <returns>The decoded batch request.</returns>
    private static SerialBatchRequest DecodeOneCSerialBatch(
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
        return new(values);
    }

    /// <summary>Decodes a generated ASCII 3C or 4C batch request.</summary>
    /// <param name="frameType">The serial frame type.</param>
    /// <param name="payload">The normalized request text.</param>
    /// <param name="isRead">Whether the operation reads device memory.</param>
    /// <param name="isWord">Whether the operation uses word units.</param>
    /// <returns>The decoded batch request.</returns>
    private static SerialBatchRequest DecodeModernAsciiSerialBatch(
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
        return new(values);
    }

    /// <summary>Decodes an MC request frame.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="payload">The payload parameter.</param>
    /// <returns>The operation result.</returns>
    private static DecodedSimulatorRequest DecodeMcRequest(
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

        return new(
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
    private static DecodedSimulatorRequest DecodeLegacyMcRequest(
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
        return new(
            mapping.Item1,
            mapping.Item2,
            payload[bodyOffset..],
            isAscii,
            IsLegacy: true,
            legacyCommand);
    }

    /// <summary>Reads a protocol UInt16.</summary>
    /// <param name="request">The request parameter.</param>
    /// <param name="offset">The offset parameter.</param>
    /// <returns>The operation result.</returns>
    private static ushort ReadUInt16(DecodedSimulatorRequest request, ref int offset)
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
    private static int ReadRandomDeviceCount(DecodedSimulatorRequest request, ref int offset)
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
    private static ushort[] ReadWordValues(
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
    private static bool[] ReadBitValues(
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
    private static bool[] ReadBlockBitValues(
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

    /// <summary>Normalizes an ASCII serial request to its body text.</summary>
    /// <param name="payload">The payload parameter.</param>
    /// <returns>The operation result.</returns>
    private static string NormalizeSerialAsciiRequest(byte[] payload)
    {
        var bytes = new byte[payload.Length];
        var byteCount = 0;
        foreach (var value in payload)
        {
            if (value is not (byte)'\r' and not (byte)'\n')
            {
                bytes[byteCount] = value;
                byteCount++;
            }
        }

        if (byteCount < MinimumSerialAsciiFrameByteCount || bytes[0] != 0x05)
        {
            throw new InvalidDataException("The serial simulator received an invalid ASCII request frame.");
        }

        return Encoding.ASCII.GetString(bytes, 1, byteCount - SerialAsciiSuffixByteCount);
    }

    /// <summary>Creates a parsed-address equivalent from decoded protocol fields.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="symbol">The symbol parameter.</param>
    /// <param name="number">The number parameter.</param>
    /// <returns>The operation result.</returns>
    private static MitsubishiDeviceAddress CreateAddress(
        MitsubishiClientOptions options,
        string symbol,
        int number) =>
        new(
            symbol,
            number,
            options.XyNotation,
            symbol + number.ToString(CultureInfo.InvariantCulture));

    /// <summary>Encodes words for the connected data code.</summary>
    /// <param name="values">The values parameter.</param>
    /// <param name="dataCode">The dataCode parameter.</param>
    /// <returns>The operation result.</returns>
    private static byte[] EncodeWords(
        ushort[] values,
        CommunicationDataCode dataCode)
    {
        if (dataCode == CommunicationDataCode.Ascii)
        {
            var characters = new char[values.Length * HexWordCharacterCount];
            for (var index = 0; index < values.Length; index++)
            {
                _ = values[index].TryFormat(
                    characters.AsSpan(index * HexWordCharacterCount, HexWordCharacterCount),
                    out _,
                    "X4",
                    CultureInfo.InvariantCulture);
            }

            return Encoding.ASCII.GetBytes(characters);
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
    private static byte[] EncodeBits(
        bool[] values,
        CommunicationDataCode dataCode)
    {
        if (dataCode == CommunicationDataCode.Ascii)
        {
            var characterCount = values.Length + (values.Length & 1);
            var characters = new char[characterCount];
            for (var index = 0; index < values.Length; index++)
            {
                characters[index] = values[index] ? '1' : '0';
            }

            if ((values.Length & 1) != 0)
            {
                characters[^1] = '0';
            }

            return Encoding.ASCII.GetBytes(characters);
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
    private static byte[] EncodeBlockBits(
        bool[] values,
        CommunicationDataCode dataCode)
    {
        if (dataCode == CommunicationDataCode.Ascii)
        {
            var characters = new char[values.Length * ProtocolWordByteCount];
            for (var index = 0; index < values.Length; index++)
            {
                characters[index * ProtocolWordByteCount] = values[index] ? '1' : '0';
                characters[(index * ProtocolWordByteCount) + 1] = '0';
            }

            return Encoding.ASCII.GetBytes(characters);
        }

        var result = new byte[values.Length];
        for (var index = 0; index < values.Length; index++)
        {
            result[index] = values[index] ? (byte)0x10 : (byte)0x00;
        }

        return result;
    }

    /// <summary>Reads a little-endian UInt16 and advances an offset.</summary>
    /// <param name="bytes">The bytes parameter.</param>
    /// <param name="offset">The offset parameter.</param>
    /// <returns>The operation result.</returns>
    private static ushort ReadLittleEndianUInt16(ReadOnlySpan<byte> bytes, ref int offset)
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
    private static ushort ReadLittleEndianUInt16(ReadOnlySpan<byte> bytes, int offset)
    {
        EnsureAvailable(bytes, offset, ProtocolWordByteCount);
        return (ushort)(bytes[offset] | (bytes[offset + 1] << BitsPerByte));
    }

    /// <summary>Parses two hexadecimal ASCII characters.</summary>
    /// <param name="value">The value parameter.</param>
    /// <returns>The operation result.</returns>
    private static byte ParseHexByte(ReadOnlySpan<byte> value) =>
        byte.Parse(Encoding.ASCII.GetString(value), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    /// <summary>Parses two hexadecimal characters.</summary>
    /// <param name="value">The value parameter.</param>
    /// <returns>The operation result.</returns>
    private static byte ParseHexByte(ReadOnlySpan<char> value) =>
        byte.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    /// <summary>Parses four hexadecimal ASCII characters.</summary>
    /// <param name="value">The value parameter.</param>
    /// <returns>The operation result.</returns>
    private static ushort ParseHexUInt16(ReadOnlySpan<byte> value) =>
        ushort.Parse(Encoding.ASCII.GetString(value), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    /// <summary>Parses four hexadecimal characters.</summary>
    /// <param name="value">The value parameter.</param>
    /// <returns>The operation result.</returns>
    private static ushort ParseHexUInt16(ReadOnlySpan<char> value) =>
        ushort.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    /// <summary>Ensures a requested span is present in a protocol buffer.</summary>
    /// <param name="bytes">The bytes parameter.</param>
    /// <param name="offset">The offset parameter.</param>
    /// <param name="length">The length parameter.</param>
    private static void EnsureAvailable(ReadOnlySpan<byte> bytes, int offset, int length)
    {
        if (offset >= 0 && length >= 0 && offset <= bytes.Length - length)
        {
            return;
        }

        throw new InvalidDataException("The Mitsubishi simulator received a truncated request frame.");
    }

    /// <summary>Returns the echoed data from an MC loopback request.</summary>
    /// <param name="request">The decoded loopback request.</param>
    /// <returns>The loopback data without its request length prefix.</returns>
    private static byte[] ExecuteLoopback(DecodedSimulatorRequest request)
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

    /// <summary>Reads binary serial batch values.</summary>
    /// <param name="payload">The payload parameter.</param>
    /// <param name="offset">The offset parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="isWord">The isWord parameter.</param>
    /// <returns>The operation result.</returns>
    private static ushort[] ReadBinaryBatchValues(
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
    private static ushort[] ReadAsciiBatchValues(
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

    /// <summary>Reads a batch point count.</summary>
    /// <param name="request">The decoded request.</param>
    /// <param name="offset">The current body offset.</param>
    /// <returns>The requested point count.</returns>
    private static int ReadPointCount(
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

    /// <summary>Reads a device address from a decoded request body.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <param name="offset">The offset parameter.</param>
    /// <returns>The operation result.</returns>
    private static MitsubishiDeviceAddress ReadDeviceAddress(
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
            foreach (var metadata in MitsubishiDeviceAddress.Metadata.Values)
            {
                if (metadata.AsciiCode == code)
                {
                    return CreateAddress(options, metadata.Symbol, number);
                }
            }

            throw new InvalidDataException($"Unsupported legacy Mitsubishi device code 0x{code:X4}.");
        }

        EnsureAvailable(request.Body, offset, ModernBinaryDeviceFieldByteCount);
        var modernNumber = request.Body[offset]
            | (request.Body[offset + 1] << BitsPerByte)
            | (request.Body[offset + ProtocolWordByteCount] << BitsPerTwoBytes);
        offset += ModernBinaryDeviceNumberByteCount;
        var binaryCode = request.Body[offset];
        offset++;
        foreach (var metadata in MitsubishiDeviceAddress.Metadata.Values)
        {
            if ((byte)metadata.BinaryCode == binaryCode)
            {
                return CreateAddress(options, metadata.Symbol, modernNumber);
            }
        }

        throw new InvalidDataException($"Unsupported Mitsubishi device code 0x{binaryCode:X2}.");
    }
}
