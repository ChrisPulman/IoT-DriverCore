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

/// <summary>Provides the MitsubishiSerialProtocolEncoding type.</summary>
internal static partial class MitsubishiSerialProtocolEncoding
{
    /// <summary>Executes the Encode4CBitWriteRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "address">The address parameter.</param>
    /// <param name = "values">The values parameter.</param>
    /// <returns>The Encode4CBitWriteRequest operation result.</returns>
    private static byte[] Encode4CBitWriteRequest(
        MitsubishiClientOptions options,
        MitsubishiDeviceAddress address,
        IReadOnlyList<bool> values)
    {
        var serial = options.ResolvedSerial;
        var metadata = address.Descriptor;
        return serial.MessageFormat switch
        {
            MitsubishiSerialMessageFormat.Format1 or MitsubishiSerialMessageFormat.Format4 =>
                Encode4CAsciiBitWriteRequest(serial, address, metadata, values),
            MitsubishiSerialMessageFormat.Format5 => Encode4CBinaryBitWriteRequest(
                serial,
                address,
                metadata,
                values),
            _ => throw new NotSupportedException(
                $"Unsupported 4C serial message format '{serial.MessageFormat}'."),
        };
    }

    /// <summary>Executes the Encode4CRandomWriteRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "values">The values parameter.</param>
    /// <returns>The Encode4CRandomWriteRequest operation result.</returns>
    private static byte[] Encode4CRandomWriteRequest(
        MitsubishiClientOptions options,
        IReadOnlyList<MitsubishiDeviceValue> values)
    {
        var serial = options.ResolvedSerial;
        return serial.MessageFormat switch
        {
            MitsubishiSerialMessageFormat.Format1 or MitsubishiSerialMessageFormat.Format4 =>
                Encode4CAsciiRandomWriteRequest(serial, values),
            MitsubishiSerialMessageFormat.Format5 => Encode4CBinaryRandomWriteRequest(
                serial,
                values),
            _ => throw new NotSupportedException(
                $"Unsupported 4C serial message format '{serial.MessageFormat}'."),
        };
    }

    /// <summary>Executes the Encode4CBlockWriteRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "request">The request parameter.</param>
    /// <returns>The Encode4CBlockWriteRequest operation result.</returns>
    private static byte[] Encode4CBlockWriteRequest(
        MitsubishiClientOptions options,
        MitsubishiBlockRequest request)
    {
        var serial = options.ResolvedSerial;
        return serial.MessageFormat switch
        {
            MitsubishiSerialMessageFormat.Format1 or MitsubishiSerialMessageFormat.Format4 =>
                Encode4CAsciiBlockWriteRequest(serial, request),
            MitsubishiSerialMessageFormat.Format5 => Encode4CBinaryBlockWriteRequest(
                serial,
                request),
            _ => throw new NotSupportedException(
                $"Unsupported 4C serial message format '{serial.MessageFormat}'."),
        };
    }

    /// <summary>Executes the Encode4CExecuteMonitorRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <returns>The Encode4CExecuteMonitorRequest operation result.</returns>
    private static byte[] Encode4CExecuteMonitorRequest(MitsubishiClientOptions options)
    {
        var serial = options.ResolvedSerial;
        return serial.MessageFormat switch
        {
            MitsubishiSerialMessageFormat.Format1 or MitsubishiSerialMessageFormat.Format4 =>
                Encode4CAsciiExecuteMonitorRequest(serial),
            MitsubishiSerialMessageFormat.Format5 => Encode4CBinaryExecuteMonitorRequest(serial),
            _ => throw new NotSupportedException(
                $"Unsupported 4C serial message format '{serial.MessageFormat}'."),
        };
    }

