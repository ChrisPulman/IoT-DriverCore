// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive.Tests;

#else

namespace IoT.DriverCore.MitsubishiRx.Tests;

#endif

/// <summary>Exercises residual logical-tag bulk dispatch and failure paths.</summary>
internal sealed class MitsubishiLogicalBulkResidualCoverageTests
{
    /// <summary>Stores the deterministic simulator port.</summary>
    private const int Port = 5015;

    /// <summary>Stores the unsigned word data type.</summary>
    private const string UInt16DataType = "UInt16";

    /// <summary>Stores the single-precision data type.</summary>
    private const string FloatDataType = "Float";

    /// <summary>Stores the logical float tag name.</summary>
    private const string FloatTagName = "Float";

    /// <summary>Stores the logical bit tag name.</summary>
    private const string BitTagName = "Bit";

    /// <summary>Stores a missing logical tag name.</summary>
    private const string MissingTagName = "Missing";

    /// <summary>Stores a tag whose value cannot be bulk encoded.</summary>
    private const string BadValueTagName = "BadValue";

    /// <summary>Stores the deterministic floating-point value.</summary>
    private const float FloatValue = 1.0F;

    /// <summary>Stores the first deterministic word value.</summary>
    private const ushort FirstWordValue = 10;

    /// <summary>Stores the second deterministic word value.</summary>
    private const ushort SecondWordValue = 11;

    /// <summary>Stores the gapped deterministic word value.</summary>
    private const ushort GappedWordValue = 20;

    /// <summary>Stores a non-default logical scan interval.</summary>
    private const int CustomScanIntervalMilliseconds = 25;

    /// <summary>Verifies non-word definitions use the per-tag logical dispatch path.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task BulkOperationsFallBackToIndividualTypedDispatchAsync()
    {
        var options = CreateOptions();
        await using var simulator = new MitsubishiSimulatorTransport(
            request => MitsubishiSimulatorTransport.CreateSuccessResponse(
                options,
                request.Description.StartsWith("Read", StringComparison.Ordinal) ? [0, 0, 0x80, 0x3F] : []));
        await using var owner = new MitsubishiRx(options, simulator, Scheduler.Immediate);
        using var logical = owner.CreateLogicalTagClient(null, null, null);
        logical.RegisterRange(
        [
            new LogicalTag(FloatTagName, "D10", FloatDataType),
            new LogicalTag(BitTagName, "M10", "Bit"),
        ]);

        var reads = await logical.ReadManyAsync([FloatTagName, BitTagName], CancellationToken.None);
        var writes = await logical.WriteManyAsync(
        [
            new LogicalTagValue(FloatTagName, FloatValue, DateTimeOffset.UnixEpoch),
            new LogicalTagValue(BitTagName, true, DateTimeOffset.UnixEpoch),
        ],
            CancellationToken.None);

        await Assert.That(reads.Select(static result => result.Succeeded).All(static value => value)).IsTrue();
        await Assert.That(reads[0].Value!.Value).IsEqualTo(FloatValue);
        await Assert.That((bool)reads[1].Value!.Value!).IsFalse();
        await Assert.That(writes.Select(static result => result.Succeeded).All(static value => value)).IsTrue();
        await Assert.That(simulator.Requests.Select(static request => request.Description))
            .Contains("Read words D10");
    }

