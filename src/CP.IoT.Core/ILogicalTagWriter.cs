// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Asynchronously writes logical tags.</summary>
public interface ILogicalTagWriter
{
    /// <summary>Writes one logical tag value.</summary>
    /// <param name="value">The value to write.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The operation result containing the accepted value or an error message.</returns>
    Task<TagOperationResult<LogicalTagValue>> WriteAsync(LogicalTagValue value, CancellationToken cancellationToken);

    /// <summary>Writes a collection of logical tag values.</summary>
    /// <param name="values">The values to write.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The operation results in the same order as <paramref name="values"/>.</returns>
    Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> WriteManyAsync(
        IReadOnlyCollection<LogicalTagValue> values,
        CancellationToken cancellationToken);
}
