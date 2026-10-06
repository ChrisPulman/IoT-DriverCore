// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;
using System.Net;
using System.Net.Sockets;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Utilities.Encoders;
using TAssert = TUnit.Assertions.Assert;

namespace IoT.Driver.S7PlcRx.Tests.Symbolic;

/// <summary>Runs an independent COTP and TLS peer without using production transport or wire serializers.</summary>
internal sealed class PlusLoopbackPeer : IAsyncDisposable
{
    /// <summary>The initial session creation function.</summary>
    private const ushort CreateFunction = 0x04ca;

    /// <summary>The request header's third reserved octet.</summary>
    private const int ReservedOctetIndex = 5;

    /// <summary>The independently specified wire fixture value 16.</summary>
    private const int WireValue16 = 16;

    /// <summary>The independently specified wire fixture value 31.</summary>
    private const int WireValue31 = 31;

    /// <summary>The independently specified wire fixture value 224.</summary>
    private const int WireValue224 = 0xe0;

    /// <summary>The independently specified wire fixture value 192.</summary>
    private const int WireValue192 = 0xc0;

    /// <summary>The independently specified wire fixture value 10.</summary>
    private const int WireValue10 = 10;

    /// <summary>The independently specified wire fixture value 193.</summary>
    private const int WireValue193 = 0xc1;

    /// <summary>The independently specified wire fixture value 2.</summary>
    private const int WireValue2 = 2;

    /// <summary>The independently specified wire fixture value 6.</summary>
    private const int WireValue6 = 6;

    /// <summary>The independently specified wire fixture value 194.</summary>
    private const int WireValue194 = 0xc2;

    /// <summary>The independently specified wire fixture value 9.</summary>
    private const int WireValue9 = 9;

    /// <summary>The independently specified wire fixture value 208.</summary>
    private const int WireValue208 = 0xd0;

    /// <summary>The independently specified wire fixture value 7.</summary>
    private const int WireValue7 = 7;

    /// <summary>The independently specified wire fixture value 240.</summary>
    private const int WireValue240 = 0xf0;

    /// <summary>The independently specified wire fixture value 128.</summary>
    private const int WireValue128 = 0x80;

    /// <summary>The independently specified wire fixture value 114.</summary>
    private const int WireValue114 = 0x72;

    /// <summary>The independently specified wire fixture value 18.</summary>
    private const int WireValue18 = 18;

    /// <summary>The independently specified wire fixture value 1459.</summary>
    private const int WireValue1459 = 0x05b3;

    /// <summary>The independently specified wire fixture value 288.</summary>
    private const int WireValue288 = 288;

    /// <summary>The independently specified wire fixture value 48.</summary>
    private const int WireValue48 = 0x30;

    /// <summary>The independently specified wire fixture value 14.</summary>
    private const int WireValue14 = 14;

    /// <summary>The independently specified wire fixture value 4.</summary>
    private const int WireValue4 = 4;

    /// <summary>The independently specified wire fixture value 8.</summary>
    private const int WireValue8 = 8;

    /// <summary>The independently specified wire fixture value 3.</summary>
    private const int WireValue3 = 3;

    /// <summary>The independently specified wire fixture value 50.</summary>
    private const int WireValue50 = 0x32;

    /// <summary>The independently specified wire fixture value 13.</summary>
    private const int WireValue13 = 13;

    /// <summary>The independently specified wire fixture value 29.</summary>
    private const int WireValue29 = 29;

    /// <summary>The independently specified wire fixture value 49.</summary>
    private const int WireValue49 = 0x31;

    /// <summary>The independently specified wire fixture value 509.</summary>
    private const int WireValue509 = 509;

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture1 =
    [
        WireValue31,
        WireValue224,
        0,
        0,
        0,
        1,
        0,
        WireValue192,
        1,
        WireValue10,
        WireValue193,
        WireValue2,
        WireValue6,
        0,
        WireValue194,
        WireValue16,
    ];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture2 = [WireValue9, WireValue208, 0, 1, 0, WireValue2, 0, WireValue192, 1, WireValue9];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture3 = [WireValue2, WireValue240, WireValue128, WireValue114, 1, 0, WireValue18];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture4 = new byte[WireValue4];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture5 = [0];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture7 = [WireValue2, WireValue240, WireValue128];

    /// <summary>Accepts one isolated loopback connection.</summary>
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

