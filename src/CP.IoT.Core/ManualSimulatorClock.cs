// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Provides manually advanced UTC time and deterministic, non-blocking delays.</summary>
public sealed class ManualSimulatorClock : ISimulatorClock
{
    /// <summary>Protects clock and pending-delay state.</summary>
    private readonly Lock _gate = new();

    /// <summary>Contains delays that have not completed or been cancelled.</summary>
    private readonly List<PendingDelay> _delays = [];

    /// <summary>Stores the current UTC instant.</summary>
    private DateTimeOffset _utcNow;

    /// <summary>Initializes a new instance of the <see cref="ManualSimulatorClock"/> class.</summary>
    /// <param name="utcNow">The initial instant, normalized to UTC.</param>
    public ManualSimulatorClock(DateTimeOffset utcNow) => _utcNow = utcNow.ToUniversalTime();

    /// <inheritdoc/>
    public DateTimeOffset UtcNow
    {
        get
        {
            lock (_gate)
            {
                return _utcNow;
            }
        }
    }

    /// <summary>Gets the number of incomplete delays waiting for time to advance.</summary>
    public int PendingDelayCount
    {
        get
        {
            lock (_gate)
            {
                return _delays.Count;
            }
        }
    }

    /// <inheritdoc/>
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (delay == TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        PendingDelay pending;
        lock (_gate)
        {
            pending = new(_utcNow.Add(delay));
            _delays.Add(pending);
        }

        pending.RegisterCancellation(Cancel, cancellationToken);
        return pending.Completion.Task;
    }

    /// <summary>Advances the clock by a non-negative duration and completes every due delay.</summary>
    /// <param name="duration">The duration by which to advance.</param>
    public void AdvanceBy(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        AdvanceTo(UtcNow.Add(duration));
    }

    /// <summary>Advances the clock to an equal or later instant and completes every due delay.</summary>
    /// <param name="utcNow">The target instant, normalized to UTC.</param>
    public void AdvanceTo(DateTimeOffset utcNow)
    {
        var target = utcNow.ToUniversalTime();
        List<PendingDelay> due;
        lock (_gate)
        {
            if (target < _utcNow)
            {
                throw new ArgumentOutOfRangeException(nameof(utcNow));
            }

            _utcNow = target;
            due = _delays.Where(delay => delay.DueUtc <= target).ToList();
            _ = _delays.RemoveAll(delay => delay.DueUtc <= target);
        }

        foreach (var pending in due)
        {
            pending.Complete();
        }
    }

    /// <summary>Removes and cancels a pending delay.</summary>
    /// <param name="pending">The pending delay to cancel.</param>
    private void Cancel(PendingDelay pending)
    {
        lock (_gate)
        {
            _ = _delays.Remove(pending);
        }

        pending.Cancel();
    }

    /// <summary>Contains one manually scheduled delay.</summary>
    private sealed class PendingDelay
    {
        /// <summary>Cancels the delay when its source token is cancelled.</summary>
        private CancellationTokenRegistration _registration;

        /// <summary>Initializes a new instance of the <see cref="PendingDelay"/> class.</summary>
        /// <param name="dueUtc">The due UTC instant.</param>
        internal PendingDelay(DateTimeOffset dueUtc)
        {
            DueUtc = dueUtc;
            Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        /// <summary>Gets the completion source for the delay task.</summary>
        internal TaskCompletionSource<bool> Completion { get; }

        /// <summary>Gets the due UTC instant.</summary>
        internal DateTimeOffset DueUtc { get; }

        /// <summary>Registers optional cancellation for the delay.</summary>
        /// <param name="cancel">The owning clock's cancellation callback.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        internal void RegisterCancellation(Action<PendingDelay> cancel, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled)
            {
                return;
            }

            _registration = cancellationToken.Register(() => cancel(this));
            if (!Completion.Task.IsCompleted)
            {
                return;
            }

            _registration.Dispose();
        }

        /// <summary>Completes the delay successfully.</summary>
        internal void Complete()
        {
            _registration.Dispose();
            _ = Completion.TrySetResult(true);
        }

        /// <summary>Completes the delay as cancelled.</summary>
        internal void Cancel()
        {
            _registration.Dispose();
            _ = Completion.TrySetCanceled();
        }
    }
}
