// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;
using System.Text;

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic.Protocol;
#else
namespace IoT.Driver.S7PlcRx.Symbolic.Protocol;
#endif

/// <summary>Provides plus object.</summary>
internal sealed class PlusObject
{
    /// <summary>Provides type metadata marker.</summary>
    private const byte TypeMetadataMarker = 0xab;

    /// <summary>Provides name metadata marker.</summary>
    private const byte NameMetadataMarker = 0xac;

    /// <summary>Provides utf8.</summary>
    private static readonly UTF8Encoding Utf8 = new(false, true);

    /// <summary>Initializes a new instance of the <see cref="PlusObject"/> class.</summary>
    /// <param name="id">The id.</param>
    /// <param name="classId">The class id.</param>
    /// <param name="attributeId">The attribute id.</param>
    internal PlusObject(uint id, uint classId, uint attributeId = 0)
    {
        Id = id;
        ClassId = classId;
        AttributeId = attributeId;
    }

    /// <summary>Gets id.</summary>
    internal uint Id { get; }

    /// <summary>Gets class id.</summary>
    internal uint ClassId { get; }

    /// <summary>Gets attribute id.</summary>
    internal uint AttributeId { get; }

    /// <summary>Gets or sets class flags.</summary>
    internal uint ClassFlags { get; set; }

    /// <summary>Gets attributes.</summary>
    internal Dictionary<uint, S7SymbolicValue> Attributes { get; } = new();

    /// <summary>Gets relations.</summary>
    internal Dictionary<uint, uint> Relations { get; } = new();

    /// <summary>Gets children.</summary>
    internal List<PlusObject> Children { get; } = new();

    /// <summary>Gets type information.</summary>
    internal byte[] TypeInformation { get; private set; } = Array.Empty<byte>();

    /// <summary>Gets names.</summary>
    internal string[] Names { get; private set; } = Array.Empty<string>();

    /// <summary>Reads from.</summary>
    /// <returns>The decoded or converted result.</returns>
    /// <param name="reader">The reader.</param>
    /// <param name="depth">The depth.</param>
    internal static PlusObject ReadFrom(PlusReader reader, int depth = 0)
    {
        S7SymbolicValue.CheckDepth(depth);
        if (reader.Byte() != 0xa1)
        {
            throw new InvalidDataException("Expected an object start marker.");
        }

        var id = reader.UInt32();
        var classId = reader.VarUInt32();
        var flags = reader.VarUInt32();
        var result = new PlusObject(id, classId, reader.VarUInt32())
        {
            ClassFlags = flags
        };
        var metadata = new HashSet<byte>();
        var entries = 0;
        while (true)
        {
            var marker = reader.Byte();
            if (marker == 0xa2)
            {
                return result;
            }

            entries++;
            S7SymbolicValue.CheckCount(entries);
            ReadElement(reader, result, marker, depth, metadata);
        }
    }

    /// <summary>Provides set metadata.</summary>
    /// <param name="typeInformation">The type information.</param>
    /// <param name="names">The names.</param>
    internal void SetMetadata(byte[] typeInformation, string[] names)
    {
        TypeInformation = typeInformation;
        Names = names;
    }

    /// <summary>Writes to.</summary>
    /// <param name="writer">The writer.</param>
    internal void WriteTo(PlusWriter writer) => WriteTo(writer, 0);

    /// <summary>Reads chunks.</summary>
    /// <returns>The decoded or converted result.</returns>
    /// <param name="reader">The reader.</param>
    private static byte[] ReadChunks(PlusReader reader)
    {
        var buffer = new List<byte>();
        ushort length;
        while ((length = reader.UInt16()) != 0)
        {
            if (buffer.Count > S7SymbolicValue.MaximumLength - length)
            {
                throw new InvalidDataException("Metadata limit exceeded.");
            }

            buffer.AddRange(reader.ReadBytes(length));
        }

        return buffer.ToArray();
    }

    /// <summary>Reads names.</summary>
    /// <returns>The decoded or converted result.</returns>
    /// <param name="reader">The reader.</param>
    private static string[] ReadNames(PlusReader reader)
    {
        var names = new List<string>();
        var totalLength = 0;
        ushort length;
        while ((length = reader.UInt16()) != 0)
        {
            totalLength += length;
            if (totalLength > S7SymbolicValue.MaximumLength)
            {
                throw new InvalidDataException("Name metadata limit exceeded.");
            }

            var chunk = new PlusReader(reader.ReadBytes(length));
            while (chunk.Remaining != 0)
            {
                S7SymbolicValue.CheckCount(names.Count + 1);
                names.Add(Utf8.GetString(chunk.ReadBytes(chunk.Byte())));
                if (chunk.Byte() != 0)
                {
                    throw new InvalidDataException("Invalid name terminator.");
                }
            }
        }

        return names.ToArray();
    }

    /// <summary>Writes chunks.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="bytes">The bytes.</param>
    private static void WriteChunks(PlusWriter writer, byte[] bytes)
    {
        if (bytes.Length > S7SymbolicValue.MaximumLength)
        {
            throw new InvalidDataException("Metadata limit exceeded.");
        }

        for (var offset = 0; offset < bytes.Length;)
        {
            var length = Math.Min(ushort.MaxValue, bytes.Length - offset);
            writer.UInt16((ushort)length);
            var chunk = new byte[length];
            Buffer.BlockCopy(bytes, offset, chunk, 0, length);
            writer.Bytes(chunk);
            offset += length;
        }

        writer.UInt16(0);
    }

