// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace S7PlcRx.Tests.Testing;

/// <summary>Provides the NUnit-compatible assertion surface backed by TUnit assertions.</summary>
public static class Assert
{
    /// <summary>Applies a constraint to an actual value.</summary>
    /// <typeparam name="TActual">The type parameter.</typeparam>
    /// <param name="actual">Describes parameter actual for helper member 1.</param>
    /// <param name="constraint">Describes parameter constraint for helper member 2.</param>
    public static void That<TActual>(TActual actual, IConstraint constraint) => That(actual, constraint, null);

    /// <summary>Applies a constraint to an actual value with a custom failure message.</summary>
    /// <typeparam name="TActual">The type parameter.</typeparam>
    /// <param name="actual">Describes parameter actual for helper member 3.</param>
    /// <param name="constraint">Describes parameter constraint for helper member 4.</param>
    /// <param name="message">Describes parameter message for helper member 5.</param>
    public static void That<TActual>(TActual actual, IConstraint constraint, string? message) =>
        constraint.Apply(actual, message);

    /// <summary>Applies an asynchronous exception constraint.</summary>
    /// <param name="action">Describes parameter action for helper member 6.</param>
    /// <param name="constraint">Describes parameter constraint for helper member 7.</param>
    public static void That(Func<Task> action, ThrowsConstraint constraint) => That(action, constraint, null);

    /// <summary>Applies an asynchronous exception constraint with a custom failure message.</summary>
    /// <param name="action">Describes parameter action for helper member 8.</param>
    /// <param name="constraint">Describes parameter constraint for helper member 9.</param>
    /// <param name="message">Describes parameter message for helper member 10.</param>
    public static void That(Func<Task> action, ThrowsConstraint constraint, string? message) =>
        constraint.Apply(action, message);

    /// <summary>Executes a group of assertions.</summary>
    /// <param name="action">Describes parameter action for helper member 11.</param>
    public static void Multiple(Action action) => action();

    /// <summary>Verifies that an action throws the specified exception type.</summary>
    /// <typeparam name="TException">The type parameter.</typeparam>
    /// <param name="action">Describes parameter action for helper member 12.</param>
    /// <param name="typeMarker">Describes parameter typeMarker for helper member 13.</param>
    /// <returns>The result.</returns>
    public static TException Throws<TException>(Action action, params TException[] typeMarker)
        where TException : Exception
    {
        _ = typeMarker;
        try
        {
            action();
        }
        catch (Exception exception)
        {
            AssertionHelpers.AssertTrue(
                exception is TException,
                AssertionHelpers.ExpectedExceptionMessage(typeof(TException), exception));
            return (TException)exception;
        }

        AssertionHelpers.AssertTrue(
            false,
            AssertionHelpers.ExpectedExceptionMessage(typeof(TException), null));
        throw new InvalidOperationException("Unreachable assertion path.");
    }

    /// <summary>Asynchronously verifies that an action throws the specified exception type.</summary>
    /// <typeparam name="TException">The type parameter.</typeparam>
    /// <param name="action">Describes parameter action for helper member 14.</param>
    /// <param name="typeMarker">Describes parameter typeMarker for helper member 15.</param>
    /// <returns>The result.</returns>
    public static async Task<TException> ThrowsAsync<TException>(Func<Task> action, params TException[] typeMarker)
        where TException : Exception
    {
        _ = typeMarker;
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await AssertionHelpers.AssertTrueAsync(
                exception is TException,
                AssertionHelpers.ExpectedExceptionMessage(typeof(TException), exception)).ConfigureAwait(false);
            return (TException)exception;
        }

        await AssertionHelpers.AssertTrueAsync(
            false,
            AssertionHelpers.ExpectedExceptionMessage(typeof(TException), null)).ConfigureAwait(false);
        throw new InvalidOperationException("Unreachable assertion path.");
    }

    /// <summary>Verifies that an action does not throw an exception.</summary>
    /// <param name="action">Describes parameter action for helper member 16.</param>
    public static void DoesNotThrow(Action action) => DoesNotThrow(action, null);

    /// <summary>Verifies that an action does not throw an exception with a custom failure message.</summary>
    /// <param name="action">Describes parameter action for helper member 17.</param>
    /// <param name="message">Describes parameter message for helper member 18.</param>
    public static void DoesNotThrow(Action action, string? message)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            AssertionHelpers.AssertTrue(
                false,
                message ?? $"Expected no exception, but got {exception.GetType().FullName}: {exception.Message}");
        }
    }

    /// <summary>Marks an assertion as successful.</summary>
    public static void Pass() => Pass(null);

    /// <summary>Marks an assertion as successful with an optional message.</summary>
    /// <param name="message">Describes parameter message for helper member 19.</param>
    public static void Pass(string? message) => AssertionHelpers.AssertTrue(true, message);

    /// <summary>Marks an assertion as failed.</summary>
    public static void Fail() => Fail(null);

    /// <summary>Marks an assertion as failed with an optional message.</summary>
    /// <param name="message">Describes parameter message for helper member 20.</param>
    public static void Fail(string? message) =>
        AssertionHelpers.AssertTrue(false, message ?? "Assertion failed.");
}
