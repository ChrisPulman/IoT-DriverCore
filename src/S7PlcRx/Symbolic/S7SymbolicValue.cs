// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using System.IO;
using System.Text;

#if REACTIVE_SHIM
using IoT.Driver.S7PlcRx.Reactive.Symbolic.Protocol;
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
using IoT.Driver.S7PlcRx.Symbolic.Protocol;
namespace IoT.Driver.S7PlcRx.Symbolic;
#endif

/// <summary>Contains an S7Plus datatype, its payload, and wire flags.</summary>
public sealed class S7SymbolicValue
{
    /// <summary>Provides maximum count.</summary>
    internal const int MaximumCount = 65_536;

    /// <summary>Provides maximum length.</summary>
    internal const int MaximumLength = 16 * 1024 * 1024;

    /// <summary>Provides maximum depth.</summary>
    internal const int MaximumDepth = 64;

    /// <summary>Provides unsupported datatype.</summary>
    private const string UnsupportedDatatype = "Unsupported S7Plus datatype.";

    /// <summary>Provides utf8.</summary>
    private static readonly UTF8Encoding Utf8 = new(false, true);

    /// <summary>Initializes a new instance of the <see cref="S7SymbolicValue"/> class.</summary>
    /// <param name="dataType">The data type.</param>
    /// <param name="value">The value.</param>
    public S7SymbolicValue(S7SymbolicDataType dataType, object? value)
        : this(dataType, value, 0)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="S7SymbolicValue"/> class.</summary>
    /// <param name="dataType">The data type.</param>
    /// <param name="value">The value.</param>
    /// <param name="flags">The flags.</param>
    public S7SymbolicValue(S7SymbolicDataType dataType, object? value, byte flags)
    {
        ValidateHeader(dataType, flags);
        DataType = dataType;
        Value = value;
        Flags = flags;
    }

    /// <summary>Gets the wire datatype.</summary>
    public S7SymbolicDataType DataType { get; }

    /// <summary>Gets the scalar, array, sparse dictionary, or structured payload.</summary>
    public object? Value { get; }

    /// <summary>Gets the datatype flags.</summary>
    public byte Flags { get; }

    /// <summary>Reads from.</summary>
    /// <returns>The decoded or converted result.</returns>
    /// <param name="reader">The reader.</param>
    /// <param name="depth">The depth.</param>
    internal static S7SymbolicValue ReadFrom(PlusReader reader, int depth = 0)
    {
        CheckDepth(depth);
        var flags = reader.Byte();
        var type = (S7SymbolicDataType)reader.Byte();
        ValidateHeader(type, flags);
        var value = flags switch
        {
            0x40 => ReadSparse(reader, type, depth + 1),
            0x10 or 0x20 => ReadArray(reader, type, depth + 1),
            _ => ReadScalar(reader, type, depth + 1)
        };
        return new(type, value, flags);
    }

    /// <summary>Reads length.</summary>
    /// <returns>The decoded or converted result.</returns>
    /// <param name="reader">The reader.</param>
    internal static int ReadLength(PlusReader reader)
    {
        var count = reader.VarUInt32();
        if (count > MaximumLength || count > reader.Remaining)
        {
            throw new InvalidDataException("Invalid S7Plus payload length.");
        }

        return (int)count;
    }

    /// <summary>Validates depth.</summary>
    /// <param name="depth">The depth.</param>
    internal static void CheckDepth(int depth)
    {
        if (depth <= MaximumDepth)
        {
            return;
        }

        throw new InvalidDataException("S7Plus nesting limit exceeded.");
    }

    /// <summary>Validates count.</summary>
    /// <param name="count">The count.</param>
    internal static void CheckCount(int count)
    {
        if (count is >= 0 and <= MaximumCount)
        {
            return;
        }

        throw new InvalidDataException("S7Plus entry limit exceeded.");
    }

    /// <summary>Writes to.</summary>
    /// <param name="writer">The writer.</param>
    internal void WriteTo(PlusWriter writer) => WriteTo(writer, 0);

