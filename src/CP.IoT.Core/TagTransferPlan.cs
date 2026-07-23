// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace IoT.DriverCore.Core;

/// <summary>Contains a deterministic set of coalesced transport ranges.</summary>
public sealed class TagTransferPlan
{
    /// <summary>Initializes a new instance of the <see cref="TagTransferPlan"/> class.</summary>
    /// <param name="inputCount">The number of source requests used to create the plan.</param>
    /// <param name="ranges">The coalesced ranges.</param>
    internal TagTransferPlan(int inputCount, IReadOnlyList<TagTransferRange> ranges)
    {
        InputCount = inputCount;
        Ranges = new ReadOnlyCollection<TagTransferRange>(ranges.ToArray());
    }

    /// <summary>Gets the number of source requests used to create this plan.</summary>
    public int InputCount { get; }

    /// <summary>Gets planned transfers in deterministic partition and numeric-offset order.</summary>
    public IReadOnlyList<TagTransferRange> Ranges { get; }

    /// <summary>Restores executor results to the exact order of the source planner input.</summary>
    /// <typeparam name="T">The executor result type.</typeparam>
    /// <param name="results">Exactly one result for each source request, in any order.</param>
    /// <returns>The result values ordered by their original source index.</returns>
    public IReadOnlyList<T> OrderResults<T>(IEnumerable<TagIndexedResult<T>> results)
    {
        if (results is null)
        {
            throw new ArgumentNullException(nameof(results));
        }

        var values = new T[InputCount];
        var received = new bool[InputCount];
        var count = 0;
        foreach (var result in results)
        {
            if (result is null)
            {
                throw new ArgumentException("Results cannot contain null entries.", nameof(results));
            }

            if ((uint)result.InputIndex >= (uint)InputCount)
            {
                throw new ArgumentOutOfRangeException(nameof(results), "A result index was not present in this plan.");
            }

            if (received[result.InputIndex])
            {
                throw new ArgumentException("Results must contain exactly one value for every input index.", nameof(results));
            }

            values[result.InputIndex] = result.Value;
            received[result.InputIndex] = true;
            count++;
        }

        if (count != InputCount)
        {
            throw new ArgumentException("Results must contain exactly one value for every input index.", nameof(results));
        }

        return new ReadOnlyCollection<T>(values);
    }
}
