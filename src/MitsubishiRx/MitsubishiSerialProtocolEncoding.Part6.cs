// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Globalization;
using System.Text;

#if REACTIVE_SHIM

using static MitsubishiRx.Reactive.MitsubishiNumericConstants;

namespace MitsubishiRx.Reactive;

#else

using static MitsubishiRx.MitsubishiNumericConstants;

namespace MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiSerialProtocolEncoding type.</summary>
internal static partial class MitsubishiSerialProtocolEncoding
{
    /// <summary>Executes the FormatBlocksAscii operation.</summary>
    /// <param name = "request">The request parameter.</param>
    /// <param name = "includeValues">The includeValues parameter.</param>
    /// <returns>The FormatBlocksAscii operation result.</returns>
    private static string FormatBlocksAscii(MitsubishiBlockRequest request, bool includeValues)
    {
        var builder = new StringBuilder();
        _ = builder.Append(FormatAsciiUInt16(checked((ushort)request.ResolvedWordBlocks.Count)));
        _ = builder.Append(FormatAsciiUInt16(checked((ushort)request.ResolvedBitBlocks.Count)));
        foreach (var block in request.ResolvedWordBlocks)
        {
            _ = builder.Append(FormatDeviceAddressModern(block.Address, block.Address.Descriptor));
            _ = builder.Append(FormatAsciiUInt16(checked((ushort)block.Values.Length)));
            if (includeValues)
            {
                _ = builder.Append(FormatWordValuesAscii(block.Values.ToArray()));
            }
        }

        foreach (var block in request.ResolvedBitBlocks)
        {
            _ = builder.Append(FormatDeviceAddressModern(block.Address, block.Address.Descriptor));
            _ = builder.Append(FormatAsciiUInt16(checked((ushort)block.Values.Length)));
            if (includeValues)
            {
                _ = builder.Append(
                    string.Concat(
                        block.Values.ToArray().Select(static value => value ? "10" : "00")));
            }
        }

        return builder.ToString();
    }

    /// <summary>Executes the FormatRemoteOperationPayloadAscii operation.</summary>
    /// <param name = "command">The command parameter.</param>
    /// <param name = "force">The force parameter.</param>
    /// <param name = "clearMode">The clearMode parameter.</param>
    /// <returns>The FormatRemoteOperationPayloadAscii operation result.</returns>
    private static string FormatRemoteOperationPayloadAscii(
        ushort command,
        bool force,
        bool clearMode) =>
        command == MitsubishiCommandCodes.RemoteRun
            ? (force ? "0001" : "0000") + (clearMode ? "0001" : "0000")
            : string.Empty;

    /// <summary>Executes the BuildLoopbackBody operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "data">The data parameter.</param>
    /// <returns>The BuildLoopbackBody operation result.</returns>
    private static byte[] BuildLoopbackBody(
        MitsubishiClientOptions options,
        ReadOnlySpan<byte> data)
    {
        if (options.DataCode == CommunicationDataCode.Ascii)
        {
            return Encoding.ASCII.GetBytes(
                FormatAsciiUInt16(checked((ushort)data.Length)) + Encoding.ASCII.GetString(data));
        }

        var buffer = new List<byte>();
        AppendUInt16LittleEndian(buffer, checked((ushort)data.Length));
        buffer.AddRange(data.ToArray());
        return buffer.ToArray();
    }

    /// <summary>Executes the BuildMemoryAccessBody operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "command">The command parameter.</param>
    /// <param name = "address">The address parameter.</param>
    /// <param name = "length">The length parameter.</param>
    /// <param name = "values">The values parameter.</param>
    /// <returns>The BuildMemoryAccessBody operation result.</returns>
    private static byte[] BuildMemoryAccessBody(
        MitsubishiClientOptions options,
        ushort command,
        ushort address,
        int length,
        ReadOnlySpan<ushort> values)
    {
        var buffer = new List<byte>();
        if (options.DataCode == CommunicationDataCode.Ascii)
        {
            buffer.AddRange(Encoding.ASCII.GetBytes(FormatAsciiUInt16(address)));
            buffer.AddRange(Encoding.ASCII.GetBytes(FormatAsciiUInt16(checked((ushort)length))));
            if (command is MitsubishiCommandCodes.MemoryWrite or MitsubishiCommandCodes.ExtendUnitWrite)
            {
                buffer.AddRange(Encoding.ASCII.GetBytes(FormatWordValuesAscii(values.ToArray())));
            }

            return buffer.ToArray();
        }

        AppendUInt16LittleEndian(buffer, address);
        AppendUInt16LittleEndian(buffer, checked((ushort)length));
        if (command is MitsubishiCommandCodes.MemoryWrite or MitsubishiCommandCodes.ExtendUnitWrite)
        {
            AppendWordsLittleEndian(buffer, values.ToArray());
        }

        return buffer.ToArray();
    }