    /// <summary>Writes to.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="depth">The depth.</param>
    internal void WriteTo(PlusWriter writer, int depth)
    {
        CheckDepth(depth);
        writer.Byte(Flags);
        writer.Byte((byte)DataType);
        if (Flags == 0x40)
        {
            var entries = Require<Dictionary<uint, object?>>(Value);
            CheckCount(entries.Count);
            foreach (var entry in entries)
            {
                if (entry.Key == 0)
                {
                    throw new InvalidDataException("Sparse array index zero is reserved.");
                }

                writer.VarUInt32(entry.Key);
                WriteScalar(writer, DataType, entry.Value, depth + 1);
            }

            writer.VarUInt32(0);
        }
        else if ((Flags & 0x30) != 0)
        {
            var entries = Require<IList>(Value);
            CheckCount(entries.Count);
            writer.VarUInt32((uint)entries.Count);
            foreach (var entry in entries)
            {
                WriteScalar(writer, DataType, entry, depth + 1);
            }
        }
        else
        {
            WriteScalar(writer, DataType, Value, depth + 1);
        }
    }

    /// <summary>Reads count.</summary>
    /// <returns>The decoded or converted result.</returns>
    /// <param name="reader">The reader.</param>
    private static int ReadCount(PlusReader reader)
    {
        var count = reader.VarUInt32();
        if (count > MaximumCount)
        {
            throw new InvalidDataException("S7Plus array limit exceeded.");
        }

        return (int)count;
    }

    /// <summary>Provides validate header.</summary>
    /// <param name="type">The type.</param>
    /// <param name="flags">The flags.</param>
    private static void ValidateHeader(S7SymbolicDataType type, byte flags)
    {
        ValidateType(type);
        ValidateFlags(flags);
        if (type is S7SymbolicDataType.Variant or S7SymbolicDataType.S7String)
        {
            throw new NotSupportedException("Unsupported Variant or S7String wire layout.");
        }

        if (flags != 0x40 || type is S7SymbolicDataType.DInt or S7SymbolicDataType.UDInt or S7SymbolicDataType.Blob or S7SymbolicDataType.WString)
        {
            return;
        }

        throw new InvalidDataException("Unsupported sparse array datatype.");
    }

    /// <summary>Reads scalar.</summary>
    /// <returns>The decoded or converted result.</returns>
    /// <param name="reader">The reader.</param>
    /// <param name="type">The type.</param>
    /// <param name="depth">The depth.</param>
    private static object? ReadScalar(PlusReader reader, S7SymbolicDataType type, int depth)
    {
        CheckDepth(depth);
        return type switch
        {
            S7SymbolicDataType.Null => null,
            S7SymbolicDataType.Bool => ReadBoolean(reader),
            S7SymbolicDataType.USInt or S7SymbolicDataType.Byte => reader.Byte(),
            S7SymbolicDataType.UInt or S7SymbolicDataType.Word => reader.UInt16(),
            S7SymbolicDataType.UDInt or S7SymbolicDataType.AID => reader.VarUInt32(),
            S7SymbolicDataType.ULInt => reader.VarUInt64(),
            S7SymbolicDataType.SInt => unchecked((sbyte)reader.Byte()),
            S7SymbolicDataType.Int => unchecked((short)reader.UInt16()),
            S7SymbolicDataType.DInt => reader.VarInt32(),
            S7SymbolicDataType.LInt or S7SymbolicDataType.Timespan => reader.VarInt64(),
            S7SymbolicDataType.DWord or S7SymbolicDataType.RID => reader.UInt32(),
            S7SymbolicDataType.LWord or S7SymbolicDataType.Timestamp => reader.UInt64(),
            S7SymbolicDataType.Real => BitConverter.ToSingle(BitConverter.GetBytes(reader.UInt32()), 0),
            S7SymbolicDataType.LReal => BitConverter.Int64BitsToDouble(unchecked((long)reader.UInt64())),
            S7SymbolicDataType.Blob => ReadBlob(reader),
            S7SymbolicDataType.WString => Utf8.GetString(reader.ReadBytes(ReadLength(reader))),
            S7SymbolicDataType.Struct => ReadStructure(reader, depth),
            S7SymbolicDataType.Variant or S7SymbolicDataType.S7String => throw new NotSupportedException("Unsupported Variant or S7String wire layout."),
            _ => throw new InvalidDataException(UnsupportedDatatype)
        };
    }

