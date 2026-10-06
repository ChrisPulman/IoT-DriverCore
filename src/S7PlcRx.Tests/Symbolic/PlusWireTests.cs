// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;
using IoT.Driver.S7PlcRx.Symbolic;
using IoT.Driver.S7PlcRx.Symbolic.Protocol;
using TUnit.Core;
using ByteArrays = Org.BouncyCastle.Utilities.Arrays;
using WireAssert = TUnit.Assertions.Assert;

namespace IoT.Driver.S7PlcRx.Tests.Symbolic;

/// <summary>Verifies checked S7Plus wire encodings and malformed message rejection.</summary>
public sealed class PlusWireTests
{
    /// <summary>Provides object depth boundary.</summary>
    private const int ObjectDepthBoundary = 0x3f;

    /// <summary>Provides excessive depth.</summary>
    private const int ExcessiveDepth = 0x41;

    /// <summary>Provides signed array value.</summary>
    private const int SignedArrayValue = -0xff;

    /// <summary>Provides next unsigned group.</summary>
    private const int NextUnsignedGroup = 0x80;

    /// <summary>Provides unsigned byte value.</summary>
    private const byte UnsignedByteValue = 0xfd;

    /// <summary>Provides unsigned word value.</summary>
    private const ushort UnsignedWordValue = 65_000;

    /// <summary>Provides signed byte value.</summary>
    private const sbyte SignedByteValue = -127;

    /// <summary>Provides signed word value.</summary>
    private const short SignedWordValue = -32_000;

    /// <summary>Provides binary byte value.</summary>
    private const byte BinaryByteValue = 0xfa;

    /// <summary>Provides binary word value.</summary>
    private const ushort BinaryWordValue = 60_000;

    /// <summary>Provides single value.</summary>
    private const float SingleValue = 1.25F;

    /// <summary>Provides double value.</summary>
    private const double DoubleValue = -1.75D;

    /// <summary>Provides duration value.</summary>
    private const long DurationValue = -123_456_789L;

    /// <summary>Provides attribute value.</summary>
    private const uint AttributeValue = 70_000U;

    /// <summary>Provides unsigned32 vector.</summary>
    private static readonly byte[] Unsigned32Vector = [0, 0x7f, 0x81, 0, 0x8f, 0xff, 0xff, 0xff, 0x7f];

    /// <summary>Provides maximum unsigned64 vector.</summary>
    private static readonly byte[] MaximumUnsigned64Vector = [0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff];

    /// <summary>Provides first nine byte unsigned vector.</summary>
    private static readonly byte[] FirstNineByteUnsignedVector = [0x80, 0xc0, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0];

    /// <summary>Provides truncated vlq vector.</summary>
    private static readonly byte[] TruncatedVlqVector = [0x81];

    /// <summary>Provides unsigned overflow vector.</summary>
    private static readonly byte[] UnsignedOverflowVector = [0x90, 0x80, 0x80, 0x80, 0];

    /// <summary>Provides signed overflow vector.</summary>
    private static readonly byte[] SignedOverflowVector = [0x88, 0x80, 0x80, 0x80, 0];

    /// <summary>Provides unterminated vlq vector.</summary>
    private static readonly byte[] UnterminatedVlqVector = [0x80, 0x80, 0x80, 0x80, 0x80];

    /// <summary>Provides single byte vector.</summary>
    private static readonly byte[] SingleByteVector = [1];

    /// <summary>Provides unicode vector.</summary>
    private static readonly byte[] UnicodeVector = [0, 0x15, 0x5, 0xc3, 0xa9, 0xe6, 0xb0, 0xb4];

    /// <summary>Provides blob vector.</summary>
    private static readonly byte[] BlobVector = [1, 0x2, 0x3];

    /// <summary>Provides packed vector.</summary>
    private static readonly byte[] PackedVector = [0x4, 0x5];

    /// <summary>Provides unsupported flags vector.</summary>
    private static readonly byte[] UnsupportedFlagsVector = [1, 1];

    /// <summary>Provides reserved type vector.</summary>
    private static readonly byte[] ReservedTypeVector = [0, 0x18];

