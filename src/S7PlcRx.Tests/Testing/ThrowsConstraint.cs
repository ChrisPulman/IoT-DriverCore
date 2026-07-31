// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.Driver.S7PlcRx.Tests.Testing;

/// <summary>Represents an asynchronous exception constraint.</summary>
/// <param name="exceptionType">Describes parameter exceptionType for helper member 77.</param>
[System.Diagnostics.DebuggerDisplay("ThrowsConstraint")]
public sealed class ThrowsConstraint(Type exceptionType)
{
    /// <summary>Stores the expected exception type.</summary>
    private readonly Type _exceptionType = exceptionType;

    /// <summary>Applies the constraint to an asynchronous action.</summary>
    /// <param name="action">Describes parameter action for helper member 78.</param>
    /// <param name="message">Describes parameter message for helper member 79.</param>
    /// <returns>A task that completes when the TUnit assertion has been evaluated.</returns>
    public async Task Apply(Func<Task> action, string? message)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await AssertionHelpers.AssertTrueAsync(
                _exceptionType.IsInstanceOfType(exception),
                message ?? AssertionHelpers.ExpectedExceptionMessage(_exceptionType, exception)).ConfigureAwait(false);
            return;
        }

        await AssertionHelpers.AssertTrueAsync(
            false,
            message ?? AssertionHelpers.ExpectedExceptionMessage(_exceptionType, null)).ConfigureAwait(false);
    }
}
