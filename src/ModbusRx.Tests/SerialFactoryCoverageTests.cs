// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net.Sockets;
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.ModbusRx.IO;
using IoT.DriverCore.Serial;
using NativeAssert = TUnit.Assertions.Assert;

namespace IoT.DriverCore.ModbusRx.UnitTests;

/// <summary>Deterministic coverage for serial factories and the serial stream bridge.</summary>
public sealed class SerialFactoryCoverageTests
{
    /// <summary>The representative unit identifier.</summary>
    private const byte UnitId = 1;

    /// <summary>A harmless loopback UDP destination used to create a connected datagram socket.</summary>
    private const int UdpDestinationPort = 9;

    /// <summary>The representative stream timeout.</summary>
    private const int StreamTimeout = 250;

    /// <summary>The number of bytes transferred by adapter tests.</summary>
    private const int PayloadLength = 3;

    /// <summary>Creates every serial master overload and validates their null and connection guards.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task SerialMasterFactories_CreateEveryTransportAndValidateGuardsAsync()
    {
        using var rtuPair = new InMemoryPortRxPair("RTU-A", "RTU-B");
        using var asciiPair = new InMemoryPortRxPair("ASCII-A", "ASCII-B");
        using var tcpRtu = new TcpClientRx(new TcpClient());
        using var tcpAscii = new TcpClientRx(new TcpClient());
        using var udpRtu = new UdpClientRx("127.0.0.1", UdpDestinationPort);
        using var udpAscii = new UdpClientRx("127.0.0.1", UdpDestinationPort);
        using var rtu = ModbusSerialMaster.CreateRtu(rtuPair.First);
        using var ascii = ModbusSerialMaster.CreateAscii(asciiPair.First);
        using var rtuTcp = ModbusSerialMaster.CreateRtu(tcpRtu);
        using var asciiTcp = ModbusSerialMaster.CreateAscii(tcpAscii);
        using var rtuUdp = ModbusSerialMaster.CreateRtu(udpRtu);
        using var asciiUdp = ModbusSerialMaster.CreateAscii(udpAscii);
        using var rtuStream = ModbusSerialMaster.CreateRtu(new StubStreamResource());
        using var asciiStream = ModbusSerialMaster.CreateAscii(new StubStreamResource());
        using var networkPair = new InMemoryPortRxPair("IP-A", "IP-B");
        using var networkSerial = ModbusIpMaster.CreateIp(networkPair.First);
        using var networkStream = ModbusIpMaster.CreateIp(new StubStreamResource());

        await NativeAssert.That(rtu.Transport).IsNotNull();
        await NativeAssert.That(ascii.Transport).IsNotNull();
        await NativeAssert.That(rtuTcp.Transport).IsNotNull();
        await NativeAssert.That(asciiTcp.Transport).IsNotNull();
        await NativeAssert.That(rtuUdp.Transport).IsNotNull();
        await NativeAssert.That(asciiUdp.Transport).IsNotNull();
        await NativeAssert.That(networkSerial.Transport).IsNotNull();
        await NativeAssert.That(networkStream.Transport).IsNotNull();
        await AssertMasterNullGuardsAsync();

        using var disconnectedUdp = new UdpClientRx();
        await NativeAssert.That(() => ModbusSerialMaster.CreateRtu(disconnectedUdp))
            .Throws<InvalidOperationException>();
        await NativeAssert.That(() => ModbusSerialMaster.CreateAscii(disconnectedUdp))
            .Throws<InvalidOperationException>();
        await NativeAssert.That(() => ModbusIpMaster.CreateIp(disconnectedUdp))
            .Throws<InvalidOperationException>();
    }

