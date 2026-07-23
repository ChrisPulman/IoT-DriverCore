// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiProtocolEncoding type.</summary>
internal static partial class MitsubishiProtocolEncoding
{
    /// <summary>Executes the Append1EAsciiWriteValues operation.</summary>
    /// <param name="builder">The builder parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="bitUnits">The bitUnits parameter.</param>
    private static void Append1EAsciiWriteValues(
        StringBuilder builder,
        ReadOnlySpan<ushort> values,
        bool bitUnits)
    {
        if (bitUnits)
        {
            foreach (var value in values)
            {
                _ = builder.Append(value == 0 ? '0' : '1');
            }

            return;
        }

        foreach (var value in values)
        {
            _ = builder.Append(FormatAsciiUInt16(value));
        }
    }

    /// <summary>Executes the Encode3EDeviceBody operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="options">The options parameter.</param>
    /// <returns>The Encode3EDeviceBody operation result.</returns>
    private static byte[] Encode3EDeviceBody(
        MitsubishiDeviceAddress address,
        int points,
        MitsubishiClientOptions options)
    {
        var buffer = new List<byte>();
        AppendDeviceAddress(buffer, address, options, legacy: false);
        AppendUInt16(buffer, checked((ushort)points), options.DataCode);
        return buffer.ToArray();
    }

    /// <summary>Executes the Encode3EDeviceWriteBody operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="options">The options parameter.</param>
    /// <param name="bitUnits">The bitUnits parameter.</param>
    /// <returns>The Encode3EDeviceWriteBody operation result.</returns>
    private static byte[] Encode3EDeviceWriteBody(
        MitsubishiDeviceAddress address,
        ReadOnlySpan<ushort> values,
        MitsubishiClientOptions options,
        bool bitUnits)
    {
        var buffer = new List<byte>();
        buffer.AddRange(Encode3EDeviceBody(address, values.Length, options));
        Append3EWriteValues(buffer, values, options, bitUnits);
        return buffer.ToArray();
    }

    /// <summary>Executes the Append3EWriteValues operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="options">The options parameter.</param>
    /// <param name="bitUnits">The bitUnits parameter.</param>
    private static void Append3EWriteValues(
        List<byte> buffer,
        ReadOnlySpan<ushort> values,
        MitsubishiClientOptions options,
        bool bitUnits)
    {
        if (options.DataCode == CommunicationDataCode.Binary)
        {
            Append3EBinaryWriteValues(buffer, values, options, bitUnits);
            return;
        }

        Append3EAsciiWriteValues(buffer, values, bitUnits);
    }

    /// <summary>Executes the Append3EBinaryWriteValues operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="options">The options parameter.</param>
    /// <param name="bitUnits">The bitUnits parameter.</param>
    private static void Append3EBinaryWriteValues(
        List<byte> buffer,
        ReadOnlySpan<ushort> values,
        MitsubishiClientOptions options,
        bool bitUnits)
    {
        if (bitUnits)
        {
            foreach (var value in values)
            {
                buffer.Add(Convert.ToByte(value == 0 ? 0x00 : 0x10));
            }

            return;
        }

        foreach (var value in values)
        {
            AppendUInt16(buffer, value, options.DataCode);
        }
    }

    /// <summary>Executes the Append3EAsciiWriteValues operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="bitUnits">The bitUnits parameter.</param>
    private static void Append3EAsciiWriteValues(
        List<byte> buffer,
        ReadOnlySpan<ushort> values,
        bool bitUnits)
    {
        if (bitUnits)
        {
            foreach (var value in values)
            {
                buffer.Add((byte)(value == 0 ? '0' : '1'));
            }

            return;
        }

        foreach (var value in values)
        {
            buffer.AddRange(Encoding.ASCII.GetBytes(FormatAsciiUInt16(value)));
        }
    }

