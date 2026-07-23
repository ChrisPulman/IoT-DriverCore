// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.ModbusRx.IO;
using IoT.DriverCore.Serial;

namespace IoT.DriverCore.ModbusRx.UnitTests;

/// <summary>Bounded loopback coverage for Modbus transport adapters and slave factories.</summary>
[TUnit.Core.NotInParallel]
public sealed class TransportAdapterResidualCoverageTests
{
    /// <summary>The unit identifier used by the slave factory tests.</summary>
    private const byte UnitId = 7;

    /// <summary>The timeout applied to loopback streams.</summary>
    private const int StreamTimeout = 1_000;

    /// <summary>The offset used to verify adapter buffer slicing.</summary>
    private const int BufferOffset = 1;

    /// <summary>The number of bytes transferred by each loopback adapter test.</summary>
    private const int PayloadLength = 3;

    /// <summary>The maximum duration permitted for a loopback I/O operation.</summary>
    private static readonly TimeSpan IoTimeout = TimeSpan.FromSeconds(3);

    /// <summary>Forwards TCP timeouts, reads, writes, flushing, and disposal through the adapter.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task TcpClientAdapter_ForwardsBoundedLoopbackOperationsAsync()
    {
        using var listener = new TestTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var client = new TcpClientRx(IPAddress.Loopback.ToString(), port);
        using var accepted = await listener.AcceptTcpClientAsync().WaitAsync(IoTimeout);
        using var adapter = new TcpClientAdapter(client)
        {
            ReadTimeout = StreamTimeout,
            WriteTimeout = StreamTimeout,
        };
        var response = new byte[] { 0x31, 0x32, 0x33 };
        var readBuffer = new byte[5];
        var request = new byte[] { 0x00, 0x41, 0x42, 0x43, 0x00 };
        var requestBuffer = new byte[PayloadLength];

        await accepted.GetStream().WriteAsync(response, 0, response.Length).WaitAsync(IoTimeout);
        var read = await adapter.ReadAsync(readBuffer, BufferOffset, response.Length).WaitAsync(IoTimeout);
        adapter.DiscardInBuffer();
        adapter.Write(request, BufferOffset, requestBuffer.Length);
        var written = await accepted.GetStream()
            .ReadAsync(requestBuffer, 0, requestBuffer.Length)
            .WaitAsync(IoTimeout);

        await TUnit.Assertions.Assert.That(adapter.InfiniteTimeout).IsEqualTo(Timeout.Infinite);
        await TUnit.Assertions.Assert.That(adapter.ReadTimeout).IsEqualTo(StreamTimeout);
        await TUnit.Assertions.Assert.That(adapter.WriteTimeout).IsEqualTo(StreamTimeout);
        await TUnit.Assertions.Assert.That(read).IsEqualTo(response.Length);
        await TUnit.Assertions.Assert.That(Slice(readBuffer, BufferOffset, PayloadLength))
            .IsEquivalentTo(response);
        await TUnit.Assertions.Assert.That(written).IsEqualTo(requestBuffer.Length);
        await TUnit.Assertions.Assert.That(requestBuffer)
            .IsEquivalentTo(Slice(request, BufferOffset, PayloadLength));
    }

