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
    /// <summary>Executes the Encode4CBinaryRandomReadRequest operation.</summary>
    /// <param name = "serial">The serial parameter.</param>
    /// <param name = "addresses">The addresses parameter.</param>
    /// <returns>The Encode4CBinaryRandomReadRequest operation result.</returns>
    private static byte[] Encode4CBinaryRandomReadRequest(
        MitsubishiSerialOptions serial,
        IReadOnlyList<MitsubishiDeviceAddress> addresses)
    {
        var buffer = Create4CBinaryHeader(serial);
        AppendUInt16LittleEndian(buffer, 0x0403);
        AppendUInt16LittleEndian(buffer, 0x0000);
        AppendUInt16LittleEndian(buffer, checked((ushort)addresses.Count));
        AppendUInt16LittleEndian(buffer, 0x0000);
        foreach (var address in addresses)
        {
            AppendThreeByteLittleEndian(buffer, address.Number);
            buffer.Add((byte)address.Descriptor.BinaryCode);
        }

        return Finalize4CBinaryFrame(buffer);
    }

    /// <summary>Executes the Encode4CBinaryRandomWriteRequest operation.</summary>
    /// <param name = "serial">The serial parameter.</param>
    /// <param name = "values">The values parameter.</param>
    /// <returns>The Encode4CBinaryRandomWriteRequest operation result.</returns>
    private static byte[] Encode4CBinaryRandomWriteRequest(
        MitsubishiSerialOptions serial,
        IReadOnlyList<MitsubishiDeviceValue> values)
    {
        var buffer = Create4CBinaryHeader(serial);
        AppendUInt16LittleEndian(buffer, 0x1402);
        AppendUInt16LittleEndian(buffer, 0x0000);
        AppendUInt16LittleEndian(buffer, checked((ushort)values.Count));
        AppendUInt16LittleEndian(buffer, 0x0000);
        foreach (var value in values)
        {
            AppendThreeByteLittleEndian(buffer, value.Address.Number);
            buffer.Add((byte)value.Address.Descriptor.BinaryCode);
            AppendUInt16LittleEndian(buffer, value.Value);
        }

        return Finalize4CBinaryFrame(buffer);
    }

    /// <summary>Executes the Encode4CBinaryBlockReadRequest operation.</summary>
    /// <param name = "serial">The serial parameter.</param>
    /// <param name = "request">The request parameter.</param>
    /// <returns>The Encode4CBinaryBlockReadRequest operation result.</returns>
    private static byte[] Encode4CBinaryBlockReadRequest(
        MitsubishiSerialOptions serial,
        MitsubishiBlockRequest request)
    {
        var buffer = Create4CBinaryHeader(serial);
        AppendUInt16LittleEndian(buffer, 0x0406);
        AppendUInt16LittleEndian(buffer, 0x0000);
        AppendBlocksBinary(buffer, request, includeValues: false);
        return Finalize4CBinaryFrame(buffer);
    }

    /// <summary>Executes the Encode4CBinaryBlockWriteRequest operation.</summary>
    /// <param name = "serial">The serial parameter.</param>
    /// <param name = "request">The request parameter.</param>
    /// <returns>The Encode4CBinaryBlockWriteRequest operation result.</returns>
    private static byte[] Encode4CBinaryBlockWriteRequest(
        MitsubishiSerialOptions serial,
        MitsubishiBlockRequest request)
    {
        var buffer = Create4CBinaryHeader(serial);
        AppendUInt16LittleEndian(buffer, 0x1406);
        AppendUInt16LittleEndian(buffer, 0x0000);
        AppendBlocksBinary(buffer, request, includeValues: true);
        return Finalize4CBinaryFrame(buffer);
    }

    /// <summary>Executes the Encode4CBinaryMonitorRegistrationRequest operation.</summary>
    /// <param name = "serial">The serial parameter.</param>
    /// <param name = "addresses">The addresses parameter.</param>
    /// <returns>The Encode4CBinaryMonitorRegistrationRequest operation result.</returns>
    private static byte[] Encode4CBinaryMonitorRegistrationRequest(
        MitsubishiSerialOptions serial,
        IReadOnlyList<MitsubishiDeviceAddress> addresses)
    {
        var buffer = Create4CBinaryHeader(serial);
        AppendUInt16LittleEndian(buffer, 0x0801);
        AppendUInt16LittleEndian(buffer, 0x0000);
        AppendUInt16LittleEndian(buffer, checked((ushort)addresses.Count));
        AppendUInt16LittleEndian(buffer, 0x0000);
        foreach (var address in addresses)
        {
            AppendThreeByteLittleEndian(buffer, address.Number);
            buffer.Add((byte)address.Descriptor.BinaryCode);
        }

        return Finalize4CBinaryFrame(buffer);
    }

    /// <summary>Executes the Encode4CBinaryExecuteMonitorRequest operation.</summary>
    /// <param name = "serial">The serial parameter.</param>
    /// <returns>The Encode4CBinaryExecuteMonitorRequest operation result.</returns>
    private static byte[] Encode4CBinaryExecuteMonitorRequest(MitsubishiSerialOptions serial)
    {
        var buffer = Create4CBinaryHeader(serial);
        AppendUInt16LittleEndian(buffer, 0x0802);
        AppendUInt16LittleEndian(buffer, 0x0000);
        return Finalize4CBinaryFrame(buffer);
    }

    /// <summary>Executes the Encode4CBinaryRemoteOperationRequest operation.</summary>
    /// <param name = "serial">The serial parameter.</param>
    /// <param name = "command">The command parameter.</param>
    /// <param name = "force">The force parameter.</param>
    /// <param name = "clearMode">The clearMode parameter.</param>
    /// <returns>The Encode4CBinaryRemoteOperationRequest operation result.</returns>
    private static byte[] Encode4CBinaryRemoteOperationRequest(
        MitsubishiSerialOptions serial,
        ushort command,
        bool force,
        bool clearMode)
    {
        var buffer = Create4CBinaryHeader(serial);
        AppendUInt16LittleEndian(buffer, command);
        AppendUInt16LittleEndian(buffer, 0x0000);
        if (command == MitsubishiCommandCodes.RemoteRun)
        {
            AppendUInt16LittleEndian(buffer, force ? (ushort)0x0001 : (ushort)0x0000);
            AppendUInt16LittleEndian(buffer, clearMode ? (ushort)0x0001 : (ushort)0x0000);
        }

        return Finalize4CBinaryFrame(buffer);
    }

    /// <summary>Executes the Encode4CBinaryRawRequest operation.</summary>
    /// <param name = "serial">The serial parameter.</param>
    /// <param name = "command">The command parameter.</param>
    /// <param name = "subcommand">The subcommand parameter.</param>
    /// <param name = "body">The body parameter.</param>
    /// <returns>The Encode4CBinaryRawRequest operation result.</returns>
    private static byte[] Encode4CBinaryRawRequest(
        MitsubishiSerialOptions serial,
        ushort command,
        ushort subcommand,
        IReadOnlyList<byte> body)
    {
        var buffer = Create4CBinaryHeader(serial);
        AppendUInt16LittleEndian(buffer, command);
        AppendUInt16LittleEndian(buffer, subcommand);
        buffer.AddRange(body);
        return Finalize4CBinaryFrame(buffer);
    }

    /// <summary>Executes the Decode1C operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "response">The response parameter.</param>
    /// <returns>The Decode1C operation result.</returns>
    private static Responce<byte[]> Decode1C(MitsubishiClientOptions options, byte[] response)
    {
        var serial = options.ResolvedSerial;
        var text = ExtractAsciiPayload(serial.MessageFormat, response);
        var result = new Responce<byte[]> { Response = text };
        if (text.Length < 4)
        {
            return result.Fail("1C serial response too short.");
        }

        if (text[0] == (char)Nak)
        {
            var errorCode = text.Length >= 6 ? Convert.ToInt32(text[Four..(Four + Two)], Sixteen) : 0;
            return result.Fail($"PLC returned 1C serial error 0x{errorCode:X2}.", errorCode);
        }

        var payloadStart = text[0] == (char)Ack ? Three : Four;
        result.Value = Encoding.ASCII.GetBytes(text[payloadStart..]);
        return result.EndTime();
    }

    /// <summary>Executes the Decode3C operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "response">The response parameter.</param>
    /// <returns>The Decode3C operation result.</returns>
    private static Responce<byte[]> Decode3C(MitsubishiClientOptions options, byte[] response)
    {
        var serial = options.ResolvedSerial;
        var text = ExtractAsciiPayload(serial.MessageFormat, response);
        var result = new Responce<byte[]> { Response = text };
        if (text.Length < 6)
        {
            return result.Fail("3C serial response too short.");
        }

        if (text[0] == (char)Nak)
        {
            var errorCode = text.Length >= 8 ? Convert.ToInt32(text[Six..(Six + Two)], Sixteen) : 0;
            return result.Fail($"PLC returned 3C serial error 0x{errorCode:X2}.", errorCode);
        }

        var payloadStart = text[0] == (char)Ack ? Five : Six;
        result.Value = Encoding.ASCII.GetBytes(text[payloadStart..]);
        return result.EndTime();
    }

    /// <summary>Executes the Decode4C operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "response">The response parameter.</param>
    /// <returns>The Decode4C operation result.</returns>
    private static Responce<byte[]> Decode4C(MitsubishiClientOptions options, byte[] response)
    {
        var serial = options.ResolvedSerial;
        return serial.MessageFormat == MitsubishiSerialMessageFormat.Format5
            ? Decode4CBinary(response)
            : Decode4CAscii(serial.MessageFormat, response);
    }

    /// <summary>Executes the Decode4CAscii operation.</summary>
    /// <param name = "messageFormat">The messageFormat parameter.</param>
    /// <param name = "response">The response parameter.</param>
    /// <returns>The Decode4CAscii operation result.</returns>
    private static Responce<byte[]> Decode4CAscii(
        MitsubishiSerialMessageFormat messageFormat,
        byte[] response)
    {
        var text = ExtractAsciiPayload(messageFormat, response);
        var result = new Responce<byte[]> { Response = text };
        if (text.Length < 12)
        {
            return result.Fail("4C serial ASCII response too short.");
        }

        if (text[0] == (char)Nak)
        {
            var errorCode = text.Length >= 14 ? Convert.ToInt32(text[Twelve..(Twelve + Two)], Sixteen) : 0;
            return result.Fail($"PLC returned 4C serial ASCII error 0x{errorCode:X2}.", errorCode);
        }

        var payloadStart = text[0] == (char)Ack ? Eleven : Twelve;
        result.Value = Encoding.ASCII.GetBytes(text[payloadStart..]);
        return result.EndTime();
    }

    /// <summary>Executes the Decode4CBinary operation.</summary>
    /// <param name = "response">The response parameter.</param>
    /// <returns>The Decode4CBinary operation result.</returns>
    private static Responce<byte[]> Decode4CBinary(byte[] response)
    {
        var result = new Responce<byte[]> { Response = Convert.ToHexString(response) };
        if (!HasBinaryFormat5Message(response))
        {
            return result.Fail("4C serial binary response is incomplete.");
        }

        if (response.Length < 17)
        {
            return result.Fail("4C serial binary response too short.");
        }

        const int payloadStart = 14;
        var responseId = BitConverter.ToUInt16(response, Twelve);
        if (responseId != ResponseIdBinary)
        {
            return result.Fail("4C serial binary response has an invalid response identifier.");
        }

        var endCode = BitConverter.ToUInt16(response, payloadStart);
        if (endCode != 0)
        {
            return result.Fail($"PLC returned 4C serial binary error 0x{endCode:X4}.", endCode);
        }

        const int dataStart = payloadStart + 2;
        var dataLength = response.Length - dataStart - Four;
        result.Value =
            dataLength <= 0 ? Array.Empty<byte>() : response[dataStart..(dataStart + dataLength)];
        return result.EndTime();
    }

    /// <summary>Executes the EnsureAscii operation.</summary>
    /// <param name = "options">The options parameter.</param>
    private static void EnsureAscii(MitsubishiClientOptions options)
    {
        if (options.DataCode == CommunicationDataCode.Ascii)
        {
            return;
        }

        throw new NotSupportedException(
            "1C and 3C serial communication require ASCII data encoding.");
    }

    /// <summary>Executes the Format1CAddress operation.</summary>
    /// <param name = "address">The address parameter.</param>
    /// <param name = "metadata">The metadata parameter.</param>
    /// <returns>The Format1CAddress operation result.</returns>
    private static string Format1CAddress(
        MitsubishiDeviceAddress address,
        MitsubishiDeviceMetadata metadata)
    {
        var digits = metadata.Symbol.Length > 1 ? Three : Four;
        return metadata.Symbol
            + address.Number.ToString($"D{digits}", CultureInfo.InvariantCulture);
    }

    /// <summary>Executes the FormatDeviceAddressModern operation.</summary>
    /// <param name = "address">The address parameter.</param>
    /// <param name = "metadata">The metadata parameter.</param>
    /// <returns>The FormatDeviceAddressModern operation result.</returns>
    private static string FormatDeviceAddressModern(
        MitsubishiDeviceAddress address,
        MitsubishiDeviceMetadata metadata) =>
        address.Number.ToString("X6", CultureInfo.InvariantCulture)
        + metadata.Symbol.PadRight(Two, '*');

    /// <summary>Executes the FormatPointCount operation.</summary>
    /// <param name = "points">The points parameter.</param>
    /// <returns>The FormatPointCount operation result.</returns>
    private static string FormatPointCount(int points) =>
        (points == TwoHundredFiftySix ? 0 : points).ToString("X2", CultureInfo.InvariantCulture);

    /// <summary>Executes the FormatWordValuesAscii operation.</summary>
    /// <param name = "values">The values parameter.</param>
    /// <returns>The FormatWordValuesAscii operation result.</returns>
    private static string FormatWordValuesAscii(IEnumerable<ushort> values) =>
        string.Concat(
            values.Select(static value => value.ToString("X4", CultureInfo.InvariantCulture)));

    /// <summary>Executes the FormatBitValuesAscii operation.</summary>
    /// <param name = "values">The values parameter.</param>
    /// <returns>The FormatBitValuesAscii operation result.</returns>
    private static string FormatBitValuesAscii(IEnumerable<bool> values) =>
        string.Concat(values.Select(static value => value ? '1' : '0'));
}