    /// <summary>Executes the Encode4CRemoteOperationRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "command">The command parameter.</param>
    /// <param name = "force">The force parameter.</param>
    /// <param name = "clearMode">The clearMode parameter.</param>
    /// <returns>The Encode4CRemoteOperationRequest operation result.</returns>
    private static byte[] Encode4CRemoteOperationRequest(
        MitsubishiClientOptions options,
        ushort command,
        bool force,
        bool clearMode)
    {
        var serial = options.ResolvedSerial;
        return serial.MessageFormat switch
        {
            MitsubishiSerialMessageFormat.Format1 or MitsubishiSerialMessageFormat.Format4 =>
                Encode4CAsciiRemoteOperationRequest(serial, command, force, clearMode),
            MitsubishiSerialMessageFormat.Format5 => Encode4CBinaryRemoteOperationRequest(
                serial,
                command,
                force,
                clearMode),
            _ => throw new NotSupportedException(
                $"Unsupported 4C serial message format '{serial.MessageFormat}'."),
        };
    }

    /// <summary>Executes the Encode4CRawRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "command">The command parameter.</param>
    /// <param name = "subcommand">The subcommand parameter.</param>
    /// <param name = "body">The body parameter.</param>
    /// <returns>The Encode4CRawRequest operation result.</returns>
    private static byte[] Encode4CRawRequest(
        MitsubishiClientOptions options,
        ushort command,
        ushort subcommand,
        IReadOnlyList<byte> body)
    {
        var serial = options.ResolvedSerial;
        return serial.MessageFormat switch
        {
            MitsubishiSerialMessageFormat.Format1 or MitsubishiSerialMessageFormat.Format4 =>
                Encode4CAsciiRawRequest(serial, command, subcommand, body),
            MitsubishiSerialMessageFormat.Format5 => Encode4CBinaryRawRequest(
                serial,
                command,
                subcommand,
                body),
            _ => throw new NotSupportedException(
                $"Unsupported 4C serial message format '{serial.MessageFormat}'."),
        };
    }

    /// <summary>Executes the Encode4CAsciiWriteRequest operation.</summary>
    /// <param name = "serial">The serial parameter.</param>
    /// <param name = "address">The address parameter.</param>
    /// <param name = "metadata">The metadata parameter.</param>
    /// <param name = "values">The values parameter.</param>
    /// <returns>The Encode4CAsciiWriteRequest operation result.</returns>
    private static byte[] Encode4CAsciiWriteRequest(
        MitsubishiSerialOptions serial,
        MitsubishiDeviceAddress address,
        MitsubishiDeviceMetadata metadata,
        IReadOnlyList<ushort> values)
    {
        var header = Format4CAsciiHeader(serial);
        var body =
            $"{header}14010000{FormatDeviceAddressModern(address, metadata)}"
            + FormatAsciiUInt16(checked((ushort)values.Count)) + FormatWordValuesAscii(values);
        return WrapAscii(body, serial.MessageFormat);
    }

    /// <summary>Executes the Encode4CAsciiBitWriteRequest operation.</summary>
    /// <param name = "serial">The serial parameter.</param>
    /// <param name = "address">The address parameter.</param>
    /// <param name = "metadata">The metadata parameter.</param>
    /// <param name = "values">The values parameter.</param>
    /// <returns>The Encode4CAsciiBitWriteRequest operation result.</returns>
    private static byte[] Encode4CAsciiBitWriteRequest(
        MitsubishiSerialOptions serial,
        MitsubishiDeviceAddress address,
        MitsubishiDeviceMetadata metadata,
        IReadOnlyList<bool> values)
    {
        var header = Format4CAsciiHeader(serial);
        var body =
            $"{header}14010001{FormatDeviceAddressModern(address, metadata)}"
            + FormatAsciiUInt16(checked((ushort)values.Count)) + FormatBitValuesAscii(values);
        return WrapAscii(body, serial.MessageFormat);
    }

    /// <summary>Executes the Encode4CAsciiRandomReadRequest operation.</summary>
    /// <param name = "serial">The serial parameter.</param>
    /// <param name = "addresses">The addresses parameter.</param>
    /// <returns>The Encode4CAsciiRandomReadRequest operation result.</returns>
    private static byte[] Encode4CAsciiRandomReadRequest(
        MitsubishiSerialOptions serial,
        IReadOnlyList<MitsubishiDeviceAddress> addresses)
    {
        var header = Format4CAsciiHeader(serial);
        var deviceAddresses = string.Concat(addresses.Select(static address =>
            FormatDeviceAddressModern(address, address.Descriptor)));
        var body =
            $"{header}04030000{FormatAsciiUInt16(checked((ushort)addresses.Count))}0000{deviceAddresses}";
        return WrapAscii(body, serial.MessageFormat);
    }

