// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Ports;
using System.Net;
#if REACTIVE_SHIM
using IoT.Driver.Serial.Reactive;
#else
using IoT.Driver.Serial;
#endif
#if REACTIVE_SHIM
using IoT.Driver.ModbusRx.Reactive.Device;
#else
using IoT.Driver.ModbusRx.Device;
#endif

#if REACTIVE_SHIM
namespace IoT.Driver.ModbusRx.Reactive;
#else
namespace IoT.Driver.ModbusRx;
#endif
    /// <summary>Provides ModbusRx functionality.</summary>
    public static partial class Create
    {
        /// <summary>Message emitted when a connected master has unexpectedly become null.</summary>
        private const string ResetConnectedMasterIsNullMessage = "Reset connected Master is null";

        /// <summary>Message emitted when communication has been lost.</summary>
        private const string LostCommunicationMessage = "Lost Communication";

        /// <summary>Message emitted before creating a master.</summary>
        private const string CreateMasterMessage = "Create Master";

        /// <summary>Message emitted when a master faults.</summary>
        private const string MasterFaultMessage = "ModbusRx Master Fault";

        /// <summary>Message emitted when a slave faults.</summary>
        private const string SlaveFaultMessage = "ModbusRx Slave Fault";

        /// <summary>Gets or sets the ping interval.</summary>
        /// <value>The ping interval.</value>
        public static TimeSpan PingInterval { get; set; } = TimeSpan.FromSeconds(Ten);

        /// <summary>Gets or sets the check connection interval.</summary>
        /// <value>The check connection interval.</value>
        public static TimeSpan CheckConnectionInterval { get; set; } = TimeSpan.FromSeconds(Five);

        /// <summary>Create a TcpIpMaster with the specified host address.</summary>
        /// <param name="hostAddress">The host address.</param>
        /// <param name="port">The port.</param>
        /// <returns>
        /// The master and connection status.
        /// </returns>
        public static IObservable<(bool Connected, Exception? Error, ModbusIpMaster? Master)> TcpIpMaster(
            string hostAddress,
            int port) =>
            NetworkIpMaster(
                hostAddress,
                () => ModbusIpMaster.CreateIp(new TcpClientRx(hostAddress, port)));

        /// <summary>TCPs the ip slave.</summary>
        /// <param name="hostAddress">The host address.</param>
        /// <param name="port">The port.</param>
        /// <param name="unitId">The unit identifier.</param>
        /// <returns>An Observable of.</returns>
        /// <exception cref="ArgumentNullException">nameof(hostAddress).</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// nameof(port)
        /// or
        /// nameof(unitId).
        /// </exception>
        public static IObservable<ModbusTcpSlave> TcpIpSlave(string hostAddress, int port, byte unitId)
        {
            if (string.IsNullOrWhiteSpace(hostAddress))
            {
                throw new ArgumentOutOfRangeException(nameof(hostAddress));
            }

            if (port is < 0 or > UShortMaximum)
            {
                throw new ArgumentOutOfRangeException(nameof(port));
            }

            if (unitId is < 1 or > TwoHundredFortySeven)
            {
                throw new ArgumentOutOfRangeException(nameof(unitId));
            }

            var address = IPAddress.Parse(hostAddress);
            return ObserveSlave(() =>
                ModbusTcpSlave.CreateTcp(unitId, new(address, port)));
        }

        /// <summary>Create a UdpIpMaster with the specified host address.</summary>
        /// <param name="hostAddress">The host address.</param>
        /// <param name="port">The port.</param>
        /// <returns>
        /// The master and connection status.
        /// </returns>
        public static IObservable<(bool Connected, Exception? Error, ModbusIpMaster? Master)> UdpIpMaster(
            string hostAddress,
            int port) =>
            NetworkIpMaster(
                hostAddress,
                () => ModbusIpMaster.CreateIp(new UdpClientRx(hostAddress, port)));

        /// <summary>Creates an UdpIp slave.</summary>
        /// <param name="hostAddress">The host address.</param>
        /// <param name="port">The port.</param>
        /// <param name="unitId">The unit identifier.</param>
        /// <returns>An Observable of.</returns>
        /// <exception cref="ArgumentNullException">nameof(hostAddress).</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// nameof(port)
        /// or
        /// nameof(unitId).
        /// </exception>
        public static IObservable<ModbusUdpSlave> UdpIpSlave(string hostAddress, int port, byte unitId)
        {
            if (string.IsNullOrWhiteSpace(hostAddress))
            {
                throw new ArgumentOutOfRangeException(nameof(hostAddress));
            }

            if (port is < 0 or > UShortMaximum)
            {
                throw new ArgumentOutOfRangeException(nameof(port));
            }

            if (unitId is < 1 or > TwoHundredFortySeven)
            {
                throw new ArgumentOutOfRangeException(nameof(unitId));
            }

            var address = IPAddress.Parse(hostAddress);
            var endpoint = new IPEndPoint(address, port);
            return ObserveSlave(() =>
                ModbusUdpSlave.CreateUdp(unitId, new(endpoint)));
        }

        /// <summary>Create a SerialIpMaster with the specified ip address.</summary>
        /// <param name="port">The COM Port.</param>
        /// <param name="baudRate">The baud rate.</param>
        /// <returns>
        /// The master and connection status.
        /// </returns>
        public static IObservable<(bool Connected, Exception? Error, ModbusIpMaster? Master)> SerialIpMaster(
            string port,
            int baudRate) =>
            Observable.Create<(bool Connected, Exception? Error, ModbusIpMaster? Master)>(observer =>
            {
                var dis = new CompositeDisposable();
                var state = new MasterConnectionState<ModbusIpMaster>();
                dis.Add(
                    ObserveMasterConnection(
                        Observable.Interval(CheckConnectionInterval),
                        observer,
                        state));
                var comdis = new CompositeDisposable();
                dis.Add(ObserveSerialPortNames().SelectMany(x => Observable.FromAsync(async () =>
                {
                    try
                    {
                        if (comdis?.Count == 0 && ContainsPortName(x, port))
                        {
                            observer.OnNext(
                                (false, new ModbusCommunicationException(CreateMasterMessage), null));
                            var serialport = new SerialPortRx(port, baudRate);
                            state.Master = ModbusIpMaster.CreateIp(serialport);
                            comdis.Add(state.Master);
                            await serialport.OpenAsync();
                            state.Connected = true;
                            state.ConnectionMessageSent = false;
                            observer.OnNext((true, null, state.Master));
                        }
                        else
                        {
                            _ = dis.Remove(comdis!);
                            comdis?.Dispose();
                            state.Connected = false;
                            state.Master = null;
                            observer.OnNext((false, null, null));
                            comdis = [];
                            dis.Add(comdis);
                        }
                    }
                    catch (Exception ex)
                    {
                        _ = dis.Remove(comdis!);
                        comdis?.Dispose();
                        state.Connected = false;
                        state.Master = null;
                        observer.OnNext(
                            (false, new ModbusCommunicationException(MasterFaultMessage, ex), null));
                        comdis = [];
                        dis.Add(comdis);
                    }

                    return RxVoid.Default;
                })).Retry(int.MaxValue).Subscribe());

                return dis;
            }).Publish().RefCount();

        /// <summary>Creates an Serial Rtu Slave.</summary>
        /// <param name="port">The port.</param>
        /// <param name="unitId">The unit identifier.</param>
        /// <param name="baudRate">The baud rate.</param>
        /// <param name="dataBits">The data bits.</param>
        /// <param name="parity">The parity.</param>
        /// <param name="stopBits">The stop bits.</param>
        /// <param name="handshake">The handshake.</param>
        /// <returns>An observable of serial RTU slave instances.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="port"/> or <paramref name="unitId"/> is invalid.
        /// </exception>
        public static IObservable<ModbusSerialSlave> SerialRtuSlave(
            string port,
            byte unitId,
            int baudRate,
            int dataBits,
            Parity parity,
            StopBits stopBits,
            Handshake handshake)
        {
            ValidateSerialSlaveArguments(port, unitId);
            return SerialSlave(
                new(port, unitId, baudRate, dataBits, parity, stopBits, handshake),
                ModbusSerialSlave.CreateRtu);
        }

        /// <summary>Creates an Serial Ascii Slave.</summary>
        /// <param name="port">The port.</param>
        /// <param name="unitId">The unit identifier.</param>
        /// <param name="baudRate">The baud rate.</param>
        /// <param name="dataBits">The data bits.</param>
        /// <param name="parity">The parity.</param>
        /// <param name="stopBits">The stop bits.</param>
        /// <param name="handshake">The handshake.</param>
        /// <returns>An observable of serial ASCII slave instances.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="port"/> or <paramref name="unitId"/> is invalid.
        /// </exception>
        public static IObservable<ModbusSerialSlave> SerialAsciiSlave(
            string port,
            byte unitId,
            int baudRate,
            int dataBits,
            Parity parity,
            StopBits stopBits,
            Handshake handshake)
        {
            ValidateSerialSlaveArguments(port, unitId);
            return SerialSlave(
                new(port, unitId, baudRate, dataBits, parity, stopBits, handshake),
                ModbusSerialSlave.CreateAscii);
        }

        /// <summary>Starts a slave when subscribed and owns it for the subscription lifetime.</summary>
        /// <typeparam name="TSlave">The concrete slave type.</typeparam>
        /// <param name="factory">Creates the slave for one subscription.</param>
        /// <returns>A shared observable of the active slave.</returns>
        private static IObservable<TSlave> ObserveSlave<TSlave>(Func<TSlave> factory)
            where TSlave : ModbusSlave =>
            Observable.CreateWithState<TSlave, Func<TSlave>>(factory, static (factory, observer) =>
            {
                var slave = factory();
                var subscription = new SlaveSubscriptionState<TSlave>(slave, observer);
                observer.OnNext(slave);
                _ = RunSlaveAsync(subscription);
                return Disposable.Create(subscription.Dispose);
            }).Retry(int.MaxValue).Publish().RefCount();

        /// <summary>Forwards a slave listener failure unless its subscription has ended.</summary>
        /// <typeparam name="TSlave">The concrete slave type.</typeparam>
        /// <param name="subscription">Owns the active slave and subscription observer.</param>
        /// <returns>A task representing the listener lifetime.</returns>
        private static async Task RunSlaveAsync<TSlave>(
            SlaveSubscriptionState<TSlave> subscription)
            where TSlave : ModbusSlave
        {
            try
            {
                await subscription.Slave.ListenAsync().ConfigureAwait(false);
                if (!subscription.IsDisposed)
                {
                    subscription.Observer.OnCompleted();
                }
            }
            catch (Exception exception)
            {
                if (!subscription.IsDisposed)
                {
                    subscription.Observer.OnError(new ModbusCommunicationException(SlaveFaultMessage, exception));
                }
            }
        }

        /// <summary>Owns a slave for the lifetime of a subscription.</summary>
        /// <typeparam name="TSlave">The concrete slave type.</typeparam>
        /// <param name="slave">The active slave.</param>
        /// <param name="observer">The subscription observer.</param>
        private sealed class SlaveSubscriptionState<TSlave>(TSlave slave, IObserver<TSlave> observer) : IDisposable
            where TSlave : ModbusSlave
        {
            /// <summary>Tracks whether the subscription has ended.</summary>
            private int _disposed;

            /// <summary>Gets the active slave.</summary>
            internal TSlave Slave { get; } = slave;

            /// <summary>Gets the subscription observer.</summary>
            internal IObserver<TSlave> Observer { get; } = observer;

            /// <summary>Gets a value indicating whether the subscription has ended.</summary>
            internal bool IsDisposed => Volatile.Read(ref _disposed) != 0;

            /// <summary>Disposes the active slave once.</summary>
            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                {
                    return;
                }

                Slave.Dispose();
            }
        }
}
