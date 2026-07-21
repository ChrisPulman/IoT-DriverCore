// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
#if REACTIVE_SHIM
using S7PlcRx.Reactive.Cache;
#else
using S7PlcRx.Cache;
#endif

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Optimization;
#else
namespace S7PlcRx.Optimization;
#endif

/// <summary>
/// Provides batch processing and caching for optimized requests, enabling efficient handling of repeated or concurrent
/// operations.
/// </summary>
/// <remarks>The OptimizationEngine is intended for internal use to improve performance by batching similar
/// requests and caching their results. It manages request queuing, cache maintenance, and periodic batch execution.
/// This class is not thread-safe for direct external manipulation, but its public methods are designed to be safe for
/// concurrent use. Dispose the instance when it is no longer needed to release resources.</remarks>
internal class OptimizationEngine : IDisposable
{
    /// <summary>Defines the default interval between batch processing operations.</summary>
    private const int DefaultBatchIntervalMilliseconds = 50;

    /// <summary>Defines the default maximum number of requests processed in each batch.</summary>
    private const int DefaultMaximumBatchSize = 20;

    /// <summary>Defines the length of the DB address prefix.</summary>
    private const int DataBlockPrefixLength = 2;

    /// <summary>Defines the first valid separator position in a data-block address.</summary>
    private const int MinimumDataBlockDotIndex = 3;

    /// <summary>Defines how long batch processing waits to acquire its lock.</summary>
    private const int ProcessingLockTimeoutMilliseconds = 100;

    /// <summary>Stores the r eq ue st qu e u e used by this instance.</summary>
    private readonly ConcurrentQueue<OptimizedRequest> _requestQueue = new();

    /// <summary>Stores the v al ue ca c h e used by this instance.</summary>
    private readonly ConcurrentDictionary<string, CachedValue> _valueCache = new();

    /// <summary>Stores the batch processing interval.</summary>
    private readonly TimeSpan _batchInterval;

    /// <summary>Protects lazy timer initialization.</summary>
    private readonly Lock _timerLock = new();

    /// <summary>Stores the p ro ce ss in gl o c k used by this instance.</summary>
    private readonly SemaphoreSlim _processingLock = new(1, 1);

    /// <summary>Stores the a xb at ch si z e used by this instance.</summary>
    private readonly int _maxBatchSize;

    /// <summary>Stores the time provider used by this instance.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>Stores the batch timer used by this instance.</summary>
    private Timer? _batchTimer;

    /// <summary>Tracks whether this instance has released its resources.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="OptimizationEngine"/> class.</summary>
    public OptimizationEngine()
        : this(DefaultBatchIntervalMilliseconds, DefaultMaximumBatchSize)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="OptimizationEngine"/> class.</summary>
    /// <param name="batchIntervalMs">The batch processing interval in milliseconds.</param>
    /// <param name="maxBatchSize">The maximum batch size.</param>
    /// <param name="timeProvider">The time provider; defaults to <see cref="TimeProvider.System"/>.</param>
    public OptimizationEngine(int batchIntervalMs, int maxBatchSize, TimeProvider? timeProvider = null)
    {
        _maxBatchSize = maxBatchSize;
        _batchInterval = TimeSpan.FromMilliseconds(batchIntervalMs);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Gets the current statistics for the cache, including the number of cached values, pending requests, and the
    /// cache hit ratio.
    /// </summary>
    /// <remarks>Use this property to monitor cache performance and usage patterns. The returned statistics
    /// represent a snapshot at the time the property is accessed and may change as cache activity occurs.</remarks>
    internal CacheStatistics CacheStats => new()
    {
        CachedValueCount = _valueCache.Count,
        PendingRequestCount = _requestQueue.Count,
        CacheHitRatio = CalculateCacheHitRatio(),
    };

    /// <summary>Adds the specified request to the processing queue.</summary>
    /// <param name="request">The request to enqueue for processing. Cannot be null.</param>
    internal void EnqueueRequest(OptimizedRequest request)
    {
        EnsureTimerStarted();
        _requestQueue.Enqueue(request);
    }

    /// <summary>
    /// Retrieves a cached value associated with the specified tag name if it exists and is not older than the specified
    /// maximum age.
    /// </summary>
    /// <param name="tagName">The tag name used to identify the cached value. Cannot be null.</param>
    /// <param name="maxAge">
    /// The maximum allowed age of the cached value. Values older than this duration are considered expired.
    /// </param>
    /// <returns>
    /// The cached value associated with the specified tag name if it exists and is not expired; otherwise,
    /// <see langword="null"/>.
    /// </returns>
    internal object? GetCachedValue(string tagName, TimeSpan maxAge)
    {
        if (!_valueCache.TryGetValue(tagName, out var cachedValue) ||
            _timeProvider.GetUtcNow().UtcDateTime - cachedValue.Timestamp > maxAge)
        {
            return null;
        }

        cachedValue.HitCount++;
        return cachedValue.Value;
    }

    /// <summary>Updates or adds a cached tag value.</summary>
    /// <param name="tagName">The unique tag name used to identify the cached value. Cannot be null.</param>
    /// <param name="value">The value to store in the cache for the specified tag name.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void UpdateCache(string tagName, object value) => _valueCache.AddOrUpdate(
            tagName,
            new CachedValue(value, _timeProvider.GetUtcNow().UtcDateTime),
            (_, existing) => new CachedValue(value, _timeProvider.GetUtcNow().UtcDateTime, existing.HitCount));

