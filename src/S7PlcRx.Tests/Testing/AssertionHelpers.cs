// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using System.Globalization;
using TUnit.Assertions.Extensions;
using TUnitAssert = TUnit.Assertions.Assert;

namespace S7PlcRx.Tests.Testing;

/// <summary>Provides value comparison and TUnit assertion helpers for the adapter.</summary>
internal static class AssertionHelpers
{
    /// <summary>Synchronously verifies an assertion through TUnit.</summary>
    /// <param name="condition">Describes parameter condition for helper member 21.</param>
    /// <param name="message">Describes parameter message for helper member 22.</param>
    internal static void AssertTrue(bool condition, string? message) =>
        AssertTrueAsync(condition, message).GetAwaiter().GetResult();

    /// <summary>Asynchronously verifies an assertion through TUnit.</summary>
    /// <param name="condition">Describes parameter condition for helper member 23.</param>
    /// <param name="message">Describes parameter message for helper member 24.</param>
    /// <returns>The result.</returns>
    internal static async Task AssertTrueAsync(bool condition, string? message)
    {
        _ = message;
        await TUnitAssert.That(condition).IsTrue();
    }

    /// <summary>Determines whether two values are equal.</summary>
    /// <param name="actual">Describes parameter actual for helper member 25.</param>
    /// <param name="expected">Describes parameter expected for helper member 26.</param>
    /// <returns>The result.</returns>
    internal static bool AreEqual(object? actual, object? expected)
    {
        if (ReferenceEquals(actual, expected))
        {
            return true;
        }

        if (actual is null || expected is null)
        {
            return false;
        }

        if (actual is string || expected is string)
        {
            return string.Equals(actual.ToString(), expected.ToString(), StringComparison.Ordinal);
        }

        if (IsNumeric(actual) && IsNumeric(expected))
        {
            return Convert.ToDecimal(actual, CultureInfo.InvariantCulture) ==
                Convert.ToDecimal(expected, CultureInfo.InvariantCulture);
        }

        return actual is IEnumerable actualEnumerable && expected is IEnumerable expectedEnumerable
            ? SequenceEqual(actualEnumerable, expectedEnumerable)
            : Equals(actual, expected);
    }

    /// <summary>Creates an equality assertion failure message.</summary>
    /// <param name="actual">Describes parameter actual for helper member 27.</param>
    /// <param name="expected">Describes parameter expected for helper member 28.</param>
    /// <returns>The result.</returns>
    internal static string ExpectedEqualityMessage(object? actual, object? expected) =>
        $"Expected {Format(actual)} to equal {Format(expected)}.";

    /// <summary>Creates an exception assertion failure message.</summary>
    /// <param name="exceptionType">Describes parameter exceptionType for helper member 29.</param>
    /// <param name="actual">Describes parameter actual for helper member 30.</param>
    /// <returns>The result.</returns>
    internal static string ExpectedExceptionMessage(Type exceptionType, Exception? actual)
    {
        var expectedName = exceptionType.FullName;
        return actual is null
            ? $"Expected exception assignable to {expectedName}, but no exception was thrown."
            : $"Expected exception assignable to {expectedName}, but got {actual.GetType().FullName}.";
    }

    /// <summary>Determines whether two time values are equal within a tolerance.</summary>
    /// <param name="actual">Describes parameter actual for helper member 31.</param>
    /// <param name="expected">Describes parameter expected for helper member 32.</param>
    /// <param name="tolerance">Describes parameter tolerance for helper member 33.</param>
    /// <returns>The result.</returns>
    internal static bool AreEqualWithin(object? actual, object? expected, TimeSpan tolerance) =>
        actual switch
        {
            DateTime actualDateTime when expected is DateTime expectedDateTime =>
                (actualDateTime - expectedDateTime).Duration() <= tolerance,
            DateTimeOffset actualOffset when expected is DateTimeOffset expectedOffset =>
                (actualOffset - expectedOffset).Duration() <= tolerance,
            TimeSpan actualTimeSpan when expected is TimeSpan expectedTimeSpan =>
                (actualTimeSpan - expectedTimeSpan).Duration() <= tolerance,
            _ => AreEqual(actual, expected),
        };

    /// <summary>Determines whether two enumerable values contain equivalent items.</summary>
    /// <param name="actual">Describes parameter actual for helper member 34.</param>
    /// <param name="expected">Describes parameter expected for helper member 35.</param>
    /// <returns>The result.</returns>
    internal static bool AreEquivalent(object? actual, object? expected)
    {
        if (actual is not IEnumerable actualEnumerable || actual is string ||
            expected is not IEnumerable expectedEnumerable || expected is string)
        {
            return AreEqual(actual, expected);
        }

        var remaining = expectedEnumerable.Cast<object?>().ToList();
        foreach (var item in actualEnumerable.Cast<object?>())
        {
            var index = remaining.FindIndex(candidate => AreEqual(item, candidate));
            if (index < 0)
            {
                return false;
            }

            remaining.RemoveAt(index);
        }

        return remaining.Count == 0;
    }

