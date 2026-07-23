// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Text;

#if REACTIVE_SHIM

using static IoT.DriverCore.MitsubishiRx.Reactive.MitsubishiNumericConstants;

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

using static IoT.DriverCore.MitsubishiRx.MitsubishiNumericConstants;

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiSerialProtocolEncoding type.</summary>
internal static partial class MitsubishiSerialProtocolEncoding
{
    /// <summary>Executes the EncodeReadTypeNameRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <returns>The EncodeReadTypeNameRequest operation result.</returns>
    internal static byte[] EncodeReadTypeNameRequest(MitsubishiClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => Encode1CRawRequest(
                options,
                MitsubishiCommandCodes.ReadTypeName,
                0x0000,
                []),
            MitsubishiFrameType.ThreeC => Encode3CRawRequest(
                options,
                MitsubishiCommandCodes.ReadTypeName,
                0x0000,
                []),
            MitsubishiFrameType.FourC => Encode4CRawRequest(
                options,
                MitsubishiCommandCodes.ReadTypeName,
                0x0000,
                []),
            _ => throw new NotSupportedException(
                $"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    /// <summary>Executes the EncodeLoopbackRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "data">The data parameter.</param>
    /// <returns>The EncodeLoopbackRequest operation result.</returns>
    internal static byte[] EncodeLoopbackRequest(
        MitsubishiClientOptions options,
        ReadOnlySpan<byte> data)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (data.IsEmpty)
        {
            throw new ArgumentException("Loopback payload must not be empty.", nameof(data));
        }

        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => Encode1CRawRequest(
                options,
                MitsubishiCommandCodes.LoopbackTest,
                0x0000,
                BuildLoopbackBody(options, data)),
            MitsubishiFrameType.ThreeC => Encode3CRawRequest(
                options,
                MitsubishiCommandCodes.LoopbackTest,
                0x0000,
                BuildLoopbackBody(options, data)),
            MitsubishiFrameType.FourC => Encode4CRawRequest(
                options,
                MitsubishiCommandCodes.LoopbackTest,
                0x0000,
                BuildLoopbackBody(options, data)),
            _ => throw new NotSupportedException(
                $"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    /// <summary>Executes the EncodeMemoryAccessRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "command">The command parameter.</param>
    /// <param name = "address">The address parameter.</param>
    /// <param name = "length">The length parameter.</param>
    /// <param name = "values">The values parameter.</param>
    /// <returns>The EncodeMemoryAccessRequest operation result.</returns>
    internal static byte[] EncodeMemoryAccessRequest(
        MitsubishiClientOptions options,
        ushort command,
        ushort address,
        int length,
        ReadOnlySpan<ushort> values)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => Encode1CRawRequest(
                options,
                command,
                0x0000,
                BuildMemoryAccessBody(options, command, address, length, values)),
            MitsubishiFrameType.ThreeC => Encode3CRawRequest(
                options,
                command,
                0x0000,
                BuildMemoryAccessBody(options, command, address, length, values)),
            MitsubishiFrameType.FourC => Encode4CRawRequest(
                options,
                command,
                0x0000,
                BuildMemoryAccessBody(options, command, address, length, values)),
            _ => throw new NotSupportedException(
                $"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    /// <summary>Executes the EncodeRawRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "request">The request parameter.</param>
    /// <returns>The EncodeRawRequest operation result.</returns>
    internal static byte[] EncodeRawRequest(
        MitsubishiClientOptions options,
        MitsubishiRawCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(request);
        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => Encode1CRawRequest(
                options,
                request.Command,
                request.Subcommand,
                request.ResolvedBody),
            MitsubishiFrameType.ThreeC => Encode3CRawRequest(
                options,
                request.Command,
                request.Subcommand,
                request.ResolvedBody),
            MitsubishiFrameType.FourC => Encode4CRawRequest(
                options,
                request.Command,
                request.Subcommand,
                request.ResolvedBody),
            _ => throw new NotSupportedException(
                $"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    /// <summary>Executes the Decode operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "response">The response parameter.</param>
    /// <returns>The Decode operation result.</returns>
    internal static Responce<byte[]> Decode(MitsubishiClientOptions options, byte[] response)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(response);
        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => Decode1C(options, response),
            MitsubishiFrameType.ThreeC => Decode3C(options, response),
            MitsubishiFrameType.FourC => Decode4C(options, response),
            _ => throw new NotSupportedException(
                $"Serial decoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    /// <summary>Executes the IsExpectedFrameComplete operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "buffer">The buffer parameter.</param>
    /// <returns>The IsExpectedFrameComplete operation result.</returns>
    internal static bool IsExpectedFrameComplete(
        MitsubishiClientOptions options,
        ReadOnlySpan<byte> buffer)
    {
        ArgumentNullException.ThrowIfNull(options);
        return buffer.IsEmpty
            ? false
            : options.FrameType switch
            {
                MitsubishiFrameType.OneC or MitsubishiFrameType.ThreeC =>
                    options.ResolvedSerial.MessageFormat switch
                {
                    MitsubishiSerialMessageFormat.Format1 or MitsubishiSerialMessageFormat.Format4 =>
                        HasAsciiChecksumFramedMessage(buffer),
                    _ => false,
                },
                MitsubishiFrameType.FourC => options.ResolvedSerial.MessageFormat switch
                {
                    MitsubishiSerialMessageFormat.Format1 or MitsubishiSerialMessageFormat.Format4 =>
                        HasAsciiChecksumFramedMessage(buffer),
                    MitsubishiSerialMessageFormat.Format5 => HasBinaryFormat5Message(buffer),
                    _ => false,
                },
                _ => false,
            };
    }

    /// <summary>Executes the Encode1CRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "address">The address parameter.</param>
    /// <param name = "points">The points parameter.</param>
    /// <param name = "wordUnits">The wordUnits parameter.</param>
    /// <returns>The Encode1CRequest operation result.</returns>
    private static byte[] Encode1CRequest(
        MitsubishiClientOptions options,
        MitsubishiDeviceAddress address,
        int points,
        bool wordUnits)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var metadata = address.Descriptor;
        var command = wordUnits && metadata.Kind == DeviceValueKind.Word ? "WR" : "BR";
        var body =
            FormatAsciiByte(serial.StationNumber)
            + FormatAsciiByte(serial.PcNumber)
            + command
            + FormatAsciiNibble(serial.MessageWait)
            + Format1CAddress(address, metadata)
            + FormatPointCount(points);
        return WrapAscii(body, serial.MessageFormat);
    }

    /// <summary>Executes the Encode3CRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "address">The address parameter.</param>
    /// <param name = "points">The points parameter.</param>
    /// <param name = "wordUnits">The wordUnits parameter.</param>
    /// <returns>The Encode3CRequest operation result.</returns>
    private static byte[] Encode3CRequest(
        MitsubishiClientOptions options,
        MitsubishiDeviceAddress address,
        int points,
        bool wordUnits)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var metadata = address.Descriptor;
        const string command = "0401";
        var subcommand = wordUnits ? "0000" : "0001";
        var body =
            FormatAsciiByte(ThreeCFrameId)
            + FormatAsciiByte(serial.StationNumber)
            + FormatAsciiByte(serial.NetworkNumber)
            + FormatAsciiByte(serial.PcNumber)
            + FormatAsciiByte(serial.SelfStationNumber)
            + command
            + subcommand
            + FormatDeviceAddressModern(address, metadata)
            + FormatPointCount(points);
        return WrapAscii(body, serial.MessageFormat);
    }

    /// <summary>Executes the Encode4CRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "address">The address parameter.</param>
    /// <param name = "points">The points parameter.</param>
    /// <param name = "wordUnits">The wordUnits parameter.</param>
    /// <returns>The Encode4CRequest operation result.</returns>
    private static byte[] Encode4CRequest(
        MitsubishiClientOptions options,
        MitsubishiDeviceAddress address,
        int points,
        bool wordUnits)
    {
        var serial = options.ResolvedSerial;
        var metadata = address.Descriptor;
        return serial.MessageFormat switch
        {
            MitsubishiSerialMessageFormat.Format1 or MitsubishiSerialMessageFormat.Format4 =>
                Encode4CAsciiRequest(serial, address, metadata, points, wordUnits),
            MitsubishiSerialMessageFormat.Format5 => Encode4CBinaryRequest(
                serial,
                address,
                metadata,
                points,
                wordUnits),
            _ => throw new NotSupportedException(
                $"Unsupported 4C serial message format '{serial.MessageFormat}'."),
        };
    }

    /// <summary>Executes the Encode4CAsciiRequest operation.</summary>
    /// <param name = "serial">The serial parameter.</param>
    /// <param name = "address">The address parameter.</param>
    /// <param name = "metadata">The metadata parameter.</param>
    /// <param name = "points">The points parameter.</param>
    /// <param name = "wordUnits">The wordUnits parameter.</param>
    /// <returns>The Encode4CAsciiRequest operation result.</returns>
    private static byte[] Encode4CAsciiRequest(
        MitsubishiSerialOptions serial,
        MitsubishiDeviceAddress address,
        MitsubishiDeviceMetadata metadata,
        int points,
        bool wordUnits)
    {
        const string command = "0401";
        var subcommand = wordUnits ? "0000" : "0001";
        var body =
            FormatAsciiByte(FourCFrameId)
            + FormatAsciiByte(serial.StationNumber)
            + FormatAsciiByte(serial.NetworkNumber)
            + FormatAsciiByte(serial.PcNumber)
            + FormatAsciiUInt16(serial.RequestDestinationModuleIoNumber)
            + FormatAsciiByte(serial.RequestDestinationModuleStationNumber)
            + FormatAsciiByte(serial.SelfStationNumber)
            + command
            + subcommand
            + FormatDeviceAddressModern(address, metadata)
            + FormatPointCount(points);
        return WrapAscii(body, serial.MessageFormat);
    }

    /// <summary>Executes the Encode4CBinaryRequest operation.</summary>
    /// <param name = "serial">The serial parameter.</param>
    /// <param name = "address">The address parameter.</param>
    /// <param name = "metadata">The metadata parameter.</param>
    /// <param name = "points">The points parameter.</param>
    /// <param name = "wordUnits">The wordUnits parameter.</param>
    /// <returns>The Encode4CBinaryRequest operation result.</returns>
    private static byte[] Encode4CBinaryRequest(
        MitsubishiSerialOptions serial,
        MitsubishiDeviceAddress address,
        MitsubishiDeviceMetadata metadata,
        int points,
        bool wordUnits)
    {
        var buffer = new List<byte>
        {
            FourCFrameId,
            serial.StationNumber,
            serial.NetworkNumber,
            serial.PcNumber,
            (byte)(serial.RequestDestinationModuleIoNumber & 0xFF),
            (byte)(serial.RequestDestinationModuleIoNumber >> 8),
            serial.RequestDestinationModuleStationNumber,
            serial.SelfStationNumber,
        };
        AppendUInt16LittleEndian(buffer, 0x0401);
        AppendUInt16LittleEndian(buffer, wordUnits ? (ushort)0x0000 : (ushort)0x0001);
        AppendThreeByteLittleEndian(buffer, address.Number);
        buffer.Add((byte)metadata.BinaryCode);
        AppendUInt16LittleEndian(buffer, checked((ushort)points));
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

    /// <summary>Executes the Encode3CRandomReadRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "addresses">The addresses parameter.</param>
    /// <returns>The Encode3CRandomReadRequest operation result.</returns>
    private static byte[] Encode3CRandomReadRequest(
        MitsubishiClientOptions options,
        IReadOnlyList<MitsubishiDeviceAddress> addresses)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var header = Format3CAsciiHeader(serial);
        var deviceAddresses = string.Concat(addresses.Select(static address =>
            FormatDeviceAddressModern(address, address.Descriptor)));
        var body =
            $"{header}04030000{FormatAsciiUInt16(checked((ushort)addresses.Count))}0000{deviceAddresses}";
        return WrapAscii(body, serial.MessageFormat);
    }
}
