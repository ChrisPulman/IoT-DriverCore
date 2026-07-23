// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive.Tests;
#else

namespace IoT.DriverCore.MitsubishiRx.Tests;
#endif

/// <summary>Drives a test scheduler using a common tick-based API.</summary>
internal static class TestSchedulerDriver
{
    /// <summary>Advances the scheduler by the specified number of ticks.</summary>
    /// <param name="scheduler">The scheduler to advance.</param>
    /// <param name="ticks">The tick count to advance.</param>
    internal static void AdvanceBy(TestScheduler scheduler, long ticks)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
#if REACTIVE_SHIM
        scheduler.AdvanceBy(ticks);
#else
        scheduler.AdvanceBy(TimeSpan.FromTicks(ticks));
#endif
    }
}
