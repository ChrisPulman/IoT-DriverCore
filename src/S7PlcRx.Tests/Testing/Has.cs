// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.S7PlcRx.Tests.Testing;

/// <summary>Creates count constraints.</summary>
public static class Has
{
    /// <summary>Creates the count constraint.</summary>
    /// <param name="expected">Describes parameter expected for helper member 55.</param>
    /// <returns>The result.</returns>
    private static Constraint CreateConstraint(int expected) =>
        new(
            actual => AssertionHelpers.TryGetCount(actual, out var count) && count == expected,
            $"count {expected}");

    /// <summary>Creates count equality constraints.</summary>
    public static class Count
    {
        /// <summary>Gets a constraint for an exact count.</summary>
        /// <returns>The result.</returns>
        /// <param name="expected">Describes parameter expected for helper member 56.</param>
        public static IConstraint EqualTo(int expected) => CreateConstraint(expected);
    }

    /// <summary>Creates length equality constraints.</summary>
    public static class Length
    {
        /// <summary>Gets a constraint for an exact length.</summary>
        /// <returns>The result.</returns>
        /// <param name="expected">Describes parameter expected for helper member 57.</param>
        public static IConstraint EqualTo(int expected) => CreateConstraint(expected);
    }
}
