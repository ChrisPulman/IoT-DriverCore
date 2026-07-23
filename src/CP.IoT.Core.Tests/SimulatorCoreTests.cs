// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
using TUnit.Assertions;
using TUnit.Core;

namespace IoT.DriverCore.Core.Tests;

/// <summary>Direct behavior and branch coverage for the reusable deterministic simulator primitives.</summary>
public sealed class SimulatorCoreTests
{
    /// <summary>The number of bytes in a simulated Int32 value.</summary>
    private const int Int32Length = 4;

    /// <summary>The number of concurrent writes used by the thread-safety test.</summary>
    private const int ConcurrentWriteCount = 32;

    /// <summary>The value two reused across deterministic scenarios.</summary>
    private const int Two = 2;

    /// <summary>The value three reused across deterministic scenarios.</summary>
    private const int Three = 3;

    /// <summary>The delayed-script duration in seconds.</summary>
    private const int Five = 5;

    /// <summary>The third Int32 binding offset.</summary>
    private const int Eight = 8;

    /// <summary>A distinct byte and logical value.</summary>
    private const int Nine = 9;

    /// <summary>The first updated logical value.</summary>
    private const int Ten = 10;

    /// <summary>The fourth Int32 binding offset.</summary>
    private const int Twelve = 12;

    /// <summary>The planner range length used by focused clients.</summary>
    private const int Sixteen = 16;

    /// <summary>The second updated logical value.</summary>
    private const int Twenty = 20;

    /// <summary>A read-only write-attempt value.</summary>
    private const int Thirty = 30;

    /// <summary>The default helper planner range length.</summary>
    private const int SixtyFour = 64;

    /// <summary>An invalid scripted operation kind.</summary>
    private const SimulatorOperationKind InvalidOperationKind = (SimulatorOperationKind)99;

    /// <summary>The missing tag name used by error-path tests.</summary>
    private const string MissingTagName = "Missing";

    /// <summary>An alternate physical memory area or partition name.</summary>
    private const string OtherName = "Other";

    /// <summary>The nullable-reference binding name.</summary>
    private const string NullableTagName = "OptionalText";

    /// <summary>The queued range failure intentionally left for later consumption.</summary>
    private const string UnusedError = "unused";

    /// <summary>The deterministic start instant used by simulator tests.</summary>
    private static readonly DateTimeOffset StartUtc = new(2026, 7, 23, 8, 0, 0, TimeSpan.Zero);

