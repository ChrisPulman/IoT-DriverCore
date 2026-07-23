// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Provides time and delay operations to deterministic simulator components.</summary>
public interface ISimulatorClock
{
    /// <summary>Gets the current simulator time in UTC.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Waits for a simulator-relative duration.</summary>
    /// <param name="delay">The non-negative duration to wait.</param>
    /// <param name="cancellationToken">A token that cancels the wait.</param>
    /// <returns>A task that completes when the duration has elapsed.</returns>
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}
