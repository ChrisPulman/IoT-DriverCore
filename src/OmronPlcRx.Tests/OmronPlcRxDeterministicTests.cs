// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
using IoT.DriverCore.OmronPlcRx.Core;
using IoT.DriverCore.OmronPlcRx.Core.Types;
using IoT.DriverCore.OmronPlcRx.Enums;
using IoT.DriverCore.OmronPlcRx.Tags;
using ReactiveUI.Primitives;
using TUnit.Core;

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>Exercises the production Omron facade over an injected deterministic FINS channel.</summary>
public sealed class OmronPlcRxDeterministicTests
{
    /// <summary>Gets the local FINS node identifier.</summary>
    private const byte LocalNode = 1;

    /// <summary>Gets the remote FINS node identifier.</summary>
    private const byte RemoteNode = 2;

    /// <summary>Gets the deterministic request timeout.</summary>
    private const int TimeoutMilliseconds = 100;

    /// <summary>Gets the expected converted integer value.</summary>
    private const int IntegerValue = 65_538;

    /// <summary>Gets the expected short value.</summary>
    private const short ShortValue = 42;

    /// <summary>Gets the expected string value.</summary>
    private const string TextValue = "AB";

    /// <summary>Gets the logical short tag name.</summary>
    private const string ShortTagName = "Short";

    /// <summary>Gets the logical integer tag name.</summary>
    private const string IntegerTagName = "Integer";

    /// <summary>Gets the logical bit tag name.</summary>
    private const string BitTagName = "Bit";

    /// <summary>Gets the logical string tag name.</summary>
    private const string StringTagName = "Text";

    /// <summary>Gets the logical bit write tag name.</summary>
    private const string BoolBitWriteTagName = "BoolBit";

    /// <summary>Gets the bracketed word tag name.</summary>
    private const string BracketedWordTagName = "BracketedWord";

    /// <summary>Gets the logical string write tag name.</summary>
    private const string StringWriteTagName = "String";

    /// <summary>Gets the low word byte in the deterministic integer response.</summary>
    private const byte IntegerLowWordByte = 2;

    /// <summary>Gets the expected number of typed read changes.</summary>
    private const int ExpectedReadChanges = 4;

    /// <summary>Gets the expected number of supported write operations.</summary>
    private const int ExpectedWriteCount = 14;

    /// <summary>Gets the deterministic single-precision write value.</summary>
    private const float SingleValue = 1.25F;

    /// <summary>Gets the deterministic double-precision write value.</summary>
    private const double DoubleValue = 2.5D;

    /// <summary>Gets the deterministic 16-bit BCD value.</summary>
    private const short Bcd16Value = 1234;

    /// <summary>Gets the deterministic unsigned 16-bit BCD value.</summary>
    private const ushort BcdU16Value = 1234;

    /// <summary>Gets the deterministic 32-bit BCD value.</summary>
    private const int Bcd32Value = 123_456;

    /// <summary>Gets the deterministic unsigned 32-bit BCD value.</summary>
    private const uint BcdU32Value = 123_456;

    /// <summary>Gets the byte write tag name.</summary>
    private static readonly string ByteTagName = typeof(byte).FullName!;

    /// <summary>Gets the double write tag name.</summary>
    private static readonly string DoubleTagName = typeof(double).FullName!;

