// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
using IoT.DriverCore.OmronPlcRx.Core.Types;
using IoT.DriverCore.OmronPlcRx.Tags;
using ReactiveUI.Primitives;
using TUnit.Core;

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>Exercises logical-tag type dispatch, merged observations, permissions, and failures.</summary>
public sealed class OmronLogicalTagDispatchCoverageTests
{
    /// <summary>Gets the expected number of supported logical types.</summary>
    private const int SupportedTypeCount = 13;

    /// <summary>Gets the deterministic 16-bit numeric value.</summary>
    private const short ShortValue = 12;

    /// <summary>Gets the deterministic unsigned 16-bit numeric value.</summary>
    private const ushort UnsignedShortValue = 13;

    /// <summary>Gets the deterministic 32-bit numeric value.</summary>
    private const int IntegerValue = 14;

    /// <summary>Gets the deterministic unsigned 32-bit numeric value.</summary>
    private const uint UnsignedIntegerValue = 15;

    /// <summary>Gets the deterministic single-precision value.</summary>
    private const float SingleValue = 16.5F;

    /// <summary>Gets the deterministic double-precision value.</summary>
    private const double DoubleValue = 17.5D;

    /// <summary>Gets the deterministic BCD value.</summary>
    private const short BcdValue = 1234;

    /// <summary>Gets the deterministic unsigned BCD value.</summary>
    private const ushort UnsignedBcdValue = 2345;

    /// <summary>Gets the first asynchronous observation tag name.</summary>
    private const string FirstTagName = "First";

    /// <summary>Gets the unsupported logical tag name.</summary>
    private const string UnsupportedTagName = "Unsupported";

    /// <summary>Gets the read-only logical tag name.</summary>
    private const string ReadOnlyTagName = "ReadOnly";

    /// <summary>Gets the write-only logical tag name.</summary>
    private const string WriteOnlyTagName = "WriteOnly";

    /// <summary>Gets the byte tag name.</summary>
    private static readonly string ByteTagName = typeof(byte).FullName!;

    /// <summary>Gets the double tag name.</summary>
    private static readonly string DoubleTagName = typeof(double).FullName!;

    /// <summary>Gets the string tag name.</summary>
    private static readonly string StringTagName = typeof(string).FullName!;

    /// <summary>Verifies all supported logical types read, write, and merge observations.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task LogicalClient_DispatchesEverySupportedTypeAndMergedObservationAsync()
    {
        using var simulator = new OmronPlcSimulator();
        using var client = new OmronLogicalTagClient(simulator);
        RegisterSupportedTags(client);
        var values = CreateSupportedValues();
        var names = values.Select(static value => value.TagName).ToArray();
        var observed = new List<LogicalTagValue>();
        var subscription = client
            .ObserveMany(names)
            .SubscribeSafe(observed.Add, static error => throw error);

        var writes = await client.WriteManyAsync(values, CancellationToken.None);
        var reads = await client.ReadManyAsync(names, CancellationToken.None);
        subscription.Dispose();
        subscription.Dispose();

        await Assert.That(writes.Count).IsEqualTo(SupportedTypeCount);
        await Assert.That(writes.All(static result => result.Succeeded)).IsTrue();
        await Assert.That(reads.Count).IsEqualTo(SupportedTypeCount);
        await Assert.That(reads.All(static result => result.Succeeded)).IsTrue();
        await Assert.That(observed.Count >= SupportedTypeCount).IsTrue();
        await Assert.That(reads.Single(static result => result.Value?.TagName == "Int").Value?.Value)
            .IsEqualTo(IntegerValue);
    }

    /// <summary>Verifies asynchronous merged observation adapts typed values through its channel.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task LogicalClient_ObservesManyAsAsyncSequenceAsync()
    {
        using var simulator = new OmronPlcSimulator();
        using var client = new OmronLogicalTagClient(simulator);
        _ = client.CreateTag(new PlcTag<short>(FirstTagName, "D1"));
        _ = client.CreateTag(new PlcTag<bool>("Second", "D2.0"));
        using var cancellation = new CancellationTokenSource();
        await using var enumerator = client
            .ObserveManyAsync([FirstTagName, "Second"], cancellation.Token)
            .GetAsyncEnumerator(cancellation.Token);

        var moved = await enumerator.MoveNextAsync();
        cancellation.Cancel();

        await Assert.That(moved).IsTrue();
        await Assert.That(enumerator.Current.TagName).IsEqualTo(FirstTagName);
    }

