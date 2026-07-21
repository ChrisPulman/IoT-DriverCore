// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ModbusRx.UnitTests.Testing;

/// <summary>Provides xUnit-compatible assertion helpers that throw on failure so TUnit can detect them.</summary>
internal static class Assert
{
    /// <summary>Asserts that <paramref name="expected"/> equals <paramref name="actual"/>; enumerables are sequence-compared.</summary>
    /// <typeparam name="T">The type of both values.</typeparam>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    internal static void Equal<T>(T expected, T actual)
    {
        if (expected is not string && expected is IEnumerable expEnum && actual is IEnumerable actEnum)
        {
            CheckSequenceEqual(expEnum, actEnum, nameof(Equal));
        }
        else if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"Assert.{nameof(Equal)}() Failure\nExpected: {Format(expected)}\nActual:   {Format(actual)}");
        }
    }

    /// <summary>Asserts that <paramref name="expected"/> sequence-equals <paramref name="actual"/>; strings are ordinal-compared.</summary>
    /// <param name="expected">The expected collection.</param>
    /// <param name="actual">The actual collection.</param>
    internal static void Equal(IEnumerable expected, IEnumerable actual)
    {
        if (expected is not string expStr || actual is not string actStr)
        {
            CheckSequenceEqual(expected, actual, nameof(Equal));
        }
        else if (!string.Equals(expStr, actStr, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Assert.{nameof(Equal)}() Failure\nExpected: {expStr}\nActual:   {actStr}");
        }
    }

    /// <summary>Asserts that <paramref name="unexpected"/> does not equal <paramref name="actual"/>.</summary>
    /// <typeparam name="T">The type of both values.</typeparam>
    /// <param name="unexpected">The value that should not be present.</param>
    /// <param name="actual">The actual value.</param>
    internal static void NotEqual<T>(T unexpected, T actual)
    {
        if (unexpected is not string && unexpected is IEnumerable unexpEnum && actual is IEnumerable actEnum)
        {
            CheckSequencesNotEqual(unexpEnum, actEnum);
        }
        else if (EqualityComparer<T>.Default.Equals(unexpected, actual))
        {
            throw new InvalidOperationException(
                $"Assert.{nameof(NotEqual)}() Failure: values are equal ({Format(actual)})");
        }
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

    /// <summary>Asserts that <paramref name="condition"/> is <c>false</c>.</summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="message">An optional failure message.</param>
    internal static void False(bool condition, string? message = null)
    {
        if (!condition)
        {
            return;
        }

        throw new InvalidOperationException(message ?? $"Assert.{nameof(False)}() Failure: condition is true");
    }

    /// <summary>Asserts that <paramref name="value"/> is not <c>null</c> and returns it.</summary>
    /// <typeparam name="T">The reference type being tested.</typeparam>
    /// <param name="value">The value to test.</param>
    /// <returns>The non-null value.</returns>
    internal static T NotNull<T>(T? value)
        where T : class =>
        value ?? throw new InvalidOperationException($"Assert.{nameof(NotNull)}() Failure: value is null");

    /// <summary>Asserts that <paramref name="value"/> is <c>null</c>.</summary>
    /// <typeparam name="T">The reference type being tested.</typeparam>
    /// <param name="value">The value to test.</param>
    internal static void Null<T>(T? value)
        where T : class
    {
        if (value is null)
        {
            return;
        }

        throw new InvalidOperationException($"Assert.{nameof(Null)}() Failure: value is {Format(value)}");
    }

    /// <summary>Asserts that <paramref name="obj"/> is of type <typeparamref name="T"/> and returns the cast result.</summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="obj">The object to test.</param>
    /// <returns>The object cast to <typeparamref name="T"/>.</returns>
    internal static T IsType<T>(object? obj) =>
        obj is T typed
            ? typed
            : throw new InvalidOperationException(
                $"Assert.{nameof(IsType)}<{nameof(T)}>() Failure: object is {obj?.GetType().Name ?? "null"}");

    /// <summary>Asserts that <paramref name="collection"/> contains exactly one element and returns it.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="collection">The collection to test.</param>
    /// <returns>The single element.</returns>
    internal static T Single<T>(IEnumerable<T> collection)
    {
        var list = collection?.ToList() ?? throw new ArgumentNullException(nameof(collection));
        return list.Count == 1
            ? list[0]
            : throw new InvalidOperationException(
                $"Assert.{nameof(Single)}() Failure: expected 1 element, got {list.Count}");
    }

    /// <summary>Asserts that <paramref name="collection"/> is empty.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="collection">The collection to test.</param>
    internal static void Empty<T>(IEnumerable<T>? collection)
    {
        if (collection?.Any() != true)
        {
            return;
        }

        throw new InvalidOperationException($"Assert.{nameof(Empty)}() Failure: collection is not empty");
    }

    /// <summary>Asserts that <paramref name="collection"/> has at least one element.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="collection">The collection to test.</param>
    internal static void NotEmpty<T>(IEnumerable<T>? collection)
    {
        if (collection?.Any() == true)
        {
            return;
        }

        throw new InvalidOperationException($"Assert.{nameof(NotEmpty)}() Failure: collection is empty");
    }

    /// <summary>Asserts that <paramref name="collection"/> does not contain <paramref name="item"/>.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="item">The item that must not be present.</param>
    /// <param name="collection">The collection to test.</param>
    internal static void DoesNotContain<T>(T item, IEnumerable<T>? collection)
    {
        if (collection?.Contains(item) != true)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Assert.{nameof(DoesNotContain)}() Failure: collection contains {Format(item)}");
    }

    /// <summary>Asserts that <paramref name="expected"/> and <paramref name="actual"/> are the same reference.</summary>
    /// <param name="expected">The expected object reference.</param>
    /// <param name="actual">The actual object reference.</param>
    internal static void Same(object? expected, object? actual)
    {
        if (ReferenceEquals(expected, actual))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Assert.{nameof(Same)}() Failure: objects are not the same reference");
    }

    /// <summary>Asserts that <paramref name="action"/> throws <typeparamref name="T"/> and returns the caught exception.</summary>
    /// <typeparam name="T">The expected exception type.</typeparam>
    /// <param name="action">The action that should throw.</param>
    /// <returns>The caught exception.</returns>
    internal static T Throws<T>(Action action)
        where T : Exception
    {
        try
        {
            action();
        }
        catch (T ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Assert.{nameof(Throws)}<{nameof(T)}>() Failure: caught {ex.GetType().Name} instead",
                ex);
        }

        throw new InvalidOperationException(
            $"Assert.{nameof(Throws)}<{nameof(T)}>() Failure: no exception was thrown");
    }

    /// <summary>Asserts that <paramref name="action"/> throws an exception of the runtime <paramref name="exceptionType"/> and returns it.</summary>
    /// <param name="exceptionType">The expected exception type checked at runtime.</param>
    /// <param name="action">The action that should throw.</param>
    /// <returns>The caught exception.</returns>
    internal static Exception Throws(Type exceptionType, Action action)
    {
        if (exceptionType is null)
        {
            throw new ArgumentNullException(nameof(exceptionType));
        }

        try
        {
            action();
        }
        catch (Exception ex) when (exceptionType.IsInstanceOfType(ex))
        {
            return ex;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Assert.{nameof(Throws)}({exceptionType.Name}) Failure: caught {ex.GetType().Name} instead",
                ex);
        }

        throw new InvalidOperationException(
            $"Assert.{nameof(Throws)}({exceptionType.Name}) Failure: no exception was thrown");
    }

    /// <summary>Asserts that <paramref name="action"/> asynchronously throws <typeparamref name="T"/> and returns the caught exception.</summary>
    /// <typeparam name="T">The expected exception type.</typeparam>
    /// <param name="action">The async action that should throw.</param>
    /// <returns>A task that resolves to the caught exception.</returns>
    internal static async Task<T> ThrowsAsync<T>(Func<Task> action)
        where T : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (T ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Assert.{nameof(ThrowsAsync)}<{nameof(T)}>() Failure: caught {ex.GetType().Name} instead",
                ex);
        }

        throw new InvalidOperationException(
            $"Assert.{nameof(ThrowsAsync)}<{nameof(T)}>() Failure: no exception was thrown");
    }

    /// <summary>Verifies that two sequences differ; throws if they contain the same elements in the same order.</summary>
    /// <param name="unexpEnum">The unexpected sequence.</param>
    /// <param name="actEnum">The actual sequence.</param>
    private static void CheckSequencesNotEqual(IEnumerable unexpEnum, IEnumerable actEnum)
    {
        var unexpList = unexpEnum.Cast<object?>().ToList();
        var actList = actEnum.Cast<object?>().ToList();
        _ = !unexpList.SequenceEqual(actList, EqualityComparer<object?>.Default)
            ? default(object?)
            : throw new InvalidOperationException(
                $"Assert.{nameof(NotEqual)}() Failure: sequences are equal.");
    }

    /// <summary>Verifies two sequences are equal element-by-element; throws with a diff message when they differ.</summary>
    /// <param name="expEnum">The expected sequence.</param>
    /// <param name="actEnum">The actual sequence.</param>
    /// <param name="methodName">The calling assertion method name used in the failure message.</param>
    private static void CheckSequenceEqual(IEnumerable expEnum, IEnumerable actEnum, string methodName)
    {
        var expList = expEnum.Cast<object?>().ToList();
        var actList = actEnum.Cast<object?>().ToList();
        _ = expList.SequenceEqual(actList, EqualityComparer<object?>.Default)
            ? default(object?)
            : throw new InvalidOperationException(
                $"Assert.{methodName}() Failure (sequence)\nExpected: [{string.Join(", ", expList)}]\nActual:   [{string.Join(", ", actList)}]");
    }

    /// <summary>Returns a human-readable display string for <paramref name="value"/>.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to format.</param>
    /// <returns>A string representation of <paramref name="value"/>, or <c>"(null)"</c>.</returns>
    private static string Format<T>(T value) =>
        value is null ? "(null)" : value.ToString() ?? "(null)";
}
