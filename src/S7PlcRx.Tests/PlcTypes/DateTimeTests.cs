// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using SystemDateTime = System.DateTime;
using SystemDateTimeOffset = System.DateTimeOffset;

namespace IoT.Driver.S7PlcRx.Tests.PlcTypes;

/// <summary>Tests the S7 DateTime PlcType.</summary>
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public class DateTimeTests
{
    /// <summary>Gets a debugger-friendly test description.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => ToString() ?? string.Empty;

    /// <summary>Ensures DateTime byte conversion roundtrips.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToByteArray_ThenFromByteArray_ShouldRoundtrip()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);

        // An S7 DATE_AND_TIME field stores calendar components, not a timezone offset.
        // The public API represents the decoded field at its specified UTC offset.
        var value = new SystemDateTimeOffset(2024, 12, 31, 23, 59, 58, 123, System.TimeSpan.Zero);
        var bytes = S7PlcRx.PlcTypes.DateTime.ToByteArray(value);
        var parsed = S7PlcRx.PlcTypes.DateTime.FromByteArray(bytes);
        await Assert.That(parsed, Is.EqualTo(value));
    }

    /// <summary>Ensures FromSpan validates required length.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FromSpan_WhenTooShort_ShouldThrow()
    {
        _ = await Assert.Throws<ArgumentOutOfRangeException>(
            static () => S7PlcRx.PlcTypes.DateTime.FromSpan(stackalloc byte[7]));
    }

    /// <summary>Ensures ToSpan validates destination capacity.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToSpan_WhenDestinationTooSmall_ShouldThrow()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        var dest = new byte[7];
        _ = await Assert.Throws<ArgumentException>(
            () => S7PlcRx.PlcTypes.DateTime.ToSpan(new SystemDateTime(2024, 1, 1), dest));
    }

    /// <summary>Ensures ToSpan enforces spec minimum.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToSpan_WhenBeforeSpecMinimum_ShouldThrow()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        var dt = S7PlcRx.PlcTypes.DateTime.SpecMinimumDateTime.AddMilliseconds(-1);
        var dest = new byte[8];
        _ = await Assert.Throws<ArgumentOutOfRangeException>(() => S7PlcRx.PlcTypes.DateTime.ToSpan(dt, dest));
    }

    /// <summary>Ensures ToSpan enforces spec maximum.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToSpan_WhenAfterSpecMaximum_ShouldThrow()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        var dt = S7PlcRx.PlcTypes.DateTime.SpecMaximumDateTime.AddMilliseconds(1);
        var dest = new byte[8];
        _ = await Assert.Throws<ArgumentOutOfRangeException>(() => S7PlcRx.PlcTypes.DateTime.ToSpan(dt, dest));
    }

    /// <summary>Ensures multiple DateTimes can roundtrip.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToArray_ThenToByteArray_ShouldRoundtripMultiple()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        SystemDateTimeOffset[] values =
        {
            new SystemDateTimeOffset(2020, 1, 1, 0, 0, 0, System.TimeSpan.Zero),
            new SystemDateTimeOffset(2025, 6, 30, 12, 34, 56, 789, System.TimeSpan.Zero),
        };

        var bytes = S7PlcRx.PlcTypes.DateTime.ToByteArray(values);
        var parsed = S7PlcRx.PlcTypes.DateTime.ToArray(bytes);
        await Assert.That(parsed, Is.EqualTo(values));
    }

    /// <summary>Ensures ToArray validates buffer length alignment.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToArray_WhenNotMultipleOf8_ShouldThrow()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        _ = await Assert.Throws<ArgumentOutOfRangeException>(
            static () => S7PlcRx.PlcTypes.DateTime.ToArray(new byte[9]));
    }
}
