// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
using IoT.DriverCore.OmronPlcRx.Core;
using IoT.DriverCore.OmronPlcRx.Enums;
using IoT.DriverCore.OmronPlcRx.Tags;
using TUnit.Core;

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>Verifies grouped logical-tag operations use native contiguous FINS transfers.</summary>
public sealed class OmronLogicalTagBulkTests
{
    /// <summary>Gets the local FINS node identifier.</summary>
    private const byte LocalNode = 1;

    /// <summary>Gets the remote FINS node identifier.</summary>
    private const byte RemoteNode = 2;

    /// <summary>Gets the deterministic request timeout.</summary>
    private const int TimeoutMilliseconds = 100;

    /// <summary>Gets the first word value.</summary>
    private const short FirstValue = 11;

    /// <summary>Gets the second word value.</summary>
    private const short SecondValue = 22;

    /// <summary>Gets the third word value.</summary>
    private const short ThirdValue = 33;

    /// <summary>Gets the number of requested tags in order-preservation tests.</summary>
    private const int ExpectedTagCount = 3;

    /// <summary>Gets the number of native transfers expected for one compatible range.</summary>
    private const int ExpectedSingleTransfer = 1;

    /// <summary>Gets the word offset of the third adjacent tag.</summary>
    private const int ThirdAddressOffset = 2;

    /// <summary>Gets a deterministic 32-bit value spanning two FINS words.</summary>
    private const int MultiWordValue = 65_538;

    /// <summary>Gets the low-word byte used by the deterministic multi-word value.</summary>
    private const byte MultiWordLowByte = 2;

    /// <summary>Gets a deterministic fixed string value.</summary>
    private const string TextValue = "AB";

    /// <summary>Gets the first logical tag name.</summary>
    private const string FirstTag = "First";

    /// <summary>Gets the second logical tag name.</summary>
    private const string SecondTag = "Second";

    /// <summary>Gets the third logical tag name.</summary>
    private const string ThirdTag = "Third";

    /// <summary>Verifies adjacent word reads coalesce while exact caller order is retained.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReadManyAsync_CoalescesAdjacentWordsAndPreservesInputOrder()
    {
        var channel = new CoreProtocolCoverageTests.TestChannel();
        channel.SetResponseData(
        [
            0, (byte)FirstValue,
            0, (byte)SecondValue,
            0, (byte)ThirdValue,
        ]);
        using var connection = CreateConnection(channel);
        using var driver = NewDriver(connection);
        using var client = CreateWordClient(driver, "D100");

        var results = await client.ReadManyAsync(
            [ThirdTag, FirstTag, SecondTag],
            CancellationToken.None);

        await Assert.That(results.Count).IsEqualTo(ExpectedTagCount);
        await Assert.That(results.All(static result => result.Succeeded)).IsTrue();
        await Assert.That((short)results[0].Value!.Value!).IsEqualTo(ThirdValue);
        await Assert.That((short)results[1].Value!.Value!).IsEqualTo(FirstValue);
        await Assert.That((short)results[2].Value!.Value!).IsEqualTo(SecondValue);
        await Assert.That(channel.SendCount).IsEqualTo(ExpectedSingleTransfer);
        await Assert.That(driver.GetValue(new LogicalTagKey<short>(ThirdTag))).IsEqualTo(ThirdValue);
    }

    /// <summary>Verifies adjacent word writes coalesce and update typed facade caches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WriteManyAsync_CoalescesAdjacentWordsAndUpdatesCaches()
    {
        var channel = new CoreProtocolCoverageTests.TestChannel();
        using var connection = CreateConnection(channel);
        using var driver = NewDriver(connection);
        using var client = CreateWordClient(driver, "D200");
        var timestamp = TimeProvider.System.GetUtcNow();

        var results = await client.WriteManyAsync(
        [
            new LogicalTagValue(ThirdTag, ThirdValue, timestamp),
            new LogicalTagValue(FirstTag, FirstValue, timestamp),
            new LogicalTagValue(SecondTag, SecondValue, timestamp),
        ],
            CancellationToken.None);

        await Assert.That(results.Count).IsEqualTo(ExpectedTagCount);
        await Assert.That(results.All(static result => result.Succeeded)).IsTrue();
        await Assert.That((short)results[0].Value!.Value!).IsEqualTo(ThirdValue);
        await Assert.That((short)results[1].Value!.Value!).IsEqualTo(FirstValue);
        await Assert.That((short)results[2].Value!.Value!).IsEqualTo(SecondValue);
        await Assert.That(channel.SendCount).IsEqualTo(ExpectedSingleTransfer);
        await Assert.That(driver.GetValue(new LogicalTagKey<short>(FirstTag))).IsEqualTo(FirstValue);
        await Assert.That(driver.GetValue(new LogicalTagKey<short>(SecondTag))).IsEqualTo(SecondValue);
        await Assert.That(driver.GetValue(new LogicalTagKey<short>(ThirdTag))).IsEqualTo(ThirdValue);
    }

