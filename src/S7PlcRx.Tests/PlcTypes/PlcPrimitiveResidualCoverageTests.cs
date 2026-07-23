// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Plc = IoT.DriverCore.S7PlcRx.PlcTypes;

namespace IoT.DriverCore.S7PlcRx.Tests.PlcTypes;

/// <summary>Exercises public primitive PLC codecs through deterministic value and validation scenarios.</summary>
public sealed class PlcPrimitiveResidualCoverageTests
{
    /// <summary>A signed integer round-trip value.</summary>
    private const int IntegerValue = -1_234_567;

    /// <summary>The S7 signed integer width.</summary>
    private const int IntegerSize = sizeof(int);

    /// <summary>The S7 word width.</summary>
    private const int WordSize = sizeof(ushort);

    /// <summary>The S7 long real width.</summary>
    private const int DoubleSize = sizeof(double);

    /// <summary>An invalid wide-string capacity.</summary>
    private const int WideCapacity = 16_383;

    /// <summary>A valid small wide-string capacity.</summary>
    private const int WideReservedLength = 2;

    /// <summary>An ordinary short string capacity.</summary>
    private const int StringReservedLength = 3;

    /// <summary>The first non-zero bit position.</summary>
    private const int FirstBit = 1;

    /// <summary>The last valid bit position in a byte.</summary>
    private const int LastBit = 7;

    /// <summary>The high byte of the word fixture.</summary>
    private const byte HighByte = 0x12;

    /// <summary>The low byte of the word fixture.</summary>
    private const byte LowByte = 0x34;

    /// <summary>A word conversion fixture value.</summary>
    private const ushort WordValue = 0x1234;

    /// <summary>A double-word conversion fixture value.</summary>
    private const uint DWordValue = 0x12345678U;

    /// <summary>A real conversion fixture value.</summary>
    private const float SingleValue = 1.25F;

    /// <summary>A long-real conversion fixture value.</summary>
    private const double DoubleValue = 1D;

    /// <summary>The expected hundredths timer value.</summary>
    private const double TimerHundredths = 0.12D;

    /// <summary>The expected tenths timer value.</summary>
    private const double TimerTenths = 12.3D;

    /// <summary>The expected seconds timer value.</summary>
    private const double TimerSeconds = 234D;

    /// <summary>The expected tens timer value.</summary>
    private const double TimerTens = 3450D;

    /// <summary>The millisecond fixture.</summary>
    private const int Milliseconds = 1234;

    /// <summary>The invalid S7 string capacity.</summary>
    private const int InvalidS7StringCapacity = 255;

    /// <summary>The ASCII value for the letter A.</summary>
    private const byte CharacterA = 65;

    /// <summary>The ASCII replacement character.</summary>
    private const byte ReplacementCharacter = 63;

    /// <summary>Verifies four-byte signed and floating codecs use their public conversion and guard paths.</summary>
    [Test]
    public void FourByteCodecs_RoundtripOffsetsAndGuards()
    {
        int[] integers = [int.MinValue, IntegerValue, int.MaxValue];
        Assert.That(Plc.DInt.ToArray(Plc.DInt.ToByteArray(integers)), Is.EqualTo(integers));
        Assert.That(Plc.DInt.FromByteArray([0, 0xFF, 0xED, 0x29, 0x79], FirstBit), Is.EqualTo(IntegerValue));
        Assert.That(Plc.DInt.FromBytes(0x79, 0x29, 0xED, 0xFF), Is.EqualTo(IntegerValue));
        Assert.That(Plc.DInt.CDWord(((long)int.MaxValue) + FirstBit), Is.EqualTo(int.MinValue));
        _ = Assert.Throws<ArgumentException>(() => Plc.DInt.FromSpan(new byte[IntegerSize - FirstBit]));
        _ = Assert.Throws<ArgumentException>(() => Plc.DInt.ToSpan(IntegerValue, new byte[IntegerSize - FirstBit]));
        _ = Assert.Throws<ArgumentException>(() => Plc.DInt.ToSpan(integers, new byte[(integers.Length * IntegerSize) - FirstBit]));
        _ = Assert.Throws<ArgumentNullException>(() => Plc.DInt.ToByteArray((int[])null!));

        float[] singles = [-12.5F, 0F, SingleValue];
        Assert.That(Plc.Real.ToArray(Plc.Real.ToByteArray(singles)), Is.EqualTo(singles));
        Assert.That(Plc.Real.FromByteArray(Plc.Real.ToByteArray(SingleValue)), Is.EqualTo(SingleValue));
        _ = Assert.Throws<ArgumentException>(() => Plc.Real.FromSpan(new byte[IntegerSize - FirstBit]));
        _ = Assert.Throws<ArgumentException>(() => Plc.Real.ToSpan(SingleValue, new byte[IntegerSize - FirstBit]));
        _ = Assert.Throws<ArgumentException>(() => Plc.Real.ToSpan(singles, new byte[(singles.Length * IntegerSize) - FirstBit]));
        _ = Assert.Throws<ArgumentNullException>(() => Plc.Real.ToByteArray((float[])null!));
    }