    /// <summary>Provides unsupported sparse type vector.</summary>
    private static readonly byte[] UnsupportedSparseTypeVector = [0x40, 1];

    /// <summary>Provides invalid boolean vector.</summary>
    private static readonly byte[] InvalidBooleanVector = [0, 1, 0x2];

    /// <summary>Provides truncated string vector.</summary>
    private static readonly byte[] TruncatedStringVector = [0, 0x15, 0x5, 1];

    /// <summary>Provides excessive array vector.</summary>
    private static readonly byte[] ExcessiveArrayVector = [0x10, 0, 0x84, 0x80, 1];

    /// <summary>Provides null vector.</summary>
    private static readonly byte[] NullVector = [0, 0];

    /// <summary>Provides variant vector.</summary>
    private static readonly byte[] VariantVector = [0, 0x16];

    /// <summary>Provides s7 string vector.</summary>
    private static readonly byte[] S7StringVector = [0, 0x19];

    /// <summary>Stores an unsupported empty Variant array wire vector.</summary>
    private static readonly byte[] EmptyVariantArrayVector = [0x10, 0x16, 0];

    /// <summary>Stores an unsupported empty S7String address array wire vector.</summary>
    private static readonly byte[] EmptyS7StringArrayVector = [0x20, 0x19, 0];

    /// <summary>Stores an empty homogeneous array payload.</summary>
    private static readonly object?[] EmptyArrayValues = [];

    /// <summary>Provides type metadata vector.</summary>
    private static readonly byte[] TypeMetadataVector = [1, 0, 0, 0, 0x9];

    /// <summary>Provides end object vector.</summary>
    private static readonly byte[] EndObjectVector = [0xa2];

    /// <summary>Provides unknown object marker vector.</summary>
    private static readonly byte[] UnknownObjectMarkerVector = [0xa1, 0, 0, 0, 1, 0x2, 0, 0, 0xaa];

    /// <summary>Provides bad name terminator vector.</summary>
    private static readonly byte[] BadNameTerminatorVector = [0xa1, 0, 0, 0, 1, 0x2, 0, 0, 0xac, 0, 0x3, 1, 0x41, 1, 0, 0, 0xa2];

    /// <summary>Provides address vector.</summary>
    private static readonly byte[] AddressVector = [0x4, 0x2, 0x3, 0x3, 1, 0x81, 0];

    /// <summary>Provides response vector.</summary>
    private static readonly byte[] ResponseVector = [0x32, 0, 0, 0x04, 0xca, 0, 0, 0x12, 0x34, 0x36, 0x63];

    /// <summary>Provides bad response vector.</summary>
    private static readonly byte[] BadResponseVector = [0x31];

    /// <summary>Stores the expected bytes after resetting and reusing a writer.</summary>
    private static readonly byte[] ReusedWriterVector = [0x12, 0x34];

    /// <summary>Provides array values.</summary>
    private static readonly int[] ArrayValues = [SignedArrayValue, 0, NextUnsignedGroup];

    /// <summary>Provides expected array values.</summary>
    private static readonly object?[] ExpectedArrayValues = [SignedArrayValue, 0, NextUnsignedGroup];

    /// <summary>Provides address local ids.</summary>
    private static readonly uint[] AddressLocalIds = [1, 0x80];

    /// <summary>Provides name metadata.</summary>
    private static readonly string[] NameMetadata = ["é", "Speed"];

    /// <summary>Provides signed64 cases.</summary>
    private static readonly long[] Signed64Cases = [long.MinValue, long.MaxValue, -1, -0x40, -0x41, 0x007fffffffffffff, 0x0080000000000000, -0x0080000000000000, -0x0080000000000001];

    /// <summary>Provides signed32 cases.</summary>
    private static readonly Dictionary<int, byte[]> Signed32Cases = new()
    {
        [0] = [0],
        [0x3f] = [0x3f],
        [0x40] = [0x80, 0x40],
        [-1] = [0x7f],
        [-0x40] = [0x40],
        [-0x41] = [0xff, 0x3f],
        [-0xff] = [0xfe, 1],
        [int.MaxValue] = [0x87, 0xff, 0xff, 0xff, 0x7f],
        [int.MinValue] = [0xf8, 0x80, 0x80, 0x80, 0],
    };

