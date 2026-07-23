// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.LogicalTags;
#else
namespace IoT.DriverCore.ModbusRx.LogicalTags;
#endif

/// <summary>Plans overlap-safe Modbus logical writes and native contiguous ranges.</summary>
internal static class ModbusLogicalWritePlanner
{
    /// <summary>The protocol maximum number of coils per write.</summary>
    private const uint MaximumCoilWriteCount = 1968U;

    /// <summary>The protocol maximum number of registers per write.</summary>
    private const uint MaximumRegisterWriteCount = 123U;

    /// <summary>
    /// Assigns overlapping writes to later execution waves so duplicate and overlapping input values
    /// retain their caller-defined order.
    /// </summary>
    /// <param name="requests">The requests for one unit and data area.</param>
    /// <returns>The scheduled requests.</returns>
    internal static List<ScheduledRequest> Schedule(IEnumerable<Request> requests)
    {
        var scheduled = new List<ScheduledRequest>();
        foreach (var request in requests.OrderBy(static request => request.Index))
        {
            var wave = 0;
            foreach (var previous in scheduled)
            {
                if (RangesOverlap(request, previous.Request))
                {
                    wave = Math.Max(wave, previous.Wave + 1);
                }
            }

            scheduled.Add(new ScheduledRequest(request, wave));
        }

        return scheduled;
    }

    /// <summary>Gets one maximum contiguous native write range.</summary>
    /// <param name="requests">The requests ordered by address.</param>
    /// <param name="startIndex">The first request index.</param>
    /// <returns>The exclusive request and address range ends.</returns>
    internal static (int End, uint RangeEnd) FindRangeEnd(
        IReadOnlyList<Request> requests,
        int startIndex)
    {
        var first = requests[startIndex].Tag;
        var maximum = first.DataArea == ModbusDataArea.Coil
            ? MaximumCoilWriteCount
            : MaximumRegisterWriteCount;
        var endIndex = startIndex + 1;
        var rangeEnd = (uint)first.Address + first.Count;
        while (endIndex < requests.Count)
        {
            var candidate = requests[endIndex].Tag;
            var candidateEnd = (uint)candidate.Address + candidate.Count;
            if (candidate.Address != rangeEnd || candidateEnd - first.Address > maximum)
            {
                break;
            }

            rangeEnd = candidateEnd;
            endIndex++;
        }

        return (endIndex, rangeEnd);
    }

    /// <summary>Determines whether two encoded write ranges overlap.</summary>
    /// <param name="left">The first request.</param>
    /// <param name="right">The second request.</param>
    /// <returns>True when the requests address at least one common point.</returns>
    private static bool RangesOverlap(Request left, Request right)
    {
        var leftEnd = (uint)left.Tag.Address + left.Tag.Count;
        var rightEnd = (uint)right.Tag.Address + right.Tag.Count;
        return left.Tag.Address < rightEnd && right.Tag.Address < leftEnd;
    }

    /// <summary>Associates an encoded write with its requested result index.</summary>
    /// <param name="index">The requested result index.</param>
    /// <param name="tag">The resolved definition.</param>
    /// <param name="requested">The requested logical value.</param>
    /// <param name="data">The encoded raw points.</param>
    internal sealed class Request(
        int index,
        ModbusLogicalTag tag,
        LogicalTagValue requested,
        Array data)
    {
        /// <summary>Gets the requested result index.</summary>
        internal int Index { get; } = index;

        /// <summary>Gets the resolved definition.</summary>
        internal ModbusLogicalTag Tag { get; } = tag;

        /// <summary>Gets the requested logical value.</summary>
        internal LogicalTagValue Requested { get; } = requested;

        /// <summary>Gets the encoded raw points.</summary>
        internal Array Data { get; } = data;
    }

    /// <summary>Associates an encoded write with its overlap-safe execution wave.</summary>
    /// <param name="request">The encoded write.</param>
    /// <param name="wave">The zero-based execution wave.</param>
    internal sealed class ScheduledRequest(Request request, int wave)
    {
        /// <summary>Gets the encoded write.</summary>
        internal Request Request { get; } = request;

        /// <summary>Gets the zero-based execution wave.</summary>
        internal int Wave { get; } = wave;
    }
}
