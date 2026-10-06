// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic.Protocol;
#else
namespace IoT.Driver.S7PlcRx.Symbolic.Protocol;
#endif

/// <summary>Decodes native symbolic type descriptions.</summary>
internal static class PlusTypeDecoder
{
    /// <summary>The byte count of classic, retain, and volatile section sizes.</summary>
    private const int FunctionBlockSectionSizeBytes = 3 * sizeof(uint);

    /// <summary>The bit position of the descriptor kind in the member flags.</summary>
    private const int DescriptorKindShift = 12;

    /// <summary>The maximum dimensions recorded by PLC array metadata.</summary>
    private const int MaximumArrayRank = 6;

    /// <summary>The bit width of a byte used in little-endian decoding.</summary>
    private const int BitsPerByte = 8;

    /// <summary>The bit width of a word used in little-endian decoding.</summary>
    private const int BitsPerWord = 16;

    /// <summary>The LegacyArrayKind protocol descriptor value.</summary>
    private const int LegacyArrayKind = 3;

    /// <summary>The LegacyMultiDimensionalArrayKind protocol descriptor value.</summary>
    private const int LegacyMultiDimensionalArrayKind = 4;

    /// <summary>The LegacyStructKind protocol descriptor value.</summary>
    private const int LegacyStructKind = 5;

    /// <summary>The LegacyStructArrayKind protocol descriptor value.</summary>
    private const int LegacyStructArrayKind = 6;

    /// <summary>The LegacyMultiDimensionalStructArrayKind protocol descriptor value.</summary>
    private const int LegacyMultiDimensionalStructArrayKind = 7;

    /// <summary>The StandardMemberKind protocol descriptor value.</summary>
    private const int StandardMemberKind = 8;

    /// <summary>The ArrayKind protocol descriptor value.</summary>
    private const int ArrayKind = 10;

    /// <summary>The MultiDimensionalArrayKind protocol descriptor value.</summary>
    private const int MultiDimensionalArrayKind = 11;

    /// <summary>The StructKind protocol descriptor value.</summary>
    private const int StructKind = 12;

    /// <summary>The StructArrayKind protocol descriptor value.</summary>
    private const int StructArrayKind = 13;

    /// <summary>The MultiDimensionalStructArrayKind protocol descriptor value.</summary>
    private const int MultiDimensionalStructArrayKind = 14;

    /// <summary>The FunctionBlockKind protocol descriptor value.</summary>
    private const int FunctionBlockKind = 15;

    /// <summary>The OpaqueStructMetadataBytes protocol descriptor value.</summary>
    private const int OpaqueStructMetadataBytes = 16;

    /// <summary>The size of a pair of metadata words.</summary>
    private const int MetadataWordPairBytes = sizeof(ulong);

    /// <summary>The descriptor kinds that carry array bounds.</summary>
    private static readonly HashSet<int> ArrayKinds =
    [
        LegacyArrayKind, LegacyMultiDimensionalArrayKind, LegacyStructArrayKind, LegacyMultiDimensionalStructArrayKind,
        ArrayKind, MultiDimensionalArrayKind, StructArrayKind, MultiDimensionalStructArrayKind
    ];

    /// <summary>The descriptor kinds that carry multiple array dimensions.</summary>
    private static readonly HashSet<int> MultiDimensionalKinds =
    [LegacyMultiDimensionalArrayKind, LegacyMultiDimensionalStructArrayKind, MultiDimensionalArrayKind, MultiDimensionalStructArrayKind];

    /// <summary>The descriptor kinds that carry struct array metadata.</summary>
    private static readonly HashSet<int> StructArrayKinds =
    [LegacyStructArrayKind, LegacyMultiDimensionalStructArrayKind, StructArrayKind, MultiDimensionalStructArrayKind];

    /// <summary>The descriptor kinds that carry a related struct type.</summary>
    private static readonly HashSet<int> RelatedTypeKinds =
    [0, LegacyStructKind, LegacyStructArrayKind, LegacyMultiDimensionalStructArrayKind, StructKind, StructArrayKind, MultiDimensionalStructArrayKind, FunctionBlockKind];

    /// <summary>Decodes the type members for symbol names.</summary>
    /// <param name="data">The encoded type description.</param>
    /// <param name="names">The corresponding symbol names.</param>
    /// <returns>The decoded type members.</returns>
    internal static PlusTypeMember[] Decode(byte[] data, string[] names)
    {
        var reader = new PlusReader(data);
        if (reader.Remaining == 0)
        {
            if (names.Length != 0)
            {
                throw new InvalidDataException("Symbol names were returned without type members.");
            }

            return [];
        }

        reader.Skip(sizeof(uint)); // First type-list identifier, present once before joined member blocks.
        var members = new List<PlusTypeMember>();
        while (reader.Remaining > 0)
        {
            var member = ReadMember(reader);
            member.Name = members.Count < names.Length ? names[members.Count] : string.Empty;
            members.Add(member);
        }

        if (members.Count != names.Length)
        {
            throw new InvalidDataException("Symbol names and type members have different counts.");
        }

        return members.ToArray();
    }