    /// <summary>Verifies that erasure resets the writer and preserves subsequent encoding.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Writer_ClearSensitive_EmptiesBufferAndAllowsReuse()
    {
        await WireAssert.That(static () => new PlusWriter(-1)).Throws<ArgumentOutOfRangeException>();
        var writer = new PlusWriter(BlobVector.Length + ReusedWriterVector.Length);
        writer.Bytes(BlobVector);
        writer.ClearSensitive();
        await WireAssert.That(writer.ToArray().Length).IsEqualTo(0);
        writer.UInt16(0x1234);
        await WireAssert.That(ByteArrays.FixedTimeEquals(writer.ToArray(), ReusedWriterVector)).IsTrue();
        writer.ClearSensitive();
        writer.ClearSensitive();
        await WireAssert.That(writer.ToArray().Length).IsEqualTo(0);
    }

    /// <summary>Provides object_ attributes_ include object nesting in depth limit.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Object_Attributes_IncludeObjectNestingInDepthLimit()
    {
        var root = new PlusObject(1, 1);
        var leaf = root;
        for (var index = 0; index < ObjectDepthBoundary; index++)
        {
            PlusObject child = new(1, 1);
            leaf.Children.Add(child);
            leaf = child;
        }

        leaf.Attributes.Add(1, new(S7SymbolicDataType.Null, null));
        await WireAssert.That(() => root.WriteTo(new())).Throws<InvalidDataException>();
    }

    /// <summary>Provides unsigned vlq32_ known vectors_ use big endian groups.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task UnsignedVlq32_KnownVectors_UseBigEndianGroups()
    {
        var writer = new PlusWriter();
        writer.VarUInt32(0);
        writer.VarUInt32(0x7f);
        writer.VarUInt32(NextUnsignedGroup);
        writer.VarUInt32(uint.MaxValue);
        await WireAssert.That(ByteArrays.FixedTimeEquals(writer.ToArray(), Unsigned32Vector)).IsTrue();
        var reader = new PlusReader(Unsigned32Vector);
        await WireAssert.That(reader.VarUInt32()).IsEqualTo(0U);
        await WireAssert.That(reader.VarUInt32()).IsEqualTo(0x7fU);
        await WireAssert.That(reader.VarUInt32()).IsEqualTo(0x80U);
        await WireAssert.That(reader.VarUInt32()).IsEqualTo(uint.MaxValue);
        await WireAssert.That(reader.Remaining).IsEqualTo(0);
    }

    /// <summary>Provides signed vlq_ known vectors_ use first group sign bit.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task SignedVlq_KnownVectors_UseFirstGroupSignBit()
    {
        foreach (var item in Signed32Cases)
        {
            var writer = new PlusWriter();
            writer.VarInt32(item.Key);
            await WireAssert.That(ByteArrays.FixedTimeEquals(writer.ToArray(), item.Value)).IsTrue();
            await WireAssert.That(new PlusReader(item.Value).VarInt32()).IsEqualTo(item.Key);
        }
    }

    /// <summary>Provides vlq64_ ninth byte_ contains eight payload bits.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Vlq64_NinthByte_ContainsEightPayloadBits()
    {
        var maximumWriter = new PlusWriter();
        maximumWriter.VarUInt64(ulong.MaxValue);
        await WireAssert.That(ByteArrays.FixedTimeEquals(maximumWriter.ToArray(), MaximumUnsigned64Vector)).IsTrue();
        await WireAssert.That(new PlusReader(MaximumUnsigned64Vector).VarUInt64()).IsEqualTo(ulong.MaxValue);
        foreach (var value in Signed64Cases)
        {
            var writer = new PlusWriter();
            writer.VarInt64(value);
            await WireAssert.That(new PlusReader(writer.ToArray()).VarInt64()).IsEqualTo(value);
        }

        var boundaryWriter = new PlusWriter();
        boundaryWriter.VarUInt64(0x0100000000000000);
        await WireAssert.That(ByteArrays.FixedTimeEquals(boundaryWriter.ToArray(), FirstNineByteUnsignedVector)).IsTrue();
    }

