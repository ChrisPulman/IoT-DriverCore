// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using TUnit.Assertions;
using TUnit.Core;

namespace IoT.DriverCore.ABPlcRx.Tests;

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

    /// <summary>The first native operation sequence value.</summary>
    private const long FirstSequence = 1L;

    /// <summary>The second native operation sequence value.</summary>
    private const long SecondSequence = 2L;

    /// <summary>The third native operation sequence value.</summary>
    private const long ThirdSequence = 3L;

    /// <summary>The fourth native operation sequence value.</summary>
    private const long FourthSequence = 4L;

    /// <summary>The expected sequence after a cleared operation log.</summary>
    private static readonly long[] ExpectedSequences = [FirstSequence, SecondSequence, ThirdSequence, FourthSequence];

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
        await Assert.That(writes.All(static result => result.StatusCode == SuccessStatus)).IsTrue();
        await Assert.That(writeMetrics.TotalOperations).IsEqualTo((long)LogicalValueCount);
        await Assert.That(writeMetrics.WriteOperations).IsEqualTo((long)LogicalValueCount);
        await Assert.That(writeMetrics.ReadOperations).IsEqualTo(0L);
        await Assert.That(writeMetrics.CreateOperations).IsEqualTo(0L);
        await Assert.That(writeMetrics.FailedOperations).IsEqualTo(0L);
        await Assert.That(writeLog.Select(static entry => entry.Sequence))
            .IsEquivalentTo(ExpectedSequences);
        await Assert.That(writeLog.Select(static entry => entry.Operation))
            .IsEquivalentTo(Enumerable.Repeat(ABPlcSimulatorOperation.Write, LogicalValueCount));
        await Assert.That(writeLog.Select(static entry => entry.TagName ?? string.Empty))
            .IsEquivalentTo(physicalTags);

        simulator.ClearOperationLog();
        var reads = await simulator.ReadManyAsync(variables, CancellationToken.None);
        var readMetrics = simulator.OperationMetrics;
        var readLog = simulator.OperationLog;

        await Assert.That(reads.Count).IsEqualTo(LogicalValueCount);
        await Assert.That(reads.All(static result => result.StatusCode == SuccessStatus)).IsTrue();
        await Assert.That(readMetrics.TotalOperations).IsEqualTo((long)LogicalValueCount);
        await Assert.That(readMetrics.ReadOperations).IsEqualTo((long)LogicalValueCount);
        await Assert.That(readMetrics.WriteOperations).IsEqualTo(0L);
        await Assert.That(readMetrics.CreateOperations).IsEqualTo(0L);
        await Assert.That(readMetrics.FailedOperations).IsEqualTo(0L);
        await Assert.That(readLog.Select(static entry => entry.Sequence))
            .IsEquivalentTo(ExpectedSequences);
        await Assert.That(readLog.Select(static entry => entry.Operation))
            .IsEquivalentTo(Enumerable.Repeat(ABPlcSimulatorOperation.Read, LogicalValueCount));
        await Assert.That(readLog.Select(static entry => entry.TagName ?? string.Empty)).IsEquivalentTo(physicalTags);
    }
}