    /// <summary>Verifies the public two- and four-byte word codecs including their error boundaries.</summary>
    [Test]
    public void WordCodecs_RoundtripAndRejectShortBuffers()
    {
        ushort[] words = [0, WordValue, ushort.MaxValue];
        Assert.That(Plc.Word.ToArray(Plc.Word.ToByteArray(words)), Is.EqualTo(words));
        Assert.That(Plc.Word.FromByteArray([0, HighByte, LowByte], FirstBit), Is.EqualTo(WordValue));
        Assert.That(Plc.Word.FromBytes(LowByte, HighByte), Is.EqualTo(WordValue));
        _ = Assert.Throws<IndexOutOfRangeException>(() => Plc.Word.FromSpan(new byte[FirstBit]));
        _ = Assert.Throws<ArgumentException>(() => Plc.Word.ToSpan(WordValue, new byte[FirstBit]));
        _ = Assert.Throws<ArgumentException>(() => Plc.Word.ToSpan(words, new byte[(words.Length * WordSize) - FirstBit]));
        _ = Assert.Throws<ArgumentNullException>(() => Plc.Word.ToByteArray((ushort[])null!));

        uint[] dwords = [0U, DWordValue, uint.MaxValue];
        Assert.That(Plc.DWord.ToArray(Plc.DWord.ToByteArray(dwords)), Is.EqualTo(dwords));
        Assert.That(Plc.DWord.FromBytes(0x78, 0x56, 0x34, 0x12), Is.EqualTo(DWordValue));
        _ = Assert.Throws<ArgumentException>(() => Plc.DWord.FromSpan(new byte[IntegerSize - FirstBit]));
        _ = Assert.Throws<ArgumentException>(() => Plc.DWord.ToSpan(DWordValue, new byte[IntegerSize - FirstBit]));
        _ = Assert.Throws<ArgumentException>(() => Plc.DWord.ToSpan(dwords, new byte[(dwords.Length * IntegerSize) - FirstBit]));
        _ = Assert.Throws<ArgumentNullException>(() => Plc.DWord.ToByteArray((uint[])null!));

        short[] signedWords = [short.MinValue, -9, short.MaxValue];
        Assert.That(Plc.Int.ToArray(Plc.Int.ToByteArray(signedWords)), Is.EqualTo(signedWords));
        Assert.That(Plc.Int.CWord(ushort.MaxValue), Is.EqualTo((short)-1));
        _ = Assert.Throws<ArgumentException>(() => Plc.Int.ToSpan(signedWords, new byte[(signedWords.Length * WordSize) - FirstBit]));
        _ = Assert.Throws<ArgumentNullException>(() => Plc.Int.ToByteArray((short[])null!));

        Assert.That(Plc.Counter.ToArray(Plc.Counter.ToByteArray(words)), Is.EqualTo(words));
        Assert.That(Plc.Counter.FromBytes(LowByte, HighByte), Is.EqualTo(WordValue));
        _ = Assert.Throws<ArgumentException>(() => Plc.Counter.FromSpan(new byte[FirstBit]));
        _ = Assert.Throws<ArgumentException>(() => Plc.Counter.ToSpan(words, new byte[(words.Length * WordSize) - FirstBit]));
    }

