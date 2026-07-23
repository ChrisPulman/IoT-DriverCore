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

/// <summary>Executes grouped logical-tag transfers over native FINS memory-area operations.</summary>
/// <param name="memory">Native FINS memory-area operations.</param>
internal sealed class OmronLogicalBatchExecutor(IOmronMemoryAreaOperations memory)
{
    /// <summary>Identifies the FINS planner partition.</summary>
    private const string TransportPartition = "FINS";

    /// <summary>Identifies word-oriented transfers.</summary>
    private const string WordEncoding = "word";

    /// <summary>Identifies bit-oriented transfers.</summary>
    private const string BitEncoding = "bit";

    /// <summary>Gets the native FINS memory-area operations.</summary>
    private readonly IOmronMemoryAreaOperations _memory =
        memory ?? throw new ArgumentNullException(nameof(memory));

    /// <summary>Reads logical tags through the minimum compatible set of FINS memory ranges.</summary>
    /// <param name="items">Indexed logical-tag read descriptions.</param>
    /// <param name="cancellationToken">Token used to cancel protocol operations.</param>
    /// <returns>Exactly one result for every supplied item.</returns>
    internal async Task<IReadOnlyList<OmronLogicalBatchResult>> ReadManyAsync(
        IReadOnlyList<OmronLogicalBatchItem> items,
        CancellationToken cancellationToken)
    {
        if (items is null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        var results = new List<OmronLogicalBatchResult>(items.Count);
        var prepared = PrepareReadItems(items, results);
        if (prepared.Count > 0)
        {
            var planner = new TagTransferPlanner(
                new TagTransferCapabilities(_memory.MaximumReadWordCount));
            await ExecuteReadPlanAsync(
                planner.Plan(prepared.Select(static item => item.Request)),
                prepared,
                results,
                cancellationToken).ConfigureAwait(false);
        }

        return OrderResults(items, results);
    }

    /// <summary>Writes logical tags through the minimum compatible set of FINS memory ranges.</summary>
    /// <param name="items">Indexed logical-tag write descriptions.</param>
    /// <param name="cancellationToken">Token used to cancel protocol operations.</param>
    /// <returns>Exactly one result for every supplied item.</returns>
    internal async Task<IReadOnlyList<OmronLogicalBatchResult>> WriteManyAsync(
        IReadOnlyList<OmronLogicalBatchItem> items,
        CancellationToken cancellationToken)
    {
        if (items is null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        var results = new List<OmronLogicalBatchResult>(items.Count);
        var prepared = PrepareWriteItems(items, results);
        if (prepared.Count > 0)
        {
            var planner = new TagTransferPlanner(
                new TagTransferCapabilities(_memory.MaximumWriteWordCount));
            await ExecuteWritePlanAsync(
                planner.Plan(prepared.Select(static item => item.Request)),
                prepared,
                results,
                cancellationToken).ConfigureAwait(false);
        }

        return OrderResults(items, results);
    }

    /// <summary>Prepares readable items and retains address failures per item.</summary>
    /// <param name="items">Source items.</param>
    /// <param name="results">Result accumulator.</param>
    /// <returns>Items suitable for transfer planning.</returns>
    private List<OmronPreparedBatchItem> PrepareReadItems(
        IReadOnlyList<OmronLogicalBatchItem> items,
        List<OmronLogicalBatchResult> results)
    {
        var prepared = new List<OmronPreparedBatchItem>(items.Count);
        foreach (var item in items)
        {
            try
            {
                var candidate = PrepareReadItem(item);
                if (candidate.WordCount > _memory.MaximumReadWordCount)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(items),
                        $"Tag '{item.TagName}' exceeds the FINS read range limit.");
                }

                prepared.Add(candidate);
            }
            catch (Exception exception)
            {
                results.Add(OmronLogicalBatchResult.Failure(item.InputIndex, exception.Message));
            }
        }

        return prepared;
    }

