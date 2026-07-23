// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using IoT.DriverCore.ModbusRx.Data;
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.Serial;
using NativeAssert = TUnit.Assertions.Assert;

namespace IoT.DriverCore.ModbusRx.UnitTests;

/// <summary>Deterministic connection-state coverage using loopback and deliberately absent serial ports.</summary>
[TUnit.Core.NotInParallel]
public sealed class ReactiveConnectionCoverageTests
{
    /// <summary>A serial port name that is deliberately absent.</summary>
    private const string MissingPort = "COM65535";

    /// <summary>The serial port name exposed by deterministic factory overrides.</summary>
    private const string InMemoryPort = "MODBUS-MEMORY";

    /// <summary>The valid unit identifier used by serial factories.</summary>
    private const byte UnitId = 1;

    /// <summary>The first invalid unit identifier above the protocol maximum.</summary>
    private const byte InvalidUnitId = 248;

    /// <summary>The standard serial baud rate.</summary>
    private const int BaudRate = 9_600;

    /// <summary>The standard serial data-bit count.</summary>
    private const int DataBits = 8;

    /// <summary>The representative register value used by loopback exchanges.</summary>
    private const ushort RegisterValue = 0x1234;

    /// <summary>The compact data-store size used by loopback slaves.</summary>
    private const ushort StoreSize = 16;

    /// <summary>The deterministic connection-monitor interval.</summary>
    private static readonly TimeSpan MonitorInterval = TimeSpan.FromMilliseconds(5);

    /// <summary>The upper bound for loopback connection setup.</summary>
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Allows the default serial-port poller to publish its initial snapshot.</summary>
    private static readonly TimeSpan SerialPollDelay = TimeSpan.FromMilliseconds(750);

    /// <summary>Creates a TCP master after a successful loopback ping and reports its state transitions.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task TcpIpMaster_ConnectsThroughLoopbackStateMachineAsync()
    {
        var originalPing = Create.PingInterval;
        var originalCheck = Create.CheckConnectionInterval;
        using var listener = new TestTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var connected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Create.PingInterval = MonitorInterval;
        Create.CheckConnectionInterval = MonitorInterval;
        try
        {
            using var subscription = Create.TcpIpMaster(IPAddress.Loopback.ToString(), port)
                .Where(static state => state.Connected)
                .Subscribe(_ => connected.TrySetResult(true));
            var completed = await Task.WhenAny(connected.Task, Task.Delay(ConnectionTimeout));

            await NativeAssert.That(completed).IsSameReferenceAs(connected.Task);
            await NativeAssert.That(await connected.Task).IsTrue();
            await NativeAssert.That(Create.UdpIpMaster(IPAddress.Loopback.ToString(), port)).IsNotNull();
        }
        finally
        {
            Create.PingInterval = originalPing;
            Create.CheckConnectionInterval = originalCheck;
        }
    }