    /// <summary>Provides is packed.</summary>
    /// <returns>The decoded or converted result.</returns>
    /// <param name="typeId">The type id.</param>
    private static bool IsPacked(uint typeId) => (typeId & 0xf0000000) == 0x90000000 || (typeId & 0xff000000) == 0x02000000;

    /// <summary>Validates the CLR representation of a value.</summary>
    /// <typeparam name="T">The expected CLR type.</typeparam>
    /// <returns>The decoded or converted result.</returns>
    /// <param name="value">The value.</param>
    private static T Require<T>(object? value)
    {
        if (value is T typed)
        {
            return typed;
        }

        throw new ArgumentException("Value does not match the S7Plus datatype.", nameof(value));
    }

    /// <summary>Writes payload.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="bytes">The bytes.</param>
    private static void WritePayload(PlusWriter writer, byte[] bytes)
    {
        if (bytes.Length > MaximumLength)
        {
            throw new InvalidDataException("S7Plus payload limit exceeded.");
        }

        writer.VarUInt32((uint)bytes.Length);
        writer.Bytes(bytes);
    }

    /// <summary>Writes scalar.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="type">The type.</param>
    /// <param name="value">The value.</param>
    /// <param name="depth">The depth.</param>
    private static void WriteScalar(PlusWriter writer, S7SymbolicDataType type, object? value, int depth)
    {
        CheckDepth(depth);
        if (IsSmallInteger(type))
        {
            WriteSmallInteger(writer, type, value);
            return;
        }

        if (IsLargeInteger(type))
        {
            WriteLargeInteger(writer, type, value);
            return;
        }

        if (type == S7SymbolicDataType.Null)
        {
            if (value is not null)
            {
                throw new ArgumentException("Null datatype requires a null value.", nameof(value));
            }

            return;
        }

        if (type == S7SymbolicDataType.Real)
        {
            writer.UInt32(BitConverter.ToUInt32(BitConverter.GetBytes(Require<float>(value)), 0));
            return;
        }

        if (type == S7SymbolicDataType.LReal)
        {
            writer.UInt64(unchecked((ulong)BitConverter.DoubleToInt64Bits(Require<double>(value))));
            return;
        }

        if (type == S7SymbolicDataType.Blob)
        {
            var blob = Require<S7SymbolicBlob>(value);
            writer.VarUInt32(blob.RootId);
            WritePayload(writer, blob.Data);
            return;
        }

        if (type == S7SymbolicDataType.WString)
        {
            WritePayload(writer, Utf8.GetBytes(Require<string>(value)));
            return;
        }

        if (type == S7SymbolicDataType.Struct)
        {
            WriteStructure(writer, value, depth);
            return;
        }

        throw new NotSupportedException(UnsupportedDatatype);
    }

    /// <summary>Reads array.</summary>
    /// <returns>The decoded or converted result.</returns>
    /// <param name="reader">The reader.</param>
    /// <param name="type">The type.</param>
    /// <param name="depth">The depth.</param>
    private static object?[] ReadArray(PlusReader reader, S7SymbolicDataType type, int depth)
    {
        var entries = new object?[ReadCount(reader)];
        for (var i = 0; i < entries.Length; i++)
        {
            entries[i] = ReadScalar(reader, type, depth);
        }

        return entries;
    }

    /// <summary>Reads sparse.</summary>
    /// <returns>The decoded or converted result.</returns>
    /// <param name="reader">The reader.</param>
    /// <param name="type">The type.</param>
    /// <param name="depth">The depth.</param>
    private static Dictionary<uint, object?> ReadSparse(PlusReader reader, S7SymbolicDataType type, int depth)
    {
        var entries = new Dictionary<uint, object?>();
        uint id;
        while ((id = reader.VarUInt32()) != 0)
        {
            CheckCount(entries.Count + 1);
            if (entries.ContainsKey(id))
            {
                throw new InvalidDataException("Duplicate sparse index.");
            }

            entries.Add(id, ReadScalar(reader, type, depth));
        }

        return entries;
    }

