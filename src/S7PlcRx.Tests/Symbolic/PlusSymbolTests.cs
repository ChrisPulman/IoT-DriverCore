// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.Driver.S7PlcRx.Symbolic;
using IoT.Driver.S7PlcRx.Symbolic.Protocol;
using TUnitAssert = TUnit.Assertions.Assert;

namespace IoT.Driver.S7PlcRx.Tests.Symbolic;

/// <summary>Verifies native symbol metadata and indexed path resolution.</summary>
public sealed class PlusSymbolTests
{
    /// <summary>The signed lower bound used by the indexed metadata fixtures.</summary>
    private const int NegativeLowerBound = -2;

    /// <summary>The lower bound of the innermost Boolean dimension.</summary>
    private const int BooleanLowerBound = 5;

    /// <summary>The last coordinate of an aligned Boolean row.</summary>
    private const int LastAlignedCoordinate = 12;

    /// <summary>The positive coordinate in the quoted-name path fixture.</summary>
    private const int ParsedSecondCoordinate = 7;

    /// <summary>The PLC soft datatype for packed Boolean arrays.</summary>
    private const uint PackedBooleanSoftType = 40;

    /// <summary>The number of outer rows in the Boolean fixtures.</summary>
    private const uint BooleanRowCount = 2;

    /// <summary>The aligned innermost Boolean row width.</summary>
    private const uint AlignedBooleanWidth = 8;

    /// <summary>The nonaligned innermost Boolean row width.</summary>
    private const uint UnalignedBooleanWidth = 9;

    /// <summary>The final element offset in an aligned row.</summary>
    private const uint LastAlignedOffset = 7;

    /// <summary>The native stride of a nine-element Boolean row.</summary>
    private const uint PaddedBooleanStride = 16;

    /// <summary>The number of elements in the signed-bound fixture.</summary>
    private const uint SignedArrayElementCount = 3;

    /// <summary>The identifier carried by the standard member fixture.</summary>
    private const uint StandardMemberLocalId = 7;

    /// <summary>The high flags byte selecting HMI accessibility.</summary>
    private const byte HmiAccessibleFlagsByte = 2;

    /// <summary>Checks an FB-array's explicit count when six-dimensional counts are absent.</summary>
    /// <returns>A task completing after the shape and relation assertions.</returns>
    [Test]
    public async Task FunctionBlockArrayPreservesExplicitCountAndSignedLowerBound()
    {
        const int payloadLength = 120;
        const int localIdOffset = 4;
        const int softTypeOffset = 12;
        const int flagsOffset = 13;
        const int relationOffset = 28;
        const int elementCountOffset = 56;
        const int lowerBoundOffset = 72;
        const byte structSoftType = 17;
        const byte elementCount = 3;
        var bytes = new byte[payloadLength];
        bytes[0] = 1;
        bytes[localIdOffset] = 1;
        bytes[softTypeOffset] = structSoftType;
        bytes[flagsOffset] = HmiAccessibleFlagsByte;
        bytes[relationOffset] = 0x78;
        bytes[relationOffset + 1] = 0x56;
        bytes[relationOffset + sizeof(ushort)] = 0x34;
        bytes[relationOffset + sizeof(uint) - 1] = 0x12;
        bytes[elementCountOffset] = elementCount;
        bytes[lowerBoundOffset] = 0xfe;
        bytes[lowerBoundOffset + 1] = 0xff;
        bytes[lowerBoundOffset + sizeof(ushort)] = 0xff;
        bytes[lowerBoundOffset + sizeof(uint) - 1] = 0xff;
        var member = PlusTypeDecoder.Decode(bytes, ["Instances"])[0];
        await TUnitAssert.That(member.Relation).IsEqualTo(0x12345678U);
        await TUnitAssert.That(member.Lengths.Length).IsEqualTo(1);
        await TUnitAssert.That(member.LowerBounds[0]).IsEqualTo(NegativeLowerBound);
        await TUnitAssert.That(member.Lengths[0]).IsEqualTo((uint)elementCount);
        await TUnitAssert.That(member.Flatten([-1])).IsEqualTo(1U);
    }

    /// <summary>Checks quoted delimiters, escaped quotes, and signed array coordinates.</summary>
    /// <returns>A task completing after the metadata assertions.</returns>
    [Test]
    public async Task QuotedNamesAndNegativeCoordinatesArePreserved()
    {
        var parts = S7SymbolPath.Parse("\"DB.with.dot\".\"a\"\"b\"[-2, 7].leaf");
        await TUnitAssert.That(parts[0].Name).IsEqualTo("DB.with.dot");
        await TUnitAssert.That(parts[1].Name).IsEqualTo("a\"b");
        await TUnitAssert.That(parts[1].Indices[0]).IsEqualTo(NegativeLowerBound);
        await TUnitAssert.That(parts[1].Indices[1]).IsEqualTo(ParsedSecondCoordinate);
    }

    /// <summary>Checks that aligned Boolean rows retain their native stride.</summary>
    /// <returns>A task completing after the metadata assertions.</returns>
    [Test]
    public async Task BooleanArrayDoesNotAddPaddingToAnAlignedRow()
    {
        var member = new PlusTypeMember { SoftType = PackedBooleanSoftType };
        member.SetDimensions([-1, BooleanLowerBound], [BooleanRowCount, AlignedBooleanWidth]);
        await TUnitAssert.That(member.Flatten([0, BooleanLowerBound])).IsEqualTo(AlignedBooleanWidth);
        await TUnitAssert.That(member.Flatten([-1, LastAlignedCoordinate])).IsEqualTo(LastAlignedOffset);
    }

    /// <summary>Checks that multidimensional Boolean stride includes byte padding.</summary>
    /// <returns>A task completing after the metadata assertions.</returns>
    [Test]
    public async Task BooleanArrayPadsOnlyTheInnermostRow()
    {
        var member = new PlusTypeMember { SoftType = PackedBooleanSoftType };
        member.SetDimensions([-1, BooleanLowerBound], [BooleanRowCount, UnalignedBooleanWidth]);
        await TUnitAssert.That(member.Flatten([0, BooleanLowerBound])).IsEqualTo(PaddedBooleanStride);
    }

    /// <summary>Checks rejection of an index equal to the array count.</summary>
    /// <returns>A task completing after the metadata assertions.</returns>
    [Test]
    public async Task ExactUpperBoundIsRejected()
    {
        var member = new PlusTypeMember();
        member.SetDimensions([NegativeLowerBound], [SignedArrayElementCount]);
        await TUnitAssert.That(() => member.Flatten([1])).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Checks member identifiers, checksum, flags, and names from native type bytes.</summary>
    /// <returns>A task completing after the metadata assertions.</returns>
    [Test]
    public async Task TypeDecoderPreservesLocalIdentifierAndCrc()
    {
        byte[] bytes = [1, 0, 0, 0, 7, 0, 0, 0, 0x78, 0x56, 0x34, 0x12, 5, 0x8e, 0x80, 0, 2, 0, 4, 0];
        var member = PlusTypeDecoder.Decode(bytes, ["Temperature"])[0];
        await TUnitAssert.That(member.LocalId).IsEqualTo(StandardMemberLocalId);
        await TUnitAssert.That(member.Crc).IsEqualTo(0x12345678U);
        await TUnitAssert.That(member.Name).IsEqualTo("Temperature");
        await TUnitAssert.That(member.Flags).IsEqualTo((ushort)0x8e80);
    }
}