    /// <summary>Authenticates and exports the server TLS context.</summary>
    private readonly PlusLoopbackTlsServer _server = new(TimeProvider.System);

    /// <summary>Processes TLS records without a blocking stream adapter.</summary>
    private readonly TlsServerProtocol _tls = new();

    /// <summary>Buffers decrypted application data across record boundaries.</summary>
    private readonly Queue<byte> _plaintext = new();

    /// <summary>Owns the accepted socket.</summary>
    private TcpClient? _socket;

    /// <summary>Owns the accepted TCP stream.</summary>
    private Stream? _stream;

    /// <summary>Stores the current request for response correlation.</summary>
    private byte[] _request = [];

    /// <summary>Stores the current Plus framing version.</summary>
    private byte _version;

    /// <summary>Checks monotonically increasing wire sequence numbers independently of client state.</summary>
    private ushort _expectedSequence;

    /// <summary>Closes the accepted socket on cancellation, including framework NetworkStream implementations.</summary>
    private CancellationTokenRegistration _connectionCancellation;

    /// <summary>Initializes a new instance of the <see cref="PlusLoopbackPeer"/> class.</summary>
    internal PlusLoopbackPeer() => _listener.Start();

    /// <summary>Gets the dynamically allocated TCP port.</summary>
    internal int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>Gets the SHA-256 pin of the independently generated certificate.</summary>
    internal string CertificatePin
    {
        get
        {
            return Hex.ToHexString(PlusLoopbackWire.Digest(_server.CertificateBytes, false));
        }
    }

    /// <summary>Gets whether the server authenticated the client's Finished message.</summary>
    internal bool HandshakeComplete => _server.HandshakeComplete;

    /// <summary>Closes the isolated socket and listener.</summary>
    /// <returns>A completed disposal operation.</returns>
    public ValueTask DisposeAsync()
    {
        _connectionCancellation.Dispose();
        _server.ClearExporter();
        _stream?.Dispose();
        _socket?.Dispose();
#if NET6_0_OR_GREATER
        _listener.Dispose();
#else
        _listener.Stop();
#endif
        return default;
    }

    /// <summary>Accepts COTP, verifies clear InitSSL, and negotiates TLS 1.3 inside DT packets.</summary>
    /// <param name="cancellationToken">Bounds all network operations.</param>
    /// <returns>A task representing acceptance and the secure handshake.</returns>
    internal async Task AcceptAsync(CancellationToken cancellationToken)
    {
#if NET6_0_OR_GREATER
        await using var registration = cancellationToken.Register(_listener.Stop);
        _socket = await _listener.AcceptTcpClientAsync(cancellationToken);
#else
        using var registration = cancellationToken.Register(_listener.Stop);
        _socket = await _listener.AcceptTcpClientAsync();
#endif
        _connectionCancellation = cancellationToken.Register(static state => ((TcpClient?)state)?.Close(), _socket);
        _socket.NoDelay = true;
        _stream = _socket.GetStream();
        var cr = await ReadPacketAsync(cancellationToken);
        await TAssert.That(PlusLoopbackWire.Equal(PlusLoopbackWire.Slice(cr, 0, WireValue16), WireFixture1)).IsTrue();
        await TAssert.That(System.Text.Encoding.ASCII.GetString(cr, WireValue16, WireValue16)).IsEqualTo("SIMATIC-ROOT-HMI");
        await WritePacketAsync(WireFixture2, cancellationToken);
        var initial = await ReadPacketAsync(cancellationToken);
        await TAssert.That(PlusLoopbackWire.Equal(PlusLoopbackWire.Slice(initial, 0, WireValue7), WireFixture3)).IsTrue();
        _version = 1;
        _request = PlusLoopbackWire.Slice(initial, WireValue7, WireValue18);
        await VerifyRequestAsync(WireValue1459, WireValue288, WireValue48);
        await TAssert.That(PlusLoopbackWire.Equal(PlusLoopbackWire.Slice(_request, WireValue14), WireFixture4)).IsTrue();
        await RespondAsync(WireFixture5, cancellationToken);
        _tls.Accept(_server);
        while (!_server.HandshakeComplete)
        {
            await FeedAsync(cancellationToken);
        }
    }

    /// <summary>Obtains an independently negotiated OMS exporter.</summary>
    /// <returns>The exporter bytes from the server context.</returns>
    internal byte[] Export() => _server.Export();

