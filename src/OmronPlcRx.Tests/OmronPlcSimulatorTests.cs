// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
using IoT.DriverCore.OmronPlcRx.Async;
using IoT.DriverCore.OmronPlcRx.Enums;
using IoT.DriverCore.OmronPlcRx.Tags;
using ReactiveUI.Primitives;
using TUnit.Core;

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>Tests the production deterministic Omron PLC simulator.</summary>
public sealed class OmronPlcSimulatorTests
{
    /// <summary>Gets the logical speed tag name.</summary>
    private const string SpeedTagName = "Speed";

    /// <summary>Gets the logical converted-value tag name.</summary>
    private const string ConvertedTagName = "Converted";

    /// <summary>Gets the logical running tag name.</summary>
    private const string RunningTagName = "Running";

    /// <summary>Gets the logical counter tag name.</summary>
    private const string CounterTagName = "Counter";

    /// <summary>Gets the first expected speed value.</summary>
    private const short InitialSpeed = 12;

    /// <summary>Gets the first written speed value.</summary>
    private const short WrittenSpeed = 24;

    /// <summary>Gets the final written speed value.</summary>
    private const short FinalSpeed = 36;

    /// <summary>Gets the converted numeric value.</summary>
    private const int ConvertedValue = 42;

    /// <summary>Gets the initial bulk-operation speed.</summary>
    private const short InitialBulkSpeed = 10;

    /// <summary>Gets the final bulk-operation speed.</summary>
    private const short FinalBulkSpeed = 20;

    /// <summary>Gets the retained counter value.</summary>
    private const int RetainedCounter = 7;

    /// <summary>Gets the attempted faulted counter value.</summary>
    private const int FaultedCounter = 8;

    /// <summary>Gets the expected count of two values.</summary>
    private const int ExpectedTwo = 2;

    /// <summary>Gets the expected count of three values.</summary>
    private const int ExpectedThree = 3;

    /// <summary>Gets the simulated minimum cycle time.</summary>
    private const double MinimumCycle = 0.5;

    /// <summary>Gets the simulated maximum cycle time.</summary>
    private const double MaximumCycle = 4.5;

    /// <summary>Gets the simulated average cycle time.</summary>
    private const double AverageCycle = 2.5;

    /// <summary>Gets the explicit simulated day of week.</summary>
    private const int ExplicitDayOfWeek = 5;

    /// <summary>Gets an invalid simulated day of week.</summary>
    private const int InvalidDayOfWeek = 7;

    /// <summary>Gets the initial simulated PLC clock.</summary>
    private static readonly DateTimeOffset InitialClock =
        new(2025, 6, 16, 10, 30, 0, TimeSpan.Zero);

    /// <summary>Verifies identity, connection lifecycle, and constructor validation.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Simulator_ProvidesDeterministicIdentityAndConnectionLifecycleAsync()
    {
        using var simulator = new OmronPlcSimulator(
            PlcType.CJ2,
            "CJ2M-SIM",
            "2.0",
            false,
            InitialClock);

        await Assert.That(simulator.PlcType).IsEqualTo(PlcType.CJ2);
        await Assert.That(simulator.ControllerModel).IsEqualTo("CJ2M-SIM");
        await Assert.That(simulator.ControllerVersion).IsEqualTo("2.0");
        await Assert.That(simulator.IsConnected).IsFalse();

        await simulator.ConnectAsync();
        simulator.Disconnect();
        await simulator.ReconnectAsync(CancellationToken.None);

        await Assert.That(simulator.IsConnected).IsTrue();
        await Assert.That(simulator.ReconnectCount).IsEqualTo(1);
        await Assert.That(simulator.Operations.Count).IsEqualTo(ExpectedTwo);

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await AssertThrowsAsync<OperationCanceledException>(
            () => simulator.ConnectAsync(cancelled.Token));
        await AssertThrowsAsync<ArgumentException>(
            () => Task.FromResult(new OmronPlcSimulator(
                PlcType.CJ2,
                " ",
                "1.0",
                true,
                InitialClock)));
        await AssertThrowsAsync<ArgumentException>(
            () => Task.FromResult(new OmronPlcSimulator(
                PlcType.CJ2,
                "CJ2",
                string.Empty,
                true,
                InitialClock)));
    }

