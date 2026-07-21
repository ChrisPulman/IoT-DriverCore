// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;

#else

namespace MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiProtocolEncoding type.</summary>
internal static partial class MitsubishiProtocolEncoding
{
    /// <summary>Executes the Encode operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <returns>The Encode operation result.</returns>
    internal static byte[] Encode(
        MitsubishiClientOptions options,
        MitsubishiRawCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(request);
        return options.FrameType switch
        {
            MitsubishiFrameType.OneE => Encode1E(options, request),
            MitsubishiFrameType.ThreeE => Encode3E(options, request),
            MitsubishiFrameType.FourE => Encode4E(options, request),
            _ => throw new ArgumentOutOfRangeException(nameof(options.FrameType)),
        };
    }

    /// <summary>Executes the Decode operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <param name="response">The response parameter.</param>
    /// <returns>The Decode operation result.</returns>
    internal static Responce<byte[]> Decode(
        MitsubishiClientOptions options,
        MitsubishiTransportRequest request,
        byte[] response)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);
        return options.DataCode switch
        {
            CommunicationDataCode.Ascii => options.FrameType == MitsubishiFrameType.OneE
                ? Decode1EAscii(response)
                : Decode3EOr4EAscii(response),
            _ => options.FrameType switch
            {
                MitsubishiFrameType.OneE => Decode1E(response),
                MitsubishiFrameType.ThreeE or MitsubishiFrameType.FourE => Decode3EOr4E(response),
                _ => throw new ArgumentOutOfRangeException(nameof(options.FrameType)),
            },
        };
    }

    /// <summary>Executes the GetFixedResponseLength operation.</summary>
    /// <param name="frameType">The frameType parameter.</param>
    /// <param name="dataCode">The dataCode parameter.</param>
    /// <param name="command">The command parameter.</param>
    /// <param name="subcommand">The subcommand parameter.</param>
    /// <param name="requestBodyLength">The requestBodyLength parameter.</param>
    /// <param name="explicitLength">The explicitLength parameter.</param>
    /// <returns>The GetFixedResponseLength operation result.</returns>
    internal static int? GetFixedResponseLength(
        MitsubishiFrameType frameType,
        CommunicationDataCode dataCode,
        ushort command,
        ushort subcommand,
        int requestBodyLength,
        int? explicitLength = null)
    {
        return explicitLength
            ?? frameType switch
            {
                MitsubishiFrameType.OneE => GetFixedResponseLength1E(
                    command,
                    requestBodyLength,
                    dataCode),
                MitsubishiFrameType.ThreeE or MitsubishiFrameType.FourE => null,
                _ => null,
            };
    }

    /// <summary>Executes the EncodeDeviceBatchRead operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="bitUnits">The bitUnits parameter.</param>
    /// <returns>The EncodeDeviceBatchRead operation result.</returns>
    internal static byte[] EncodeDeviceBatchRead(
        MitsubishiClientOptions options,
        MitsubishiDeviceAddress address,
        int points,
        bool bitUnits)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);
        var subcommand =
            options.FrameType == MitsubishiFrameType.OneE
                ? (ushort)(bitUnits ? 0x0000 : 0x0001)
                : (ushort)(bitUnits ? 0x0001 : 0x0000);
        var body =
            options.FrameType == MitsubishiFrameType.OneE
                ? Encode1EDeviceBody(address, points, options)
                : Encode3EDeviceBody(address, points, options);
        return Encode(
            options,
            new MitsubishiRawCommandRequest(
                MitsubishiCommandCodes.DeviceRead,
                subcommand,
                body,
                $"Read {address.Original}"));
    }

    /// <summary>Executes the EncodeDeviceBatchWrite operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="address">The address parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="bitUnits">The bitUnits parameter.</param>
    /// <returns>The EncodeDeviceBatchWrite operation result.</returns>
    internal static byte[] EncodeDeviceBatchWrite(
        MitsubishiClientOptions options,
        MitsubishiDeviceAddress address,
        ReadOnlySpan<ushort> values,
        bool bitUnits)
    {
        if (values.IsEmpty)
        {
            throw new ArgumentException("At least one value must be supplied.", nameof(values));
        }

        var subcommand =
            options.FrameType == MitsubishiFrameType.OneE
                ? (ushort)(bitUnits ? 0x0002 : 0x0003)
                : (ushort)(bitUnits ? 0x0001 : 0x0000);
        var body =
            options.FrameType == MitsubishiFrameType.OneE
                ? Encode1EDeviceWriteBody(address, values, options, bitUnits)
                : Encode3EDeviceWriteBody(address, values, options, bitUnits);
        return Encode(
            options,
            new MitsubishiRawCommandRequest(
                MitsubishiCommandCodes.DeviceWrite,
                subcommand,
                body,
                $"Write {address.Original}"));
    }

    /// <summary>Executes the EncodeRandomRead operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="wordDevices">The wordDevices parameter.</param>
    /// <returns>The EncodeRandomRead operation result.</returns>
    internal static byte[] EncodeRandomRead(
        MitsubishiClientOptions options,
        IReadOnlyList<MitsubishiDeviceAddress> wordDevices)
    {
        ArgumentNullException.ThrowIfNull(wordDevices);
        if (wordDevices.Count == 0)
        {
            throw new ArgumentException(
                "At least one device must be supplied.",
                nameof(wordDevices));
        }

        var body =
            options.FrameType == MitsubishiFrameType.OneE
                ? throw new NotSupportedException(
                    "1E random read is not implemented in this release.")
                : Encode3ERandomReadBody(wordDevices, options);
        return Encode(
            options,
            new MitsubishiRawCommandRequest(
                MitsubishiCommandCodes.RandomRead,
                0x0000,
                body,
                "Random read"));
    }

    /// <summary>Executes the EncodeRandomWrite operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <returns>The EncodeRandomWrite operation result.</returns>
    internal static byte[] EncodeRandomWrite(
        MitsubishiClientOptions options,
        IReadOnlyList<MitsubishiDeviceValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException(
                "At least one device value must be supplied.",
                nameof(values));
        }

        var body =
            options.FrameType == MitsubishiFrameType.OneE
                ? throw new NotSupportedException(
                    "1E random write is not implemented in this release.")
                : Encode3ERandomWriteBody(values, options);
        return Encode(
            options,
            new MitsubishiRawCommandRequest(
                MitsubishiCommandCodes.RandomWrite,
                0x0000,
                body,
                "Random write"));
    }

    /// <summary>Executes the EncodeMonitorRegistration operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="wordDevices">The wordDevices parameter.</param>
    /// <returns>The EncodeMonitorRegistration operation result.</returns>
    internal static byte[] EncodeMonitorRegistration(
        MitsubishiClientOptions options,
        IReadOnlyList<MitsubishiDeviceAddress> wordDevices)
    {
        ArgumentNullException.ThrowIfNull(wordDevices);
        if (wordDevices.Count == 0)
        {
            throw new ArgumentException(
                "At least one device must be supplied.",
                nameof(wordDevices));
        }

        var body =
            options.FrameType == MitsubishiFrameType.OneE
                ? throw new NotSupportedException(
                    "1E monitor registration is not implemented in this release.")
                : Encode3ERandomReadBody(wordDevices, options);
        return Encode(
            options,
            new MitsubishiRawCommandRequest(
                MitsubishiCommandCodes.EntryMonitorDevice,
                0x0000,
                body,
                "Entry monitor device"));
    }

    /// <summary>Executes the EncodeExecuteMonitor operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <returns>The EncodeExecuteMonitor operation result.</returns>
    internal static byte[] EncodeExecuteMonitor(MitsubishiClientOptions options) =>
        Encode(
            options,
            new MitsubishiRawCommandRequest(
                MitsubishiCommandCodes.ExecuteMonitor,
                0x0000,
                [],
                "Execute monitor"));

    /// <summary>Executes the EncodeBlockRead operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <returns>The EncodeBlockRead operation result.</returns>
    internal static byte[] EncodeBlockRead(
        MitsubishiClientOptions options,
        MitsubishiBlockRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (options.FrameType == MitsubishiFrameType.OneE)
        {
            throw new NotSupportedException("1E block read is not implemented in this release.");
        }

        var body = Encode3EBlocks(request, options, includeValues: false);
        return Encode(
            options,
            new MitsubishiRawCommandRequest(
                MitsubishiCommandCodes.BlockRead,
                0x0000,
                body,
                "Block read"));
    }

    /// <summary>Executes the EncodeBlockWrite operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <returns>The EncodeBlockWrite operation result.</returns>
    internal static byte[] EncodeBlockWrite(
        MitsubishiClientOptions options,
        MitsubishiBlockRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (options.FrameType == MitsubishiFrameType.OneE)
        {
            throw new NotSupportedException("1E block write is not implemented in this release.");
        }

        var body = Encode3EBlocks(request, options, includeValues: true);
        return Encode(
            options,
            new MitsubishiRawCommandRequest(
                MitsubishiCommandCodes.BlockWrite,
                0x0000,
                body,
                "Block write"));
    }

    /// <summary>Executes the EncodeReadTypeName operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <returns>The EncodeReadTypeName operation result.</returns>
    internal static byte[] EncodeReadTypeName(MitsubishiClientOptions options) =>
        Encode(
            options,
            new MitsubishiRawCommandRequest(
                MitsubishiCommandCodes.ReadTypeName,
                0x0000,
                [],
                "Read type name"));

    /// <summary>Executes the EncodeRemoteOperation operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="command">The command parameter.</param>
    /// <param name="force">The force parameter.</param>
    /// <param name="clearMode">The clearMode parameter.</param>
    /// <returns>The EncodeRemoteOperation operation result.</returns>
    internal static byte[] EncodeRemoteOperation(
        MitsubishiClientOptions options,
        ushort command,
        bool force = true,
        bool clearMode = false)
    {
        byte[] body = command switch
        {
            MitsubishiCommandCodes.RemoteRun => options.DataCode == CommunicationDataCode.Binary
                ? [Convert.ToByte(force ? 0x01 : 0x00), Convert.ToByte(clearMode ? 0x01 : 0x00)]
                : Encoding.ASCII.GetBytes(
                    (force ? "0001" : "0000") + (clearMode ? "0001" : "0000")),
            MitsubishiCommandCodes.RemoteStop
            or MitsubishiCommandCodes.RemotePause
            or MitsubishiCommandCodes.RemoteLatchClear
            or MitsubishiCommandCodes.RemoteReset => Array.Empty<byte>(),
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };
        return Encode(
            options,
            new MitsubishiRawCommandRequest(command, 0x0000, body, $"Remote op {command:X4}"));
    }

    /// <summary>Executes the EncodeLoopback operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="data">The data parameter.</param>
    /// <returns>The EncodeLoopback operation result.</returns>
    internal static byte[] EncodeLoopback(MitsubishiClientOptions options, ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            throw new ArgumentException("Loopback payload must not be empty.", nameof(data));
        }

        var body = (options.FrameType, options.DataCode) switch
        {
            (MitsubishiFrameType.OneE, CommunicationDataCode.Binary) => BuildLoopback1EBinary(data),
            (MitsubishiFrameType.OneE, _) => BuildLoopback1EAscii(data),
            (_, CommunicationDataCode.Binary) => BuildLoopback3EBinary(data),
            _ => BuildLoopback3EAscii(data),
        };

        return Encode(
            options,
            new MitsubishiRawCommandRequest(
                MitsubishiCommandCodes.LoopbackTest,
                0x0000,
                body,
                "Loopback"));
    }
}
