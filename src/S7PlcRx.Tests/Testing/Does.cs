// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.S7PlcRx.Tests.Testing;

/// <summary>Creates containment constraints.</summary>
public static class Does
{
    /// <summary>Gets a constraint for a contained value.</summary>
    /// <param name="expected">Describes parameter expected for helper member 52.</param>
    /// <returns>The result.</returns>
    public static IConstraint Contain(object? expected) =>
        new Constraint(actual => AssertionHelpers.Contains(actual, expected), "a contained value");
}