    /// <summary>Determines whether a value contains the expected item.</summary>
    /// <param name="actual">Describes parameter actual for helper member 36.</param>
    /// <param name="expected">Describes parameter expected for helper member 37.</param>
    /// <returns>The result.</returns>
    internal static bool Contains(object? actual, object? expected) => actual switch
    {
        string text => expected is not null &&
            text.Contains(expected.ToString() ?? string.Empty, StringComparison.Ordinal),
        IEnumerable enumerable => enumerable.Cast<object?>().Any(item => AreEqual(item, expected)),
        _ => false,
    };

    /// <summary>Determines whether a dictionary-like value contains a key.</summary>
    /// <param name="actual">Describes parameter actual for helper member 38.</param>
    /// <param name="key">Describes parameter key for helper member 39.</param>
    /// <returns>The result.</returns>
    internal static bool ContainsKey(object? actual, object? key)
    {
        if (actual is IDictionary dictionary)
        {
            return key is not null && dictionary.Contains(key);
        }

        var method = actual?.GetType().GetMethod(nameof(ContainsKey), [key?.GetType() ?? typeof(object)]);
        return method?.Invoke(actual, [key]) is true;
    }

    /// <summary>Determines whether a value is empty.</summary>
    /// <param name="actual">Describes parameter actual for helper member 40.</param>
    /// <returns>The result.</returns>
    internal static bool IsEmpty(object? actual) => actual switch
    {
        null => false,
        string text => text.Length == 0,
        _ => TryGetCount(actual, out var count)
            ? count == 0
            : actual is IEnumerable enumerable && !enumerable.Cast<object?>().Any(),
    };

    /// <summary>Attempts to obtain an item's count or length.</summary>
    /// <param name="actual">Describes parameter actual for helper member 41.</param>
    /// <param name="count">Describes parameter count for helper member 42.</param>
    /// <returns>The result.</returns>
    internal static bool TryGetCount(object? actual, out int count)
    {
        if (actual is ICollection collection)
        {
            count = collection.Count;
            return true;
        }

        if (actual is null)
        {
            count = 0;
            return false;
        }

        var property = actual.GetType().GetProperty("Count") ??
            actual.GetType().GetProperty("Length");
        if (property?.GetValue(actual) is int propertyCount)
        {
            count = propertyCount;
            return true;
        }

        count = 0;
        return false;
    }

    /// <summary>Compares two values.</summary>
    /// <param name="actual">Describes parameter actual for helper member 43.</param>
    /// <param name="expected">Describes parameter expected for helper member 44.</param>
    /// <returns>The result.</returns>
    internal static int Compare(object? actual, object expected)
    {
        if (actual is null)
        {
            return -1;
        }

        if (actual.GetType() == expected.GetType() && actual is IComparable comparable)
        {
            return comparable.CompareTo(expected);
        }

        var actualDecimal = Convert.ToDecimal(actual, CultureInfo.InvariantCulture);
        var expectedDecimal = Convert.ToDecimal(expected, CultureInfo.InvariantCulture);
        return actualDecimal.CompareTo(expectedDecimal);
    }

    /// <summary>Formats a value for an assertion message.</summary>
    /// <param name="value">Describes parameter value for helper member 45.</param>
    /// <returns>The result.</returns>
    internal static string Format(object? value) => value switch
    {
        null => "<null>",
        string text => $"\"{text}\"",
        IEnumerable enumerable when value is not string =>
            $"[{string.Join(", ", enumerable.Cast<object?>())}]",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    /// <summary>Determines whether a value has a numeric runtime type.</summary>
    /// <param name="value">Describes parameter value for helper member 46.</param>
    /// <returns>The result.</returns>
    private static bool IsNumeric(object value)
    {
        var typeCode = Type.GetTypeCode(value.GetType());
        return typeCode is >= TypeCode.SByte and <= TypeCode.Decimal;
    }

    /// <summary>Determines whether two enumerables contain equal items in the same order.</summary>
    /// <param name="actual">Describes parameter actual for helper member 47.</param>
    /// <param name="expected">Describes parameter expected for helper member 48.</param>
    /// <returns>The result.</returns>
    private static bool SequenceEqual(IEnumerable actual, IEnumerable expected)
    {
        using var actualEnumerator = actual.Cast<object?>().GetEnumerator();
        using var expectedEnumerator = expected.Cast<object?>().GetEnumerator();
        while (actualEnumerator.MoveNext())
        {
            if (!expectedEnumerator.MoveNext() ||
                !AreEqual(actualEnumerator.Current, expectedEnumerator.Current))
            {
                return false;
            }
        }

        return !expectedEnumerator.MoveNext();
    }
}