    /// <summary>Verifies manual time, delay completion, cancellation, and argument validation.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task ManualClockControlsDelaysAndCancellationAsync()
    {
        var clock = new ManualSimulatorClock(StartUtc.ToOffset(TimeSpan.FromHours(Two)));
        await Assert.That(clock.UtcNow).IsEqualTo(StartUtc);
        await Assert.That(clock.DelayAsync(TimeSpan.Zero, CancellationToken.None).IsCompleted).IsTrue();
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => clock.DelayAsync(TimeSpan.FromTicks(-1), CancellationToken.None));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => clock.AdvanceBy(TimeSpan.FromTicks(-1)));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => clock.AdvanceTo(StartUtc.AddTicks(-1)));

        var first = clock.DelayAsync(TimeSpan.FromSeconds(Two), CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var cancelled = clock.DelayAsync(TimeSpan.FromSeconds(Three), cancellation.Token);
        await Assert.That(clock.PendingDelayCount).IsEqualTo(Two);
        clock.AdvanceBy(TimeSpan.FromSeconds(1));
        await Assert.That(first.IsCompleted).IsFalse();
        cancellation.Cancel();
        await Assert.That(await ThrowsAsync<OperationCanceledException>(() => cancelled)).IsTrue();
        await Assert.That(clock.PendingDelayCount).IsEqualTo(1);

        clock.AdvanceTo(StartUtc.AddSeconds(Two));
        await first;
        await Assert.That(clock.PendingDelayCount).IsEqualTo(0);
        await Assert.That(clock.UtcNow).IsEqualTo(StartUtc.AddSeconds(Two));

        using var alreadyCancelled = new CancellationTokenSource();
        alreadyCancelled.Cancel();
        _ = Assert.Throws<OperationCanceledException>(
            () => clock.DelayAsync(TimeSpan.FromSeconds(1), alreadyCancelled.Token));
    }

    /// <summary>Verifies the real-time clock's immediate and validation paths.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task SystemClockSupportsImmediateDelaysAsync()
    {
        await Assert.That(SystemSimulatorClock.Instance.UtcNow.Offset).IsEqualTo(TimeSpan.Zero);
        await SystemSimulatorClock.Instance.DelayAsync(TimeSpan.Zero, CancellationToken.None);
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => SystemSimulatorClock.Instance.DelayAsync(TimeSpan.FromTicks(-1), CancellationToken.None));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var delay = SystemSimulatorClock.Instance.DelayAsync(TimeSpan.FromSeconds(1), cancellation.Token);
        await Assert.That(await ThrowsAsync<OperationCanceledException>(() => delay)).IsTrue();
    }

    /// <summary>Verifies FIFO operation scripts, independent queues, latency, faults, and defaults.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task ScriptAppliesOrderedLatencyFailuresAndExceptionsAsync()
    {
        var clock = new ManualSimulatorClock(StartUtc);
        var script = new SimulatorScript(clock);
        await Assert.That(script.PendingCount(SimulatorOperationKind.Read)).IsEqualTo(0);
        var delayedFailure = SimulatorOutcome.Failure("offline", TimeSpan.FromSeconds(Five));
        script.Enqueue(SimulatorOperationKind.Read, delayedFailure);
        script.Enqueue(SimulatorOperationKind.Read, SimulatorOutcome.Throw(new InvalidOperationException("boom")));
        script.Enqueue(SimulatorOperationKind.Write, SimulatorOutcome.Success());

        await Assert.That(script.PendingCount(SimulatorOperationKind.Read)).IsEqualTo(Two);
        await Assert.That(script.PendingCount(SimulatorOperationKind.Write)).IsEqualTo(1);
        var pending = script.NextAsync(SimulatorOperationKind.Read);
        await Assert.That(pending.IsCompleted).IsFalse();
        await Assert.That(clock.PendingDelayCount).IsEqualTo(1);
        clock.AdvanceBy(TimeSpan.FromSeconds(Five));
        var failure = await pending;
        await Assert.That(failure.Succeeded).IsFalse();
        await Assert.That(failure.Error).IsEqualTo("offline");
        await Assert.That(failure.Exception).IsNull();
        await Assert.That(failure.Latency).IsEqualTo(TimeSpan.FromSeconds(Five));
        await Assert.That(await ThrowsAsync<InvalidOperationException>(
            () => script.NextAsync(SimulatorOperationKind.Read))).IsTrue();
        await Assert.That((await script.NextAsync(SimulatorOperationKind.Read)).Succeeded).IsTrue();
        await Assert.That((await script.NextAsync(SimulatorOperationKind.Write)).Succeeded).IsTrue();

        _ = Assert.Throws<ArgumentNullException>(() => _ = new SimulatorScript(null!));
        _ = Assert.Throws<ArgumentNullException>(
            () => script.Enqueue(SimulatorOperationKind.Read, null!));
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => script.Enqueue(InvalidOperationKind, SimulatorOutcome.Success()));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => script.PendingCount(InvalidOperationKind));
        await Assert.That(await ThrowsAsync<ArgumentOutOfRangeException>(
            () => script.NextAsync(InvalidOperationKind))).IsTrue();
    }

    /// <summary>Verifies outcome factories reject malformed latency and failure values.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task OutcomesValidateFactoriesAsync()
    {
        var success = SimulatorOutcome.Success(TimeSpan.FromMilliseconds(1));
        var exception = new InvalidOperationException("fault");
        var thrown = SimulatorOutcome.Throw(exception, TimeSpan.FromMilliseconds(Two));

        await Assert.That(success.Succeeded).IsTrue();
        await Assert.That(success.Error).IsEqualTo(string.Empty);
        await Assert.That(thrown.Succeeded).IsFalse();
        await Assert.That(thrown.Exception).IsEqualTo(exception);
        await Assert.That(SimulatorOutcome.Failure("failure").Succeeded).IsFalse();
        await Assert.That(SimulatorOutcome.Throw(exception).Latency).IsEqualTo(TimeSpan.Zero);
        _ = Assert.Throws<ArgumentException>(() => SimulatorOutcome.Failure(" "));
        _ = Assert.Throws<ArgumentNullException>(() => SimulatorOutcome.Throw(null!));
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => SimulatorOutcome.Success(TimeSpan.FromTicks(-1)));
    }

    /// <summary>Verifies sparse bytes, typed codecs, overlapping ranges, journal retention, and observers.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task MemoryImageProvidesTypedSparseJournaledStorageAsync()
    {
        var clock = new ManualSimulatorClock(StartUtc);
        var memory = new SimulatorMemoryImage(clock, Two);
        var observer = new RecordingObserver<SimulatorMemoryChange>();
        using var subscription = memory.Subscribe(observer);
        var firstAddress = Address(offset: 1, length: Int32Length);

        await Assert.That(memory.CurrentSequence).IsEqualTo(0L);
        await Assert.That(memory.Read(firstAddress)).IsEquivalentTo(new byte[Int32Length]);
        var firstInput = BitConverter.GetBytes(Ten);
        var first = memory.Write(firstAddress, firstInput);
        firstInput[0] = byte.MaxValue;
        clock.AdvanceBy(TimeSpan.FromSeconds(1));
        var overlapping = Address(encoding: "Raw", access: TagTransferAccess.Write, offset: Three, length: Two);
        var second = memory.Write(overlapping, [Nine, Eight]);
        var otherRoute = Address(route: "other", offset: 1, length: Int32Length);
        var third = memory.Write(otherRoute, Thirty, BitConverter.GetBytes);

        await Assert.That(first.Sequence).IsEqualTo(1L);
        await Assert.That(first.TimestampUtc).IsEqualTo(StartUtc);
        await Assert.That(first.Address).IsEqualTo(firstAddress);
        await Assert.That(first.PreviousBytes).IsEquivalentTo(new byte[Int32Length]);
        await Assert.That(first.CurrentBytes).IsEquivalentTo(BitConverter.GetBytes(Ten));
        await Assert.That(second.PreviousBytes).IsEquivalentTo([byte.MinValue, byte.MinValue]);
        await Assert.That(memory.Read(firstAddress))
            .IsEquivalentTo([(byte)Ten, byte.MinValue, (byte)Nine, (byte)Eight]);
        await Assert.That(memory.Read(otherRoute, bytes => BitConverter.ToInt32(bytes.ToArray(), 0)))
            .IsEqualTo(Thirty);
        await Assert.That(memory.CurrentSequence).IsEqualTo((long)Three);
        await Assert.That(memory.GetChanges().Select(change => change.Sequence).ToArray())
            .IsEquivalentTo([(long)Two, Three]);
        await Assert.That(memory.GetChanges(Two).Single()).IsEqualTo(third);
        await Assert.That(observer.Values.Count).IsEqualTo(Three);

        var snapshot = memory.Read(firstAddress);
        snapshot[0] = byte.MaxValue;
        await Assert.That(memory.Read(firstAddress)[0]).IsEqualTo((byte)Ten);
        subscription.Dispose();
        _ = memory.Write(firstAddress, new byte[Int32Length]);
        await Assert.That(observer.Values.Count).IsEqualTo(Three);
    }

    /// <summary>Verifies memory validation paths and independent physical partitions.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task MemoryImageValidatesInputsAndPartitionsAsync()
    {
        _ = Assert.Throws<ArgumentNullException>(() => _ = new SimulatorMemoryImage(null!));
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => _ = new SimulatorMemoryImage(SystemSimulatorClock.Instance, 0));
        var memory = new SimulatorMemoryImage();
        var address = Address(length: Two);
        _ = Assert.Throws<ArgumentNullException>(() => memory.Read(null!));
        _ = Assert.Throws<ArgumentNullException>(() => memory.Read(address, (Func<IReadOnlyList<byte>, int>)null!));
        _ = Assert.Throws<ArgumentNullException>(() => memory.Write(null!, new byte[Two]));
        _ = Assert.Throws<ArgumentNullException>(() => memory.Write(address, null!));
        _ = Assert.Throws<ArgumentException>(() => memory.Write(address, new byte[1]));
        _ = Assert.Throws<ArgumentNullException>(() => memory.Write(address, 1, (Func<int, IReadOnlyList<byte>>)null!));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => memory.GetChanges(-1));
        _ = Assert.Throws<ArgumentNullException>(() => memory.Subscribe(null!));
        var huge = Address(length: (long)int.MaxValue + 1);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => memory.Read(huge));

        _ = memory.Write(address, [1, Two]);
        var otherArea = Address(memoryArea: OtherName, length: Two);
        var otherPartition = Address(partition: OtherName, length: Two);
        await Assert.That(memory.Read(otherArea)).IsEquivalentTo(new byte[Two]);
        await Assert.That(memory.Read(otherPartition)).IsEquivalentTo(new byte[Two]);
    }

    /// <summary>Verifies concurrent writes receive unique ordered journal sequences without corrupting bytes.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task MemoryImageSerializesConcurrentWritesAsync()
    {
        var memory = new SimulatorMemoryImage(SystemSimulatorClock.Instance, ConcurrentWriteCount);
        _ = Parallel.For(
            0,
            ConcurrentWriteCount,
            index => memory.Write(Address(offset: index), [checked((byte)index)]));

        var changes = memory.GetChanges();
        await Assert.That(changes.Count).IsEqualTo(ConcurrentWriteCount);
        await Assert.That(changes.Select(change => change.Sequence).Distinct().Count())
            .IsEqualTo(ConcurrentWriteCount);
        for (var index = 0; index < ConcurrentWriteCount; index++)
        {
            await Assert.That(memory.Read(Address(offset: index))[0]).IsEqualTo(checked((byte)index));
        }
    }

    /// <summary>Verifies binding factories provide typed codecs and validate every dependency.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task BindingsAndClientConstructorsValidateCompositionAsync()
    {
        var tag = Tag("A");
        var address = Address(length: Int32Length);
        _ = Assert.Throws<ArgumentNullException>(
            () => SimulatorTagBinding.Create(null!, address, DecodeInt32, EncodeInt32));
        _ = Assert.Throws<ArgumentNullException>(
            () => SimulatorTagBinding.Create(tag, null!, DecodeInt32, EncodeInt32));
        _ = Assert.Throws<ArgumentNullException>(
            () => SimulatorTagBinding.Create(tag, address, (Func<IReadOnlyList<byte>, int>)null!, EncodeInt32));
        _ = Assert.Throws<ArgumentNullException>(
            () => SimulatorTagBinding.Create(tag, address, DecodeInt32, (Func<int, IReadOnlyList<byte>>)null!));

        var binding = Int32Binding("A", 0);
        var memory = new SimulatorMemoryImage();
        var planner = new TagTransferPlanner(new TagTransferCapabilities(Sixteen));
        _ = Assert.Throws<ArgumentNullException>(
            () => _ = new SimulatorLogicalTagClient(null!, memory, planner));
        _ = Assert.Throws<ArgumentNullException>(
            () => _ = new SimulatorLogicalTagClient([binding], null!, planner));
        _ = Assert.Throws<ArgumentNullException>(
            () => _ = new SimulatorLogicalTagClient([binding], memory, null!));
        _ = Assert.Throws<ArgumentNullException>(
            () => _ = new SimulatorLogicalTagClient([binding], memory, planner, null!, SystemSimulatorClock.Instance));
        _ = Assert.Throws<ArgumentNullException>(
            () => _ = new SimulatorLogicalTagClient([binding], memory, planner, new(), null!));
        _ = Assert.Throws<ArgumentException>(
            () => _ = new SimulatorLogicalTagClient([null!], memory, planner));
        _ = Assert.Throws<ArgumentException>(
            () => _ = new SimulatorLogicalTagClient([binding, binding], memory, planner));

        var client = new SimulatorLogicalTagClient([binding], memory, planner);
        await Assert.That(client.Bindings["A"]).IsEqualTo(binding);
        await Assert.That(client.Memory).IsEqualTo(memory);
        await Assert.That(client.Planner).IsEqualTo(planner);
        await Assert.That(client.Script).IsNotNull();

        var nullableBinding = SimulatorTagBinding.Create(
            Tag(NullableTagName),
            Address(offset: Int32Length),
            DecodeNullableString,
            EncodeNullableString);
        var nullableClient = new SimulatorLogicalTagClient([nullableBinding], memory, planner);
        var nullableWrite = await nullableClient.WriteAsync(new(NullableTagName, null, StartUtc), CancellationToken.None);
        await Assert.That(nullableWrite.Succeeded).IsTrue();
        await Assert.That((await nullableClient.ReadAsync(NullableTagName, CancellationToken.None)).Value!.Value).IsNull();
    }

    /// <summary>Verifies logical reads and writes preserve order, enforce access, and coalesce through the planner.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task LogicalClientUsesPlannerAndPreservesResultsAsync()
    {
        var clock = new ManualSimulatorClock(StartUtc);
        var memory = new SimulatorMemoryImage(clock);
        var script = new SimulatorScript(clock);
        var bindings = new[]
        {
            Int32Binding("A", 0),
            Int32Binding("B", Int32Length),
            Int32Binding("ReadOnly", Eight, LogicalTagAccessMode.Read),
            Int32Binding("WriteOnly", Twelve, LogicalTagAccessMode.Write),
        };
        var client = new SimulatorLogicalTagClient(
            bindings,
            memory,
            new(new TagTransferCapabilities(Sixteen)),
            script,
            clock);
        _ = memory.Write(bindings[0].Address, EncodeInt32(1));
        _ = memory.Write(bindings[1].Address, EncodeInt32(Two));
        _ = memory.Write(bindings[Two].Address, EncodeInt32(Three));
        script.Enqueue(SimulatorOperationKind.Read, SimulatorOutcome.Success());
        script.Enqueue(SimulatorOperationKind.Read, SimulatorOutcome.Failure(UnusedError));

        var reads = await client.ReadManyAsync([MissingTagName, "B", "A", "WriteOnly"], CancellationToken.None);
        await Assert.That(reads[0].Succeeded).IsFalse();
        await Assert.That(reads[0].Error).Contains("not registered");
        await Assert.That(reads[1].Value!.Value).IsEqualTo(Two);
        await Assert.That(reads[Two].Value!.Value).IsEqualTo(1);
        await Assert.That(reads[Two].Value!.TimestampUtc).IsEqualTo(StartUtc);
        await Assert.That(reads[Two].Value!.Quality).IsEqualTo("Good");
        await Assert.That(reads[Three].Error).Contains("does not permit reads");
        await Assert.That(script.PendingCount(SimulatorOperationKind.Read)).IsEqualTo(1);

        script.Enqueue(SimulatorOperationKind.Write, SimulatorOutcome.Success());
        script.Enqueue(SimulatorOperationKind.Write, SimulatorOutcome.Failure(UnusedError));
        var writes = await client.WriteManyAsync(
            [
                new(MissingTagName, Nine, StartUtc),
                new("B", Twenty, StartUtc.AddDays(-1)),
                new("A", Ten, StartUtc.AddDays(-1)),
                new("ReadOnly", Thirty, StartUtc),
            ],
            CancellationToken.None);
        await Assert.That(writes[0].Error).Contains("not registered");
        await Assert.That(writes[1].Value!.Value).IsEqualTo(Twenty);
        await Assert.That(writes[Two].Value!.Value).IsEqualTo(Ten);
        await Assert.That(writes[Two].Value!.TimestampUtc).IsEqualTo(StartUtc);
        await Assert.That(writes[Three].Error).Contains("does not permit writes");
        await Assert.That(script.PendingCount(SimulatorOperationKind.Write)).IsEqualTo(1);
        await Assert.That((await client.ReadAsync("A", CancellationToken.None)).Error).IsEqualTo(UnusedError);
        await Assert.That((await client.ReadAsync("A", CancellationToken.None)).Value!.Value).IsEqualTo(Ten);
        await Assert.That(memory.Read(bindings[1].Address, DecodeInt32)).IsEqualTo(Twenty);
    }

    /// <summary>Verifies range-level expected and exceptional faults and codec failures.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task LogicalClientAppliesOneScriptOutcomePerPlannedRangeAsync()
    {
        var memory = new SimulatorMemoryImage();
        var script = new SimulatorScript();
        var first = Int32Binding("A", 0);
        var second = Int32Binding("B", Int32Length);
        var client = new SimulatorLogicalTagClient(
            [first, second],
            memory,
            new(new TagTransferCapabilities(Sixteen, 1)),
            script,
            SystemSimulatorClock.Instance);
        script.Enqueue(SimulatorOperationKind.Write, SimulatorOutcome.Failure("first range failed"));
        script.Enqueue(SimulatorOperationKind.Write, SimulatorOutcome.Success());
        var results = await client.WriteManyAsync(
            [new("A", 1, StartUtc), new("B", Two, StartUtc)],
            CancellationToken.None);

        await Assert.That(results[0].Error).IsEqualTo("first range failed");
        await Assert.That(results[1].Succeeded).IsTrue();
        await Assert.That(memory.Read(first.Address, DecodeInt32)).IsEqualTo(0);
        await Assert.That(memory.Read(second.Address, DecodeInt32)).IsEqualTo(Two);

        script.Enqueue(SimulatorOperationKind.Read, SimulatorOutcome.Throw(new InvalidOperationException("transport")));
        await Assert.That(await ThrowsAsync<InvalidOperationException>(
            () => client.ReadManyAsync(["A", "B"], CancellationToken.None))).IsTrue();
        await Assert.That(script.PendingCount(SimulatorOperationKind.Read)).IsEqualTo(0);

        await Assert.That(await ThrowsAsync<InvalidCastException>(
            () => client.WriteAsync(new("A", "wrong", StartUtc), CancellationToken.None))).IsTrue();
        var malformed = SimulatorTagBinding.Create(
            Tag("Malformed"),
            Address(offset: Eight, length: Int32Length),
            DecodeInt32,
            _ => new byte[1]);
        var malformedClient = new SimulatorLogicalTagClient(
            [malformed],
            memory,
            new(new TagTransferCapabilities(Sixteen)));
        await Assert.That(await ThrowsAsync<ArgumentException>(
            () => malformedClient.WriteAsync(new("Malformed", 1, StartUtc), CancellationToken.None))).IsTrue();
    }

    /// <summary>Verifies logical-client collection validation and pre-cancelled operations.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task LogicalClientValidatesCollectionsAndCancellationAsync()
    {
        var client = Client(Int32Binding("A", 0));
        await Assert.That(await ThrowsAsync<ArgumentNullException>(
            () => client.ReadManyAsync(null!, CancellationToken.None))).IsTrue();
        await Assert.That(await ThrowsAsync<ArgumentException>(
            () => client.ReadManyAsync([" "], CancellationToken.None))).IsTrue();
        await Assert.That(await ThrowsAsync<ArgumentNullException>(
            () => client.WriteManyAsync(null!, CancellationToken.None))).IsTrue();
        await Assert.That(await ThrowsAsync<ArgumentException>(
            () => client.WriteManyAsync([null!], CancellationToken.None))).IsTrue();

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.That(await ThrowsAsync<OperationCanceledException>(
            () => client.ReadAsync("A", cancellation.Token))).IsTrue();
        await Assert.That(await ThrowsAsync<OperationCanceledException>(
            () => client.WriteAsync(new("A", 1, StartUtc), cancellation.Token))).IsTrue();
    }

    /// <summary>Verifies classic logical observation filters memory changes and unsubscribes exactly once.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task LogicalClientProvidesFilteredObservableChangesAsync()
    {
        var first = Int32Binding("A", 0);
        var second = Int32Binding("B", Int32Length);
        var client = Client(first, second);
        var observer = new RecordingObserver<LogicalTagValue>();
        var observable = client.Observe("A");
        _ = Assert.Throws<ArgumentNullException>(() => observable.Subscribe(null!));
        using var subscription = observable.Subscribe(observer);

        _ = client.Memory.Write(second.Address, EncodeInt32(Two));
        _ = client.Memory.Write(first.Address, EncodeInt32(1));
        _ = client.Memory.Write(Address(route: "other", length: Int32Length), EncodeInt32(Nine));
        await Assert.That(observer.Values.Count).IsEqualTo(1);
        await Assert.That(observer.Values[0].TagName).IsEqualTo("A");
        await Assert.That(observer.Values[0].Value).IsEqualTo(1);
        subscription.Dispose();
        _ = client.Memory.Write(first.Address, EncodeInt32(Three));
        await Assert.That(observer.Values.Count).IsEqualTo(1);

        _ = Assert.Throws<ArgumentException>(() => client.Observe(MissingTagName));
        _ = Assert.Throws<ArgumentException>(() => client.ObserveMany([" "]));
        _ = Assert.Throws<ArgumentNullException>(() => client.ObserveMany(null!));
    }

    /// <summary>Verifies asynchronous logical observation queues values and responds to both cancellation tokens.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task LogicalClientProvidesCancellableAsyncObservationAsync()
    {
        var binding = Int32Binding("A", 0);
        var client = Client(binding);
        using var sourceCancellation = new CancellationTokenSource();
        using var enumerationCancellation = new CancellationTokenSource();
        var enumerator = client.ObserveManyAsync(["A"], sourceCancellation.Token)
            .GetAsyncEnumerator(enumerationCancellation.Token);
        _ = client.Memory.Write(binding.Address, EncodeInt32(1));
        _ = client.Memory.Write(binding.Address, EncodeInt32(Two));

        await Assert.That(await enumerator.MoveNextAsync()).IsTrue();
        await Assert.That(enumerator.Current.Value).IsEqualTo(1);
        await Assert.That(await enumerator.MoveNextAsync()).IsTrue();
        await Assert.That(enumerator.Current.Value).IsEqualTo(Two);
        enumerationCancellation.Cancel();
        await Assert.That(await ThrowsAsync<OperationCanceledException>(
            () => enumerator.MoveNextAsync().AsTask())).IsTrue();
        await enumerator.DisposeAsync();
        await enumerator.DisposeAsync();

        using var directCancellation = new CancellationTokenSource();
        var direct = client.ObserveAsync("A", directCancellation.Token).GetAsyncEnumerator();
        directCancellation.Cancel();
        await Assert.That(await ThrowsAsync<OperationCanceledException>(
            () => direct.MoveNextAsync().AsTask())).IsTrue();
        await direct.DisposeAsync();

        var disposed = client.ObserveAsync("A", CancellationToken.None).GetAsyncEnumerator();
        await disposed.DisposeAsync();
        await Assert.That(await disposed.MoveNextAsync()).IsFalse();

        var waiting = client.ObserveAsync("A", CancellationToken.None).GetAsyncEnumerator();
        var moveNext = waiting.MoveNextAsync().AsTask();
        await Assert.That(moveNext.IsCompleted).IsFalse();
        _ = client.Memory.Write(binding.Address, EncodeInt32(Three));
        await Assert.That(await moveNext).IsTrue();
        await Assert.That(waiting.Current.Value).IsEqualTo(Three);
        await waiting.DisposeAsync();

        using var preCancelledSource = new CancellationTokenSource();
        preCancelledSource.Cancel();
        var preCancelled = client.ObserveAsync("A", preCancelledSource.Token).GetAsyncEnumerator();
        await Assert.That(await ThrowsAsync<OperationCanceledException>(
            () => preCancelled.MoveNextAsync().AsTask())).IsTrue();
        await preCancelled.DisposeAsync();
    }

    /// <summary>Verifies remaining public guard and parser paths through realistic invalid consumer input.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task ExistingContractsRejectCorruptAndIncompleteInputsAsync()
    {
        _ = Assert.Throws<ArgumentNullException>(
            () => _ = new LogicalTagChangedEventArgs(LogicalTagChangeKind.Added, null!));
        _ = Assert.Throws<ArgumentNullException>(() => _ = new TagTransferRequest("A", null!));

        using var writer = new StringWriter();
        await LogicalTagCsv.ExportAsync([Tag("A")], writer, CancellationToken.None);
        var imported = await LogicalTagCsv.ImportAsync(new StringReader(writer.ToString()), CancellationToken.None);
        await Assert.That(imported.Count).IsEqualTo(1);

        const string malformedMetadata =
            "Name,Address,DataType,GroupName,Description,Metadata,AccessMode,ScanIntervalMilliseconds\r\n"
            + "A,D0,Int32,,,missing-equals,ReadWrite,";
        await Assert.That(await ThrowsAsync<FormatException>(
            () => LogicalTagCsv.ImportAsync(new StringReader(malformedMetadata)))).IsTrue();

        var nullReader = new NullResultReader();
        var typedResult = await nullReader.ReadAsync(new LogicalTagKey<int>("A"));
        await Assert.That(typedResult.Succeeded).IsFalse();
        await Assert.That(typedResult.Error).Contains("returned null");

        var planner = new TagTransferPlanner(new TagTransferCapabilities(1));
        var plan = planner.Plan(
        [
            new("Partition", Address(partition: "A")),
            new("Memory", Address(partition: "A", memoryArea: OtherName)),
            new("Encoding", Address(partition: "A", memoryArea: OtherName, encoding: "Raw")),
            new("Access", Address(partition: "A", memoryArea: OtherName, encoding: "Raw", access: TagTransferAccess.Write)),
            new("Route", Address(partition: "A", memoryArea: OtherName, encoding: "Raw", access: TagTransferAccess.Write, route: "B")),
        ]);
        await Assert.That(plan.Ranges.Count).IsEqualTo(Five);
        await Assert.That(plan.Ranges.All(range => range.EndOffset == 1)).IsTrue();
    }

    /// <summary>Creates a standard byte transport address.</summary>
    /// <param name="partition">The transport partition.</param>
    /// <param name="memoryArea">The memory area.</param>
    /// <param name="encoding">The encoding.</param>
    /// <param name="access">The access direction.</param>
    /// <param name="route">The route.</param>
    /// <param name="offset">The byte offset.</param>
    /// <param name="length">The byte length.</param>
    /// <returns>The transport address.</returns>
    private static TagTransportAddress Address(
        string partition = "Device",
        string memoryArea = "Memory",
        string encoding = "Int32",
        TagTransferAccess access = TagTransferAccess.Read,
        string route = "",
        long offset = 0,
        long length = 1) =>
        new(partition, memoryArea, encoding, access, route, offset, length);

    /// <summary>Creates a logical Int32 tag.</summary>
    /// <param name="name">The tag name.</param>
    /// <param name="accessMode">The logical access mode.</param>
    /// <returns>The logical tag.</returns>
    private static LogicalTag Tag(
        string name,
        LogicalTagAccessMode accessMode = LogicalTagAccessMode.ReadWrite) =>
        new(name, $"Memory:{name}", "Int32", new() { AccessMode = accessMode });

    /// <summary>Creates a standard Int32 simulator binding.</summary>
    /// <param name="name">The tag name.</param>
    /// <param name="offset">The byte offset.</param>
    /// <param name="accessMode">The logical access mode.</param>
    /// <returns>The typed binding.</returns>
    private static SimulatorTagBinding Int32Binding(
        string name,
        long offset,
        LogicalTagAccessMode accessMode = LogicalTagAccessMode.ReadWrite) =>
        SimulatorTagBinding.Create(
            Tag(name, accessMode),
            Address(offset: offset, length: Int32Length),
            DecodeInt32,
            EncodeInt32);

    /// <summary>Creates a logical client with standard simulator dependencies.</summary>
    /// <param name="bindings">The tag bindings.</param>
    /// <returns>The simulator client.</returns>
    private static SimulatorLogicalTagClient Client(params SimulatorTagBinding[] bindings) =>
        new(bindings, new(), new(new TagTransferCapabilities(SixtyFour)));

    /// <summary>Decodes an Int32 from simulator bytes.</summary>
    /// <param name="bytes">The source bytes.</param>
    /// <returns>The decoded value.</returns>
    private static int DecodeInt32(IReadOnlyList<byte> bytes) =>
        BitConverter.ToInt32(bytes.ToArray(), 0);

    /// <summary>Encodes an Int32 to simulator bytes.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The encoded bytes.</returns>
    private static IReadOnlyList<byte> EncodeInt32(int value) => BitConverter.GetBytes(value);

    /// <summary>Decodes the nullable string representation used by binding tests.</summary>
    /// <param name="bytes">The source bytes.</param>
    /// <returns>The nullable value.</returns>
    private static string? DecodeNullableString(IReadOnlyList<byte> bytes) =>
        bytes[0] == byte.MinValue ? null : "value";

    /// <summary>Encodes a nullable string into a single marker byte.</summary>
    /// <param name="value">The nullable value.</param>
    /// <returns>The encoded marker byte.</returns>
    private static IReadOnlyList<byte> EncodeNullableString(string? value) =>
        [value is null ? byte.MinValue : byte.MaxValue];

    /// <summary>Returns whether an asynchronous action throws an expected exception.</summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="action">The action to execute.</param>
    /// <returns><see langword="true"/> when the expected exception is thrown.</returns>
    private static async Task<bool> ThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }

    /// <summary>Records observable notifications for assertions.</summary>
    /// <typeparam name="T">The notification type.</typeparam>
    private sealed class RecordingObserver<T> : IObserver<T>
    {
        /// <summary>Gets recorded values.</summary>
        internal List<T> Values { get; } = [];

        /// <inheritdoc/>
        public void OnCompleted()
        {
        }

        /// <inheritdoc/>
        public void OnError(Exception error)
        {
        }

        /// <inheritdoc/>
        public void OnNext(T value) => Values.Add(value);
    }

    /// <summary>Returns a successful result with a missing logical value to exercise defensive typed reads.</summary>
    private sealed class NullResultReader : ILogicalTagReader
    {
        /// <inheritdoc/>
        public Task<TagOperationResult<LogicalTagValue>> ReadAsync(
            string tagName,
            CancellationToken cancellationToken) =>
            Task.FromResult(TagOperationResult<LogicalTagValue>.Success(null!));

        /// <inheritdoc/>
        public Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> ReadManyAsync(
            IReadOnlyCollection<string> tagNames,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TagOperationResult<LogicalTagValue>>>([]);
    }
}
