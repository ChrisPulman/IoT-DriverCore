// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive;
#else
namespace IoT.DriverCore.OmronPlcRx;
#endif

/// <summary>Abstracts grouped FINS memory operations used by the logical-tag client.</summary>
internal interface IOmronLogicalBatchOperations
{
    /// <summary>Reads compatible logical tags through grouped FINS memory operations.</summary>
    /// <param name="items">Indexed logical-tag read descriptions.</param>
    /// <param name="cancellationToken">Token used to cancel protocol operations.</param>
    /// <returns>Exactly one indexed result for every supplied item.</returns>
    Task<IReadOnlyList<OmronLogicalBatchResult>> ReadManyAsync(
        IReadOnlyList<OmronLogicalBatchItem> items,
        CancellationToken cancellationToken);

    /// <summary>Writes compatible logical tags through grouped FINS memory operations.</summary>
    /// <param name="items">Indexed logical-tag write descriptions.</param>
    /// <param name="cancellationToken">Token used to cancel protocol operations.</param>
    /// <returns>Exactly one indexed result for every supplied item.</returns>
    Task<IReadOnlyList<OmronLogicalBatchResult>> WriteManyAsync(
        IReadOnlyList<OmronLogicalBatchItem> items,
        CancellationToken cancellationToken);
}
