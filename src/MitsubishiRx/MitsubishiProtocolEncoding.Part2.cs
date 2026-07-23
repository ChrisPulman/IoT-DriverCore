// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

#if REACTIVE_SHIM

using static IoT.DriverCore.MitsubishiRx.Reactive.MitsubishiNumericConstants;

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

using static IoT.DriverCore.MitsubishiRx.MitsubishiNumericConstants;

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiProtocolEncoding type.</summary>
internal static partial class MitsubishiProtocolEncoding
{
    /// <summary>Executes the EncodeRemotePassword operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="command">The command parameter.</param>
    /// <param name="password">The password parameter.</param>
    /// <returns>The EncodeRemotePassword operation result.</returns>
    internal static byte[] EncodeRemotePassword(
        MitsubishiClientOptions options,
        ushort command,
        string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        var body =
            options.DataCode == CommunicationDataCode.Binary
                ? BuildBinaryPasswordPayload(password)
                : BuildAsciiPasswordPayload(password);
        return Encode(
            options,
            new MitsubishiRawCommandRequest(
                command,
                0x0000,
                body,
                command == MitsubishiCommandCodes.Unlock ? "Unlock" : "Lock"));
    }

    /// <summary>Executes the EncodeMemoryAccess operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="command">The command parameter.</param>
    /// <param name="address">The address parameter.</param>
    /// <param name="length">The length parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <returns>The EncodeMemoryAccess operation result.</returns>
    internal static byte[] EncodeMemoryAccess(
        MitsubishiClientOptions options,
        ushort command,
        ushort address,
        int length,
        ReadOnlySpan<ushort> values)
    {
        if (options.FrameType == MitsubishiFrameType.OneE)
        {
            throw new NotSupportedException(
                "1E memory / extend unit access is not implemented in this release.");
        }

        var list = new List<byte>();
        AppendUInt16(list, address, options.DataCode);
        AppendUInt16(list, checked((ushort)length), options.DataCode);
        if (command is MitsubishiCommandCodes.MemoryWrite or MitsubishiCommandCodes.ExtendUnitWrite)
        {
            foreach (var value in values)
            {
                AppendUInt16(list, value, options.DataCode);
            }
        }

        return Encode(
            options,
            new MitsubishiRawCommandRequest(command, 0x0000, list, $"Memory op {command:X4}"));
    }

    /// <summary>Executes the Encode1E operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <returns>The Encode1E operation result.</returns>
    private static byte[] Encode1E(
        MitsubishiClientOptions options,
        MitsubishiRawCommandRequest request)
    {
        var body = request.ResolvedBody;
        var buffer = new List<byte>();
        AppendByte(buffer, Get1ECommand(request.Command, request.Subcommand), options.DataCode);
        AppendByte(buffer, options.LegacyPcNumber, options.DataCode);
        AppendUInt16(buffer, options.MonitoringTimer, options.DataCode);
        buffer.AddRange(body);
        return buffer.ToArray();
    }

    /// <summary>Executes the Encode3E operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <returns>The Encode3E operation result.</returns>
    private static byte[] Encode3E(
        MitsubishiClientOptions options,
        MitsubishiRawCommandRequest request)
    {
        var body = request.ResolvedBody;
        var buffer = new List<byte>();
        Append3EOr4ESubheader(
            buffer,
            options.DataCode,
            fourE: false,
            options.GetNextSerialNumber());
        AppendRoute(buffer, options.ResolvedRoute, options.DataCode);
        AppendUInt16(buffer, checked((ushort)(Two + Two + Two + body.Count)), options.DataCode);
        AppendUInt16(buffer, options.MonitoringTimer, options.DataCode);
        AppendUInt16(buffer, request.Command, options.DataCode);
        AppendUInt16(buffer, request.Subcommand, options.DataCode);
        buffer.AddRange(body);
        return buffer.ToArray();
    }

    /// <summary>Executes the Encode4E operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <returns>The Encode4E operation result.</returns>
    private static byte[] Encode4E(
        MitsubishiClientOptions options,
        MitsubishiRawCommandRequest request)
    {
        var body = request.ResolvedBody;
        var buffer = new List<byte>();
        Append3EOr4ESubheader(buffer, options.DataCode, fourE: true, options.GetNextSerialNumber());
        AppendRoute(buffer, options.ResolvedRoute, options.DataCode);
        AppendUInt16(buffer, checked((ushort)(Two + Two + Two + body.Count)), options.DataCode);
        AppendUInt16(buffer, options.MonitoringTimer, options.DataCode);
        AppendUInt16(buffer, request.Command, options.DataCode);
        AppendUInt16(buffer, request.Subcommand, options.DataCode);
        buffer.AddRange(body);
        return buffer.ToArray();
    }

