// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Associates an executor result with its original planner input index.</summary>
/// <typeparam name="T">The executor result type.</typeparam>
public sealed class TagIndexedResult<T>
{
    /// <summary>Initializes a new instance of the <see cref="TagIndexedResult{T}"/> class.</summary>
    /// <param name="inputIndex">The original planner input index.</param>
    /// <param name="value">The result value.</param>
    public TagIndexedResult(int inputIndex, T value)
    {
        InputIndex = inputIndex;
        Value = value;
    }

    /// <summary>Gets the original planner input index.</summary>
    public int InputIndex { get; }

    /// <summary>Gets the result value.</summary>
    public T Value { get; }
}
