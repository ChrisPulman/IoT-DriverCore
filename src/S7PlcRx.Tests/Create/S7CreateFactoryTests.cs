// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.S7PlcRx.Tests.Create;

/// <summary>Tests for PLC factory helpers in the `S7PlcRx.Create` namespace.</summary>
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public class S7CreateFactoryTests
{
    /// <summary>Gets the local endpoint used by the factory tests.</summary>
    private const string LoopbackAddress = "127.0.0.1";

    /// <summary>Gets the debugger display text.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay
    {
        get => ToString() ?? string.Empty;
    }

    /// <summary>Validates `S71200.Create` rejects invalid rack values.</summary>
    [Test]
    public void S71200Create_WhenRackOutOfRange_ShouldThrow()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => S71200.Create(LoopbackAddress, rack: -1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => S71200.Create(LoopbackAddress, rack: 8));
    }

    /// <summary>Validates `S7300.Create` rejects invalid rack values.</summary>
    [Test]
    public void S7300Create_WhenRackOutOfRange_ShouldThrow()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => S7300.Create(LoopbackAddress, rack: -1, slot: 2));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => S7300.Create(LoopbackAddress, rack: 8, slot: 2));
    }

    /// <summary>Validates `S7300.Create` rejects invalid slot values.</summary>
    [Test]
    public void S7300Create_WhenSlotOutOfRange_ShouldThrow()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => S7300.Create(LoopbackAddress, rack: 0, slot: 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => S7300.Create(LoopbackAddress, rack: 0, slot: 32));
    }

    /// <summary>Validates `S7400.Create` rejects invalid rack values.</summary>
    [Test]
    public void S7400Create_WhenRackOutOfRange_ShouldThrow()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => S7400.Create(LoopbackAddress, rack: -1, slot: 2));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => S7400.Create(LoopbackAddress, rack: 8, slot: 2));
    }

    /// <summary>Validates `S7400.Create` rejects invalid slot values.</summary>
    [Test]
    public void S7400Create_WhenSlotOutOfRange_ShouldThrow()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => S7400.Create(LoopbackAddress, rack: 0, slot: 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => S7400.Create(LoopbackAddress, rack: 0, slot: 32));
    }

    /// <summary>Smoke test ensuring `S7200.Create` returns an instance.</summary>
    [Test]
    public void S7200Create_ShouldReturnInstance()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        using var plc = S7200.Create(LoopbackAddress, rack: 0, slot: 2);
        Assert.That(plc, Is.Not.Null);
    }
}
