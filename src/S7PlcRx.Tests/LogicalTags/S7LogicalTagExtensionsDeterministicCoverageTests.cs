// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
using IoT.DriverCore.S7PlcRx.Binding;
using IoT.DriverCore.S7PlcRx.LogicalTags;

namespace IoT.DriverCore.S7PlcRx.Tests.LogicalTags;

/// <summary>Provides deterministic coverage of S7 logical-tag extension methods.</summary>
public sealed class S7LogicalTagExtensionsDeterministicCoverageTests
{
    /// <summary>Defines the logical tag name used for typed extension operations.</summary>
    private const string ValueTagName = "Value";

    /// <summary>Defines the read-only logical tag name used for write failure projection.</summary>
    private const string ReadOnlyTagName = "ReadOnly";

    /// <summary>Defines the missing logical tag name used for read failure projection.</summary>
    private const string MissingTagName = "Missing";

    /// <summary>Defines the generated binding polling interval.</summary>
    private const int PollIntervalMilliseconds = 250;

    /// <summary>Defines the input binding polling interval.</summary>
    private const int InputPollIntervalMilliseconds = 100;

    /// <summary>Defines the generated binding array length.</summary>
    private const int ArrayLength = 3;

    /// <summary>Defines the initial value read through the typed extension.</summary>
    private const ushort InitialValue = 17;

    /// <summary>Defines the first typed write payload.</summary>
    private const int WrittenValue = 18;

    /// <summary>Defines the cancellation write payload.</summary>
    private const int CanceledWriteValue = 19;

    /// <summary>Defines the null-time-provider write payload.</summary>
    private const int NullProviderWriteValue = 20;

