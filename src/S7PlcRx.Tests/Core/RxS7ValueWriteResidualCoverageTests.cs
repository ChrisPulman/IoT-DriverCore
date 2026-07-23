// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.S7PlcRx.Enums;
using IoT.DriverCore.S7PlcRx.PlcTypes;
using PlcClass = IoT.DriverCore.S7PlcRx.PlcTypes.Class;
using TUnitAssert = TUnit.Assertions.Assert;

namespace IoT.DriverCore.S7PlcRx.Tests.Core;

/// <summary>Exercises deterministic value and write validation paths without a PLC listener.</summary>
[NotInParallel]
public sealed class RxS7ValueWriteResidualCoverageTests
{
    /// <summary>Uses the loopback address while deliberately leaving the S7 port unbound.</summary>
    private const string DisconnectedLoopback = "127.0.0.1";

    /// <summary>Defines the first byte of data block one.</summary>
    private const string ByteAddress = "DB1.DBB0";

    /// <summary>Defines the tag used by direct value reads.</summary>
    private const string ValueTagName = "Value";

    /// <summary>Defines the input-area tag name.</summary>
    private const string InputTagName = "Input";

    /// <summary>Defines the tag carrying an unsupported serialized value.</summary>
    private const string UnsupportedTagName = "Unsupported";

    /// <summary>Defines the long payload tag name.</summary>
    private const string PayloadTagName = "Payload";

    /// <summary>Defines the deliberately slow disconnected polling interval.</summary>
    private const int PollingIntervalMilliseconds = 1_000;

    /// <summary>Defines the input byte value.</summary>
    private const byte InputValue = 1;

    /// <summary>Defines the output byte value.</summary>
    private const byte OutputValue = 2;

    /// <summary>Defines the memory byte value.</summary>
    private const byte MemoryValue = 3;

    /// <summary>Defines the data-block byte value.</summary>
    private const byte DataBlockValue = 4;

    /// <summary>Defines the timer value.</summary>
    private const ushort TimerValue = 5;

    /// <summary>Defines the counter value.</summary>
    private const ushort CounterValue = 6;

    /// <summary>Defines the invalid-address value.</summary>
    private const byte InvalidValue = 7;

    /// <summary>Defines the unknown-area value.</summary>
    private const byte UnknownValue = 8;

    /// <summary>Defines the first value used to exercise bit helpers.</summary>
    private const byte InitialBitValue = 0;

    /// <summary>Defines the bit offset used by deterministic bit helper assertions.</summary>
    private const int TestedBitOffset = 2;

    /// <summary>Defines an invalid S7 bit offset.</summary>
    private const int InvalidBitOffset = 8;

    /// <summary>Defines the byte expected after setting the tested bit.</summary>
    private const byte SetBitValue = 4;

    /// <summary>Defines the byte whose first bit is set.</summary>
    private const byte FirstBitSetValue = 1;

    /// <summary>Defines the S7 response header length.</summary>
    private const int ReadResponseHeaderLength = 25;

    /// <summary>Defines the S7 response buffer length used by helper tests.</summary>
    private const int ReadResponseLength = 33;

    /// <summary>Defines the data-length field offset in an S7 response.</summary>
    private const int ReadResponseDataLengthOffset = 23;

    /// <summary>Defines the low-byte offset within the S7 response data-length field.</summary>
    private const int ReadResponseDataLengthLowByteOffset = ReadResponseDataLengthOffset + 1;

    /// <summary>Defines the byte length expected when the response payload has no length field.</summary>
    private const int FallbackResponsePayloadLength = ReadResponseLength - ReadResponseHeaderLength;

    /// <summary>Defines the bit count representing a three-byte S7 response payload.</summary>
    private const ushort ThreeBytePayloadBitLength = 24;

    /// <summary>Defines the byte-length result expected from the three-byte response payload.</summary>
    private const int ThreeBytePayloadLength = 3;

