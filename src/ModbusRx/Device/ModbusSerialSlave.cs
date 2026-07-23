// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
#if REACTIVE_SHIM
using IoT.DriverCore.Serial.Reactive;
#else
using IoT.DriverCore.Serial;
#endif
#if REACTIVE_SHIM
using IoT.DriverCore.ModbusRx.Reactive.IO;
#else
using IoT.DriverCore.ModbusRx.IO;
#endif
#if REACTIVE_SHIM
using IoT.DriverCore.ModbusRx.Reactive.Message;
#else
using IoT.DriverCore.ModbusRx.Message;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.Device;
#else
namespace IoT.DriverCore.ModbusRx.Device;
#endif

/// <summary>Modbus serial slave device.</summary>
public sealed class ModbusSerialSlave : ModbusSlave
{
    /// <summary>Initializes a new instance of the Modbus Serial Slave class.</summary>
    /// <param name="unitId">The unit Id value.</param>
    /// <param name="transport">The transport value.</param>
    private ModbusSerialSlave(byte unitId, ModbusTransport transport)
        : base(unitId, transport)
    {
    }

    /// <summary>Gets the Serial Transport value.</summary>
    private ModbusSerialTransport? SerialTransport
    {
        get
        {
            if (Transport is not ModbusSerialTransport transport)
            {
                throw new ObjectDisposedException(nameof(SerialTransport));
            }

            return transport;
        }
    }

    /// <summary>Modbus ASCII slave factory method.</summary>
    /// <param name="unitId">The unit identifier.</param>
    /// <param name="serialPort">The serial port.</param>
    /// <returns>A ModbusSerialSlave.</returns>
    /// <exception cref="System.ArgumentNullException">serialPort.</exception>
    public static ModbusSerialSlave CreateAscii(byte unitId, SerialPortRx serialPort)
    {
        if (serialPort is null)
        {
            throw new ArgumentNullException(nameof(serialPort));
        }

        return CreateAscii(unitId, new SerialPortAdapter(serialPort));
    }

    /// <summary>Modbus ASCII slave factory method.</summary>
    /// <param name="unitId">The unit identifier.</param>
    /// <param name="streamResource">The stream resource.</param>
    /// <returns>A ModbusSerialSlave.</returns>
    /// <exception cref="System.ArgumentNullException">streamResource.</exception>
    public static ModbusSerialSlave CreateAscii(byte unitId, IStreamResource streamResource)
    {
        if (streamResource is null)
        {
            throw new ArgumentNullException(nameof(streamResource));
        }

        return new ModbusSerialSlave(unitId, new ModbusAsciiTransport(streamResource));
    }

    /// <summary>Modbus RTU slave factory method.</summary>
    /// <param name="unitId">The unit identifier.</param>
    /// <param name="serialPort">The serial port.</param>
    /// <returns>A ModbusSerialSlave.</returns>
    /// <exception cref="System.ArgumentNullException">serialPort.</exception>
    public static ModbusSerialSlave CreateRtu(byte unitId, SerialPortRx serialPort)
    {
        if (serialPort is null)
        {
            throw new ArgumentNullException(nameof(serialPort));
        }

        return CreateRtu(unitId, new SerialPortAdapter(serialPort));
    }

    /// <summary>Modbus RTU slave factory method.</summary>
    /// <param name="unitId">The unit identifier.</param>
    /// <param name="streamResource">The stream resource.</param>
    /// <returns>A ModbusSerialSlave.</returns>
    /// <exception cref="System.ArgumentNullException">streamResource.</exception>
    public static ModbusSerialSlave CreateRtu(byte unitId, IStreamResource streamResource)
    {
        if (streamResource is null)
        {
            throw new ArgumentNullException(nameof(streamResource));
        }

        return new ModbusSerialSlave(unitId, new ModbusRtuTransport(streamResource));
    }

    /// <summary>Start slave listening for requests.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public override async Task ListenAsync()
    {
        while (true)
        {
            try
            {
                // read request and build message
                var transport = SerialTransport
                    ?? throw new InvalidOperationException("The serial transport is not initialized.");
                var frame = await transport.ReadRequestAsync().ConfigureAwait(false);
                var request = ModbusMessageFactory.CreateModbusRequest(frame);

                if (transport.CheckFrame && !transport.ChecksumsMatch(request, frame))
                {
                    var msg = $"Checksums failed to match {string.Join(", ", request.MessageFrame)} " +
                              $"!= {string.Join(", ", frame)}.";
                    Debug.WriteLine(msg);
                    throw new IOException(msg);
                }

                // only service requests addressed to this particular slave
                if (request.SlaveAddress != UnitId)
                {
                    Debug.WriteLine(
                        $"NModbus Slave {UnitId} ignoring request intended for NModbus Slave " +
                        $"{request.SlaveAddress}");
                    continue;
                }

                // perform action
                var response = ApplyRequest(request);

                // write response
                transport.Write(response);
            }
            catch (ObjectDisposedException)
            {
                // Disposing the serial resource is the terminal lifecycle signal.
                break;
            }
            catch (InvalidOperationException) when (IsDisposed)
            {
                // ModbusDevice disposal clears the transport; complete the loop normally.
                break;
            }
            catch (Exception exception) when (IsRecoverableRequestException(exception))
            {
                Debug.WriteLine(
                    $"Recoverable serial request exception encountered while listening - {exception.Message}");
                if (!TryDiscardInBuffer())
                {
                    break;
                }
            }
        }
    }

    /// <summary>Determines whether a request failure permits the slave to process the next frame.</summary>
    /// <param name="exception">The exception raised while decoding or handling a request.</param>
    /// <returns><c>true</c> when the pending serial input can safely be discarded and processing continued.</returns>
    private static bool IsRecoverableRequestException(Exception exception) =>
        exception is IOException or TimeoutException or FormatException or ArgumentException or
        NotSupportedException or NotImplementedException;

    /// <summary>Discards a malformed request while tolerating concurrent disposal.</summary>
    /// <returns><c>true</c> when the receive buffer was discarded.</returns>
    private bool TryDiscardInBuffer()
    {
        try
        {
            SerialTransport?.DiscardInBuffer();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}
