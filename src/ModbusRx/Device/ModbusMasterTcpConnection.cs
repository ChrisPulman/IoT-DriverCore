// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Net;
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

/// <summary>Represents an incoming Modbus-master connection and its request-processing logic.</summary>
internal sealed class ModbusMasterTcpConnection : ModbusDevice
{
    /// <summary>Stores the slave value.</summary>
    private readonly ModbusTcpSlave _slave;

    /// <summary>Stores the mbap Header value.</summary>
    private readonly byte[] _mbapHeader = new byte[6];

    /// <summary>Stores the message Frame value.</summary>
    private byte[]? _messageFrame;

    /// <summary>Initializes a new instance of the Modbus Master Tcp Connection class.</summary>
    /// <param name="client">The client value.</param>
    /// <param name="slave">The slave value.</param>
    public ModbusMasterTcpConnection(TcpClientRx client, ModbusTcpSlave slave)
        : base(new ModbusIpTransport(new TcpClientAdapter(client)))
    {
        TcpClient = client;
        _slave = slave;

        EndPoint = client.Client.RemoteEndPoint?.ToString()
            ?? throw new InvalidOperationException("The TCP client does not have a remote endpoint.");
        Stream = client.Stream;
        _ = Task.Run(HandleRequestAsync);
    }

    /// <summary>Occurs when a Modbus master TCP connection is closed.</summary>
    internal event EventHandler<TcpConnectionEventArgs>? ModbusMasterTcpConnectionClosed;

    /// <summary>Gets or sets the End Point value.</summary>
    internal string EndPoint { get; }

    /// <summary>Gets or sets the Stream value.</summary>
    internal Stream Stream { get; }

    /// <summary>Gets or sets the Tcp Client value.</summary>
    internal TcpClientRx TcpClient { get; }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Stream.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Executes the Handle Request Async operation.</summary>
    /// <returns>The result.</returns>
    private async Task HandleRequestAsync()
    {
        while (true)
        {
            Debug.WriteLine($"Begin reading header from Master at IP: {EndPoint}");
#if NET8_0_OR_GREATER
            var readBytes = await Stream.ReadAsync(
                _mbapHeader.AsMemory(0, Six),
                CancellationToken.None).ConfigureAwait(false);
#else
            var readBytes = await Stream.ReadAsync(
                _mbapHeader,
                0,
                Six,
                CancellationToken.None).ConfigureAwait(false);
#endif
            if (readBytes == 0)
            {
                Debug.WriteLine($"0 bytes read, Master at {EndPoint} has closed Socket connection.");
                ModbusMasterTcpConnectionClosed?.Invoke(this, new TcpConnectionEventArgs(EndPoint));
                return;
            }

            var frameLength = (ushort)IPAddress.HostToNetworkOrder(BitConverter.ToInt16(_mbapHeader, Four));
            Debug.WriteLine(
                $"Master at {EndPoint} sent header: \"{string.Join(", ", _mbapHeader)}\" " +
                $"with {frameLength} bytes in PDU");

            _messageFrame = new byte[frameLength];
#if NET8_0_OR_GREATER
            readBytes = await Stream.ReadAsync(
                _messageFrame.AsMemory(0, frameLength),
                CancellationToken.None).ConfigureAwait(false);
#else
            readBytes = await Stream.ReadAsync(
                _messageFrame,
                0,
                frameLength,
                CancellationToken.None).ConfigureAwait(false);
#endif
            if (readBytes == 0)
            {
                Debug.WriteLine($"0 bytes read, Master at {EndPoint} has closed Socket connection.");
                ModbusMasterTcpConnectionClosed?.Invoke(this, new TcpConnectionEventArgs(EndPoint));
                return;
            }

            Debug.WriteLine($"Read frame from Master at {EndPoint} completed {readBytes} bytes");
            var frame = new byte[_mbapHeader.Length + _messageFrame.Length];
            Array.Copy(_mbapHeader, 0, frame, 0, _mbapHeader.Length);
            Array.Copy(_messageFrame, 0, frame, _mbapHeader.Length, _messageFrame.Length);
            Debug.WriteLine($"RX from Master at {EndPoint}: {string.Join(", ", frame)}");

            var request = ModbusMessageFactory.CreateModbusRequest(_messageFrame);
            request.TransactionId = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(frame, 0));

            // perform action and build response
            var response = _slave.ApplyRequest(request);
            response.TransactionId = request.TransactionId;

            // write response
            var responseFrame = Transport?.BuildMessageFrame(response);
            Debug.WriteLine($"TX to Master at {EndPoint}: {string.Join(", ", responseFrame!)}");
#if NET8_0_OR_GREATER
            await Stream.WriteAsync(
                responseFrame.AsMemory(0, responseFrame!.Length),
                CancellationToken.None).ConfigureAwait(false);
#else
            await Stream.WriteAsync(
                responseFrame!,
                0,
                responseFrame!.Length,
                CancellationToken.None).ConfigureAwait(false);
#endif
        }
    }
}
