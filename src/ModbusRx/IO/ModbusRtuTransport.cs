// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
#if REACTIVE_SHIM
using IoT.DriverCore.ModbusRx.Reactive.Message;
#else
using IoT.DriverCore.ModbusRx.Message;
#endif
#if REACTIVE_SHIM
using IoT.DriverCore.ModbusRx.Reactive.Utility;
#else
using IoT.DriverCore.ModbusRx.Utility;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.IO;
#else
namespace IoT.DriverCore.ModbusRx.IO;
#endif

/// <summary>Refined Abstraction - http://en.wikipedia.org/wiki/Bridge_Pattern.</summary>
internal sealed class ModbusRtuTransport : ModbusSerialTransport
{
    /// <summary>Defines the Request Frame Start Length value.</summary>
    internal const int RequestFrameStartLength = 7;

    /// <summary>Defines the Response Frame Start Length value.</summary>
    internal const int ResponseFrameStartLength = 4;

    /// <summary>Defines the function-23 request header bytes that follow the common request prefix.</summary>
    private const int ReadWriteRequestHeaderRemainderLength = 4;

    /// <summary>Defines the CRC byte count appended to every RTU frame.</summary>
    private const int CrcLength = 2;

    /// <summary>Initializes a new instance of the Modbus Rtu Transport class.</summary>
    /// <param name="streamResource">The stream Resource value.</param>
    internal ModbusRtuTransport(IStreamResource streamResource)
        : base(streamResource) => Debug.Assert(streamResource is not null, "Argument streamResource cannot be null.");

    /// <summary>Executes the Request Bytes To Read operation.</summary>
    /// <param name="frameStart">The frame Start value.</param>
    /// <returns>The result.</returns>
    internal static int RequestBytesToRead(byte[] frameStart)
    {
        var functionCode = frameStart[1];
        try
        {
            return functionCode switch
            {
                Modbus.ReadCoils or
                Modbus.ReadInputs or
                Modbus.ReadHoldingRegisters or
                Modbus.ReadInputRegisters or
                Modbus.WriteSingleCoil or
                Modbus.WriteSingleRegister or
                Modbus.Diagnostics
                    => 1,

                Modbus.WriteMultipleCoils or
                Modbus.WriteMultipleRegisters
                    => frameStart[6] + Two,

                _ => throw new NotSupportedException($"Function code {functionCode} is not supported."),
            };
        }
        catch (NotSupportedException ex)
        {
            Debug.WriteLine(ex.Message);
            throw;
        }
    }

    /// <summary>Executes the Response Bytes To Read operation.</summary>
    /// <param name="frameStart">The frame Start value.</param>
    /// <returns>The result.</returns>
    internal static int ResponseBytesToRead(byte[] frameStart)
    {
        var functionCode = frameStart[1];

        // exception response
        if (functionCode > Modbus.ExceptionOffset)
        {
            return 1;
        }

        if (IsVariableLengthReadResponse(functionCode))
        {
            return frameStart[2] + 1;
        }

        try
        {
            return functionCode switch
            {
                Modbus.WriteSingleCoil or
                Modbus.WriteSingleRegister or
                Modbus.WriteMultipleCoils or
                Modbus.WriteMultipleRegisters or
                Modbus.Diagnostics
                    => Four,

                _ => throw new NotSupportedException($"Function code {functionCode} is not supported."),
            };
        }
        catch (NotSupportedException ex)
        {
            Debug.WriteLine(ex.Message);
            throw;
        }
    }

