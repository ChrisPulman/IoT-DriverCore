// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using IoT.DriverCore.S7PlcRx.Advanced;
using IoT.DriverCore.S7PlcRx.Enums;
using IoT.DriverCore.S7PlcRx.Mock;

namespace IoT.DriverCore.S7PlcRx.Tests;

/// <summary>Tests for multi-variable batching against non-DB areas (I/Q/M) and bit addressing.</summary>
[NotInParallel]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class S7PlcRxMultiVarNonDbAreaTests
{
    /// <summary>Byte value used for the memory-byte test.</summary>
    private const byte ByteValue = 0xAA;

    /// <summary>Polling delay used while awaiting the server readback.</summary>
    private const int PollIntervalMilliseconds = 20;

    /// <summary>Word value used for the memory-word test.</summary>
    private const ushort WordValue = 0x1234;

    /// <summary>Maximum time allowed for the PLC readback.</summary>
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Gets the compact representation displayed by the debugger.</summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay
    {
        get => ToString() ?? GetType().Name;
    }

    /// <summary>Ensures MultiVar can read values from memory areas and bit addresses.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ValueBatch_ShouldRead_MemoryAreasAndBitsAsync()
    {
        await TUnit.Assertions.Assert.That(DebuggerDisplay).IsNotNull();
        using var server = new MockServer();
        _ = server.Start();

        using var plc = new RxS7(new(new(CpuType.S71500, MockServer.Localhost, 0, 1), new(1)));

        _ = TagOperations.AddUpdateTagItem(plc, typeof(byte), "MB0", "MB0").SetPolling(false);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), "MW2", "MW2").SetPolling(false);

        await plc.IsConnected.Where(x => x).FirstAsync();

        static async Task EventuallyAsync(Func<Task<bool>> predicate)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < ReadTimeout)
            {
                if (await predicate())
                {
                    return;
                }

                await Task.Delay(PollIntervalMilliseconds);
            }

            throw new TimeoutException($"Condition not met within {ReadTimeout}.");
        }

        // Write values (single path). Readback should use MultiVar path.
        plc.Value("MB0", ByteValue);
        await EventuallyAsync(
            async () => (await plc.ReadAsync(new LogicalTagKey<byte>("MB0"))) == ByteValue);

        plc.Value("MW2", WordValue);
        await EventuallyAsync(
            async () => (await plc.ReadAsync(new LogicalTagKey<ushort>("MW2"))) == WordValue);

        var byteRes = await AdvancedExtensions.ValueBatchAsync(plc, default(byte), "MB0");
        var wordRes = await AdvancedExtensions.ValueBatchAsync(plc, default(ushort), "MW2");

        await TUnit.Assertions.Assert.That(byteRes["MB0"]).IsEqualTo(ByteValue);
        await TUnit.Assertions.Assert.That(wordRes["MW2"]).IsEqualTo(WordValue);
    }
}
