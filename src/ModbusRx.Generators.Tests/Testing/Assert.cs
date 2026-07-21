// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace ModbusRx.Generators.Tests.Testing;

/// <summary>Provides xUnit-compatible assertion helpers that throw on failure so TUnit can detect them.</summary>
internal static class Assert
{
    /// <summary>Asserts that <paramref name="haystack"/> contains the substring <paramref name="needle"/>.</summary>
    /// <param name="needle">The substring expected to be present.</param>
    /// <param name="haystack">The string to search.</param>
    internal static void Contains(string needle, string haystack)
    {
        if (haystack?.Contains(needle, StringComparison.Ordinal) == true)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Assert.{nameof(Contains)}() Failure: expected to find \"{needle}\" inside the string.");
    }

    /// <summary>Asserts that <paramref name="condition"/> is <c>true</c>.</summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="message">An optional failure message.</param>
    internal static void True(bool condition, string? message = null)
    {
        if (condition)
        {
            return;
        }

        throw new InvalidOperationException(message ?? $"Assert.{nameof(True)}() Failure: condition is false");
    }
}
