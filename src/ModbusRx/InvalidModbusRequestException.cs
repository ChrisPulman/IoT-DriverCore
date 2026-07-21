// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ModbusRx.Reactive;
#else
namespace ModbusRx;
#endif

/// <summary>An exception that provides the exception code sent in response to an invalid Modbus request.</summary>
public class InvalidModbusRequestException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="InvalidModbusRequestException"/> class.</summary>
    public InvalidModbusRequestException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InvalidModbusRequestException"/> class.</summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public InvalidModbusRequestException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InvalidModbusRequestException"/> class.</summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public InvalidModbusRequestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InvalidModbusRequestException"/> class.</summary>
    /// <param name="exceptionCode">The Modbus exception code to provide to the slave.</param>
    public InvalidModbusRequestException(byte exceptionCode)
        : this(GetMessage(exceptionCode), exceptionCode)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InvalidModbusRequestException"/> class.</summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="exceptionCode">The Modbus exception code to provide to the slave.</param>
    public InvalidModbusRequestException(string message, byte exceptionCode)
        : this(message, exceptionCode, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InvalidModbusRequestException"/> class.</summary>
    /// <param name="exceptionCode">The Modbus exception code to provide to the slave.</param>
    /// <param name="innerException">
    /// The exception that caused the current exception. If <paramref name="innerException" /> is not null,
    /// a null reference, the current exception is raised in a catch block that handles the inner exception.
    /// </param>
    public InvalidModbusRequestException(byte exceptionCode, Exception innerException)
        : this(GetMessage(exceptionCode), exceptionCode, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InvalidModbusRequestException"/> class.</summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="exceptionCode">The Modbus exception code to provide to the slave.</param>
    /// <param name="innerException">
    /// The exception that caused the current exception. If <paramref name="innerException" /> is not null,
    /// a null reference, the current exception is raised in a catch block that handles the inner exception.
    /// </param>
    public InvalidModbusRequestException(string message, byte exceptionCode, Exception? innerException)
        : base(message, innerException) => ExceptionCode = exceptionCode;

    /// <summary>Gets the Modbus exception code to provide to the slave.</summary>
    public byte ExceptionCode { get; }

    /// <summary>Executes the Get Message operation.</summary>
    /// <param name="exceptionCode">The exception Code value.</param>
    /// <returns>The result.</returns>
    private static string GetMessage(byte exceptionCode) =>
        $"Modbus exception code {exceptionCode}.";
}
