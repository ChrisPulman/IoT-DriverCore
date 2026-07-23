// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Provides an immutable deterministic snapshot for one bulk transfer direction.</summary>
public sealed class MitsubishiLogicalTagBulkDirectionMetrics
{
    /// <summary>Initializes a new instance of the <see cref="MitsubishiLogicalTagBulkDirectionMetrics"/> class.</summary>
    /// <param name="planCount">The number of eligible plans created.</param>
    /// <param name="itemCount">The number of eligible word operations planned.</param>
    /// <param name="rangeCount">The number of contiguous ranges produced by the planner.</param>
    /// <param name="protocolCallCount">The number of grouped protocol calls issued.</param>
    public MitsubishiLogicalTagBulkDirectionMetrics(
        long planCount,
        long itemCount,
        long rangeCount,
        long protocolCallCount)
    {
        PlanCount = planCount;
        ItemCount = itemCount;
        RangeCount = rangeCount;
        ProtocolCallCount = protocolCallCount;
    }

    /// <summary>Gets the number of eligible plans created.</summary>
    public long PlanCount { get; }

    /// <summary>Gets the number of eligible word operations planned.</summary>
    public long ItemCount { get; }

    /// <summary>Gets the number of contiguous ranges produced by the planner.</summary>
    public long RangeCount { get; }

    /// <summary>Gets the number of grouped protocol calls issued.</summary>
    public long ProtocolCallCount { get; }
}
