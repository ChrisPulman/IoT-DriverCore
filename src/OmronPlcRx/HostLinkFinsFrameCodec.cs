// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Text;

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive;
#else
namespace IoT.DriverCore.OmronPlcRx;
#endif

/// <summary>Encodes and decodes Omron FINS frames carried in Host Link serial frames.</summary>
public sealed class HostLinkFinsFrameCodec
{
    /// <summary>Stores the h ea de rc od e value.</summary>
    private const string HeaderCode = "FA";

    /// <summary>Stores the o pt io ns value.</summary>
    private readonly OmronSerialOptions _options;

    /// <summary>Initializes a new instance of the <see cref="HostLinkFinsFrameCodec"/> class.</summary>
    /// <param name="options">Serial Host Link options.</param>
    public HostLinkFinsFrameCodec(OmronSerialOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
    }

    /// <summary>Calculates the Host Link frame-check sequence.</summary>
    /// <param name="frameText">
    /// Frame text from @ through the final text character, excluding FCS and terminator.
    /// </param>
    /// <returns>Two-character uppercase hexadecimal FCS.</returns>
    public static string CalculateFcs(string frameText)
    {
        if (frameText is null)
        {
            throw new ArgumentNullException(nameof(frameText));
        }

        byte value = 0;
        foreach (var ch in Encoding.ASCII.GetBytes(frameText))
        {
            value ^= ch;
        }

        return value.ToString("X2", CultureInfo.InvariantCulture);
    }

    /// <summary>Encodes a binary FINS request into an ASCII Host Link FINS frame.</summary>
    /// <param name="finsMessage">Binary FINS request message.</param>
    /// <returns>ASCII Host Link FINS frame including FCS and terminator.</returns>
    public string EncodeRequest(ReadOnlyMemory<byte> finsMessage)
    {
        if (finsMessage.Length < 12)
        {
            throw new ArgumentException("The FINS request is too short.", nameof(finsMessage));
        }

        var fins = finsMessage.ToArray();
        var body = new StringBuilder()
            .Append('@')
            .Append(_options.HostLinkUnitNumber.ToString("D2", CultureInfo.InvariantCulture))
            .Append(HeaderCode)
            .Append(_options.ResponseWaitTime.ToString("X1", CultureInfo.InvariantCulture));

        if (_options.FrameMode == OmronHostLinkFinsFrameMode.Direct)
        {
            _ = body.Append("00"); // ICF: directly connected CPU Unit.
            _ = body.Append(fins[5].ToString("X2", CultureInfo.InvariantCulture)); // DA2.
            _ = body.Append(fins[8].ToString("X2", CultureInfo.InvariantCulture)); // SA2.
            _ = body.Append(fins[9].ToString("X2", CultureInfo.InvariantCulture)); // SID.
            _ = body.Append(
                ToHex(fins, ProtocolConstants.Ten, fins.Length - ProtocolConstants.Ten));
        }
        else
        {
            _ = body.Append(ToHex(fins, 0, fins.Length));
        }

        var bodyText = body.ToString();
        return $"{bodyText}{CalculateFcs(bodyText)}*\r";
    }

