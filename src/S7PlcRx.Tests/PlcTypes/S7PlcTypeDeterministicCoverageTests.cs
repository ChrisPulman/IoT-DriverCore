// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using IoT.DriverCore.S7PlcRx.Enums;
using IoT.DriverCore.S7PlcRx.PlcTypes;
using SystemTimeSpan = System.TimeSpan;

namespace IoT.DriverCore.S7PlcRx.Tests.PlcTypes;

/// <summary>Exercises deterministic PlcType conversion, bounds, and structured-value paths.</summary>
public sealed class S7PlcTypeDeterministicCoverageTests
{
    /// <summary>A negative signed word test value.</summary>
    private const short NegativeShort = -7;

    /// <summary>An offset signed word test value.</summary>
    private const short OffsetShort = -2;

    /// <summary>The signed-word transition value.</summary>
    private const int SignTransition = 32_768;

    /// <summary>A counter test value.</summary>
    private const ushort CounterValue = 0xBEEF;

    /// <summary>A DWord test value.</summary>
    private const uint DwordValue = 0x12345678U;

    /// <summary>A negative double test value.</summary>
    private const double NegativeDouble = -12.5D;

    /// <summary>The double value two.</summary>
    private const double Two = 2D;

    /// <summary>The hundredths timer expectation.</summary>
    private const double HundredthsTimer = 0.12D;

    /// <summary>The tenths timer expectation.</summary>
    private const double TenthsTimer = 12.3D;

    /// <summary>The seconds timer expectation.</summary>
    private const double SecondsTimer = 234D;

    /// <summary>The tens timer expectation.</summary>
    private const double TensTimer = 3450D;

    /// <summary>The standard date fixture year.</summary>
    private const int FixtureYear = 1999;

    /// <summary>The standard date fixture month.</summary>
    private const int FixtureMonth = 12;

    /// <summary>The standard date fixture day.</summary>
    private const int FixtureDay = 31;

    /// <summary>The standard date fixture hour.</summary>
    private const int FixtureHour = 23;

    /// <summary>The standard date fixture minute.</summary>
    private const int FixtureMinute = 59;

    /// <summary>The standard date fixture second.</summary>
    private const int FixtureSecond = 58;

    /// <summary>The standard date fixture millisecond.</summary>
    private const int FixtureMillisecond = 987;

    /// <summary>The long date fixture year.</summary>
    private const int LongFixtureYear = 2024;

    /// <summary>The long date fixture month.</summary>
    private const int LongFixtureMonth = 2;

    /// <summary>The long date fixture day.</summary>
    private const int LongFixtureDay = 29;

    /// <summary>The long date fixture hour.</summary>
    private const int LongFixtureHour = 12;

    /// <summary>The long date fixture minute.</summary>
    private const int LongFixtureMinute = 34;

    /// <summary>The long date fixture second.</summary>
    private const int LongFixtureSecond = 56;

    /// <summary>The long date fixture millisecond.</summary>
    private const int LongFixtureMillisecond = 789;

    /// <summary>The long date fixture additional ticks.</summary>
    private const long LongFixtureTicks = 1234;

    /// <summary>The serialized class count.</summary>
    private const short ClassCount = -12;

    /// <summary>An invalid month wire value.</summary>
    private const byte InvalidMonth = 13;

    /// <summary>The number of DateTimeLong values required to exercise its pooled-array path.</summary>
    private const int DateTimeLongPoolCount = 86;

    /// <summary>The serialized outer class status value.</summary>
    private const uint OuterStatus = 0xAABBCCDDU;

    /// <summary>The first serialized measurement.</summary>
    private const float FirstMeasurement = 1.5F;

    /// <summary>The second serialized measurement.</summary>
    private const float SecondMeasurement = -2.25F;

    /// <summary>The vector Y coordinate.</summary>
    private const float VectorY = -2F;

    /// <summary>The vector Z coordinate.</summary>
    private const float VectorZ = 3F;

    /// <summary>A byte value used by structured serialization.</summary>
    private const byte StructuredByte = 0x5A;

    /// <summary>An integer value used by structured serialization.</summary>
    private const int StructuredInteger = 12_345;

    /// <summary>The number of DateTime values required to exercise its pooled-array path.</summary>
    private const int DateTimePoolCount = 129;

    /// <summary>The aligned size of a Boolean-only class layout.</summary>
    private const double BooleanOnlyClassSize = 2D;

    /// <summary>The reserved character count used for narrow S7 strings.</summary>
    private const int StringReservedLength = 4;

    /// <summary>The narrow S7 string header size.</summary>
    private const int StringHeaderLength = 2;

    /// <summary>The buffer length used for invalid class layouts.</summary>
    private const int InvalidLayoutBufferLength = 16;

    /// <summary>The initial pooled byte-array capacity.</summary>
    private const int InitialByteArrayCapacity = 1;

    /// <summary>The number of time-span values that exceeds the pooled-buffer threshold.</summary>
    private const int PooledTimeSpanCount = 257;

    /// <summary>A byte pattern used by the Bit convenience overloads.</summary>
    private const byte BitPattern = 0x81;

    /// <summary>The limited number of bits requested from one byte.</summary>
    private const int LimitedBitCount = 4;

    /// <summary>The number of bits represented by one byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>The payload size that forces pooled byte-array growth.</summary>
    private const int ByteArrayGrowthLength = 64;

    /// <summary>An invalid standalone UTF-8 byte.</summary>
    private const byte InvalidUtf8Byte = 0xFF;

    /// <summary>The emitted true Boolean field name.</summary>
    private const string TrueFlagFieldName = "TrueFlag";

    /// <summary>The emitted false Boolean field name.</summary>
    private const string FalseFlagFieldName = "FalseFlag";

    /// <summary>The emitted byte field name.</summary>
    private const string MarkerFieldName = "Marker";

    /// <summary>The emitted signed word field name.</summary>
    private const string CountFieldName = "Count";

    /// <summary>The emitted unsigned word field name.</summary>
    private const string UnsignedCountFieldName = "UnsignedCount";

    /// <summary>The emitted unsigned double-word field name.</summary>
    private const string StatusFieldName = "Status";

    /// <summary>The emitted floating-point field name.</summary>
    private const string TotalFieldName = "Total";

    /// <summary>The emitted duration field name.</summary>
    private const string DurationFieldName = "Duration";

    /// <summary>The emitted narrow-string field name.</summary>
    private const string TextFieldName = "Text";

    /// <summary>The emitted wide-string field name.</summary>
    private const string WideTextFieldName = "WideText";