    /// <summary>Verifies typed reads traverse address parsing, FINS framing, caching, and observations.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Driver_ReadsTypedValuesThroughInjectedChannelAsync()
    {
        var channel = new CoreProtocolCoverageTests.TestChannel();
        using var connection = CreateConnection(channel);
        using var driver = NewDriver(connection);
        var changes = new List<IPlcTag?>();
        using var allSubscription = driver.ObserveAll.SubscribeSafe(
            changes.Add,
            static error => throw error);

        var shortTag = new PlcTag<short>(ShortTagName, "D100");
        var integerTag = new PlcTag<int>(IntegerTagName, "DM101");
        var bitTag = new PlcTag<bool>(BitTagName, "CIO10.3");
        var stringTag = new PlcTag<string>(StringTagName, "W20[4]");
        driver.AddUpdateTagItem(shortTag);
        driver.AddUpdateTagItem(integerTag);
        driver.AddUpdateTagItem(bitTag);
        driver.AddUpdateTagItem(stringTag);
        var observedShorts = new List<short>();
        using var shortSubscription = driver
            .Observe(new LogicalTagKey<short>(ShortTagName))
            .SubscribeSafe(observedShorts.Add, static error => throw error);

        channel.SetResponseData([0, (byte)ShortValue]);
        var shortValue = await driver.ReadValueAsync(
            new LogicalTagKey<short>(ShortTagName),
            CancellationToken.None);
        channel.SetResponseData([0, IntegerLowWordByte, 0, 1]);
        var integerValue = await driver.ReadValueAsync(
            new LogicalTagKey<int>(IntegerTagName),
            CancellationToken.None);
        channel.SetResponseData([1]);
        var bitValue = await driver.ReadValueAsync(
            new LogicalTagKey<bool>(BitTagName),
            CancellationToken.None);
        channel.SetResponseData([(byte)'A', (byte)'B', 0, 0]);
        var stringValue = await driver.ReadValueAsync(
            new LogicalTagKey<string>(StringTagName),
            CancellationToken.None);

        await Assert.That(shortValue).IsEqualTo(ShortValue);
        await Assert.That(integerValue).IsEqualTo(IntegerValue);
        await Assert.That(bitValue).IsTrue();
        await Assert.That(stringValue).IsEqualTo(TextValue);
        await Assert.That(driver.GetValue(new LogicalTagKey<short>(ShortTagName))).IsEqualTo(ShortValue);
        await Assert.That(observedShorts.Contains(ShortValue)).IsTrue();
        await Assert.That(changes.Count).IsEqualTo(ExpectedReadChanges);
        await Assert.That(driver.PlcType).IsEqualTo(PlcType.CJ2);
        await Assert.That(driver.ControllerModel).IsEqualTo("CJ2M");
        await Assert.That(driver.ControllerVersion).IsEqualTo("1.0");
    }

    /// <summary>Verifies every supported write conversion traverses the injected FINS channel.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Driver_WritesSupportedTypesThroughInjectedChannelAsync()
    {
        var channel = new CoreProtocolCoverageTests.TestChannel();
        using var connection = CreateConnection(channel);
        using var driver = NewDriver(connection);
        AddWriteTags(driver);

        await driver.WriteValueAsync(
            new LogicalTagKey<bool>(BoolBitWriteTagName),
            true,
            CancellationToken.None);
        await driver.WriteValueAsync(new LogicalTagKey<bool>("BoolWord"), true, CancellationToken.None);
        await driver.WriteValueAsync(new LogicalTagKey<byte>(ByteTagName), byte.MaxValue, CancellationToken.None);
        await driver.WriteValueAsync(new LogicalTagKey<short>("Short"), short.MinValue, CancellationToken.None);
        await driver.WriteValueAsync(new LogicalTagKey<ushort>("UShort"), ushort.MaxValue, CancellationToken.None);
        await driver.WriteValueAsync(new LogicalTagKey<int>("Int"), int.MinValue, CancellationToken.None);
        await driver.WriteValueAsync(new LogicalTagKey<uint>("UInt"), uint.MaxValue, CancellationToken.None);
        await driver.WriteValueAsync(new LogicalTagKey<float>("Float"), SingleValue, CancellationToken.None);
        await driver.WriteValueAsync(new LogicalTagKey<double>(DoubleTagName), DoubleValue, CancellationToken.None);
        await driver.WriteValueAsync(
            new LogicalTagKey<string>(StringWriteTagName),
            TextValue,
            CancellationToken.None);
        await driver.WriteValueAsync(
            new LogicalTagKey<Bcd16>(nameof(Bcd16)),
            new Bcd16(Bcd16Value),
            CancellationToken.None);
        await driver.WriteValueAsync(
            new LogicalTagKey<BcdU16>(nameof(BcdU16)),
            new BcdU16(BcdU16Value),
            CancellationToken.None);
        await driver.WriteValueAsync(
            new LogicalTagKey<Bcd32>(nameof(Bcd32)),
            new Bcd32(Bcd32Value),
            CancellationToken.None);
        await driver.WriteValueAsync(
            new LogicalTagKey<BcdU32>(nameof(BcdU32)),
            new BcdU32(BcdU32Value),
            CancellationToken.None);

        await Assert.That(channel.SendCount >= ExpectedWriteCount).IsTrue();
        await Assert.That(driver.GetValue(new LogicalTagKey<string>(StringWriteTagName))).IsEqualTo(TextValue);
        await Assert.That(driver.GetValue(new LogicalTagKey<bool>(BoolBitWriteTagName))).IsTrue();
    }