    /// <summary>Removes all cache entries that have expired based on the specified maximum age.</summary>
    /// <remarks>Use this method to periodically clean up expired items and free memory. The method compares
    /// each entry's timestamp to the current UTC time minus the specified maximum age.</remarks>
    /// <param name="maxAge">
    /// The maximum duration a cache entry is considered valid. Entries older than this value are removed.
    /// </param>
    internal void ClearExpiredCache(TimeSpan maxAge)
    {
        var cutoff = _timeProvider.GetUtcNow().UtcDateTime - maxAge;
        var expiredKeys = new List<string>();
        foreach (var kvp in _valueCache)
        {
            if (kvp.Value.Timestamp < cutoff)
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        foreach (var key in expiredKeys)
        {
            _ = _valueCache.TryRemove(key, out _);
        }
    }

    /// <summary>Releases resources owned by the optimization engine.</summary>
    /// <param name="disposing">
    /// <see langword="true"/> to release managed resources; otherwise, <see langword="false"/>.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _batchTimer?.Dispose();
            _processingLock.Dispose();
            _valueCache.Clear();
        }

        _disposed = true;
    }

    /// <summary>Disposes the optimization engine.</summary>
    void IDisposable.Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Processes optimized requests that target the same data block.</summary>
    /// <remarks>Batching requests reduces network round trips and can significantly enhance throughput when
    /// multiple operations target the same data block. Each request's completion is signaled via its associated
    /// completion source.</remarks>
    /// <param name="requests">
    /// A list of <see cref="OptimizedRequest"/> objects representing the requests to be processed as a batch. Cannot be
    /// <see langword="null"/>.
    /// </param>
    private static void ProcessDataBlockBatch(List<OptimizedRequest> requests)
    {
        // Implementation would batch read/write requests for the same data block
        // This would significantly improve performance by reducing network round trips
        foreach (var request in requests)
        {
            try
            {
                // Process individual request (this would be replaced with actual batch logic)
                request.CompletionSource?.SetResult(true);
            }
            catch (Exception ex)
            {
                request.CompletionSource?.SetException(ex);
            }
        }
    }

    /// <summary>Processes a batch of optimized requests by grouping them for efficient data block access.</summary>
    /// <remarks>Requests are grouped by their associated data block to maximize batch processing efficiency.
    /// If an error occurs while processing a group, the error is logged and processing continues with the remaining
    /// groups.</remarks>
    /// <param name="requests">
    /// The collection of optimized requests to process. Cannot be null and must contain only valid requests.
    /// </param>
    private static void ProcessRequestBatch(List<OptimizedRequest> requests)
    {
        // Group requests by data block for optimal batch reading
        var groupedRequests = new Dictionary<int, List<OptimizedRequest>>();
        foreach (var request in requests)
        {
            var dataBlock = GetDataBlockFromAddress(request.Tag.Address);
            if (!groupedRequests.TryGetValue(dataBlock, out var group))
            {
                group = [];
                groupedRequests[dataBlock] = group;
            }

            group.Add(request);
        }

        var orderedGroups = new List<KeyValuePair<int, List<OptimizedRequest>>>(groupedRequests);
        orderedGroups.Sort(static (left, right) => right.Value.Count.CompareTo(left.Value.Count));

        foreach (var group in orderedGroups)
        {
            try
            {
                ProcessDataBlockBatch(group.Value);
            }
            catch (Exception ex)
            {
                // Log error and continue with other groups
                System.Diagnostics.Debug.WriteLine($"Batch processing error for DB{group.Key}: {ex.Message}");
            }
        }
    }

    /// <summary>Extracts the data block number from a PLC address string in the format "DB{number}.{...}".</summary>
    /// <remarks>This method returns -1 if the address is null, empty, does not start with "DB", or does not
    /// contain a valid data block number before the first dot.</remarks>
    /// <param name="address">
    /// The PLC address string from which to extract the data block number. The address must start with "DB" followed
    /// by the block number and a dot (for example, "DB10.DBX0.0").
    /// </param>
    /// <returns>
    /// The data block number if the address is valid and contains a parsable block number; otherwise, -1.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetDataBlockFromAddress(string? address)
    {
        if (address is not { Length: > 0 })
        {
            return -1;
        }

        if (!address.StartsWith("DB", StringComparison.Ordinal))
        {
            return -1;
        }

        var dotIndex = address.IndexOf('.');
        if (dotIndex < MinimumDataBlockDotIndex)
        {
            return -1;
        }

        return int.TryParse(address[DataBlockPrefixLength..dotIndex], out var dbNumber) ? dbNumber : -1;
    }

    /// <summary>Processes a batch of queued requests, up to the configured maximum batch size.</summary>
    /// <remarks>This method is intended to be invoked by a timer or background scheduler. If the processing
    /// lock cannot be acquired within 100 milliseconds, the method exits without processing any requests. The method
    /// processes requests in batches to improve throughput and efficiency.</remarks>
    private void BeginProcessBatchedRequests() => _ = ProcessBatchedRequestsAsync();

    /// <summary>Starts the batching timer after construction when it is first needed.</summary>
    private void EnsureTimerStarted()
    {
        if (Volatile.Read(ref _batchTimer) is not null)
        {
            return;
        }

        lock (_timerLock)
        {
            if (Volatile.Read(ref _batchTimer) is null)
            {
                _batchTimer = new(
                    static state => (state as OptimizationEngine)?.BeginProcessBatchedRequests(),
                    this,
                    _batchInterval,
                    _batchInterval);
            }
        }
    }

    /// <summary>Processes the next queued batch asynchronously.</summary>
    /// <returns>A task representing the asynchronous batch operation.</returns>
    private async Task ProcessBatchedRequestsAsync()
    {
        // Quick timeout to avoid blocking
        if (!await _processingLock.WaitAsync(ProcessingLockTimeoutMilliseconds))
        {
            return;
        }

        try
        {
            var requests = new List<OptimizedRequest>();

            // Dequeue up to maxBatchSize requests
            for (var processed = 0;
                _requestQueue.TryDequeue(out var request) && processed < _maxBatchSize;
                processed++)
            {
                requests.Add(request);
            }

            if (requests.Count > 0)
            {
                ProcessRequestBatch(requests);
            }
        }
        finally
        {
            _ = _processingLock.Release();
        }
    }

    /// <summary>Calculates the ratio of cache hits to total cache requests.</summary>
    /// <remarks>The cache hit ratio provides an indication of how effectively the cache is serving requests.
    /// A higher ratio suggests better cache performance. If the cache is empty or no requests have been made, the
    /// method returns 0.0.</remarks>
    /// <returns>
    /// A double value representing the cache hit ratio. Returns 0.0 if there are no cache entries or requests.
    /// </returns>
    private double CalculateCacheHitRatio()
    {
        if (_valueCache.IsEmpty)
        {
            return 0.0;
        }

        var totalHits = 0L;
        foreach (var cachedValue in _valueCache.Values)
        {
            totalHits += cachedValue.HitCount;
        }

        var totalRequests = _valueCache.Count + totalHits;

        return totalRequests > 0 ? (double)totalHits / totalRequests : 0.0;
    }
}
