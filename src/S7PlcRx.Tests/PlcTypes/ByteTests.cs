// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using Byte = S7PlcRx.PlcTypes.Byte;

namespace S7PlcRx.Tests.PlcTypes;

/// <summary>Tests the byte PlcType helpers.</summary>
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public class ByteTests
{
    /// <summary>Gets a debugger-friendly test description.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => ToString() ?? string.Empty;

    /// <summary>Ensures byte conversion roundtrips.</summary>
    [Test]
    public void ToByteArray_ThenFromByteArray_ShouldRoundtrip()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        var bytes = Byte.ToByteArray(0xAB);
        var parsed = Byte.FromByteArray(bytes);
        Assert.That(parsed, Is.EqualTo(0xAB));
    }

    /// <summary>Ensures span write guard is enforced.</summary>
    [Test]
    public void ToSpan_WhenDestinationTooSmall_ShouldThrow()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        var dest = Array.Empty<byte>();
        _ = Assert.Throws<ArgumentException>(() => Byte.ToSpan(0x01, dest));
    }

    /// <summary>Ensures span read guard is enforced.</summary>
    [Test]
    public void FromSpan_WhenEmpty_ShouldThrow()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        _ = Assert.Throws<ArgumentException>(() => Byte.FromSpan(ReadOnlySpan<byte>.Empty));
    }

    /// <summary>Ensures multi-byte copy works.</summary>
    [Test]
    public void ToSpan_WhenCopyingMultipleBytes_ShouldCopy()
    {
        byte[] src = [1, 2, 3];
        Span<byte> dest = stackalloc byte[3];
        Byte.ToSpan(src, dest);
        Assert.That(dest.ToArray(), Is.EqualTo(src));
    }

    /// <summary>Ensures multi-byte copy guard is enforced.</summary>
    [Test]
    public void ToSpan_WhenDestinationTooSmallForMultiple_ShouldThrow()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        byte[] src = [1, 2, 3];
        var dest = new byte[2];
        _ = Assert.Throws<ArgumentException>(() => Byte.ToSpan(src, dest));
    }
}