    /// <summary>Executes the Encode4CAsciiRandomWriteRequest operation.</summary>
    /// <param name = "serial">The serial parameter.</param>
    /// <param name = "values">The values parameter.</param>
    /// <returns>The Encode4CAsciiRandomWriteRequest operation result.</returns>
    private static byte[] Encode4CAsciiRandomWriteRequest(
        MitsubishiSerialOptions serial,
        IReadOnlyList<MitsubishiDeviceValue> values)
    {
        var header = Format4CAsciiHeader(serial);
        var deviceValues = string.Concat(values.Select(static value =>
            FormatDeviceAddressModern(value.Address, value.Address.Descriptor)
            + value.Value.ToString("X4", CultureInfo.InvariantCulture)));
        var body =
            $"{header}14020000{FormatAsciiUInt16(checked((ushort)values.Count))}0000{deviceValues}";
        return WrapAscii(body, serial.MessageFormat);
    }

    /// <summary>Executes the Encode4CAsciiBlockReadRequest operation.</summary>
    /// <param name = "serial">The serial parameter.</param>
    /// <param name = "request">The request parameter.</param>
    /// <returns>The Encode4CAsciiBlockReadRequest operation result.</returns>
    private static byte[] Encode4CAsciiBlockReadRequest(
        MitsubishiSerialOptions serial,
        MitsubishiBlockRequest request)
    {
        var header = Format4CAsciiHeader(serial);
        var body =
            $"{header}04060000{FormatBlocksAscii(request, includeValues: false)}";
        return WrapAscii(body, serial.MessageFormat);
    }

    /// <summary>Executes the Encode4CAsciiBlockWriteRequest operation.</summary>
    /// <param name = "serial">The serial parameter.</param>
    /// <param name = "request">The request parameter.</param>
    /// <returns>The Encode4CAsciiBlockWriteRequest operation result.</returns>
    private static byte[] Encode4CAsciiBlockWriteRequest(
        MitsubishiSerialOptions serial,
        MitsubishiBlockRequest request)
    {
        var header = Format4CAsciiHeader(serial);
        var body =
            $"{header}14060000{FormatBlocksAscii(request, includeValues: true)}";
        return WrapAscii(body, serial.MessageFormat);
    }

    /// <summary>Executes the Encode4CAsciiMonitorRegistrationRequest operation.</summary>
    /// <param name = "serial">The serial parameter.</param>
    /// <param name = "addresses">The addresses parameter.</param>
    /// <returns>The Encode4CAsciiMonitorRegistrationRequest operation result.</returns>
    private static byte[] Encode4CAsciiMonitorRegistrationRequest(
        MitsubishiSerialOptions serial,
        IReadOnlyList<MitsubishiDeviceAddress> addresses)
    {
        var header = Format4CAsciiHeader(serial);
        var deviceAddresses = string.Concat(addresses.Select(static address =>
            FormatDeviceAddressModern(address, address.Descriptor)));
        var body =
            $"{header}08010000{FormatAsciiUInt16(checked((ushort)addresses.Count))}0000{deviceAddresses}";
        return WrapAscii(body, serial.MessageFormat);
    }

    /// <summary>Executes the Encode4CAsciiExecuteMonitorRequest operation.</summary>
    /// <param name = "serial">The serial parameter.</param>
    /// <returns>The Encode4CAsciiExecuteMonitorRequest operation result.</returns>
    private static byte[] Encode4CAsciiExecuteMonitorRequest(MitsubishiSerialOptions serial)
    {
        var header = Format4CAsciiHeader(serial);
        var body =
            $"{header}08020000";
        return WrapAscii(body, serial.MessageFormat);
    }

