// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using IoT.DriverCore.TwinCATRx.Core;

namespace IoT.DriverCore.TwinCATRx.Tests.Core;

/// <summary>Tests for custom exception constructors.</summary>
public class ExceptionTests
{
    /// <summary>The expected message for wrapped exception cases.</summary>
    private const string WrappedMessage = "wrapped";

    /// <summary>Verifies the SimpleTypeException constructors.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task SimpleTypeException_Constructors_Set_Exception_StateAsync()
    {
        var defaultException = new SimpleTypeException();
        var messageException = new SimpleTypeException("simple");
        var inner = new InvalidOperationException("inner");
        var wrappedException = new SimpleTypeException(WrappedMessage, inner);

        await TUnitAssert.That(defaultException).IsNotNull();
        await TUnitAssert.That(messageException.Message).IsEqualTo("simple");
        await TUnitAssert.That(wrappedException.Message).IsEqualTo(WrappedMessage);
        await TUnitAssert.That(wrappedException.InnerException).IsSameReferenceAs(inner);
    }

    /// <summary>Verifies the UnsuportedTypeException constructors.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task UnsuportedTypeException_Constructors_Set_Exception_StateAsync()
    {
        var defaultException = new UnsuportedTypeException();
        var messageException = new UnsuportedTypeException("unsupported");
        var inner = new InvalidOperationException("inner");
        var wrappedException = new UnsuportedTypeException(WrappedMessage, inner);

        await TUnitAssert.That(defaultException).IsNotNull();
        await TUnitAssert.That(messageException.Message).IsEqualTo("unsupported");
        await TUnitAssert.That(wrappedException.Message).IsEqualTo(WrappedMessage);
        await TUnitAssert.That(wrappedException.InnerException).IsSameReferenceAs(inner);
    }
}