    /// <summary>Reads element.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="result">The result.</param>
    /// <param name="marker">The marker.</param>
    /// <param name="depth">The depth.</param>
    /// <param name="metadata">The metadata.</param>
    private static void ReadElement(PlusReader reader, PlusObject result, byte marker, int depth, HashSet<byte> metadata)
    {
        if (marker == 0xa1)
        {
            reader.Position--;
            result.Children.Add(ReadFrom(reader, depth + 1));
            return;
        }

        if (marker == 0xa3)
        {
            ReadAttribute(reader, result, depth);
            return;
        }

        if (marker == 0xa4)
        {
            ReadRelation(reader, result);
            return;
        }

        if (marker == 0xab)
        {
            ReadTypeMetadata(reader, result, metadata);
            return;
        }

        if (marker == 0xac)
        {
            ReadNameMetadata(reader, result, metadata);
            return;
        }

        throw new InvalidDataException("Unknown object element marker.");
    }

    /// <summary>Reads attribute.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="result">The result.</param>
    /// <param name="depth">The depth.</param>
    private static void ReadAttribute(PlusReader reader, PlusObject result, int depth)
    {
        var attribute = reader.VarUInt32();
        if (result.Attributes.ContainsKey(attribute))
        {
            throw new InvalidDataException("Duplicate object attribute.");
        }

        result.Attributes.Add(attribute, S7SymbolicValue.ReadFrom(reader, depth + 1));
    }

    /// <summary>Reads relation.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="result">The result.</param>
    private static void ReadRelation(PlusReader reader, PlusObject result)
    {
        var relation = reader.VarUInt32();
        if (result.Relations.ContainsKey(relation))
        {
            throw new InvalidDataException("Duplicate object relation.");
        }

        result.Relations.Add(relation, reader.UInt32());
    }

    /// <summary>Reads type metadata.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="result">The result.</param>
    /// <param name="metadata">The metadata.</param>
    private static void ReadTypeMetadata(PlusReader reader, PlusObject result, HashSet<byte> metadata)
    {
        if (!metadata.Add(TypeMetadataMarker))
        {
            throw new InvalidDataException("Duplicate type metadata.");
        }

        result.TypeInformation = ReadChunks(reader);
        if (result.TypeInformation.Length is 0 or >= 0x4)
        {
            return;
        }

        throw new InvalidDataException("Type metadata lacks its first identifier.");
    }

    /// <summary>Reads name metadata.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="result">The result.</param>
    /// <param name="metadata">The metadata.</param>
    private static void ReadNameMetadata(PlusReader reader, PlusObject result, HashSet<byte> metadata)
    {
        if (!metadata.Add(NameMetadataMarker))
        {
            throw new InvalidDataException("Duplicate name metadata.");
        }

        result.Names = ReadNames(reader);
    }

    /// <summary>Writes to.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="depth">The depth.</param>
    private void WriteTo(PlusWriter writer, int depth)
    {
        S7SymbolicValue.CheckDepth(depth);
        S7SymbolicValue.CheckCount(Attributes.Count + Relations.Count + Children.Count);
        writer.Byte(0xa1);
        writer.UInt32(Id);
        writer.VarUInt32(ClassId);
        writer.VarUInt32(ClassFlags);
        writer.VarUInt32(AttributeId);
        foreach (var attribute in Attributes)
        {
            writer.Byte(0xa3);
            writer.VarUInt32(attribute.Key);
            attribute.Value.WriteTo(writer, depth + 1);
        }

        foreach (var relation in Relations)
        {
            writer.Byte(0xa4);
            writer.VarUInt32(relation.Key);
            writer.UInt32(relation.Value);
        }

        foreach (var child in Children)
        {
            child.WriteTo(writer, depth + 1);
        }

        if (TypeInformation.Length != 0)
        {
            if (TypeInformation.Length < 0x4)
            {
                throw new InvalidDataException("Type metadata lacks its first identifier.");
            }

            writer.Byte(0xab);
            WriteChunks(writer, TypeInformation);
        }

        if (Names.Length != 0)
        {
            WriteNames(writer);
        }

        writer.Byte(0xa2);
    }

    /// <summary>Writes names.</summary>
    /// <param name="writer">The writer.</param>
    private void WriteNames(PlusWriter writer)
    {
        S7SymbolicValue.CheckCount(Names.Length);
        writer.Byte(0xac);
        var totalLength = 0;
        foreach (var name in Names)
        {
            var bytes = Utf8.GetBytes(name);
            totalLength += bytes.Length + 0x2;
            if (bytes.Length > byte.MaxValue || totalLength > S7SymbolicValue.MaximumLength)
            {
                throw new InvalidDataException("Name metadata limit exceeded.");
            }

            writer.UInt16((ushort)(bytes.Length + 0x2));
            writer.Byte((byte)bytes.Length);
            writer.Bytes(bytes);
            writer.Byte(0);
        }

        writer.UInt16(0);
    }
}