    /// <summary>Verifies signed and unsigned word-like types preserve values, offsets, and bounds.</summary>
    [Test]
    public void NumericPlcTypes_RoundtripValuesOffsetsAndBounds()
    {
        short[] intValues = [short.MinValue, NegativeShort, short.MaxValue];
        var intBytes = S7PlcRx.PlcTypes.Int.ToByteArray(intValues);
        Assert.That(S7PlcRx.PlcTypes.Int.ToArray(intBytes), Is.EqualTo(intValues));
        Assert.That(S7PlcRx.PlcTypes.Int.FromByteArray([0, 0xFF, 0xFE], 1), Is.EqualTo(OffsetShort));
        Assert.That(S7PlcRx.PlcTypes.Int.CWord(SignTransition), Is.EqualTo(short.MinValue));
        _ = Assert.Throws<ArgumentException>(() => S7PlcRx.PlcTypes.Int.FromSpan(stackalloc byte[1]));
        _ = Assert.Throws<ArgumentException>(() => S7PlcRx.PlcTypes.Int.ToSpan(1, stackalloc byte[1]));

        ushort[] counterValues = [0, CounterValue, ushort.MaxValue];
        var counterBytes = S7PlcRx.PlcTypes.Counter.ToByteArray(counterValues);
        Assert.That(S7PlcRx.PlcTypes.Counter.ToArray(counterBytes), Is.EqualTo(counterValues));
        Assert.That(S7PlcRx.PlcTypes.Counter.FromByteArray([0, 0x12, 0x34], 1), Is.EqualTo((ushort)0x1234));
        _ = Assert.Throws<ArgumentException>(() => S7PlcRx.PlcTypes.Counter.ToSpan(1, stackalloc byte[1]));

        uint[] dwordValues = [0, DwordValue, uint.MaxValue];
        var dwordBytes = S7PlcRx.PlcTypes.DWord.ToByteArray(dwordValues);
        Assert.That(S7PlcRx.PlcTypes.DWord.ToArray(dwordBytes), Is.EqualTo(dwordValues));
        Assert.That(S7PlcRx.PlcTypes.DWord.FromByteArray([0, 0x12, 0x34, 0x56, 0x78], 1), Is.EqualTo(DwordValue));
        Assert.That(S7PlcRx.PlcTypes.DWord.FromBytes(0x78, 0x56, 0x34, 0x12), Is.EqualTo(DwordValue));
        _ = Assert.Throws<ArgumentException>(() => S7PlcRx.PlcTypes.DWord.FromSpan(stackalloc byte[3]));
        _ = Assert.Throws<ArgumentException>(() => S7PlcRx.PlcTypes.DWord.ToSpan(1, stackalloc byte[3]));
    }

    /// <summary>Verifies integer array span writers.</summary>
    [Test]
    public void NumericPlcTypes_WriteIntegerArraySpans()
    {
        ushort[] counterValues = [0, CounterValue];
        var counterBytes = new byte[counterValues.Length * sizeof(ushort)];
        S7PlcRx.PlcTypes.Counter.ToSpan(counterValues, counterBytes);
        Assert.That(S7PlcRx.PlcTypes.Counter.ToArray(counterBytes), Is.EqualTo(counterValues));
        _ = Assert.Throws<ArgumentNullException>(
            () => S7PlcRx.PlcTypes.Counter.ToByteArray((ushort[])null!));

        short[] intValues = [NegativeShort, OffsetShort];
        var intBytes = new byte[intValues.Length * sizeof(short)];
        S7PlcRx.PlcTypes.Int.ToSpan(intValues, intBytes);
        Assert.That(S7PlcRx.PlcTypes.Int.ToArray(intBytes), Is.EqualTo(intValues));
        Assert.That(S7PlcRx.PlcTypes.Int.FromBytes(0xFE, 0xFF), Is.EqualTo(OffsetShort));

        ushort[] wordValues = [0, CounterValue];
        var wordBytes = new byte[wordValues.Length * sizeof(ushort)];
        S7PlcRx.PlcTypes.Word.ToSpan(wordValues, wordBytes);
        Assert.That(S7PlcRx.PlcTypes.Word.ToArray(wordBytes), Is.EqualTo(wordValues));

        int[] dintValues = [-StructuredInteger, StructuredInteger];
        var dintBytes = new byte[dintValues.Length * sizeof(int)];
        S7PlcRx.PlcTypes.DInt.ToSpan(dintValues, dintBytes);
        Assert.That(S7PlcRx.PlcTypes.DInt.ToArray(dintBytes), Is.EqualTo(dintValues));

        uint[] dwordValues = [0, DwordValue];
        var dwordBytes = new byte[dwordValues.Length * sizeof(uint)];
        S7PlcRx.PlcTypes.DWord.ToSpan(dwordValues, dwordBytes);
        Assert.That(S7PlcRx.PlcTypes.DWord.ToArray(dwordBytes), Is.EqualTo(dwordValues));
    }

    /// <summary>Verifies floating-point and timer array span writers.</summary>
    [Test]
    public void NumericPlcTypes_WriteFloatingPointAndTimerArraySpans()
    {
        float[] realValues = [FirstMeasurement, SecondMeasurement];
        var realBytes = new byte[realValues.Length * sizeof(float)];
        S7PlcRx.PlcTypes.Real.ToSpan(realValues, realBytes);
        Assert.That(S7PlcRx.PlcTypes.Real.ToArray(realBytes), Is.EqualTo(realValues));

        double[] lrealValues = [NegativeDouble, Two];
        var lrealBytes = new byte[lrealValues.Length * sizeof(double)];
        S7PlcRx.PlcTypes.LReal.ToSpan(lrealValues, lrealBytes);
        Assert.That(S7PlcRx.PlcTypes.LReal.ToArray(lrealBytes), Is.EqualTo(lrealValues));

        ushort[] timerValues = [0x0012, 0x1123];
        var timerBytes = new byte[timerValues.Length * sizeof(ushort)];
        S7PlcRx.PlcTypes.Timer.ToSpan(timerValues, timerBytes);
        Assert.That(
            S7PlcRx.PlcTypes.Timer.ToArray(timerBytes),
            Is.EqualTo((double[])[HundredthsTimer, TenthsTimer]));
    }

    /// <summary>Verifies normal and pooled time-span array writer paths.</summary>
    [Test]
    public void NumericPlcTypes_WriteTimeSpanArraySpans()
    {
        SystemTimeSpan[] timeSpanValues =
        [
            SystemTimeSpan.Zero,
            SystemTimeSpan.FromMilliseconds(StructuredInteger),
        ];
        var timeSpanBytes = new byte[
            timeSpanValues.Length * S7PlcRx.PlcTypes.TimeSpan.TypeLengthInBytes];
        S7PlcRx.PlcTypes.TimeSpan.ToSpan(timeSpanValues, timeSpanBytes);
        Assert.That(
            S7PlcRx.PlcTypes.TimeSpan.ToArray(timeSpanBytes),
            Is.EqualTo(timeSpanValues));
        var pooledTimeSpans = new SystemTimeSpan[PooledTimeSpanCount];
        Assert.That(
            S7PlcRx.PlcTypes.TimeSpan.ToByteArray(pooledTimeSpans).Length,
            Is.EqualTo(PooledTimeSpanCount * S7PlcRx.PlcTypes.TimeSpan.TypeLengthInBytes));
    }

