// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.Driver.S7PlcRx.Enums;

namespace IoT.Driver.S7PlcRx.Tests.Exceptions;

/// <summary>Tests for `PlcException`.</summary>
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public class PlcExceptionTests
{
    /// <summary>Gets the debugger display text.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay
    {
        get => ToString() ?? string.Empty;
    }

    /// <summary>Ensures error code and default message are set.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Ctor_WithErrorCode_ShouldSetErrorCodeAndMessage()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        var ex = new PlcException(ErrorCode.ReadData);
        await Assert.That(ex.ErrorCode, Is.EqualTo(ErrorCode.ReadData));
        await Assert.That(ex.Message, Does.Contain("PLC communication failed"));
    }

    /// <summary>Ensures the inner exception is propagated and its message becomes the exception message.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Ctor_WithErrorCodeAndInnerException_ShouldPropagateInnerMessage()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        var inner = new InvalidOperationException("boom");
        var ex = new PlcException(ErrorCode.ReadData, inner);
        await Assert.That(ex.ErrorCode, Is.EqualTo(ErrorCode.ReadData));
        await Assert.That(ex.InnerException, Is.SameAs(inner));
        await Assert.That(ex.Message, Is.EqualTo("boom"));
    }

    /// <summary>Ensures custom message and inner exception are set.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Ctor_WithErrorCodeMessageAndInner_ShouldSetProperties()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        var inner = new InvalidOperationException("inner");
        var ex = new PlcException(ErrorCode.WriteData, "custom", inner);
        await Assert.That(ex.ErrorCode, Is.EqualTo(ErrorCode.WriteData));
        await Assert.That(ex.Message, Is.EqualTo("custom"));
        await Assert.That(ex.InnerException, Is.SameAs(inner));
    }
}