    /// <summary>Executes the Decode1E operation.</summary>
    /// <param name="response">The response parameter.</param>
    /// <returns>The Decode1E operation result.</returns>
    private static Responce<byte[]> Decode1E(byte[] response)
    {
        var result = new Responce<byte[]> { Response = Convert.ToHexString(response) };
        if (response.Length < 2)
        {
            return result.Fail("1E response too short.");
        }

        var endCode = response[1];
        if (endCode == 0x5B)
        {
            var errorCode =
                response.Length >= 4
                    ? BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(Two, Two))
                    : (ushort)0;
            return result.Fail($"PLC returned 1E error 0x{errorCode:X4}.", errorCode);
        }

        result.Value = response.Length > Two ? response[Two..] : Array.Empty<byte>();
        return result.EndTime();
    }

    /// <summary>Executes the Decode3EOr4E operation.</summary>
    /// <param name="response">The response parameter.</param>
    /// <returns>The Decode3EOr4E operation result.</returns>
    private static Responce<byte[]> Decode3EOr4E(byte[] response)
    {
        var result = new Responce<byte[]> { Response = Convert.ToHexString(response) };
        if (response.Length < 11)
        {
            return result.Fail("3E/4E response too short.");
        }

        var isFourE = response[0] == 0xD4;
        var offset = isFourE ? Six : Two;
        if (response.Length < offset + Nine)
        {
            return result.Fail("3E/4E response header incomplete.");
        }

        var responseDataLength = BinaryPrimitives.ReadUInt16LittleEndian(
            response.AsSpan(offset + Five, Two));
        var endCode = BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(offset + Seven, Two));
        var payloadLength = Math.Max(0, responseDataLength - Two);
        var payloadStart = offset + Nine;
        if (response.Length < payloadStart + payloadLength)
        {
            return result.Fail("3E/4E response payload truncated.");
        }

        if (endCode != 0)
        {
            result.ErrCode = endCode;
            return result.Fail($"PLC returned error 0x{endCode:X4}.", endCode);
        }

        result.Value =
            payloadLength == 0
                ? Array.Empty<byte>()
                : response[payloadStart..(payloadStart + payloadLength)];
        return result.EndTime();
    }

    /// <summary>Executes the Decode1EAscii operation.</summary>
    /// <param name="response">The response parameter.</param>
    /// <returns>The Decode1EAscii operation result.</returns>
    private static Responce<byte[]> Decode1EAscii(byte[] response)
    {
        var text = Encoding.ASCII.GetString(response);
        var result = new Responce<byte[]> { Response = text };
        if (text.Length < 4)
        {
            return result.Fail("1E ASCII response too short.");
        }

        var endCode = text[Two..(Two + Two)];
        if (string.Equals(endCode, "5B", StringComparison.OrdinalIgnoreCase))
        {
            var errorCode = text.Length >= 8 ? Convert.ToInt32(text[Four..(Four + Four)], Sixteen) : 0;
            return result.Fail($"PLC returned 1E ASCII error 0x{errorCode:X4}.", errorCode);
        }

        result.Value = text.Length > Four ? Encoding.ASCII.GetBytes(text[Four..]) : Array.Empty<byte>();
        return result.EndTime();
    }

    /// <summary>Executes the Decode3EOr4EAscii operation.</summary>
    /// <param name="response">The response parameter.</param>
    /// <returns>The Decode3EOr4EAscii operation result.</returns>
    private static Responce<byte[]> Decode3EOr4EAscii(byte[] response)
    {
        var text = Encoding.ASCII.GetString(response);
        var result = new Responce<byte[]> { Response = text };
        var isFourE = text.StartsWith("D4", StringComparison.OrdinalIgnoreCase);
        var expectedPrefixLength = isFourE ? Thirty : TwentyTwo;
        if (text.Length < expectedPrefixLength)
        {
            return result.Fail("3E/4E ASCII response too short.");
        }

        var responseDataLength = Convert.ToInt32(text.Substring(isFourE ? TwentyTwo : Fourteen, Four), Sixteen);
        var endCode = Convert.ToInt32(text.Substring(isFourE ? TwentySix : Eighteen, Four), Sixteen);
        var payloadLength = Math.Max(0, responseDataLength - Two) * Two;
        var payloadStart = isFourE ? Thirty : TwentyTwo;
        var remaining = text.Length - payloadStart;
        payloadStart = AdjustAsciiPayloadStart(payloadStart, remaining, payloadLength);

        if (text.Length < payloadStart + payloadLength)
        {
            return result.Fail("3E/4E ASCII response payload truncated.");
        }

        if (endCode != 0)
        {
            result.ErrCode = endCode;
            return result.Fail($"PLC returned ASCII error 0x{endCode:X4}.", endCode);
        }

        result.Value =
            payloadLength == 0
                ? Array.Empty<byte>()
                : Encoding.ASCII.GetBytes(text[payloadStart..(payloadStart + payloadLength)]);
        return result.EndTime();
    }

    /// <summary>Executes the AdjustAsciiPayloadStart operation.</summary>
    /// <param name="payloadStart">The payloadStart parameter.</param>
    /// <param name="remaining">The remaining parameter.</param>
    /// <param name="payloadLength">The payloadLength parameter.</param>
    /// <returns>The AdjustAsciiPayloadStart operation result.</returns>
    private static int AdjustAsciiPayloadStart(int payloadStart, int remaining, int payloadLength)
    {
        if (remaining == payloadLength - Four)
        {
            return payloadStart - Four;
        }

        return remaining == payloadLength + Four ? payloadStart + Four : payloadStart;
    }

    /// <summary>Executes the GetFixedResponseLength1E operation.</summary>
    /// <param name="command">The command parameter.</param>
    /// <param name="requestBodyLength">The requestBodyLength parameter.</param>
    /// <param name="dataCode">The dataCode parameter.</param>
    /// <returns>The GetFixedResponseLength1E operation result.</returns>
    private static int? GetFixedResponseLength1E(
        ushort command,
        int requestBodyLength,
        CommunicationDataCode dataCode) =>
        command switch
        {
            MitsubishiCommandCodes.DeviceRead when requestBodyLength >= 0 => dataCode
            == CommunicationDataCode.Ascii
                ? Four + (Math.Max(1, requestBodyLength / Ten) * Four)
                : Two + Math.Max(1, requestBodyLength / Six),
            MitsubishiCommandCodes.LoopbackTest => Four + requestBodyLength,
            _ => null,
        };

    /// <summary>Executes the Encode1EDeviceBody operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="options">The options parameter.</param>
    /// <returns>The Encode1EDeviceBody operation result.</returns>
    private static byte[] Encode1EDeviceBody(
        MitsubishiDeviceAddress address,
        int points,
        MitsubishiClientOptions options)
    {
        var metadata = address.Descriptor;
        if (options.DataCode == CommunicationDataCode.Binary)
        {
            var buffer = new List<byte>(Eight);
            AppendLegacyHeadDevice(buffer, address);
            AppendLegacyDeviceCode(buffer, metadata);
            AppendByte(buffer, ConvertPointCountToLegacyByte(points), options.DataCode);
            AppendByte(buffer, 0x00, options.DataCode);
            return buffer.ToArray();
        }

        var text = $"{FormatAsciiLegacyAddress(address)}{FormatAsciiByte(ConvertPointCountToLegacyByte(points))}00";
        return Encoding.ASCII.GetBytes(text);
    }

    /// <summary>Executes the Encode1EDeviceWriteBody operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="options">The options parameter.</param>
    /// <param name="bitUnits">The bitUnits parameter.</param>
    /// <returns>The Encode1EDeviceWriteBody operation result.</returns>
    private static byte[] Encode1EDeviceWriteBody(
        MitsubishiDeviceAddress address,
        ReadOnlySpan<ushort> values,
        MitsubishiClientOptions options,
        bool bitUnits)
    {
        var baseBody = Encode1EDeviceBody(address, values.Length, options).ToList();
        if (options.DataCode == CommunicationDataCode.Binary)
        {
            Append1EBinaryWriteValues(baseBody, values, options, bitUnits);
            return baseBody.ToArray();
        }

        var builder = new StringBuilder(Encoding.ASCII.GetString(baseBody.ToArray()));
        Append1EAsciiWriteValues(builder, values, bitUnits);
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    /// <summary>Executes the Append1EBinaryWriteValues operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="options">The options parameter.</param>
    /// <param name="bitUnits">The bitUnits parameter.</param>
    private static void Append1EBinaryWriteValues(
        List<byte> buffer,
        ReadOnlySpan<ushort> values,
        MitsubishiClientOptions options,
        bool bitUnits)
    {
        if (bitUnits)
        {
            foreach (var value in values)
            {
                buffer.Add(Convert.ToByte(value != 0 ? 0x01 : 0x00));
            }

            return;
        }

        foreach (var value in values)
        {
            AppendUInt16(buffer, value, options.DataCode);
        }
    }
}