    /// <summary>Verifies address, type, registration, constructor, and lifecycle validation paths.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Driver_RejectsInvalidTagsAndLifecycleOperationsAsync()
    {
        var channel = new CoreProtocolCoverageTests.TestChannel();
        using var connection = CreateConnection(channel);
        var driver = NewDriver(connection);

        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(() => driver.AddUpdateTagItem((PlcTag<int>)null!)));
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(() => driver.Observe<int>(null!)));
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(() => driver.GetValue<int>(null!)));
        await AssertThrowsAsync<ArgumentNullException>(
            () => driver.ReadValueAsync<int>(null!, CancellationToken.None));
        await AssertThrowsAsync<ArgumentNullException>(
            () => driver.WriteValueAsync(null!, 1, CancellationToken.None));
        await AssertThrowsAsync<KeyNotFoundException>(
            () => driver.ReadValueAsync(new LogicalTagKey<int>("Missing"), CancellationToken.None));
        await AssertThrowsAsync<KeyNotFoundException>(
            () => driver.WriteValueAsync(new LogicalTagKey<int>("Missing"), 1, CancellationToken.None));
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(() => driver.RemoveTagItem(null!)));
        await AssertThrowsAsync<ArgumentException>(
            () => Task.Run(() => driver.RemoveTagItem(" ")));

        await AssertInvalidReadAsync<int>(driver, "NoNumber", "D");
        await AssertInvalidReadAsync<int>(driver, "BadNumber", "D999999");
        await AssertInvalidReadAsync<bool>(driver, "BadBit", "D10.16");
        await AssertInvalidReadAsync<bool>(driver, "BadBitText", "D10.X");
        await AssertInvalidReadAsync<int>(driver, "BadArea", "Z10");
        await AssertInvalidReadAsync<string>(driver, "BitString", "D10.1[4]");
        await AssertInvalidReadAsync<DateTime>(driver, "BadType", "D10");

        await Assert.That(driver.RemoveTagItem("BadType")).IsTrue();
        driver.Dispose();
        driver.Dispose();
        await Assert.That(driver.IsDisposed).IsTrue();
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.FromResult(new OmronPlcRx(
                (OmronPLCConnection)null!,
                TimeSpan.FromSeconds(1))));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => Task.FromResult(new OmronPlcRx(connection, TimeSpan.Zero)));
    }

    /// <summary>Verifies alternate memory-area aliases and string-length metadata parsing.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Driver_ParsesEveryAddressAreaAndLengthFormAsync()
    {
        var channel = new CoreProtocolCoverageTests.TestChannel();
        using var connection = CreateConnection(channel);
        using var driver = NewDriver(connection);
        driver.AddUpdateTagItem(new PlcTag<short>(BracketedWordTagName, "D100[4]"));
        driver.AddUpdateTagItem(new PlcTag<string>("UnclosedLength", "D110["));
        driver.AddUpdateTagItem(new PlcTag<string>("InvalidLength", "D120[0]"));
        driver.AddUpdateTagItem(new PlcTag<bool>("WorkBit", "W10.1"));
        driver.AddUpdateTagItem(new PlcTag<bool>("HoldingBit", "H10.2"));
        driver.AddUpdateTagItem(new PlcTag<bool>("AuxiliaryBit", "A10.3"));
        driver.AddUpdateTagItem(new PlcTag<short>("HoldingWord", "H20"));
        driver.AddUpdateTagItem(new PlcTag<short>("AuxiliaryWord", "A20"));

        channel.SetResponseData([0, 1]);
        _ = await driver.ReadValueAsync(
            new LogicalTagKey<short>(BracketedWordTagName),
            CancellationToken.None);
        await AssertThrowsAsync<FormatException>(
            () => driver.ReadValueAsync(
                new LogicalTagKey<string>("UnclosedLength"),
                CancellationToken.None));
        var invalidLengthResponse = new byte[16];
        invalidLengthResponse[0] = (byte)'B';
        channel.SetResponseData(invalidLengthResponse);
        _ = await driver.ReadValueAsync(
            new LogicalTagKey<string>("InvalidLength"),
            CancellationToken.None);
        foreach (var name in new[] { "WorkBit", "HoldingBit", "AuxiliaryBit" })
        {
            channel.SetResponseData([1]);
            _ = await driver.ReadValueAsync(
                new LogicalTagKey<bool>(name),
                CancellationToken.None);
        }

        foreach (var name in new[] { "HoldingWord", "AuxiliaryWord" })
        {
            channel.SetResponseData([0, 1]);
            _ = await driver.ReadValueAsync(
                new LogicalTagKey<short>(name),
                CancellationToken.None);
        }

        await AssertInvalidReadAsync<bool>(driver, "InvalidBitArea", "Z10.1");
        await Assert.That(driver.GetValue(new LogicalTagKey<short>(BracketedWordTagName))).IsEqualTo((short)1);
    }

    /// <summary>Adds tags for each supported write type.</summary>
    /// <param name="driver">Driver to configure.</param>
    private static void AddWriteTags(OmronPlcRx driver)
    {
        driver.AddUpdateTagItem(new PlcTag<bool>(BoolBitWriteTagName, "D10.1"));
        driver.AddUpdateTagItem(new PlcTag<bool>("BoolWord", "D10"));
        driver.AddUpdateTagItem(new PlcTag<byte>(ByteTagName, "D11"));
        driver.AddUpdateTagItem(new PlcTag<short>("Short", "D12"));
        driver.AddUpdateTagItem(new PlcTag<ushort>("UShort", "D13"));
        driver.AddUpdateTagItem(new PlcTag<int>("Int", "D14"));
        driver.AddUpdateTagItem(new PlcTag<uint>("UInt", "D16"));
        driver.AddUpdateTagItem(new PlcTag<float>("Float", "D18"));
        driver.AddUpdateTagItem(new PlcTag<double>(DoubleTagName, "D20"));
        driver.AddUpdateTagItem(new PlcTag<string>(StringWriteTagName, "D24[4]"));
        driver.AddUpdateTagItem(new PlcTag<Bcd16>(nameof(Bcd16), "D26"));
        driver.AddUpdateTagItem(new PlcTag<BcdU16>(nameof(BcdU16), "D27"));
        driver.AddUpdateTagItem(new PlcTag<Bcd32>(nameof(Bcd32), "D28"));
        driver.AddUpdateTagItem(new PlcTag<BcdU32>(nameof(BcdU32), "D30"));
    }

    /// <summary>Creates an initialized connection over the deterministic channel.</summary>
    /// <param name="channel">Injected channel.</param>
    /// <returns>The initialized connection.</returns>
    private static OmronPLCConnection CreateConnection(CoreProtocolCoverageTests.TestChannel channel) =>
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

    /// <summary>Creates a facade with a long poll interval for direct deterministic operations.</summary>
    /// <param name="driverConnection">Injected connection.</param>
    /// <returns>The facade.</returns>
    private static OmronPlcRx NewDriver(OmronPLCConnection driverConnection) =>
        new(driverConnection, TimeSpan.FromDays(1), false);

    /// <summary>Verifies that one invalid tag read fails.</summary>
    /// <typeparam name="T">Tag type.</typeparam>
    /// <param name="driver">Driver under test.</param>
    /// <param name="name">Logical tag name.</param>
    /// <param name="address">Invalid address.</param>
    /// <returns>A task that represents the asynchronous assertion.</returns>
    private static async Task AssertInvalidReadAsync<T>(
        OmronPlcRx driver,
        string name,
        string address)
    {
        driver.AddUpdateTagItem(new PlcTag<T>(name, address));
        await AssertThrowsAsync<Exception>(
            () => driver.ReadValueAsync(new LogicalTagKey<T>(name), CancellationToken.None));
    }

    /// <summary>Captures and verifies an asynchronous exception.</summary>
    /// <typeparam name="TException">Expected exception type.</typeparam>
    /// <param name="action">Action to invoke.</param>
    /// <returns>A task that represents the asynchronous assertion.</returns>
    private static async Task AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException ex)
        {
            await Assert.That(ex).IsNotNull();
            return;
        }

        throw new InvalidOperationException($"Expected {nameof(TException)}.");
    }
}