    /// <summary>Reassembles an encrypted Plus request and checks its operation and session identity.</summary>
    /// <param name="function">The expected operation identifier.</param>
    /// <param name="session">The expected session identifier.</param>
    /// <param name="flags">The expected request flags.</param>
    /// <param name="cancellationToken">Bounds the receive.</param>
    /// <returns>The request body after the fixed header.</returns>
    internal async Task<byte[]> ReceiveAsync(ushort function, uint session, byte flags, CancellationToken cancellationToken)
    {
        var header = await ReadPlainAsync(WireValue4, cancellationToken);
        _version = header[1];
        var bytes = new List<byte>();
        while (true)
        {
            await TAssert.That(header[0]).IsEqualTo((byte)WireValue114);
            await TAssert.That(header[1]).IsEqualTo(_version);
            var length = (header[WireValue2] << WireValue8) | header[WireValue3];
            if (length == 0)
            {
                break;
            }

            bytes.AddRange(await ReadPlainAsync(length, cancellationToken));
            header = await ReadPlainAsync(WireValue4, cancellationToken);
        }

        _request = bytes.ToArray();
        await VerifyRequestAsync(function, session, flags);
        return PlusLoopbackWire.Slice(_request, WireValue14);
    }

    /// <summary>Returns a correlated response, splitting Plus and TLS records independently of COTP.</summary>
    /// <param name="body">The independently encoded response body.</param>
    /// <param name="cancellationToken">Bounds the send.</param>
    /// <returns>A task representing the send.</returns>
    internal async Task RespondAsync(byte[] body, CancellationToken cancellationToken)
    {
        byte[] header = [WireValue50, 0, 0, _request[WireValue3], _request[WireValue4], 0, 0, _request[WireValue7], _request[WireValue8], _request[WireValue13]];
        var response = PlusLoopbackWire.Join(header, body);
        var framed = new List<byte>();
        for (var offset = 0; offset < response.Length;)
        {
            var count = Math.Min(WireValue29, response.Length - offset);
            framed.Add((byte)WireValue114);
            framed.Add(_version);
            framed.Add(0);
            framed.Add((byte)count);
            framed.AddRange(PlusLoopbackWire.Slice(response, offset, count));
            offset += count;
        }

        framed.Add((byte)WireValue114);
        framed.Add(_version);
        framed.Add(0);
        framed.Add(0);
        if (!_server.HandshakeComplete)
        {
            await WritePacketAsync(PlusLoopbackWire.Join(WireFixture7, framed.ToArray()), cancellationToken);
            return;
        }

        var plaintext = framed.ToArray();
        _tls.WriteApplicationData(plaintext, 0, plaintext.Length);
        await FlushAsync(cancellationToken);
    }

    /// <summary>Calculates the response integrity from the actual request sequence.</summary>
    /// <param name="requestIntegrity">The independently decoded request integrity counter.</param>
    /// <returns>The expected response integrity.</returns>
    internal uint Integrity(uint requestIntegrity) => (uint)((_request[WireValue7] << WireValue8) | _request[WireValue8]) + requestIntegrity;

    /// <summary>Checks the fixed request header independently of production decoding.</summary>
    /// <param name="function">The expected function.</param>
    /// <param name="session">The expected session.</param>
    /// <param name="flags">The expected flags.</param>
    /// <returns>A task representing the assertions.</returns>
    private async Task VerifyRequestAsync(ushort function, uint session, byte flags)
    {
        _expectedSequence++;
        await TAssert.That(_version).IsEqualTo(function is WireValue1459 or CreateFunction ? (byte)1 : (byte)WireValue2);
        await TAssert.That(_request[0]).IsEqualTo((byte)WireValue49);
        await TAssert.That((_request[WireValue3] << WireValue8) | _request[WireValue4]).IsEqualTo((int)function);
        await TAssert.That((_request[WireValue7] << WireValue8) | _request[WireValue8]).IsEqualTo((int)_expectedSequence);
        await TAssert.That(_request[1] | _request[WireValue2] | _request[ReservedOctetIndex] | _request[WireValue6]).IsEqualTo(0);
        var position = WireValue9;
        await TAssert.That(PlusLoopbackWire.Fixed(_request, ref position)).IsEqualTo(session);
        await TAssert.That(_request[WireValue13]).IsEqualTo(flags);
    }