    /// <summary>Executes the Format3CAsciiHeader operation.</summary>
    /// <param name = "serial">The serial parameter.</param>
    /// <returns>The Format3CAsciiHeader operation result.</returns>
    private static string Format3CAsciiHeader(MitsubishiSerialOptions serial) =>
        FormatAsciiByte(ThreeCFrameId)
        + FormatAsciiByte(serial.StationNumber)
        + FormatAsciiByte(serial.NetworkNumber)
        + FormatAsciiByte(serial.PcNumber)
        + FormatAsciiByte(serial.SelfStationNumber);

    /// <summary>Executes the Format4CAsciiHeader operation.</summary>
    /// <param name = "serial">The serial parameter.</param>
    /// <returns>The Format4CAsciiHeader operation result.</returns>
    private static string Format4CAsciiHeader(MitsubishiSerialOptions serial) =>
        FormatAsciiByte(FourCFrameId)
        + FormatAsciiByte(serial.StationNumber)
        + FormatAsciiByte(serial.NetworkNumber)
        + FormatAsciiByte(serial.PcNumber)
        + FormatAsciiUInt16(serial.RequestDestinationModuleIoNumber)
        + FormatAsciiByte(serial.RequestDestinationModuleStationNumber)
        + FormatAsciiByte(serial.SelfStationNumber);

    /// <summary>Executes the FormatAsciiByte operation.</summary>
    /// <param name = "value">The value parameter.</param>
    /// <returns>The FormatAsciiByte operation result.</returns>
    private static string FormatAsciiByte(byte value) =>
        value.ToString("X2", CultureInfo.InvariantCulture);

    /// <summary>Executes the FormatAsciiNibble operation.</summary>
    /// <param name = "value">The value parameter.</param>
    /// <returns>The FormatAsciiNibble operation result.</returns>
    private static string FormatAsciiNibble(byte value) =>
        (value & 0x0F).ToString("X1", CultureInfo.InvariantCulture);

    /// <summary>Executes the FormatAsciiUInt16 operation.</summary>
    /// <param name = "value">The value parameter.</param>
    /// <returns>The FormatAsciiUInt16 operation result.</returns>
    private static string FormatAsciiUInt16(ushort value) =>
        value.ToString("X4", CultureInfo.InvariantCulture);

    /// <summary>Executes the WrapAscii operation.</summary>
    /// <param name = "body">The body parameter.</param>
    /// <param name = "format">The format parameter.</param>
    /// <returns>The WrapAscii operation result.</returns>
    private static byte[] WrapAscii(string body, MitsubishiSerialMessageFormat format)
    {
        var checksum = Encoding.ASCII.GetBytes(ComputeChecksum(Encoding.ASCII.GetBytes(body)));
        return format switch
        {
            MitsubishiSerialMessageFormat.Format1 =>
            [
                Enq,
                .. Encoding.ASCII.GetBytes(body),
                .. checksum,],
            MitsubishiSerialMessageFormat.Format4 =>
            [
                Cr,
                Lf,
                Enq,
                .. Encoding.ASCII.GetBytes(body),
                .. checksum,
                Cr,
                Lf,],
            _ => throw new NotSupportedException(
                $"ASCII wrapping is not supported for serial format '{format}'."),
        };
    }

    /// <summary>Executes the ExtractAsciiPayload operation.</summary>
    /// <param name = "format">The format parameter.</param>
    /// <param name = "response">The response parameter.</param>
    /// <returns>The ExtractAsciiPayload operation result.</returns>
    private static string ExtractAsciiPayload(MitsubishiSerialMessageFormat format, byte[] response)
    {
        var trimmed =
            format == MitsubishiSerialMessageFormat.Format4
                ? response.Where(static value => value is not Cr and not Lf).ToArray()
                : response;
        if (trimmed.Length < 3)
        {
            return string.Empty;
        }

        var withoutChecksum = trimmed[..^Two];
        return Encoding.ASCII.GetString(withoutChecksum);
    }

    /// <summary>Executes the HasAsciiChecksumFramedMessage operation.</summary>
    /// <param name = "buffer">The buffer parameter.</param>
    /// <returns>The HasAsciiChecksumFramedMessage operation result.</returns>
    private static bool HasAsciiChecksumFramedMessage(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 5)
        {
            return false;
        }

        if (buffer[0] == Cr)
        {
            var trimmed = buffer
                .ToArray()
                .Where(static value => value is not Cr and not Lf)
                .ToArray();
            return trimmed.Length >= 5
                && (
                    trimmed[0] == Enq || trimmed[0] == Stx || trimmed[0] == Ack || trimmed[0] == Nak);
        }