    /// <summary>Verifies generated binding definitions become correctly configured common logical tags.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CreateLogicalTagCatalogMapsDirectionsMetadataAndPollingAsync()
    {
        var catalog = S7LogicalTagExtensions.CreateLogicalTagCatalog(
            [
                new S7TagDefinition(
                    ValueTagName,
                    "DB1.DBW0",
                    typeof(ushort),
                    PollIntervalMilliseconds,
                    S7TagDirection.ReadWrite,
                    ArrayLength),
                new S7TagDefinition("Output", "DB1.DBW2", typeof(ushort), 0, S7TagDirection.WriteOnly, 1),
                new S7TagDefinition(
                    "Input",
                    "DB1.DBW4",
                    typeof(bool),
                    InputPollIntervalMilliseconds,
                    S7TagDirection.ReadOnly,
                    1),
            ]);

        await TUnit.Assertions.Assert.That(catalog.TryGet(ValueTagName, out var value)).IsTrue();
        await TUnit.Assertions.Assert.That(value?.AccessMode).IsEqualTo(LogicalTagAccessMode.ReadWrite);
        await TUnit.Assertions.Assert.That(value?.Metadata["ArrayLength"]).IsEqualTo(ArrayLength.ToString());
        await TUnit.Assertions.Assert.That(value?.ScanInterval).IsEqualTo(TimeSpan.FromMilliseconds(PollIntervalMilliseconds));
        await TUnit.Assertions.Assert.That(catalog.TryGet("Output", out var output)).IsTrue();
        await TUnit.Assertions.Assert.That(output?.AccessMode).IsEqualTo(LogicalTagAccessMode.Write);
        await TUnit.Assertions.Assert.That(output?.ScanInterval).IsNull();
        await TUnit.Assertions.Assert.That(catalog.TryGet("Input", out var input)).IsTrue();
        await TUnit.Assertions.Assert.That(input?.AccessMode).IsEqualTo(LogicalTagAccessMode.Read);
        await TUnit.Assertions.Assert.That(() => S7LogicalTagExtensions.CreateLogicalTagCatalog(null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>Verifies typed read and write projections handle conversion, conversion failures, and cancellation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypedReadAndWriteExtensionsProjectResultsThroughTheManagedFakeAsync()
    {
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        using var catalog = CreateCatalog();
        using var client = S7LogicalTagExtensions.CreateLogicalTagClient(plc, catalog);
        plc.SetAsyncValue(ValueTagName, InitialValue);
        using var cancellation = new CancellationTokenSource();

        var read = await S7LogicalTagExtensions.ReadAsync(client, ValueTagName, default(int), CancellationToken.None);
        var failedConversion = await S7LogicalTagExtensions.ReadAsync(
            client,
            ValueTagName,
            Guid.Empty,
            CancellationToken.None);
        var write = await S7LogicalTagExtensions.WriteAsync(
            client,
            ValueTagName,
            WrittenValue,
            TimeProvider.System,
            CancellationToken.None);
        await AsyncCompatibility.CancelAsync(cancellation);
        Func<Task> canceledWrite = async () => _ = await S7LogicalTagExtensions.WriteAsync(
            client,
            ValueTagName,
            CanceledWriteValue,
            TimeProvider.System,
            cancellation.Token);
        Func<Task> nullClient = async () => _ = await S7LogicalTagExtensions.ReadAsync(
            null!,
            ValueTagName,
            default(int),
            CancellationToken.None);
        Func<Task> nullTimeProvider = async () => _ = await S7LogicalTagExtensions.WriteAsync(
            client,
            ValueTagName,
            NullProviderWriteValue,
            null!,
            CancellationToken.None);

        await TUnit.Assertions.Assert.That(read.Succeeded).IsTrue();
        await TUnit.Assertions.Assert.That(read.Value).IsEqualTo((int)InitialValue);
        await TUnit.Assertions.Assert.That(failedConversion.Succeeded).IsFalse();
        await TUnit.Assertions.Assert.That(write.Succeeded).IsTrue();
        await TUnit.Assertions.Assert.That(write.Value).IsEqualTo(WrittenValue);
        await TUnit.Assertions.Assert.That(plc.WrittenValues[ValueTagName]).IsEqualTo((ushort)WrittenValue);
        await TUnit.Assertions.Assert.That(canceledWrite).Throws<OperationCanceledException>();
        await TUnit.Assertions.Assert.That(nullClient).Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(nullTimeProvider).Throws<ArgumentNullException>();
    }

    /// <summary>Verifies default overloads preserve logical failures and the persistent client factory is usable.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DefaultOverloadsProjectFailuresAndCreatePersistentClientsAsync()
    {
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        using var catalog = CreateCatalog();
        using var client = S7LogicalTagExtensions.CreateLogicalTagClient(plc, catalog);
        using var persistentClient = S7LogicalTagExtensions.CreateLogicalTagClient(
            plc,
            catalog,
            new LogicalTagSqliteStore("Data Source=:memory:"));
        plc.SetAsyncValue(ValueTagName, InitialValue);

        var read = await S7LogicalTagExtensions.ReadAsync(client, ValueTagName, default(int));
        var missingRead = await S7LogicalTagExtensions.ReadAsync(client, MissingTagName, default(int));
        var write = await S7LogicalTagExtensions.WriteAsync(
            client,
            ValueTagName,
            WrittenValue,
            CancellationToken.None);
        var failedWrite = await S7LogicalTagExtensions.WriteAsync(
            client,
            ReadOnlyTagName,
            WrittenValue,
            CancellationToken.None);

        await TUnit.Assertions.Assert.That(read.Succeeded).IsTrue();
        await TUnit.Assertions.Assert.That(missingRead.Succeeded).IsFalse();
        await TUnit.Assertions.Assert.That(write.Succeeded).IsTrue();
        await TUnit.Assertions.Assert.That(failedWrite.Succeeded).IsFalse();
        await TUnit.Assertions.Assert.That(persistentClient.Catalog).IsEqualTo(catalog);
    }

    /// <summary>Creates a one-tag catalog used for typed extension tests.</summary>
    /// <returns>A catalog with one writable word definition.</returns>
    private static LogicalTagCatalog CreateCatalog()
    {
        var catalog = new LogicalTagCatalog();
        catalog.Upsert(new LogicalTag(ValueTagName, "DB1.DBW0", "WORD"));
        catalog.Upsert(new LogicalTag(
            ReadOnlyTagName,
            "DB1.DBW2",
            "WORD",
            new LogicalTagOptions { AccessMode = LogicalTagAccessMode.Read }));
        return catalog;
    }
}
