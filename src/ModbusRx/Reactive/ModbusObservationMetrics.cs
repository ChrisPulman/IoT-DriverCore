// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using IoT.DriverCore.ModbusRx.Reactive.Data;
#else
using IoT.DriverCore.ModbusRx.Data;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive;
#else
namespace IoT.DriverCore.ModbusRx;
#endif

/// <summary>Provides deterministic work counters for event-driven Modbus server observation.</summary>
public sealed class ModbusObservationMetrics
{
    /// <summary>Stores accepted write notifications.</summary>
    private long _writeNotifications;

    /// <summary>Stores snapshots constructed from accepted notifications.</summary>
    private long _snapshotsCreated;

    /// <summary>Stores snapshots emitted to observers.</summary>
    private long _snapshotsEmitted;

    /// <summary>Gets the number of accepted data-store write notifications.</summary>
    public long WriteNotifications => Interlocked.Read(ref _writeNotifications);

    /// <summary>Gets the number of snapshots constructed from accepted notifications.</summary>
    public long SnapshotsCreated => Interlocked.Read(ref _snapshotsCreated);

    /// <summary>Gets the number of snapshots emitted to observers.</summary>
    public long SnapshotsEmitted => Interlocked.Read(ref _snapshotsEmitted);

    /// <summary>Records an accepted write notification.</summary>
    /// <param name="eventArgs">The data-store event that triggered observation.</param>
    internal void RecordWriteNotification(DataStoreEventArgs eventArgs)
    {
        if (eventArgs is null)
        {
            throw new ArgumentNullException(nameof(eventArgs));
        }

        _ = Interlocked.Increment(ref _writeNotifications);
    }

    /// <summary>Records a snapshot construction.</summary>
    internal void RecordSnapshotCreated() => _ = Interlocked.Increment(ref _snapshotsCreated);

    /// <summary>Records a snapshot emission.</summary>
    internal void RecordSnapshotEmitted() => _ = Interlocked.Increment(ref _snapshotsEmitted);
}
