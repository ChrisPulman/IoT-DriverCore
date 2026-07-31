// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Boolean = IoT.Driver.S7PlcRx.PlcTypes.Boolean;

namespace IoT.Driver.S7PlcRx.Tests.PlcTypes;

/// <summary>Tests Boolean PlcType helpers.</summary>
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public class BooleanTests
{
    /// <summary>The first bit index.</summary>
    private const byte FirstBitIndex = 2;

    /// <summary>The second bit index.</summary>
    private const byte SecondBitIndex = 4;

    /// <summary>The third bit index.</summary>
    private const byte ThirdBitIndex = 7;

    /// <summary>The fourth bit index.</summary>
    private const byte FourthBitIndex = 3;

    /// <summary>Gets a debugger-friendly test description.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => ToString() ?? string.Empty;

    /// <summary>Ensures GetValue reads the selected bit.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetValue_ShouldReturnExpected()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        await Assert.That(Boolean.GetValue(0b0000_0010, 1), Is.True);
        await Assert.That(Boolean.GetValue(0b0000_0010, 0), Is.False);
    }

    /// <summary>Ensures SetBit sets the specified bit.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SetBit_ShouldSetBit()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        var value = Boolean.SetBit(0, FirstBitIndex);
        await Assert.That(value, Is.EqualTo(0b0000_0100));
    }

    /// <summary>Ensures SetBit by ref mutates the input value.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SetBit_ByRef_ShouldMutate()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        byte value = 0;
        Boolean.SetBit(ref value, SecondBitIndex);
        await Assert.That(value, Is.EqualTo(0b0001_0000));
    }

    /// <summary>Ensures ClearBit clears the specified bit.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClearBit_ShouldClearBit()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        var value = Boolean.ClearBit(0b1111_1111, ThirdBitIndex);
        await Assert.That(value, Is.EqualTo(0b0111_1111));
    }

    /// <summary>Ensures ClearBit by ref mutates the input value.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClearBit_ByRef_ShouldMutate()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        byte value = 0b0000_1000;
        Boolean.ClearBit(ref value, FourthBitIndex);
        await Assert.That(value, Is.EqualTo(0));
    }
}