    /// <summary>Exercises deterministic serial-master creation failures without physical hardware.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task SerialMasters_ReportAbsentPortAndValidateNamesAsync()
    {
        var rtu = await Create.SerialRtuMaster(
                MissingPort,
                BaudRate,
                DataBits,
                Parity.None,
                StopBits.One,
                Handshake.None)
            .Skip(1)
            .FirstAsync();
        var ascii = await Create.SerialAsciiMaster(
                MissingPort,
                BaudRate,
                DataBits,
                Parity.None,
                StopBits.One,
                Handshake.None)
            .Skip(1)
            .FirstAsync();

        await NativeAssert.That(rtu.Connected).IsFalse();
        await NativeAssert.That(rtu.Error).IsNotNull();
        await NativeAssert.That(ascii.Connected).IsFalse();
        await NativeAssert.That(ascii.Error).IsNotNull();
        await NativeAssert.That(
                async () => await Create.SerialRtuMaster(
                        string.Empty,
                        BaudRate,
                        DataBits,
                        Parity.None,
                        StopBits.One,
                        Handshake.None)
                    .FirstAsync())
            .Throws<ArgumentOutOfRangeException>();
        await NativeAssert.That(
                async () => await Create.SerialAsciiMaster(
                        " ",
                        BaudRate,
                        DataBits,
                        Parity.None,
                        StopBits.One,
                        Handshake.None)
                    .FirstAsync())
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Validates all reactive IP and serial slave factory arguments.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task SlaveFactories_ValidateProtocolEndpointsAsync()
    {
        await NativeAssert.That(() => Create.TcpIpSlave(string.Empty, 0, UnitId))
            .Throws<ArgumentOutOfRangeException>();
        await NativeAssert.That(() => Create.TcpIpSlave(IPAddress.Loopback.ToString(), -1, UnitId))
            .Throws<ArgumentOutOfRangeException>();
        await NativeAssert.That(() => Create.TcpIpSlave(IPAddress.Loopback.ToString(), 0, 0))
            .Throws<ArgumentOutOfRangeException>();
        await NativeAssert.That(() => Create.UdpIpSlave(string.Empty, 0, UnitId))
            .Throws<ArgumentOutOfRangeException>();
        await NativeAssert.That(() => Create.UdpIpSlave(IPAddress.Loopback.ToString(), -1, UnitId))
            .Throws<ArgumentOutOfRangeException>();
        await NativeAssert.That(() => Create.UdpIpSlave(IPAddress.Loopback.ToString(), 0, InvalidUnitId))
            .Throws<ArgumentOutOfRangeException>();
        await AssertSerialSlaveGuardsAsync(Create.SerialRtuSlave);
        await AssertSerialSlaveGuardsAsync(Create.SerialAsciiSlave);
    }

    /// <summary>Starts IP slave listeners with the requested unit identifiers and disposes them with subscriptions.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task IpSlaveFactories_EmitConfiguredSubscriptionOwnedSlavesAsync()
    {
        var tcpPort = ReserveTcpPort();
        ModbusTcpSlave? tcpSlave = null;
        ModbusUdpSlave? udpSlave = null;
        var tcpSubscription = Create.TcpIpSlave(IPAddress.Loopback.ToString(), tcpPort, UnitId)
            .Subscribe(slave => tcpSlave = slave);
        var udpSubscription = Create.UdpIpSlave(IPAddress.Loopback.ToString(), 0, UnitId)
            .Subscribe(slave => udpSlave = slave);

        await NativeAssert.That(tcpSlave?.UnitId).IsEqualTo(UnitId);
        await NativeAssert.That(udpSlave?.UnitId).IsEqualTo(UnitId);
        await NativeAssert.That(tcpSlave?.IsDisposed).IsFalse();
        await NativeAssert.That(udpSlave?.IsDisposed).IsFalse();

        tcpSubscription.Dispose();
        udpSubscription.Dispose();
        await Task.Delay(MonitorInterval);

        await NativeAssert.That(tcpSlave?.IsDisposed).IsTrue();
        await NativeAssert.That(udpSlave?.IsDisposed).IsTrue();
    }

    /// <summary>Round-trips a register through the corrected reactive TCP slave endpoint.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task TcpIpSlave_RoundTripsThroughRequestedLoopbackEndpointAsync()
    {
        var port = ReserveTcpPort();
        using var store = DataStoreFactory.CreateDefaultDataStore(StoreSize, StoreSize, StoreSize, StoreSize);
        ModbusTcpSlave? slave = null;
        using var subscription = Create.TcpIpSlave(IPAddress.Loopback.ToString(), port, UnitId)
            .Subscribe(value =>
            {
                value.DataStore = store;
                slave = value;
            });
        using var master = ModbusIpMaster.CreateIp(new TcpClientRx(IPAddress.Loopback.ToString(), port));

        await master.WriteSingleRegisterAsync(UnitId, 0, RegisterValue).WaitAsync(ConnectionTimeout);
        var registers = await master.ReadHoldingRegistersAsync(UnitId, 0, UnitId)
            .WaitAsync(ConnectionTimeout);
        await master.WriteSingleCoilAsync(0, true).WaitAsync(ConnectionTimeout);
        await master.WriteSingleRegisterAsync(0, RegisterValue).WaitAsync(ConnectionTimeout);
        await master.WriteMultipleCoilsAsync(0, [true]).WaitAsync(ConnectionTimeout);
        await master.WriteMultipleRegistersAsync(0, [RegisterValue]).WaitAsync(ConnectionTimeout);
        _ = await master.ReadCoilsAsync(0, UnitId).WaitAsync(ConnectionTimeout);
        _ = await master.ReadInputsAsync(0, UnitId).WaitAsync(ConnectionTimeout);
        _ = await master.ReadHoldingRegistersAsync(0, UnitId).WaitAsync(ConnectionTimeout);
        _ = await master.ReadInputRegistersAsync(0, UnitId).WaitAsync(ConnectionTimeout);
        _ = await master.ReadWriteMultipleRegistersAsync(0, UnitId, 0, [RegisterValue])
            .WaitAsync(ConnectionTimeout);

        await NativeAssert.That(slave?.UnitId).IsEqualTo(UnitId);
        await NativeAssert.That(registers).IsEquivalentTo([RegisterValue]);
    }

    /// <summary>Polls all four IP data areas through the internal reactive read composition.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task IpReadCores_PollAllAreasThroughLoopbackMasterAsync()
    {
        var port = ReserveTcpPort();
        using var store = DataStoreFactory.CreateDefaultDataStore(StoreSize, StoreSize, StoreSize, StoreSize);
        store.CoilDiscretes[UnitId] = true;
        store.InputDiscretes[UnitId] = true;
        store.HoldingRegisters[UnitId] = RegisterValue;
        store.InputRegisters[UnitId] = RegisterValue;
        using var slaveSubscription = Create.TcpIpSlave(IPAddress.Loopback.ToString(), port, UnitId)
            .Subscribe(slave => slave.DataStore = store);
        using var master = ModbusIpMaster.CreateIp(new TcpClientRx(IPAddress.Loopback.ToString(), port));
        var source = Observable.Return<(bool Connected, Exception? Error, ModbusIpMaster? Master)>(
            (true, null, master));
        var interval = ConnectionTimeout.TotalMilliseconds;

        var coils = await Create.ReadCoilsCore(source, 0, UnitId, interval)
            .FirstAsync()
            .WaitAsync(ConnectionTimeout);
        var inputs = await Create.ReadInputsCore(source, 0, UnitId, interval)
            .FirstAsync()
            .WaitAsync(ConnectionTimeout);
        var holdingRegisters = await Create.ReadHoldingRegistersCore(source, 0, UnitId, interval)
            .FirstAsync()
            .WaitAsync(ConnectionTimeout);
        var inputRegisters = await Create.ReadInputRegistersCore(source, 0, UnitId, interval)
            .FirstAsync()
            .WaitAsync(ConnectionTimeout);

        await NativeAssert.That(coils.Error).IsNull();
        await NativeAssert.That(coils.Data).IsEquivalentTo([true]);
        await NativeAssert.That(inputs.Error).IsNull();
        await NativeAssert.That(inputs.Data).IsEquivalentTo([true]);
        await NativeAssert.That(holdingRegisters.Error).IsNull();
        await NativeAssert.That(holdingRegisters.Data).IsEquivalentTo([RegisterValue]);
        await NativeAssert.That(inputRegisters.Error).IsNull();
        await NativeAssert.That(inputRegisters.Data).IsEquivalentTo([RegisterValue]);
    }

    /// <summary>Round-trips a coil through the corrected reactive UDP slave endpoint.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task UdpIpSlave_RoundTripsThroughRequestedLoopbackEndpointAsync()
    {
        var port = ReserveUdpPort();
        using var store = DataStoreFactory.CreateDefaultDataStore(StoreSize, StoreSize, StoreSize, StoreSize);
        ModbusUdpSlave? slave = null;
        using var subscription = Create.UdpIpSlave(IPAddress.Loopback.ToString(), port, UnitId)
            .Subscribe(value =>
            {
                value.DataStore = store;
                slave = value;
            });
        using var master = ModbusIpMaster.CreateIp(new UdpClientRx(IPAddress.Loopback.ToString(), port));

        await master.WriteSingleCoilAsync(UnitId, 0, true).WaitAsync(ConnectionTimeout);
        var coils = await master.ReadCoilsAsync(UnitId, 0, UnitId).WaitAsync(ConnectionTimeout);

        await NativeAssert.That(slave?.UnitId).IsEqualTo(UnitId);
        await NativeAssert.That(coils).IsEquivalentTo([true]);
    }

    /// <summary>Runs both serial-slave state machines against an absent port snapshot.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task SerialSlaveFactories_ResetWhenPortIsAbsentAsync()
    {
        var emissions = 0;
        using var rtu = Create.SerialRtuSlave(
                MissingPort,
                UnitId,
                BaudRate,
                DataBits,
                Parity.None,
                StopBits.One,
                Handshake.None)
            .Subscribe(_ => emissions++);
        using var ascii = Create.SerialAsciiSlave(
                MissingPort,
                UnitId,
                BaudRate,
                DataBits,
                Parity.None,
                StopBits.One,
                Handshake.None)
            .Subscribe(_ => emissions++);

        await Task.Delay(SerialPollDelay);

        await NativeAssert.That(emissions).IsEqualTo(0);
    }

    /// <summary>Exercises successful serial master and slave state machines through composed in-memory ports.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task SerialFactoryOverrides_CreateInMemoryMastersAndSlavesAsync()
    {
        var originalFactory = Create.SerialPortFactoryOverride;
        var originalNames = Create.SerialPortNamesOverride;
        try
        {
            using var rtuMasterPair = new InMemoryPortRxPair(InMemoryPort, "RTU-PEER");
            await AssertSerialMasterConnectsAsync(
                rtuMasterPair.First,
                () => Create.SerialRtuMaster(
                    InMemoryPort,
                    BaudRate,
                    DataBits,
                    Parity.None,
                    StopBits.One,
                    Handshake.None));

            using var asciiMasterPair = new InMemoryPortRxPair(InMemoryPort, "ASCII-PEER");
            await AssertSerialMasterConnectsAsync(
                asciiMasterPair.First,
                () => Create.SerialAsciiMaster(
                    InMemoryPort,
                    BaudRate,
                    DataBits,
                    Parity.None,
                    StopBits.One,
                    Handshake.None));

            using var rtuSlavePair = new InMemoryPortRxPair(InMemoryPort, "RTU-SLAVE-PEER");
            await AssertSerialSlaveConnectsAsync(
                rtuSlavePair.First,
                () => Create.SerialRtuSlave(
                    InMemoryPort,
                    UnitId,
                    BaudRate,
                    DataBits,
                    Parity.None,
                    StopBits.One,
                    Handshake.None));

            using var asciiSlavePair = new InMemoryPortRxPair(InMemoryPort, "ASCII-SLAVE-PEER");
            await AssertSerialSlaveConnectsAsync(
                asciiSlavePair.First,
                () => Create.SerialAsciiSlave(
                    InMemoryPort,
                    UnitId,
                    BaudRate,
                    DataBits,
                    Parity.None,
                    StopBits.One,
                    Handshake.None));
        }
        finally
        {
            Create.SerialPortFactoryOverride = originalFactory;
            Create.SerialPortNamesOverride = originalNames;
        }
    }

    /// <summary>Exercises composed serial master failure and missing-port state transitions.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task SerialFactoryOverrides_ReportInjectedFailuresAsync()
    {
        var originalFactory = Create.SerialPortFactoryOverride;
        var originalNames = Create.SerialPortNamesOverride;
        try
        {
            Create.SerialPortNamesOverride = () => Observable.Return<string[]>([InMemoryPort]);
            Create.SerialPortFactoryOverride = (_, _, _, _, _, _) =>
                throw new IOException("injected serial factory failure");

            var rtu = await Create.SerialRtuMaster(
                    InMemoryPort,
                    BaudRate,
                    DataBits,
                    Parity.None,
                    StopBits.One,
                    Handshake.None)
                .Skip(1)
                .FirstAsync();
            var ascii = await Create.SerialAsciiMaster(
                    InMemoryPort,
                    BaudRate,
                    DataBits,
                    Parity.None,
                    StopBits.One,
                    Handshake.None)
                .Skip(1)
                .FirstAsync();
            var networkFailure = await Create.SerialIpMaster(InMemoryPort, BaudRate)
                .Where(static state => state.Error?.InnerException is not null)
                .FirstAsync();

            await NativeAssert.That(rtu.Error?.InnerException).IsTypeOf<IOException>();
            await NativeAssert.That(ascii.Error?.InnerException).IsTypeOf<IOException>();
            await NativeAssert.That(networkFailure.Error?.InnerException).IsNotNull();

            Create.SerialPortNamesOverride = () => Observable.Return<string[]>([]);
            var missing = await Create.SerialIpMaster(InMemoryPort, BaudRate).FirstAsync();
            await NativeAssert.That(missing.Connected).IsFalse();
            await NativeAssert.That(missing.Master).IsNull();
        }
        finally
        {
            Create.SerialPortFactoryOverride = originalFactory;
            Create.SerialPortNamesOverride = originalNames;
        }
    }

    /// <summary>Asserts common serial-slave factory argument guards.</summary>
    /// <param name="factory">The serial slave factory.</param>
    /// <returns>A task representing the asynchronous assertion.</returns>
    private static async Task AssertSerialSlaveGuardsAsync(
        Func<string, byte, int, int, Parity, StopBits, Handshake, IObservable<ModbusSerialSlave>> factory)
    {
        await NativeAssert.That(
                () => factory(string.Empty, UnitId, BaudRate, DataBits, Parity.None, StopBits.One, Handshake.None))
            .Throws<ArgumentOutOfRangeException>();
        await NativeAssert.That(
                () => factory(MissingPort, 0, BaudRate, DataBits, Parity.None, StopBits.One, Handshake.None))
            .Throws<ArgumentOutOfRangeException>();
        await NativeAssert.That(
                () => factory(
                    MissingPort,
                    InvalidUnitId,
                    BaudRate,
                    DataBits,
                    Parity.None,
                    StopBits.One,
                    Handshake.None))
            .Throws<ArgumentOutOfRangeException>();
        await NativeAssert.That(
                factory(MissingPort, UnitId, BaudRate, DataBits, Parity.None, StopBits.One, Handshake.None))
            .IsNotNull();
    }

    /// <summary>Asserts a serial master reaches the connected state through an injected port.</summary>
    /// <param name="port">The injected in-memory port.</param>
    /// <param name="source">Creates the master state observable.</param>
    /// <returns>A task representing the asynchronous assertion.</returns>
    private static async Task AssertSerialMasterConnectsAsync(
        SerialPortRx port,
        Func<IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)>> source)
    {
        ConfigureSerialOverrides(port);
        var connected = new TaskCompletionSource<IModbusSerialMaster>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = source().Subscribe(state =>
            _ = state.Connected &&
                state.Master is not null &&
                connected.TrySetResult(state.Master));

        await NativeAssert.That(await connected.Task.WaitAsync(ConnectionTimeout)).IsNotNull();
    }

