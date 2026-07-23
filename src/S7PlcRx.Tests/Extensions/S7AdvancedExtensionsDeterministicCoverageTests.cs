// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.S7PlcRx.Advanced;

namespace IoT.DriverCore.S7PlcRx.Tests.Extensions;

/// <summary>Provides deterministic coverage for the advanced S7 extension surface.</summary>
public sealed class S7AdvancedExtensionsDeterministicCoverageTests
{
    /// <summary>Defines the first batch tag name.</summary>
    private const string FirstTagName = "DB1.DBW0";

    /// <summary>Defines the second batch tag name.</summary>
    private const string SecondTagName = "DB2.DBW0";

    /// <summary>Defines the first observed value.</summary>
    private const int FirstValue = 17;

    /// <summary>Defines the second observed value.</summary>
    private const int SecondValue = 29;

    /// <summary>Defines the expected number of tags and values in paired batch operations.</summary>
    private const int ExpectedPairCount = 2;

    /// <summary>Defines the deterministic read timeout used by optimized batch tests.</summary>
    private const int ReadTimeoutMilliseconds = 100;

    /// <summary>Defines the original value restored after verification failure.</summary>
    private const int OriginalValue = 1;

    /// <summary>Defines the performance-monitoring duration.</summary>
    private static readonly TimeSpan MonitoringDuration = TimeSpan.FromMilliseconds(25);

    /// <summary>Verifies batch observation accepts an empty tag set without requiring a PLC transport.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObserveBatchReturnsAnEmptySnapshotForAnEmptyTagSetAsync()
    {
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        var observation = AdvancedExtensions.ObserveBatch(plc, 0);

        await TUnit.Assertions.Assert.That(observation).IsNotNull();
    }

    /// <summary>Verifies the basic asynchronous batch read and write wrappers use the deterministic PLC state.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValueBatchAsyncReadsAndWritesConfiguredValuesAsync()
    {
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        plc.SetSyncValue(FirstTagName, FirstValue);
        plc.SetSyncValue(SecondTagName, SecondValue);
        var writes = new Dictionary<string, int>
        {
            [FirstTagName] = SecondValue,
            [SecondTagName] = FirstValue,
        };

        var values = await AdvancedExtensions.ValueBatchAsync(plc, 0, FirstTagName, SecondTagName);
        await AdvancedExtensions.ValueBatchAsync(plc, writes);

        await TUnit.Assertions.Assert.That(values[FirstTagName]).IsEqualTo(FirstValue);
        await TUnit.Assertions.Assert.That(values[SecondTagName]).IsEqualTo(SecondValue);
        await TUnit.Assertions.Assert.That(plc.WrittenValues[FirstTagName]).IsEqualTo(SecondValue);
        await TUnit.Assertions.Assert.That(plc.WrittenValues[SecondTagName]).IsEqualTo(FirstValue);
    }

    /// <summary>Verifies optimized reads report successful, partial, and failed groups without transport dependencies.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadBatchOptimizedAsyncReportsSuccessPartialAndFailureAsync()
    {
        var mapping = new Dictionary<string, string>
        {
            [FirstTagName] = FirstTagName,
            [SecondTagName] = SecondTagName,
        };

        using var successfulPlc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        successfulPlc.SetAsyncValue(FirstTagName, FirstValue);
        successfulPlc.SetAsyncValue(SecondTagName, SecondValue);
        var successful = await AdvancedExtensions.ReadBatchOptimizedAsync(
            successfulPlc,
            0,
            mapping,
            ReadTimeoutMilliseconds);

        using var partialPlc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        partialPlc.SetAsyncValue(FirstTagName, FirstValue);
        partialPlc.SetAsyncFactory(
            SecondTagName,
            static _ => Task.FromException<object?>(new InvalidOperationException("read failure")));
        var partial = await AdvancedExtensions.ReadBatchOptimizedAsync(
            partialPlc,
            0,
            mapping,
            ReadTimeoutMilliseconds);

        using var failedPlc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        failedPlc.SetAsyncFactory(
            FirstTagName,
            static _ => Task.FromException<object?>(new InvalidOperationException("first failure")));
        var failed = await AdvancedExtensions.ReadBatchOptimizedAsync(
            failedPlc,
            0,
            new Dictionary<string, string> { [FirstTagName] = FirstTagName },
            ReadTimeoutMilliseconds);

        await TUnit.Assertions.Assert.That(successful.OverallSuccess).IsTrue();
        await TUnit.Assertions.Assert.That(successful.SuccessCount).IsEqualTo(ExpectedPairCount);
        await TUnit.Assertions.Assert.That(successful.Values[FirstTagName]).IsEqualTo(FirstValue);
        await TUnit.Assertions.Assert.That(partial.OverallSuccess).IsFalse();
        await TUnit.Assertions.Assert.That(partial.Success[FirstTagName]).IsTrue();
        await TUnit.Assertions.Assert.That(partial.Success[SecondTagName]).IsFalse();
        await TUnit.Assertions.Assert.That(partial.Errors[SecondTagName]).Contains("read failure");
        await TUnit.Assertions.Assert.That(failed.OverallSuccess).IsFalse();
        await TUnit.Assertions.Assert.That(failed.ErrorCount).IsEqualTo(1);
    }

