// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using IoT.DriverCore.S7PlcRx.Reactive.Core;
#else
using IoT.DriverCore.S7PlcRx.Core;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.S7PlcRx.Reactive.BatchOperations;
#else
namespace IoT.DriverCore.S7PlcRx.BatchOperations;
#endif

/// <summary>Represents a single request to be processed as part of a batch operation.</summary>
/// <param name="type">The type of the batch request to perform.</param>
/// <param name="tag">The tag associated with the request to be processed.</param>
/// <param name="priority">The priority level assigned to the request. Defaults to RequestPriority.Normal.</param>
/// <param name="timeProvider">The time provider; defaults to <see cref="TimeProvider.System"/>.</param>
internal class BatchRequest(BatchRequestType type, Tag tag, RequestPriority priority = RequestPriority.Normal, TimeProvider? timeProvider = null)
{
    /// <summary>Gets the request type.</summary>
    internal BatchRequestType Type { get; } = type;

    /// <summary>Gets the tag to process.</summary>
    internal Tag Tag { get; } = tag;

    /// <summary>Gets the priority of the request.</summary>
    internal RequestPriority Priority { get; } = priority;

    /// <summary>Gets the timestamp when the request was created.</summary>
    internal DateTime Timestamp { get; } = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
}
