// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Enterprise;
#else
namespace S7PlcRx.Enterprise;
#endif

/// <summary>Provides high-availability management for PLC connections.</summary>
public sealed class HighAvailabilityPlcManager : IDisposable
{
    /// <summary>Defines the default health-check interval.</summary>
    private const int DefaultHealthCheckIntervalSeconds = 30;

    /// <summary>Stores the managed PLCs.</summary>
    private readonly IList<IRxS7> _backupPlcs;

    /// <summary>Stores the health-check timer.</summary>
    private readonly Timer _healthCheckTimer;

    /// <summary>Stores the failover event signal.</summary>
    private readonly Signal<PlcFailoverEvent> _failoverEvents = new();

    /// <summary>Stores the time provider used by this instance.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>Indicates whether this manager has been disposed.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="HighAvailabilityPlcManager"/> class.</summary>
    /// <param name="primaryPlc">The primary PLC.</param>
    /// <param name="backupPlcs">The backup PLCs.</param>
    public HighAvailabilityPlcManager(IRxS7 primaryPlc, IList<IRxS7> backupPlcs)
        : this(primaryPlc, backupPlcs, TimeSpan.FromSeconds(DefaultHealthCheckIntervalSeconds))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HighAvailabilityPlcManager"/> class.</summary>
    /// <param name="primaryPlc">The primary PLC.</param>
    /// <param name="backupPlcs">The backup PLCs.</param>
    /// <param name="healthCheckInterval">The health-check interval.</param>
    public HighAvailabilityPlcManager(
        IRxS7 primaryPlc,
        IList<IRxS7> backupPlcs,
        TimeSpan healthCheckInterval)
        : this(primaryPlc, backupPlcs, healthCheckInterval, TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HighAvailabilityPlcManager"/> class.</summary>
    /// <param name="primaryPlc">The primary PLC.</param>
    /// <param name="backupPlcs">The backup PLCs.</param>
    /// <param name="healthCheckInterval">The health-check interval.</param>
    /// <param name="timeProvider">The time provider.</param>
    public HighAvailabilityPlcManager(
        IRxS7 primaryPlc,
        IList<IRxS7> backupPlcs,
        TimeSpan healthCheckInterval,
        TimeProvider timeProvider)
    {
        Guard.NotNull(primaryPlc, nameof(primaryPlc));
        Guard.NotNull(backupPlcs, nameof(backupPlcs));

        _timeProvider = timeProvider;
        _backupPlcs = backupPlcs;
        _backupPlcs.Insert(0, primaryPlc);
        ActivePLC = primaryPlc;
        _healthCheckTimer = new(_ => StartHealthCheck(), null, healthCheckInterval, healthCheckInterval);
    }

    /// <summary>Gets the currently active PLC connection.</summary>
    public IRxS7 ActivePLC { get; private set; }

    /// <summary>Gets the observable stream of failover events.</summary>
    public IObservable<PlcFailoverEvent> FailoverEvents => _failoverEvents;

    /// <summary>Manually triggers a failover to the next available backup.</summary>
    /// <returns>A value indicating whether failover was successful.</returns>
    public Task<bool> TriggerFailoverAsync() => PerformFailoverAsync("Manual failover triggered.");

    /// <summary>Disposes the high-availability manager.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _healthCheckTimer.Dispose();
        _failoverEvents.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>Starts a health check without blocking the timer callback.</summary>
    private void StartHealthCheck() => _ = PerformHealthCheckAsync();

    /// <summary>Checks PLC health and initiates failover when required.</summary>
    /// <returns>A task that represents the operation.</returns>
    private async Task PerformHealthCheckAsync()
    {
        if (_disposed)
        {
            return;
        }

        var reason = ActivePLC.IsConnectedValue ? null : "Primary PLC connection lost.";
        try
        {
            if (reason is not null)
            {
                _ = await PerformFailoverAsync(reason);
            }
        }
        catch (Exception ex)
        {
            _ = await PerformFailoverAsync($"Health check failed: {ex.Message}");
        }
    }

    /// <summary>Attempts to switch to a connected PLC.</summary>
    /// <param name="reason">The failover reason.</param>
    /// <returns>A value indicating whether a connected PLC was found.</returns>
    private async Task<bool> PerformFailoverAsync(string reason)
    {
        foreach (var backupPlc in _backupPlcs)
        {
            if (backupPlc.IsConnectedValue)
            {
                var oldPlc = ActivePLC;
                ActivePLC = backupPlc;
                _failoverEvents.OnNext(new PlcFailoverEvent
                {
                    Timestamp = _timeProvider.GetUtcNow(),
                    Reason = reason,
                    OldPlc = $"{oldPlc.IP}:{oldPlc.PLCType}",
                    NewPlc = $"{backupPlc.IP}:{backupPlc.PLCType}",
                });
                return true;
            }
        }

        await Task.CompletedTask;
        return false;
    }
}
