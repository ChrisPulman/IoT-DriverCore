// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.Driver.Core;

#if REACTIVE_SHIM

namespace IoT.Driver.MitsubishiRx.Reactive.Tests;

#else

namespace IoT.Driver.MitsubishiRx.Tests;

#endif

/// <summary>Verifies memory-area-aware grouped logical reads and writes.</summary>
internal sealed class MitsubishiLogicalTagClientBulkTests
{
    /// <summary>Stores the loopback host.</summary>
    private const string LoopbackHost = "127.0.0.1";

    /// <summary>Stores the deterministic test port.</summary>
    private const int LoopbackPort = 5000;

    /// <summary>Stores the signed word data type.</summary>
    private const string Int16DataType = "Int16";

    /// <summary>Stores the unsigned word data type.</summary>
    private const string UInt16DataType = "UInt16";

    /// <summary>Stores the first contiguous tag name.</summary>
    private const string FirstTagName = "First";

    /// <summary>Stores the signed contiguous tag name.</summary>
    private const string SignedTagName = "Signed";

    /// <summary>Stores the last contiguous tag name.</summary>
    private const string LastTagName = "Last";

    /// <summary>Stores the signed word test value.</summary>
    private const short SignedWordValue = -2;

    /// <summary>Stores the first contiguous read value.</summary>
    private const ushort FirstReadValue = 100;

    /// <summary>Stores the last contiguous read value.</summary>
    private const ushort LastReadValue = 102;

    /// <summary>Stores the first contiguous write value.</summary>
    private const ushort FirstWriteValue = 1000;

    /// <summary>Stores the last contiguous write value.</summary>
    private const ushort LastWriteValue = 1002;

    /// <summary>Stores the D10 test value.</summary>
    private const ushort D10Value = 10;

    /// <summary>Stores the unused validation-only write value.</summary>
    private const ushort MissingWriteValue = 11;

    /// <summary>Stores the D20 test value.</summary>
    private const ushort D20Value = 20;

    /// <summary>Stores the read-only validation write value.</summary>
    private const ushort ReadOnlyWriteValue = 21;

    /// <summary>Stores the W1 test value.</summary>
    private const ushort W1Value = 33;

    /// <summary>Stores the expected number of grouped protocol requests.</summary>
    private const int GroupedRequestCount = 2;

    /// <summary>Stores the number of eligible items across two repeated contiguous batches.</summary>
    private const int RepeatedContiguousItemCount = 6;

    /// <summary>Stores the number of items combined by one random grouped request.</summary>
    private const int RandomGroupedItemCount = 3;

    /// <summary>Stores the expected indexed failure count.</summary>
    private const int IndexedFailureCount = 4;

    /// <summary>Verifies an out-of-order caller list becomes one sorted contiguous protocol request.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task ContiguousWordsUseOneRequestAndPreserveCallerOrderAsync()
    {
        var options = CreateOptions(MitsubishiFrameType.ThreeE);
        await using var simulator = new MitsubishiSimulatorTransport(
            request => MitsubishiSimulatorTransport.CreateSuccessResponse(
                options,
                request.Description.StartsWith("Read", StringComparison.Ordinal)
                    ? [0x64, 0x00, 0xFE, 0xFF, 0x66, 0x00]
                    : []));
        await using var owner = new MitsubishiRx(options, simulator, Scheduler.Immediate);
        using var logical = CreateLogicalClient(owner);

        var reads = await logical.ReadManyAsync(
            [LastTagName, FirstTagName, SignedTagName],
            CancellationToken.None);

        var readsSucceeded = true;
        foreach (var result in reads)
        {
            if (!result.Succeeded)
            {
                readsSucceeded = false;
                break;
            }
        }

        await Assert.That(readsSucceeded)
            .IsTrue();
        await Assert.That(reads[0].Value!.Value).IsEqualTo(LastReadValue);
        await Assert.That(reads[1].Value!.Value).IsEqualTo(FirstReadValue);
        await Assert.That(reads[2].Value!.Value).IsEqualTo(SignedWordValue);
        await Assert.That(simulator.Requests).Count().IsEqualTo(1);
        await Assert.That(simulator.Requests[0].Description).IsEqualTo("Read words D100");

        await AssertGappedWritesAsync(logical, simulator, options);
    }