    /// <summary>Executes the Encode4CAsciiRemoteOperationRequest operation.</summary>
    /// <param name = "serial">The serial parameter.</param>
    /// <param name = "command">The command parameter.</param>
    /// <param name = "force">The force parameter.</param>
    /// <param name = "clearMode">The clearMode parameter.</param>
    /// <returns>The Encode4CAsciiRemoteOperationRequest operation result.</returns>
    private static byte[] Encode4CAsciiRemoteOperationRequest(
        MitsubishiSerialOptions serial,
        ushort command,
        bool force,
        bool clearMode)
    {
        var header = Format4CAsciiHeader(serial);
        var body =
            $"{header}{command.ToString("X4", CultureInfo.InvariantCulture)}0000"
            + FormatRemoteOperationPayloadAscii(command, force, clearMode);
        return WrapAscii(body, serial.MessageFormat);
    }

    /// <summary>Executes the Encode4CAsciiRawRequest operation.</summary>
    /// <param name = "serial">The serial parameter.</param>
    /// <param name = "command">The command parameter.</param>
    /// <param name = "subcommand">The subcommand parameter.</param>
    /// <param name = "body">The body parameter.</param>
    /// <returns>The Encode4CAsciiRawRequest operation result.</returns>
    private static byte[] Encode4CAsciiRawRequest(
        MitsubishiSerialOptions serial,
        ushort command,
        ushort subcommand,
        IReadOnlyList<byte> body)
    {
        var payload = body.Count == 0 ? string.Empty : Encoding.ASCII.GetString(body.ToArray());
        var requestBody =
            FormatAsciiByte(FourCFrameId)
            + FormatAsciiByte(serial.StationNumber)
            + FormatAsciiByte(serial.NetworkNumber)
            + FormatAsciiByte(serial.PcNumber)
            + FormatAsciiUInt16(serial.RequestDestinationModuleIoNumber)
            + FormatAsciiByte(serial.RequestDestinationModuleStationNumber)
            + FormatAsciiByte(serial.SelfStationNumber)
            + command.ToString("X4", CultureInfo.InvariantCulture)
            + subcommand.ToString("X4", CultureInfo.InvariantCulture)
            + payload;
        return WrapAscii(requestBody, serial.MessageFormat);
    }

    /// <summary>Executes the Encode4CBinaryWriteRequest operation.</summary>
    /// <param name = "serial">The serial parameter.</param>
    /// <param name = "address">The address parameter.</param>
    /// <param name = "metadata">The metadata parameter.</param>
    /// <param name = "values">The values parameter.</param>
    /// <returns>The Encode4CBinaryWriteRequest operation result.</returns>
    private static byte[] Encode4CBinaryWriteRequest(
        MitsubishiSerialOptions serial,
        MitsubishiDeviceAddress address,
        MitsubishiDeviceMetadata metadata,
        IReadOnlyList<ushort> values)
    {
        var buffer = Create4CBinaryHeader(serial);
        AppendUInt16LittleEndian(buffer, 0x1401);
        AppendUInt16LittleEndian(buffer, 0x0000);
        AppendThreeByteLittleEndian(buffer, address.Number);
        buffer.Add((byte)metadata.BinaryCode);
        AppendUInt16LittleEndian(buffer, checked((ushort)values.Count));
        AppendWordsLittleEndian(buffer, values);
        return Finalize4CBinaryFrame(buffer);
    }

    /// <summary>Executes the Encode4CBinaryBitWriteRequest operation.</summary>
    /// <param name = "serial">The serial parameter.</param>
    /// <param name = "address">The address parameter.</param>
    /// <param name = "metadata">The metadata parameter.</param>
    /// <param name = "values">The values parameter.</param>
    /// <returns>The Encode4CBinaryBitWriteRequest operation result.</returns>
    private static byte[] Encode4CBinaryBitWriteRequest(
        MitsubishiSerialOptions serial,
        MitsubishiDeviceAddress address,
        MitsubishiDeviceMetadata metadata,
        IReadOnlyList<bool> values)
    {
        var buffer = Create4CBinaryHeader(serial);
        AppendUInt16LittleEndian(buffer, 0x1401);
        AppendUInt16LittleEndian(buffer, 0x0001);
        AppendThreeByteLittleEndian(buffer, address.Number);
        buffer.Add((byte)metadata.BinaryCode);
        AppendUInt16LittleEndian(buffer, checked((ushort)values.Count));
        AppendBitsBinary(buffer, values);
        return Finalize4CBinaryFrame(buffer);
    }
}