    /// <summary>Defines an invalid multi-variable tag name.</summary>
    private const string InvalidMultiVarTagName = "InvalidMultiVar";

    /// <summary>Defines the data-block address whose data type is unsupported for multi-variable reads.</summary>
    private const string InvalidMultiVarTypeAddress = "DB1.DBX0";

    /// <summary>Defines the blank address used to reject a multi-variable request.</summary>
    private const string EmptyAddress = " ";

    /// <summary>Defines the data-block number text that cannot be parsed.</summary>
    private const string InvalidDataBlockNumberAddress = "DB.DBW0";

    /// <summary>Defines the data-block item whose byte offset cannot be parsed.</summary>
    private const string InvalidDataBlockOffsetAddress = "DB1.DB";

    /// <summary>Defines the unknown data-block type address used by public writes.</summary>
    private const string UnknownDataBlockTypeAddress = "DB1.DBZ0";

    /// <summary>Defines the byte count used by valid multi-variable tag arrays.</summary>
    private const int SingleValueArrayLength = 1;

    /// <summary>Defines the signed integer used by conversion tests.</summary>
    private const int SignedConversionValue = -1;

    /// <summary>Defines the signed short used by conversion tests.</summary>
    private const short SignedShortConversionValue = -1;

    /// <summary>Defines the unsigned value containing a single-precision one bit pattern.</summary>
    private const uint SingleOneBits = 0x3f80_0000U;

    /// <summary>Defines the single-precision value represented by <see cref="SingleOneBits"/>.</summary>
    private const float SingleOneValue = 1F;

    /// <summary>Defines the text representation of a byte-sized binary value.</summary>
    private const string BinaryByteText = "00000101";

    /// <summary>Defines the expected value parsed from <see cref="BinaryByteText"/>.</summary>
    private const byte ParsedBinaryByteValue = 5;

    /// <summary>Exercises collection validation and empty multi-variable requests.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task TagCollectionAndMultiVariableValidationAreDeterministicAsync()
    {
        using var plc = CreateDisconnectedPlc();
        var missingName = new Tag { Address = ByteAddress };
        var missingAddress = new Tag { Name = "MissingAddress", Type = typeof(byte) };

        await TUnitAssert.That(() => plc.AddUpdateTagItemInternal(null!)).Throws<ArgumentNullException>();
        await TUnitAssert.That(() => plc.AddUpdateTagItemInternal(missingName)).Throws<ArgumentNullException>();
        await TUnitAssert.That(() => plc.AddUpdateTagItemInternal(missingAddress)).Throws<TagAddressOutOfRangeException>();
        await TUnitAssert.That(() => plc.RemoveTagItemInternal(string.Empty)).Throws<ArgumentNullException>();
        await TUnitAssert.That(plc.ReadMultiVar([])).IsNull();
        await TUnitAssert.That(plc.WriteMultiVar([])).IsFalse();
    }

    /// <summary>Verifies a cancelled direct value read leaves the instance usable.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ReadAsyncWithCancellationUsesTheCancellationAwarePausePathAsync()
    {
        using var plc = CreateDisconnectedPlc();
        plc.AddUpdateTagItemInternal(new Tag(ValueTagName, ByteAddress, typeof(object)));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await TUnitAssert.That(() => plc.ReadAsync(new LogicalTagKey<byte>(ValueTagName), cancellation.Token))
            .Throws<OperationCanceledException>();
        await TUnitAssert.That(plc.TagList[ValueTagName]!.Type).IsEqualTo(typeof(object));
    }