    /// <summary>Creates both serial slave formats and validates all factory null guards.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task SerialSlaveFactories_CreateEveryTransportAndValidateGuardsAsync()
    {
        using var rtuPair = new InMemoryPortRxPair("SLAVE-RTU-A", "SLAVE-RTU-B");
        using var asciiPair = new InMemoryPortRxPair("SLAVE-ASCII-A", "SLAVE-ASCII-B");
        using var rtu = ModbusSerialSlave.CreateRtu(UnitId, rtuPair.First);
        using var ascii = ModbusSerialSlave.CreateAscii(UnitId, asciiPair.First);
        using var rtuStream = ModbusSerialSlave.CreateRtu(UnitId, new StubStreamResource());
        using var asciiStream = ModbusSerialSlave.CreateAscii(UnitId, new StubStreamResource());

        await NativeAssert.That(rtu.UnitId).IsEqualTo(UnitId);
        await NativeAssert.That(ascii.UnitId).IsEqualTo(UnitId);
        await NativeAssert.That(rtuStream.UnitId).IsEqualTo(UnitId);
        await NativeAssert.That(asciiStream.UnitId).IsEqualTo(UnitId);
        await NativeAssert.That(() => ModbusSerialSlave.CreateRtu(UnitId, (SerialPortRx)null!))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(() => ModbusSerialSlave.CreateAscii(UnitId, (SerialPortRx)null!))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(() => ModbusSerialSlave.CreateRtu(UnitId, (IStreamResource)null!))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(() => ModbusSerialSlave.CreateAscii(UnitId, (IStreamResource)null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>Round-trips raw bytes through the serial adapter and its in-memory connection.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task SerialPortAdapter_ForwardsConfigurationBuffersAndBytesAsync()
    {
        using var pair = new InMemoryPortRxPair("ADAPTER-A", "ADAPTER-B");
        pair.First.EnableAutoDataReceive = false;
        pair.Second.EnableAutoDataReceive = false;
        await pair.First.OpenAsync();
        await pair.Second.OpenAsync();
        using var adapter = new SerialPortAdapter(pair.First)
        {
            ReadTimeout = StreamTimeout,
            WriteTimeout = StreamTimeout,
        };
        var request = new byte[] { UnitId, PayloadLength, UnitId };
        var peerBuffer = new byte[PayloadLength];
        var responseBuffer = new byte[PayloadLength];

        adapter.DiscardInBuffer();
        adapter.Write(request, 0, request.Length);
        var peerRead = await pair.Second.ReadAsync(peerBuffer, 0, peerBuffer.Length);
        pair.Second.Write(request, 0, request.Length);
        var responseRead = await adapter.ReadAsync(responseBuffer, 0, responseBuffer.Length);

        await NativeAssert.That(adapter.ReadTimeout).IsEqualTo(StreamTimeout);
        await NativeAssert.That(adapter.WriteTimeout).IsEqualTo(StreamTimeout);
        await NativeAssert.That(adapter.InfiniteTimeout).IsLessThan(0);
        await NativeAssert.That(peerRead).IsEqualTo(PayloadLength);
        await NativeAssert.That(responseRead).IsEqualTo(PayloadLength);
        await NativeAssert.That(peerBuffer).IsEquivalentTo(request);
        await NativeAssert.That(responseBuffer).IsEquivalentTo(request);
        await NativeAssert.That(() => new SerialPortAdapter(null!)).Throws<ArgumentNullException>();
    }

    /// <summary>Asserts the null guard on every serial-master factory overload.</summary>
    /// <returns>A task representing the asynchronous assertions.</returns>
    private static async Task AssertMasterNullGuardsAsync()
    {
        await NativeAssert.That(() => ModbusSerialMaster.CreateRtu((SerialPortRx)null!))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(() => ModbusSerialMaster.CreateAscii((SerialPortRx)null!))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(() => ModbusSerialMaster.CreateRtu((TcpClientRx)null!))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(() => ModbusSerialMaster.CreateAscii((TcpClientRx)null!))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(() => ModbusSerialMaster.CreateRtu((UdpClientRx)null!))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(() => ModbusSerialMaster.CreateAscii((UdpClientRx)null!))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(() => ModbusSerialMaster.CreateRtu((IStreamResource)null!))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(() => ModbusSerialMaster.CreateAscii((IStreamResource)null!))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(() => ModbusIpMaster.CreateIp((SerialPortRx)null!))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(() => ModbusIpMaster.CreateIp((TcpClientRx)null!))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(() => ModbusIpMaster.CreateIp((UdpClientRx)null!))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(() => ModbusIpMaster.CreateIp((IStreamResource)null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>A minimal deterministic stream resource used only for construction coverage.</summary>
    private sealed class StubStreamResource : IStreamResource
    {
        /// <inheritdoc />
        public int InfiniteTimeout => Timeout.Infinite;

        /// <inheritdoc />
        public int ReadTimeout { get; set; }

        /// <inheritdoc />
        public int WriteTimeout { get; set; }

        /// <inheritdoc />
        public void DiscardInBuffer()
        {
        }

        /// <inheritdoc />
        public Task<int> ReadAsync(byte[] buffer, int offset, int count) => Task.FromResult(0);

        /// <inheritdoc />
        public void Write(byte[] buffer, int offset, int count)
        {
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}