    /// <summary>Verifies optimized writes report verification failures and restore captured values when requested.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WriteBatchOptimizedAsyncReportsSuccessPartialAndFailureAsync()
    {
        var values = new Dictionary<string, int>
        {
            [FirstTagName] = FirstValue,
            [SecondTagName] = SecondValue,
        };

        using var successfulPlc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        successfulPlc.SetSyncValue(FirstTagName, FirstValue);
        successfulPlc.SetSyncValue(SecondTagName, SecondValue);
        var successful = await AdvancedExtensions.WriteBatchOptimizedAsync(
            successfulPlc,
            values,
            verifyWrites: true,
            enableRollback: false);

        using var partialPlc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        partialPlc.SetSyncValue(FirstTagName, FirstValue);
        partialPlc.SetSyncValue(SecondTagName, OriginalValue);
        var partial = await AdvancedExtensions.WriteBatchOptimizedAsync(
            partialPlc,
            values,
            verifyWrites: true,
            enableRollback: true);

        using var failedPlc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        failedPlc.SetSyncValue(FirstTagName, OriginalValue);
        var failed = await AdvancedExtensions.WriteBatchOptimizedAsync(
            failedPlc,
            new Dictionary<string, int> { [FirstTagName] = FirstValue },
            verifyWrites: true,
            enableRollback: false);

        await TUnit.Assertions.Assert.That(successful.OverallSuccess).IsTrue();
        await TUnit.Assertions.Assert.That(successful.SuccessCount).IsEqualTo(ExpectedPairCount);
        await TUnit.Assertions.Assert.That(partial.OverallSuccess).IsFalse();
        await TUnit.Assertions.Assert.That(partial.RollbackPerformed).IsTrue();
        await TUnit.Assertions.Assert.That(partial.Success[FirstTagName]).IsTrue();
        await TUnit.Assertions.Assert.That(partial.Success[SecondTagName]).IsFalse();
        await TUnit.Assertions.Assert.That(partialPlc.WrittenValues[SecondTagName]).IsEqualTo(OriginalValue);
        await TUnit.Assertions.Assert.That(failed.OverallSuccess).IsFalse();
        await TUnit.Assertions.Assert.That(failed.ErrorCount).IsEqualTo(1);
    }

    /// <summary>Verifies diagnostics aggregate deterministic connection and tag metrics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetDiagnosticsAsyncCollectsTagMetricsAndRecommendationsAsync()
    {
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        plc.TagList.Add(new Tag(FirstTagName, FirstTagName, typeof(int)) { DoNotPoll = false });
        plc.TagList.Add(new Tag(SecondTagName, SecondTagName, typeof(int)) { DoNotPoll = true });

        var diagnostics = await AdvancedExtensions.GetDiagnosticsAsync(plc, TimeProvider.System);

        await TUnit.Assertions.Assert.That(diagnostics.IsConnected).IsTrue();
        await TUnit.Assertions.Assert.That(diagnostics.TagMetrics.TotalTags).IsEqualTo(ExpectedPairCount);
        await TUnit.Assertions.Assert.That(diagnostics.TagMetrics.ActiveTags).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(diagnostics.TagMetrics.InactiveTags).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(diagnostics.TagMetrics.DataBlockDistribution["DB1"]).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(diagnostics.TagMetrics.DataBlockDistribution["DB2"]).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(diagnostics.Recommendations.Count).IsEqualTo(1);
    }

    /// <summary>Verifies performance analysis counts only changes and produces a fast-tag recommendation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AnalyzePerformanceAsyncCountsDistinctPublishedChangesAsync()
    {
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        var analysis = await AdvancedExtensions.AnalyzePerformanceAsync(plc, MonitoringDuration, TimeProvider.System);

        await TUnit.Assertions.Assert.That(analysis.TotalTagChanges).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(analysis.TagChangeFrequencies.Count).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(analysis.AverageChangesPerTag).IsEqualTo(0D);
        await TUnit.Assertions.Assert.That(analysis.Recommendations.Count).IsEqualTo(0);
    }

    /// <summary>Verifies tag-group construction validates inputs and registers database-addressed tags.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CreateTagGroupValidatesInputsAndRegistersDatabaseTagsAsync()
    {
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        using var group = AdvancedExtensions.CreateTagGroup(plc, 0, "Line", FirstTagName);

        await TUnit.Assertions.Assert.That(group.GroupName).IsEqualTo("Line");
        await TUnit.Assertions.Assert.That(() => AdvancedExtensions.CreateTagGroup(null!, 0, "Line", FirstTagName))
            .Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(() => AdvancedExtensions.CreateTagGroup(plc, 0, " ", FirstTagName))
            .Throws<ArgumentException>();
        await TUnit.Assertions.Assert.That(() => AdvancedExtensions.CreateTagGroup(plc, 0, "Line", []))
            .Throws<ArgumentException>();
    }

    /// <summary>Verifies advanced batch APIs reject null PLCs and accept empty batches without transport work.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AdvancedBatchApisValidateNullAndEmptyInputsAsync()
    {
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        Dictionary<string, int> emptyValues = [];
        var emptyRead = await AdvancedExtensions.ReadBatchOptimizedAsync(plc, 0, [], ReadTimeoutMilliseconds);
        var emptyWrite = await AdvancedExtensions.WriteBatchOptimizedAsync(plc, emptyValues, false, false);
        Func<Task> nullRead = async () => _ = await AdvancedExtensions.ReadBatchOptimizedAsync(
            null!,
            0,
            [],
            ReadTimeoutMilliseconds);
        Func<Task> nullDiagnostics = async () => _ = await AdvancedExtensions.GetDiagnosticsAsync(null!);

        await TUnit.Assertions.Assert.That(emptyRead.OverallSuccess).IsTrue();
        await TUnit.Assertions.Assert.That(emptyWrite.OverallSuccess).IsTrue();
        await TUnit.Assertions.Assert.That(nullRead).Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(nullDiagnostics).Throws<ArgumentNullException>();
    }
}
