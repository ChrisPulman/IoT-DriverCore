// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using IoT.Driver.S7PlcRx.Symbolic;
using IoT.Driver.S7PlcRx.Symbolic.Protocol;
using TUnitAssert = TUnit.Assertions.Assert;

namespace IoT.Driver.S7PlcRx.Tests.Symbolic;

/// <summary>Verifies CLR mappings against serialized native symbolic values.</summary>
public sealed class PlusTypeConversionTests
{
    /// <summary>Checks signed and unsigned 64-bit representations without narrowing.</summary>
    /// <param name="softType">The PLC soft datatype.</param>
    /// <param name="expected">The expected native CLR scalar.</param>
    /// <returns>A task completing after the wire roundtrip assertions.</returns>
    [Test]
    [Arguments(49U, ulong.MaxValue)]
    [Arguments(50U, long.MinValue)]
    [Arguments(51U, ulong.MaxValue)]
    public async Task Scalar64ValuesPreserveAllBits(uint softType, object expected)
    {
        var decoded = Roundtrip(expected, softType);
        await TUnitAssert.That(S7SymbolicTypeConversion.ToClr<object>(decoded, softType)).IsEqualTo(expected);
    }

    /// <summary>Checks STRING Latin-1 bytes and WSTRING UTF-16 words with capacity headers.</summary>
    /// <returns>A task completing after the string assertions.</returns>
    [Test]
    public async Task StringEncodingsPreserveTheirDistinctWireRepresentations()
    {
        const uint stringType = 19;
        const uint wideStringType = 62;
        const int capacity = 8;
        const int insufficientCapacity = 2;
        const string latin = "café";
        const string unicode = "Ω𐐀";
        var narrow = Roundtrip(latin, stringType, capacity);
        var wide = Roundtrip(unicode, wideStringType, capacity);
        await TUnitAssert.That(narrow.DataType).IsEqualTo(S7SymbolicDataType.USInt);
        await TUnitAssert.That(wide.DataType).IsEqualTo(S7SymbolicDataType.UInt);
        var narrowBytes = (System.Collections.IList)narrow.Value!;
        var wideWords = (System.Collections.IList)wide.Value!;
        const byte latinAcuteE = 0xe9;
        const int accentIndex = 5;
        const int firstCharacterIndex = 2;
        await TUnitAssert.That(narrowBytes[0]).IsEqualTo((object)(byte)capacity);
        await TUnitAssert.That(narrowBytes[1]).IsEqualTo((object)(byte)latin.Length);
        await TUnitAssert.That(narrowBytes[accentIndex]).IsEqualTo((object)latinAcuteE);
        await TUnitAssert.That(wideWords[0]).IsEqualTo((object)(ushort)capacity);
        await TUnitAssert.That(wideWords[1]).IsEqualTo((object)(ushort)unicode.Length);
        await TUnitAssert.That(wideWords[firstCharacterIndex]).IsEqualTo((object)(ushort)unicode[0]);
        await TUnitAssert.That(S7SymbolicTypeConversion.ToClr<string>(narrow, stringType)).IsEqualTo(latin);
        await TUnitAssert.That(S7SymbolicTypeConversion.ToClr<string>(wide, wideStringType)).IsEqualTo(unicode);
        await TUnitAssert.That(static () => S7SymbolicTypeConversion.FromClr(unicode, stringType, capacity)).Throws<ArgumentException>();
        await TUnitAssert.That(static () => S7SymbolicTypeConversion.FromClr(latin, stringType, insufficientCapacity)).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Checks DATE, BCD DATE_AND_TIME, and UTC long timestamps.</summary>
    /// <returns>A task completing after the calendar assertions.</returns>
    [Test]
    public async Task CalendarValuesRoundtripThroughNativeWireValues()
    {
        const uint dateType = 9;
        const uint dateAndTimeType = 14;
        const uint longDateTimeType = 66;
        var date = DateTime.Parse("2026-10-06", CultureInfo.InvariantCulture);
        var timestamp = DateTime.Parse("2026-10-06T12:34:56.789Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        var dateWire = Roundtrip(date, dateType);
        var dateEpoch = DateTime.Parse("1990-01-01", CultureInfo.InvariantCulture);
        await TUnitAssert.That(dateWire.Value).IsEqualTo((object)checked((ushort)(date - dateEpoch).TotalDays));
        var bcd = (System.Collections.IList)Roundtrip(timestamp, dateAndTimeType).Value!;
        byte[] expectedBcd = [0x26, 0x10, 0x06, 0x12, 0x34, 0x56, 0x78, 0x93];
        for (var i = 0; i < expectedBcd.Length; i++)
        {
            await TUnitAssert.That(bcd[i]).IsEqualTo((object)expectedBcd[i]);
        }

        await TUnitAssert.That(S7SymbolicTypeConversion.ToClr<DateTime>(Roundtrip(date, dateType), dateType)).IsEqualTo(date);
        await TUnitAssert.That(S7SymbolicTypeConversion.ToClr<DateTime>(Roundtrip(timestamp, dateAndTimeType), dateAndTimeType).Ticks).IsEqualTo(timestamp.Ticks);
        await TUnitAssert.That(S7SymbolicTypeConversion.ToClr<DateTime>(Roundtrip(timestamp, longDateTimeType), longDateTimeType)).IsEqualTo(timestamp);
    }

    /// <summary>Checks duration units and the valid upper bound of time of day.</summary>
    /// <param name="softType">The PLC temporal datatype.</param>
    /// <returns>A task completing after the duration assertions.</returns>
    [Test]
    [Arguments(10U)]
    [Arguments(11U)]
    [Arguments(12U)]
    [Arguments(64U)]
    [Arguments(65U)]
    public async Task TemporalUnitsAreMappedWithoutNarrowing(uint softType)
    {
        var duration = TimeSpan.FromSeconds(1);
        const uint milliseconds = 1_000;
        const ulong nanoseconds = 1_000_000_000;
        const ushort oneSecondS5Bcd = 0x0100;
        const uint timeType = 11;
        const uint s5TimeType = 12;
        const uint longTimeType = 64;
        const uint longTimeOfDayType = 65;
        object expectedWire = softType switch
        {
            timeType => (int)milliseconds,
            s5TimeType => oneSecondS5Bcd,
            longTimeType => (long)nanoseconds,
            longTimeOfDayType => nanoseconds,
            _ => milliseconds
        };
        var wire = Roundtrip(duration, softType);
        await TUnitAssert.That(wire.Value).IsEqualTo(expectedWire);
        await TUnitAssert.That(S7SymbolicTypeConversion.ToClr<TimeSpan>(wire, softType)).IsEqualTo(duration);
        const uint timeOfDayType = 10;
        await TUnitAssert.That(static () => S7SymbolicTypeConversion.FromClr(TimeSpan.FromDays(1), timeOfDayType, 0)).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Checks DTL fractions beyond CLR ticks and the required structure interface metadata.</summary>
    /// <returns>A task completing after the packed structure assertions.</returns>
    [Test]
    public async Task DtlPreservesNanosecondsAndInterfaceMetadata()
    {
        const uint dtlType = 67;
        const uint nanoseconds = 123_456_789;
        const ulong interfaceTimestamp = 0x123456789abcdef0;
        var date = DateTimeOffset.Parse("2026-10-06T12:34:56Z", CultureInfo.InvariantCulture);
        var expected = new S7SymbolicDtl(date, nanoseconds, interfaceTimestamp);
        var decoded = S7SymbolicTypeConversion.ToClr<S7SymbolicDtl>(Roundtrip(expected, dtlType), dtlType);
        await TUnitAssert.That(decoded.DateTime).IsEqualTo(expected.DateTime);
        await TUnitAssert.That(decoded.Nanoseconds).IsEqualTo(nanoseconds);
        await TUnitAssert.That(decoded.InterfaceTimestamp).IsEqualTo(interfaceTimestamp);
    }

    /// <summary>Checks homogeneous arrays using wire serialization rather than only conversion helpers.</summary>
    /// <returns>A task completing after the native array assertions.</returns>
    [Test]
    public async Task NativeArraysPreserveElementWidthsAndTemporalUnits()
    {
        const uint unsignedLongType = 49;
        const uint timeType = 11;
        ulong[] integers = [0, ulong.MaxValue];
        TimeSpan[] durations = [TimeSpan.Zero, TimeSpan.FromMilliseconds(1)];
        var decodedIntegers = S7SymbolicTypeConversion.ToClr<ulong[]>(Roundtrip(integers, unsignedLongType), unsignedLongType);
        var decodedDurations = S7SymbolicTypeConversion.ToClr<TimeSpan[]>(Roundtrip(durations, timeType), timeType);
        await TUnitAssert.That(decodedIntegers[1]).IsEqualTo(ulong.MaxValue);
        await TUnitAssert.That(decodedDurations[1]).IsEqualTo(durations[1]);
    }

    /// <summary>Serializes and parses a mapped value to exercise both conversion and wire contracts.</summary>
    /// <param name="value">The native CLR input.</param>
    /// <param name="softType">The PLC soft datatype.</param>
    /// <returns>The parsed wire value.</returns>
    private static S7SymbolicValue Roundtrip(object value, uint softType) => Roundtrip(value, softType, 0);

    /// <summary>Serializes and parses a mapped value with an explicit string capacity.</summary>
    /// <param name="value">The native CLR input.</param>
    /// <param name="softType">The PLC soft datatype.</param>
    /// <param name="capacity">The declared PLC string capacity.</param>
    /// <returns>The parsed wire value.</returns>
    private static S7SymbolicValue Roundtrip(object value, uint softType, int capacity)
    {
        var writer = new PlusWriter();
        S7SymbolicTypeConversion.FromClr(value, softType, capacity).WriteTo(writer);
        return S7SymbolicValue.ReadFrom(new(writer.ToArray()));
    }
}
