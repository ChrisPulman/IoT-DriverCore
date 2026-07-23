// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
#if REACTIVE_SHIM
using IoT.DriverCore.OmronPlcRx.Reactive.Core;
using IoT.DriverCore.OmronPlcRx.Reactive.Enums;
#else
using IoT.DriverCore.OmronPlcRx.Core;
using IoT.DriverCore.OmronPlcRx.Enums;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive;
#else
namespace IoT.DriverCore.OmronPlcRx;
#endif

/// <summary>Provides native grouped FINS memory operations for logical tags.</summary>
public sealed partial class OmronPlcRx : IOmronLogicalBatchOperations
{
    /// <inheritdoc />
    async Task<IReadOnlyList<OmronLogicalBatchResult>> IOmronLogicalBatchOperations.ReadManyAsync(
        IReadOnlyList<OmronLogicalBatchItem> items,
        CancellationToken cancellationToken)
    {
        var executor = new OmronLogicalBatchExecutor(new ConnectionMemoryAreaOperations(_plc));
        var results = await executor.ReadManyAsync(items, cancellationToken).ConfigureAwait(false);
        UpdateBatchCache(items, results, false);
        return results;
    }

    /// <inheritdoc />
    async Task<IReadOnlyList<OmronLogicalBatchResult>> IOmronLogicalBatchOperations.WriteManyAsync(
        IReadOnlyList<OmronLogicalBatchItem> items,
        CancellationToken cancellationToken)
    {
        var executor = new OmronLogicalBatchExecutor(new ConnectionMemoryAreaOperations(_plc));
        var results = await executor.WriteManyAsync(items, cancellationToken).ConfigureAwait(false);
        UpdateBatchCache(items, results, true);
        return results;
    }

    /// <summary>Updates typed tag caches and observers from successful grouped results.</summary>
    /// <param name="items">Source indexed items.</param>
    /// <param name="results">Grouped operation results.</param>
    /// <param name="publishUnchanged">Whether successful writes publish unchanged values.</param>
    private void UpdateBatchCache(
        IReadOnlyList<OmronLogicalBatchItem> items,
        IReadOnlyList<OmronLogicalBatchResult> results,
        bool publishUnchanged)
    {
        var itemsByIndex = items.ToDictionary(static item => item.InputIndex);
        foreach (var result in results)
        {
            if (!result.Succeeded
                || !itemsByIndex.TryGetValue(result.InputIndex, out var item)
                || !_entries.TryGetValue(item.TagName, out var entry))
            {
                continue;
            }

            var changed = entry.UpdateValue(result.Value);
            if (!changed && !publishUnchanged)
            {
                continue;
            }

            if (_subjects.TryGetValue(item.TagName, out var subject))
            {
                subject.OnNext(result.Value);
            }

            _tagChanged.OnNext(entry.Tag);
        }
    }

    /// <summary>Adapts a concrete PLC connection to native memory-area operations.</summary>
    /// <param name="connection">Concrete FINS connection.</param>
    private sealed class ConnectionMemoryAreaOperations(OmronPLCConnection connection)
        : IOmronMemoryAreaOperations
    {
        /// <summary>Gets the concrete FINS connection.</summary>
        private readonly OmronPLCConnection _connection =
            connection ?? throw new ArgumentNullException(nameof(connection));

        /// <inheritdoc />
        public string RouteIdentity =>
            $"{_connection.LocalNodeID}:{_connection.RemoteNodeID}";

        /// <inheritdoc />
        public int MaximumReadWordCount => _connection.MaximumReadWordLength;

        /// <inheritdoc />
        public int MaximumWriteWordCount => _connection.MaximumWriteWordLength;

        /// <inheritdoc />
        public async Task<short[]> ReadWordsAsync(
            ushort address,
            ushort length,
            MemoryWordDataType dataType,
            CancellationToken cancellationToken)
        {
            var result = await _connection
                .ReadWordsAsync(address, length, dataType, cancellationToken)
                .ConfigureAwait(false);
            return result.Values;
        }

        /// <inheritdoc />
        public async Task WriteWordsAsync(
            short[] values,
            ushort address,
            MemoryWordDataType dataType,
            CancellationToken cancellationToken)
        {
            _ = await _connection
                .WriteWordsAsync(values, address, dataType, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool[]> ReadBitsAsync(
            ushort address,
            byte bitIndex,
            byte length,
            MemoryBitDataType dataType,
            CancellationToken cancellationToken)
        {
            var result = await _connection
                .ReadBitsAsync(address, bitIndex, length, dataType, cancellationToken)
                .ConfigureAwait(false);
            return result.Values;
        }

        /// <inheritdoc />
        public async Task WriteBitsAsync(
            bool[] values,
            ushort address,
            byte bitIndex,
            MemoryBitDataType dataType,
            CancellationToken cancellationToken)
        {
            _ = await _connection
                .WriteBitsAsync(values, address, bitIndex, dataType, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