    /// <summary>Verifies tag setup, conversion, observation, reads, writes, and removal.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Simulator_RegistersReadsWritesObservesAndConvertsAsync()
    {
        using var simulator = new OmronPlcSimulator();
        var speedTag = new PlcTag<short>(SpeedTagName, "D100");
        var observed = new List<short>();
        var all = new List<IPlcTag?>();
        simulator.Seed(speedTag, InitialSpeed);
        using var subscription = simulator
            .Observe(new LogicalTagKey<short>(SpeedTagName))
            .SubscribeSafe(observed.Add, static error => throw error);
        using var allSubscription = simulator.ObserveAll.SubscribeSafe(
            all.Add,
            static error => throw error);

        await simulator.WriteValueAsync(
            new LogicalTagKey<short>(SpeedTagName),
            WrittenSpeed,
            CancellationToken.None);
        simulator.SetValue(new LogicalTagKey<short>(SpeedTagName), FinalSpeed);
        var read = await simulator.ReadValueAsync(
            new LogicalTagKey<short>(SpeedTagName),
            CancellationToken.None);

        var convertedTag = new PlcTag<int>(ConvertedTagName, "D101");
        simulator.AddUpdateTagItem(convertedTag);
        simulator.SetValue(new LogicalTagKey<int>(ConvertedTagName), ConvertedValue);
        var converted = simulator.GetValue(new LogicalTagKey<long>(ConvertedTagName));
        var replacement = new PlcTag<int>(ConvertedTagName, "D102");
        simulator.AddUpdateTagItem(replacement);

        await Assert.That(read).IsEqualTo(FinalSpeed);
        await Assert.That(speedTag.Value).IsEqualTo(FinalSpeed);
        await Assert.That(observed.Contains(WrittenSpeed)).IsTrue();
        await Assert.That(observed.Contains(FinalSpeed)).IsTrue();
        await Assert.That(all.Count).IsEqualTo(ExpectedThree);
        await Assert.That(converted).IsEqualTo(ConvertedValue);
        await Assert.That(replacement.Value).IsEqualTo(ConvertedValue);
        await Assert.That(simulator.RemoveTagItem(ConvertedTagName)).IsTrue();
        await Assert.That(simulator.RemoveTagItem(ConvertedTagName)).IsFalse();
        await Assert.That(simulator.GetValue(new LogicalTagKey<int>("Missing"))).IsEqualTo(0);

        await AssertThrowsAsync<KeyNotFoundException>(
            () => simulator.ReadValueAsync(new LogicalTagKey<int>("Missing"), CancellationToken.None));
        await AssertThrowsAsync<KeyNotFoundException>(
            () => simulator.WriteValueAsync(new LogicalTagKey<int>(SpeedTagName), 1, CancellationToken.None));
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(() => simulator.AddUpdateTagItem<int>(null!)));
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(() => simulator.RemoveTagItem(null!)));
        await AssertThrowsAsync<ArgumentException>(
            () => Task.Run(() => simulator.RemoveTagItem(" ")));
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(() => simulator.Observe<int>(null!)));
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(() => simulator.GetValue<int>(null!)));
    }

    /// <summary>Verifies composition through the logical client including bulk operations.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Simulator_ComposesWithLogicalClientForBulkOperationsAsync()
    {
        using var simulator = new OmronPlcSimulator();
        using var client = new OmronLogicalTagClient(simulator);
        var speed = new PlcTag<short>(SpeedTagName, "D100");
        var running = new PlcTag<bool>(RunningTagName, "D100.0");
        _ = client.CreateTag(speed);
        _ = client.CreateTag(running);
        simulator.Seed(speed, InitialBulkSpeed);
        simulator.Seed(running, true);

        var reads = await client.ReadManyAsync(
            [SpeedTagName, RunningTagName],
            CancellationToken.None);
        var writes = await client.WriteManyAsync(
            [
                new LogicalTagValue(SpeedTagName, FinalBulkSpeed, InitialClock),
                new LogicalTagValue(RunningTagName, false, InitialClock),
            ],
            CancellationToken.None);

        await Assert.That(reads.Count).IsEqualTo(ExpectedTwo);
        await Assert.That(reads.All(static result => result.Succeeded)).IsTrue();
        await Assert.That(writes.Count).IsEqualTo(ExpectedTwo);
        await Assert.That(writes.All(static result => result.Succeeded)).IsTrue();
        await Assert.That(simulator.GetValue(new LogicalTagKey<short>(SpeedTagName))).IsEqualTo(FinalBulkSpeed);
        await Assert.That(simulator.GetValue(new LogicalTagKey<bool>(RunningTagName))).IsFalse();
    }

    /// <summary>Verifies scripted faults, error publication, and retained state across reconnect.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Simulator_ScriptsFaultsAndReconnectsWithoutLosingMemoryAsync()
    {
        using var simulator = new OmronPlcSimulator();
        var errors = new List<OmronPLCException?>();
        using var subscription = simulator.Errors.SubscribeSafe(
            errors.Add,
            static error => throw error);
        var tag = new PlcTag<int>(CounterTagName, "D200");
        simulator.Seed(tag, RetainedCounter);
        simulator.QueueFault(
            OmronSimulatorOperation.Read,
            new TimeoutException("read timeout"),
            ExpectedTwo,
            false);

        await AssertThrowsAsync<OmronPLCException>(
            () => simulator.ReadValueAsync(new LogicalTagKey<int>(CounterTagName), CancellationToken.None));
        await AssertThrowsAsync<OmronPLCException>(
            () => simulator.ReadValueAsync(new LogicalTagKey<int>(CounterTagName), CancellationToken.None));
        var recoveredRead = await simulator.ReadValueAsync(
            new LogicalTagKey<int>(CounterTagName),
            CancellationToken.None);

        simulator.QueueFault(
            OmronSimulatorOperation.Write,
            new InvalidOperationException("write failed"));
        await AssertThrowsAsync<OmronPLCException>(
            () => simulator.WriteValueAsync(
                new LogicalTagKey<int>(CounterTagName),
                FaultedCounter,
                CancellationToken.None));
        await Assert.That(simulator.IsConnected).IsFalse();
        await AssertThrowsAsync<OmronPLCException>(
            () => simulator.ReadValueAsync(new LogicalTagKey<int>(CounterTagName), CancellationToken.None));

        await simulator.ReconnectAsync();
        var retained = await simulator.ReadValueAsync(
            new LogicalTagKey<int>(CounterTagName),
            CancellationToken.None);

        await Assert.That(recoveredRead).IsEqualTo(RetainedCounter);
        await Assert.That(retained).IsEqualTo(RetainedCounter);
        await Assert.That(errors.Count).IsEqualTo(ExpectedThree);
        await Assert.That(simulator.Operations.Count(static operation => !operation.Succeeded)).IsEqualTo(ExpectedThree);

        simulator.QueueFault(OmronSimulatorOperation.Connect, new TimeoutException("connect failed"));
        simulator.Disconnect();
        await AssertThrowsAsync<OmronPLCException>(() => simulator.ConnectAsync());
    }

    /// <summary>Verifies deterministic clock and scan-cycle operations and their faults.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Simulator_ProvidesClockAndCycleOperationsAsync()
    {
        using var simulator = new OmronPlcSimulator(
            PlcType.NJ501,
            "NJ501-SIM",
            "1.0",
            true,
            InitialClock)
        {
            MinimumCycleTime = MinimumCycle,
            MaximumCycleTime = MaximumCycle,
            AverageCycleTime = AverageCycle,
        };

        var initial = await simulator.ReadClockAsync(CancellationToken.None);
        var target = InitialClock.AddDays(1);
        var write = await simulator.WriteClockAsync(target, CancellationToken.None);
        var explicitWrite = await simulator.WriteClockAsync(
            target,
            ExplicitDayOfWeek,
            CancellationToken.None);
        var updated = await simulator.ReadClockAsync(CancellationToken.None);
        var cycle = await simulator.ReadCycleTimeAsync(CancellationToken.None);

        await Assert.That(initial.Clock).IsEqualTo(InitialClock);
        await Assert.That(write.PacketsSent).IsEqualTo(1);
        await Assert.That(explicitWrite.PacketsReceived).IsEqualTo(1);
        await Assert.That(updated.DayOfWeek).IsEqualTo(ExplicitDayOfWeek);
        await Assert.That(cycle.MinimumCycleTime).IsEqualTo(MinimumCycle);
        await Assert.That(cycle.MaximumCycleTime).IsEqualTo(MaximumCycle);
        await Assert.That(cycle.AverageCycleTime).IsEqualTo(AverageCycle);
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => simulator.WriteClockAsync(target, -1, CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => simulator.WriteClockAsync(target, InvalidDayOfWeek, CancellationToken.None));

        simulator.QueueFault(OmronSimulatorOperation.ReadClock, new TimeoutException());
        await AssertThrowsAsync<OmronPLCException>(
            () => simulator.ReadClockAsync(CancellationToken.None));
        await simulator.ReconnectAsync();
        simulator.QueueFault(OmronSimulatorOperation.WriteClock, new TimeoutException());
        await AssertThrowsAsync<OmronPLCException>(
            () => simulator.WriteClockAsync(target, CancellationToken.None));
        await simulator.ReconnectAsync();
        simulator.QueueFault(OmronSimulatorOperation.ReadCycleTime, new TimeoutException());
        await AssertThrowsAsync<OmronPLCException>(
            () => simulator.ReadCycleTimeAsync(CancellationToken.None));
    }

    /// <summary>Verifies disposal is idempotent and rejects further operations.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Simulator_DisposeCompletesAndRejectsOperationsAsync()
    {
        var simulator = new OmronPlcSimulator();
        var completed = false;
        var tag = new PlcTag<int>(CounterTagName, "D10");
        simulator.Seed(tag, 1);
        using var subscription = simulator.ObserveAll.SubscribeSafe(
            static _ => { },
            static error => throw error,
            () => completed = true);

        simulator.Dispose();
        simulator.Dispose();

        await Assert.That(simulator.IsDisposed).IsTrue();
        await Assert.That(simulator.IsConnected).IsFalse();
        await Assert.That(completed).IsTrue();
        await AssertThrowsAsync<ObjectDisposedException>(
            () => simulator.ConnectAsync(CancellationToken.None));
        await AssertThrowsAsync<ObjectDisposedException>(
            () => Task.Run(simulator.Disconnect));
        await AssertThrowsAsync<ObjectDisposedException>(
            () => Task.Run(() => simulator.QueueFault(
                OmronSimulatorOperation.Read,
                new TimeoutException())));
    }

    /// <summary>Verifies fault argument validation.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Simulator_ValidatesFaultArgumentsAsync()
    {
        using var simulator = new OmronPlcSimulator();

        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(() => simulator.QueueFault(OmronSimulatorOperation.Read, null!)));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => Task.Run(() => simulator.QueueFault(
                OmronSimulatorOperation.Read,
                new TimeoutException(),
                0,
                true)));
    }

    /// <summary>Verifies the convenience constructor, unseeded observation, null guards, and async bridges.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Simulator_CoversConvenienceAndAsyncObservationPathsAsync()
    {
        using var simulator = new OmronPlcSimulator(false);
        var initiallyConnected = simulator.IsConnected;
        await simulator.ConnectAsync();
        var tag = new LogicalTagKey<int>("AsyncCounter");
        var observed = new List<int>();
        using var subscription = simulator
            .Observe(tag)
            .SubscribeSafe(
                observed.Add,
                static error => throw error);

        simulator.AddUpdateTagItem(new PlcTag<int>(tag.Name, "D300"));
        simulator.SetValue(tag, RetainedCounter);

        _ = OmronPlcRxAsyncObservableExtensions.ObserveAsAsyncObservable(simulator, tag);
        _ = OmronPlcRxAsyncObservableExtensions.ObserveAllAsAsyncObservable(simulator);
        _ = OmronPlcRxAsyncObservableExtensions.ErrorsAsAsyncObservable(simulator);
        using var cancellation = new CancellationTokenSource();
        await using var enumerator = OmronPlcRxAsyncObservableExtensions
            .ObserveValuesAsync(simulator, tag, cancellation.Token)
            .GetAsyncEnumerator(cancellation.Token);
        var hasValue = await enumerator.MoveNextAsync();

        await Assert.That(initiallyConnected).IsFalse();
        await Assert.That(simulator.IsConnected).IsTrue();
        await Assert.That(observed.Contains(0)).IsTrue();
        await Assert.That(observed.Contains(RetainedCounter)).IsTrue();
        await Assert.That(hasValue).IsTrue();
        await Assert.That(enumerator.Current).IsEqualTo(RetainedCounter);
        await AssertThrowsAsync<ArgumentNullException>(
            () => simulator.ReadValueAsync<int>(null!, CancellationToken.None));
        await AssertThrowsAsync<ArgumentNullException>(
            () => simulator.WriteValueAsync(null!, RetainedCounter, CancellationToken.None));
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(() => OmronPlcRxAsyncObservableExtensions.ObserveAsAsyncObservable(null!, tag)));
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(() => OmronPlcRxAsyncObservableExtensions.ObserveAllAsAsyncObservable(null!)));
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(() => OmronPlcRxAsyncObservableExtensions.ErrorsAsAsyncObservable(null!)));
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