    /// <summary>Reads one native type member.</summary>
    /// <param name="reader">The protocol reader.</param>
    /// <returns>The decoded member.</returns>
    private static PlusTypeMember ReadMember(PlusReader reader)
    {
        var member = new PlusTypeMember { LocalId = Little32(reader), Crc = Little32(reader), SoftType = reader.Byte(), Flags = reader.UInt16() };
        reader.Skip(1);
        var kind = member.Flags >> DescriptorKindShift;
        if (kind is 1 or StandardMemberKind)
        {
            reader.Skip(sizeof(uint));
        }
        else
        {
            ReadMemberDetails(reader, member, kind);
        }

        return member;
    }

    /// <summary>Reads an extended member description.</summary>
    /// <param name="reader">The protocol reader.</param>
    /// <param name="member">The decoded member.</param>
    /// <param name="kind">The encoded type kind.</param>
    private static void ReadMemberDetails(PlusReader reader, PlusTypeMember member, int kind)
    {
        member.MaximumStringLength = Little16(reader);
        reader.Skip(sizeof(ushort) + MetadataWordPairBytes);
        if (ArrayKinds.Contains(kind))
        {
            member.SetDimensions([unchecked((int)Little32(reader))], [Little32(reader)]);
            if (MultiDimensionalKinds.Contains(kind))
            {
                ReadDimensions(reader, member);
            }
        }

        if (StructArrayKinds.Contains(kind))
        {
            reader.Skip(MetadataWordPairBytes);
        }

        if (RelatedTypeKinds.Contains(kind))
        {
            member.Relation = Little32(reader);
            reader.Skip(OpaqueStructMetadataBytes);
        }

        if (kind is 0 or FunctionBlockKind)
        {
            reader.Skip(MetadataWordPairBytes);
        }

        if (kind != 0)
        {
            return;
        }

        var elementCount = Little32(reader);
        reader.Skip(FunctionBlockSectionSizeBytes);
        ReadDimensions(reader, member, elementCount);
    }

    /// <summary>Reads a member's multidimensional array bounds.</summary>
    /// <param name="reader">The protocol reader.</param>
    /// <param name="member">The member receiving dimensions.</param>
    private static void ReadDimensions(PlusReader reader, PlusTypeMember member) => ReadDimensions(reader, member, 0);

    /// <summary>Reads array dimensions, retaining an FB-array count when dimension counts are absent.</summary>
    /// <param name="reader">The protocol reader.</param>
    /// <param name="member">The member receiving dimensions.</param>
    /// <param name="elementCount">The explicit FB-array element count, or zero for another descriptor.</param>
    private static void ReadDimensions(PlusReader reader, PlusTypeMember member, uint elementCount)
    {
        var lower = new int[MaximumArrayRank];
        var lengths = new uint[MaximumArrayRank];
        for (var i = 0; i < MaximumArrayRank; i++)
        {
            lower[i] = unchecked((int)Little32(reader));
        }

        for (var i = 0; i < MaximumArrayRank; i++)
        {
            lengths[i] = Little32(reader);
        }

        var count = 0;
        foreach (var length in lengths)
        {
            if (length != 0)
            {
                count++;
            }
        }

        if (count == 0 && elementCount != 0)
        {
            member.SetDimensions([lower[0]], [elementCount]);
            return;
        }

        member.SetDimensions(new int[count], new uint[count]);
        for (var i = 0; i < count; i++)
        {
            member.LowerBounds[i] = lower[count - i - 1];
            member.Lengths[i] = lengths[count - i - 1];
        }
    }

    /// <summary>Reads a little-endian 16-bit value.</summary>
    /// <param name="reader">The protocol reader.</param>
    /// <returns>The decoded value.</returns>
    private static ushort Little16(PlusReader reader) => (ushort)(reader.Byte() | (reader.Byte() << BitsPerByte));

    /// <summary>Reads a little-endian 32-bit value.</summary>
    /// <param name="reader">The protocol reader.</param>
    /// <returns>The decoded value.</returns>
    private static uint Little32(PlusReader reader) => Little16(reader) | ((uint)Little16(reader) << BitsPerWord);
}
