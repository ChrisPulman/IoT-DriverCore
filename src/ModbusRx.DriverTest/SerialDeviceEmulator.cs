// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using IoT.DriverCore.Serial;

namespace IoT.DriverCore.ModbusRx.DriverTest;

/// <summary>Tests the SerialDeviceEmulator behavior.</summary>
public class SerialDeviceEmulator : IDisposable
{
    /// <summary>The serial port baud rate used by the emulator.</summary>
    private const int DefaultBaudRate = 9600;

    /// <summary>The serial port data bit count used by the emulator.</summary>
    private const int DefaultDataBits = 8;

    /// <summary>The minimum valid Modbus RTU frame length.</summary>
    private const int MinimumFrameLength = 8;

    /// <summary>The CRC field length.</summary>
    private const int CrcLength = 2;

    /// <summary>The invariant timestamp format used by diagnostic output.</summary>
    private const string TimestampFormat = "HH':'mm':'ss.fff";

    /// <summary>The emulated serial port.</summary>
    private readonly SerialPortRx _port;

    /// <summary>The simulated temperature controller.</summary>
    private readonly DummyTemperatureController _controller;

    /// <summary>The Modbus request handler.</summary>
    private readonly ModbusRtuHandler _handler;

    /// <summary>The clock used by diagnostic output.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>A value indicating whether the emulator has been disposed.</summary>
    private bool _disposedValue;

    /// <summary>Initializes a new instance of the <see cref="SerialDeviceEmulator"/> class.</summary>
    /// <param name="portName">Name of the port.</param>
    /// <param name="slaveId">The slave identifier.</param>
    public SerialDeviceEmulator(string portName, byte slaveId)
        : this(portName, slaveId, TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SerialDeviceEmulator"/> class.</summary>
    /// <param name="portName">Name of the port.</param>
    /// <param name="slaveId">The slave identifier.</param>
    /// <param name="timeProvider">The clock used for diagnostic output.</param>
    public SerialDeviceEmulator(string portName, byte slaveId, TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _controller = new();
        _handler = new(_controller, slaveId);

        _port = new(portName, DefaultBaudRate, DefaultDataBits, Parity.None, StopBits.One);
        _ = _port.OpenAsync();
        _ = _port.IsOpenObservable.Where(x => x).Subscribe(isOpen =>
        {
            var timestamp = _timeProvider.GetLocalNow().ToString(TimestampFormat, CultureInfo.InvariantCulture);
            Debug.WriteLine($"{timestamp} Serial port {portName} opened.");
            _controller.Update(); // Update once to set initial data
            _ = Task.Run(ReceiveLoopAsync);
        });
    }

    /// <summary>Performs resource cleanup.</summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases unmanaged and optionally managed resources.</summary>
    /// <param name="disposing">
    /// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue)
        {
            return;
        }

        if (disposing)
        {
            _port.Dispose();
        }

        _disposedValue = true;
    }

    /// <summary>Receives and handles Modbus RTU frames from the serial port.</summary>
    /// <returns>A task that completes when the receive loop exits.</returns>
    private Task ReceiveLoopAsync()
    {
        var buffer = new List<byte>();
        try
        {
            while (true)
            {
                var b = (byte)_port.ReadByte();
                buffer.Add(b);
                if (buffer.Count >= MinimumFrameLength)
                {
                    var frame = buffer.ToArray();
                    var crcReceived = (ushort)(frame[^CrcLength] | (frame[^1] << 8));
                    var crcCalc = ModbusCrc.Compute(frame, frame.Length - CrcLength);
                    if (crcReceived == crcCalc)
                    {
                        var receivedTimestamp = _timeProvider.GetLocalNow().ToString(TimestampFormat, CultureInfo.InvariantCulture);
                        Debug.WriteLine($"{receivedTimestamp} Emulator RX: {BitConverter.ToString(frame)}");
                        var response = _handler.HandleRequest(frame, frame.Length);
                        if (response is not null)
                        {
                            var transmittedTimestamp = _timeProvider.GetLocalNow().ToString(
                                TimestampFormat,
                                CultureInfo.InvariantCulture);
                            var responseText = BitConverter.ToString(response);
                            Debug.WriteLine($"{transmittedTimestamp} Emulator TX: {responseText}");
                            _port.Write(response, 0, response.Length);
                        }

                        buffer.Clear();
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Port disposed, exit gracefully
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ReceiveLoopAsync error: {ex.Message}");
        }

        return Task.CompletedTask;
    }
}
