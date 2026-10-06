// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.IO;

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic.Protocol;
#else
namespace IoT.Driver.S7PlcRx.Symbolic.Protocol;
#endif
/// <summary>Encodes the request fields and validates response elements.</summary>
internal sealed partial class S7PlusSession
{
    /// <summary>Rejects a missing operation argument on each supported framework.</summary>
    /// <param name="value">The argument value.</param>
    /// <param name="name">The argument name.</param>
    private static void RequireNotNull(object? value, string name)
    {
#if NETFRAMEWORK
        if (value is not null)
        {
            return;
        }

        throw new ArgumentNullException(name);
#else
        ArgumentNullException.ThrowIfNull(value, name);
#endif
    }

    /// <summary>Handles Positive limit.</summary>
    /// <param name = "value">The value.</param>
    /// <returns>The operation result.</returns>
    private static int PositiveLimit(S7SymbolicValue value)
    {
        var limit = Convert.ToUInt32(value.Value, System.Globalization.CultureInfo.InvariantCulture);
        if (limit == 0 || limit > MaximumBatchLimit)
        {
            throw new InvalidDataException("Controller resource limit is outside the supported range.");
        }

        return (int)limit;
    }

    /// <summary>Handles Uses modern authentication.</summary>
    /// <param name = "serverVersion">The serverVersion.</param>
    /// <returns>The operation result.</returns>
    private static bool UsesModernAuthentication(S7SymbolicValue serverVersion)
    {
        if (serverVersion.Value is not S7SymbolicStruct version || !version.Fields.TryGetValue(PaomVersionId, out var paom) || paom.Value is not string text)
        {
            throw new InvalidDataException("Cannot select authentication without the controller version.");
        }

        var parts = text.Split(';');
        if (parts.Length < 3 || !Version.TryParse(parts[^1].TrimStart('V', 'S'), out var firmware))
        {
            throw new InvalidDataException("Controller firmware version is malformed.");
        }

        var model = FindModelFamily(parts[1]);
        if (model == '\0')
        {
            throw new InvalidDataException("Controller model identifier is malformed.");
        }

        return model switch
        {
            '5' => firmware >= new Version(3, 1),
            '2' => text.Contains("50-0XB0") || firmware >= new Version(4, 7),
            '6' => false,
            _ => throw new NotSupportedException("Controller model does not advertise a supported authentication mode."),
        };
    }

    /// <summary>Reads the model family from a controller order number.</summary>
    /// <param name = "orderNumber">The controller order number.</param>
    /// <returns>The model family digit, or zero when unavailable.</returns>
    private static char FindModelFamily(string orderNumber)
    {
        for (var index = 0; index < orderNumber.Length; index++)
        {
            if (orderNumber[index] is not ('1' or '7'))
            {
                continue;
            }

            var start = index + 1;
            while (start < orderNumber.Length && char.IsWhiteSpace(orderNumber[start]))
            {
                start++;
            }

            if (start + ThirdModelDigitOffset < orderNumber.Length &&
                char.IsDigit(orderNumber[start]) &&
                char.IsDigit(orderNumber[start + 1]) &&
                char.IsDigit(orderNumber[start + ThirdModelDigitOffset]))
            {
                return orderNumber[start];
            }
        }

        return '\0';
    }

    /// <summary>Converts a protocol byte array into challenge bytes.</summary>
    /// <param name = "values">The wire array elements.</param>
    /// <returns>The byte array.</returns>
    private static byte[] ChallengeBytes(object?[] values)
    {
        var bytes = new byte[values.Length];
        for (var index = 0; index < values.Length; index++)
        {
            bytes[index] = Convert.ToByte(values[index], System.Globalization.CultureInfo.InvariantCulture);
        }

        return bytes;
    }

    /// <summary>Handles Qualifier.</summary>
    /// <param name = "writer">The writer.</param>
    private static void Qualifier(PlusWriter writer)
    {
        writer.UInt32(QualifierId);
        writer.VarUInt32(QualifierRidId);
        new S7SymbolicValue(S7SymbolicDataType.RID, 0U).WriteTo(writer);
        writer.VarUInt32(QualifierAidId);
        new S7SymbolicValue(S7SymbolicDataType.AID, 0U).WriteTo(writer);
        writer.VarUInt32(QualifierValueId);
        new S7SymbolicValue(S7SymbolicDataType.UDInt, 0U).WriteTo(writer);
        writer.Byte(0);
    }

    /// <summary>Handles Check return.</summary>
    /// <param name = "reader">The reader.</param>
    private static void CheckReturn(PlusReader reader)
    {
        var code = reader.VarUInt64();
        if (code == 0)
        {
            return;
        }

        throw new S7SymbolicException($"Controller returned symbolic error 0x{code:X}.", code);
    }

    /// <summary>Handles Read object ids.</summary>
    /// <param name = "reader">The reader.</param>
    /// <returns>The operation result.</returns>
    private static uint[] ReadObjectIds(PlusReader reader)
    {
        var ids = new uint[reader.Byte()];
        for (var index = 0; index < ids.Length; index++)
        {
            ids[index] = reader.VarUInt32();
        }

        return ids;
    }

    /// <summary>Handles Create body.</summary>
    /// <param name = "parentId">The parentId.</param>
    /// <param name = "obj">The obj.</param>
    /// <param name = "integrity">The integrity.</param>
    /// <param name = "withIntegrity">The withIntegrity.</param>
    /// <returns>The operation result.</returns>
    private static byte[] CreateBody(uint parentId, PlusObject obj, uint integrity, bool withIntegrity)
    {
        var writer = new PlusWriter();
        writer.UInt32(parentId);
        new S7SymbolicValue(S7SymbolicDataType.UDInt, 0U).WriteTo(writer);
        writer.UInt32(0);
        if (withIntegrity)
        {
            writer.VarUInt32(integrity);
        }

        obj.WriteTo(writer);
        writer.UInt32(0);
        return writer.ToArray();
    }

    /// <summary>Handles Write addresses.</summary>
    /// <param name = "writer">The writer.</param>
    /// <param name = "addresses">The addresses.</param>
    /// <param name = "offset">The offset.</param>
    /// <param name = "count">The count.</param>
    private static void WriteAddresses(PlusWriter writer, S7SymbolicAddress[] addresses, int offset, int count)
    {
        writer.VarUInt32((uint)count);
        uint fields = 0;
        for (var index = 0; index < count; index++)
        {
            fields += (uint)addresses[offset + index].LocalIds.Length + AddressFieldCount;
        }

        writer.VarUInt32(fields);
        for (var index = 0; index < count; index++)
        {
            addresses[offset + index].WriteTo(writer);
        }
    }

    /// <summary>Handles Validate index.</summary>
    /// <param name = "index">The index.</param>
    /// <param name = "count">The count.</param>
    /// <param name = "seen">The seen.</param>
    private static void ValidateIndex(uint index, int count, bool[] seen)
    {
        if (index > count || seen[index - 1])
        {
            throw new InvalidDataException("Response contains an invalid or duplicate item index.");
        }

        seen[index - 1] = true;
    }
}
