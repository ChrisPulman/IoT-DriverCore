// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Provides immutable deterministic snapshots of logical-tag bulk planning and protocol dispatch activity.</summary>
public sealed class MitsubishiLogicalTagBulkOperationMetrics
{
    /// <summary>Initializes a new instance of the <see cref="MitsubishiLogicalTagBulkOperationMetrics"/> class.</summary>
    /// <param name="read">The read planning and dispatch snapshot.</param>
    /// <param name="write">The write planning and dispatch snapshot.</param>
    public MitsubishiLogicalTagBulkOperationMetrics(
        MitsubishiLogicalTagBulkDirectionMetrics read,
        MitsubishiLogicalTagBulkDirectionMetrics write)
    {
        Read = read ?? throw new ArgumentNullException(nameof(read));
        Write = write ?? throw new ArgumentNullException(nameof(write));
    }

    /// <summary>Gets the read planning and dispatch snapshot.</summary>
    public MitsubishiLogicalTagBulkDirectionMetrics Read { get; }

    /// <summary>Gets the write planning and dispatch snapshot.</summary>
    public MitsubishiLogicalTagBulkDirectionMetrics Write { get; }
}
