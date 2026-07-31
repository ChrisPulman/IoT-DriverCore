// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.Driver.Core;

#if REACTIVE_SHIM

namespace IoT.Driver.MitsubishiRx.Reactive;

#else

namespace IoT.Driver.MitsubishiRx;

#endif

/// <summary>Composes grouped logical-tag transfer helpers with Mitsubishi protocol transports.</summary>
public sealed partial class MitsubishiLogicalTagClient
{
    /// <summary>Groups planned ranges by memory area while preserving their first-seen order.</summary>
    /// <param name="ranges">The planner ranges.</param>
    /// <returns>The memory-area groups.</returns>
    private static List<TagTransferRange[]> GroupRangesByMemoryArea(
        IReadOnlyList<TagTransferRange> ranges)
    {
        var groupsByMemoryArea = new Dictionary<string, List<TagTransferRange>>(
            StringComparer.Ordinal);
        var orderedGroups = new List<List<TagTransferRange>>();
        foreach (var range in ranges)
        {
            if (!groupsByMemoryArea.TryGetValue(range.Address.MemoryArea, out var group))
            {
                group = [];
                groupsByMemoryArea.Add(range.Address.MemoryArea, group);
                orderedGroups.Add(group);
            }

            group.Add(range);
        }

        var groups = new List<TagTransferRange[]>(orderedGroups.Count);
        foreach (var group in orderedGroups)
        {
            groups.Add([.. group]);
        }

        return groups;
    }

    /// <summary>Gets the requests represented by one transfer range.</summary>
    /// <param name="range">The transfer range.</param>
    /// <param name="requests">All requests.</param>
    /// <returns>The range requests in planner order.</returns>
    private static BulkWordRequest[] GetRangeRequests(
        TagTransferRange range,
        IReadOnlyList<BulkWordRequest> requests)
    {
        var rangeRequests = new BulkWordRequest[range.Items.Count];
        for (var index = 0; index < range.Items.Count; index++)
        {
            rangeRequests[index] = requests[range.Items[index].InputIndex];
        }

        return rangeRequests;
    }

    /// <summary>Gets and caller-orders requests across compatible transfer ranges.</summary>
    /// <param name="ranges">The compatible transfer ranges.</param>
    /// <param name="requests">All requests.</param>
    /// <returns>The caller-ordered requests.</returns>
    private static BulkWordRequest[] GetOrderedRangeRequests(
        IReadOnlyList<TagTransferRange> ranges,
        IReadOnlyList<BulkWordRequest> requests)
    {
        var rangeRequests = new List<BulkWordRequest>();
        foreach (var range in ranges)
        {
            foreach (var item in range.Items)
            {
                rangeRequests.Add(requests[item.InputIndex]);
            }
        }

        rangeRequests.Sort(static (left, right) => left.Index.CompareTo(right.Index));
        return [.. rangeRequests];
    }

    /// <summary>Gets the request at the starting address of one transfer range.</summary>
    /// <param name="requests">The range requests.</param>
    /// <param name="offset">The range start offset.</param>
    /// <returns>The request at the range start.</returns>
    private static BulkWordRequest GetRangeStartRequest(
        IReadOnlyList<BulkWordRequest> requests,
        long offset)
    {
        foreach (var request in requests)
        {
            if (request.Address.Number == offset)
            {
                return request;
            }
        }

        throw new InvalidOperationException("The transfer range does not contain its starting address.");
    }

    /// <summary>Copies one protocol-sized request chunk.</summary>
    /// <param name="requests">The complete request list.</param>
    /// <param name="offset">The first request index.</param>
    /// <param name="count">The request count.</param>
    /// <returns>The copied chunk.</returns>
    private static BulkWordRequest[] CopyRequestChunk(
        IReadOnlyList<BulkWordRequest> requests,
        int offset,
        int count)
    {
        var chunk = new BulkWordRequest[count];
        for (var index = 0; index < count; index++)
        {
            chunk[index] = requests[offset + index];
        }

        return chunk;
    }

