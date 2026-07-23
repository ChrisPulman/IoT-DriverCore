// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Ports;
#if REACTIVE_SHIM
using IoT.DriverCore.Serial.Reactive;
#else
using IoT.DriverCore.Serial;
#endif
#if REACTIVE_SHIM
using IoT.DriverCore.ModbusRx.Reactive.Device;
#else
using IoT.DriverCore.ModbusRx.Device;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive;
#else
namespace IoT.DriverCore.ModbusRx;
#endif
    /// <summary>Provides ModbusRx functionality.</summary>
    public static partial class Create
    {
        /// <summary>Create a reactive Modbus Serial RTU master that automatically manages connection state.</summary>
        /// <param name="port">The COM port (e.g., "COM1").</param>
        /// <param name="baudRate">The baud rate.</param>
        /// <param name="dataBits">The data bits.</param>
        /// <param name="parity">The parity.</param>
        /// <param name="stopBits">The stop bits.</param>
        /// <param name="handshake">The handshake.</param>
        /// <returns>An observable stream of connection status and the RTU master.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when port is null or whitespace.</exception>
        public static IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> SerialRtuMaster(
            string port,
            int baudRate,
            int dataBits,
            Parity parity,
            StopBits stopBits,
            Handshake handshake) =>
            Observable.Create<(bool Connected, Exception? Error, IModbusSerialMaster? Master)>(async observer =>
            {
                if (string.IsNullOrWhiteSpace(port))
                {
                    throw new ArgumentOutOfRangeException(nameof(port));
                }

                var dis = new CompositeDisposable();
                IModbusSerialMaster? master = null;
                var connected = false;
                var connectionMessageSent = false;

                // Connection watchdog
                dis.Add(Observable.Interval(CheckConnectionInterval)
                    .Subscribe(_ =>
                    {
                        if (connected && master is null)
                        {
                            observer.OnNext((
                                false,
                                new ModbusCommunicationException("Reset connected Master is null"),
                                null));
                            connected = false;
                        }

                        if (connected || connectionMessageSent)
                        {
                            return;
                        }

                        connectionMessageSent = true;
                        observer.OnNext((connected, new ModbusCommunicationException("Lost Communication"), master));
                    }));

                // Directly create master
                try
                {
                    observer.OnNext((false, new ModbusCommunicationException("Create Master"), null));
                    var serial = CreateSerialPort(port, baudRate, dataBits, parity, stopBits, handshake);
                    await serial.OpenAsync();
                    serial.ReadTimeout = TenThousand; // Set timeout to 10 seconds
                    master = ModbusSerialMaster.CreateRtu(serial);
                    dis.Add(master);
                    connected = true;
                    connectionMessageSent = false;
                    observer.OnNext((connected, null, master));
                }
                catch (Exception ex)
                {
                    connected = false;
                    master = null;
                    ModbusDiagnostics.Write($"SerialRtuMaster error: {ex.Message}");
                    observer.OnNext((
                        connected,
                        new ModbusCommunicationException("ModbusRx Serial RTU Master Fault", ex),
                        master));
                }

                return dis;
            }).Publish().RefCount();

        /// <summary>Create a reactive Modbus Serial ASCII master that automatically manages connection state.</summary>
        /// <param name="port">The COM port (e.g., "COM1").</param>
        /// <param name="baudRate">The baud rate.</param>
        /// <param name="dataBits">The data bits.</param>
        /// <param name="parity">The parity.</param>
        /// <param name="stopBits">The stop bits.</param>
        /// <param name="handshake">The handshake.</param>
        /// <returns>An observable stream of connection status and the ASCII master.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when port is null or whitespace.</exception>
        public static IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> SerialAsciiMaster(
            string port,
            int baudRate,
            int dataBits,
            Parity parity,
            StopBits stopBits,
            Handshake handshake) =>
            Observable.Create<(bool Connected, Exception? Error, IModbusSerialMaster? Master)>(async observer =>
            {
                if (string.IsNullOrWhiteSpace(port))
                {
                    throw new ArgumentOutOfRangeException(nameof(port));
                }

                var dis = new CompositeDisposable();
                IModbusSerialMaster? master = null;
                var connected = false;
                var connectionMessageSent = false;

                // Connection watchdog
                dis.Add(Observable.Interval(CheckConnectionInterval)
                    .Subscribe(_ =>
                    {
                        if (connected && master is null)
                        {
                            observer.OnNext((
                                false,
                                new ModbusCommunicationException("Reset connected Master is null"),
                                null));
                            connected = false;
                        }

                        if (connected || connectionMessageSent)
                        {
                            return;
                        }

                        connectionMessageSent = true;
                        observer.OnNext((connected, new ModbusCommunicationException("Lost Communication"), master));
                    }));

                // Directly create master
                try
                {
                    observer.OnNext((false, new ModbusCommunicationException("Create Master"), null));
                    var serialport = CreateSerialPort(port, baudRate, dataBits, parity, stopBits, handshake);
                    master = ModbusSerialMaster.CreateAscii(serialport);
                    dis.Add(master);
                    await serialport.OpenAsync();
                    connected = true;
                    connectionMessageSent = false;
                    observer.OnNext((connected, null, master));
                }
                catch (Exception ex)
                {
                    connected = false;
                    master = null;
                    observer.OnNext((
                        connected,
                        new ModbusCommunicationException("ModbusRx Serial ASCII Master Fault", ex),
                        master));
                }

                return dis;
            }).Publish().RefCount();
}