    /// <summary>Verifies gaps use random commands while different device areas remain separate.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task GappedWordsUseRandomCommandsGroupedByMemoryAreaAsync()
    {
        var options = CreateOptions(MitsubishiFrameType.ThreeE);
        await using var simulator = new MitsubishiSimulatorTransport(
            request => MitsubishiSimulatorTransport.CreateSuccessResponse(
                options,
                GetGroupedReadPayload(request.Description)));
        await using var owner = new MitsubishiRx(options, simulator, Scheduler.Immediate);
        using var logical = owner.CreateLogicalTagClient(null, null, null);
        RegisterGappedTags(logical);

        await AssertGappedReadsAsync(logical, simulator);

        simulator.ClearRequests();
        var writes = await logical.WriteManyAsync(
        [
            CreateValue("D20", D20Value),
            CreateValue("W1", W1Value),
            CreateValue("D10", D10Value),
            CreateValue("D11", SignedWordValue),
        ],
            CancellationToken.None);

        var expectedRandom = MitsubishiProtocolEncoding.EncodeRandomWrite(
            options,
        [
            new(
                MitsubishiDeviceAddress.Parse("D20", options.XyNotation),
                D20Value),
            new(
                MitsubishiDeviceAddress.Parse("D10", options.XyNotation),
                D10Value),
            new(
                MitsubishiDeviceAddress.Parse("D11", options.XyNotation),
                unchecked((ushort)SignedWordValue)),
        ]);
        var writesSucceeded = true;
        foreach (var result in writes)
        {
            if (!result.Succeeded)
            {
                writesSucceeded = false;
                break;
            }
        }

        await Assert.That(writesSucceeded)
            .IsTrue();
        await Assert.That(simulator.Requests).Count().IsEqualTo(GroupedRequestCount);
        await Assert.That(simulator.Requests[0].Payload.SequenceEqual(expectedRandom)).IsTrue();
        await Assert.That(simulator.Requests[1].Description).IsEqualTo("Write words W1");
    }

    /// <summary>Verifies validation and transport failures retain exact caller indexes.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task BulkFailuresRetainCallerIndexesAsync()
    {
        var options = CreateOptions(MitsubishiFrameType.ThreeE);
        await using var simulator = new MitsubishiSimulatorTransport(
            _ => MitsubishiSimulatorTransport.CreateErrorResponse(options, 0xC051));
        await using var owner = new MitsubishiRx(options, simulator, Scheduler.Immediate);
        using var logical = owner.CreateLogicalTagClient(null, null, null);
        RegisterFailureTags(logical);

        var reads = await logical.ReadManyAsync(
            ["D10", "Missing", "D20", "WriteOnly"],
            CancellationToken.None);

        await AssertIndexedFailuresAsync(reads);
        await Assert.That(simulator.Requests).Count().IsEqualTo(1);

        simulator.ClearRequests();
        var writes = await logical.WriteManyAsync(
        [
            CreateValue("D10", D10Value),
            CreateValue("Missing", MissingWriteValue),
            CreateValue("D20", D20Value),
            CreateValue("ReadOnly", ReadOnlyWriteValue),
        ],
            CancellationToken.None);

        await AssertIndexedFailuresAsync(writes);
        await Assert.That(simulator.Requests).Count().IsEqualTo(1);
    }

