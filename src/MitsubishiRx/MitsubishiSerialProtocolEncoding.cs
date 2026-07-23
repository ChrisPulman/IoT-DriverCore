// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Text;

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiSerialProtocolEncoding type.</summary>
internal static partial class MitsubishiSerialProtocolEncoding
{
    /// <summary>Stores the Enq field.</summary>
    private const byte Enq = 0x05;

    /// <summary>Stores the Ack field.</summary>
    private const byte Ack = 0x06;

    /// <summary>Stores the Nak field.</summary>
    private const byte Nak = 0x15;

    /// <summary>Stores the Stx field.</summary>
    private const byte Stx = 0x02;

    /// <summary>Stores the Etx field.</summary>
    private const byte Etx = 0x03;

    /// <summary>Stores the Cr field.</summary>
    private const byte Cr = 0x0D;

    /// <summary>Stores the Lf field.</summary>
    private const byte Lf = 0x0A;

    /// <summary>Stores the Dle field.</summary>
    private const byte Dle = 0x10;

    /// <summary>Stores the FourCFrameId field.</summary>
    private const byte FourCFrameId = 0xF8;

    /// <summary>Stores the ThreeCFrameId field.</summary>
    private const byte ThreeCFrameId = 0xF9;

    /// <summary>Stores the ResponseIdBinary field.</summary>
    private const ushort ResponseIdBinary = 0xFFFF;

