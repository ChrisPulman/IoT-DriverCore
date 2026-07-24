// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.S7PlcRx.Advanced;
using IoT.DriverCore.S7PlcRx.Enums;
using IoT.DriverCore.S7PlcRx.Mock;

namespace IoT.DriverCore.S7PlcRx.Tests;

/// <summary>Tests for multi-variable PDU write batching.</summary>
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public class S7PlcRxMultiVarPduWriteTests
{
    /// <summary>First word value in the write batch.</summary>
    private const ushort FirstValue = 10;

    /// <summary>Second word value in the write batch.</summary>
    private const ushort SecondValue = 20;

    /// <summary>Third word value in the write batch.</summary>
    private const ushort ThirdValue = 30;

    /// <summary>Maximum time allowed for the simulated PLC connection retry policy.</summary>
    private const int ConnectionTimeoutSeconds = 60;

    /// <summary>Gets the compact representation displayed by the debugger.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay
    {
        get => ToString() ?? GetType().Name;
    }

    /// <summary>Ensures `ValueBatch(Dictionary)` can write multiple tags.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [NotInParallel]
    public async Task ValueBatch_ShouldWriteMultipleTagsInOneCallAsync()
    {
        await TUnit.Assertions.Assert.That(DebuggerDisplay).IsNotNull();
        using var server = new MockServer();
        _ = server.Start();

        using var plc = new RxS7(new(new(CpuType.S71500, MockServer.Localhost, 0, 1), new(1)));

        _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), "W0", "DB1.DBW0").SetPolling(false);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), "W1", "DB1.DBW2").SetPolling(false);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), "W2", "DB1.DBW4").SetPolling(false);

        await plc.IsConnected
            .Where(static connected => connected)
            .Timeout(TimeSpan.FromSeconds(ConnectionTimeoutSeconds))
            .FirstAsync();

        await AdvancedExtensions.ValueBatchAsync(plc, new Dictionary<string, ushort>
        {
            ["W0"] = FirstValue,
            ["W1"] = SecondValue,
            ["W2"] = ThirdValue,
        });

        var values = await AdvancedExtensions.ValueBatchAsync(plc, default(ushort), "W0", "W1", "W2");

        await TUnit.Assertions.Assert.That(values["W0"]).IsEqualTo(FirstValue);
        await TUnit.Assertions.Assert.That(values["W1"]).IsEqualTo(SecondValue);
        await TUnit.Assertions.Assert.That(values["W2"]).IsEqualTo(ThirdValue);
    }
}
