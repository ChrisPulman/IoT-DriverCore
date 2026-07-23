// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;

namespace IoT.DriverCore.ModbusRx.UnitTests;

/// <summary>Tests the InvalidModbusRequestExceptionFixture behavior.</summary>
public class InvalidModbusRequestExceptionFixture
{
    /// <summary>A reusable exception message.</summary>
    private const string HelloWorld = "Hello World";

    /// <summary>Constructors the with exception code.</summary>
    [TUnit.Core.Test]
    public void ConstructorWithExceptionCode()
    {
        var e = new InvalidModbusRequestException(Modbus.SlaveDeviceBusy);
        Assert.Equal($"Modbus exception code {Modbus.SlaveDeviceBusy}.", e.Message);
        Assert.Equal(Modbus.SlaveDeviceBusy, e.ExceptionCode);
        Assert.Null(e.InnerException);
    }

    /// <summary>Constructors the with exception code and inner exception.</summary>
    [TUnit.Core.Test]
    public void ConstructorWithExceptionCodeAndInnerException()
    {
        var inner = new IOException("Bar");
        var e = new InvalidModbusRequestException(Num.Value42, inner);
        Assert.Equal("Modbus exception code 42.", e.Message);
        Assert.Equal(Num.Value42, e.ExceptionCode);
        Assert.Same(inner, e.InnerException);
    }

    /// <summary>Constructors the with message and exception code.</summary>
    [TUnit.Core.Test]
    public void ConstructorWithMessageAndExceptionCode()
    {
        var e = new InvalidModbusRequestException(HelloWorld, Modbus.IllegalFunction);
        Assert.Equal(HelloWorld, e.Message);
        Assert.Equal(Modbus.IllegalFunction, e.ExceptionCode);
        Assert.Null(e.InnerException);
    }

    /// <summary>Constructors the with custom message and slave exception response.</summary>
    [TUnit.Core.Test]
    public void ConstructorWithCustomMessageAndSlaveExceptionResponse()
    {
        var inner = new IOException("Bar");
        var e = new InvalidModbusRequestException(HelloWorld, Modbus.IllegalDataAddress, inner);
        Assert.Equal(HelloWorld, e.Message);
        Assert.Equal(Modbus.IllegalDataAddress, e.ExceptionCode);
        Assert.Same(inner, e.InnerException);
    }
}