    /// <summary>Verifies a short contiguous response and PLC failure retain bulk caller correlation.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task ContiguousBulkFailuresAreReturnedForEveryPlannedItemAsync()
    {
        var options = CreateOptions();
        await using var simulator = new MitsubishiSimulatorTransport(
            request => MitsubishiSimulatorTransport.CreateSuccessResponse(
                options,
                request.Description.StartsWith("Read", StringComparison.Ordinal) ? [0x34, 0x12] : []));
        await using var owner = new MitsubishiRx(options, simulator, Scheduler.Immediate);
        using var logical = CreateWordLogicalClient(owner);

        var shortReads = await logical.ReadManyAsync(["D10", "D11"], CancellationToken.None);
        simulator.EnqueueResponse(MitsubishiSimulatorTransport.CreateErrorResponse(options, 0xC051));
        var failedWrites = await logical.WriteManyAsync(
        [
            new LogicalTagValue("D10", FirstWordValue, DateTimeOffset.UnixEpoch),
            new LogicalTagValue("D11", SecondWordValue, DateTimeOffset.UnixEpoch),
        ],
            CancellationToken.None);

        await Assert.That(shortReads.Select(static result => result.Succeeded).All(static value => !value)).IsTrue();
        await Assert.That(shortReads[0].Error.Contains("Expected 2 words but received 1", StringComparison.Ordinal)).IsTrue();
        await Assert.That(failedWrites.Select(static result => result.Succeeded).All(static value => !value)).IsTrue();
        await Assert.That(failedWrites[0].Error.Contains("[0]", StringComparison.Ordinal)).IsTrue();
        await Assert.That(failedWrites[1].Error.Contains("[1]", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Verifies a short random response and PLC failure retain gapped caller correlation.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task RandomBulkFailuresAreReturnedForEveryPlannedItemAsync()
    {
        var options = CreateOptions();
        await using var simulator = new MitsubishiSimulatorTransport(
            request => MitsubishiSimulatorTransport.CreateSuccessResponse(
                options,
                request.Description == "Random read words" ? [0x34, 0x12] : []));
        await using var owner = new MitsubishiRx(options, simulator, Scheduler.Immediate);
        using var logical = owner.CreateLogicalTagClient(null, null, null);
        logical.RegisterRange(
        [
            new LogicalTag("D10", "D10", UInt16DataType),
            new LogicalTag("D20", "D20", UInt16DataType),
        ]);

        var shortReads = await logical.ReadManyAsync(["D10", "D20"], CancellationToken.None);
        simulator.EnqueueResponse(MitsubishiSimulatorTransport.CreateErrorResponse(options, 0xC052));
        var failedWrites = await logical.WriteManyAsync(
        [
            new LogicalTagValue("D10", FirstWordValue, DateTimeOffset.UnixEpoch),
            new LogicalTagValue("D20", GappedWordValue, DateTimeOffset.UnixEpoch),
        ],
            CancellationToken.None);

        await Assert.That(shortReads.Select(static result => result.Succeeded).All(static value => !value)).IsTrue();
        await Assert.That(shortReads[0].Error.Contains("Expected 2 words but received 1", StringComparison.Ordinal)).IsTrue();
        await Assert.That(failedWrites.Select(static result => result.Succeeded).All(static value => !value)).IsTrue();
        await Assert.That(simulator.Requests.Select(static request => request.Description))
            .Contains("Random write words");
    }

    /// <summary>Verifies missing and access-limited tags return stable individual and indexed failures.</summary>
    /// <returns>A task that completes after the guard paths have been verified.</returns>
    [Test]
    internal async Task LogicalAccessAndIndividualFailureGuardsAreCorrelatedAsync()
    {
        var options = CreateOptions();
        await using var simulator = new MitsubishiSimulatorTransport();
        await using var owner = new MitsubishiRx(options, simulator, Scheduler.Immediate);
        using var logical = owner.CreateLogicalTagClient(null, null, null);
        logical.RegisterRange(
        [
            new LogicalTag(
                "ReadOnly",
                "D0",
                UInt16DataType,
                new LogicalTagOptions { AccessMode = LogicalTagAccessMode.Read }),
            new LogicalTag(
                "WriteOnly",
                "D1",
                UInt16DataType,
                new LogicalTagOptions { AccessMode = LogicalTagAccessMode.Write }),
            new LogicalTag(FloatTagName, "D10", FloatDataType),
        ]);

        var missingRead = await logical.ReadAsync(MissingTagName, CancellationToken.None);
        var typedMissingRead = await logical.ReadAsync(
            new LogicalTagKey<ushort>(MissingTagName),
            CancellationToken.None);
        var blockedRead = await logical.ReadAsync("WriteOnly", CancellationToken.None);
        var missingWrite = await logical.WriteAsync(
            new LogicalTagValue(MissingTagName, FirstWordValue, DateTimeOffset.UnixEpoch),
            CancellationToken.None);
        var blockedWrite = await logical.WriteAsync(
            new LogicalTagValue("ReadOnly", FirstWordValue, DateTimeOffset.UnixEpoch),
            CancellationToken.None);
        simulator.EnqueueResponse(MitsubishiSimulatorTransport.CreateErrorResponse(options, 0xC051));
        var bulkReads = await logical.ReadManyAsync([MissingTagName, FloatTagName], CancellationToken.None);
        simulator.EnqueueResponse(MitsubishiSimulatorTransport.CreateErrorResponse(options, 0xC052));
        var bulkWrites = await logical.WriteManyAsync(
            [new LogicalTagValue(FloatTagName, FloatValue, DateTimeOffset.UnixEpoch)],
            CancellationToken.None);

        await Assert.That(missingRead.Succeeded).IsFalse();
        await Assert.That(typedMissingRead.Succeeded).IsFalse();
        await Assert.That(blockedRead.Succeeded).IsFalse();
        await Assert.That(missingWrite.Succeeded).IsFalse();
        await Assert.That(blockedWrite.Succeeded).IsFalse();
        await Assert.That(bulkReads.All(static result => !result.Succeeded)).IsTrue();
        await Assert.That(bulkWrites[0].Succeeded).IsFalse();
    }

    /// <summary>Verifies private bulk eligibility and scalar encoding guard combinations.</summary>
    /// <returns>A task that completes after the planner guards have been verified.</returns>
    [Test]
    internal async Task BulkPlannerEligibilityGuardsRejectInvalidShapesAsync()
    {
        MitsubishiTagDefinition[] definitions =
        [
            new("InvalidAddress", "BAD", UInt16DataType),
            new("BitAddress", "M0", UInt16DataType),
            new(BadValueTagName, "D0", UInt16DataType),
            new("NonSingle", "D10", FloatDataType),
        ];
        await using var simulator = new MitsubishiSimulatorTransport();
        await using var owner = new MitsubishiRx(CreateOptions(), simulator, Scheduler.Immediate)
        {
            TagDatabase = new(definitions),
        };
        using var logical = owner.CreateLogicalTagClient(null, null, null);
        LogicalTag[] logicalTags = definitions
            .Select(static definition =>
                new LogicalTag(definition.Name, definition.Address, definition.DataType!))
            .ToArray();
        logical.RegisterRange(logicalTags);

        var method = typeof(MitsubishiLogicalTagClient).GetMethod(
            "TryCreateBulkWordRequest",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new MissingMethodException(
                typeof(MitsubishiLogicalTagClient).FullName,
                "TryCreateBulkWordRequest");
        for (var index = 0; index < definitions.Length; index++)
        {
            object? value = definitions[index].Name == BadValueTagName
                ? new LogicalTagValue(BadValueTagName, new object(), DateTimeOffset.UnixEpoch)
                : null;
            object?[] arguments = [index, logicalTags[index], value, null];
            await Assert.That((bool)method.Invoke(logical, arguments)!).IsFalse();
        }

        await VerifyBulkWordEncodingGuardsAsync();
    }

    /// <summary>Verifies serial and Ethernet frame support decisions for random-word operations.</summary>
    /// <returns>A task that completes after every transport/frame decision has been verified.</returns>
    [Test]
    internal async Task RandomWordCommandSupportCoversEveryTransportFrameDecisionAsync()
    {
        var method = typeof(MitsubishiLogicalTagClient).GetMethod(
            "SupportsRandomWordCommands",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new MissingMethodException(
                typeof(MitsubishiLogicalTagClient).FullName,
                "SupportsRandomWordCommands");
        (MitsubishiTransportKind Transport, MitsubishiFrameType Frame, bool Expected)[] cases =
        [
            (MitsubishiTransportKind.Tcp, MitsubishiFrameType.OneE, false),
            (MitsubishiTransportKind.Tcp, MitsubishiFrameType.ThreeE, true),
            (MitsubishiTransportKind.Serial, MitsubishiFrameType.OneC, false),
            (MitsubishiTransportKind.Serial, MitsubishiFrameType.ThreeC, true),
        ];
        foreach (var item in cases)
        {
            var options = CreateOptions() with
            {
                TransportKind = item.Transport,
                FrameType = item.Frame,
                Serial = item.Transport == MitsubishiTransportKind.Serial
                    ? new MitsubishiSerialOptions("COM1")
                    : null,
            };
            await using var simulator = new MitsubishiSimulatorTransport();
            await using var owner = new MitsubishiRx(options, simulator, Scheduler.Immediate);
            using var logical = owner.CreateLogicalTagClient(null, null, null);
            await Assert.That((bool)method.Invoke(logical, null)!).IsEqualTo(item.Expected);
        }
    }

    /// <summary>Verifies legacy transports fall back to correlated individual writes.</summary>
    /// <returns>A task that completes after success, failure, and argument guards are verified.</returns>
    [Test]
    internal async Task LegacyBulkFallbackCoversIndividualSuccessAndFailureAsync()
    {
        var options = CreateOptions() with
        {
            Host = "COM1",
            Port = 0,
            FrameType = MitsubishiFrameType.OneC,
            DataCode = CommunicationDataCode.Ascii,
            TransportKind = MitsubishiTransportKind.Serial,
            Serial = new("COM1", MessageFormat: MitsubishiSerialMessageFormat.Format1),
        };
        await using var simulator = new MitsubishiSimulatorTransport();
        await using var owner = new MitsubishiRx(options, simulator, Scheduler.Immediate);
        using var logical = owner.CreateLogicalTagClient(null, null, null);
        logical.RegisterRange(
        [
            new LogicalTag("D0", "D0", UInt16DataType),
            new LogicalTag(
                "D2",
                "D2",
                UInt16DataType,
                new LogicalTagOptions
                {
                    ScanInterval = TimeSpan.FromMilliseconds(CustomScanIntervalMilliseconds),
                }),
        ]);

        var successful = await logical.WriteManyAsync(
        [
            new LogicalTagValue("D0", FirstWordValue, DateTimeOffset.UnixEpoch),
            new LogicalTagValue("D2", GappedWordValue, DateTimeOffset.UnixEpoch),
        ],
            CancellationToken.None);
        simulator.EnqueueResponse(MitsubishiSimulatorTransport.CreateErrorResponse(options, 0xC051));
        var mixed = await logical.WriteManyAsync(
        [
            new LogicalTagValue("D0", FirstWordValue, DateTimeOffset.UnixEpoch),
            new LogicalTagValue("D2", GappedWordValue, DateTimeOffset.UnixEpoch),
        ],
            CancellationToken.None);
        var typedSuccess = await logical.WriteAsync("D0", FirstWordValue, CancellationToken.None);
        simulator.EnqueueResponse(MitsubishiSimulatorTransport.CreateErrorResponse(options, 0xC052));
        var typedFailure = await logical.WriteAsync("D0", FirstWordValue, CancellationToken.None);

        _ = logical.Observe("D0");
        _ = logical.Observe("D2");
        _ = Assert.Throws<InvalidOperationException>(() => logical.Observe(MissingTagName));

        bool rejectedNullEntry = await RejectNullBulkEntryAsync(logical);

        await Assert.That(successful.All(static result => result.Succeeded)).IsTrue();
        await Assert.That(mixed[0].Succeeded).IsFalse();
        await Assert.That(mixed[1].Succeeded).IsTrue();
        await Assert.That(typedSuccess.Succeeded).IsTrue();
        await Assert.That(typedFailure.Succeeded).IsFalse();
        await Assert.That(rejectedNullEntry).IsTrue();
    }

    /// <summary>Passes a runtime-created null array through the bulk argument guard.</summary>
    /// <param name="logical">The logical client.</param>
    /// <returns><see langword="true"/> when the null element is rejected.</returns>
    private static async Task<bool> RejectNullBulkEntryAsync(MitsubishiLogicalTagClient logical)
    {
        var nullEntries = (IReadOnlyCollection<LogicalTagValue>)Array.CreateInstance(
            typeof(LogicalTagValue),
            1);
        try
        {
            _ = await logical.WriteManyAsync(nullEntries, CancellationToken.None);
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    /// <summary>Verifies all scalar word encoding type/value combinations.</summary>
    /// <returns>A task that completes after the encoding guards have been verified.</returns>
    private static async Task VerifyBulkWordEncodingGuardsAsync()
    {
        var method = typeof(MitsubishiLogicalTagClient).GetMethod(
            "TryEncodeBulkWord",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?? throw new MissingMethodException(
                typeof(MitsubishiLogicalTagClient).FullName,
                "TryEncodeBulkWord");
        (MitsubishiTagDefinition Definition, object Value, bool Expected)[] cases =
        [
            (new("Signed", "D0", "Int16"), (short)-1, true),
            (new("SignedMismatch", "D0", "Int16"), FirstWordValue, false),
            (new("Word", "D0", "Word"), FirstWordValue, true),
            (new("WordMismatch", "D0", "Word"), new object(), false),
            (new("Unsigned", "D0", UInt16DataType), FirstWordValue, true),
        ];
        foreach (var item in cases)
        {
            object?[] arguments = [item.Definition, item.Value, null];
            await Assert.That((bool)method.Invoke(null, arguments)!).IsEqualTo(item.Expected);
        }
    }

    /// <summary>Creates a logical client with a contiguous two-word range.</summary>
    /// <param name="owner">The Mitsubishi owner.</param>
    /// <returns>The populated logical client.</returns>
    private static MitsubishiLogicalTagClient CreateWordLogicalClient(MitsubishiRx owner)
    {
        var logical = owner.CreateLogicalTagClient(null, null, null);
        logical.RegisterRange(
        [
            new LogicalTag("D10", "D10", UInt16DataType),
            new LogicalTag("D11", "D11", UInt16DataType),
        ]);
        return logical;
    }

    /// <summary>Creates deterministic Ethernet MC options with random command support.</summary>
    /// <returns>The client options.</returns>
    private static MitsubishiClientOptions CreateOptions() =>
        new(
            "127.0.0.1",
            Port,
            MitsubishiFrameType.ThreeE,
            CommunicationDataCode.Binary,
            MitsubishiTransportKind.Tcp);
}