        return buffer[0] == Enq || buffer[0] == Stx || buffer[0] == Ack || buffer[0] == Nak;
    }

    /// <summary>Executes the HasBinaryFormat5Message operation.</summary>
    /// <param name = "buffer">The buffer parameter.</param>
    /// <returns>The HasBinaryFormat5Message operation result.</returns>
    private static bool HasBinaryFormat5Message(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 8 || buffer[0] != Dle || buffer[1] != Stx)
        {
            return false;
        }

        var byteCount = buffer[2] | (buffer[3] << 8);
        var expectedLength = Four + byteCount + Two + Two;
        return buffer.Length >= expectedLength;
    }

    /// <summary>Executes the ComputeChecksum operation.</summary>
    /// <param name = "bytes">The bytes parameter.</param>
    /// <returns>The ComputeChecksum operation result.</returns>
    private static string ComputeChecksum(IEnumerable<byte> bytes)
    {
        var sum = bytes.Aggregate(0, static (current, value) => current + value);
        return (sum & 0xFF).ToString("X2", CultureInfo.InvariantCulture);
    }

    /// <summary>Executes the AppendUInt16LittleEndian operation.</summary>
    /// <param name = "buffer">The buffer parameter.</param>
    /// <param name = "value">The value parameter.</param>
    private static void AppendUInt16LittleEndian(List<byte> buffer, ushort value)
    {
        buffer.Add((byte)(value & 0xFF));
        buffer.Add((byte)(value >> 8));
    }

    /// <summary>Executes the AppendThreeByteLittleEndian operation.</summary>
    /// <param name = "buffer">The buffer parameter.</param>
    /// <param name = "value">The value parameter.</param>
    private static void AppendThreeByteLittleEndian(List<byte> buffer, int value)
    {
        buffer.Add((byte)(value & 0xFF));
        buffer.Add((byte)((value >> 8) & 0xFF));
        buffer.Add((byte)((value >> 16) & 0xFF));
    }

    /// <summary>Executes the AppendWordsLittleEndian operation.</summary>
    /// <param name = "buffer">The buffer parameter.</param>
    /// <param name = "values">The values parameter.</param>
    private static void AppendWordsLittleEndian(List<byte> buffer, IEnumerable<ushort> values)
    {
        foreach (var value in values)
        {
            AppendUInt16LittleEndian(buffer, value);
        }
    }

    /// <summary>Executes the AppendBlocksBinary operation.</summary>
    /// <param name = "buffer">The buffer parameter.</param>
    /// <param name = "request">The request parameter.</param>
    /// <param name = "includeValues">The includeValues parameter.</param>
    private static void AppendBlocksBinary(
        List<byte> buffer,
        MitsubishiBlockRequest request,
        bool includeValues)
    {
        AppendUInt16LittleEndian(buffer, checked((ushort)request.ResolvedWordBlocks.Count));
        AppendUInt16LittleEndian(buffer, checked((ushort)request.ResolvedBitBlocks.Count));
        foreach (var block in request.ResolvedWordBlocks)
        {
            AppendThreeByteLittleEndian(buffer, block.Address.Number);
            buffer.Add((byte)block.Address.Descriptor.BinaryCode);
            AppendUInt16LittleEndian(buffer, checked((ushort)block.Values.Length));
            if (includeValues)
            {
                AppendWordsLittleEndian(buffer, block.Values.ToArray());
            }
        }

        foreach (var block in request.ResolvedBitBlocks)
        {
            AppendThreeByteLittleEndian(buffer, block.Address.Number);
            buffer.Add((byte)block.Address.Descriptor.BinaryCode);
            AppendUInt16LittleEndian(buffer, checked((ushort)block.Values.Length));
            if (includeValues)
            {
                AppendBitsBinary(buffer, block.Values.ToArray());
            }
        }
    }

    /// <summary>Executes the Create4CBinaryHeader operation.</summary>
    /// <param name = "serial">The serial parameter.</param>
    /// <returns>The Create4CBinaryHeader operation result.</returns>
    private static List<byte> Create4CBinaryHeader(MitsubishiSerialOptions serial) =>
        [
            FourCFrameId,
            serial.StationNumber,
            serial.NetworkNumber,
            serial.PcNumber,
            (byte)(serial.RequestDestinationModuleIoNumber & 0xFF),
            (byte)(serial.RequestDestinationModuleIoNumber >> 8),
            serial.RequestDestinationModuleStationNumber,
            serial.SelfStationNumber,];

    /// <summary>Executes the Finalize4CBinaryFrame operation.</summary>
    /// <param name = "buffer">The buffer parameter.</param>
    /// <returns>The Finalize4CBinaryFrame operation result.</returns>
    private static byte[] Finalize4CBinaryFrame(List<byte> buffer)
    {
        buffer.Add(Dle);
        buffer.Add(Etx);
        var numberOfDataBytes = checked((ushort)(buffer.Count - Two));
        var prefix = new List<byte> { Dle, Stx };
        AppendUInt16LittleEndian(prefix, numberOfDataBytes);
        prefix.AddRange(buffer);
        var checksum = ComputeChecksum(prefix.Skip(Two).Take(Two + numberOfDataBytes));
        prefix.AddRange(Encoding.ASCII.GetBytes(checksum));
        return prefix.ToArray();
    }

    /// <summary>Executes the AppendBitsBinary operation.</summary>
    /// <param name = "buffer">The buffer parameter.</param>
    /// <param name = "values">The values parameter.</param>
    private static void AppendBitsBinary(List<byte> buffer, IEnumerable<bool> values)
    {
        foreach (var value in values)
        {
            buffer.Add(value ? (byte)0x10 : (byte)0x00);
        }
    }
}