    /// <summary>Asserts a serial slave is emitted through an injected port and is subscription-owned.</summary>
    /// <param name="port">The injected in-memory port.</param>
    /// <param name="source">Creates the slave observable.</param>
    /// <returns>A task representing the asynchronous assertion.</returns>
    private static async Task AssertSerialSlaveConnectsAsync(
        SerialPortRx port,
        Func<IObservable<ModbusSerialSlave>> source)
    {
        ConfigureSerialOverrides(port);
        var connected = new TaskCompletionSource<ModbusSerialSlave>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var subscription = source().Subscribe(slave => connected.TrySetResult(slave));
        var active = await connected.Task.WaitAsync(ConnectionTimeout);

        await NativeAssert.That(active.UnitId).IsEqualTo(UnitId);
        subscription.Dispose();
        await Task.Delay(MonitorInterval);
        await NativeAssert.That(active.IsDisposed).IsTrue();
    }

    /// <summary>Configures deterministic serial port discovery and construction.</summary>
    /// <param name="port">The in-memory port returned to the state machine.</param>
    private static void ConfigureSerialOverrides(SerialPortRx port)
    {
        Create.SerialPortFactoryOverride = (_, _, _, _, _, _) => port;
        Create.SerialPortNamesOverride = () => Observable.Return<string[]>([InMemoryPort]);
    }

    /// <summary>Reserves and releases an available loopback TCP port.</summary>
    /// <returns>The available port number.</returns>
    private static int ReserveTcpPort()
    {
        using var listener = new TestTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    /// <summary>Reserves and releases an available loopback UDP port.</summary>
    /// <returns>The available port number.</returns>
    private static int ReserveUdpPort()
    {
        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)client.Client.LocalEndPoint!).Port;
    }
}
