// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace IoT.Driver.S7PlcRx.Tests.PlcTypes;

/// <summary>Tests the S7 TimeSpan (TIME) PlcType.</summary>
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public class TimeSpanTests
{
    /// <summary>The milliseconds used for a single value roundtrip.</summary>
    private const int RoundtripMilliseconds = 123_456;

    /// <summary>The milliseconds used for an array roundtrip.</summary>
    private const int ArrayRoundtripMilliseconds = 3_456;

    /// <summary>Gets a debugger-friendly test description.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => ToString() ?? string.Empty;

    /// <summary>Ensures TimeSpan byte conversion roundtrips.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToByteArray_ThenFromByteArray_ShouldRoundtrip()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        var value = TimeSpan.FromMilliseconds(RoundtripMilliseconds);
        var bytes = S7PlcRx.PlcTypes.TimeSpan.ToByteArray(value);
        var parsed = S7PlcRx.PlcTypes.TimeSpan.FromByteArray(bytes);
        await Assert.That(parsed, Is.EqualTo(value));
    }

    /// <summary>Ensures FromSpan validates required length.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FromSpan_WhenTooShort_ShouldThrow()
    {
        _ = await Assert.Throws<ArgumentOutOfRangeException>(static () => S7PlcRx.PlcTypes.TimeSpan.FromSpan(stackalloc byte[3]));
    }

    /// <summary>Ensures ToSpan validates destination capacity.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToSpan_WhenDestinationTooSmall_ShouldThrow()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        var dest = new byte[3];
        _ = await Assert.Throws<ArgumentException>(() => S7PlcRx.PlcTypes.TimeSpan.ToSpan(System.TimeSpan.Zero, dest));
    }

    /// <summary>Ensures ToSpan enforces spec minimum.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToSpan_WhenBeforeSpecMinimum_ShouldThrow()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        var ts = S7PlcRx.PlcTypes.TimeSpan.SpecMinimumTimeSpan - TimeSpan.FromMilliseconds(1);
        var dest = new byte[4];
        _ = await Assert.Throws<ArgumentOutOfRangeException>(() => S7PlcRx.PlcTypes.TimeSpan.ToSpan(ts, dest));
    }

    /// <summary>Ensures ToSpan enforces spec maximum.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToSpan_WhenAfterSpecMaximum_ShouldThrow()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        var ts = S7PlcRx.PlcTypes.TimeSpan.SpecMaximumTimeSpan + TimeSpan.FromMilliseconds(1);
        var dest = new byte[4];
        _ = await Assert.Throws<ArgumentOutOfRangeException>(() => S7PlcRx.PlcTypes.TimeSpan.ToSpan(ts, dest));
    }

    /// <summary>Ensures multiple TimeSpans can roundtrip.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToArray_ThenToByteArray_ShouldRoundtripMultiple()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        var values = new[]
        {
            TimeSpan.FromMilliseconds(-1),
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(ArrayRoundtripMilliseconds),
        };

        var bytes = S7PlcRx.PlcTypes.TimeSpan.ToByteArray(values);
        var parsed = S7PlcRx.PlcTypes.TimeSpan.ToArray(bytes);
        await Assert.That(parsed, Is.EqualTo(values));
    }

    /// <summary>Ensures ToArray validates buffer length alignment.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToArray_WhenNotMultipleOf4_ShouldThrow()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        _ = await Assert.Throws<ArgumentOutOfRangeException>(static () => S7PlcRx.PlcTypes.TimeSpan.ToArray(new byte[5]));
    }

    /// <summary>Ensures ToByteArray validates null input.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToByteArray_WhenNullArray_ShouldThrow()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        _ = await Assert.Throws<ArgumentNullException>(static () => S7PlcRx.PlcTypes.TimeSpan.ToByteArray((TimeSpan[])null!));
    }
}
