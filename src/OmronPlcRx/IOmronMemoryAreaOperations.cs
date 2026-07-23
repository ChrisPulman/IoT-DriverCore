// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
#if REACTIVE_SHIM
using IoT.DriverCore.OmronPlcRx.Reactive.Enums;
#else
using IoT.DriverCore.OmronPlcRx.Enums;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive;
#else
namespace IoT.DriverCore.OmronPlcRx;
#endif

/// <summary>Abstracts native contiguous FINS memory-area operations.</summary>
internal interface IOmronMemoryAreaOperations
{
    /// <summary>Gets a stable FINS route identity used to prevent cross-route grouping.</summary>
    string RouteIdentity { get; }

    /// <summary>Gets the maximum number of words accepted by one FINS read.</summary>
    int MaximumReadWordCount { get; }

    /// <summary>Gets the maximum number of words accepted by one FINS write.</summary>
    int MaximumWriteWordCount { get; }

    /// <summary>Reads a contiguous word range.</summary>
    /// <param name="address">Starting word address.</param>
    /// <param name="length">Number of words.</param>
    /// <param name="dataType">FINS word memory area.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The read words.</returns>
    Task<short[]> ReadWordsAsync(
        ushort address,
        ushort length,
        MemoryWordDataType dataType,
        CancellationToken cancellationToken);

    /// <summary>Writes a contiguous word range.</summary>
    /// <param name="values">Words to write.</param>
    /// <param name="address">Starting word address.</param>
    /// <param name="dataType">FINS word memory area.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    Task WriteWordsAsync(
        short[] values,
        ushort address,
        MemoryWordDataType dataType,
        CancellationToken cancellationToken);

    /// <summary>Reads contiguous bits within one word.</summary>
    /// <param name="address">Containing word address.</param>
    /// <param name="bitIndex">Starting bit index.</param>
    /// <param name="length">Number of bits.</param>
    /// <param name="dataType">FINS bit memory area.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The read bits.</returns>
    Task<bool[]> ReadBitsAsync(
        ushort address,
        byte bitIndex,
        byte length,
        MemoryBitDataType dataType,
        CancellationToken cancellationToken);

    /// <summary>Writes contiguous bits within one word.</summary>
    /// <param name="values">Bits to write.</param>
    /// <param name="address">Containing word address.</param>
    /// <param name="bitIndex">Starting bit index.</param>
    /// <param name="dataType">FINS bit memory area.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    Task WriteBitsAsync(
        bool[] values,
        ushort address,
        byte bitIndex,
        MemoryBitDataType dataType,
        CancellationToken cancellationToken);
}