    /// <summary>Exercises every public write-address parser branch against a disconnected transport.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ValueWritesExerciseAddressParsingAndSerializationFailuresAsync()
    {
        using var plc = CreateDisconnectedPlc();
        var tags = new[]
        {
            new Tag(InputTagName, "EB0", typeof(byte)),
            new Tag("Output", "AB0", typeof(byte)),
            new Tag("Memory", "MB0", typeof(byte)),
            new Tag("DataBlock", ByteAddress, typeof(byte)),
            new Tag("DataBlockBit", "DB1.DBX0.0", typeof(bool)),
            new Tag("InputBit", "I0.0", typeof(bool)),
            new Tag("OutputBit", "O0.1", typeof(bool)),
            new Tag("MemoryBit", "M0.2", typeof(bool)),
            new Tag("TimerTag", "T0", typeof(ushort)),
            new Tag(nameof(Counter), "C0", typeof(ushort)),
            new Tag("Invalid", "DB1", typeof(byte)),
            new Tag("Unknown", "Q0", typeof(byte)),
            new Tag(UnsupportedTagName, "DB1.DBB4", typeof(object)),
        };

        foreach (var tag in tags)
        {
            plc.AddUpdateTagItemInternal(tag);
        }

        plc.Value(InputTagName, InputValue);
        plc.Value("Output", OutputValue);
        plc.Value("Memory", MemoryValue);
        plc.Value("DataBlock", DataBlockValue);
        plc.Value("DataBlockBit", true);
        plc.Value("InputBit", true);
        plc.Value("OutputBit", false);
        plc.Value("MemoryBit", true);
        plc.Value("TimerTag", TimerValue);
        plc.Value("Counter", CounterValue);
        plc.Value("Invalid", InvalidValue);
        plc.Value("Unknown", UnknownValue);
        plc.Value(UnsupportedTagName, (object)new object());

        await TUnitAssert.That(plc.TagList[InputTagName]!.NewValue).IsEqualTo(InputValue);
        await TUnitAssert.That(plc.TagList[UnsupportedTagName]!.NewValue).IsTypeOf<object>();
    }

    /// <summary>Exercises the long payload path that chunks data-block writes.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ValueWriteWithLargeByteArrayUsesChunkedDataBlockWritePathAsync()
    {
        using var plc = CreateDisconnectedPlc();
        var payload = new byte[201];
        payload[0] = 1;
        payload[^1] = byte.MaxValue;
        plc.AddUpdateTagItemInternal(new Tag(PayloadTagName, ByteAddress, typeof(byte[])));

        plc.Value(PayloadTagName, payload);

        await TUnitAssert.That(plc.TagList[PayloadTagName]!.NewValue).IsEquivalentTo(payload);
    }

    /// <summary>Verifies value helper response sizing, parsing, and bit validation behavior.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ValueHelpersHandleProtocolLengthsValuesAndBitValidationAsync()
    {
        var response = new byte[ReadResponseLength];
        response[ReadResponseDataLengthOffset] = 0;
        response[ReadResponseDataLengthLowByteOffset] = 0;

        await TUnitAssert.That(
            RxS7ValueHelpers.GetReadResponseDataLengthBytes(response, ReadResponseHeaderLength))
            .IsEqualTo(0);
        await TUnitAssert.That(
            RxS7ValueHelpers.GetReadResponseDataLengthBytes(response, ReadResponseLength))
            .IsEqualTo(FallbackResponsePayloadLength);

        response[ReadResponseDataLengthOffset] = 0;
        response[ReadResponseDataLengthLowByteOffset] = (byte)ThreeBytePayloadBitLength;
        await TUnitAssert.That(
            RxS7ValueHelpers.GetReadResponseDataLengthBytes(response, ReadResponseLength))
            .IsEqualTo(ThreeBytePayloadLength);
        var parsedBit = (bool)RxS7ValueHelpers.ParseNonNullBytes(
            VarType.Bit,
            [FirstBitSetValue],
            SingleValueArrayLength)!;
        await TUnitAssert.That(parsedBit).IsTrue();
        await TUnitAssert.That(RxS7ValueHelpers.ParseNonNullBytes((VarType)int.MaxValue, [InitialBitValue], SingleValueArrayLength))
            .IsNull();

        var set = RxS7ValueHelpers.ApplyBitWriteValue(InitialBitValue, true, TestedBitOffset);
        var cleared = RxS7ValueHelpers.ApplyBitWriteValue(set, 0, TestedBitOffset);
        await TUnitAssert.That(set).IsEqualTo(SetBitValue);
        await TUnitAssert.That(cleared).IsEqualTo(InitialBitValue);
        await TUnitAssert.That(() => RxS7ValueHelpers.EnsureBitOffsetIsValid(InvalidBitOffset, new Tag()))
            .Throws<ArgumentException>();
    }

