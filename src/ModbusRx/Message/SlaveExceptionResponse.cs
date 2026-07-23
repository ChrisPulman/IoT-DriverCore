// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.Message;
#else
namespace IoT.DriverCore.ModbusRx.Message;
#endif

/// <summary>Provides SlaveExceptionResponse functionality.</summary>
/// <seealso cref="AbstractModbusMessage" />
/// <seealso cref="IModbusMessage" />
public class SlaveExceptionResponse : AbstractModbusMessage, IModbusMessage
{
    /// <summary>Initializes a new instance of the <see cref="SlaveExceptionResponse"/> class.</summary>
    public SlaveExceptionResponse()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SlaveExceptionResponse"/> class.</summary>
    /// <param name="slaveAddress">The slave address.</param>
    /// <param name="functionCode">The function code.</param>
    /// <param name="exceptionCode">The exception code.</param>
    public SlaveExceptionResponse(byte slaveAddress, byte functionCode, byte exceptionCode)
        : base(slaveAddress, functionCode) => SlaveExceptionCode = exceptionCode;

    /// <inheritdoc/>
    public override int MinimumFrameSize => Three;

    /// <summary>Gets or sets the slave exception code.</summary>
/// <value>The slave exception code.</value>
    public byte SlaveExceptionCode
    {
        get => MessageImpl.ExceptionCode!.Value;
        set => MessageImpl.ExceptionCode = value;
    }

    /// <summary>Gets the exception messages indexed by Modbus exception code.</summary>
    private static Dictionary<byte, string> ExceptionMessages { get; } = CreateExceptionMessages();

    /// <summary>Returns a string that represents the current object.</summary>
    /// <returns>
    /// A <see cref="T:System.String"></see> that represents the current <see cref="T:System.Object"></see>.
    /// </returns>
    public override string ToString()
    {
        var msg = ExceptionMessages.TryGetValue(SlaveExceptionCode, out var value)
            ? value
            : Resources.Unknown;

        return $"Function Code: {FunctionCode}{Environment.NewLine}Exception Code: {SlaveExceptionCode} - {msg}";
    }

    /// <summary>Executes the Create Exception Messages operation.</summary>
    /// <returns>The result.</returns>
    internal static Dictionary<byte, string> CreateExceptionMessages() =>
        new(Nine)
        {
            { 1, Resources.IllegalFunction },
            { Two, Resources.IllegalDataAddress },
            { Three, Resources.IllegalDataValue },
            { Four, Resources.SlaveDeviceFailure },
            { Five, Resources.Acknowlege },
            { Six, Resources.SlaveDeviceBusy },
            { Eight, Resources.MemoryParityError },
            { Ten, Resources.GatewayPathUnavailable },
            { Eleven, Resources.GatewayTargetDeviceFailedToRespond },
        };

    /// <inheritdoc/>
    protected override void InitializeUnique(byte[] frame)
    {
        if (frame is null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        if (FunctionCode <= Modbus.ExceptionOffset)
        {
            throw new FormatException(Resources.SlaveExceptionResponseInvalidFunctionCode);
        }

        SlaveExceptionCode = frame[2];
    }
}
