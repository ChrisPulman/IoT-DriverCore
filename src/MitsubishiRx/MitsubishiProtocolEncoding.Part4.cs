// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
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
    /// <summary>Executes the AppendDeviceAddress operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="address">The address parameter.</param>
    /// <param name="options">The options parameter.</param>
    /// <param name="legacy">The legacy parameter.</param>
    private static void AppendDeviceAddress(
        List<byte> buffer,
        MitsubishiDeviceAddress address,
        MitsubishiClientOptions options,
        bool legacy)
    {
        var metadata = address.Descriptor;
        if (options.DataCode == CommunicationDataCode.Binary)
        {
            if (legacy)
            {
                AppendLegacyHeadDevice(buffer, address);
                AppendLegacyDeviceCode(buffer, metadata);
            }
            else
            {
                var raw = BitConverter.GetBytes(address.Number);
                buffer.Add(raw[0]);
                buffer.Add(raw[1]);
                buffer.Add(raw[2]);
                buffer.Add((byte)metadata.BinaryCode);
            }

            return;
        }

        if (legacy)
        {
            buffer.AddRange(
                Encoding.ASCII.GetBytes(FormatAsciiLegacyAddress(address)));
            return;
        }

        buffer.AddRange(
            Encoding.ASCII.GetBytes(FormatAsciiModernAddress(address, metadata)));
    }

    /// <summary>Executes the AppendLegacyHeadDevice operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="address">The address parameter.</param>
    private static void AppendLegacyHeadDevice(
        List<byte> buffer,
        MitsubishiDeviceAddress address)
    {
        var bytes = BitConverter.GetBytes(address.Number);
        buffer.Add(bytes[0]);
        buffer.Add(bytes[1]);
        buffer.Add(bytes[2]);
        buffer.Add(0x00);
    }

    /// <summary>Executes the AppendLegacyDeviceCode operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="metadata">The metadata parameter.</param>
    private static void AppendLegacyDeviceCode(List<byte> buffer, MitsubishiDeviceMetadata metadata)
    {
        var code = BitConverter.GetBytes(metadata.AsciiCode);
        buffer.Add(code[0]);
        buffer.Add(code[1]);
    }

    /// <summary>Executes the FormatAsciiLegacyAddress operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <returns>The FormatAsciiLegacyAddress operation result.</returns>
    private static string FormatAsciiLegacyAddress(MitsubishiDeviceAddress address) =>
        address.Number.ToString("X8", CultureInfo.InvariantCulture)
        + address.Descriptor.Symbol.PadRight(Two, '*');

    /// <summary>Executes the FormatAsciiModernAddress operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="metadata">The metadata parameter.</param>
    /// <returns>The FormatAsciiModernAddress operation result.</returns>
    private static string FormatAsciiModernAddress(
        MitsubishiDeviceAddress address,
        MitsubishiDeviceMetadata metadata) =>
        address.Number.ToString("X6", CultureInfo.InvariantCulture)
        + metadata.Symbol.PadRight(Two, '*');

    /// <summary>Executes the Get1ECommand operation.</summary>
    /// <param name="command">The command parameter.</param>
    /// <param name="subcommand">The subcommand parameter.</param>
    /// <returns>The Get1ECommand operation result.</returns>
    private static byte Get1ECommand(ushort command, ushort subcommand) =>
        command switch
        {
            MitsubishiCommandCodes.DeviceRead when subcommand == 0x0000 => 0x00,
            MitsubishiCommandCodes.DeviceRead when subcommand == 0x0001 => 0x01,
            MitsubishiCommandCodes.DeviceWrite when subcommand == 0x0002 => 0x02,
            MitsubishiCommandCodes.DeviceWrite when subcommand == 0x0003 => 0x03,
            MitsubishiCommandCodes.RandomWrite when subcommand == 0x0001 => 0x04,
            MitsubishiCommandCodes.RandomWrite when subcommand == 0x0000 => 0x05,
            MitsubishiCommandCodes.EntryMonitorDevice => 0x06,
            MitsubishiCommandCodes.ExecuteMonitor => 0x08,
            MitsubishiCommandCodes.RemoteRun => 0x13,
            MitsubishiCommandCodes.RemoteStop => 0x14,
            MitsubishiCommandCodes.ReadTypeName => 0x15,
            MitsubishiCommandCodes.LoopbackTest => 0x16,
            _ => throw new NotSupportedException(
                $"1E command {command:X4}/{subcommand:X4} is not supported in this release."),
        };

    /// <summary>Executes the ConvertPointCountToLegacyByte operation.</summary>
    /// <param name="points">The points parameter.</param>
    /// <returns>The ConvertPointCountToLegacyByte operation result.</returns>
    private static byte ConvertPointCountToLegacyByte(int points)
    {
        if (points is < 1 or > TwoHundredFiftySix)
        {
            throw new ArgumentOutOfRangeException(
                nameof(points),
                "1E point count must be between 1 and 256.");
        }

        return points == TwoHundredFiftySix ? (byte)0x00 : checked((byte)points);
    }

    /// <summary>Executes the AppendByte operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="dataCode">The dataCode parameter.</param>
    private static void AppendByte(List<byte> buffer, byte value, CommunicationDataCode dataCode)
    {
        if (dataCode == CommunicationDataCode.Binary)
        {
            buffer.Add(value);
            return;
        }

        buffer.AddRange(Encoding.ASCII.GetBytes(FormatAsciiByte(value)));
    }

    /// <summary>Executes the AppendUInt16 operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="dataCode">The dataCode parameter.</param>
    private static void AppendUInt16(
        List<byte> buffer,
        ushort value,
        CommunicationDataCode dataCode)
    {
        if (dataCode == CommunicationDataCode.Binary)
        {
            buffer.Add((byte)(value & 0xFF));
            buffer.Add((byte)(value >> 8));
            return;
        }

        buffer.AddRange(Encoding.ASCII.GetBytes(FormatAsciiUInt16(value)));
    }

    /// <summary>Executes the FormatAsciiByte operation.</summary>
    /// <param name="value">The value parameter.</param>
    /// <returns>The FormatAsciiByte operation result.</returns>
    private static string FormatAsciiByte(byte value) =>
        value.ToString("X2", CultureInfo.InvariantCulture);

    /// <summary>Executes the FormatAsciiUInt16 operation.</summary>
    /// <param name="value">The value parameter.</param>
    /// <returns>The FormatAsciiUInt16 operation result.</returns>
    private static string FormatAsciiUInt16(ushort value) =>
        value.ToString("X4", CultureInfo.InvariantCulture);
}
