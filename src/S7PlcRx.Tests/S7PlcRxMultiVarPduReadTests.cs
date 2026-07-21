// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using MockS7Plc;
using S7PlcRx.Advanced;
using S7PlcRx.Enums;

namespace S7PlcRx.Tests;

/// <summary>Tests for multi-variable PDU read batching.</summary>
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public class S7PlcRxMultiVarPduReadTests
{
    /// <summary>First word value in the read batch.</summary>
    private const ushort FirstValue = 10;

    /// <summary>Second word value in the read batch.</summary>
    private const ushort SecondValue = 20;

    /// <summary>Third word value in the read batch.</summary>
    private const ushort ThirdValue = 30;

    /// <summary>Gets the compact representation displayed by the debugger.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay
    {
        get => ToString() ?? GetType().Name;
    }

    /// <summary>Ensures `ValueBatch` returns values for multiple tags.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [NotInParallel]
    public async Task ValueBatch_ShouldReadMultipleTagsInOneCallAsync()
    {
        await TUnit.Assertions.Assert.That(DebuggerDisplay).IsNotNull();
        using var server = new MockServer();
        _ = server.Start();

        using var plc = new RxS7(new(new(CpuType.S71500, MockServer.Localhost, 0, 1), new(1)));

        _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), "T0", "DB1.DBW0").SetPolling(false);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), "T1", "DB1.DBW2").SetPolling(false);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), "T2", "DB1.DBW4").SetPolling(false);

        await plc.IsConnected.Where(x => x).FirstAsync();

        plc.Value("T0", FirstValue);
        plc.Value("T1", SecondValue);
        plc.Value("T2", ThirdValue);

        var values = await AdvancedExtensions.ValueBatchAsync(plc, default(ushort), "T0", "T1", "T2");

        await TUnit.Assertions.Assert.That(values["T0"]).IsEqualTo(FirstValue);
        await TUnit.Assertions.Assert.That(values["T1"]).IsEqualTo(SecondValue);
        await TUnit.Assertions.Assert.That(values["T2"]).IsEqualTo(ThirdValue);
    }
}