    /// <summary>Executes the Encode3ERandomReadBody operation.</summary>
    /// <param name="devices">The devices parameter.</param>
    /// <param name="options">The options parameter.</param>
    /// <returns>The Encode3ERandomReadBody operation result.</returns>
    private static byte[] Encode3ERandomReadBody(
        IReadOnlyList<MitsubishiDeviceAddress> devices,
        MitsubishiClientOptions options)
    {
        var buffer = new List<byte>();
        AppendByte(buffer, checked((byte)devices.Count), options.DataCode);
        AppendByte(buffer, 0x00, options.DataCode);
        foreach (var device in devices)
        {
            AppendDeviceAddress(buffer, device, options, legacy: false);
        }

        return buffer.ToArray();
    }

    /// <summary>Executes the Encode3ERandomWriteBody operation.</summary>
    /// <param name="values">The values parameter.</param>
    /// <param name="options">The options parameter.</param>
    /// <returns>The Encode3ERandomWriteBody operation result.</returns>
    private static byte[] Encode3ERandomWriteBody(
        IReadOnlyList<MitsubishiDeviceValue> values,
        MitsubishiClientOptions options)
    {
        var buffer = new List<byte>();
        AppendUInt16(buffer, checked((ushort)values.Count), options.DataCode);
        AppendUInt16(buffer, 0x0000, options.DataCode);
        foreach (var value in values)
        {
            AppendDeviceAddress(buffer, value.Address, options, legacy: false);
            AppendUInt16(buffer, value.Value, options.DataCode);
        }

        return buffer.ToArray();
    }

    /// <summary>Executes the Encode3EBlocks operation.</summary>
    /// <param name="request">The request parameter.</param>
    /// <param name="options">The options parameter.</param>
    /// <param name="includeValues">The includeValues parameter.</param>
    /// <returns>The Encode3EBlocks operation result.</returns>
    private static byte[] Encode3EBlocks(
        MitsubishiBlockRequest request,
        MitsubishiClientOptions options,
        bool includeValues)
    {
        var buffer = new List<byte>();
        AppendUInt16(buffer, checked((ushort)request.ResolvedWordBlocks.Count), options.DataCode);
        AppendUInt16(buffer, checked((ushort)request.ResolvedBitBlocks.Count), options.DataCode);
        foreach (var block in request.ResolvedWordBlocks)
        {
            Append3EWordBlock(buffer, block, options, includeValues);
        }

        foreach (var block in request.ResolvedBitBlocks)
        {
            Append3EBitBlock(buffer, block, options, includeValues);
        }

        return buffer.ToArray();
    }

    /// <summary>Executes the Append3EWordBlock operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="block">The block parameter.</param>
    /// <param name="options">The options parameter.</param>
    /// <param name="includeValues">The includeValues parameter.</param>
    private static void Append3EWordBlock(
        List<byte> buffer,
        MitsubishiWordBlock block,
        MitsubishiClientOptions options,
        bool includeValues)
    {
        AppendDeviceAddress(buffer, block.Address, options, legacy: false);
        AppendUInt16(buffer, checked((ushort)block.Values.Length), options.DataCode);
        if (!includeValues)
        {
            return;
        }

        foreach (var value in block.Values.Span)
        {
            AppendUInt16(buffer, value, options.DataCode);
        }
    }

    /// <summary>Executes the Append3EBitBlock operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="block">The block parameter.</param>
    /// <param name="options">The options parameter.</param>
    /// <param name="includeValues">The includeValues parameter.</param>
    private static void Append3EBitBlock(
        List<byte> buffer,
        MitsubishiBitBlock block,
        MitsubishiClientOptions options,
        bool includeValues)
    {
        AppendDeviceAddress(buffer, block.Address, options, legacy: false);
        AppendUInt16(buffer, checked((ushort)block.Values.Length), options.DataCode);
        if (!includeValues)
        {
            return;
        }

        foreach (var value in block.Values.Span)
        {
            AppendByte(buffer, value ? (byte)0x10 : (byte)0x00, options.DataCode);
        }
    }

    /// <summary>Executes the BuildLoopback1EBinary operation.</summary>
    /// <param name="data">The data parameter.</param>
    /// <returns>The BuildLoopback1EBinary operation result.</returns>
    private static byte[] BuildLoopback1EBinary(ReadOnlySpan<byte> data)
    {
        var buffer = new List<byte> { (byte)data.Length, 0x00 };
        buffer.AddRange(data.ToArray());
        return buffer.ToArray();
    }

