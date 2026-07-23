// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.S7PlcRx.Tests.Testing;

/// <summary>Represents an equality constraint with optional time tolerance.</summary>
/// <param name="expected">Describes parameter expected for helper member 53.</param>
[System.Diagnostics.DebuggerDisplay("EqualConstraint")]
public sealed class EqualConstraint(object? expected) : IConstraint
{
    /// <summary>Stores the expected value.</summary>
    private readonly object? _expected = expected;

    /// <summary>Stores the optional time tolerance.</summary>
    private TimeSpan? _tolerance;

    /// <summary>Sets the time tolerance for the equality constraint.</summary>
    /// <param name="tolerance">Describes parameter tolerance for helper member 54.</param>
    /// <returns>The result.</returns>
    public EqualConstraint Within(TimeSpan tolerance)
    {
        _tolerance = tolerance;
        return this;
    }

    /// <inheritdoc />
    public void Apply(object? actual, string? message)
    {
        var isEqual = _tolerance is { } tolerance
            ? AssertionHelpers.AreEqualWithin(actual, _expected, tolerance)
            : AssertionHelpers.AreEqual(actual, _expected);
        AssertionHelpers.AssertTrue(
            isEqual,
            message ?? AssertionHelpers.ExpectedEqualityMessage(actual, _expected));
    }
}
