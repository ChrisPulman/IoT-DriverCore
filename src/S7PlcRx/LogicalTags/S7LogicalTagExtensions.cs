// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using CP.IoT.Core;
#if REACTIVE_SHIM
using S7PlcRx.Reactive.Binding;

namespace S7PlcRx.Reactive.LogicalTags;

#else
using S7PlcRx.Binding;

namespace S7PlcRx.LogicalTags;

#endif

/// <summary>Creates common logical clients and typed operation-result projections for S7 callers.</summary>
public static class S7LogicalTagExtensions
{
    /// <summary>Builds a common catalog from generated S7 binding metadata.</summary>
    /// <param name="definitions">The generated S7 binding definitions.</param>
    /// <returns>The common logical-tag catalog.</returns>
    public static LogicalTagCatalog CreateLogicalTagCatalog(
        IEnumerable<S7TagDefinition> definitions)
    {
        if (definitions is null)
        {
            throw new ArgumentNullException(nameof(definitions));
        }

        var catalog = new LogicalTagCatalog();
        foreach (var definition in definitions)
        {
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ArrayLength"] = definition.ArrayLength.ToString(CultureInfo.InvariantCulture),
            };
            var accessMode = definition.Direction switch
            {
                S7TagDirection.ReadOnly => LogicalTagAccessMode.Read,
                S7TagDirection.WriteOnly => LogicalTagAccessMode.Write,
                _ => LogicalTagAccessMode.ReadWrite,
            };
            catalog.Upsert(new LogicalTag(
                definition.Name,
                definition.Address,
                definition.ValueType.FullName ?? definition.ValueType.Name,
                new LogicalTagOptions
                {
                    Metadata = metadata,
                    AccessMode = accessMode,
                    ScanInterval = definition.CanRead
                        ? TimeSpan.FromMilliseconds(definition.PollIntervalMs)
                        : null,
                }));
        }

        return catalog;
    }

    /// <summary>Reads and converts a common logical result to the requested payload type.</summary>
    /// <typeparam name="T">The requested payload type.</typeparam>
    /// <param name="client">The logical-tag client.</param>
    /// <param name="tagName">The logical tag name.</param>
    /// <param name="type">A value that supplies the requested payload type.</param>
    /// <returns>The typed tag operation result.</returns>
    public static Task<TagOperationResult<T>> ReadAsync<T>(
        ILogicalTagClient client,
        string tagName,
        T type) => ReadAsync(client, tagName, type, CancellationToken.None);

    /// <summary>Reads and converts a common logical result to the requested payload type.</summary>
    /// <typeparam name="T">The requested payload type.</typeparam>
    /// <param name="client">The logical-tag client.</param>
    /// <param name="tagName">The logical tag name.</param>
    /// <param name="type">A value that supplies the requested payload type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The typed tag operation result.</returns>
    public static async Task<TagOperationResult<T>> ReadAsync<T>(
        ILogicalTagClient client,
        string tagName,
        T type,
        CancellationToken cancellationToken)
    {
        _ = type;
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        var result = await client.ReadAsync(tagName, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return TagOperationResult<T>.Failure(result.Error);
        }

        var value = result.Value!.Value;
        if (value is T typed)
        {
            return TagOperationResult<T>.Success(typed);
        }

        try
        {
            return TagOperationResult<T>.Success(
                (T)Convert.ChangeType(value!, typeof(T), CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
        {
            return TagOperationResult<T>.Failure(ex.GetBaseException().Message);
        }
    }

    /// <summary>Writes a typed common logical value and projects the result payload.</summary>
    /// <typeparam name="T">The logical value type.</typeparam>
    /// <param name="client">The logical-tag client.</param>
    /// <param name="tagName">The logical tag name.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The typed tag operation result.</returns>
    public static Task<TagOperationResult<T>> WriteAsync<T>(
        ILogicalTagClient client,
        string tagName,
        T value,
        CancellationToken cancellationToken) =>
        WriteAsync(client, tagName, value, TimeProvider.System, cancellationToken);

    /// <summary>Writes a typed common logical value and projects the result payload.</summary>
    /// <typeparam name="T">The logical value type.</typeparam>
    /// <param name="client">The logical-tag client.</param>
    /// <param name="tagName">The logical tag name.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The typed tag operation result.</returns>
    public static async Task<TagOperationResult<T>> WriteAsync<T>(
        ILogicalTagClient client,
        string tagName,
        T value,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        if (timeProvider is null)
        {
            throw new ArgumentNullException(nameof(timeProvider));
        }

        var result = await client.WriteAsync(
            new LogicalTagValue(tagName, value, timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
        return result.Succeeded
            ? TagOperationResult<T>.Success(value)
            : TagOperationResult<T>.Failure(result.Error);
    }

    /// <summary>Creates a dynamically synchronized logical client.</summary>
    /// <param name="plc">The S7 connection.</param>
    /// <param name="catalog">The logical-tag catalog.</param>
    /// <returns>The dynamically synchronized logical client.</returns>
    public static S7LogicalTagClient CreateLogicalTagClient(
        IRxS7 plc,
        ILogicalTagCatalog catalog) => new(plc, catalog, store: null);

    /// <summary>Creates a dynamically synchronized logical client with persistence.</summary>
    /// <param name="plc">The S7 connection.</param>
    /// <param name="catalog">The logical-tag catalog.</param>
    /// <param name="store">The SQLite store.</param>
    /// <returns>The dynamically synchronized logical client.</returns>
    public static S7LogicalTagClient CreateLogicalTagClient(
        IRxS7 plc,
        ILogicalTagCatalog catalog,
        LogicalTagSqliteStore store) => new(plc, catalog, store);
}
