// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.S7PlcRx.Reactive.LogicalTags;

#else
namespace IoT.DriverCore.S7PlcRx.LogicalTags;

#endif

/// <summary>Adapts a concrete <see cref="RxS7"/> connection to logical batch operations.</summary>
/// <param name="plc">The concrete S7 connection.</param>
internal sealed class RxS7LogicalBatchOperations(RxS7 plc) : IS7LogicalBatchOperations
{
    /// <inheritdoc />
    public IReadOnlyDictionary<string, object?>? ReadMultiple(IReadOnlyList<Tag> tags) =>
        plc.ReadMultiVar(tags);

    /// <inheritdoc />
    public bool WriteMultiple(IReadOnlyList<Tag> tags) => plc.WriteMultiVar(tags);
}