    /// <summary>Decodes an ASCII Host Link FINS response into a binary FINS response message.</summary>
    /// <param name="frame">ASCII Host Link FINS response frame including FCS and terminator.</param>
    /// <returns>Binary FINS response message.</returns>
    public Memory<byte> DecodeResponse(string frame)
    {
        if (frame is null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        if (!frame.EndsWith("*\r", StringComparison.Ordinal))
        {
            throw new OmronPLCException("The Host Link FINS response terminator was invalid.");
        }

        if (frame.Length < 10)
        {
            throw new OmronPLCException("The Host Link FINS response was too short.");
        }

        var withoutTerminator = frame[..^ProtocolConstants.Two];
        var body = withoutTerminator[..^ProtocolConstants.Two];
        var receivedFcs = withoutTerminator[^ProtocolConstants.Two..];
        var expectedFcs = CalculateFcs(body);
        if (!string.Equals(receivedFcs, expectedFcs, StringComparison.OrdinalIgnoreCase))
        {
            throw new OmronPLCException(
                $"The Host Link FINS response FCS was invalid. Expected '{expectedFcs}', received '{receivedFcs}'.");
        }

        if (body[0] != '@')
        {
            throw new OmronPLCException("The Host Link FINS response did not start with '@'.");
        }

        var unit = body[1..ProtocolConstants.Three];
        var expectedUnit = _options.HostLinkUnitNumber.ToString("D2", CultureInfo.InvariantCulture);
        if (!string.Equals(unit, expectedUnit, StringComparison.Ordinal))
        {
            throw new OmronPLCException(
                $"The Host Link FINS response unit number '{unit}' did not match expected unit '{expectedUnit}'.");
        }

        var headerCode = body[ProtocolConstants.Three..ProtocolConstants.Five];
        if (!string.Equals(headerCode, HeaderCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new OmronPLCException($"The Host Link FINS response header code '{headerCode}' was invalid.");
        }

        const int payloadStart = 5;
        var hostLinkEndCode = body[payloadStart..(payloadStart + ProtocolConstants.Two)];
        if (!string.Equals(hostLinkEndCode, "00", StringComparison.OrdinalIgnoreCase))
        {
            throw new OmronPLCException(
                $"The Host Link FINS response end code was not normal completion: '{hostLinkEndCode}'.");
        }

        var payload = body[(payloadStart + ProtocolConstants.Two)..];
        return _options.FrameMode == OmronHostLinkFinsFrameMode.Direct
            ? DecodeDirectResponse(payload)
            : DecodeNetworkResponse(payload);
    }

    /// <summary>Initializes a new instance of the <see cref="DecodeNetworkResponse"/> class.</summary>
    /// <param name="payload">The p ay lo ad value.</param>
    /// <returns>The result produced by the operation.</returns>
    private static Memory<byte> DecodeNetworkResponse(string payload) => FromHex(payload);

    /// <summary>Initializes a new instance of the <see cref="DecodeDirectResponse"/> class.</summary>
    /// <param name="payload">The p ay lo ad value.</param>
    /// <returns>The result produced by the operation.</returns>
    private static Memory<byte> DecodeDirectResponse(string payload)
    {
        if (payload.Length < 16)
        {
            throw new OmronPLCException("The direct Host Link FINS response payload was too short.");
        }

        var icf = ParseByte(payload, 0);
        var da2 = ParseByte(payload, ProtocolConstants.Two);
        var sa2 = ParseByte(payload, ProtocolConstants.Four);
        var sid = ParseByte(payload, ProtocolConstants.Six);
        var commandAndData = FromHex(payload[ProtocolConstants.Eight..]).ToArray();
        var message = new byte[ProtocolConstants.Ten + commandAndData.Length];
        message[0] = icf;
        message[1] = 0x00;
        message[2] = 0x02;
        message[3] = 0x00;
        message[4] = 0x00;
        message[5] = da2;
        message[6] = 0x00;
        message[7] = 0x00;
        message[8] = sa2;
        message[9] = sid;
        Array.Copy(commandAndData, 0, message, ProtocolConstants.Ten, commandAndData.Length);
        return message;
    }

    /// <summary>Initializes a new instance of the <see cref="ToHex"/> class.</summary>
    /// <param name="bytes">The b yt es value.</param>
    /// <param name="offset">The o ff se t value.</param>
    /// <param name="count">The c ou nt value.</param>
    /// <returns>The result produced by the operation.</returns>
    private static string ToHex(byte[] bytes, int offset, int count)
    {
        var builder = new StringBuilder(count * ProtocolConstants.Two);
        for (var i = offset; i < offset + count; i++)
        {
            _ = builder.Append(bytes[i].ToString("X2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    /// <summary>Initializes a new instance of the <see cref="FromHex"/> class.</summary>
    /// <param name="value">The v al ue value.</param>
    /// <returns>The result produced by the operation.</returns>
    private static Memory<byte> FromHex(string value)
    {
        if (value.Length % ProtocolConstants.Two != 0)
        {
            throw new OmronPLCException("The Host Link FINS hexadecimal payload length was invalid.");
        }

        var bytes = new byte[value.Length / ProtocolConstants.Two];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = ParseByte(value, i * ProtocolConstants.Two);
        }

        return bytes;
    }

    /// <summary>Initializes a new instance of the <see cref="ParseByte"/> class.</summary>
    /// <param name="value">The v al ue value.</param>
    /// <param name="startIndex">The s ta rt in de x value.</param>
    /// <returns>The result produced by the operation.</returns>
    private static byte ParseByte(string value, int startIndex) =>
        byte.Parse(
            value[startIndex..(startIndex + ProtocolConstants.Two)],
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture);
}
