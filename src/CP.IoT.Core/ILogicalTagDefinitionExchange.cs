// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Imports and exports logical-tag definitions using the common CSV representation.</summary>
public interface ILogicalTagDefinitionExchange
{
    /// <summary>Imports logical tags and registers them with the live client.</summary>
    /// <param name="reader">The CSV source.</param>
    /// <param name="delimiter">The field delimiter.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The imported logical tags.</returns>
    Task<IReadOnlyList<LogicalTag>> ImportCsvAsync(
        TextReader reader,
        char delimiter,
        CancellationToken cancellationToken);

    /// <summary>Exports the live catalog using the common CSV representation.</summary>
    /// <param name="writer">The CSV destination.</param>
    /// <param name="delimiter">The field delimiter.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that represents the asynchronous export operation.</returns>
    Task ExportCsvAsync(
        TextWriter writer,
        char delimiter,
        CancellationToken cancellationToken);
}
