// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Ports;
#if REACTIVE_SHIM
using CP.IO.Ports.Reactive;
using ModbusRx.Reactive.Device;
#else
using CP.IO.Ports;
using ModbusRx.Device;
#endif

#if REACTIVE_SHIM
namespace ModbusRx.Reactive;
#else
namespace ModbusRx;
#endif

/// <summary>Provides ModbusRx functionality.</summary>
public static partial class Create
{
    /// <summary>Creates a serial slave from available-port notifications.</summary>
    /// <param name="settings">The serial slave settings.</param>
    /// <param name="slaveFactory">Creates the protocol-specific slave.</param>
    /// <returns>An observable sequence of serial slaves.</returns>
    private static IObservable<ModbusSerialSlave> SerialSlave(
        SerialSlaveSettings settings,
        Func<byte, SerialPortRx, ModbusSerialSlave> slaveFactory) =>
        Observable.Create<ModbusSerialSlave>(observer =>
        {
            var state = new SerialSlaveState();
            state.Resources.Add(state.PortResources);
            state.Resources.Add(
                SerialPortRx.PortNames()
                    .SelectMany(names => Observable.FromAsync(
                        () => UpdateSerialSlaveAsync(
                            names,
                            settings,
                            slaveFactory,
                            observer,
                            state)))
                    .Retry(int.MaxValue)
                    .Subscribe());
            return Disposable.Create(state.Dispose);
        }).Retry(int.MaxValue).Publish().RefCount();

    /// <summary>Creates or removes a slave in response to the current available-port set.</summary>
    /// <param name="portNames">The available port names.</param>
    /// <param name="settings">The serial slave settings.</param>
    /// <param name="slaveFactory">Creates the protocol-specific slave.</param>
    /// <param name="observer">The slave observer.</param>
    /// <param name="state">The current slave state.</param>
    /// <returns>A task that completes after processing the port set.</returns>
    private static async Task<RxVoid> UpdateSerialSlaveAsync(
        IEnumerable<string> portNames,
        SerialSlaveSettings settings,
        Func<byte, SerialPortRx, ModbusSerialSlave> slaveFactory,
        IObserver<ModbusSerialSlave> observer,
        SerialSlaveState state)
    {
        if (state.PortResources.Count != 0 || !ContainsPortName(portNames, settings.Port))
        {
            state.Reset();
            return RxVoid.Default;
        }

        try
        {
            var serialPort = CreateSerialPort(
                settings.Port,
                settings.BaudRate,
                settings.DataBits,
                settings.Parity,
                settings.StopBits,
                settings.Handshake);
            var slave = slaveFactory(settings.UnitId, serialPort);
            await serialPort.OpenAsync();
            state.ListenerTask = Task.Factory.StartNew(
                slave.ListenAsync,
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap();
            state.PortResources.Add(slave);
            state.PortResources.Add(
                Observable.FromAsync(async () =>
                {
                    await state.ListenerTask;
                    return RxVoid.Default;
                }).Subscribe(
                    _ => { },
                    ex => observer.OnError(new ModbusCommunicationException(SlaveFaultMessage, ex))));
            observer.OnNext(slave);
        }
        catch (Exception ex)
        {
            observer.OnError(new ModbusCommunicationException(SlaveFaultMessage, ex));
            state.Reset();
        }

        return RxVoid.Default;
    }

    /// <summary>Validates common serial-slave arguments.</summary>
    /// <param name="port">The COM port.</param>
    /// <param name="unitId">The Modbus unit identifier.</param>
    private static void ValidateSerialSlaveArguments(string port, byte unitId)
    {
        ValidateSerialPort(port);
        ValidateUnitId(unitId);
    }

    /// <summary>Validates a serial port name.</summary>
    /// <param name="port">The COM port.</param>
    private static void ValidateSerialPort(string port)
    {
        _ = string.IsNullOrWhiteSpace(port)
            ? throw new ArgumentOutOfRangeException(nameof(port))
            : port;
    }

    /// <summary>Validates a Modbus unit identifier.</summary>
    /// <param name="unitId">The Modbus unit identifier.</param>
    private static void ValidateUnitId(byte unitId)
    {
        _ = unitId is < 1 or > TwoHundredFortySeven
            ? throw new ArgumentOutOfRangeException(nameof(unitId))
            : unitId;
    }

    /// <summary>Stores immutable serial slave settings.</summary>
    /// <param name="port">The COM port.</param>
    /// <param name="unitId">The Modbus unit identifier.</param>
    /// <param name="baudRate">The baud rate.</param>
    /// <param name="dataBits">The data bits.</param>
    /// <param name="parity">The parity.</param>
    /// <param name="stopBits">The stop bits.</param>
    /// <param name="handshake">The handshake.</param>
    private sealed class SerialSlaveSettings(
        string port,
        byte unitId,
        int baudRate,
        int dataBits,
        Parity parity,
        StopBits stopBits,
        Handshake handshake)
    {
        /// <summary>Gets the COM port.</summary>
        public string Port { get; } = port;

        /// <summary>Gets the Modbus unit identifier.</summary>
        public byte UnitId { get; } = unitId;

        /// <summary>Gets the baud rate.</summary>
        public int BaudRate { get; } = baudRate;

        /// <summary>Gets the data bits.</summary>
        public int DataBits { get; } = dataBits;

        /// <summary>Gets the parity.</summary>
        public Parity Parity { get; } = parity;

        /// <summary>Gets the stop bits.</summary>
        public StopBits StopBits { get; } = stopBits;

        /// <summary>Gets the handshake.</summary>
        public Handshake Handshake { get; } = handshake;
    }

    /// <summary>Stores mutable state for a dynamically connected serial slave.</summary>
    private sealed class SerialSlaveState : IDisposable
    {
        /// <summary>Stores the active-port resources.</summary>
        private CompositeDisposable _portResources = [];

        /// <summary>Gets the shared resources.</summary>
        public CompositeDisposable Resources { get; } = [];

        /// <summary>Gets the active-port resources.</summary>
        public CompositeDisposable PortResources => _portResources;

        /// <summary>Gets or sets the active listener task.</summary>
        public Task? ListenerTask { get; set; }

        /// <summary>Disposes all serial slave resources.</summary>
        public void Dispose()
        {
            ListenerTask?.Dispose();
            _portResources.Dispose();
            Resources.Dispose();
        }

        /// <summary>Resets resources associated with the active serial slave.</summary>
        public void Reset()
        {
            _ = Resources.Remove(_portResources);
            _portResources.Dispose();
            ListenerTask?.Dispose();
            ListenerTask = null;
            _portResources = [];
            Resources.Add(_portResources);
        }
    }
}