    /// <summary>Reads boolean.</summary>
    /// <returns>The decoded or converted result.</returns>
    /// <param name="reader">The reader.</param>
    private static bool ReadBoolean(PlusReader reader)
    {
        var value = reader.Byte();
        if (value > 1)
        {
            throw new InvalidDataException("Invalid Boolean value.");
        }

        return value != 0;
    }

    /// <summary>Reads blob.</summary>
    /// <returns>The decoded or converted result.</returns>
    /// <param name="reader">The reader.</param>
    private static S7SymbolicBlob ReadBlob(PlusReader reader)
    {
        var root = reader.VarUInt32();
        return new(root, reader.ReadBytes(ReadLength(reader)));
    }

    /// <summary>Reads structure.</summary>
    /// <returns>The decoded or converted result.</returns>
    /// <param name="reader">The reader.</param>
    /// <param name="depth">The depth.</param>
    private static object ReadStructure(PlusReader reader, int depth)
    {
        var typeId = reader.UInt32();
        if (IsPacked(typeId))
        {
            return ReadPackedStructure(reader, typeId);
        }

        var fields = new Dictionary<uint, S7SymbolicValue>();
        uint id;
        while ((id = reader.VarUInt32()) != 0)
        {
            CheckCount(fields.Count + 1);
            if (fields.ContainsKey(id))
            {
                throw new InvalidDataException("Duplicate structure field.");
            }

            fields.Add(id, ReadFrom(reader, depth));
        }

        return new S7SymbolicStruct(typeId, fields);
    }

    /// <summary>Reads packed structure.</summary>
    /// <returns>The decoded or converted result.</returns>
    /// <param name="reader">The reader.</param>
    /// <param name="typeId">The type id.</param>
    private static S7SymbolicPackedStruct ReadPackedStructure(PlusReader reader, uint typeId)
    {
        var timestamp = reader.UInt64();
        var flags = reader.VarUInt32();
        var firstLength = reader.VarUInt32();
        var length = (flags & 0x0400) != 0 ? reader.VarUInt32() : firstLength;
        if (firstLength > MaximumLength || length > MaximumLength || length > reader.Remaining)
        {
            throw new InvalidDataException("Invalid packed structure length.");
        }

        return new(typeId, timestamp, flags, reader.ReadBytes((int)length), firstLength);
    }

    /// <summary>Writes small integer.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="type">The type.</param>
    /// <param name="value">The value.</param>
    private static void WriteSmallInteger(PlusWriter writer, S7SymbolicDataType type, object? value)
    {
        if (type == S7SymbolicDataType.Bool)
        {
            writer.Byte(Require<bool>(value) ? (byte)1 : (byte)0);
            return;
        }

        if (type is S7SymbolicDataType.USInt or S7SymbolicDataType.Byte)
        {
            writer.Byte(Require<byte>(value));
            return;
        }

        if (type is S7SymbolicDataType.UInt or S7SymbolicDataType.Word)
        {
            writer.UInt16(Require<ushort>(value));
            return;
        }

        if (type == S7SymbolicDataType.SInt)
        {
            writer.Byte(unchecked((byte)Require<sbyte>(value)));
            return;
        }

        if (type == S7SymbolicDataType.Int)
        {
            writer.UInt16(unchecked((ushort)Require<short>(value)));
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(type));
    }

    /// <summary>Writes large integer.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="type">The type.</param>
    /// <param name="value">The value.</param>
    private static void WriteLargeInteger(PlusWriter writer, S7SymbolicDataType type, object? value)
    {
        if (type is S7SymbolicDataType.DWord or S7SymbolicDataType.RID or S7SymbolicDataType.LWord or S7SymbolicDataType.Timestamp)
        {
            WriteFixedInteger(writer, type, value);
            return;
        }

        WriteVariableInteger(writer, type, value);
    }

    /// <summary>Writes structure.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value.</param>
    /// <param name="depth">The depth.</param>
    private static void WriteStructure(PlusWriter writer, object? value, int depth)
    {
        if (value is S7SymbolicPackedStruct packed)
        {
            WritePackedStructure(writer, packed);
            return;
        }

        var structure = Require<S7SymbolicStruct>(value);
        if (IsPacked(structure.TypeId))
        {
            throw new InvalidDataException("Packed type identifier requires a packed structure.");
        }

        CheckCount(structure.Fields.Count);
        writer.UInt32(structure.TypeId);
        foreach (var field in structure.Fields)
        {
            if (field.Key == 0)
            {
                throw new InvalidDataException("Structure field zero is reserved.");
            }

            writer.VarUInt32(field.Key);
            field.Value.WriteTo(writer, depth);
        }

        writer.VarUInt32(0);
    }