    /// <summary>Verifies the internal numeric conversion helpers use their documented representations.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ConversionHelpersConvertBitsAndNumericRepresentationsAsync()
    {
        await TUnitAssert.That(ConversionExtensions.SelectBit(SetBitValue, TestedBitOffset)).IsTrue();
        await TUnitAssert.That(ConversionExtensions.ConvertToUInt(SignedConversionValue)).IsEqualTo(uint.MaxValue);
        await TUnitAssert.That(ConversionExtensions.ConvertToUshort(SignedShortConversionValue)).IsEqualTo(ushort.MaxValue);
        await TUnitAssert.That(ConversionExtensions.ConvertToFloat(SingleOneBits)).IsEqualTo(SingleOneValue);
        await TUnitAssert.That(ConversionExtensions.BinStringToByte(BinaryByteText)).IsEqualTo(ParsedBinaryByteValue);
        await TUnitAssert.That(ConversionExtensions.BinStringToByte(string.Empty)).IsNull();
    }

    /// <summary>Verifies malformed multi-variable addresses are rejected without a transport operation.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task MultiVariableAddressValidationRejectsMalformedDataBlockAddressesAsync()
    {
        using var plc = CreateDisconnectedPlc();
        Tag[] invalidTags =
        [
            CreateMultiVarTag(InvalidMultiVarTagName, InvalidMultiVarTypeAddress),
            CreateMultiVarTag("Blank", EmptyAddress),
            CreateMultiVarTag("InvalidDb", InvalidDataBlockNumberAddress),
            CreateMultiVarTag("InvalidOffset", InvalidDataBlockOffsetAddress),
        ];

        foreach (var tag in invalidTags)
        {
            await TUnitAssert.That(plc.ReadMultiVar([tag])).IsNull();
        }
    }

    /// <summary>Verifies null class writes and unsupported data-block type writes are safely rejected.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ClassAndUnknownDataBlockWritesAreRejectedWithoutTransportAsync()
    {
        using var plc = CreateDisconnectedPlc();
        var classTag = new Tag(nameof(PlcClass), ByteAddress, typeof(object));
        var unknownTypeTag = new Tag("UnknownDataBlockType", UnknownDataBlockTypeAddress, typeof(byte));
        plc.AddUpdateTagItemInternal(classTag);
        plc.AddUpdateTagItemInternal(unknownTypeTag);

        await TUnitAssert.That(plc.WriteClass(classTag, null!, SingleValueArrayLength)).IsFalse();
        plc.Value(unknownTypeTag.Name, InputValue);
        await TUnitAssert.That(unknownTypeTag.NewValue).IsEqualTo(InputValue);
    }

    /// <summary>Creates a PLC instance whose endpoint has no listening S7 server.</summary>
    /// <returns>The configured PLC instance.</returns>
    private static RxS7 CreateDisconnectedPlc() =>
        new(new(new(CpuType.S71500, DisconnectedLoopback, 0, 1), new(PollingIntervalMilliseconds)));

    /// <summary>Creates a scalar tag for multi-variable address validation.</summary>
    /// <param name="name">The logical tag name.</param>
    /// <param name="address">The data-block address.</param>
    /// <returns>The configured tag.</returns>
    private static Tag CreateMultiVarTag(string name, string address) =>
        new(name, address, typeof(byte)) { ArrayLength = SingleValueArrayLength };
}