    /// <summary>Reads er_ truncation overflow and invalid position_ are rejected.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Reader_TruncationOverflowAndInvalidPosition_AreRejected()
    {
        await WireAssert.That(static () => new PlusReader(TruncatedVlqVector).VarUInt32()).Throws<InvalidDataException>();
        await WireAssert.That(static () => new PlusReader(UnsignedOverflowVector).VarUInt32()).Throws<InvalidDataException>();
        await WireAssert.That(static () => new PlusReader(SignedOverflowVector).VarInt32()).Throws<InvalidDataException>();
        await WireAssert.That(static () => new PlusReader(UnterminatedVlqVector).VarUInt32()).Throws<InvalidDataException>();
        await WireAssert.That(static () => new PlusReader([]).UInt64()).Throws<InvalidDataException>();
        var reader = new PlusReader(SingleByteVector);
        await WireAssert.That(() => reader.Position = 0x2).Throws<InvalidDataException>();
        await WireAssert.That(() => reader.Skip(-1)).Throws<InvalidDataException>();
        await WireAssert.That(() => reader.ReadBytes(0x2)).Throws<InvalidDataException>();
    }

    /// <summary>Provides value_ unicode string_ uses utf8 byte length.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Value_UnicodeString_UsesUtf8ByteLength()
    {
        var writer = new PlusWriter();
        new S7SymbolicValue(S7SymbolicDataType.WString, "é水").WriteTo(writer);
        await WireAssert.That(ByteArrays.FixedTimeEquals(writer.ToArray(), UnicodeVector)).IsTrue();
        await WireAssert.That(S7SymbolicValue.ReadFrom(new(UnicodeVector)).Value).IsEqualTo("é水");
    }

    /// <summary>Provides value_ scalar types_ preserve values.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Value_ScalarTypes_PreserveValues()
    {
        S7SymbolicValue[] values =
        [
            new(S7SymbolicDataType.Null, null),
            new(S7SymbolicDataType.Bool, true),
            new(S7SymbolicDataType.USInt, UnsignedByteValue),
            new(S7SymbolicDataType.UInt, UnsignedWordValue),
            new(S7SymbolicDataType.UDInt, uint.MaxValue),
            new(S7SymbolicDataType.ULInt, ulong.MaxValue),
            new(S7SymbolicDataType.SInt, SignedByteValue),
            new(S7SymbolicDataType.Int, SignedWordValue),
            new(S7SymbolicDataType.DInt, int.MinValue),
            new(S7SymbolicDataType.LInt, long.MinValue),
            new(S7SymbolicDataType.Byte, BinaryByteValue),
            new(S7SymbolicDataType.Word, BinaryWordValue),
            new(S7SymbolicDataType.DWord, 0xfedcba98U),
            new(S7SymbolicDataType.LWord, ulong.MaxValue),
            new(S7SymbolicDataType.Real, SingleValue),
            new(S7SymbolicDataType.LReal, DoubleValue),
            new(S7SymbolicDataType.Timestamp, 0x0123456789abcdefUL),
            new(S7SymbolicDataType.Timespan, DurationValue),
            new(S7SymbolicDataType.RID, 0x12345678U),
            new(S7SymbolicDataType.AID, AttributeValue),
        ];
        foreach (var value in values)
        {
            var writer = new PlusWriter();
            value.WriteTo(writer);
            var reader = new PlusReader(writer.ToArray());
            var decoded = S7SymbolicValue.ReadFrom(reader);
            await WireAssert.That(decoded.DataType).IsEqualTo(value.DataType);
            await WireAssert.That(decoded.Value).IsEqualTo(value.Value);
            await WireAssert.That(reader.Remaining).IsEqualTo(0);
        }
    }

