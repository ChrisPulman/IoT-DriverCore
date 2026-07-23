// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.S7PlcRx.Performance;

namespace IoT.DriverCore.S7PlcRx.Tests.Performance;

/// <summary>Provides deterministic coverage for S7 performance helpers.</summary>
public sealed class S7PerformanceDeterministicCoverageTests
{
    /// <summary>Defines the first performance tag.</summary>
    private const string FirstTagName = "DB1.DBW0";

    /// <summary>Defines the second performance tag.</summary>
    private const string SecondTagName = "DB1.DBW2";

    /// <summary>Defines the value used by read and write tests.</summary>
    private const int Value = 17;

    /// <summary>Verifies high-performance tag groups read, write, dispose, and validate deterministically.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HighPerformanceTagGroupReadsWritesDisposesAndValidatesAsync()
    {
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        plc.SetSyncValue(FirstTagName, Value);
        plc.SetSyncValue(SecondTagName, Value);
        using var group = new HighPerformanceTagGroup<int>(plc, "Line", [FirstTagName, SecondTagName]);
        var values = await group.ReadAllAsync();
        await group.WriteAllAsync(new Dictionary<string, int> { [FirstTagName] = Value, ["Other"] = Value });

        await TUnit.Assertions.Assert.That(values[FirstTagName]).IsEqualTo(Value);
        await TUnit.Assertions.Assert.That(plc.WrittenValues[FirstTagName]).IsEqualTo(Value);
        await TUnit.Assertions.Assert.That(plc.WrittenValues.ContainsKey("Other")).IsFalse();
        await TUnit.Assertions.Assert.That(() => new HighPerformanceTagGroup<int>(null!, "Line", [FirstTagName]))
            .Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(() => new HighPerformanceTagGroup<int>(plc, " ", [FirstTagName]))
            .Throws<ArgumentException>();
        await TUnit.Assertions.Assert.That(() => new HighPerformanceTagGroup<int>(plc, "Line", []))
            .Throws<ArgumentException>();
    }

    /// <summary>Verifies optimized performance reads, writes, statistics, and failure argument paths.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PerformanceExtensionsReadWriteStatisticsAndArgumentFailuresAsync()
    {
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        plc.SetSyncValue(FirstTagName, Value);
        var reads = await PerformanceExtensions.ReadOptimizedAsync(plc, [FirstTagName], Value, null);
        var writes = await PerformanceExtensions.WriteOptimizedAsync(plc, new Dictionary<string, int> { [FirstTagName] = Value }, null);
        var statistics = PerformanceExtensions.GetPerformanceStatistics(plc, TimeProvider.System);
        Func<Task> nullRead = async () => _ = await PerformanceExtensions.ReadOptimizedAsync(
            null!,
            [FirstTagName],
            Value,
            null);
        Func<Task> nullWrite = async () => _ = await PerformanceExtensions.WriteOptimizedAsync<int>(null!, [], null);

        await TUnit.Assertions.Assert.That(reads[FirstTagName]).IsEqualTo(Value);
        await TUnit.Assertions.Assert.That(writes.SuccessfulWrites.ContainsKey(FirstTagName)).IsTrue();
        await TUnit.Assertions.Assert.That(statistics.TotalOperations).IsGreaterThanOrEqualTo(1L);
        await TUnit.Assertions.Assert.That(nullRead)
            .Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(nullWrite)
            .Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(() => PerformanceExtensions.GetPerformanceStatistics(null!))
            .Throws<ArgumentNullException>();
    }
}