    /// <summary>Prepares writable items and retains conversion or address failures per item.</summary>
    /// <param name="items">Source items.</param>
    /// <param name="results">Result accumulator.</param>
    /// <returns>Items suitable for transfer planning.</returns>
    private List<OmronPreparedBatchItem> PrepareWriteItems(
        IReadOnlyList<OmronLogicalBatchItem> items,
        List<OmronLogicalBatchResult> results)
    {
        var prepared = new List<OmronPreparedBatchItem>(items.Count);
        foreach (var item in items)
        {
            try
            {
                if (item.Value is null)
                {
                    results.Add(OmronLogicalBatchResult.Success(item.InputIndex, null));
                    continue;
                }

                var candidate = PrepareWriteItem(item);
                if (candidate.WordCount > _memory.MaximumWriteWordCount)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(items),
                        $"Tag '{item.TagName}' exceeds the FINS write range limit.");
                }

                prepared.Add(candidate);
            }
            catch (Exception exception)
            {
                results.Add(OmronLogicalBatchResult.Failure(item.InputIndex, exception.Message));
            }
        }

        return prepared;
    }

    /// <summary>Prepares one item for a read transfer.</summary>
    /// <param name="item">Source item.</param>
    /// <returns>The prepared item.</returns>
    private OmronPreparedBatchItem PrepareReadItem(OmronLogicalBatchItem item)
    {
        var (baseAddress, stringLength) = item.ValueType == typeof(string)
            ? OmronLogicalBatchCodec.ExtractStringMeta(item.Address)
            : (item.Address, 0);
        var (area, address, bitIndex) = OmronLogicalBatchCodec.ParseAddress(baseAddress);
        var isBit = item.ValueType == typeof(bool) && bitIndex.HasValue;
        var wordCount = OmronLogicalBatchCodec.GetReadWordCount(item.ValueType, stringLength);
        var transferAddress = CreateTransferAddress(
            area,
            address,
            bitIndex,
            isBit,
            wordCount,
            TagTransferAccess.Read);
        return new OmronPreparedBatchItem(
            item,
            new TagTransferRequest(item.TagName, transferAddress),
            area,
            address,
            wordCount,
            stringLength,
            null);
    }

    /// <summary>Prepares one item for a write transfer.</summary>
    /// <param name="item">Source item.</param>
    /// <returns>The prepared item.</returns>
    private OmronPreparedBatchItem PrepareWriteItem(OmronLogicalBatchItem item)
    {
        var (baseAddress, stringLength) = item.ValueType == typeof(string)
            ? OmronLogicalBatchCodec.ExtractStringMeta(item.Address)
            : (item.Address, 0);
        var (area, address, bitIndex) = OmronLogicalBatchCodec.ParseAddress(baseAddress);
        var isBit = item.ValueType == typeof(bool) && bitIndex.HasValue;
        var words = isBit ? null : OmronLogicalBatchCodec.GetWriteWords(item, stringLength);
        var wordCount = words?.Length ?? 1;
        var transferAddress = CreateTransferAddress(
            area,
            address,
            bitIndex,
            isBit,
            wordCount,
            TagTransferAccess.Write);
        return new OmronPreparedBatchItem(
            item,
            new TagTransferRequest(item.TagName, transferAddress),
            area,
            address,
            wordCount,
            stringLength,
            words);
    }

    /// <summary>Executes planned read ranges and decodes each member independently.</summary>
    /// <param name="plan">Transfer plan.</param>
    /// <param name="prepared">Prepared source items.</param>
    /// <param name="results">Result accumulator.</param>
    /// <param name="cancellationToken">Token used to cancel protocol operations.</param>
    /// <returns>A task representing the operation.</returns>
    private async Task ExecuteReadPlanAsync(
        TagTransferPlan plan,
        List<OmronPreparedBatchItem> prepared,
        List<OmronLogicalBatchResult> results,
        CancellationToken cancellationToken)
    {
        foreach (var range in plan.Ranges)
        {
            var rangeItems = range.Items.Select(item => prepared[item.InputIndex]).ToArray();
            await ExecuteReadRangeAsync(
                range,
                rangeItems,
                results,
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Executes one planned read range.</summary>
    /// <param name="range">Planned range.</param>
    /// <param name="rangeItems">Prepared range members.</param>
    /// <param name="results">Result accumulator.</param>
    /// <param name="cancellationToken">Token used to cancel protocol operations.</param>
    /// <returns>A task representing the operation.</returns>
    private async Task ExecuteReadRangeAsync(
        TagTransferRange range,
        OmronPreparedBatchItem[] rangeItems,
        List<OmronLogicalBatchResult> results,
        CancellationToken cancellationToken)
    {
        try
        {
            if (range.Address.Encoding == BitEncoding)
            {
                await ReadBitRangeAsync(range, rangeItems, results, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            await ReadWordRangeAsync(range, rangeItems, results, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            AddFailures(rangeItems, results, exception.Message);
        }
    }

    /// <summary>Reads and correlates one native FINS bit range.</summary>
    /// <param name="range">Planned range.</param>
    /// <param name="rangeItems">Prepared range members.</param>
    /// <param name="results">Result accumulator.</param>
    /// <param name="cancellationToken">Token used to cancel protocol operations.</param>
    /// <returns>A task representing the operation.</returns>
    private async Task ReadBitRangeAsync(
        TagTransferRange range,
        OmronPreparedBatchItem[] rangeItems,
        List<OmronLogicalBatchResult> results,
        CancellationToken cancellationToken)
    {
        var first = rangeItems[0];
        var bits = await _memory.ReadBitsAsync(
            first.Address,
            checked((byte)range.Offset),
            checked((byte)range.Length),
            OmronLogicalBatchCodec.ToBitType(first.Area),
            cancellationToken).ConfigureAwait(false);
        foreach (var item in rangeItems)
        {
            var offset = checked((int)(item.Request.Address.Offset - range.Offset));
            AddDecodedResult(item, bits[offset], results);
        }
    }

    /// <summary>Reads and correlates one native FINS word range.</summary>
    /// <param name="range">Planned range.</param>
    /// <param name="rangeItems">Prepared range members.</param>
    /// <param name="results">Result accumulator.</param>
    /// <param name="cancellationToken">Token used to cancel protocol operations.</param>
    /// <returns>A task representing the operation.</returns>
    private async Task ReadWordRangeAsync(
        TagTransferRange range,
        OmronPreparedBatchItem[] rangeItems,
        List<OmronLogicalBatchResult> results,
        CancellationToken cancellationToken)
    {
        var words = await _memory.ReadWordsAsync(
            checked((ushort)range.Offset),
            checked((ushort)range.Length),
            OmronLogicalBatchCodec.ToWordType(rangeItems[0].Area),
            cancellationToken).ConfigureAwait(false);
        foreach (var item in rangeItems)
        {
            try
            {
                var offset = checked((int)(item.Address - range.Offset));
                var valueWords = words.Skip(offset).Take(item.WordCount).ToArray();
                var value = OmronLogicalBatchCodec.DecodeWords(
                    item.Item.ValueType,
                    item.StringLength,
                    item.WordCount,
                    valueWords);
                AddDecodedResult(item, value, results);
            }
            catch (Exception exception)
            {
                results.Add(
                    OmronLogicalBatchResult.Failure(
                        item.Item.InputIndex,
                        exception.Message));
            }
        }
    }

    /// <summary>Executes planned write ranges and reports failures for only affected members.</summary>
    /// <param name="plan">Transfer plan.</param>
    /// <param name="prepared">Prepared source items.</param>
    /// <param name="results">Result accumulator.</param>
    /// <param name="cancellationToken">Token used to cancel protocol operations.</param>
    /// <returns>A task representing the operation.</returns>
    private async Task ExecuteWritePlanAsync(
        TagTransferPlan plan,
        List<OmronPreparedBatchItem> prepared,
        List<OmronLogicalBatchResult> results,
        CancellationToken cancellationToken)
    {
        foreach (var range in plan.Ranges)
        {
            var rangeItems = range.Items.Select(item => prepared[item.InputIndex]).ToArray();
            await ExecuteWriteRangeAsync(
                range,
                rangeItems,
                results,
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Executes one planned write range.</summary>
    /// <param name="range">Planned range.</param>
    /// <param name="rangeItems">Prepared range members.</param>
    /// <param name="results">Result accumulator.</param>
    /// <param name="cancellationToken">Token used to cancel protocol operations.</param>
    /// <returns>A task representing the operation.</returns>
    private async Task ExecuteWriteRangeAsync(
        TagTransferRange range,
        OmronPreparedBatchItem[] rangeItems,
        List<OmronLogicalBatchResult> results,
        CancellationToken cancellationToken)
    {
        try
        {
            if (range.Address.Encoding == BitEncoding)
            {
                await WriteBitRangeAsync(range, rangeItems, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await WriteWordRangeAsync(range, rangeItems, cancellationToken)
                    .ConfigureAwait(false);
            }

            AddSuccesses(rangeItems, results);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            AddFailures(rangeItems, results, exception.Message);
        }
    }

    /// <summary>Writes one native FINS bit range.</summary>
    /// <param name="range">Planned range.</param>
    /// <param name="rangeItems">Prepared range members.</param>
    /// <param name="cancellationToken">Token used to cancel protocol operations.</param>
    /// <returns>A task representing the operation.</returns>
    private Task WriteBitRangeAsync(
        TagTransferRange range,
        OmronPreparedBatchItem[] rangeItems,
        CancellationToken cancellationToken)
    {
        var bitValues = new bool[checked((int)range.Length)];
        foreach (var item in rangeItems)
        {
            var offset = checked((int)(item.Request.Address.Offset - range.Offset));
            bitValues[offset] = Convert.ToBoolean(item.Item.Value);
        }

        return _memory.WriteBitsAsync(
            bitValues,
            rangeItems[0].Address,
            checked((byte)range.Offset),
            OmronLogicalBatchCodec.ToBitType(rangeItems[0].Area),
            cancellationToken);
    }

    /// <summary>Writes one native FINS word range.</summary>
    /// <param name="range">Planned range.</param>
    /// <param name="rangeItems">Prepared range members.</param>
    /// <param name="cancellationToken">Token used to cancel protocol operations.</param>
    /// <returns>A task representing the operation.</returns>
    private Task WriteWordRangeAsync(
        TagTransferRange range,
        OmronPreparedBatchItem[] rangeItems,
        CancellationToken cancellationToken)
    {
        var wordValues = new short[checked((int)range.Length)];
        foreach (var item in rangeItems)
        {
            var offset = checked((int)(item.Address - range.Offset));
            Array.Copy(item.Words!, 0, wordValues, offset, item.Words!.Length);
        }

        return _memory.WriteWordsAsync(
            wordValues,
            checked((ushort)range.Offset),
            OmronLogicalBatchCodec.ToWordType(rangeItems[0].Area),
            cancellationToken);
    }

    /// <summary>Adds a successful decoded result.</summary>
    /// <param name="item">Prepared source item.</param>
    /// <param name="value">Decoded value.</param>
    /// <param name="results">Result accumulator.</param>
    private void AddDecodedResult(
        OmronPreparedBatchItem item,
        object? value,
        List<OmronLogicalBatchResult> results) =>
        results.Add(OmronLogicalBatchResult.Success(item.Item.InputIndex, value));

    /// <summary>Adds successful results for a completed native range.</summary>
    /// <param name="items">Prepared range members.</param>
    /// <param name="results">Result accumulator.</param>
    private void AddSuccesses(
        IEnumerable<OmronPreparedBatchItem> items,
        List<OmronLogicalBatchResult> results)
    {
        foreach (var item in items)
        {
            AddDecodedResult(item, item.Item.Value, results);
        }
    }

    /// <summary>Adds a shared failure for every member of a failed native range.</summary>
    /// <param name="items">Prepared range members.</param>
    /// <param name="results">Result accumulator.</param>
    /// <param name="error">Failure detail.</param>
    private void AddFailures(
        IEnumerable<OmronPreparedBatchItem> items,
        List<OmronLogicalBatchResult> results,
        string error)
    {
        foreach (var item in items)
        {
            results.Add(OmronLogicalBatchResult.Failure(item.Item.InputIndex, error));
        }
    }

    /// <summary>Returns results in exact source order and detects executor omissions.</summary>
    /// <param name="items">Source items.</param>
    /// <param name="results">Unordered results.</param>
    /// <returns>Ordered results.</returns>
    private OmronLogicalBatchResult[] OrderResults(
        IReadOnlyList<OmronLogicalBatchItem> items,
        IReadOnlyCollection<OmronLogicalBatchResult> results)
    {
        var byIndex = results.ToDictionary(static result => result.InputIndex);
        return items
            .Select(
                item => byIndex.TryGetValue(item.InputIndex, out var result)
                    ? result
                    : OmronLogicalBatchResult.Failure(
                        item.InputIndex,
                        "The grouped FINS operation did not return a result."))
            .ToArray();
    }

    /// <summary>Creates a planner address from parsed Omron memory coordinates.</summary>
    /// <param name="area">Parsed memory area.</param>
    /// <param name="address">Word address.</param>
    /// <param name="bitIndex">Optional bit index.</param>
    /// <param name="isBit">Whether bit addressing is required.</param>
    /// <param name="wordCount">Number of words occupied by the item.</param>
    /// <param name="access">Transfer direction.</param>
    /// <returns>The planner address.</returns>
    private TagTransportAddress CreateTransferAddress(
        string area,
        ushort address,
        byte? bitIndex,
        bool isBit,
        int wordCount,
        TagTransferAccess access)
    {
        var route = _memory.RouteIdentity;
        return isBit
            ? new TagTransportAddress(
                TransportPartition,
                OmronLogicalBatchCodec.ToBitType(area).ToString(),
                BitEncoding,
                access,
                $"{route}/{address}",
                bitIndex!.Value,
                1)
            : new TagTransportAddress(
                TransportPartition,
                OmronLogicalBatchCodec.ToWordType(area).ToString(),
                WordEncoding,
                access,
                route,
                address,
                wordCount);
    }
}
