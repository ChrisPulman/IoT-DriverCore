// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using TUnit.Assertions.Extensions;
using TUnitAssert = TUnit.Assertions.Assert;

namespace IoT.Driver.S7PlcRx.Tests.Testing;

/// <summary>Provides the legacy assertion surface backed exclusively by TUnit assertions.</summary>
public static class Assert
{
    /// <summary>Applies a constraint to an actual value.</summary>
    /// <typeparam name="TActual">The type parameter.</typeparam>
    /// <param name="actual">Describes parameter actual for helper member 1.</param>
    /// <param name="constraint">Describes parameter constraint for helper member 2.</param>
    /// <returns>A task that completes when the constraint has been evaluated.</returns>
    public static Task That<TActual>(TActual actual, IConstraint constraint) =>
        That(actual, constraint, null);

    /// <summary>Applies a constraint to an actual value with a custom failure message.</summary>
    /// <typeparam name="TActual">The type parameter.</typeparam>
    /// <param name="actual">Describes parameter actual for helper member 3.</param>
    /// <param name="constraint">Describes parameter constraint for helper member 4.</param>
    /// <param name="message">Describes parameter message for helper member 5.</param>
    /// <returns>A task that completes when the constraint has been evaluated.</returns>
    public static Task That<TActual>(TActual actual, IConstraint constraint, string? message) =>
        constraint.Apply(actual, message);

    /// <summary>Applies an asynchronous exception constraint.</summary>
    /// <param name="action">Describes parameter action for helper member 6.</param>
    /// <param name="constraint">Describes parameter constraint for helper member 7.</param>
    /// <returns>A task that completes when the constraint has been evaluated.</returns>
    public static Task That(Func<Task> action, ThrowsConstraint constraint) =>
        That(action, constraint, null);

    /// <summary>Applies an asynchronous exception constraint with a custom failure message.</summary>
    /// <param name="action">Describes parameter action for helper member 8.</param>
    /// <param name="constraint">Describes parameter constraint for helper member 9.</param>
    /// <param name="message">Describes parameter message for helper member 10.</param>
    /// <returns>A task that completes when the constraint has been evaluated.</returns>
    public static Task That(Func<Task> action, ThrowsConstraint constraint, string? message) =>
        constraint.Apply(action, message);

    /// <summary>Executes a group of assertions.</summary>
    /// <param name="action">Describes parameter action for helper member 11.</param>
    /// <returns>A task that completes when every assertion in the group has been evaluated.</returns>
    public static async Task Multiple(Func<Task> action)
    {
        using (TUnitAssert.Multiple())
        {
            await action().ConfigureAwait(false);
        }
    }

    /// <summary>Verifies that an action throws the specified exception type.</summary>
    /// <typeparam name="TException">The type parameter.</typeparam>
    /// <param name="action">Describes parameter action for helper member 12.</param>
    /// <param name="typeMarker">Describes parameter typeMarker for helper member 13.</param>
    /// <returns>A task that resolves to the asserted exception when the TUnit assertion succeeds.</returns>
    public static async Task<TException> Throws<TException>(Action action, params TException[] typeMarker)
        where TException : Exception
    {
        _ = typeMarker;
        return await TUnitAssert.That(action).Throws<TException>()
            ?? throw new InvalidOperationException("TUnit did not return the asserted exception.");
    }

    /// <summary>Asynchronously verifies that an action throws the specified exception type.</summary>
    /// <typeparam name="TException">The type parameter.</typeparam>
    /// <param name="action">Describes parameter action for helper member 14.</param>
    /// <param name="typeMarker">Describes parameter typeMarker for helper member 15.</param>
    /// <returns>A task that resolves to the asserted exception when the TUnit assertion succeeds.</returns>
    public static async Task<TException> ThrowsAsync<TException>(Func<Task> action, params TException[] typeMarker)
        where TException : Exception
    {
        _ = typeMarker;
        return await TUnitAssert.That(action).Throws<TException>()
            ?? throw new InvalidOperationException("TUnit did not return the asserted exception.");
    }

    /// <summary>Verifies that an action does not throw an exception.</summary>
    /// <param name="action">Describes parameter action for helper member 16.</param>
    /// <returns>A task that completes when the TUnit assertion has been evaluated.</returns>
    public static Task DoesNotThrow(Action action) => DoesNotThrow(action, null);

    /// <summary>Verifies that an action does not throw an exception with a custom failure message.</summary>
    /// <param name="action">Describes parameter action for helper member 17.</param>
    /// <param name="message">Describes parameter message for helper member 18.</param>
    /// <returns>A task that completes when the TUnit assertion has been evaluated.</returns>
    public static async Task DoesNotThrow(Action action, string? message)
    {
        if (message is null)
        {
            _ = await TUnitAssert.That(action).ThrowsNothing();
            return;
        }

        _ = await TUnitAssert.That(action).ThrowsNothing().Because(message);
    }

    /// <summary>Verifies that an asynchronous action does not throw an exception.</summary>
    /// <param name="action">The asynchronous action to evaluate.</param>
    /// <returns>A task that completes when the TUnit assertion has been evaluated.</returns>
    public static Task DoesNotThrow(Func<Task> action) => DoesNotThrow(action, null);

    /// <summary>Verifies that an asynchronous action does not throw an exception with a custom failure message.</summary>
    /// <param name="action">The asynchronous action to evaluate.</param>
    /// <param name="message">The optional assertion failure message.</param>
    /// <returns>A task that completes when the TUnit assertion has been evaluated.</returns>
    public static async Task DoesNotThrow(Func<Task> action, string? message)
    {
        if (message is null)
        {
            _ = await TUnitAssert.That(action).ThrowsNothing();
            return;
        }

        _ = await TUnitAssert.That(action).ThrowsNothing().Because(message);
    }

    /// <summary>Marks an assertion as successful.</summary>
    /// <returns>A task that completes when the TUnit assertion has been evaluated.</returns>
    public static Task Pass() => Pass(null);

    /// <summary>Marks an assertion as successful with an optional message.</summary>
    /// <param name="message">Describes parameter message for helper member 19.</param>
    /// <returns>A task that completes when the TUnit assertion has been evaluated.</returns>
    public static Task Pass(string? message) => AssertionHelpers.AssertTrueAsync(true, message);

    /// <summary>Marks an assertion as failed.</summary>
    /// <returns>A task that completes when the TUnit assertion has been evaluated.</returns>
    public static Task Fail() => Fail(null);

    /// <summary>Marks an assertion as failed with an optional message.</summary>
    /// <param name="message">Describes parameter message for helper member 20.</param>
    /// <returns>A task that completes when the TUnit assertion has been evaluated.</returns>
    public static Task Fail(string? message) =>
        AssertionHelpers.AssertTrueAsync(false, message ?? "Assertion failed.");
}
