// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.Driver.Core;

#if REACTIVELIST_REACTIVE
namespace IoT.Driver.ABPlcRx.Reactive;
#else
namespace IoT.Driver.ABPlcRx;
#endif

/// <summary>Adapts existing Allen-Bradley setup members to the common logical-tag setup contracts.</summary>
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed partial class ABLogicalTagClient
{
    /// <summary>Gets debugger-only type information without affecting the public API.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => ToString() ?? GetType().Name;

    /// <inheritdoc/>
    bool ILogicalTagRegistry.RemoveTag(string name) => RemoveTag(name);

    /// <inheritdoc/>
    Task<LogicalTag?> ILogicalTagPersistence.GetTagAsync(
        string name,
        CancellationToken cancellationToken) =>
        GetTagAsync(name, cancellationToken);

    /// <inheritdoc/>
    Task<bool> ILogicalTagPersistence.DeleteTagAsync(
        string name,
        CancellationToken cancellationToken) =>
        DeleteTagAsync(name, cancellationToken);
}
