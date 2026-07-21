// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.IoT.Core;

/// <summary>Reads logical PLC tag values.</summary>
public interface ILogicalTagReader
{
    /// <summary>Reads the value of a single tag.</summary>
    /// <param name="tagName">The logical tag name.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A result containing the tag value or an error message.</returns>
    Task<TagOperationResult<LogicalTagValue>> ReadAsync(string tagName, CancellationToken cancellationToken);

    /// <summary>Reads multiple tag values while preserving input order.</summary>
    /// <param name="tagNames">The tag names to read.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>Results in the same order as <paramref name="tagNames"/>.</returns>
    Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> ReadManyAsync(
        IReadOnlyCollection<string> tagNames,
        CancellationToken cancellationToken);
}
