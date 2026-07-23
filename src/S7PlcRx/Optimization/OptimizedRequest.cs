// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.S7PlcRx.Reactive.Optimization;
#else
namespace IoT.DriverCore.S7PlcRx.Optimization;
#endif

/// <summary>Represents a request for tag optimization, including its type, priority, and associated metadata.</summary>
/// <param name="tag">The tag to be processed by the optimization request. Cannot be null.</param>
/// <param name="requestType">The type of optimization to perform for the request.</param>
/// <param name="priority">
/// The priority level assigned to the request. Defaults to <see cref="OptimizationRequestPriority.Normal"/>.
/// </param>
/// <param name="timeProvider">The time provider; defaults to <see cref="TimeProvider.System"/>.</param>
internal class OptimizedRequest(
    Tag tag,
    OptimizedRequestType requestType,
    OptimizationRequestPriority priority = OptimizationRequestPriority.Normal,
    TimeProvider? timeProvider = null)
{
    /// <summary>Gets the tag to process.</summary>
    internal Tag Tag { get; } = tag;

    /// <summary>Gets the type of the optimized request associated with this instance.</summary>
    internal OptimizedRequestType RequestType { get; } = requestType;

    /// <summary>Gets the priority level assigned to the optimization request.</summary>
    internal OptimizationRequestPriority Priority { get; } = priority;

    /// <summary>Gets the UTC timestamp indicating when the object was created.</summary>
    internal DateTime Timestamp { get; } = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

    /// <summary>Gets the completion source for async operations.</summary>
    internal TaskCompletionSource<bool>? CompletionSource { get; } = new();
}