    /// <summary>Executes the Read operation.</summary>
    /// <param name="count">The count value.</param>
    /// <returns>The result.</returns>
    internal byte[] Read(int count)
    {
        var frameBytes = new byte[count];
        for (var i = 0; i < count; i++)
        {
            int br;
            try
            {
                br = StreamResource.ReadAsync(frameBytes, i, 1).GetAwaiter().GetResult();
            }
            catch (TimeoutException error)
            {
                throw new TimeoutException(
                    $"The RTU stream timed out while reading byte {i + 1} of {count}.",
                    error);
            }

            if (br != 1)
            {
                if (br == 0 && StreamResource.ReadTimeout > 0)
                {
                    throw new TimeoutException($"The stream timed out while reading byte at position {i}.");
                }

                throw new IOException($"Unable to read byte at position {i}");
            }
        }

        return frameBytes;
    }

    internal override byte[] BuildMessageFrame(IModbusMessage message)
    {
        var messageFrame = message.MessageFrame;
        var crc = ModbusUtility.CalculateCrc(messageFrame);
        using var messageBody = new MemoryStream(messageFrame.Length + crc.Length);

        messageBody.Write(messageFrame, 0, messageFrame.Length);
        messageBody.Write(crc, 0, crc.Length);

        return messageBody.ToArray();
    }

    internal override bool ChecksumsMatch(IModbusMessage message, byte[] messageFrame) =>
        BitConverter.ToUInt16(messageFrame, messageFrame.Length - Two) ==
            BitConverter.ToUInt16(ModbusUtility.CalculateCrc(message.MessageFrame), 0);

    internal override Task<IModbusMessage> ReadResponseAsync<T>(Func<T> responseFactory)
    {
        var frameStart = Read(ResponseFrameStartLength);
        var frameEnd = Read(ResponseBytesToRead(frameStart));
        var frame = CombineFrames(frameStart, frameEnd);
        ModbusDiagnostics.Write($"{TimeProvider.GetLocalNow():HH':'mm':'ss'.'fff} Master RX: {string.Join(", ", frame)}");

        return CreateResponseAsync(Task.FromResult(frame), responseFactory);
    }

    internal override Task<byte[]> ReadRequestAsync()
    {
        var frameStart = Read(RequestFrameStartLength);
        if (frameStart[1] == Modbus.ReadWriteMultipleRegisters)
        {
            var extendedHeader = Read(ReadWriteRequestHeaderRemainderLength);
            var payloadByteCount = extendedHeader[NumericConstants.Three];
            var readWriteFrameEnd = Read(payloadByteCount + CrcLength);
            var extendedFrameStart = CombineFrames(frameStart, extendedHeader);
            var readWriteFrame = CombineFrames(extendedFrameStart, readWriteFrameEnd);
            ModbusDiagnostics.Write($"Slave RX: {string.Join(", ", readWriteFrame)}");
            return Task.FromResult(readWriteFrame);
        }

        var frameEnd = Read(RequestBytesToRead(frameStart));
        var frame = CombineFrames(frameStart, frameEnd);
        ModbusDiagnostics.Write($"Slave RX: {string.Join(", ", frame)}");

        return Task.FromResult(frame);
    }

    /// <summary>Determines whether the response includes a byte-count-prefixed payload.</summary>
    /// <param name="functionCode">The Modbus function code.</param>
    /// <returns><c>true</c> when the response length is encoded in the frame; otherwise, <c>false</c>.</returns>
    private static bool IsVariableLengthReadResponse(byte functionCode) =>
        functionCode is
            Modbus.ReadCoils or
            Modbus.ReadInputs or
            Modbus.ReadHoldingRegisters or
            Modbus.ReadInputRegisters or
            Modbus.ReadWriteMultipleRegisters;

    /// <summary>Combines frame segments into a single message frame.</summary>
    /// <param name="frameStart">The first frame segment.</param>
    /// <param name="frameEnd">The final frame segment.</param>
    /// <returns>The combined frame.</returns>
    private static byte[] CombineFrames(byte[] frameStart, byte[] frameEnd)
    {
        var frame = new byte[frameStart.Length + frameEnd.Length];
        Array.Copy(frameStart, 0, frame, 0, frameStart.Length);
        Array.Copy(frameEnd, 0, frame, frameStart.Length, frameEnd.Length);
        return frame;
    }
}
