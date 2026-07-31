// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using TUnit.Assertions;
using TUnit.Core;

namespace IoT.Driver.ABPlcRx.Tests;

/// <summary>Provides deterministic native-operation acceptance coverage for Allen-Bradley bulk transfers.</summary>
public sealed class ABPlcDeterministicPerformanceAcceptanceTests
{
    /// <summary>The count of logical values transferred by each bulk operation.</summary>
    private const int LogicalValueCount = 4;

    /// <summary>The first value written through the bulk pipeline.</summary>
    private const int FirstValue = 10;

    /// <summary>The second value written through the bulk pipeline.</summary>
    private const int SecondValue = 20;

    /// <summary>The third value written through the bulk pipeline.</summary>
    private const int ThirdValue = 30;

    /// <summary>The fourth value written through the bulk pipeline.</summary>
    private const int FourthValue = 40;

    /// <summary>The per-operation status that denotes native success.</summary>
    private static readonly int SuccessStatus = PlcTagStatus.StatusOK;

    /// <summary>Verifies a bulk transfer uses exactly one native operation per logical item, without timing thresholds.</summary>
    /// <returns>A task representing the asynchronous acceptance test.</returns>
    [Test]
    public async Task BulkTransfers_ReportExactNativeOperationCountsAsync()
    {
        using var simulator = new ABPlcSimulator(PlcType.SLC);
        simulator.ScanEnabled = false;
        simulator.AutoWriteValue = false;
        var variables = new[] { "First", "Second", "Third", "Fourth" };
        var physicalTags = new[] { "N7:0", "N7:1", "N7:2", "N7:3" };
        for (var index = 0; index < LogicalValueCount; index++)
        {
            simulator.AddUpdateTagItem<int>(variables[index], physicalTags[index], default);
        }

        simulator.ClearOperationLog();
        var writes = await simulator.WriteManyAsync(
            new Dictionary<string, object?>
            {
                [variables[0]] = FirstValue,
                [variables[1]] = SecondValue,
                [variables[2]] = ThirdValue,
                [variables[3]] = FourthValue,
            },
            CancellationToken.None);
        var writeMetrics = simulator.OperationMetrics;
        var writeLog = simulator.OperationLog;

        await Assert.That(writes.Count).IsEqualTo(LogicalValueCount);
        await Assert.That(writeMetrics.TotalOperations).IsEqualTo((long)LogicalValueCount);
        await Assert.That(writeMetrics.WriteOperations).IsEqualTo((long)LogicalValueCount);
        await Assert.That(writeMetrics.ReadOperations).IsEqualTo(0L);
        await Assert.That(writeMetrics.CreateOperations).IsEqualTo(0L);
        await Assert.That(writeMetrics.FailedOperations).IsEqualTo(0L);
        await Assert.That(writeLog.Count).IsEqualTo(LogicalValueCount);
        for (var index = 0; index < LogicalValueCount; index++)
        {
            await Assert.That(writes[index].StatusCode).IsEqualTo(SuccessStatus);
            await Assert.That(writeLog[index].Sequence).IsEqualTo(index + 1L);
            await Assert.That(writeLog[index].Operation).IsEqualTo(ABPlcSimulatorOperation.Write);
            await Assert.That(writeLog[index].TagName ?? string.Empty).IsEqualTo(physicalTags[index]);
        }

        simulator.ClearOperationLog();
        var reads = await simulator.ReadManyAsync(variables, CancellationToken.None);
        var readMetrics = simulator.OperationMetrics;
        var readLog = simulator.OperationLog;

        await Assert.That(reads.Count).IsEqualTo(LogicalValueCount);
        await Assert.That(readMetrics.TotalOperations).IsEqualTo((long)LogicalValueCount);
        await Assert.That(readMetrics.ReadOperations).IsEqualTo((long)LogicalValueCount);
        await Assert.That(readMetrics.WriteOperations).IsEqualTo(0L);
        await Assert.That(readMetrics.CreateOperations).IsEqualTo(0L);
        await Assert.That(readMetrics.FailedOperations).IsEqualTo(0L);
        await Assert.That(readLog.Count).IsEqualTo(LogicalValueCount);
        for (var index = 0; index < LogicalValueCount; index++)
        {
            await Assert.That(reads[index].StatusCode).IsEqualTo(SuccessStatus);
            await Assert.That(readLog[index].Sequence).IsEqualTo(index + 1L);
            await Assert.That(readLog[index].Operation).IsEqualTo(ABPlcSimulatorOperation.Read);
            await Assert.That(readLog[index].TagName ?? string.Empty).IsEqualTo(physicalTags[index]);
        }
    }
}
