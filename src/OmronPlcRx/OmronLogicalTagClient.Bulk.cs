// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IoT.DriverCore.Core;
#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive;
#else
namespace IoT.DriverCore.OmronPlcRx;
#endif

/// <summary>Contains grouped FINS operations for the logical-tag client.</summary>
public sealed partial class OmronLogicalTagClient
{
    /// <summary>Reads a caller-ordered logical-tag collection through grouped operations when available.</summary>
    /// <param name="tagNames">Logical tag names in caller order.</param>
    /// <param name="cancellationToken">Token used to cancel protocol operations.</param>
    /// <returns>Exactly one result for every supplied tag name.</returns>
    private async Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> ReadManyCoreAsync(
        IReadOnlyCollection<string> tagNames,
        CancellationToken cancellationToken)
    {
        var names = tagNames.ToArray();
        if (_batchOperations is null || names.Length < 2)
        {
            var tasks = names.Select(name => ReadAsync(name, cancellationToken)).ToArray();
            return await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        var results = new TagOperationResult<LogicalTagValue>?[names.Length];
        var batchItems = new List<OmronLogicalBatchItem>(names.Length);
        for (var index = 0; index < names.Length; index++)
        {
            var name = names[index];
            if (!TryGetTag(name, LogicalTagAccessMode.Read, out var tag, out var failure))
            {
                results[index] = failure;
                continue;
            }

            batchItems.Add(
                new OmronLogicalBatchItem(
                    index,
                    tag!.Name,
                    tag.Address,
                    GetValueType(tag),
                    null));
        }

        if (batchItems.Count > 0)
        {
            try
            {
                var batchResults = await _batchOperations
                    .ReadManyAsync(batchItems, cancellationToken)
                    .ConfigureAwait(false);
                ApplyBatchResults(batchResults, batchItems, results, null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                ApplyBatchFailure(batchItems, results, exception.Message);
            }
        }

        return CompleteResults(results);
    }

    /// <summary>Writes a caller-ordered logical-tag collection through grouped operations when available.</summary>
    /// <param name="values">Logical values in caller order.</param>
    /// <param name="cancellationToken">Token used to cancel protocol operations.</param>
    /// <returns>Exactly one result for every supplied value.</returns>
    private async Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> WriteManyCoreAsync(
        IReadOnlyCollection<LogicalTagValue> values,
        CancellationToken cancellationToken)
    {
        var source = values.ToArray();
        if (_batchOperations is null || source.Length < 2)
        {
            var tasks = source.Select(value => WriteAsync(value, cancellationToken)).ToArray();
            return await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        var results = new TagOperationResult<LogicalTagValue>?[source.Length];
        var batchItems = PrepareWriteBatch(source, results);
        if (batchItems.Count > 0)
        {
            try
            {
                var batchResults = await _batchOperations
                    .WriteManyAsync(batchItems, cancellationToken)
                    .ConfigureAwait(false);
                ApplyBatchResults(batchResults, batchItems, results, source);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                ApplyBatchFailure(batchItems, results, exception.Message);
            }
        }

        return CompleteResults(results);
    }

    /// <summary>Validates and converts write values into indexed grouped items.</summary>
    /// <param name="source">Caller-ordered write values.</param>
    /// <param name="results">Caller-positioned result array.</param>
    /// <returns>Items suitable for grouped execution.</returns>
    private List<OmronLogicalBatchItem> PrepareWriteBatch(
        LogicalTagValue[] source,
        TagOperationResult<LogicalTagValue>?[] results)
    {
        var batchItems = new List<OmronLogicalBatchItem>(source.Length);
        for (var index = 0; index < source.Length; index++)
        {
            var value = source[index];
            if (value is null)
            {
                results[index] = TagOperationResult<LogicalTagValue>.Failure(
                    "Bulk values cannot contain null entries.");
                continue;
            }

            if (!TryGetTag(value.TagName, LogicalTagAccessMode.Write, out var tag, out var failure))
            {
                results[index] = failure;
                continue;
            }

            try
            {
                batchItems.Add(
                    new OmronLogicalBatchItem(
                        index,
                        tag!.Name,
                        tag.Address,
                        GetValueType(tag),
                        ConvertBatchValue(tag, value.Value)));
            }
            catch (Exception exception)
            {
                results[index] = TagOperationResult<LogicalTagValue>.Failure(exception.Message);
            }
        }

        return batchItems;
    }

    /// <summary>Applies indexed grouped results to caller-positioned logical results.</summary>
    /// <param name="batchResults">Indexed grouped operation results.</param>
    /// <param name="batchItems">Indexed grouped operation inputs.</param>
    /// <param name="results">Caller-positioned result array.</param>
    /// <param name="writes">Original write values, or <see langword="null"/> for reads.</param>
    private void ApplyBatchResults(
        IReadOnlyList<OmronLogicalBatchResult> batchResults,
        IReadOnlyList<OmronLogicalBatchItem> batchItems,
        TagOperationResult<LogicalTagValue>?[] results,
        LogicalTagValue[]? writes)
    {
        var tagNames = batchItems.ToDictionary(
            static item => item.InputIndex,
            static item => item.TagName);
        foreach (var batchResult in batchResults)
        {
            if ((uint)batchResult.InputIndex >= (uint)results.Length
                || results[batchResult.InputIndex] is not null)
            {
                continue;
            }

            if (!batchResult.Succeeded)
            {
                results[batchResult.InputIndex] =
                    TagOperationResult<LogicalTagValue>.Failure(
                        batchResult.Error ?? "The grouped FINS operation failed.");
                continue;
            }

            if (!tagNames.TryGetValue(batchResult.InputIndex, out var tagName))
            {
                continue;
            }

            var quality = writes?[batchResult.InputIndex].Quality;
            results[batchResult.InputIndex] =
                TagOperationResult<LogicalTagValue>.Success(
                    new LogicalTagValue(
                        tagName,
                        batchResult.Value,
                        _timeProvider.GetUtcNow(),
                        quality));
        }
    }
}