    /// <summary>Verifies a legacy 1E frame uses safe contiguous fallbacks for address gaps.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task OneEFrameUsesContiguousFallbackForGappedWordsAsync()
    {
        var options = CreateOptions(MitsubishiFrameType.OneE);
        await using var simulator = new MitsubishiSimulatorTransport(
            request => MitsubishiSimulatorTransport.CreateSuccessResponse(
                options,
                GetOneEReadPayload(request.Description)));
        await using var owner = new MitsubishiRx(options, simulator, Scheduler.Immediate);
        using var logical = owner.CreateLogicalTagClient(null, null, null);
        logical.RegisterRange(
        [
            new LogicalTag("D10", "D10", UInt16DataType),
            new LogicalTag("D20", "D20", UInt16DataType),
        ]);

        var reads = await logical.ReadManyAsync(["D20", "D10"], CancellationToken.None);

        await Assert.That(reads[0].Value!.Value).IsEqualTo(D20Value);
        await Assert.That(reads[1].Value!.Value).IsEqualTo(D10Value);
        var requestDescriptions = new string[simulator.Requests.Count];
        for (var index = 0; index < simulator.Requests.Count; index++)
        {
            requestDescriptions[index] = simulator.Requests[index].Description;
        }

        await Assert.That(requestDescriptions).IsEquivalentTo(["Read words D10", "Read words D20"]);
    }

    /// <summary>
    /// Verifies repeated, shuffled contiguous batches create one deterministic plan and protocol
    /// call per invocation while preserving caller ordering.
    /// </summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [TUnit.Core.Test]
    internal async Task BulkMetricsProveContiguousPlanningAndStableOrderAcrossRepeatedCallsAsync()
    {
        var options = CreateOptions(MitsubishiFrameType.ThreeE);
        await using var simulator = new MitsubishiSimulatorTransport(
            request => MitsubishiSimulatorTransport.CreateSuccessResponse(
                options,
                request.Description.StartsWith("Read", StringComparison.Ordinal)
                    ? [0x64, 0x00, 0xFE, 0xFF, 0x66, 0x00]
                    : []));
        await using var owner = new MitsubishiRx(options, simulator, Scheduler.Immediate);
        using var logical = CreateLogicalClient(owner);

        var first = await logical.ReadManyAsync(
            [LastTagName, FirstTagName, SignedTagName],
            CancellationToken.None);
        var second = await logical.ReadManyAsync(
            [SignedTagName, LastTagName, FirstTagName],
            CancellationToken.None);
        var metrics = logical.BulkOperationMetrics;

        var firstTagNames = new string[first.Count];
        for (var index = 0; index < first.Count; index++)
        {
            firstTagNames[index] = first[index].Value!.TagName;
        }

        var secondTagNames = new string[second.Count];
        for (var index = 0; index < second.Count; index++)
        {
            secondTagNames[index] = second[index].Value!.TagName;
        }

        await TUnit.Assertions.Assert.That(firstTagNames)
            .IsEquivalentTo([LastTagName, FirstTagName, SignedTagName]);
        await TUnit.Assertions.Assert.That(secondTagNames)
            .IsEquivalentTo([SignedTagName, LastTagName, FirstTagName]);
        await TUnit.Assertions.Assert.That(metrics.Read.PlanCount).IsEqualTo(GroupedRequestCount);
        await TUnit.Assertions.Assert.That(metrics.Read.ItemCount).IsEqualTo(RepeatedContiguousItemCount);
        await TUnit.Assertions.Assert.That(metrics.Read.RangeCount).IsEqualTo(GroupedRequestCount);
        await TUnit.Assertions.Assert.That(metrics.Read.ProtocolCallCount).IsEqualTo(GroupedRequestCount);
        await TUnit.Assertions.Assert.That(simulator.Requests).Count().IsEqualTo(GroupedRequestCount);
    }