    /// <summary>Provides value_ arrays sparse blobs and structures_ are preserved.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Value_ArraysSparseBlobsAndStructures_ArePreserved()
    {
        var array = new S7SymbolicValue(S7SymbolicDataType.DInt, ArrayValues, 0x10);
        var arrayWriter = new PlusWriter();
        array.WriteTo(arrayWriter);
        var decodedArray = S7SymbolicValue.ReadFrom(new(arrayWriter.ToArray()));
        await WireAssert.That(((object?[])decodedArray.Value!).SequenceEqual(ExpectedArrayValues)).IsTrue();
        Dictionary<uint, object?> languages = new()
        {
            [0x409] = "Alarm",
            [0x407] = "Störung"
        };
        S7SymbolicValue sparse = new(S7SymbolicDataType.WString, languages, 0x40);
        Dictionary<uint, S7SymbolicValue> fields = new()
        {
            [1] = sparse,
            [0x2] = new(S7SymbolicDataType.Blob, new S7SymbolicBlob(0x7, BlobVector)),
        };
        var structure = new S7SymbolicValue(S7SymbolicDataType.Struct, new S7SymbolicStruct(0x2a, fields));
        var structureWriter = new PlusWriter();
        structure.WriteTo(structureWriter);
        var decoded = (S7SymbolicStruct)S7SymbolicValue.ReadFrom(new(structureWriter.ToArray())).Value!;
        await WireAssert.That(decoded.TypeId).IsEqualTo(0x2aU);
        await WireAssert.That(((Dictionary<uint, object?>)decoded.Fields[1].Value!)[0x407]).IsEqualTo("Störung");
        var blob = (S7SymbolicBlob)decoded.Fields[0x2].Value!;
        await WireAssert.That(blob.RootId).IsEqualTo(0x7U);
        await WireAssert.That(ByteArrays.FixedTimeEquals(blob.Data, BlobVector)).IsTrue();
    }

    /// <summary>Provides value_ packed struct_ preserves both lengths.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Value_PackedStruct_PreservesBothLengths()
    {
        var writer = new PlusWriter();
        var packed = new S7SymbolicPackedStruct(0x90000001, 0x7b, 0x400, PackedVector, 0x9);
        new S7SymbolicValue(S7SymbolicDataType.Struct, packed).WriteTo(writer);
        var value = (S7SymbolicPackedStruct)S7SymbolicValue.ReadFrom(new(writer.ToArray())).Value!;
        await WireAssert.That(value.TypeId).IsEqualTo(0x90000001U);
        await WireAssert.That(value.InterfaceTimestamp).IsEqualTo(0x7bUL);
        await WireAssert.That(value.FirstLength).IsEqualTo(0x9U);
        await WireAssert.That(ByteArrays.FixedTimeEquals(value.Data, PackedVector)).IsTrue();
    }

    /// <summary>Provides value_ malformed headers and counts_ are rejected.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Value_MalformedHeadersAndCounts_AreRejected()
    {
        await WireAssert.That(static () => S7SymbolicValue.ReadFrom(new(UnsupportedFlagsVector))).Throws<InvalidDataException>();
        await WireAssert.That(static () => S7SymbolicValue.ReadFrom(new(ReservedTypeVector))).Throws<InvalidDataException>();
        await WireAssert.That(static () => S7SymbolicValue.ReadFrom(new(UnsupportedSparseTypeVector))).Throws<InvalidDataException>();
        await WireAssert.That(static () => S7SymbolicValue.ReadFrom(new(InvalidBooleanVector))).Throws<InvalidDataException>();
        await WireAssert.That(static () => S7SymbolicValue.ReadFrom(new(TruncatedStringVector))).Throws<InvalidDataException>();
        await WireAssert.That(static () => S7SymbolicValue.ReadFrom(new(ExcessiveArrayVector))).Throws<InvalidDataException>();
        await WireAssert.That(static () => S7SymbolicValue.ReadFrom(new(NullVector), ExcessiveDepth)).Throws<InvalidDataException>();
    }