    /// <summary>Executes the BuildLoopback1EAscii operation.</summary>
    /// <param name="data">The data parameter.</param>
    /// <returns>The BuildLoopback1EAscii operation result.</returns>
    private static byte[] BuildLoopback1EAscii(ReadOnlySpan<byte> data) =>
        Encoding.ASCII.GetBytes(
            FormatAsciiByte((byte)data.Length) + Encoding.ASCII.GetString(data));

    /// <summary>Executes the BuildLoopback3EBinary operation.</summary>
    /// <param name="data">The data parameter.</param>
    /// <returns>The BuildLoopback3EBinary operation result.</returns>
    private static byte[] BuildLoopback3EBinary(ReadOnlySpan<byte> data)
    {
        var buffer = new List<byte>();
        AppendUInt16(buffer, checked((ushort)data.Length), CommunicationDataCode.Binary);
        buffer.AddRange(data.ToArray());
        return buffer.ToArray();
    }

    /// <summary>Executes the BuildLoopback3EAscii operation.</summary>
    /// <param name="data">The data parameter.</param>
    /// <returns>The BuildLoopback3EAscii operation result.</returns>
    private static byte[] BuildLoopback3EAscii(ReadOnlySpan<byte> data) =>
        Encoding.ASCII.GetBytes(
            FormatAsciiUInt16((ushort)data.Length) + Encoding.ASCII.GetString(data));

    /// <summary>Executes the BuildBinaryPasswordPayload operation.</summary>
    /// <param name="password">The password parameter.</param>
    /// <returns>The BuildBinaryPasswordPayload operation result.</returns>
    private static byte[] BuildBinaryPasswordPayload(string password)
    {
        var bytes = Encoding.ASCII.GetBytes(password);
        var buffer = new List<byte>();
        AppendUInt16(buffer, checked((ushort)bytes.Length), CommunicationDataCode.Binary);
        buffer.AddRange(bytes);
        return buffer.ToArray();
    }

    /// <summary>Executes the BuildAsciiPasswordPayload operation.</summary>
    /// <param name="password">The password parameter.</param>
    /// <returns>The BuildAsciiPasswordPayload operation result.</returns>
    private static byte[] BuildAsciiPasswordPayload(string password) =>
        Encoding.ASCII.GetBytes(
            FormatAsciiUInt16((ushort)password.Length) + password.ToUpperInvariant());

    /// <summary>Executes the Append3EOr4ESubheader operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="dataCode">The dataCode parameter.</param>
    /// <param name="fourE">The fourE parameter.</param>
    /// <param name="serialNumber">The serialNumber parameter.</param>
    private static void Append3EOr4ESubheader(
        List<byte> buffer,
        CommunicationDataCode dataCode,
        bool fourE,
        ushort serialNumber)
    {
        if (dataCode == CommunicationDataCode.Binary)
        {
            if (fourE)
            {
                buffer.Add(0x54);
                buffer.Add(0x00);
                AppendUInt16(buffer, serialNumber, dataCode);
                buffer.Add(0x00);
                buffer.Add(0x00);
            }
            else
            {
                buffer.Add(0x50);
                buffer.Add(0x00);
            }

            return;
        }

        if (fourE)
        {
            buffer.AddRange(Encoding.ASCII.GetBytes($"5400{serialNumber:X4}0000"));
        }
        else
        {
            buffer.AddRange(Encoding.ASCII.GetBytes("5000"));
        }
    }

    /// <summary>Executes the AppendRoute operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="route">The route parameter.</param>
    /// <param name="dataCode">The dataCode parameter.</param>
    private static void AppendRoute(
        List<byte> buffer,
        MitsubishiRoute route,
        CommunicationDataCode dataCode)
    {
        AppendByte(buffer, route.NetworkNumber, dataCode);
        AppendByte(buffer, route.StationNumber, dataCode);
        AppendUInt16(buffer, route.ModuleIoNumber, dataCode);
        AppendByte(buffer, route.MultidropStationNumber, dataCode);
    }
}
