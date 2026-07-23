// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Provides deterministic FIFO latency and fault outcomes independently for read and write transfers.</summary>
public sealed class SimulatorScript
{
    /// <summary>Applies scripted latency.</summary>
    private readonly ISimulatorClock _clock;

    /// <summary>Protects the outcome queues.</summary>
    private readonly Lock _gate = new();

    /// <summary>Contains ordered outcomes by operation kind.</summary>
    private readonly Dictionary<SimulatorOperationKind, Queue<SimulatorOutcome>> _outcomes = [];

    /// <summary>Initializes a new instance of the <see cref="SimulatorScript"/> class using system time.</summary>
    public SimulatorScript()
        : this(SystemSimulatorClock.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SimulatorScript"/> class.</summary>
    /// <param name="clock">The clock used to apply scripted latency.</param>
    public SimulatorScript(ISimulatorClock clock) =>
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    /// <summary>Adds one outcome to the FIFO queue for an operation kind.</summary>
    /// <param name="kind">The operation kind.</param>
    /// <param name="outcome">The outcome to enqueue.</param>
    public void Enqueue(SimulatorOperationKind kind, SimulatorOutcome outcome)
    {
        ValidateKind(kind);
        if (outcome is null)
        {
            throw new ArgumentNullException(nameof(outcome));
        }

        lock (_gate)
        {
            if (!_outcomes.TryGetValue(kind, out var queue))
            {
                queue = new();
                _outcomes.Add(kind, queue);
            }

            queue.Enqueue(outcome);
        }
    }

    /// <summary>Gets the number of queued outcomes for an operation kind.</summary>
    /// <param name="kind">The operation kind.</param>
    /// <returns>The number of queued outcomes.</returns>
    public int PendingCount(SimulatorOperationKind kind)
    {
        ValidateKind(kind);
        lock (_gate)
        {
            return _outcomes.TryGetValue(kind, out var queue) ? queue.Count : 0;
        }
    }

    /// <summary>Consumes and applies the next outcome, defaulting to immediate success when the queue is empty.</summary>
    /// <param name="kind">The operation kind.</param>
    /// <returns>The consumed outcome.</returns>
    public Task<SimulatorOutcome> NextAsync(SimulatorOperationKind kind) =>
        NextAsync(kind, CancellationToken.None);

    /// <summary>Consumes and applies the next outcome, defaulting to immediate success when the queue is empty.</summary>
    /// <param name="kind">The operation kind.</param>
    /// <param name="cancellationToken">A token that cancels scripted latency.</param>
    /// <returns>The consumed outcome.</returns>
    public async Task<SimulatorOutcome> NextAsync(
        SimulatorOperationKind kind,
        CancellationToken cancellationToken)
    {
        ValidateKind(kind);
        SimulatorOutcome outcome;
        lock (_gate)
        {
            outcome = _outcomes.TryGetValue(kind, out var queue) && queue.Count != 0
                ? queue.Dequeue()
                : SimulatorOutcome.Success();
        }

        await _clock.DelayAsync(outcome.Latency, cancellationToken).ConfigureAwait(false);
        if (outcome.Exception is not null)
        {
            throw outcome.Exception;
        }

        return outcome;
    }

    /// <summary>Validates an operation-kind enum value.</summary>
    /// <param name="kind">The operation kind.</param>
    private static void ValidateKind(SimulatorOperationKind kind)
    {
        if (Enum.IsDefined(typeof(SimulatorOperationKind), kind))
        {
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(kind));
    }
}
