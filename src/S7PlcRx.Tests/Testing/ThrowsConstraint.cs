// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.S7PlcRx.Tests.Testing;

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
    public void Apply(Func<Task> action, string? message)
    {
        try
        {
            action().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            AssertionHelpers.AssertTrue(
                _exceptionType.IsInstanceOfType(exception),
                message ?? AssertionHelpers.ExpectedExceptionMessage(_exceptionType, exception));
            return;
        }

        AssertionHelpers.AssertTrue(
            false,
            message ?? AssertionHelpers.ExpectedExceptionMessage(_exceptionType, null));
    }
}