    /// <summary>Executes the EncodeWordReadRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "address">The address parameter.</param>
    /// <param name = "points">The points parameter.</param>
    /// <returns>The EncodeWordReadRequest operation result.</returns>
    internal static byte[] EncodeWordReadRequest(
        MitsubishiClientOptions options,
        MitsubishiDeviceAddress address,
        int points)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);
        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => Encode1CRequest(options, address, points, wordUnits: true),
            MitsubishiFrameType.ThreeC => Encode3CRequest(
                options,
                address,
                points,
                wordUnits: true),
            MitsubishiFrameType.FourC => Encode4CRequest(options, address, points, wordUnits: true),
            _ => throw new NotSupportedException(
                $"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    /// <summary>Executes the EncodeWordWriteRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "address">The address parameter.</param>
    /// <param name = "values">The values parameter.</param>
    /// <returns>The EncodeWordWriteRequest operation result.</returns>
    internal static byte[] EncodeWordWriteRequest(
        MitsubishiClientOptions options,
        MitsubishiDeviceAddress address,
        IReadOnlyList<ushort> values)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException("At least one value must be supplied.", nameof(values));
        }

        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => Encode1CWriteRequest(options, address, values),
            MitsubishiFrameType.ThreeC => Encode3CWriteRequest(options, address, values),
            MitsubishiFrameType.FourC => Encode4CWriteRequest(options, address, values),
            _ => throw new NotSupportedException(
                $"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    /// <summary>Executes the EncodeBitReadRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "address">The address parameter.</param>
    /// <param name = "points">The points parameter.</param>
    /// <returns>The EncodeBitReadRequest operation result.</returns>
    internal static byte[] EncodeBitReadRequest(
        MitsubishiClientOptions options,
        MitsubishiDeviceAddress address,
        int points)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);
        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => Encode1CRequest(options, address, points, wordUnits: false),
            MitsubishiFrameType.ThreeC => Encode3CRequest(
                options,
                address,
                points,
                wordUnits: false),
            MitsubishiFrameType.FourC => Encode4CRequest(
                options,
                address,
                points,
                wordUnits: false),
            _ => throw new NotSupportedException(
                $"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    /// <summary>Executes the EncodeBitWriteRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "address">The address parameter.</param>
    /// <param name = "values">The values parameter.</param>
    /// <returns>The EncodeBitWriteRequest operation result.</returns>
    internal static byte[] EncodeBitWriteRequest(
        MitsubishiClientOptions options,
        MitsubishiDeviceAddress address,
        IReadOnlyList<bool> values)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException("At least one value must be supplied.", nameof(values));
        }

        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => Encode1CBitWriteRequest(options, address, values),
            MitsubishiFrameType.ThreeC => Encode3CBitWriteRequest(options, address, values),
            MitsubishiFrameType.FourC => Encode4CBitWriteRequest(options, address, values),
            _ => throw new NotSupportedException(
                $"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    /// <summary>Executes the EncodeRandomReadRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "addresses">The addresses parameter.</param>
    /// <returns>The EncodeRandomReadRequest operation result.</returns>
    internal static byte[] EncodeRandomReadRequest(
        MitsubishiClientOptions options,
        IReadOnlyList<MitsubishiDeviceAddress> addresses)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(addresses);
        if (addresses.Count == 0)
        {
            throw new ArgumentException("At least one device must be supplied.", nameof(addresses));
        }

        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => Encode1CRawRequest(
                options,
                MitsubishiCommandCodes.RandomRead,
                0x0000,
                Build1CRandomReadBody(addresses)),
            MitsubishiFrameType.ThreeC => Encode3CRandomReadRequest(options, addresses),
            MitsubishiFrameType.FourC => Encode4CRandomReadRequest(options, addresses),
            _ => throw new NotSupportedException(
                $"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    /// <summary>Executes the EncodeRandomWriteRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "values">The values parameter.</param>
    /// <returns>The EncodeRandomWriteRequest operation result.</returns>
    internal static byte[] EncodeRandomWriteRequest(
        MitsubishiClientOptions options,
        IReadOnlyList<MitsubishiDeviceValue> values)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException(
                "At least one device value must be supplied.",
                nameof(values));
        }

        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => Encode1CRawRequest(
                options,
                MitsubishiCommandCodes.RandomWrite,
                0x0000,
                Build1CRandomWriteBody(values)),
            MitsubishiFrameType.ThreeC => Encode3CRandomWriteRequest(options, values),
            MitsubishiFrameType.FourC => Encode4CRandomWriteRequest(options, values),
            _ => throw new NotSupportedException(
                $"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    /// <summary>Executes the EncodeBlockReadRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "request">The request parameter.</param>
    /// <returns>The EncodeBlockReadRequest operation result.</returns>
    internal static byte[] EncodeBlockReadRequest(
        MitsubishiClientOptions options,
        MitsubishiBlockRequest request)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(request);
        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => Encode1CRawRequest(
                options,
                MitsubishiCommandCodes.BlockRead,
                0x0000,
                Encoding.ASCII.GetBytes(FormatBlocksAscii(request, includeValues: false))),
            MitsubishiFrameType.ThreeC => Encode3CBlockReadRequest(options, request),
            MitsubishiFrameType.FourC => Encode4CBlockReadRequest(options, request),
            _ => throw new NotSupportedException(
                $"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    /// <summary>Executes the EncodeBlockWriteRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "request">The request parameter.</param>
    /// <returns>The EncodeBlockWriteRequest operation result.</returns>
    internal static byte[] EncodeBlockWriteRequest(
        MitsubishiClientOptions options,
        MitsubishiBlockRequest request)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(request);
        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => Encode1CRawRequest(
                options,
                MitsubishiCommandCodes.BlockWrite,
                0x0000,
                Encoding.ASCII.GetBytes(FormatBlocksAscii(request, includeValues: true))),
            MitsubishiFrameType.ThreeC => Encode3CBlockWriteRequest(options, request),
            MitsubishiFrameType.FourC => Encode4CBlockWriteRequest(options, request),
            _ => throw new NotSupportedException(
                $"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    /// <summary>Executes the EncodeMonitorRegistrationRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "addresses">The addresses parameter.</param>
    /// <returns>The EncodeMonitorRegistrationRequest operation result.</returns>
    internal static byte[] EncodeMonitorRegistrationRequest(
        MitsubishiClientOptions options,
        IReadOnlyList<MitsubishiDeviceAddress> addresses)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(addresses);
        if (addresses.Count == 0)
        {
            throw new ArgumentException("At least one device must be supplied.", nameof(addresses));
        }

        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => Encode1CRawRequest(
                options,
                MitsubishiCommandCodes.EntryMonitorDevice,
                0x0000,
                Build1CRandomReadBody(addresses)),
            MitsubishiFrameType.ThreeC => Encode3CMonitorRegistrationRequest(options, addresses),
            MitsubishiFrameType.FourC => Encode4CMonitorRegistrationRequest(options, addresses),
            _ => throw new NotSupportedException(
                $"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    /// <summary>Executes the EncodeExecuteMonitorRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <returns>The EncodeExecuteMonitorRequest operation result.</returns>
    internal static byte[] EncodeExecuteMonitorRequest(MitsubishiClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => Encode1CRawRequest(
                options,
                MitsubishiCommandCodes.ExecuteMonitor,
                0x0000,
                []),
            MitsubishiFrameType.ThreeC => Encode3CExecuteMonitorRequest(options),
            MitsubishiFrameType.FourC => Encode4CExecuteMonitorRequest(options),
            _ => throw new NotSupportedException(
                $"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    /// <summary>Executes the EncodeRemoteOperationRequest operation.</summary>
    /// <param name = "options">The options parameter.</param>
    /// <param name = "command">The command parameter.</param>
    /// <param name = "force">The force parameter.</param>
    /// <param name = "clearMode">The clearMode parameter.</param>
    /// <returns>The EncodeRemoteOperationRequest operation result.</returns>
    internal static byte[] EncodeRemoteOperationRequest(
        MitsubishiClientOptions options,
        ushort command,
        bool force = true,
        bool clearMode = false)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => Encode1CRawRequest(
                options,
                command,
                0x0000,
                Encoding.ASCII.GetBytes(
                    FormatRemoteOperationPayloadAscii(command, force, clearMode))),
            MitsubishiFrameType.ThreeC => Encode3CRemoteOperationRequest(
                options,
                command,
                force,
                clearMode),
            MitsubishiFrameType.FourC => Encode4CRemoteOperationRequest(
                options,
                command,
                force,
                clearMode),
            _ => throw new NotSupportedException(
                $"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }
}
