// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Performance;
#else
namespace S7PlcRx.Performance;
#endif

/// <summary>Provides metrics related to a connection, including reconnection count and uptime.</summary>
/// <remarks>This class is intended for internal use to track basic connection statistics. It is not
/// thread-safe.</remarks>
internal sealed class SimpleConnectionMetrics
{
    /// <summary>Stores the time provider used by this instance.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>Stores the s ta rt ti m e used by this instance.</summary>
    private readonly DateTime _startTime;

    /// <summary>Initializes a new instance of the <see cref="SimpleConnectionMetrics"/> class.</summary>
    /// <param name="timeProvider">The time provider; defaults to <see cref="TimeProvider.System"/>.</param>
    internal SimpleConnectionMetrics(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _startTime = _timeProvider.GetUtcNow().UtcDateTime;
    }

    /// <summary>Gets the number of reconnections.</summary>
    internal int ReconnectionCount { get; private set; }

    /// <summary>Gets the connection uptime.</summary>
    /// <returns>Connection uptime.</returns>
    internal TimeSpan GetUptime() => _timeProvider.GetUtcNow().UtcDateTime - _startTime;

    /// <summary>Records a reconnection.</summary>
    internal void RecordReconnection() => ReconnectionCount++;
}
