// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
using LeanCoreExtensions = IoT.DriverCore.TwinCATRx.Core.TwinCatRxExtensions;
using LeanSettings = IoT.DriverCore.TwinCATRx.Core.Settings;

namespace IoT.DriverCore.TwinCATRx.Tests.Rx;

/// <summary>Provides deterministic native-operation acceptance coverage for grouped TwinCAT logical tags.</summary>
public sealed class TwinCatLogicalTagPerformanceAcceptanceTests
{
    /// <summary>The number of logical members in the structure group.</summary>
    private const int LogicalItemsPerGroup = 2;

    /// <summary>The expected native read operations for the read and write groups.</summary>
    private const int ExpectedNativeReadOperations = 2;

    /// <summary>The expected native write operations for the write group.</summary>
    private const int ExpectedNativeWriteOperations = 1;

    /// <summary>The expected native notification publications for the write group.</summary>
    private const int ExpectedNativeNotificationPublications = 1;

    /// <summary>The deterministic grouped operation ratio.</summary>
    private const double ExpectedGroupingRatio = 2.0D;

    /// <summary>The initial structure count.</summary>
    private const int InitialCount = 10;

    /// <summary>The updated structure count.</summary>
    private const int UpdatedCount = 25;

    /// <summary>The configured TwinCAT 3 ADS port.</summary>
    private const int TwinCat3Port = 851;

    /// <summary>The count logical tag name.</summary>
    private const string CountTag = "Count";

    /// <summary>The enabled logical tag name.</summary>
    private const string EnabledTag = "Enabled";

    /// <summary>The structure root address.</summary>
    private const string StructureRoot = ".Machine.State";

    /// <summary>Verifies that grouped structure requests reduce native operations without a wall-clock assertion.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Grouped_Structure_Bulk_Operations_Have_Deterministic_Native_Operation_RatiosAsync()
    {
        using var native = new InMemoryAdsClient();
        var settings = new LeanSettings
        {
            AdsAddress = "simulation",
            Port = TwinCat3Port,
            SettingsId = "grouped-performance",
        };
        LeanCoreExtensions.AddNotification(settings, StructureRoot);
        LeanCoreExtensions.AddWriteVariable(settings, StructureRoot);
        _ = native.RegisterStructure(StructureRoot, new State { Count = InitialCount, Enabled = true });
        native.Connect(settings);

        using var client = new TwinCatLogicalTagClient(native);
        client.RegisterTag(CreateTag(CountTag, CountTag, "DINT"));
        client.RegisterTag(CreateTag(EnabledTag, EnabledTag, "BOOL"));
        native.ResetOperationMetrics();

        var reads = await client.ReadManyAsync([CountTag, EnabledTag]);
        var readMetrics = native.OperationMetrics;
        var writes = await client.WriteManyAsync(
            [
                new LogicalTagValue(CountTag, UpdatedCount, TimeProvider.System.GetUtcNow(), "Good"),
                new LogicalTagValue(EnabledTag, false, TimeProvider.System.GetUtcNow(), "Good"),
            ]);
        var metrics = native.OperationMetrics;

        await TUnitAssert.That(reads.All(static result => result.Succeeded)).IsTrue();
        await TUnitAssert.That(writes.All(static result => result.Succeeded)).IsTrue();
        await TUnitAssert.That(readMetrics.ReadOperations).IsEqualTo(1);
        await TUnitAssert.That(readMetrics.WriteOperations).IsEqualTo(0);
        await TUnitAssert.That(LogicalItemsPerGroup / (double)readMetrics.ReadOperations)
            .IsEqualTo(ExpectedGroupingRatio);
        await TUnitAssert.That(metrics.ReadOperations).IsEqualTo(ExpectedNativeReadOperations);
        await TUnitAssert.That(metrics.WriteOperations).IsEqualTo(ExpectedNativeWriteOperations);
        await TUnitAssert.That(LogicalItemsPerGroup / (double)metrics.WriteOperations)
            .IsEqualTo(ExpectedGroupingRatio);
        await TUnitAssert.That(metrics.NotificationPublications).IsEqualTo(ExpectedNativeNotificationPublications);
        await TUnitAssert.That(native.TryGetValue<State>(StructureRoot, out var state)).IsTrue();
        await TUnitAssert.That(state!.Count).IsEqualTo(UpdatedCount);
        await TUnitAssert.That(state.Enabled).IsFalse();
    }

    /// <summary>Creates a structure-backed logical tag.</summary>
    /// <param name="name">The logical tag name.</param>
    /// <param name="member">The structure member name.</param>
    /// <param name="dataType">The logical data type.</param>
    /// <returns>The logical tag.</returns>
    private static LogicalTag CreateTag(string name, string member, string dataType) =>
        new(
            name,
            $"{StructureRoot}.{member}",
            dataType,
            new LogicalTagOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["TwinCAT.StructureRoot"] = StructureRoot,
                    ["TwinCAT.MemberAddress"] = member,
                },
            });

    /// <summary>Provides the grouped root payload.</summary>
    private sealed class State
    {
        /// <summary>Gets or sets the count.</summary>
        public int Count { get; set; }

        /// <summary>Gets or sets whether the state is enabled.</summary>
        public bool Enabled { get; set; }
    }
}
