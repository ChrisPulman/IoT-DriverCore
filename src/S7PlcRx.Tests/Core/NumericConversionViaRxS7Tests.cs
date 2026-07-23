// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using IoT.DriverCore.S7PlcRx.Enums;

namespace IoT.DriverCore.S7PlcRx.Tests.Core;

/// <summary>Verifies numeric decoding performed by RxS7 internal VarType parsing.</summary>
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public class NumericConversionViaRxS7Tests
{
    /// <summary>Gets the debugger display text.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay
    {
        get => ToString() ?? string.Empty;
    }

    /// <summary>Ensures internal VarType-to-bytes parsing uses the expected big-endian conventions.</summary>
    [Test]
    public void ParseBytes_ShouldDecodeWordDWordDInt()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        using var plc = new RxS7(new(new(CpuType.S7200, "127.0.0.1", 0, 0)));

        var parseBytes = typeof(RxS7).GetMethod("ParseBytes", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("RxS7.ParseBytes was not found.");

        // Word (ushort): 0x1234
        var word = (ushort)(parseBytes.Invoke(plc, [VarType.Word, (byte[])[0x12, 0x34], 1, typeof(ushort)])
            ?? throw new InvalidOperationException("RxS7.ParseBytes returned null for Word."));
        Assert.That(word, Is.EqualTo(0x1234));

        // DWord (uint): 0x01020304
        var dword = (uint)(parseBytes.Invoke(plc, [VarType.DWord, (byte[])[0x01, 0x02, 0x03, 0x04], 1, typeof(uint)])
            ?? throw new InvalidOperationException("RxS7.ParseBytes returned null for DWord."));
        Assert.That(dword, Is.EqualTo(0x01020304U));

        // DInt (int): -1 => 0xFFFFFFFF
        var dint = (int)(parseBytes.Invoke(plc, [VarType.DInt, (byte[])[0xFF, 0xFF, 0xFF, 0xFF], 1, typeof(int)])
            ?? throw new InvalidOperationException("RxS7.ParseBytes returned null for DInt."));
        Assert.That(dint, Is.EqualTo(-1));
    }

    /// <summary>Ensures internal floating parsing roundtrips using S7 big-endian format.</summary>
    [Test]
    public void ParseBytes_ShouldDecodeRealAndLReal()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        using var plc = new RxS7(new(new(CpuType.S7200, "127.0.0.1", 0, 0)));

        var parseBytes = typeof(RxS7).GetMethod("ParseBytes", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("RxS7.ParseBytes was not found.");

        // float 1.0f => 0x3F800000 (big-endian bytes)
        var real = (float)(parseBytes.Invoke(plc, [VarType.Real, (byte[])[0x3F, 0x80, 0x00, 0x00], 1, typeof(float)])
            ?? throw new InvalidOperationException("RxS7.ParseBytes returned null for Real."));
        Assert.That(real, Is.EqualTo(1.0F));

        // double 1.0 => 0x3FF0000000000000 (big-endian bytes)
        var lreal = (double)(parseBytes.Invoke(
            plc,
            [VarType.LReal, (byte[])[0x3F, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00], 1, typeof(double)])
            ?? throw new InvalidOperationException("RxS7.ParseBytes returned null for LReal."));
        Assert.That(lreal, Is.EqualTo(1.0D));
    }
}
