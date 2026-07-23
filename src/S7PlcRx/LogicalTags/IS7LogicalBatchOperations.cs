// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.S7PlcRx.Reactive.LogicalTags;

#else
namespace IoT.DriverCore.S7PlcRx.LogicalTags;

#endif

/// <summary>Abstracts the concrete S7 multi-variable operations used by logical-tag batches.</summary>
internal interface IS7LogicalBatchOperations
{
    /// <summary>Reads the supplied runtime tags in one S7 operation.</summary>
    /// <param name="tags">The runtime tags to read.</param>
    /// <returns>The values keyed by runtime-tag name, or <see langword="null"/> when unavailable.</returns>
    IReadOnlyDictionary<string, object?>? ReadMultiple(IReadOnlyList<Tag> tags);

    /// <summary>Writes the supplied runtime tags in one S7 operation.</summary>
    /// <param name="tags">The runtime tags to write.</param>
    /// <returns><see langword="true"/> when the operation succeeds.</returns>
    bool WriteMultiple(IReadOnlyList<Tag> tags);
}
