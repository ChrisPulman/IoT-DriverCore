// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Builds deterministic, capability-aware bulk transfer plans from parser-provided addresses.</summary>
public sealed class TagTransferPlanner
{
    /// <summary>Initializes a new instance of the <see cref="TagTransferPlanner"/> class.</summary>
    /// <param name="capabilities">The limits advertised by the protocol adapter.</param>
    public TagTransferPlanner(TagTransferCapabilities capabilities) =>
        Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));

    /// <summary>Gets the limits advertised by the protocol adapter.</summary>
    public TagTransferCapabilities Capabilities { get; }

    /// <summary>Plans compatible contiguous and overlapping addresses into the fewest valid ranges.</summary>
    /// <param name="requests">Parser-provided requests in caller-defined order.</param>
    /// <returns>Deterministic ranges suitable for protocol adapter execution.</returns>
    public TagTransferPlan Plan(IEnumerable<TagTransferRequest> requests)
    {
        if (requests is null)
        {
            throw new ArgumentNullException(nameof(requests));
        }

        var items = MaterializeItems(requests);
        var ordered = items
            .OrderBy(static item => item.Request.Address.TransportPartition, StringComparer.Ordinal)
            .ThenBy(static item => item.Request.Address.MemoryArea, StringComparer.Ordinal)
            .ThenBy(static item => item.Request.Address.Encoding, StringComparer.Ordinal)
            .ThenBy(static item => item.Request.Address.Access)
            .ThenBy(static item => item.Request.Address.Route, StringComparer.Ordinal)
            .ThenBy(static item => item.Request.Address.Offset)
            .ThenBy(static item => item.Request.Address.Length)
            .ThenBy(static item => item.InputIndex)
            .ToArray();
        return new TagTransferPlan(items.Length, BuildRanges(ordered));
    }

    /// <summary>Materializes and validates caller requests while retaining their input positions.</summary>
    /// <param name="requests">The caller requests.</param>
    /// <returns>The indexed requests.</returns>
    private TagTransferItem[] MaterializeItems(IEnumerable<TagTransferRequest> requests) =>
        requests.Select((request, inputIndex) =>
        {
            if (request is null)
            {
                throw new ArgumentException("Requests cannot contain null entries.", nameof(requests));
            }

            if (request.Address.Length > Capabilities.MaximumRangeLength)
            {
                throw new ArgumentException("A request is larger than the adapter maximum transfer length.", nameof(requests));
            }

            return new TagTransferItem(inputIndex, request);
        }).ToArray();

    /// <summary>Coalesces ordered items into capability-compatible transfer ranges.</summary>
    /// <param name="ordered">The deterministically ordered items.</param>
    /// <returns>The coalesced transfer ranges.</returns>
    private List<TagTransferRange> BuildRanges(TagTransferItem[] ordered)
    {
        var ranges = new List<TagTransferRange>();
        var cursor = 0;
        while (cursor < ordered.Length)
        {
            var first = ordered[cursor];
            var address = first.Request.Address;
            var offset = address.Offset;
            var endOffset = address.EndOffset;
            var members = new List<TagTransferItem> { first };
            cursor++;

            while (cursor < ordered.Length)
            {
                var candidate = ordered[cursor];
                var candidateAddress = candidate.Request.Address;
                if (TagTransportAddress.ComparePartition(address, candidateAddress) != 0
                    || candidateAddress.Offset > endOffset)
                {
                    break;
                }

                var candidateEnd = Math.Max(endOffset, candidateAddress.EndOffset);
                var candidateLength = checked(candidateEnd - offset);
                if (candidateLength > Capabilities.MaximumRangeLength
                    || members.Count == Capabilities.MaximumItemsPerRange)
                {
                    break;
                }

                endOffset = candidateEnd;
                members.Add(candidate);
                cursor++;
            }

            ranges.Add(new TagTransferRange(address, offset, checked(endOffset - offset), members));
        }

        return ranges;
    }
}