    /// <summary>Feeds one independently framed ciphertext packet into TLS.</summary>
    /// <param name="cancellationToken">Bounds the receive.</param>
    /// <returns>A task representing record processing.</returns>
    private async Task FeedAsync(CancellationToken cancellationToken)
    {
        var dt = await ReadPacketAsync(cancellationToken);
        await TAssert.That(PlusLoopbackWire.Equal(PlusLoopbackWire.Slice(dt, 0, WireValue3), WireFixture7)).IsTrue();
        _tls.OfferInput(dt, WireValue3, dt.Length - WireValue3);
        var available = _tls.GetAvailableInputBytes();
        if (available > 0)
        {
            var bytes = new byte[available];
            var read = _tls.ReadInput(bytes, 0, bytes.Length);
            for (var index = 0; index < read; index++)
            {
                _plaintext.Enqueue(bytes[index]);
            }
        }

        await FlushAsync(cancellationToken);
    }

    /// <summary>Fragments outgoing TLS records into negotiated COTP DT sizes.</summary>
    /// <param name="cancellationToken">Bounds the send.</param>
    /// <returns>A task representing the send.</returns>
    private async Task FlushAsync(CancellationToken cancellationToken)
    {
        var bytes = new byte[_tls.GetAvailableOutputBytes()];
        _ = _tls.ReadOutput(bytes, 0, bytes.Length);
        for (var offset = 0; offset < bytes.Length; offset += WireValue509)
        {
            var length = Math.Min(WireValue509, bytes.Length - offset);
            await WritePacketAsync(PlusLoopbackWire.Join(WireFixture7, PlusLoopbackWire.Slice(bytes, offset, length)), cancellationToken);
        }
    }

    /// <summary>Reads an exact plaintext length across TLS records.</summary>
    /// <param name="length">The required length.</param>
    /// <param name="cancellationToken">Bounds the receive.</param>
    /// <returns>The decrypted bytes.</returns>
    private async Task<byte[]> ReadPlainAsync(int length, CancellationToken cancellationToken)
    {
        while (_plaintext.Count < length)
        {
            await FeedAsync(cancellationToken);
        }

        var result = new byte[length];
        for (var index = 0; index < length; index++)
        {
            result[index] = _plaintext.Dequeue();
        }

        return result;
    }

    /// <summary>Reads and validates a complete TPKT frame.</summary>
    /// <param name="cancellationToken">Bounds the receive.</param>
    /// <returns>The COTP packet.</returns>
    private async Task<byte[]> ReadPacketAsync(CancellationToken cancellationToken)
    {
        var header = await ReadExactAsync(WireValue4, cancellationToken);
        await TAssert.That(header[0]).IsEqualTo((byte)WireValue3);
        await TAssert.That(header[1]).IsEqualTo((byte)0);
        return await ReadExactAsync(((header[WireValue2] << WireValue8) | header[WireValue3]) - WireValue4, cancellationToken);
    }

    /// <summary>Reads bytes while detecting premature socket closure.</summary>
    /// <param name="length">The required length.</param>
    /// <param name="cancellationToken">Bounds the receive.</param>
    /// <returns>The received bytes.</returns>
    private async Task<byte[]> ReadExactAsync(int length, CancellationToken cancellationToken)
    {
        var bytes = new byte[length];
        for (var offset = 0; offset < length;)
        {
#if NET6_0_OR_GREATER
            var read = await _stream!.ReadAsync(bytes.AsMemory(offset, length - offset), cancellationToken);
#else
            var read = await _stream!.ReadAsync(bytes, offset, length - offset, cancellationToken);
#endif
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            offset += read;
        }

        return bytes;
    }

    /// <summary>Writes an independently constructed TPKT frame.</summary>
    /// <param name="cotp">The COTP packet.</param>
    /// <param name="cancellationToken">Bounds the send.</param>
    /// <returns>A task representing the send.</returns>
    private async Task WritePacketAsync(byte[] cotp, CancellationToken cancellationToken)
    {
        var length = cotp.Length + WireValue4;
        byte[] header = [WireValue3, 0, (byte)(length >> WireValue8), (byte)length];
        var bytes = PlusLoopbackWire.Join(header, cotp);
#if NET6_0_OR_GREATER
        await _stream!.WriteAsync(bytes.AsMemory(), cancellationToken);
#else
        await _stream!.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
#endif
    }
}
