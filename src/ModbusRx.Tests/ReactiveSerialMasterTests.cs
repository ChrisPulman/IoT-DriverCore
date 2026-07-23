// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO.Ports;
using System.Threading.Tasks;

namespace IoT.DriverCore.ModbusRx.UnitTests;

/// <summary>Tests for reactive serial masters (RTU/ASCII) in Create.</summary>
public class ReactiveSerialMasterTests
{
    /// <summary>The original connection interval restored after each fixture instance.</summary>
    private readonly TimeSpan _origInterval;

    /// <summary>Initializes a new instance of the <see cref="ReactiveSerialMasterTests"/> class.</summary>
    public ReactiveSerialMasterTests()
    {
        _origInterval = Create.CheckConnectionInterval;
        Create.CheckConnectionInterval = TimeSpan.FromMilliseconds(Num.Value50); // speed up tests
    }

    /// <summary>Finalizes an instance of the <see cref="ReactiveSerialMasterTests"/> class.</summary>
    /// <remarks>Restores the original connection interval.</remarks>
    ~ReactiveSerialMasterTests()
    {
        Create.CheckConnectionInterval = _origInterval;
    }

    /// <summary>Verifies that the reactive RTU master stream emits a status tuple upon subscription.</summary>
    /// <returns>A task.</returns>
    [TUnit.Core.Test]
    public async Task SerialRtuMaster_Subscribe_ShouldEmitStatusAsync()
    {
        // Arrange
        var emitted = false;

        // Act
        using var sub = Create.SerialRtuMaster(
                "COM_DOES_NOT_EXIST",
                Num.Value1200,
                Num.Value8,
                Parity.None,
                StopBits.One,
                Handshake.None)
            .Take(1)
            .Timeout(TimeSpan.FromSeconds(Num.Value2))
            .Subscribe(_ => emitted = true);

        await Task.Delay(Num.Value200);

        // Assert
        Assert.True(emitted);
    }

    /// <summary>Verifies that the reactive ASCII master stream emits a status tuple upon subscription.</summary>
    /// <returns>A task.</returns>
    [TUnit.Core.Test]
    public async Task SerialAsciiMaster_Subscribe_ShouldEmitStatusAsync()
    {
        // Arrange
        var emitted = false;

        // Act
        using var sub = Create.SerialAsciiMaster(
                "COM_DOES_NOT_EXIST",
                Num.Value1200,
                Num.Value8,
                Parity.None,
                StopBits.One,
                Handshake.None)
            .Take(1)
            .Timeout(TimeSpan.FromSeconds(Num.Value2))
            .Subscribe(_ => emitted = true);

        await Task.Delay(Num.Value200);

        // Assert
        Assert.True(emitted);
    }
}
