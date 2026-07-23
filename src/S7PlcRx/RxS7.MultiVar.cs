// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
#if REACTIVE_SHIM
using IoT.DriverCore.S7PlcRx.Reactive.Core;
using IoT.DriverCore.S7PlcRx.Reactive.Enums;
using IoT.DriverCore.S7PlcRx.Reactive.PlcTypes;
#else
using IoT.DriverCore.S7PlcRx.Core;
using IoT.DriverCore.S7PlcRx.Enums;
using IoT.DriverCore.S7PlcRx.PlcTypes;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.S7PlcRx.Reactive;
#else
namespace IoT.DriverCore.S7PlcRx;
#endif

/// <summary>Contains multi-variable operation members for <see cref="RxS7"/>.</summary>
public partial class RxS7
{
    /// <summary>Returns whether the value is null, empty, or contains only whitespace.</summary>
    /// <param name="value">The value to inspect.</param>
    /// <returns>true when no non-whitespace text is present; otherwise, false.</returns>
    private static bool HasNoText(string? value)
    {
        if (value is null)
        {
            return true;
        }

        foreach (var character in value)
        {
            if (!char.IsWhiteSpace(character))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Disposes a semaphore while preserving debug diagnostics.</summary>
    /// <param name="semaphore">The semaphore to dispose.</param>
    /// <param name="name">The semaphore diagnostic name.</param>
    private static void DisposeSemaphore(SemaphoreSlim semaphore, string name)
    {
        try
        {
            semaphore.Wait();
            semaphore.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{name} disposal failed. {ex}");
        }
    }

    /// <summary>Builds read-var request items from tags.</summary>
    /// <param name="tags">The tags to read.</param>
    /// <param name="items">The generated read items.</param>
    /// <param name="varTypes">The parsed variable types.</param>
    /// <param name="arrayLengths">The tag array lengths.</param>
    /// <returns>true when all tags are valid; otherwise, false.</returns>
    private static bool TryBuildMultiVarReadItems(
        IReadOnlyList<Tag> tags,
        out List<S7MultiVar.ReadItem> items,
        out VarType[] varTypes,
        out int[] arrayLengths)
    {
        items = new(tags.Count);
        varTypes = new VarType[tags.Count];
        arrayLengths = new int[tags.Count];

        for (var i = 0; i < tags.Count; i++)
        {
            var tag = tags[i];
            if (tag is null || string.IsNullOrWhiteSpace(tag.Name) ||
                !TryParseDbAddressForMultiVar(tag, out var db, out var startByte, out var varType, out var countBytes))
            {
                return false;
            }

            varTypes[i] = varType;
            arrayLengths[i] = tag.ArrayLength!.Value;
            items.Add(new S7MultiVar.ReadItem(DataType.DataBlock, db, startByte, countBytes, tag.Name!));
        }

        return true;
    }

    /// <summary>Builds write-var request items from tags.</summary>
    /// <param name="tags">The tags to write.</param>
    /// <param name="items">The generated write items.</param>
    /// <returns>true when all tags are valid and serializable; otherwise, false.</returns>
    private static bool TryBuildMultiVarWriteItems(IReadOnlyList<Tag> tags, out List<S7MultiVar.WriteItem> items)
    {
        items = new(tags.Count);
        for (var i = 0; i < tags.Count; i++)
        {
            var tag = tags[i];
            if (tag is null || string.IsNullOrWhiteSpace(tag.Name) || tag.NewValue is null)
            {
                return false;
            }

            if (!TryParseDbAddressForMultiVar(tag, out var db, out var startByte, out _, out var countBytes))
            {
                return false;
            }

            if (!TrySerializeTagNewValue(tag, out var transportSize, out var data))
            {
                return false;
            }

            items.Add(new S7MultiVar.WriteItem(
                DataType.DataBlock,
                db,
                startByte,
                countBytes,
                transportSize,
                data,
                tag.Name!));
        }

        return true;
    }

    /// <summary>Checks parsed multi-var write response results.</summary>
    /// <param name="receiveBuffer">The response buffer.</param>
    /// <param name="itemCount">The expected item count.</param>
    /// <returns>true when all write results succeeded; otherwise, false.</returns>
    private static bool AreMultiVarWriteResultsSuccessful(byte[] receiveBuffer, int itemCount)
    {
        var results = S7MultiVar.ParseWriteVarResponse(receiveBuffer, itemCount);
        if (results.Count != itemCount)
        {
            return false;
        }

        foreach (var result in results)
        {
            if (result.ReturnCode != 0xFF)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Sends a multi-var request and receives the response.</summary>
    /// <param name="socketRx">The socket wrapper to use.</param>
    /// <param name="tag">The tag used for error context.</param>
    /// <param name="request">The request buffer.</param>
    /// <param name="receiveBuffer">The receive buffer.</param>
    /// <returns>true when the request was sent and a response was received; otherwise, false.</returns>
    private static bool TrySendMultiVarRequest(S7SocketRx socketRx, Tag tag, byte[] request, byte[] receiveBuffer)
    {
        if (request.Length == 0)
        {
            return false;
        }

        var sent = socketRx.Send(tag, request, request.Length);
        if (sent != request.Length)
        {
            return false;
        }

        Array.Clear(receiveBuffer, 0, receiveBuffer.Length);
        _ = socketRx.Receive(tag, receiveBuffer, receiveBuffer.Length);
        return true;
    }

    /// <summary>Creates a dictionary from parsed multi-var read results.</summary>
    /// <param name="tags">The original tags.</param>
    /// <param name="parsed">The parsed read results.</param>
    /// <param name="varTypes">The variable types.</param>
    /// <param name="arrayLengths">The array lengths.</param>
    /// <param name="parseBytes">The byte parser.</param>
    /// <returns>The read result dictionary.</returns>
    private static Dictionary<string, object?> CreateMultiVarReadResult(
        IReadOnlyList<Tag> tags,
        List<S7MultiVar.ReadResult> parsed,
        VarType[] varTypes,
        int[] arrayLengths,
        Func<VarType, byte[], int, Type, object?> parseBytes)
    {
        var dict = new Dictionary<string, object?>(tags.Count, StringComparer.InvariantCultureIgnoreCase);
        for (var i = 0; i < tags.Count && i < parsed.Count; i++)
        {
            var result = parsed[i];
            if (result.ReturnCode != 0xFF || result.Data.IsEmpty)
            {
                dict[tags[i].Name!] = default;
                continue;
            }

            dict[tags[i].Name!] = parseBytes(
                varTypes[i],
                result.Data.ToArray(),
                arrayLengths[i],
                tags[i].Type);
        }

        return dict;
    }

    /// <summary>Serializes the specified tag's new value into a byte array suitable for transport.</summary>
    /// <remarks>The method supports serialization for common primitive types, arrays of bytes, and strings.
    /// If the tag's type is not supported or the new value is null, the method returns false and outputs an empty
    /// array.</remarks>
    /// <param name="tag">The tag whose new value is to be serialized. The tag's type and new value determine
    /// the serialization format.</param>
    /// <param name="transportSize">When this method returns, contains the transport size code associated with
    /// the serialized
    /// data,
    /// if
    /// serialization
    /// succeeds.</param>
    /// <param name="data">When this method returns, contains the serialized byte array representation of the tag's new
    /// value,
    /// if
    /// serialization succeeds; otherwise, an empty array.</param>
    /// <returns>true if the tag's new value was successfully serialized; otherwise, false.</returns>
    private static bool TrySerializeTagNewValue(Tag tag, out byte transportSize, out byte[] data)
    {
        transportSize = ByteTransportSize;
        data = [];

        return tag.NewValue is not null &&
            (TrySerializeScalarTagValue(tag, out data) || TrySerializeArrayTagValue(tag, out data));
    }

    /// <summary>Attempts to serialize a scalar tag value.</summary>
    /// <param name="tag">The tag to serialize.</param>
    /// <param name="data">The serialized bytes.</param>
    /// <returns>true when serialization succeeds; otherwise, false.</returns>
    private static bool TrySerializeScalarTagValue(Tag tag, out byte[] data)
    {
        data = [];
        switch (tag.Type.Name)
        {
            case "Boolean" or "Byte":
                {
                    data = [Convert.ToByte(tag.NewValue, CultureInfo.InvariantCulture)];
                    return true;
                }

            case "Int16" or "short":
                {
                    data = Int.ToByteArray(Convert.ToInt16(tag.NewValue, CultureInfo.InvariantCulture));
                    return true;
                }

            case "UInt16" or "ushort":
                {
                    data = Word.ToByteArray(Convert.ToUInt16(tag.NewValue, CultureInfo.InvariantCulture));
                    return true;
                }

            case "Int32" or "int":
                {
                    data = DInt.ToByteArray(Convert.ToInt32(tag.NewValue, CultureInfo.InvariantCulture));
                    return true;
                }

            case "UInt32" or "uint":
                {
                    data = DWord.ToByteArray(Convert.ToUInt32(tag.NewValue, CultureInfo.InvariantCulture));
                    return true;
                }

            case "Single":
                {
                    data = Real.ToByteArray((float)tag.NewValue!);
                    return true;
                }

            case "Double":
                {
                    data = LReal.ToByteArray((double)tag.NewValue!);
                    return true;
                }

            default:
                {
                    return false;
                }
        }
    }

    /// <summary>Attempts to serialize an array or string tag value.</summary>
    /// <param name="tag">The tag to serialize.</param>
    /// <param name="data">The serialized bytes.</param>
    /// <returns>true when serialization succeeds; otherwise, false.</returns>
    private static bool TrySerializeArrayTagValue(Tag tag, out byte[] data)
    {
        data = [];
        switch (tag.Type.Name)
        {
            case "Byte[]":
                {
                    data = (byte[])tag.NewValue!;
                    return true;
                }

            case "Int16[]" or "short[]":
                {
                    data = Int.ToByteArray((short[])tag.NewValue!);
                    return true;
                }

            case "UInt16[]" or "ushort[]":
                {
                    data = Word.ToByteArray((ushort[])tag.NewValue!);
                    return true;
                }

            case "Int32[]" or "int[]":
                {
                    data = DInt.ToByteArray((int[])tag.NewValue!);
                    return true;
                }

            case "UInt32[]" or "uint[]" when tag.NewValue is uint[] unsignedValues:
                {
                    data = DWord.ToByteArray(unsignedValues);
                    return true;
                }

            case "Single[]":
                {
                    data = Real.ToByteArray((float[])tag.NewValue!);
                    return true;
                }

            case "Double[]":
                {
                    data = LReal.ToByteArray((double[])tag.NewValue!);
                    return true;
                }

            case "String":
                {
                    data = PlcTypes.String.ToByteArray(tag.NewValue as string);
                    return true;
                }

            default:
                {
                    return false;
                }
        }
    }
}