    /// <summary>Verifies compatible bit reads use one native within-word FINS range.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReadManyAsync_CoalescesBitsWithinOneWord()
    {
        var channel = new CoreProtocolCoverageTests.TestChannel();
        channel.SetResponseData([1, 0, 1]);
        using var connection = CreateConnection(channel);
        using var driver = NewDriver(connection);
        using var client = new OmronLogicalTagClient(driver);
        _ = client.CreateTag(new PlcTag<bool>(FirstTag, "D300.1"));
        _ = client.CreateTag(new PlcTag<bool>(SecondTag, "D300.2"));
        _ = client.CreateTag(new PlcTag<bool>(ThirdTag, "D300.3"));

        var results = await client.ReadManyAsync(
            [ThirdTag, FirstTag, SecondTag],
            CancellationToken.None);

        await Assert.That(results.All(static result => result.Succeeded)).IsTrue();
        await Assert.That((bool)results[0].Value!.Value!).IsTrue();
        await Assert.That((bool)results[1].Value!.Value!).IsTrue();
        await Assert.That((bool)results[2].Value!.Value!).IsFalse();
        await Assert.That(channel.SendCount).IsEqualTo(ExpectedSingleTransfer);
    }

    /// <summary>Verifies compatible bit writes use one native within-word FINS range.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WriteManyAsync_CoalescesBitsWithinOneWord()
    {
        var channel = new CoreProtocolCoverageTests.TestChannel();
        using var connection = CreateConnection(channel);
        using var driver = NewDriver(connection);
        using var client = new OmronLogicalTagClient(driver);
        _ = client.CreateTag(new PlcTag<bool>(FirstTag, "D310.1"));
        _ = client.CreateTag(new PlcTag<bool>(SecondTag, "D310.2"));
        _ = client.CreateTag(new PlcTag<bool>(ThirdTag, "D310.3"));
        var timestamp = TimeProvider.System.GetUtcNow();

        var results = await client.WriteManyAsync(
        [
            new LogicalTagValue(ThirdTag, true, timestamp),
            new LogicalTagValue(FirstTag, false, timestamp),
            new LogicalTagValue(SecondTag, true, timestamp),
        ],
            CancellationToken.None);

        await Assert.That(results.All(static result => result.Succeeded)).IsTrue();
        await Assert.That(channel.SendCount).IsEqualTo(ExpectedSingleTransfer);
        await Assert.That(driver.GetValue(new LogicalTagKey<bool>(FirstTag))).IsFalse();
        await Assert.That(driver.GetValue(new LogicalTagKey<bool>(SecondTag))).IsTrue();
        await Assert.That(driver.GetValue(new LogicalTagKey<bool>(ThirdTag))).IsTrue();
    }

    /// <summary>Verifies mixed word-encoded types share one native contiguous read.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReadManyAsync_CoalescesMixedWordEncodedTypes()
    {
        var channel = new CoreProtocolCoverageTests.TestChannel();
        channel.SetResponseData(
        [
            0, (byte)FirstValue,
            0, MultiWordLowByte,
            0, 1,
            (byte)'A', (byte)'B',
            0, 0,
            0, 1,
        ]);
        using var connection = CreateConnection(channel);
        using var driver = NewDriver(connection);
        using var client = new OmronLogicalTagClient(driver);
        _ = client.CreateTag(new PlcTag<short>(FirstTag, "D500"));
        _ = client.CreateTag(new PlcTag<int>(SecondTag, "D501"));
        _ = client.CreateTag(new PlcTag<string>(ThirdTag, "D503[4]"));
        _ = client.CreateTag(new PlcTag<bool>("Enabled", "D505"));

        var results = await client.ReadManyAsync(
            ["Enabled", ThirdTag, FirstTag, SecondTag],
            CancellationToken.None);

        await Assert.That(results.All(static result => result.Succeeded)).IsTrue();
        await Assert.That((bool)results[0].Value!.Value!).IsTrue();
        await Assert.That((string)results[1].Value!.Value!).IsEqualTo(TextValue);
        await Assert.That((short)results[2].Value!.Value!).IsEqualTo(FirstValue);
        await Assert.That((int)results[3].Value!.Value!).IsEqualTo(MultiWordValue);
        await Assert.That(channel.SendCount).IsEqualTo(ExpectedSingleTransfer);
    }

