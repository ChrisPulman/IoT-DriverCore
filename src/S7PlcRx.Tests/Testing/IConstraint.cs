// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace S7PlcRx.Tests.Testing;

/// <summary>Defines a constraint that can be applied to an actual value.</summary>
public interface IConstraint
{
    /// <summary>Applies the constraint to an actual value.</summary>
    /// <param name="actual">Describes parameter actual for helper member 58.</param>
    /// <param name="message">Describes parameter message for helper member 59.</param>
    void Apply(object? actual, string? message);
}
