// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.S7PlcRx.Enums;

namespace IoT.DriverCore.S7PlcRx.Tests.Exceptions;

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
    [Test]
    public void Ctor_WithErrorCode_ShouldSetErrorCodeAndMessage()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        var ex = new PlcException(ErrorCode.ReadData);
        Assert.That(ex.ErrorCode, Is.EqualTo(ErrorCode.ReadData));
        Assert.That(ex.Message, Does.Contain("PLC communication failed"));
    }

    /// <summary>Ensures the inner exception is propagated and its message becomes the exception message.</summary>
    [Test]
    public void Ctor_WithErrorCodeAndInnerException_ShouldPropagateInnerMessage()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        var inner = new InvalidOperationException("boom");
        var ex = new PlcException(ErrorCode.ReadData, inner);
        Assert.That(ex.ErrorCode, Is.EqualTo(ErrorCode.ReadData));
        Assert.That(ex.InnerException, Is.SameAs(inner));
        Assert.That(ex.Message, Is.EqualTo("boom"));
    }

    /// <summary>Ensures custom message and inner exception are set.</summary>
    [Test]
    public void Ctor_WithErrorCodeMessageAndInner_ShouldSetProperties()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        var inner = new InvalidOperationException("inner");
        var ex = new PlcException(ErrorCode.WriteData, "custom", inner);
        Assert.That(ex.ErrorCode, Is.EqualTo(ErrorCode.WriteData));
        Assert.That(ex.Message, Is.EqualTo("custom"));
        Assert.That(ex.InnerException, Is.SameAs(inner));
    }
}