    /// <summary>Verifies mixed word-encoded types share one native contiguous write.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WriteManyAsync_CoalescesMixedWordEncodedTypes()
    {
        var channel = new CoreProtocolCoverageTests.TestChannel();
        using var connection = CreateConnection(channel);
        using var driver = NewDriver(connection);
        using var client = new OmronLogicalTagClient(driver);
        _ = client.CreateTag(new PlcTag<int>(FirstTag, "D600"));
        _ = client.CreateTag(new PlcTag<string>(SecondTag, "D602[4]"));
        _ = client.CreateTag(new PlcTag<bool>(ThirdTag, "D604"));
        var timestamp = TimeProvider.System.GetUtcNow();

        var results = await client.WriteManyAsync(
        [
            new LogicalTagValue(ThirdTag, true, timestamp),
            new LogicalTagValue(FirstTag, MultiWordValue, timestamp),
            new LogicalTagValue(SecondTag, TextValue, timestamp),
        ],
            CancellationToken.None);

        await Assert.That(results.All(static result => result.Succeeded)).IsTrue();
        await Assert.That(channel.SendCount).IsEqualTo(ExpectedSingleTransfer);
        await Assert.That(driver.GetValue(new LogicalTagKey<int>(FirstTag))).IsEqualTo(MultiWordValue);
        await Assert.That(driver.GetValue(new LogicalTagKey<string>(SecondTag))).IsEqualTo(TextValue);
        await Assert.That(driver.GetValue(new LogicalTagKey<bool>(ThirdTag))).IsTrue();
    }

    /// <summary>Verifies validation failures retain positions without preventing valid transfers.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReadManyAsync_RetainsPerItemValidationFailures()
    {
        var channel = new CoreProtocolCoverageTests.TestChannel();
        channel.SetResponseData([0, (byte)FirstValue]);
        using var connection = CreateConnection(channel);
        using var driver = NewDriver(connection);
        using var client = new OmronLogicalTagClient(driver);
        _ = client.CreateTag(new PlcTag<short>(FirstTag, "Z100"));
        _ = client.CreateTag(new PlcTag<short>(SecondTag, "D100"));

        var results = await client.ReadManyAsync(
            [FirstTag, SecondTag, "Missing"],
            CancellationToken.None);

        await Assert.That(results.Count).IsEqualTo(ExpectedTagCount);
        await Assert.That(results[0].Succeeded).IsFalse();
        await Assert.That(results[0].Error).Contains("Unsupported word area");
        await Assert.That(results[1].Succeeded).IsTrue();
        await Assert.That((short)results[1].Value!.Value!).IsEqualTo(FirstValue);
        await Assert.That(results[2].Succeeded).IsFalse();
        await Assert.That(results[2].Error).Contains("not registered");
        await Assert.That(channel.SendCount).IsEqualTo(ExpectedSingleTransfer);
    }

    /// <summary>Verifies one native range failure is reported independently for each affected item.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReadManyAsync_ReportsNativeRangeFailurePerItem()
    {
        var channel = new CoreProtocolCoverageTests.TestChannel();
        channel.SetResponseData([0, (byte)FirstValue]);
        using var connection = CreateConnection(channel);
        using var driver = NewDriver(connection);
        using var client = CreateWordClient(driver, "D400");

        var results = await client.ReadManyAsync(
            [ThirdTag, FirstTag, SecondTag],
            CancellationToken.None);

        await Assert.That(results.Count).IsEqualTo(ExpectedTagCount);
        await Assert.That(results.All(static result => !result.Succeeded)).IsTrue();
        await Assert.That(results.All(static result => result.Error.Contains("too short"))).IsTrue();
        await Assert.That(channel.SendCount).IsEqualTo(ExpectedSingleTransfer);
    }

    /// <summary>Creates a logical client containing three adjacent word tags.</summary>
    /// <param name="driver">Production Omron facade.</param>
    /// <param name="baseAddress">Starting Omron word address.</param>
    /// <returns>The configured logical client.</returns>
    private static OmronLogicalTagClient CreateWordClient(
        OmronPlcRx driver,
        string baseAddress)
    {
        var prefix = baseAddress[0];
        var start = ushort.Parse(baseAddress[1..]);
        var client = new OmronLogicalTagClient(driver);
        _ = client.CreateTag(new PlcTag<short>(FirstTag, $"{prefix}{start}"));
        _ = client.CreateTag(new PlcTag<short>(SecondTag, $"{prefix}{start + 1}"));
        _ = client.CreateTag(
            new PlcTag<short>(ThirdTag, $"{prefix}{start + ThirdAddressOffset}"));
        return client;
    }

    /// <summary>Creates an initialized connection over a deterministic channel.</summary>
    /// <param name="channel">Injected deterministic channel.</param>
    /// <returns>The initialized FINS connection.</returns>
    private static OmronPLCConnection CreateConnection(
        CoreProtocolCoverageTests.TestChannel channel) =>
        new(
            new OmronConnectionOptions(LocalNode, RemoteNode, ConnectionMethod.UDP, "127.0.0.1")
            {
                Timeout = TimeoutMilliseconds,
                Retries = 0,
            },
            channel,
            PlcType.CJ2,
            "CJ2M",
            "1.0",
            true);

    /// <summary>Creates a production facade with deterministic direct-operation control.</summary>
    /// <param name="connection">Injected FINS connection.</param>
    /// <returns>The production facade.</returns>
    private static OmronPlcRx NewDriver(OmronPLCConnection connection) =>
        new(connection, TimeSpan.FromDays(1), false);
}
