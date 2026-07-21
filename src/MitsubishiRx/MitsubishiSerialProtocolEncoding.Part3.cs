// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Globalization;
using System.Text;

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;

#else

namespace MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiSerialProtocolEncoding type.</summary>
internal static partial class MitsubishiSerialProtocolEncoding
{
    /// <summary>Executes the Encode4CRandomReadRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "addresses">The addresses parameter.</param>
    /// <returns>The Encode4CRandomReadRequest operation result.</returns>
    private static byte[] Encode4CRandomReadRequest(
        MitsubishiClientOptions options,
        IReadOnlyList<MitsubishiDeviceAddress> addresses)
    {
        var serial = options.ResolvedSerial;
        return serial.MessageFormat switch
        {
            MitsubishiSerialMessageFormat.Format1 or MitsubishiSerialMessageFormat.Format4 =>
                Encode4CAsciiRandomReadRequest(serial, addresses),
            MitsubishiSerialMessageFormat.Format5 => Encode4CBinaryRandomReadRequest(
                serial,
                addresses),
            _ => throw new NotSupportedException(
                $"Unsupported 4C serial message format '{serial.MessageFormat}'."),
        };
    }

    /// <summary>Executes the Encode3CBlockReadRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "request">The request parameter.</param>
    /// <returns>The Encode3CBlockReadRequest operation result.</returns>
    private static byte[] Encode3CBlockReadRequest(
        MitsubishiClientOptions options,
        MitsubishiBlockRequest request)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var header = Format3CAsciiHeader(serial);
        var body =
            $"{header}04060000{FormatBlocksAscii(request, includeValues: false)}";
        return WrapAscii(body, serial.MessageFormat);
    }

    /// <summary>Executes the Encode3CMonitorRegistrationRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "addresses">The addresses parameter.</param>
    /// <returns>The Encode3CMonitorRegistrationRequest operation result.</returns>
    private static byte[] Encode3CMonitorRegistrationRequest(
        MitsubishiClientOptions options,
        IReadOnlyList<MitsubishiDeviceAddress> addresses)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var header = Format3CAsciiHeader(serial);
        var deviceAddresses = string.Concat(addresses.Select(static address =>
            FormatDeviceAddressModern(address, address.Descriptor)));
        var body =
            $"{header}08010000{FormatAsciiUInt16(checked((ushort)addresses.Count))}0000{deviceAddresses}";
        return WrapAscii(body, serial.MessageFormat);
    }

    /// <summary>Executes the Encode4CBlockReadRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "request">The request parameter.</param>
    /// <returns>The Encode4CBlockReadRequest operation result.</returns>
    private static byte[] Encode4CBlockReadRequest(
        MitsubishiClientOptions options,
        MitsubishiBlockRequest request)
    {
        var serial = options.ResolvedSerial;
        return serial.MessageFormat switch
        {
            MitsubishiSerialMessageFormat.Format1 or MitsubishiSerialMessageFormat.Format4 =>
                Encode4CAsciiBlockReadRequest(serial, request),
            MitsubishiSerialMessageFormat.Format5 => Encode4CBinaryBlockReadRequest(
                serial,
                request),
            _ => throw new NotSupportedException(
                $"Unsupported 4C serial message format '{serial.MessageFormat}'."),
        };
    }

    /// <summary>Executes the Encode4CMonitorRegistrationRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "addresses">The addresses parameter.</param>
    /// <returns>The Encode4CMonitorRegistrationRequest operation result.</returns>
    private static byte[] Encode4CMonitorRegistrationRequest(
        MitsubishiClientOptions options,
        IReadOnlyList<MitsubishiDeviceAddress> addresses)
    {
        var serial = options.ResolvedSerial;
        return serial.MessageFormat switch
        {
            MitsubishiSerialMessageFormat.Format1 or MitsubishiSerialMessageFormat.Format4 =>
                Encode4CAsciiMonitorRegistrationRequest(serial, addresses),
            MitsubishiSerialMessageFormat.Format5 => Encode4CBinaryMonitorRegistrationRequest(
                serial,
                addresses),
            _ => throw new NotSupportedException(
                $"Unsupported 4C serial message format '{serial.MessageFormat}'."),
        };
    }

    /// <summary>Executes the Encode1CWriteRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "address">The address parameter.</param>
    /// <param name = "values">The values parameter.</param>
    /// <returns>The Encode1CWriteRequest operation result.</returns>
    private static byte[] Encode1CWriteRequest(
        MitsubishiClientOptions options,
        MitsubishiDeviceAddress address,
        IReadOnlyList<ushort> values)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var metadata = address.Descriptor;
        var body =
            FormatAsciiByte(serial.StationNumber) + FormatAsciiByte(serial.PcNumber)
            + $"WW{FormatAsciiNibble(serial.MessageWait)}{Format1CAddress(address, metadata)}"
            + FormatPointCount(values.Count) + FormatWordValuesAscii(values);
        return WrapAscii(body, serial.MessageFormat);
    }

    /// <summary>Executes the Encode1CBitWriteRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "address">The address parameter.</param>
    /// <param name = "values">The values parameter.</param>
    /// <returns>The Encode1CBitWriteRequest operation result.</returns>
    private static byte[] Encode1CBitWriteRequest(
        MitsubishiClientOptions options,
        MitsubishiDeviceAddress address,
        IReadOnlyList<bool> values)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var metadata = address.Descriptor;
        var body =
            FormatAsciiByte(serial.StationNumber) + FormatAsciiByte(serial.PcNumber)
            + $"BW{FormatAsciiNibble(serial.MessageWait)}{Format1CAddress(address, metadata)}"
            + FormatPointCount(values.Count) + FormatBitValuesAscii(values);
        return WrapAscii(body, serial.MessageFormat);
    }

    /// <summary>Executes the Encode1CRawRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "command">The command parameter.</param>
    /// <param name = "subcommand">The subcommand parameter.</param>
    /// <param name = "body">The body parameter.</param>
    /// <returns>The Encode1CRawRequest operation result.</returns>
    private static byte[] Encode1CRawRequest(
        MitsubishiClientOptions options,
        ushort command,
        ushort subcommand,
        IReadOnlyList<byte> body)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var payload = body.Count == 0 ? string.Empty : Encoding.ASCII.GetString(body.ToArray());
        var requestBody =
            FormatAsciiByte(serial.StationNumber)
            + FormatAsciiByte(serial.PcNumber)
            + command.ToString("X4", CultureInfo.InvariantCulture)
            + subcommand.ToString("X4", CultureInfo.InvariantCulture)
            + payload;
        return WrapAscii(requestBody, serial.MessageFormat);
    }

    /// <summary>Executes the Build1CRandomReadBody operation.</summary>
    /// <param name = "addresses">The addresses parameter.</param>
    /// <returns>The Build1CRandomReadBody operation result.</returns>
    private static byte[] Build1CRandomReadBody(IReadOnlyList<MitsubishiDeviceAddress> addresses)
    {
        var deviceAddresses = string.Concat(addresses.Select(static address =>
            FormatDeviceAddressModern(address, address.Descriptor)));
        return Encoding.ASCII.GetBytes(
            $"{FormatAsciiUInt16(checked((ushort)addresses.Count))}0000{deviceAddresses}");
    }

    /// <summary>Executes the Build1CRandomWriteBody operation.</summary>
    /// <param name = "values">The values parameter.</param>
    /// <returns>The Build1CRandomWriteBody operation result.</returns>
    private static byte[] Build1CRandomWriteBody(IReadOnlyList<MitsubishiDeviceValue> values)
    {
        var deviceValues = string.Concat(values.Select(static value =>
            FormatDeviceAddressModern(value.Address, value.Address.Descriptor)
            + value.Value.ToString("X4", CultureInfo.InvariantCulture)));
        return Encoding.ASCII.GetBytes(
            $"{FormatAsciiUInt16(checked((ushort)values.Count))}0000{deviceValues}");
    }

    /// <summary>Executes the Encode3CWriteRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "address">The address parameter.</param>
    /// <param name = "values">The values parameter.</param>
    /// <returns>The Encode3CWriteRequest operation result.</returns>
    private static byte[] Encode3CWriteRequest(
        MitsubishiClientOptions options,
        MitsubishiDeviceAddress address,
        IReadOnlyList<ushort> values)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var metadata = address.Descriptor;
        var header = Format3CAsciiHeader(serial);
        var body =
            $"{header}14010000{FormatDeviceAddressModern(address, metadata)}"
            + FormatAsciiUInt16(checked((ushort)values.Count)) + FormatWordValuesAscii(values);
        return WrapAscii(body, serial.MessageFormat);
    }

    /// <summary>Executes the Encode3CBitWriteRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "address">The address parameter.</param>
    /// <param name = "values">The values parameter.</param>
    /// <returns>The Encode3CBitWriteRequest operation result.</returns>
    private static byte[] Encode3CBitWriteRequest(
        MitsubishiClientOptions options,
        MitsubishiDeviceAddress address,
        IReadOnlyList<bool> values)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var metadata = address.Descriptor;
        var header = Format3CAsciiHeader(serial);
        var body =
            $"{header}14010001{FormatDeviceAddressModern(address, metadata)}"
            + FormatAsciiUInt16(checked((ushort)values.Count)) + FormatBitValuesAscii(values);
        return WrapAscii(body, serial.MessageFormat);
    }

    /// <summary>Executes the Encode3CRandomWriteRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "values">The values parameter.</param>
    /// <returns>The Encode3CRandomWriteRequest operation result.</returns>
    private static byte[] Encode3CRandomWriteRequest(
        MitsubishiClientOptions options,
        IReadOnlyList<MitsubishiDeviceValue> values)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var header = Format3CAsciiHeader(serial);
        var deviceValues = string.Concat(values.Select(static value =>
            FormatDeviceAddressModern(value.Address, value.Address.Descriptor)
            + value.Value.ToString("X4", CultureInfo.InvariantCulture)));
        var body =
            $"{header}14020000{FormatAsciiUInt16(checked((ushort)values.Count))}0000{deviceValues}";
        return WrapAscii(body, serial.MessageFormat);
    }

    /// <summary>Executes the Encode3CBlockWriteRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "request">The request parameter.</param>
    /// <returns>The Encode3CBlockWriteRequest operation result.</returns>
    private static byte[] Encode3CBlockWriteRequest(
        MitsubishiClientOptions options,
        MitsubishiBlockRequest request)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var header = Format3CAsciiHeader(serial);
        var body =
            $"{header}14060000{FormatBlocksAscii(request, includeValues: true)}";
        return WrapAscii(body, serial.MessageFormat);
    }

    /// <summary>Executes the Encode3CExecuteMonitorRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <returns>The Encode3CExecuteMonitorRequest operation result.</returns>
    private static byte[] Encode3CExecuteMonitorRequest(MitsubishiClientOptions options)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var header = Format3CAsciiHeader(serial);
        var body =
            $"{header}08020000";
        return WrapAscii(body, serial.MessageFormat);
    }

    /// <summary>Executes the Encode3CRemoteOperationRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "command">The command parameter.</param>
    /// <param name = "force">The force parameter.</param>
    /// <param name = "clearMode">The clearMode parameter.</param>
    /// <returns>The Encode3CRemoteOperationRequest operation result.</returns>
    private static byte[] Encode3CRemoteOperationRequest(
        MitsubishiClientOptions options,
        ushort command,
        bool force,
        bool clearMode)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var header = Format3CAsciiHeader(serial);
        var body =
            $"{header}{command.ToString("X4", CultureInfo.InvariantCulture)}0000"
            + FormatRemoteOperationPayloadAscii(command, force, clearMode);
        return WrapAscii(body, serial.MessageFormat);
    }

    /// <summary>Executes the Encode3CRawRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "command">The command parameter.</param>
    /// <param name = "subcommand">The subcommand parameter.</param>
    /// <param name = "body">The body parameter.</param>
    /// <returns>The Encode3CRawRequest operation result.</returns>
    private static byte[] Encode3CRawRequest(
        MitsubishiClientOptions options,
        ushort command,
        ushort subcommand,
        IReadOnlyList<byte> body)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var payload = body.Count == 0 ? string.Empty : Encoding.ASCII.GetString(body.ToArray());
        var requestBody =
            FormatAsciiByte(ThreeCFrameId)
            + FormatAsciiByte(serial.StationNumber)
            + FormatAsciiByte(serial.NetworkNumber)
            + FormatAsciiByte(serial.PcNumber)
            + FormatAsciiByte(serial.SelfStationNumber)
            + command.ToString("X4", CultureInfo.InvariantCulture)
            + subcommand.ToString("X4", CultureInfo.InvariantCulture)
            + payload;
        return WrapAscii(requestBody, serial.MessageFormat);
    }

    /// <summary>Executes the Encode4CWriteRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "address">The address parameter.</param>
    /// <param name = "values">The values parameter.</param>
    /// <returns>The Encode4CWriteRequest operation result.</returns>
    private static byte[] Encode4CWriteRequest(
        MitsubishiClientOptions options,
        MitsubishiDeviceAddress address,
        IReadOnlyList<ushort> values)
    {
        var serial = options.ResolvedSerial;
        var metadata = address.Descriptor;
        return serial.MessageFormat switch
        {
            MitsubishiSerialMessageFormat.Format1 or MitsubishiSerialMessageFormat.Format4 =>
                Encode4CAsciiWriteRequest(serial, address, metadata, values),
            MitsubishiSerialMessageFormat.Format5 => Encode4CBinaryWriteRequest(
                serial,
                address,
                metadata,
                values),
            _ => throw new NotSupportedException(
                $"Unsupported 4C serial message format '{serial.MessageFormat}'."),
        };
    }
}