    /// <summary>
    /// Verifies random grouped calls reduce protocol dispatches below both tag and planned-range
    /// counts without using elapsed-time thresholds.
    /// </summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [TUnit.Core.Test]
    internal async Task BulkMetricsProveRandomGroupingReducesProtocolCallsAsync()
    {
        var options = CreateOptions(MitsubishiFrameType.ThreeE);
        await using var simulator = new MitsubishiSimulatorTransport(
            request => MitsubishiSimulatorTransport.CreateSuccessResponse(
                options,
                GetGroupedReadPayload(request.Description)));
        await using var owner = new MitsubishiRx(options, simulator, Scheduler.Immediate);
        using var logical = owner.CreateLogicalTagClient(null, null, null);
        logical.RegisterRange(
        [
            new LogicalTag("D20", "D20", UInt16DataType),
            new LogicalTag("D10", "D10", UInt16DataType),
            new LogicalTag("D11", "D11", Int16DataType),
        ]);

        var reads = await logical.ReadManyAsync(["D20", "D10", "D11"], CancellationToken.None);
        var metrics = logical.BulkOperationMetrics;

        var readsSucceeded = true;
        foreach (var result in reads)
        {
            if (!result.Succeeded)
            {
                readsSucceeded = false;
                break;
            }
        }

        await TUnit.Assertions.Assert.That(readsSucceeded)
            .IsTrue();
        await TUnit.Assertions.Assert.That(metrics.Read.ItemCount).IsEqualTo(RandomGroupedItemCount);
        await TUnit.Assertions.Assert.That(metrics.Read.RangeCount).IsEqualTo(GroupedRequestCount);
        await TUnit.Assertions.Assert.That(metrics.Read.ProtocolCallCount).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(simulator.Requests).Count().IsEqualTo(1);
    }

    /// <summary>Verifies gapped bulk writes issue a random command and a separate-area command.</summary>
    /// <param name="logical">The logical tag client.</param>
    /// <param name="simulator">The deterministic simulator transport.</param>
    /// <param name="options">The Mitsubishi client options.</param>
    /// <returns>A task that represents the asynchronous assertion.</returns>
    private static async Task AssertGappedWritesAsync(
        MitsubishiLogicalTagClient logical,
        MitsubishiSimulatorTransport simulator,
        MitsubishiClientOptions options)
    {
        simulator.ClearRequests();
        var writes = await logical.WriteManyAsync(
        [
            CreateValue(LastTagName, LastWriteValue),
            CreateValue(FirstTagName, FirstWriteValue),
            CreateValue(SignedTagName, SignedWordValue),
        ],
            CancellationToken.None);

        var expected = MitsubishiProtocolEncoding.EncodeDeviceBatchWrite(
            options,
            MitsubishiDeviceAddress.Parse("D100", options.XyNotation),
            [FirstWriteValue, unchecked((ushort)SignedWordValue), LastWriteValue],
            bitUnits: false);
        var writesSucceeded = true;
        foreach (var result in writes)
        {
            if (!result.Succeeded)
            {
                writesSucceeded = false;
                break;
            }
        }

        await Assert.That(writesSucceeded)
            .IsTrue();
        await Assert.That(simulator.Requests).Count().IsEqualTo(1);
        await Assert.That(simulator.Requests[0].Payload.SequenceEqual(expected)).IsTrue();
    }

    /// <summary>Verifies the gapped read values and command grouping.</summary>
    /// <param name="logical">The logical tag client.</param>
    /// <param name="simulator">The deterministic simulator transport.</param>
    /// <returns>A task that represents the asynchronous assertion.</returns>
    private static async Task AssertGappedReadsAsync(
        MitsubishiLogicalTagClient logical,
        MitsubishiSimulatorTransport simulator)
    {
        var reads = await logical.ReadManyAsync(
            ["D20", "W1", "D10", "D11"],
            CancellationToken.None);
        var readsSucceeded = true;
        foreach (var result in reads)
        {
            if (!result.Succeeded)
            {
                readsSucceeded = false;
                break;
            }
        }

        await Assert.That(readsSucceeded).IsTrue();
        await Assert.That(reads[0].Value!.Value).IsEqualTo(D20Value);
        await Assert.That(reads[1].Value!.Value).IsEqualTo(W1Value);
        await Assert.That(reads[2].Value!.Value).IsEqualTo(D10Value);
        await Assert.That(reads[3].Value!.Value).IsEqualTo(SignedWordValue);
        var requestDescriptions = new string[simulator.Requests.Count];
        for (var index = 0; index < simulator.Requests.Count; index++)
        {
            requestDescriptions[index] = simulator.Requests[index].Description;
        }

        await Assert.That(requestDescriptions).IsEquivalentTo(["Random read words", "Read words W1"]);
    }