    /// <summary>Provides value_ unverified variant and s7 string layouts_ are explicitly unsupported.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Value_UnverifiedVariantAndS7StringLayouts_AreExplicitlyUnsupported()
    {
        await WireAssert.That(static () => S7SymbolicValue.ReadFrom(new(VariantVector))).Throws<NotSupportedException>();
        await WireAssert.That(static () => S7SymbolicValue.ReadFrom(new(S7StringVector))).Throws<NotSupportedException>();
        await WireAssert.That(static () => S7SymbolicValue.ReadFrom(new(EmptyVariantArrayVector))).Throws<NotSupportedException>();
        await WireAssert.That(static () => S7SymbolicValue.ReadFrom(new(EmptyS7StringArrayVector))).Throws<NotSupportedException>();
        await WireAssert.That(static () => new S7SymbolicValue(S7SymbolicDataType.Variant, null).WriteTo(new())).Throws<NotSupportedException>();
        await WireAssert.That(static () => new S7SymbolicValue(S7SymbolicDataType.S7String, null).WriteTo(new())).Throws<NotSupportedException>();
        await WireAssert.That(static () => new S7SymbolicValue(S7SymbolicDataType.Variant, EmptyArrayValues, 0x10).WriteTo(new())).Throws<NotSupportedException>();
        await WireAssert.That(static () => new S7SymbolicValue(S7SymbolicDataType.S7String, EmptyArrayValues, 0x20).WriteTo(new())).Throws<NotSupportedException>();
    }

    /// <summary>Provides object_ relations children and metadata_ are preserved.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Object_RelationsChildrenAndMetadata_ArePreserved()
    {
        var source = new PlusObject(0xc, 0x22, 0x38)
        {
            ClassFlags = 0x7
        };
        source.SetMetadata(TypeMetadataVector, NameMetadata);
        source.Relations.Add(0xa64, 0x8);
        source.Attributes.Add(0x14, new(S7SymbolicDataType.DInt, SignedArrayValue));
        source.Children.Add(new(0xd, 0x23));
        var writer = new PlusWriter();
        source.WriteTo(writer);
        var reader = new PlusReader(writer.ToArray());
        var result = PlusObject.ReadFrom(reader);
        await WireAssert.That(result.Id).IsEqualTo(0xcU);
        await WireAssert.That(result.ClassId).IsEqualTo(0x22U);
        await WireAssert.That(result.AttributeId).IsEqualTo(0x38U);
        await WireAssert.That(result.ClassFlags).IsEqualTo(0x7U);
        await WireAssert.That(result.Relations[0xa64]).IsEqualTo(0x8U);
        await WireAssert.That(result.Children[0].Id).IsEqualTo(0xdU);
        await WireAssert.That(ByteArrays.FixedTimeEquals(result.TypeInformation, TypeMetadataVector)).IsTrue();
        await WireAssert.That(result.Names.SequenceEqual(NameMetadata)).IsTrue();
        await WireAssert.That(reader.Remaining).IsEqualTo(0);
    }

    /// <summary>Provides object_ invalid markers and metadata_ are rejected.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Object_InvalidMarkersAndMetadata_AreRejected()
    {
        await WireAssert.That(static () => PlusObject.ReadFrom(new(EndObjectVector))).Throws<InvalidDataException>();
        await WireAssert.That(static () => PlusObject.ReadFrom(new(UnknownObjectMarkerVector))).Throws<InvalidDataException>();
        await WireAssert.That(static () => PlusObject.ReadFrom(new(BadNameTerminatorVector))).Throws<InvalidDataException>();
    }

    /// <summary>Provides address_ encodes crc area path count and local ids.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Address_EncodesCrcAreaPathCountAndLocalIds()
    {
        var ids = (uint[])AddressLocalIds.Clone();
        var address = new S7SymbolicAddress(0x2, 0x3, ids, 0x4);
        ids[0] = 0x63;
        var writer = new PlusWriter();
        address.WriteTo(writer);
        await WireAssert.That(ByteArrays.FixedTimeEquals(writer.ToArray(), AddressVector)).IsTrue();
        await WireAssert.That(address.LocalIds[0]).IsEqualTo(1U);
    }

    /// <summary>Provides response_ parses header and retains body position.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Response_ParsesHeaderAndRetainsBodyPosition()
    {
        var response = new PlusResponse(new(0x2, ResponseVector));
        await WireAssert.That(response.Function).IsEqualTo((ushort)0x04ca);
        await WireAssert.That(response.Sequence).IsEqualTo((ushort)0x1234);
        await WireAssert.That(response.Flags).IsEqualTo((byte)0x36);
        await WireAssert.That(response.Version).IsEqualTo((byte)0x2);
        await WireAssert.That(response.Body.Byte()).IsEqualTo((byte)0x63);
        await WireAssert.That(static () => new PlusResponse(new(0x2, BadResponseVector))).Throws<InvalidDataException>();
    }
}