    /// <summary>Verifies double, timer, and S7 time codecs through array and invalid-range behavior.</summary>
    [Test]
    public void DoubleTimerAndTimeCodecs_RoundtripAndValidate()
    {
        double[] doubles = [-12.5D, 0D, Math.PI];
        Assert.That(Plc.LReal.ToArray(Plc.LReal.ToByteArray(doubles)), Is.EqualTo(doubles));
        _ = Assert.Throws<ArgumentException>(() => Plc.LReal.FromDWord(0x3F800000U));
        _ = Assert.Throws<ArgumentException>(() => Plc.LReal.FromSpan(new byte[DoubleSize - FirstBit]));
        _ = Assert.Throws<ArgumentException>(() => Plc.LReal.ToSpan(DoubleValue, new byte[DoubleSize - FirstBit]));
        _ = Assert.Throws<ArgumentException>(() => Plc.LReal.ToSpan(doubles, new byte[(doubles.Length * DoubleSize) - FirstBit]));
        _ = Assert.Throws<ArgumentNullException>(() => Plc.LReal.ToByteArray((double[])null!));

        TimeSpan[] spans = [System.TimeSpan.FromMilliseconds(-1), System.TimeSpan.Zero, System.TimeSpan.FromMilliseconds(Milliseconds)];
        Assert.That(Plc.TimeSpan.ToArray(Plc.TimeSpan.ToByteArray(spans)), Is.EqualTo(spans));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Plc.TimeSpan.FromSpan(new byte[IntegerSize - FirstBit]));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Plc.TimeSpan.ToArray(new byte[IntegerSize - FirstBit]));
        _ = Assert.Throws<ArgumentException>(() => Plc.TimeSpan.ToSpan(System.TimeSpan.Zero, new byte[IntegerSize - FirstBit]));
        _ = Assert.Throws<ArgumentException>(() => Plc.TimeSpan.ToSpan(spans, new byte[(spans.Length * IntegerSize) - FirstBit]));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Plc.TimeSpan.ToByteArray(Plc.TimeSpan.SpecMinimumTimeSpan - System.TimeSpan.FromMilliseconds(FirstBit)));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Plc.TimeSpan.ToByteArray(Plc.TimeSpan.SpecMaximumTimeSpan + System.TimeSpan.FromMilliseconds(FirstBit)));

        ushort[] timers = [0x0012, 0x1123, 0x2234, 0x3345];
        Assert.That(Plc.Timer.ToArray(Plc.Timer.ToByteArray(timers)), Is.EqualTo(new[] { TimerHundredths, TimerTenths, TimerSeconds, TimerTens }));
        _ = Assert.Throws<ArgumentException>(() => Plc.Timer.FromByteArray(new byte[FirstBit], 0));
        _ = Assert.Throws<ArgumentException>(() => Plc.Timer.ToSpan(timers, new byte[(timers.Length * WordSize) - FirstBit]));
        _ = Assert.Throws<ArgumentNullException>(() => Plc.Timer.ToByteArray((ushort[])null!));
    }

    /// <summary>Verifies string codecs and pooled byte/bit utilities at normal and failure boundaries.</summary>
    [Test]
    public void StringsByteArrayAndBits_UsePublicBoundaryBehavior()
    {
        Assert.That(Plc.String.FromByteArray(null!, 0, FirstBit), Is.EqualTo(string.Empty));
        Assert.That(Plc.String.FromByteArray([CharacterA], FirstBit, FirstBit), Is.EqualTo(string.Empty));
        Assert.That(Plc.String.ToByteArray("AΩ"), Is.EqualTo(new byte[] { CharacterA, ReplacementCharacter }));
        _ = Assert.Throws<ArgumentException>(() => Plc.String.ToSpan("ABC", new byte[WordSize]));
        Assert.That(Plc.String.TryToSpan("ABC", new byte[WordSize], out var failedBytes), Is.False);
        Assert.That(failedBytes, Is.EqualTo(0));

        var previousEncoding = Plc.S7String.StringEncoding;
        try
        {
            Plc.S7String.StringEncoding = Encoding.UTF8;
            var s7Bytes = Plc.S7String.ToByteArray("é", WideReservedLength);
            Assert.That(Plc.S7String.FromByteArray(s7Bytes), Is.EqualTo("é"));
            Assert.That(Plc.S7String.TryToSpan("ABC", WideReservedLength, new byte[IntegerSize], out _), Is.False);
            _ = Assert.Throws<ArgumentException>(() => Plc.S7String.ToSpan("A", InvalidS7StringCapacity, new byte[InvalidS7StringCapacity + FirstBit]));
            _ = Assert.Throws<Exception>(() => Plc.S7String.FromByteArray([WideReservedLength, StringReservedLength]));
        }
        finally
        {
            Plc.S7String.StringEncoding = previousEncoding;
        }

        Assert.That(Plc.S7WString.FromByteArray(Plc.S7WString.ToByteArray("Ω", WideReservedLength)), Is.EqualTo("Ω"));
        _ = Assert.Throws<ArgumentException>(() => Plc.S7WString.ToByteArray("A", WideCapacity));
        _ = Assert.Throws<Exception>(() => Plc.S7WString.FromByteArray([0, FirstBit, 0, WideReservedLength]));

        using var source = new Plc.ByteArray(FirstBit);
        source.Add(FirstBit);
        source.Add([WordSize, StringReservedLength, IntegerSize]);
        using var combined = new Plc.ByteArray();
        combined.Add(source);
        Assert.That(combined.TryCopyTo(new byte[IntegerSize]), Is.True);
        Assert.That(combined.TryCopyTo(new byte[StringReservedLength]), Is.False);
        _ = Assert.Throws<ArgumentNullException>(() => combined.Add((Plc.ByteArray)null!));

        var bytes = new byte[WordSize];
        Plc.Bit.SetBits(bytes, [(0, FirstBit, true), (FirstBit, LastBit, true)]);
        Assert.That(Plc.Bit.GetBits(bytes, [(0, FirstBit), (FirstBit, LastBit)]), Is.EqualTo(new[] { true, true }));
        Assert.That(Plc.Bit.ToBitArray([(byte)StringReservedLength], WordSize)[0], Is.True);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Plc.Bit.SetBit(bytes, 0, WordSize << StringReservedLength, true));
        _ = Assert.Throws<ArgumentException>(() => Plc.Bit.ToBitArray([], 0));
        _ = Assert.Throws<ArgumentException>(() => Plc.Bit.ToBitArray([FirstBit], LastBit + WordSize));
    }
}