    /// <summary>Forwards UDP timeouts, reads, writes, the no-op discard, and disposal through the adapter.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task UdpClientAdapter_ForwardsBoundedLoopbackOperationsAsync()
    {
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var receiverEndpoint = (IPEndPoint)receiver.Client.LocalEndPoint!;
        using var client = new UdpClientRx(new IPEndPoint(IPAddress.Loopback, 0));
        client.Connect(receiverEndpoint);
        using var adapter = new UdpClientAdapter(client)
        {
            ReadTimeout = StreamTimeout,
            WriteTimeout = StreamTimeout,
        };
        var clientEndpoint = (IPEndPoint)client.Client.LocalEndPoint!;
        var response = new byte[] { 0x51, 0x52, 0x53 };
        var readBuffer = new byte[5];
        var request = new byte[] { 0x00, 0x61, 0x62, 0x63, 0x00 };

        await receiver.SendAsync(response, response.Length, clientEndpoint).WaitAsync(IoTimeout);
        var read = await adapter.ReadAsync(readBuffer, BufferOffset, response.Length).WaitAsync(IoTimeout);
        adapter.DiscardInBuffer();
        adapter.Write(request, BufferOffset, response.Length);
        var received = await receiver.ReceiveAsync().WaitAsync(IoTimeout);

        await TUnit.Assertions.Assert.That(adapter.InfiniteTimeout).IsEqualTo(Timeout.Infinite);
        await TUnit.Assertions.Assert.That(adapter.ReadTimeout).IsEqualTo(StreamTimeout);
        await TUnit.Assertions.Assert.That(adapter.WriteTimeout).IsEqualTo(StreamTimeout);
        await TUnit.Assertions.Assert.That(read).IsEqualTo(response.Length);
        await TUnit.Assertions.Assert.That(Slice(readBuffer, BufferOffset, PayloadLength))
            .IsEquivalentTo(response);
        await TUnit.Assertions.Assert.That(received.Buffer)
            .IsEquivalentTo(Slice(request, BufferOffset, PayloadLength));
        await TUnit.Assertions.Assert.That(() => new UdpClientAdapter(null!)).Throws<ArgumentNullException>();
    }

    /// <summary>Exposes serial transports through the explicit serial-master contract without opening a listener.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusSerialMaster_ExposesExplicitSerialTransportAsync()
    {
        using var rtu = ModbusSerialMaster.CreateRtu(new StubStreamResource());
        using var ascii = ModbusSerialMaster.CreateAscii(new StubStreamResource());

        await TUnit.Assertions.Assert.That(((IModbusSerialMaster)rtu).Transport).IsNotNull();
        await TUnit.Assertions.Assert.That(((IModbusSerialMaster)ascii).Transport).IsNotNull();
    }

    /// <summary>Constructs and disposes TCP and UDP slaves without starting their unbounded listeners.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusSlaves_FactoriesAndBoundedDisposalRemainSafeAsync()
    {
        using var listener = new TestTcpListener(IPAddress.Loopback, 0);
        using var tcpSlave = ModbusTcpSlave.CreateTcp(UnitId, listener);
        using var udpClient = new UdpClientRx(new IPEndPoint(IPAddress.Loopback, 0));
        using var udpSlave = ModbusUdpSlave.CreateUdp(UnitId, udpClient);

        await TUnit.Assertions.Assert.That(tcpSlave.UnitId).IsEqualTo(UnitId);
        await TUnit.Assertions.Assert.That(tcpSlave.Masters).IsEmpty();
        await TUnit.Assertions.Assert.That(udpSlave.UnitId).IsEqualTo(UnitId);
        await TUnit.Assertions.Assert.That(() => ModbusTcpSlave.CreateTcp(UnitId, null!))
            .Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(() => ModbusUdpSlave.CreateUdp(UnitId, null!))
            .Throws<ArgumentNullException>();

        tcpSlave.Dispose();
        tcpSlave.Dispose();
        udpSlave.Dispose();
        udpSlave.Dispose();

        await TUnit.Assertions.Assert.That(async () => await tcpSlave.ListenAsync()).Throws<ObjectDisposedException>();
    }

    /// <summary>Copies a bounded byte range without relying on framework-specific range APIs.</summary>
    /// <param name="source">The source buffer.</param>
    /// <param name="offset">The source offset.</param>
    /// <param name="count">The byte count.</param>
    /// <returns>The copied byte range.</returns>
    private static byte[] Slice(byte[] source, int offset, int count)
    {
        var result = new byte[count];
        Array.Copy(source, offset, result, 0, count);
        return result;
    }

    /// <summary>A minimal stream resource used solely to construct serial transports.</summary>
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