    /// <summary>Writes packed structure.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="packed">The packed.</param>
    private static void WritePackedStructure(PlusWriter writer, S7SymbolicPackedStruct packed)
    {
        if (!IsPacked(packed.TypeId) || packed.Data.Length > MaximumLength || packed.FirstLength > MaximumLength)
        {
            throw new InvalidDataException("Invalid packed structure.");
        }

        writer.UInt32(packed.TypeId);
        writer.UInt64(packed.InterfaceTimestamp);
        writer.VarUInt32(packed.TransportFlags);
        if ((packed.TransportFlags & 0x0400) != 0)
        {
            writer.VarUInt32(packed.FirstLength);
        }

        WritePayload(writer, packed.Data);
    }

    /// <summary>Provides validate type.</summary>
    /// <param name="type">The type.</param>
    private static void ValidateType(S7SymbolicDataType type)
    {
        if ((int)type is >= 0 and <= 0x19 and not 0x18)
        {
            return;
        }

        throw new InvalidDataException(UnsupportedDatatype);
    }

    /// <summary>Provides validate flags.</summary>
    /// <param name="flags">The flags.</param>
    private static void ValidateFlags(byte flags)
    {
        if (flags is 0 or 0x10 or 0x20 or 0x40)
        {
            return;
        }

        throw new InvalidDataException("Unsupported S7Plus flags.");
    }

    /// <summary>Provides is small integer.</summary>
    /// <returns>The decoded or converted result.</returns>
    /// <param name="type">The type.</param>
    private static bool IsSmallInteger(S7SymbolicDataType type) => type is S7SymbolicDataType.Bool or
            S7SymbolicDataType.USInt or
            S7SymbolicDataType.Byte or
            S7SymbolicDataType.UInt or
            S7SymbolicDataType.Word or
            S7SymbolicDataType.SInt or
            S7SymbolicDataType.Int;

    /// <summary>Provides is large integer.</summary>
    /// <returns>The decoded or converted result.</returns>
    /// <param name="type">The type.</param>
    private static bool IsLargeInteger(S7SymbolicDataType type) => type is S7SymbolicDataType.UDInt or
            S7SymbolicDataType.AID or
            S7SymbolicDataType.ULInt or
            S7SymbolicDataType.DInt or
            S7SymbolicDataType.LInt or
            S7SymbolicDataType.Timespan or
            S7SymbolicDataType.DWord or
            S7SymbolicDataType.RID or
            S7SymbolicDataType.LWord or
            S7SymbolicDataType.Timestamp;

    /// <summary>Writes fixed integer.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="type">The type.</param>
    /// <param name="value">The value.</param>
    private static void WriteFixedInteger(PlusWriter writer, S7SymbolicDataType type, object? value)
    {
        if (type is S7SymbolicDataType.DWord or S7SymbolicDataType.RID)
        {
            writer.UInt32(Require<uint>(value));
            return;
        }

        if (type is S7SymbolicDataType.LWord or S7SymbolicDataType.Timestamp)
        {
            writer.UInt64(Require<ulong>(value));
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(type));
    }

    /// <summary>Writes variable integer.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="type">The type.</param>
    /// <param name="value">The value.</param>
    private static void WriteVariableInteger(PlusWriter writer, S7SymbolicDataType type, object? value)
    {
        if (type is S7SymbolicDataType.UDInt or S7SymbolicDataType.AID)
        {
            writer.VarUInt32(Require<uint>(value));
            return;
        }

        if (type == S7SymbolicDataType.ULInt)
        {
            writer.VarUInt64(Require<ulong>(value));
            return;
        }

        if (type == S7SymbolicDataType.DInt)
        {
            writer.VarInt32(Require<int>(value));
            return;
        }

        if (type is S7SymbolicDataType.LInt or S7SymbolicDataType.Timespan)
        {
            writer.VarInt64(Require<long>(value));
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(type));
    }
}
