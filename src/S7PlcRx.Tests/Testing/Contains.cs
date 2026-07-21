// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace S7PlcRx.Tests.Testing;

/// <summary>Creates dictionary key constraints.</summary>
public static class Contains
{
    /// <summary>Gets a constraint for a dictionary key.</summary>
    /// <param name="key">Describes parameter key for helper member 51.</param>
    /// <returns>The result.</returns>
    public static IConstraint Key(object? key) =>
        new Constraint(actual => AssertionHelpers.ContainsKey(actual, key), "a dictionary key");
}