    /// <summary>Verifies residual scalar, bit, string, and guard convenience overloads.</summary>
    [Test]
    public void PlcTypes_ExerciseResidualConvenienceOverloads()
    {
        Span<byte> singleByte = stackalloc byte[1];
        S7PlcRx.PlcTypes.Byte.ToSpan(StructuredByte, singleByte);
        Assert.That(singleByte[0], Is.EqualTo(StructuredByte));

        byte[] bitBytes = [BitPattern];
        Assert.That(S7PlcRx.PlcTypes.Bit.ToBitArray(bitBytes).Length, Is.EqualTo(BitsPerByte));
        Assert.That(
            S7PlcRx.PlcTypes.Bit.ToBitArray(new ReadOnlySpan<byte>(bitBytes)).Length,
            Is.EqualTo(BitsPerByte));
        Assert.That(
            S7PlcRx.PlcTypes.Bit.ToBitArray(bitBytes, LimitedBitCount).Length,
            Is.EqualTo(LimitedBitCount));
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => S7PlcRx.PlcTypes.Bit.SetBit(bitBytes, bitBytes.Length, 0, true));

        Span<byte> stringBytes = stackalloc byte[StringReservedLength];
        Assert.That(S7PlcRx.PlcTypes.String.ToSpan(null, stringBytes), Is.EqualTo(0));
        Assert.That(
            S7PlcRx.PlcTypes.String.ToSpan("S7", stringBytes),
            Is.EqualTo(StringHeaderLength));
        _ = Assert.Throws<ArgumentException>(
            () => _ = new S7StringAttribute(S7StringType.None, StringReservedLength));
    }

    /// <summary>Verifies floating point and timer time-base paths without relying on a PLC.</summary>
    [Test]
    public void FloatingPointAndTimerTypes_ConvertValuesAndRejectShortBuffers()
    {
        double[] lrealValues = [NegativeDouble, 0D, Math.PI];
        var lrealBytes = S7PlcRx.PlcTypes.LReal.ToByteArray(lrealValues);
        Assert.That(S7PlcRx.PlcTypes.LReal.ToArray(lrealBytes), Is.EqualTo(lrealValues));
        Assert.That(S7PlcRx.PlcTypes.LReal.FromByteArray([0, 0x40, 0, 0, 0, 0, 0, 0, 0], 1), Is.EqualTo(Two));
        _ = Assert.Throws<ArgumentException>(() => S7PlcRx.PlcTypes.LReal.FromSpan(stackalloc byte[7]));
        _ = Assert.Throws<ArgumentException>(() => S7PlcRx.PlcTypes.LReal.ToSpan(1D, stackalloc byte[7]));

        Assert.That(S7PlcRx.PlcTypes.Timer.FromByteArray([0x00, 0x12]), Is.EqualTo(HundredthsTimer));
        Assert.That(S7PlcRx.PlcTypes.Timer.FromByteArray([0x11, 0x23]), Is.EqualTo(TenthsTimer));
        Assert.That(S7PlcRx.PlcTypes.Timer.FromByteArray([0x22, 0x34]), Is.EqualTo(SecondsTimer));
        Assert.That(S7PlcRx.PlcTypes.Timer.FromByteArray([0x33, 0x45]), Is.EqualTo(TensTimer));
        ushort[] timerValues = [0x0012, 0x1123];
        double[] timerExpected = [HundredthsTimer, TenthsTimer];
        Assert.That(S7PlcRx.PlcTypes.Timer.ToArray(S7PlcRx.PlcTypes.Timer.ToByteArray(timerValues)), Is.EqualTo(timerExpected));
        _ = Assert.Throws<ArgumentException>(() => S7PlcRx.PlcTypes.Timer.FromByteArray(stackalloc byte[1], 0));
        _ = Assert.Throws<ArgumentException>(() => S7PlcRx.PlcTypes.Timer.ToSpan(1, stackalloc byte[1]));
    }

    /// <summary>Verifies both date encodings at bounds and invalid wire-value boundaries.</summary>
    [Test]
    public void DateTypes_RoundtripBoundsArraysAndInvalidWireComponents()
    {
        var date = new DateTimeOffset(
            FixtureYear,
            FixtureMonth,
            FixtureDay,
            FixtureHour,
            FixtureMinute,
            FixtureSecond,
            FixtureMillisecond,
            System.TimeSpan.Zero);
        DateTimeOffset[] dateValues = [date, S7PlcRx.PlcTypes.DateTime.SpecMaximumDateTime];
        Assert.That(S7PlcRx.PlcTypes.DateTime.ToArray(S7PlcRx.PlcTypes.DateTime.ToByteArray(dateValues)), Is.EqualTo(dateValues));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => S7PlcRx.PlcTypes.DateTime.ToByteArray((DateTimeOffset[])null!));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => S7PlcRx.PlcTypes.DateTime.FromByteArray([0x89, 0x1A, 1, 0, 0, 0, 0, 0]));

        var longDate = new DateTimeOffset(
            LongFixtureYear,
            LongFixtureMonth,
            LongFixtureDay,
            LongFixtureHour,
            LongFixtureMinute,
            LongFixtureSecond,
            LongFixtureMillisecond,
            System.TimeSpan.Zero).AddTicks(LongFixtureTicks);
        DateTimeOffset[] longDateValues = [longDate, S7PlcRx.PlcTypes.DateTimeLong.SpecMinimumDateTime];
        Assert.That(S7PlcRx.PlcTypes.DateTimeLong.ToArray(S7PlcRx.PlcTypes.DateTimeLong.ToByteArray(longDateValues)), Is.EqualTo(longDateValues));
        var singleLongDateBytes = S7PlcRx.PlcTypes.DateTimeLong.ToByteArray(longDate);
        Assert.That(S7PlcRx.PlcTypes.DateTimeLong.FromByteArray(singleLongDateBytes), Is.EqualTo(longDate));
        Assert.That(S7PlcRx.PlcTypes.DateTimeLong.FromSpan(singleLongDateBytes), Is.EqualTo(longDate));
        var pooledLongDateValues = new DateTimeOffset[DateTimeLongPoolCount];
        TestArray.Fill(pooledLongDateValues, longDate);
        Assert.That(
            S7PlcRx.PlcTypes.DateTimeLong.ToArray(S7PlcRx.PlcTypes.DateTimeLong.ToByteArray(pooledLongDateValues)),
            Is.EqualTo(pooledLongDateValues));
        _ = Assert.Throws<ArgumentNullException>(() => S7PlcRx.PlcTypes.DateTimeLong.ToByteArray((DateTimeOffset[])null!));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => S7PlcRx.PlcTypes.DateTimeLong.FromSpan(stackalloc byte[11]));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => S7PlcRx.PlcTypes.DateTimeLong.ToArray([0]));
        _ = Assert.Throws<ArgumentException>(() => S7PlcRx.PlcTypes.DateTimeLong.ToSpan(longDate, stackalloc byte[11]));
        _ = Assert.Throws<ArgumentException>(() => S7PlcRx.PlcTypes.DateTimeLong.ToSpan(longDateValues, stackalloc byte[23]));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => S7PlcRx.PlcTypes.DateTimeLong.ToByteArray(S7PlcRx.PlcTypes.DateTimeLong.SpecMinimumDateTime.AddTicks(-1)));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => S7PlcRx.PlcTypes.DateTimeLong.ToByteArray(S7PlcRx.PlcTypes.DateTimeLong.SpecMaximumDateTime.AddTicks(1)));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => S7PlcRx.PlcTypes.DateTimeLong.FromByteArray([0x07, 0xB1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0]));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => S7PlcRx.PlcTypes.DateTimeLong.FromByteArray([0x07, 0xE8, InvalidMonth, 1, 1, 0, 0, 0, 0, 0, 0, 0]));
    }

    /// <summary>Verifies date span overloads, pooled arrays, and invalid encoded components.</summary>
    [Test]
    public void DateTypes_ConvertSpanArraysAndRejectInvalidEncodedValues()
    {
        var date = new DateTimeOffset(
            FixtureYear,
            FixtureMonth,
            FixtureDay,
            FixtureHour,
            FixtureMinute,
            FixtureSecond,
            FixtureMillisecond,
            System.TimeSpan.Zero);
        DateTimeOffset[] dates = [date, S7PlcRx.PlcTypes.DateTime.SpecMinimumDateTime];
        var dateLength = S7PlcRx.PlcTypes.DateTime.ToByteArray(date).Length;
        var dateDestination = new byte[dateLength * dates.Length];
        S7PlcRx.PlcTypes.DateTime.ToSpan(dates, dateDestination);
        Assert.That(S7PlcRx.PlcTypes.DateTime.ToArray(dateDestination), Is.EqualTo(dates));
        var pooledDates = new DateTimeOffset[DateTimePoolCount];
        TestArray.Fill(pooledDates, date);
        Assert.That(
            S7PlcRx.PlcTypes.DateTime.ToArray(S7PlcRx.PlcTypes.DateTime.ToByteArray(pooledDates)),
            Is.EqualTo(pooledDates));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => S7PlcRx.PlcTypes.DateTime.FromSpan(new byte[dateLength - 1]));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => S7PlcRx.PlcTypes.DateTime.ToArray(new byte[dateLength - 1]));
        _ = Assert.Throws<ArgumentException>(() => S7PlcRx.PlcTypes.DateTime.ToSpan(dates, new byte[dateDestination.Length - 1]));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => S7PlcRx.PlcTypes.DateTime.FromByteArray([0xFA, 1, 1, 0, 0, 0, 0, 0]));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => S7PlcRx.PlcTypes.DateTime.FromByteArray([0x24, 0, 1, 0, 0, 0, 0, 0]));

        var longDate = new DateTimeOffset(
            LongFixtureYear,
            LongFixtureMonth,
            LongFixtureDay,
            LongFixtureHour,
            LongFixtureMinute,
            LongFixtureSecond,
            LongFixtureMillisecond,
            System.TimeSpan.Zero).AddTicks(LongFixtureTicks);
        DateTimeOffset[] longDates = [longDate, S7PlcRx.PlcTypes.DateTimeLong.SpecMinimumDateTime];
        var longDateLength = S7PlcRx.PlcTypes.DateTimeLong.ToByteArray(longDate).Length;
        var longDateDestination = new byte[longDateLength * longDates.Length];
        S7PlcRx.PlcTypes.DateTimeLong.ToSpan(longDates, longDateDestination);
        Assert.That(S7PlcRx.PlcTypes.DateTimeLong.ToArray(longDateDestination), Is.EqualTo(longDates));
    }

    /// <summary>Verifies Class and Struct validate metadata and null or mismatched inputs.</summary>
    [Test]
    public void StructuredTypes_RoundtripClassAndValidateInputs()
    {
        var source = new StructuredClass { Enabled = true, Count = ClassCount, Name = "S7" };
        var size = S7PlcRx.PlcTypes.Class.GetClassSize(source);
        var bytes = new byte[(int)size];
        Assert.That(S7PlcRx.PlcTypes.Class.ToBytes(source, bytes), Is.EqualTo(size));

        var destination = new StructuredClass();
        Assert.That(S7PlcRx.PlcTypes.Class.FromBytes(destination, bytes), Is.EqualTo(size));
        Assert.That(destination.Enabled, Is.True);
        Assert.That(destination.Count, Is.EqualTo(ClassCount));
        Assert.That(destination.Name, Is.EqualTo(source.Name));
        _ = Assert.Throws<ArgumentNullException>(() => S7PlcRx.PlcTypes.Class.GetClassSize(null!));
        _ = Assert.Throws<ArgumentException>(() => S7PlcRx.PlcTypes.Class.GetClassSize(new MissingStringMetadataClass()));

        Assert.That(S7PlcRx.PlcTypes.Struct.ToBytes(null!).Length, Is.EqualTo(0));
        _ = Assert.Throws<ArgumentNullException>(() => S7PlcRx.PlcTypes.Struct.GetStructSize(null!));
    }

    /// <summary>Verifies non-recursive class nesting, arrays, attributes, offsets, and struct fields.</summary>
    [Test]
    public void StructuredTypes_RoundtripNestedClassesArraysOffsetsAndVectorStruct()
    {
        var source = new OuterStructuredClass
        {
            Status = OuterStatus,
            Measurements = [FirstMeasurement, SecondMeasurement],
            Labels = ["A", "BC"],
            Details = new InnerStructuredClass { Active = true, WideLabel = "Ω" },
        };
        var offset = 2D;
        var size = S7PlcRx.PlcTypes.Class.GetClassSize(source, offset);
        var bytes = new byte[(int)size];
        Assert.That(S7PlcRx.PlcTypes.Class.ToBytes(source, bytes, offset), Is.EqualTo(size));

        var destination = new OuterStructuredClass();
        Assert.That(S7PlcRx.PlcTypes.Class.FromBytes(destination, bytes, offset), Is.EqualTo(size));
        Assert.That(destination.Status, Is.EqualTo(source.Status));
        Assert.That(destination.Measurements, Is.EqualTo(source.Measurements));
        Assert.That(destination.Labels, Is.EqualTo(source.Labels));
        Assert.That(destination.Details.Active, Is.EqualTo(source.Details.Active));
        Assert.That(destination.Details.WideLabel, Is.EqualTo(source.Details.WideLabel));

        _ = Assert.Throws<InvalidOperationException>(() => S7PlcRx.PlcTypes.Class.GetClassSize(new EmptyArrayClass()));
        _ = Assert.Throws<ArgumentException>(() => S7PlcRx.PlcTypes.Class.GetClassSize(new NullArrayClass()));
        _ = Assert.Throws<ArgumentNullException>(() => S7PlcRx.PlcTypes.Class.ToBytes(source, null!));
        Assert.That(S7PlcRx.PlcTypes.Class.FromBytes(destination, null!, offset), Is.EqualTo(offset));

        var vector = new Vector3(1F, VectorY, VectorZ);
        var vectorBytes = S7PlcRx.PlcTypes.Struct.ToBytes(vector);
        Assert.That(S7PlcRx.PlcTypes.Struct.GetStructSize(typeof(Vector3)), Is.EqualTo(vectorBytes.Length));
        Assert.That((Vector3)S7PlcRx.PlcTypes.Struct.FromBytes(typeof(Vector3), vectorBytes)!, Is.EqualTo(vector));
        Assert.That(S7PlcRx.PlcTypes.Struct.FromBytes(typeof(Vector3), null!), Is.NullValue);
        Assert.That(S7PlcRx.PlcTypes.Struct.FromBytes(typeof(Vector3), new byte[vectorBytes.Length - 1]), Is.NullValue);
    }

    /// <summary>Verifies the supported scalar Class and Struct field representations.</summary>
    [Test]
    public void StructuredTypes_RoundtripAllSupportedScalarFields()
    {
        var source = new ScalarStructuredClass
        {
            Enabled = false,
            Marker = StructuredByte,
            Count = ClassCount,
            UnsignedCount = CounterValue,
            SignedStatus = StructuredInteger,
            Status = DwordValue,
            Measurement = FirstMeasurement,
            Total = NegativeDouble,
        };
        var size = S7PlcRx.PlcTypes.Class.GetClassSize(source);
        var bytes = new byte[(int)size];
        Assert.That(S7PlcRx.PlcTypes.Class.ToBytes(source, bytes), Is.EqualTo(size));
        var destination = new ScalarStructuredClass();
        Assert.That(S7PlcRx.PlcTypes.Class.FromBytes(destination, bytes), Is.EqualTo(size));
        Assert.That(destination.Enabled, Is.False);
        Assert.That(destination.Marker, Is.EqualTo(source.Marker));
        Assert.That(destination.Count, Is.EqualTo(source.Count));
        Assert.That(destination.UnsignedCount, Is.EqualTo(source.UnsignedCount));
        Assert.That(destination.SignedStatus, Is.EqualTo(source.SignedStatus));
        Assert.That(destination.Status, Is.EqualTo(source.Status));
        Assert.That(destination.Measurement, Is.EqualTo(source.Measurement));
        Assert.That(destination.Total, Is.EqualTo(source.Total));
        Assert.That(S7PlcRx.PlcTypes.Class.GetClassSize(new BooleanOnlyClass()), Is.EqualTo(BooleanOnlyClassSize));
        _ = Assert.Throws<ArgumentNullException>(() => S7PlcRx.PlcTypes.Class.FromBytes(null!, bytes));
        _ = Assert.Throws<ArgumentNullException>(() => S7PlcRx.PlcTypes.Class.ToBytes(null!, bytes));

        var trueTuple = (true, StructuredByte, ClassCount, CounterValue, StructuredInteger, DwordValue, FirstMeasurement);
        var falseTuple = (false, StructuredByte, ClassCount, CounterValue, StructuredInteger, DwordValue, FirstMeasurement);
        Assert.That(S7PlcRx.PlcTypes.Struct.GetStructSize(trueTuple.GetType()), Is.EqualTo(S7PlcRx.PlcTypes.Struct.ToBytes(trueTuple).Length));
        Assert.That(
            ((bool, byte, short, ushort, int, uint, float))S7PlcRx.PlcTypes.Struct.FromBytes(trueTuple.GetType(), S7PlcRx.PlcTypes.Struct.ToBytes(trueTuple))!,
            Is.EqualTo(trueTuple));
        Assert.That(
            ((bool, byte, short, ushort, int, uint, float))S7PlcRx.PlcTypes.Struct.FromBytes(falseTuple.GetType(), S7PlcRx.PlcTypes.Struct.ToBytes(falseTuple))!,
            Is.EqualTo(falseTuple));
        var timedTuple = (NegativeDouble, SystemTimeSpan.FromSeconds(SecondsTimer));
        Assert.That(
            ((double, SystemTimeSpan))S7PlcRx.PlcTypes.Struct.FromBytes(timedTuple.GetType(), S7PlcRx.PlcTypes.Struct.ToBytes(timedTuple))!,
            Is.EqualTo(timedTuple));
        _ = Assert.Throws<ArgumentException>(() => S7PlcRx.PlcTypes.Struct.GetStructSize(typeof(ValueTuple<string>)));
    }

    /// <summary>Verifies Class scalar fields and invalid property layouts without the pending Int32 path.</summary>
    [Test]
    public void StructuredTypes_RoundtripSafeScalarClassAndValidatePropertyLayouts()
    {
        var source = new SafeScalarStructuredClass
        {
            Enabled = false,
            Marker = StructuredByte,
            Count = ClassCount,
            UnsignedCount = CounterValue,
            Status = DwordValue,
            Measurement = FirstMeasurement,
            Total = NegativeDouble,
            Text = "RX",
            WideText = "Ω",
        };
        var size = S7PlcRx.PlcTypes.Class.GetClassSize(source);
        var bytes = new byte[(int)size];
        Assert.That(S7PlcRx.PlcTypes.Class.ToBytes(source, bytes), Is.EqualTo(size));
        var destination = new SafeScalarStructuredClass();
        Assert.That(S7PlcRx.PlcTypes.Class.FromBytes(destination, bytes), Is.EqualTo(size));
        Assert.That(destination.Enabled, Is.False);
        Assert.That(destination.Marker, Is.EqualTo(source.Marker));
        Assert.That(destination.Count, Is.EqualTo(source.Count));
        Assert.That(destination.UnsignedCount, Is.EqualTo(source.UnsignedCount));
        Assert.That(destination.Status, Is.EqualTo(source.Status));
        Assert.That(destination.Measurement, Is.EqualTo(source.Measurement));
        Assert.That(destination.Total, Is.EqualTo(source.Total));
        Assert.That(destination.Text, Is.EqualTo(source.Text));
        Assert.That(destination.WideText, Is.EqualTo(source.WideText));

        var invalidBytes = new byte[InvalidLayoutBufferLength];
        _ = Assert.Throws<ArgumentException>(() => S7PlcRx.PlcTypes.Class.FromBytes(new MissingStringMetadataClass(), invalidBytes));
        _ = Assert.Throws<ArgumentException>(() => S7PlcRx.PlcTypes.Class.ToBytes(new MissingStringMetadataClass(), invalidBytes));
        _ = Assert.Throws<ArgumentException>(() => S7PlcRx.PlcTypes.Class.ToBytes(new NullStringValueClass(), invalidBytes));
        _ = Assert.Throws<ArgumentException>(() => S7PlcRx.PlcTypes.Class.FromBytes(new NullArrayClass(), invalidBytes));
        var nullArrayElementSource = new OuterStructuredClass();
        var nullArrayElementBytes = new byte[(int)S7PlcRx.PlcTypes.Class.GetClassSize(nullArrayElementSource)];
        _ = Assert.Throws<ArgumentException>(() => S7PlcRx.PlcTypes.Class.ToBytes(nullArrayElementSource, nullArrayElementBytes));
        Assert.That(S7PlcRx.PlcTypes.Class.GetClassSize(new BooleanOnlyClass()), Is.EqualTo(BooleanOnlyClassSize));
        _ = Assert.Throws<ArgumentNullException>(() => S7PlcRx.PlcTypes.Class.FromBytes(null!, bytes));
        _ = Assert.Throws<ArgumentNullException>(() => S7PlcRx.PlcTypes.Class.ToBytes(null!, bytes));
    }

    /// <summary>Verifies supported fixed-size arrays and bounded partial-buffer traversal.</summary>
    [Test]
    public void StructuredTypes_RoundtripSafeArraysAndStopAtBufferBoundary()
    {
        var source = new SafeArrayStructuredClass
        {
            Flags = [true, false],
            Markers = [StructuredByte, 0],
            Counts = [ClassCount, NegativeShort],
            UnsignedCounts = [CounterValue, 0],
            Statuses = [DwordValue, 0],
            Measurements = [FirstMeasurement, SecondMeasurement],
            Totals = [NegativeDouble, Two],
            Text = ["RX", "S7"],
            WideText = ["Ω", "λ"],
            Details =
            [
                new InnerStructuredClass { Active = true, WideLabel = "Ω" },
                new InnerStructuredClass { Active = false, WideLabel = "λ" },
            ],
        };

        var size = S7PlcRx.PlcTypes.Class.GetClassSize(source);
        var bytes = new byte[(int)size];
        Assert.That(S7PlcRx.PlcTypes.Class.ToBytes(source, bytes), Is.EqualTo(size));

        var destination = new SafeArrayStructuredClass();
        Assert.That(S7PlcRx.PlcTypes.Class.FromBytes(destination, bytes), Is.EqualTo(size));
        Assert.That(destination.Flags, Is.EqualTo(source.Flags));
        Assert.That(destination.Markers, Is.EqualTo(source.Markers));
        Assert.That(destination.Counts, Is.EqualTo(source.Counts));
        Assert.That(destination.UnsignedCounts, Is.EqualTo(source.UnsignedCounts));
        Assert.That(destination.Statuses, Is.EqualTo(source.Statuses));
        Assert.That(destination.Measurements, Is.EqualTo(source.Measurements));
        Assert.That(destination.Totals, Is.EqualTo(source.Totals));
        Assert.That(destination.Text, Is.EqualTo(source.Text));
        Assert.That(destination.WideText, Is.EqualTo(source.WideText));
        Assert.That(destination.Details[0].Active, Is.True);
        Assert.That(destination.Details[0].WideLabel, Is.EqualTo(source.Details[0].WideLabel));
        Assert.That(destination.Details[1].Active, Is.False);
        Assert.That(destination.Details[1].WideLabel, Is.EqualTo(source.Details[1].WideLabel));

        var partialSource = new FixedShortArrayClass { Values = [ClassCount, NegativeShort] };
        var partialBytes = new byte[sizeof(short)];
        Assert.That(S7PlcRx.PlcTypes.Class.ToBytes(partialSource, partialBytes), Is.EqualTo(sizeof(short)));

        var partialDestination = new FixedShortArrayClass { Values = [0, OffsetShort] };
        Assert.That(
            S7PlcRx.PlcTypes.Class.FromBytes(partialDestination, partialBytes),
            Is.EqualTo(sizeof(short)));
        Assert.That(partialDestination.Values[0], Is.EqualTo(ClassCount));
        Assert.That(partialDestination.Values[1], Is.EqualTo(OffsetShort));
    }

    /// <summary>Verifies both populated and zero-sized nested Struct traversal.</summary>
    [Test]
    public void StructuredTypes_DeserializeNestedAndZeroSizedStructFields()
    {
        var nestedVector = new ValueTuple<Vector3>(Vector3.Zero);
        var nestedVectorBytes = S7PlcRx.PlcTypes.Struct.ToBytes(nestedVector);
        Assert.That(
            S7PlcRx.PlcTypes.Struct.GetStructSize(nestedVector.GetType()),
            Is.EqualTo(nestedVectorBytes.Length));
        Assert.That(
            (ValueTuple<Vector3>)S7PlcRx.PlcTypes.Struct.FromBytes(
                nestedVector.GetType(),
                nestedVectorBytes)!,
            Is.EqualTo(nestedVector));

        var zeroSized = new ValueTuple<ValueTuple>(default);
        var zeroSizedBytes = S7PlcRx.PlcTypes.Struct.ToBytes(zeroSized);
        Assert.That(zeroSizedBytes.Length, Is.EqualTo(0));
        Assert.That(
            (ValueTuple<ValueTuple>)S7PlcRx.PlcTypes.Struct.FromBytes(
                zeroSized.GetType(),
                zeroSizedBytes)!,
            Is.EqualTo(zeroSized));
    }

    /// <summary>Verifies narrow and wide S7 string success and malformed-data boundaries.</summary>
    [Test]
    [NotInParallel]
    public void StringTypes_ConvertBoundariesAndWrapDecoderFailures()
    {
        var previousEncoding = S7PlcRx.PlcTypes.S7String.StringEncoding;
        try
        {
            _ = Assert.Throws<ArgumentNullException>(() => S7PlcRx.PlcTypes.S7String.StringEncoding = null!);
            S7PlcRx.PlcTypes.S7String.StringEncoding = new UTF8Encoding(false, true);
            _ = Assert.Throws<PlcException>(() =>
                S7PlcRx.PlcTypes.S7String.FromByteArray([1, 1, InvalidUtf8Byte]));
            var destination = new byte[S7PlcRx.PlcTypes.S7String.GetByteLength(StringReservedLength)];
            Assert.That(
                S7PlcRx.PlcTypes.S7String.TryToSpan("RX", StringReservedLength, destination, out var bytesWritten),
                Is.True);
            Assert.That(bytesWritten, Is.EqualTo(StringReservedLength + StringHeaderLength));
        }
        finally
        {
            S7PlcRx.PlcTypes.S7String.StringEncoding = previousEncoding;
        }

        var fullLength = S7PlcRx.PlcTypes.S7String.GetByteLength(StringReservedLength);
        _ = Assert.Throws<ArgumentNullException>(() =>
            S7PlcRx.PlcTypes.S7String.ToSpan(null, StringReservedLength, new byte[fullLength]));
        _ = Assert.Throws<ArgumentException>(() =>
            S7PlcRx.PlcTypes.S7String.ToSpan("RX", StringReservedLength, new byte[fullLength - 1]));
        Assert.That(fullLength, Is.EqualTo(StringReservedLength + StringHeaderLength));
        _ = Assert.Throws<PlcException>(() =>
            S7PlcRx.PlcTypes.S7WString.FromByteArray([0, 1, 0, 1]));
    }

    /// <summary>Verifies pooled ByteArray accessors, growth, copying, clearing, and disposal.</summary>
    [Test]
    public void ByteArray_GrowsCopiesClearsAndDisposesIdempotently()
    {
        var buffer = new ByteArray(InitialByteArrayCapacity);
        try
        {
            buffer.Add(ReadOnlySpan<byte>.Empty);
            Assert.That(buffer.Length, Is.EqualTo(0));
            Assert.That(buffer.Memory.Length, Is.EqualTo(0));
            buffer.Add(StructuredByte);
            var payload = new byte[ByteArrayGrowthLength];
            TestArray.Fill(payload, StructuredByte);
            buffer.Add(payload);
            Assert.That(buffer.Length, Is.EqualTo(ByteArrayGrowthLength + InitialByteArrayCapacity));
            Assert.That(buffer.Memory.Length, Is.EqualTo(buffer.Length));
            Assert.That(buffer.Array[0], Is.EqualTo(StructuredByte));
            Assert.That(buffer.TryCopyTo(new byte[buffer.Length - 1]), Is.False);
            var destination = new byte[buffer.Length];
            Assert.That(buffer.TryCopyTo(destination), Is.True);
            Assert.That(destination, Is.EqualTo(buffer.Array));
            buffer.Clear();
            Assert.That(buffer.Length, Is.EqualTo(0));
        }
        finally
        {
            buffer.Dispose();
            buffer.Dispose();
        }
    }

    /// <summary>Verifies reachable Struct field branches with a non-recursive emitted value type.</summary>
    [Test]
    public void StructuredTypes_RoundtripSafeEmittedStructFields()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            S7PlcRx.PlcTypes.Struct.GetStructSize(typeof(ValueTuple<string>)));

        var fixtureType = CreateSafeStructFixtureType();
        var source = Activator.CreateInstance(fixtureType) ??
            throw new InvalidOperationException("Failed to create the emitted Struct fixture.");
        SetFieldValue(fixtureType, source, TrueFlagFieldName, true);
        SetFieldValue(fixtureType, source, FalseFlagFieldName, false);
        SetFieldValue(fixtureType, source, MarkerFieldName, StructuredByte);
        SetFieldValue(fixtureType, source, CountFieldName, ClassCount);
        SetFieldValue(fixtureType, source, UnsignedCountFieldName, CounterValue);
        SetFieldValue(fixtureType, source, StatusFieldName, 0U);
        SetFieldValue(fixtureType, source, TotalFieldName, NegativeDouble);
        SetFieldValue(fixtureType, source, DurationFieldName, SystemTimeSpan.FromSeconds(SecondsTimer));
        SetFieldValue(fixtureType, source, TextFieldName, "S7");
        SetFieldValue(fixtureType, source, WideTextFieldName, "Ω");

        var bytes = S7PlcRx.PlcTypes.Struct.ToBytes(source);
        Assert.That(S7PlcRx.PlcTypes.Struct.GetStructSize(fixtureType), Is.EqualTo(bytes.Length));
        var destination = S7PlcRx.PlcTypes.Struct.FromBytes(fixtureType, bytes) ??
            throw new InvalidOperationException("Failed to deserialize the emitted Struct fixture.");
        Assert.That(GetFieldValue<bool>(fixtureType, destination, TrueFlagFieldName), Is.True);
        Assert.That(GetFieldValue<bool>(fixtureType, destination, FalseFlagFieldName), Is.False);
        Assert.That(GetFieldValue<byte>(fixtureType, destination, MarkerFieldName), Is.EqualTo(StructuredByte));
        Assert.That(GetFieldValue<short>(fixtureType, destination, CountFieldName), Is.EqualTo(ClassCount));
        Assert.That(GetFieldValue<ushort>(fixtureType, destination, UnsignedCountFieldName), Is.EqualTo(CounterValue));
        Assert.That(GetFieldValue<uint>(fixtureType, destination, StatusFieldName), Is.EqualTo(0U));
        Assert.That(GetFieldValue<double>(fixtureType, destination, TotalFieldName), Is.EqualTo(NegativeDouble));
        Assert.That(
            GetFieldValue<SystemTimeSpan>(fixtureType, destination, DurationFieldName),
            Is.EqualTo(SystemTimeSpan.FromSeconds(SecondsTimer)));
        Assert.That(GetFieldValue<string>(fixtureType, destination, TextFieldName), Is.EqualTo("S7"));
        Assert.That(GetFieldValue<string>(fixtureType, destination, WideTextFieldName), Is.EqualTo("Ω"));
    }

    /// <summary>Creates an analyzer-safe runtime value type containing supported Struct fields.</summary>
    /// <returns>The emitted non-recursive value type.</returns>
    private static Type CreateSafeStructFixtureType()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("S7PlcTypeDeterministicCoverageFixtures"),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Fixtures");
        var typeBuilder = module.DefineType(
            "SafeS7StructFixture",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout,
            typeof(ValueType));
        _ = typeBuilder.DefineField(TrueFlagFieldName, typeof(bool), FieldAttributes.Public);
        _ = typeBuilder.DefineField(FalseFlagFieldName, typeof(bool), FieldAttributes.Public);
        _ = typeBuilder.DefineField(MarkerFieldName, typeof(byte), FieldAttributes.Public);
        _ = typeBuilder.DefineField(CountFieldName, typeof(short), FieldAttributes.Public);
        _ = typeBuilder.DefineField(UnsignedCountFieldName, typeof(ushort), FieldAttributes.Public);
        _ = typeBuilder.DefineField(StatusFieldName, typeof(uint), FieldAttributes.Public);
        _ = typeBuilder.DefineField(TotalFieldName, typeof(double), FieldAttributes.Public);
        _ = typeBuilder.DefineField(DurationFieldName, typeof(SystemTimeSpan), FieldAttributes.Public);
        DefineStringField(typeBuilder, TextFieldName, S7StringType.S7String);
        DefineStringField(typeBuilder, WideTextFieldName, S7StringType.S7WString);
        return typeBuilder.CreateType() ??
            throw new InvalidOperationException("Failed to create the emitted Struct fixture type.");
    }

    /// <summary>Defines an attributed string field on an emitted Struct fixture.</summary>
    /// <param name="typeBuilder">The fixture type builder.</param>
    /// <param name="fieldName">The field name.</param>
    /// <param name="stringType">The S7 string representation.</param>
    private static void DefineStringField(
        TypeBuilder typeBuilder,
        string fieldName,
        S7StringType stringType)
    {
        var field = typeBuilder.DefineField(fieldName, typeof(string), FieldAttributes.Public);
        var constructor = typeof(S7StringAttribute).GetConstructor([typeof(S7StringType), typeof(int)]) ??
            throw new InvalidOperationException("Failed to locate the S7StringAttribute constructor.");
        field.SetCustomAttribute(
            new CustomAttributeBuilder(constructor, [stringType, StringReservedLength]));
    }

    /// <summary>Sets a public field on an emitted Struct fixture.</summary>
    /// <param name="fixtureType">The emitted fixture type.</param>
    /// <param name="fixture">The fixture instance.</param>
    /// <param name="fieldName">The public field name.</param>
    /// <param name="value">The field value.</param>
    private static void SetFieldValue(Type fixtureType, object fixture, string fieldName, object value)
    {
        var field = fixtureType.GetField(fieldName) ??
            throw new InvalidOperationException($"Field '{fieldName}' was not emitted.");
        field.SetValue(fixture, value);
    }

    /// <summary>Gets a typed public field value from an emitted Struct fixture.</summary>
    /// <typeparam name="TValue">The expected field value type.</typeparam>
    /// <param name="fixtureType">The emitted fixture type.</param>
    /// <param name="fixture">The fixture instance.</param>
    /// <param name="fieldName">The public field name.</param>
    /// <returns>The typed field value.</returns>
    private static TValue GetFieldValue<TValue>(Type fixtureType, object fixture, string fieldName)
    {
        var value = fixtureType.GetField(fieldName)?.GetValue(fixture);
        return value is TValue typedValue
            ? typedValue
            : throw new InvalidOperationException($"Field '{fieldName}' did not contain {typeof(TValue)}.");
    }

    /// <summary>Provides a class serialization fixture.</summary>
    public sealed class StructuredClass
    {
        /// <summary>Gets or sets a Boolean marker.</summary>
        public bool Enabled { get; set; }

        /// <summary>Gets or sets a signed count.</summary>
        public short Count { get; set; }

        /// <summary>Gets or sets the S7 text.</summary>
        [S7String(S7StringType.S7String, 4)]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Provides a missing string metadata fixture.</summary>
    public sealed class MissingStringMetadataClass
    {
        /// <summary>Gets or sets text without serialization metadata.</summary>
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>Provides a non-recursive nested class serialization fixture.</summary>
    public sealed class OuterStructuredClass
    {
        /// <summary>Gets or sets the unsigned status.</summary>
        public uint Status { get; set; }

        /// <summary>Gets or sets the fixed-size measurements.</summary>
        public float[] Measurements { get; init; } = new float[2];

        /// <summary>Gets or sets fixed-size labels.</summary>
        [S7String(S7StringType.S7String, 3)]
        public string[] Labels { get; init; } = new string[2];

        /// <summary>Gets or sets the nested details.</summary>
        public InnerStructuredClass Details { get; set; } = new();
    }

    /// <summary>Provides a nested class serialization fixture.</summary>
    public sealed class InnerStructuredClass
    {
        /// <summary>Gets or sets a Boolean marker.</summary>
        public bool Active { get; set; }

        /// <summary>Gets or sets a wide S7 text label.</summary>
        [S7String(S7StringType.S7WString, 2)]
        public string WideLabel { get; set; } = string.Empty;
    }

    /// <summary>Provides an invalid empty-array layout fixture.</summary>
    public sealed class EmptyArrayClass
    {
        /// <summary>Gets or sets the variable-length values.</summary>
        public short[] Values { get; init; } = [];
    }

    /// <summary>Provides an invalid null-array layout fixture.</summary>
    public sealed class NullArrayClass
    {
        /// <summary>Gets or sets the required values.</summary>
        public short[] Values { get; init; } = null!;
    }

    /// <summary>Provides all supported scalar properties for class serialization.</summary>
    public sealed class ScalarStructuredClass
    {
        /// <summary>Gets or sets a Boolean marker.</summary>
        public bool Enabled { get; set; }

        /// <summary>Gets or sets a byte marker.</summary>
        public byte Marker { get; set; }

        /// <summary>Gets or sets a signed count.</summary>
        public short Count { get; set; }

        /// <summary>Gets or sets an unsigned count.</summary>
        public ushort UnsignedCount { get; set; }

        /// <summary>Gets or sets a signed status.</summary>
        public int SignedStatus { get; set; }

        /// <summary>Gets or sets an unsigned status.</summary>
        public uint Status { get; set; }

        /// <summary>Gets or sets a single precision measurement.</summary>
        public float Measurement { get; set; }

        /// <summary>Gets or sets a double precision total.</summary>
        public double Total { get; set; }
    }

    /// <summary>Provides an odd-sized class layout fixture.</summary>
    public sealed class BooleanOnlyClass
    {
        /// <summary>Gets or sets a Boolean marker.</summary>
        public bool Enabled { get; set; }
    }

    /// <summary>Provides supported scalar properties without the pending Int32 path.</summary>
    public sealed class SafeScalarStructuredClass
    {
        /// <summary>The reserved character count used for wide S7 strings.</summary>
        private const int WideStringReservedLength = 2;

        /// <summary>Gets or sets a Boolean marker.</summary>
        public bool Enabled { get; set; }

        /// <summary>Gets or sets a byte marker.</summary>
        public byte Marker { get; set; }

        /// <summary>Gets or sets a signed count.</summary>
        public short Count { get; set; }

        /// <summary>Gets or sets an unsigned count.</summary>
        public ushort UnsignedCount { get; set; }

        /// <summary>Gets or sets an unsigned status.</summary>
        public uint Status { get; set; }

        /// <summary>Gets or sets a single precision measurement.</summary>
        public float Measurement { get; set; }

        /// <summary>Gets or sets a double precision total.</summary>
        public double Total { get; set; }

        /// <summary>Gets or sets a narrow S7 string.</summary>
        [S7String(S7StringType.S7String, StringReservedLength)]
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets or sets a wide S7 string.</summary>
        [S7String(S7StringType.S7WString, WideStringReservedLength)]
        public string WideText { get; set; } = string.Empty;
    }

    /// <summary>Provides a null property value for Class validation.</summary>
    public sealed class NullStringValueClass
    {
        /// <summary>Gets or sets the required narrow string.</summary>
        [S7String(S7StringType.S7String, StringReservedLength)]
        public string Value { get; set; } = null!;
    }

    /// <summary>Provides supported fixed-size array properties for class serialization.</summary>
    public sealed class SafeArrayStructuredClass
    {
        /// <summary>The reserved character count used for wide S7 strings.</summary>
        private const int WideStringReservedLength = 2;

        /// <summary>Gets or sets Boolean markers.</summary>
        public bool[] Flags { get; init; } = new bool[2];

        /// <summary>Gets or sets byte markers.</summary>
        public byte[] Markers { get; init; } = new byte[2];

        /// <summary>Gets or sets signed counts.</summary>
        public short[] Counts { get; init; } = new short[2];

        /// <summary>Gets or sets unsigned counts.</summary>
        public ushort[] UnsignedCounts { get; init; } = new ushort[2];

        /// <summary>Gets or sets unsigned status values.</summary>
        public uint[] Statuses { get; init; } = new uint[2];

        /// <summary>Gets or sets single-precision measurements.</summary>
        public float[] Measurements { get; init; } = new float[2];

        /// <summary>Gets or sets double-precision totals.</summary>
        public double[] Totals { get; init; } = new double[2];

        /// <summary>Gets or sets narrow S7 strings.</summary>
        [S7String(S7StringType.S7String, StringReservedLength)]
        public string[] Text { get; init; } = new string[2];

        /// <summary>Gets or sets wide S7 strings.</summary>
        [S7String(S7StringType.S7WString, WideStringReservedLength)]
        public string[] WideText { get; init; } = new string[2];

        /// <summary>Gets or sets nested details.</summary>
        public InnerStructuredClass[] Details { get; init; } =
            [new InnerStructuredClass(), new InnerStructuredClass()];
    }

    /// <summary>Provides a fixed-size signed-word array for partial-buffer traversal.</summary>
    public sealed class FixedShortArrayClass
    {
        /// <summary>Gets or sets signed words.</summary>
        public short[] Values { get; init; } = new short[2];
    }
}