    /// <summary>Determines whether a request set writes the same address more than once.</summary>
    /// <param name="requests">The requests to inspect.</param>
    /// <returns><see langword="true"/> when an address appears more than once.</returns>
    private static bool HasDuplicateAddresses(IReadOnlyList<BulkWordRequest> requests)
    {
        var addresses = new HashSet<int>();
        foreach (var request in requests)
        {
            if (!addresses.Add(request.Address.Number))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Executes all eligible grouped reads.</summary>
    /// <param name="requests">The indexed eligible requests.</param>
    /// <param name="results">The caller-ordered result array.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when every group has been read.</returns>
    private async Task ExecuteBulkReadsAsync(
        IReadOnlyList<BulkWordRequest> requests,
        TagOperationResult<LogicalTagValue>[] results,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
        {
            return;
        }

        var plan = CreateBulkTransferPlan(requests, TagTransferAccess.Read);
        _ = Interlocked.Increment(ref _bulkReadPlanCount);
        _ = Interlocked.Add(ref _bulkReadItemCount, requests.Count);
        _ = Interlocked.Add(ref _bulkReadRangeCount, plan.Ranges.Count);
        foreach (var ranges in GroupRangesByMemoryArea(plan.Ranges))
        {
            if (ranges.Length == 1)
            {
                await ExecuteContiguousReadAsync(
                        ranges[0],
                        requests,
                        results,
                        cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            var groupedRequests = GetOrderedRangeRequests(ranges, requests);
            if (SupportsRandomWordCommands())
            {
                await ExecuteRandomReadsAsync(
                        groupedRequests,
                        results,
                        cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            foreach (var range in ranges)
            {
                await ExecuteContiguousReadAsync(
                        range,
                        requests,
                        results,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    /// <summary>Executes one contiguous word read and correlates values to caller indexes.</summary>
    /// <param name="range">The planned contiguous range.</param>
    /// <param name="requests">All eligible requests.</param>
    /// <param name="results">The caller-ordered result array.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the range has been read.</returns>
    private async Task ExecuteContiguousReadAsync(
        TagTransferRange range,
        IReadOnlyList<BulkWordRequest> requests,
        TagOperationResult<LogicalTagValue>[] results,
        CancellationToken cancellationToken)
    {
        var groupedRequests = GetRangeRequests(range, requests);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var first = GetRangeStartRequest(groupedRequests, range.Offset);
            _ = Interlocked.Increment(ref _bulkReadProtocolCallCount);
            var response = await _owner
                .ReadWordsAsync(
                    first.Address.Original,
                    checked((int)range.Length),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSucceed || response.Value is null)
            {
                SetBulkFailures(
                    BulkReadOperation,
                    groupedRequests,
                    GetError(response),
                    results);
                return;
            }

            if (response.Value.Length != range.Length)
            {
                SetBulkFailures(
                    BulkReadOperation,
                    groupedRequests,
                    $"Expected {range.Length} words but received {response.Value.Length}.",
                    results);
                return;
            }

            var timestamp = _timeProvider.GetUtcNow();
            foreach (var request in groupedRequests)
            {
                var word = response.Value[checked(request.Address.Number - (int)range.Offset)];
                results[request.Index] = CreateBulkReadSuccess(request, word, timestamp);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            SetBulkFailures(
                BulkReadOperation,
                groupedRequests,
                ex.GetBaseException().Message,
                results);
        }
    }

    /// <summary>Executes random-word reads in protocol-sized chunks.</summary>
    /// <param name="requests">The memory-area-compatible requests.</param>
    /// <param name="results">The caller-ordered result array.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when every chunk has been read.</returns>
    private async Task ExecuteRandomReadsAsync(
        IReadOnlyList<BulkWordRequest> requests,
        TagOperationResult<LogicalTagValue>[] results,
        CancellationToken cancellationToken)
    {
        for (var offset = 0; offset < requests.Count; offset += MaximumRandomWordCount)
        {
            var count = Math.Min(MaximumRandomWordCount, requests.Count - offset);
            var chunk = CopyRequestChunk(requests, offset, count);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = Interlocked.Increment(ref _bulkReadProtocolCallCount);
                var addresses = new string[chunk.Length];
                for (var index = 0; index < chunk.Length; index++)
                {
                    addresses[index] = chunk[index].Address.Original;
                }

                var response = await _owner
                    .RandomReadWordsAsync(addresses, cancellationToken)
                    .ConfigureAwait(false);
                if (!response.IsSucceed || response.Value is null)
                {
                    SetBulkFailures(BulkReadOperation, chunk, GetError(response), results);
                    continue;
                }

                if (response.Value.Length != chunk.Length)
                {
                    SetBulkFailures(
                        BulkReadOperation,
                        chunk,
                        $"Expected {chunk.Length} words but received {response.Value.Length}.",
                        results);
                    continue;
                }

                var timestamp = _timeProvider.GetUtcNow();
                for (var index = 0; index < chunk.Length; index++)
                {
                    var request = chunk[index];
                    results[request.Index] = CreateBulkReadSuccess(
                        request,
                        response.Value[index],
                        timestamp);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                SetBulkFailures(
                    BulkReadOperation,
                    chunk,
                    ex.GetBaseException().Message,
                    results);
            }
        }
    }

    /// <summary>Executes all eligible grouped writes.</summary>
    /// <param name="requests">The indexed eligible requests.</param>
    /// <param name="results">The caller-ordered result array.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when every group has been written.</returns>
    private async Task ExecuteBulkWritesAsync(
        IReadOnlyList<BulkWordRequest> requests,
        TagOperationResult<LogicalTagValue>[] results,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
        {
            return;
        }

        var plan = CreateBulkTransferPlan(requests, TagTransferAccess.Write);
        _ = Interlocked.Increment(ref _bulkWritePlanCount);
        _ = Interlocked.Add(ref _bulkWriteItemCount, requests.Count);
        _ = Interlocked.Add(ref _bulkWriteRangeCount, plan.Ranges.Count);
        foreach (var ranges in GroupRangesByMemoryArea(plan.Ranges))
        {
            var groupedRequests = GetOrderedRangeRequests(ranges, requests);
            var hasDuplicateAddresses = HasDuplicateAddresses(groupedRequests);
            if (ranges.Length == 1 && !hasDuplicateAddresses)
            {
                await ExecuteContiguousWriteAsync(
                        ranges[0],
                        requests,
                        results,
                        cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            if (SupportsRandomWordCommands())
            {
                await ExecuteRandomWritesAsync(
                        groupedRequests,
                        results,
                        cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            foreach (var request in groupedRequests)
            {
                var result = await WriteAsync(request.Value!, cancellationToken).ConfigureAwait(false);
                results[request.Index] = result.Succeeded
                    ? result
                    : CreateIndexedFailure(
                        BulkWriteOperation,
                        request.Index,
                        request.Tag.Name,
                        result.Error);
            }
        }
    }

    /// <summary>Executes one contiguous word write and correlates success to caller indexes.</summary>
    /// <param name="range">The planned contiguous range.</param>
    /// <param name="requests">All eligible requests.</param>
    /// <param name="results">The caller-ordered result array.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the range has been written.</returns>
    private async Task ExecuteContiguousWriteAsync(
        TagTransferRange range,
        IReadOnlyList<BulkWordRequest> requests,
        TagOperationResult<LogicalTagValue>[] results,
        CancellationToken cancellationToken)
    {
        var groupedRequests = GetRangeRequests(range, requests);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var first = GetRangeStartRequest(groupedRequests, range.Offset);
            var words = new ushort[checked((int)range.Length)];
            foreach (var request in groupedRequests)
            {
                words[checked(request.Address.Number - (int)range.Offset)] = request.Word!.Value;
            }

            _ = Interlocked.Increment(ref _bulkWriteProtocolCallCount);
            var response = await _owner
                .WriteWordsAsync(first.Address.Original, words, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSucceed)
            {
                SetBulkFailures(
                    BulkWriteOperation,
                    groupedRequests,
                    GetError(response),
                    results);
                return;
            }

            SetBulkWriteSuccesses(groupedRequests, results);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            SetBulkFailures(
                BulkWriteOperation,
                groupedRequests,
                ex.GetBaseException().Message,
                results);
        }
    }

    /// <summary>Executes random-word writes in protocol-sized chunks.</summary>
    /// <param name="requests">The memory-area-compatible requests.</param>
    /// <param name="results">The caller-ordered result array.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when every chunk has been written.</returns>
    private async Task ExecuteRandomWritesAsync(
        IReadOnlyList<BulkWordRequest> requests,
        TagOperationResult<LogicalTagValue>[] results,
        CancellationToken cancellationToken)
    {
        for (var offset = 0; offset < requests.Count; offset += MaximumRandomWordCount)
        {
            var count = Math.Min(MaximumRandomWordCount, requests.Count - offset);
            var chunk = CopyRequestChunk(requests, offset, count);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = Interlocked.Increment(ref _bulkWriteProtocolCallCount);
                var values = new KeyValuePair<string, ushort>[chunk.Length];
                for (var index = 0; index < chunk.Length; index++)
                {
                    var request = chunk[index];
                    values[index] = new(
                        request.Address.Original,
                        request.Word!.Value);
                }

                var response = await _owner
                    .RandomWriteWordsAsync(values, cancellationToken)
                    .ConfigureAwait(false);
                if (!response.IsSucceed)
                {
                    SetBulkFailures(BulkWriteOperation, chunk, GetError(response), results);
                    continue;
                }

                SetBulkWriteSuccesses(chunk, results);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                SetBulkFailures(
                    BulkWriteOperation,
                    chunk,
                    ex.GetBaseException().Message,
                    results);
            }
        }
    }

    /// <summary>Stores successful write results for a completed protocol request.</summary>
    /// <param name="requests">The completed requests.</param>
    /// <param name="results">The caller-ordered result array.</param>
    private void SetBulkWriteSuccesses(
        IEnumerable<BulkWordRequest> requests,
        TagOperationResult<LogicalTagValue>[] results)
    {
        var timestamp = _timeProvider.GetUtcNow();
        foreach (var request in requests)
        {
            results[request.Index] = TagOperationResult<LogicalTagValue>.Success(
                new(
                    request.Tag.Name,
                    request.Value!.Value,
                    timestamp,
                    "Good"));
        }
    }
}
