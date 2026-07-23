// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
using IoT.DriverCore.OmronPlcRx.Reactive.Async;
using IoT.DriverCore.OmronPlcRx.Reactive.Tags;
using TUnit.Core;

namespace IoT.DriverCore.OmronPlcRx.Reactive.Tests;

/// <summary>Verifies the deterministic simulator in the System.Reactive compatibility assembly.</summary>
public sealed class OmronPlcSimulatorReactiveTests
{
    /// <summary>Gets the logical counter tag name.</summary>
    private const string CounterTagName = "ReactiveCounter";

    /// <summary>Gets the initial deterministic value.</summary>
    private const int InitialValue = 10;

    /// <summary>Gets the updated deterministic value.</summary>
    private const int UpdatedValue = 20;

    /// <summary>Verifies setup, observation, async bridging, faults, reconnect, and disposal.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ReactiveSimulator_ExercisesLifecycleAndStreamsAsync()
    {
        var simulator = new OmronPlcSimulator();
        var tag = new LogicalTagKey<int>(CounterTagName);
        var values = new List<int>();
        simulator.Seed(new PlcTag<int>(CounterTagName, "D100"), InitialValue);
        using var subscription = simulator
            .Observe(tag)
            .Subscribe(values.Add, static error => throw error);
        _ = OmronPlcRxAsyncObservableExtensions.ObserveAsAsyncObservable(simulator, tag);

        await simulator.WriteValueAsync(tag, UpdatedValue, CancellationToken.None);
        var read = await simulator.ReadValueAsync(tag, CancellationToken.None);
        simulator.QueueFault(
            OmronSimulatorOperation.Read,
            new TimeoutException("reactive simulator timeout"));
        await AssertThrowsAsync<OmronPLCException>(
            () => simulator.ReadValueAsync(tag, CancellationToken.None));
        await simulator.ReconnectAsync(CancellationToken.None);

        await Assert.That(read).IsEqualTo(UpdatedValue);
        await Assert.That(values.Contains(InitialValue)).IsTrue();
        await Assert.That(values.Contains(UpdatedValue)).IsTrue();
        await Assert.That(simulator.ReconnectCount).IsEqualTo(1);

        simulator.Dispose();
        simulator.Dispose();
        await Assert.That(simulator.IsDisposed).IsTrue();
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
        catch (TException exception)
        {
            await Assert.That(exception).IsNotNull();
            return;
        }

        throw new InvalidOperationException($"Expected {nameof(TException)}.");
    }
}
