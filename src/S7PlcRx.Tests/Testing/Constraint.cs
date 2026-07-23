// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.S7PlcRx.Tests.Testing;

/// <summary>Represents a predicate-based value constraint.</summary>
/// <param name="predicate">Describes parameter predicate for helper member 49.</param>
/// <param name="expectation">Describes parameter expectation for helper member 50.</param>
[System.Diagnostics.DebuggerDisplay("Constraint")]
public sealed class Constraint(Func<object?, bool> predicate, string expectation) : IConstraint
{
    /// <summary>Stores the predicate that evaluates the actual value.</summary>
    private readonly Func<object?, bool> _predicate = predicate;

    /// <summary>Stores the expected condition for an assertion message.</summary>
    private readonly string _expectation = expectation;

    /// <inheritdoc />
    public void Apply(object? actual, string? message) =>
        AssertionHelpers.AssertTrue(
            _predicate(actual),
            message ?? $"Expected {AssertionHelpers.Format(actual)} to be {_expectation}.");
}