    /// <summary>Creates deterministic Ethernet MC options.</summary>
    /// <param name="frameType">The MC frame type.</param>
    /// <returns>The client options.</returns>
    private static MitsubishiClientOptions CreateOptions(MitsubishiFrameType frameType) =>
        new(
            LoopbackHost,
            LoopbackPort,
            frameType,
            CommunicationDataCode.Binary,
            MitsubishiTransportKind.Tcp);

    /// <summary>Creates and populates the contiguous-tag client.</summary>
    /// <param name="owner">The Mitsubishi owner.</param>
    /// <returns>The populated logical client.</returns>
    private static MitsubishiLogicalTagClient CreateLogicalClient(MitsubishiRx owner)
    {
        var logical = owner.CreateLogicalTagClient(null, null, null);
        logical.RegisterRange(
        [
            new LogicalTag(LastTagName, "D102", UInt16DataType),
            new LogicalTag(FirstTagName, "D100", UInt16DataType),
            new LogicalTag(SignedTagName, "D101", Int16DataType),
        ]);
        return logical;
    }

    /// <summary>Registers tags spanning gaps and Mitsubishi word memory areas.</summary>
    /// <param name="logical">The logical client.</param>
    private static void RegisterGappedTags(MitsubishiLogicalTagClient logical) =>
        logical.RegisterRange(
        [
            new LogicalTag("D20", "D20", UInt16DataType),
            new LogicalTag("W1", "W1", UInt16DataType),
            new LogicalTag("D10", "D10", UInt16DataType),
            new LogicalTag("D11", "D11", Int16DataType),
        ]);

    /// <summary>Registers tags used to verify indexed failure correlation.</summary>
    /// <param name="logical">The logical client.</param>
    private static void RegisterFailureTags(MitsubishiLogicalTagClient logical) =>
        logical.RegisterRange(
        [
            new LogicalTag("D10", "D10", UInt16DataType),
            new LogicalTag("D20", "D20", UInt16DataType),
            new LogicalTag(
                "ReadOnly",
                "D30",
                UInt16DataType,
                new LogicalTagOptions { AccessMode = LogicalTagAccessMode.Read }),
            new LogicalTag(
                "WriteOnly",
                "D40",
                UInt16DataType,
                new LogicalTagOptions { AccessMode = LogicalTagAccessMode.Write }),
        ]);

    /// <summary>Returns the decoded payload required by one grouped read request.</summary>
    /// <param name="description">The captured transport description.</param>
    /// <returns>The decoded protocol payload.</returns>
    private static byte[] GetGroupedReadPayload(string description) =>
        description switch
        {
            "Random read words" => [0x14, 0x00, 0x0A, 0x00, 0xFE, 0xFF],
            "Read words W1" => [0x21, 0x00],
            _ => [],
        };

    /// <summary>Returns the decoded payload required by a legacy contiguous read request.</summary>
    /// <param name="description">The captured transport description.</param>
    /// <returns>The decoded protocol payload.</returns>
    private static byte[] GetOneEReadPayload(string description)
    {
        if (description.EndsWith("D10", StringComparison.Ordinal))
        {
            return [0x0A, 0x00];
        }

        return description.StartsWith("Read", StringComparison.Ordinal)
            ? [0x14, 0x00]
            : [];
    }

    /// <summary>Creates a logical value at the Unix epoch.</summary>
    /// <param name="tagName">The logical tag name.</param>
    /// <param name="value">The declared value.</param>
    /// <returns>The logical value.</returns>
    private static LogicalTagValue CreateValue(string tagName, object value) =>
        new(tagName, value, DateTimeOffset.UnixEpoch);

    /// <summary>Asserts that every result failed and retained its exact caller index.</summary>
    /// <param name="results">The results to inspect.</param>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    private static async Task AssertIndexedFailuresAsync(
        IReadOnlyList<TagOperationResult<LogicalTagValue>> results)
    {
        await Assert.That(results).Count().IsEqualTo(IndexedFailureCount);
        for (var index = 0; index < results.Count; index++)
        {
            await Assert.That(results[index].Succeeded).IsFalse();
            await Assert.That(
                results[index].Error.Contains(
                    $"[{index}]",
                    StringComparison.Ordinal)).IsTrue();
        }
    }
}