    /// <summary>Verifies access permissions, unsupported types, cancellation, and disposed failures.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task LogicalClient_ReportsDispatchValidationFailuresAsync()
    {
        using var simulator = new OmronPlcSimulator();
        using var catalog = new LogicalTagCatalog();
        catalog.Upsert(new LogicalTag(UnsupportedTagName, "D1", "System.Decimal"));
        var client = new OmronLogicalTagClient(simulator, catalog);
        _ = client.CreateTag(
            new PlcTag<short>(ReadOnlyTagName, "D2"),
            null,
            null,
            null,
            LogicalTagAccessMode.Read,
            null);
        _ = client.CreateTag(
            new PlcTag<short>(WriteOnlyTagName, "D3"),
            null,
            null,
            null,
            LogicalTagAccessMode.Write,
            null);

        var missing = await client.ReadAsync("Missing", CancellationToken.None);
        var deniedRead = await client.ReadAsync(WriteOnlyTagName, CancellationToken.None);
        var deniedWrite = await client.WriteAsync(
            new LogicalTagValue(ReadOnlyTagName, ShortValue, TimeProvider.System.GetUtcNow()),
            CancellationToken.None);
        var unsupportedRead = await client.ReadAsync(UnsupportedTagName, CancellationToken.None);
        var unsupportedWrite = await client.WriteAsync(
            new LogicalTagValue(UnsupportedTagName, ShortValue, TimeProvider.System.GetUtcNow()),
            CancellationToken.None);

        await Assert.That(missing.Succeeded).IsFalse();
        await Assert.That(deniedRead.Succeeded).IsFalse();
        await Assert.That(deniedWrite.Succeeded).IsFalse();
        await Assert.That(unsupportedRead.Succeeded).IsFalse();
        await Assert.That(unsupportedWrite.Succeeded).IsFalse();
        await AssertThrowsAsync<NotSupportedException>(
            () => Task.Run(() => client.Observe(UnsupportedTagName)));
        await AssertThrowsAsync<NotSupportedException>(
            () => Task.Run(() => client.RegisterTag(new LogicalTag("Bad", "D4", "Decimal"))));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await AssertThrowsAsync<OperationCanceledException>(
            () => client.ReadAsync(ReadOnlyTagName, cancellation.Token));
        await AssertThrowsAsync<OperationCanceledException>(
            () => client.WriteAsync(
                new LogicalTagValue(WriteOnlyTagName, ShortValue, TimeProvider.System.GetUtcNow()),
                cancellation.Token));
        await AssertNullArgumentsAsync(client);

        client.Dispose();
        client.Dispose();
        await AssertThrowsAsync<ObjectDisposedException>(
            () => client.ReadAsync(ReadOnlyTagName, CancellationToken.None));
    }

    /// <summary>Registers one tag for every logical Omron payload type.</summary>
    /// <param name="client">Logical client.</param>
    private static void RegisterSupportedTags(OmronLogicalTagClient client)
    {
        _ = client.CreateTag(new PlcTag<bool>("Bool", "D1.0"));
        _ = client.CreateTag(new PlcTag<byte>(ByteTagName, "D2"));
        _ = client.CreateTag(new PlcTag<short>("Short", "D3"));
        _ = client.CreateTag(new PlcTag<ushort>("UShort", "D4"));
        _ = client.CreateTag(new PlcTag<int>("Int", "D5"));
        _ = client.CreateTag(new PlcTag<uint>("UInt", "D7"));
        _ = client.CreateTag(new PlcTag<float>("Float", "D9"));
        _ = client.CreateTag(new PlcTag<double>(DoubleTagName, "D11"));
        _ = client.CreateTag(new PlcTag<string>(StringTagName, "D15[4]"));
        _ = client.CreateTag(new PlcTag<Bcd16>(nameof(Bcd16), "D19"));
        _ = client.CreateTag(new PlcTag<BcdU16>(nameof(BcdU16), "D20"));
        _ = client.CreateTag(new PlcTag<Bcd32>(nameof(Bcd32), "D21"));
        _ = client.CreateTag(new PlcTag<BcdU32>(nameof(BcdU32), "D23"));
    }

    /// <summary>Creates one deterministic value for every logical Omron payload type.</summary>
    /// <returns>The supported logical values.</returns>
    private static LogicalTagValue[] CreateSupportedValues()
    {
        var timestamp = TimeProvider.System.GetUtcNow();
        return
        [
            new("Bool", true, timestamp),
            new(ByteTagName, byte.MaxValue, timestamp),
            new("Short", ShortValue, timestamp),
            new("UShort", UnsignedShortValue, timestamp),
            new("Int", IntegerValue, timestamp),
            new("UInt", UnsignedIntegerValue, timestamp),
            new("Float", SingleValue, timestamp),
            new(DoubleTagName, DoubleValue, timestamp),
            new(StringTagName, "AB", timestamp),
            new(nameof(Bcd16), new Bcd16(BcdValue), timestamp),
            new(nameof(BcdU16), new BcdU16(UnsignedBcdValue), timestamp),
            new(nameof(Bcd32), new Bcd32(IntegerValue), timestamp),
            new(nameof(BcdU32), new BcdU32(UnsignedIntegerValue), timestamp),
        ];
    }

    /// <summary>Verifies public null argument guards.</summary>
    /// <param name="client">Logical client.</param>
    /// <returns>A task that represents the assertions.</returns>
    private static async Task AssertNullArgumentsAsync(OmronLogicalTagClient client)
    {
        await AssertThrowsAsync<ArgumentNullException>(
            () => client.ReadManyAsync(null!, CancellationToken.None));
        await AssertThrowsAsync<ArgumentNullException>(
            () => client.WriteManyAsync(null!, CancellationToken.None));
        await AssertThrowsAsync<ArgumentNullException>(
            () => client.WriteAsync(null!, CancellationToken.None));
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(() => client.ObserveMany(null!)));
        await AssertThrowsAsync<KeyNotFoundException>(
            () => Task.Run(() => client.Observe("Missing")));
    }

    /// <summary>Captures and verifies an asynchronous exception.</summary>
    /// <typeparam name="TException">Expected exception type.</typeparam>
    /// <param name="action">Action to invoke.</param>
    /// <returns>A task that represents the assertion.</returns>
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
