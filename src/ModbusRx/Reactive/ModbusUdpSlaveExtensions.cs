// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using IoT.DriverCore.ModbusRx.Reactive.Data;
#else
using IoT.DriverCore.ModbusRx.Data;
#endif
#if REACTIVE_SHIM
using IoT.DriverCore.ModbusRx.Reactive.Device;
#else
using IoT.DriverCore.ModbusRx.Device;
#endif
#if REACTIVE_SHIM
using IoT.DriverCore.ModbusRx.Reactive.Message;
#else
using IoT.DriverCore.ModbusRx.Message;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive;
#else
namespace IoT.DriverCore.ModbusRx;
#endif
    /// <summary>Writes observable values to UDP slave data stores.</summary>
    public sealed class ModbusUdpSlaveExtensions
    {
        /// <summary>Stores the Modbus unit identifier.</summary>
        private readonly byte _unitIdentifier;

        /// <summary>Initializes a new instance of the <see cref="ModbusUdpSlaveExtensions"/> class.</summary>
        public ModbusUdpSlaveExtensions()
            : this(1)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ModbusUdpSlaveExtensions"/> class.</summary>
        /// <param name="unitIdentifier">The Modbus unit identifier used by write requests.</param>
        public ModbusUdpSlaveExtensions(byte unitIdentifier)
        {
            _unitIdentifier = unitIdentifier;
        }

        /// <summary>Writes the Input Registers.</summary>
        /// <param name="slave">The extension receiver.</param>
        /// <param name="startAddress">The start address.</param>
        /// <param name="valuesToWrite">The values to write.</param>
        /// <returns>Observable ModbusUdpSlave.</returns>
        public IObservable<ModbusUdpSlave> WriteInputRegisters(
            IObservable<ModbusUdpSlave> slave,
            ushort startAddress,
            IObservable<ushort[]> valuesToWrite)
        {
            _ = slave.CombineLatest(
                valuesToWrite,
                (currentSlave, data) => (currentSlave, data)).Subscribe(source =>
                    ModbusSlave.WriteMultipleRegisters(
                        new WriteMultipleRegistersRequest(
                            _unitIdentifier,
                            startAddress,
                            new RegisterCollection(source.data)),
                        source.currentSlave.DataStore,
                        source.currentSlave.DataStore.InputRegisters));
            return slave;
        }

        /// <summary>Writes the holding registers.</summary>
        /// <param name="slave">The extension receiver.</param>
        /// <param name="startAddress">The start address.</param>
        /// <param name="valuesToWrite">The values to write.</param>
        /// <returns>Observable ModbusUdpSlave.</returns>
        public IObservable<ModbusUdpSlave> WriteHoldingRegisters(
            IObservable<ModbusUdpSlave> slave,
            ushort startAddress,
            IObservable<ushort[]> valuesToWrite)
        {
            _ = slave.CombineLatest(
                valuesToWrite,
                (currentSlave, data) => (currentSlave, data)).Subscribe(source =>
                    ModbusSlave.WriteMultipleRegisters(
                        new WriteMultipleRegistersRequest(
                            _unitIdentifier,
                            startAddress,
                            new RegisterCollection(source.data)),
                        source.currentSlave.DataStore,
                        source.currentSlave.DataStore.HoldingRegisters));
            return slave;
        }

        /// <summary>Writes the coil discretes.</summary>
        /// <param name="slave">The extension receiver.</param>
        /// <param name="startAddress">The start address.</param>
        /// <param name="valuesToWrite">The values to write.</param>
        /// <returns>Observable ModbusUdpSlave.</returns>
        public IObservable<ModbusUdpSlave> WriteCoilDiscretes(
            IObservable<ModbusUdpSlave> slave,
            ushort startAddress,
            IObservable<bool[]> valuesToWrite)
        {
            _ = slave.CombineLatest(
                valuesToWrite,
                (currentSlave, data) => (currentSlave, data)).Subscribe(source =>
                    ModbusSlave.WriteMultipleCoils(
                        new WriteMultipleCoilsRequest(
                            _unitIdentifier,
                            startAddress,
                            new DiscreteCollection(source.data)),
                        source.currentSlave.DataStore,
                        source.currentSlave.DataStore.CoilDiscretes));
            return slave;
        }

        /// <summary>Writes the input discretes.</summary>
        /// <param name="slave">The extension receiver.</param>
        /// <param name="startAddress">The start address.</param>
        /// <param name="valuesToWrite">The values to write.</param>
        /// <returns>Observable ModbusUdpSlave.</returns>
        public IObservable<ModbusUdpSlave> WriteInputDiscretes(
            IObservable<ModbusUdpSlave> slave,
            ushort startAddress,
            IObservable<bool[]> valuesToWrite)
        {
            _ = slave.CombineLatest(
                valuesToWrite,
                (currentSlave, data) => (currentSlave, data)).Subscribe(source =>
                    ModbusSlave.WriteMultipleCoils(
                        new WriteMultipleCoilsRequest(
                            _unitIdentifier,
                            startAddress,
                            new DiscreteCollection(source.data)),
                        source.currentSlave.DataStore,
                        source.currentSlave.DataStore.InputDiscretes));
            return slave;
        }
}
