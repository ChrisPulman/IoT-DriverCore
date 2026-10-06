// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;
using System.Net.Sockets;
using Org.BouncyCastle.Tls;

#if REACTIVE_SHIM
using IoT.Driver.S7PlcRx.Reactive.Symbolic;
using IoT.Driver.S7PlcRx.Reactive.Symbolic.Protocol;

namespace IoT.Driver.S7PlcRx.Reactive.Symbolic.Transport;
#else
using IoT.Driver.S7PlcRx.Symbolic;
using IoT.Driver.S7PlcRx.Symbolic.Protocol;

namespace IoT.Driver.S7PlcRx.Symbolic.Transport;
#endif

/// <summary>Provides s7plus transport.</summary>
internal sealed class S7PlusTransport : IS7PlusTransport
{
    /// <summary>Stores the default tpdu size.</summary>
    private const int DefaultTpduSize = 1024;

    /// <summary>Stores the cotp data header length.</summary>
    private const int CotpDataHeaderLength = 3;

    /// <summary>Stores the connection request length.</summary>
    private const int ConnectionRequestLength = 36;

    /// <summary>Stores the connection request indicator.</summary>
    private const int ConnectionRequestIndicator = 31;

    /// <summary>Stores the connection request code.</summary>
    private const int ConnectionRequestCode = 0xe0;

    /// <summary>Stores the tpdu size parameter.</summary>
    private const int TpduSizeParameter = 0xc0;

    /// <summary>Stores the requested tpdu exponent.</summary>
    private const int RequestedTpduExponent = 10;

    /// <summary>Stores the source tsap parameter.</summary>
    private const int SourceTsapParameter = 0xc1;

    /// <summary>Stores the word byte count.</summary>
    private const int WordByteCount = 2;

    /// <summary>Stores the cotp class offset.</summary>
    private const int CotpClassOffset = 6;

    /// <summary>Stores the destination tsap parameter.</summary>
    private const int DestinationTsapParameter = 0xc2;

    /// <summary>Stores the remote tsap length.</summary>
    private const int RemoteTsapLength = 16;

    /// <summary>Stores the cotp connection header length.</summary>
    private const int CotpConnectionHeaderLength = 7;

    /// <summary>Stores the connection confirm code.</summary>
    private const int ConnectionConfirmCode = 0xd0;

    /// <summary>Stores the maximum port.</summary>
    private const int MaximumPort = 65_535;

    /// <summary>Stores the system event version.</summary>
    private const int SystemEventVersion = 0xfe;

    /// <summary>Stores the maximum fragment payload.</summary>
    private const int MaximumFragmentPayload = 986;

    /// <summary>Stores the complete frame overhead.</summary>
    private const int CompleteFrameOverhead = 11;

    /// <summary>Stores the plus header length.</summary>
    private const int PlusHeaderLength = 4;

    /// <summary>Stores the plus marker.</summary>
    private const int PlusMarker = 0x72;

    /// <summary>Stores the bits per byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>Stores the default timeout seconds.</summary>
    private const int DefaultTimeoutSeconds = 5;

    /// <summary>Stores the data tpdu code.</summary>
    private const int DataTpduCode = 0xf0;

    /// <summary>Stores the end of tpdu mask.</summary>
    private const int EndOfTpduMask = 0x80;

    /// <summary>Stores the tpdu number mask.</summary>
    private const int TpduNumberMask = 0x7f;

    /// <summary>Stores the maximum message.</summary>
    private const int MaximumMessage = 16 * 1024 * 1024;

    /// <summary>Stores the options.</summary>
    private readonly S7SymbolicConnectionOptions _options;

    /// <summary>Stores the tls gate.</summary>
    private readonly Lock _tlsGate = new();

    /// <summary>Stores the write gate.</summary>
    private readonly S7OperationGate _writeGate;

    /// <summary>Stores the receive gate.</summary>
    private readonly S7OperationGate _receiveGate;

    /// <summary>Protects the shared disposal task.</summary>
    private readonly Lock _disposeGate = new();

    /// <summary>Stores the lifetime.</summary>
    private readonly CancellationTokenSource _lifetime = new();

    /// <summary>Stores the plaintext.</summary>
    private readonly Queue<byte> _plaintext = new();

    /// <summary>Stores the socket.</summary>
    private TcpClient? _socket;

    /// <summary>Stores the stream.</summary>
    private Stream? _stream;

    /// <summary>Stores the tls.</summary>
    private TlsClientProtocol? _tls;

    /// <summary>Stores the tls client.</summary>
    private S7TlsClient? _tlsClient;

    /// <summary>Stores the tpdu size.</summary>
    private int _tpduSize = DefaultTpduSize;

    /// <summary>Stores the disposed.</summary>
    private int _disposed;

    /// <summary>Stores the shared disposal operation.</summary>
    private Task? _disposeTask;

    /// <summary>Initializes a new instance of the <see cref="S7PlusTransport"/> class.</summary>
    /// <param name="options">The options.</param>
    internal S7PlusTransport(S7SymbolicConnectionOptions options)
        : this(options, null, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="S7PlusTransport"/> class with controlled operation gates.</summary>
    /// <param name="options">The connection options.</param>
    /// <param name="writeGate">The gate for concurrent write operations.</param>
    /// <param name="receiveGate">The gate for concurrent receive operations.</param>
    internal S7PlusTransport(S7SymbolicConnectionOptions options, S7OperationGate? writeGate, S7OperationGate? receiveGate)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        options.Validate();
        _writeGate = writeGate ?? new();
        _receiveGate = receiveGate ?? new();
    }

    /// <summary>Connect async.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        using var receiveLease = await _receiveGate.AcquireAsync(cancellationToken).ConfigureAwait(false);
        using var writeLease = await _writeGate.AcquireAsync(cancellationToken).ConfigureAwait(false);
        if (_socket is not null)
        {
            throw new InvalidOperationException("Transport is already connected.");
        }

        using var timeout = OperationToken(cancellationToken);
        TcpClient socket = new();
        _socket = socket;

#if NET8_0_OR_GREATER
        await using var registration = RegisterCancellation(socket, timeout.Token);
#else
        using var registration = RegisterCancellation(socket, timeout.Token);
#endif
        try
        {
            await socket.ConnectAsync(_options.Host, _options.Port).ConfigureAwait(false);
            timeout.Token.ThrowIfCancellationRequested();
            socket.NoDelay = true;
            _stream = socket.GetStream();
            var request = ConnectionRequest();
            await WriteBytesAsync(_stream, request, timeout.Token).ConfigureAwait(false);
            _tpduSize = ValidateConnectionConfirm(await ReadTpktAsync(timeout.Token).ConfigureAwait(false));
        }
        catch (Exception)when (timeout.IsCancellationRequested)
        {
            _socket = null;
            _stream = null;
            throw new OperationCanceledException(timeout.Token);
        }
        catch
        {
            socket.Close();
            _socket = null;
            _stream = null;
            throw;
        }
    }

    /// <summary>Upgrade to tls async.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    public async Task UpgradeToTlsAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        using var timeout = OperationToken(cancellationToken);
        using var receiveLease = await _receiveGate.AcquireAsync(timeout.Token).ConfigureAwait(false);

#if NET8_0_OR_GREATER
        await using var cancellation = RegisterCancellation(_socket, timeout.Token);
#else
        using var cancellation = RegisterCancellation(_socket, timeout.Token);
#endif
        _tlsClient = new(_options);
        lock (_tlsGate)
        {
            if (_tls is not null)
            {
                throw new InvalidOperationException("TLS is already active.");
            }

            _tls = new();
            _tls.Connect(_tlsClient);
        }

        try
        {
            await FlushTlsAsync(timeout.Token).ConfigureAwait(false);
            while (!_tlsClient.HandshakeComplete)
            {
                await FeedTlsAsync(timeout.Token).ConfigureAwait(false);
            }
        }
        catch (Exception)when (timeout.IsCancellationRequested)
        {
            throw new OperationCanceledException(timeout.Token);
        }
    }

    /// <summary>Export keying material.</summary>
    /// <param name="label">The label.</param>
    /// <param name="length">The length.</param>
    /// <returns>The operation result.</returns>
    public byte[] ExportKeyingMaterial(string label, int length)
    {
        ThrowIfDisposed();
        if (string.IsNullOrEmpty(label) || length <= 0 || length > MaximumPort)
        {
            throw new ArgumentException("Exporter label and length must be valid.");
        }

        lock (_tlsGate)
        {
            return _tlsClient?.Export(label, length) ?? throw new InvalidOperationException("TLS is not active.");
        }
    }

    /// <summary>Send async.</summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    public async Task SendAsync(PlusMessage message, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (message.Payload.Length > MaximumMessage)
        {
            throw new IOException("S7Plus message exceeds the transport limit.");
        }

        if (message.Version == SystemEventVersion && message.Payload.Length > Math.Min(MaximumFragmentPayload, _tpduSize - CompleteFrameOverhead))
        {
            throw new IOException("System events cannot be fragmented.");
        }

        using var timeout = OperationToken(cancellationToken);

#if NET8_0_OR_GREATER
        await using var cancellation = RegisterCancellation(_socket, timeout.Token);
#else
        using var cancellation = RegisterCancellation(_socket, timeout.Token);
#endif
        using var writeLease = await _writeGate.AcquireAsync(timeout.Token).ConfigureAwait(false);
        try
        {
            var offset = 0;
            do
            {
                var count = Math.Min(Math.Min(MaximumFragmentPayload, _tpduSize - CompleteFrameOverhead), message.Payload.Length - offset);
                var final = offset + count == message.Payload.Length;
                var fragment = new byte[PlusHeaderLength + count + (final && message.Version != SystemEventVersion ? PlusHeaderLength : 0)];
                fragment[0] = PlusMarker;
                fragment[1] = message.Version;
                fragment[WordByteCount] = (byte)(count >> BitsPerByte);
                fragment[CotpDataHeaderLength] = (byte)count;
                Buffer.BlockCopy(message.Payload, offset, fragment, PlusHeaderLength, count);
                if (final && message.Version != SystemEventVersion)
                {
                    fragment[PlusHeaderLength + count] = PlusMarker;
                    fragment[DefaultTimeoutSeconds + count] = message.Version;
                }

                if (_tls is null)
                {
                    await WriteDtAsync(fragment, timeout.Token).ConfigureAwait(false);
                }
                else
                {
                    byte[] encrypted;
                    lock (_tlsGate)
                    {
                        _tls.WriteApplicationData(fragment, 0, fragment.Length);
                        encrypted = DrainTlsOutput();
                    }

                    await WriteCiphertextAsync(encrypted, timeout.Token).ConfigureAwait(false);
                }

                offset += count;
            }
            while (offset < message.Payload.Length);
        }
        catch (Exception)when (timeout.IsCancellationRequested)
        {
            throw new OperationCanceledException(timeout.Token);
        }
    }

    /// <summary>Receive async.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    public async Task<PlusMessage> ReceiveAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);

#if NET8_0_OR_GREATER
        await using var cancellation = RegisterCancellation(_socket, timeout.Token);
#else
        using var cancellation = RegisterCancellation(_socket, timeout.Token);
#endif
        using var receiveLease = await _receiveGate.AcquireAsync(timeout.Token).ConfigureAwait(false);
        try
        {
#if NET8_0_OR_GREATER
            await using MemoryStream body = new();
#else
            using MemoryStream body = new();
#endif
            var header = await ReadPlainAsync(PlusHeaderLength, timeout.Token).ConfigureAwait(false);
            var version = header[1];
            while (true)
            {
                var length = ValidateFragmentHeader(header, version);
                if (length == 0 && version != SystemEventVersion)
                {
                    if (body.Length == 0)
                    {
                        throw new IOException("S7Plus message has no body.");
                    }

                    return new(version, body.ToArray());
                }

                if (body.Length + length > MaximumMessage)
                {
                    throw new IOException("S7Plus message exceeds the transport limit.");
                }

                var fragment = await ReadPlainAsync(length, timeout.Token).ConfigureAwait(false);
                body.Write(fragment, 0, fragment.Length);
                if (version == SystemEventVersion)
                {
                    return new(version, body.ToArray());
                }

                header = await ReadPlainAsync(PlusHeaderLength, timeout.Token).ConfigureAwait(false);
            }
        }
        catch (Exception)when (timeout.IsCancellationRequested)
        {
            throw new OperationCanceledException(timeout.Token);
        }
    }

    /// <summary>Dispose async.</summary>
    /// <returns>The operation result.</returns>
    public async ValueTask DisposeAsync()
    {
        Task disposeTask;
        lock (_disposeGate)
        {
            _disposeTask ??= DisposeCoreAsync();
            disposeTask = _disposeTask;
        }

        await disposeTask.ConfigureAwait(false);
    }

    /// <summary>Disconnect async.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        _socket?.Close();
        using var receiveLease = await _receiveGate.AcquireAsync(cancellationToken).ConfigureAwait(false);
        using var writeLease = await _writeGate.AcquireAsync(cancellationToken).ConfigureAwait(false);
        _stream?.Dispose();
        _stream = null;
        _socket = null;
        lock (_tlsGate)
        {
            _tlsClient?.ClearExporter();
            _tls = null;
            _tlsClient = null;
            _plaintext.Clear();
        }

        _tpduSize = DefaultTpduSize;
    }

    /// <summary>Connection request.</summary>
    /// <returns>The operation result.</returns>
    internal static byte[] ConnectionRequest()
    {
        byte[] header =
            [CotpDataHeaderLength, 0, 0, ConnectionRequestLength, ConnectionRequestIndicator,
             ConnectionRequestCode, 0, 0, 0, 1, 0, TpduSizeParameter, 1, RequestedTpduExponent,
             SourceTsapParameter, WordByteCount, CotpClassOffset, 0, DestinationTsapParameter, RemoteTsapLength];
        var request = new byte[ConnectionRequestLength];
        Buffer.BlockCopy(header, 0, request, 0, header.Length);
        var remoteTsap = "SIMATIC-ROOT-HMI"u8.ToArray();
        Buffer.BlockCopy(remoteTsap, 0, request, header.Length, remoteTsap.Length);
        return request;
    }

    /// <summary>Validate connection confirm.</summary>
    /// <param name="cotp">The cotp.</param>
    /// <returns>The operation result.</returns>
    internal static int ValidateConnectionConfirm(byte[] cotp)
    {
        if (!IsValidConfirmHeader(cotp))
        {
            throw new IOException("Invalid COTP connection confirmation.");
        }

        var size = DefaultTpduSize;
        for (var offset = CotpConnectionHeaderLength; offset < cotp.Length;)
        {
            if (offset + WordByteCount > cotp.Length || offset + WordByteCount + cotp[offset + 1] > cotp.Length)
            {
                throw new IOException("Truncated COTP parameter.");
            }

            var length = cotp[offset + 1];
            if (cotp[offset] == TpduSizeParameter)
            {
                if (length != 1 || cotp[offset + WordByteCount] is < CotpConnectionHeaderLength or > RequestedTpduExponent)
                {
                    throw new IOException("Invalid negotiated TPDU size.");
                }

                size = 1 << cotp[offset + WordByteCount];
            }

            offset += WordByteCount + length;
        }

        return size;
    }

    /// <summary>Validate confirm header.</summary>
    /// <param name="cotp">The cotp.</param>
    /// <returns>Whether the fixed confirmation header is valid.</returns>
    private static bool IsValidConfirmHeader(byte[] cotp) =>
        cotp.Length >= CotpConnectionHeaderLength && cotp[0] + 1 == cotp.Length &&
        cotp[1] == ConnectionConfirmCode && cotp[WordByteCount] == 0 &&
        cotp[CotpDataHeaderLength] == 1 && cotp[CotpClassOffset] == 0;

    /// <summary>Register cancellation.</summary>
    /// <param name="socket">The socket.</param>
    /// <param name="token">The token.</param>
    /// <returns>The operation result.</returns>
    private static CancellationTokenRegistration RegisterCancellation(TcpClient? socket, CancellationToken token) =>
        token.Register(static state => ((TcpClient?)state)?.Close(), socket);

    /// <summary>Validate fragment header.</summary>
    /// <param name="header">The header.</param>
    /// <param name="version">The version.</param>
    /// <returns>The operation result.</returns>
    private static int ValidateFragmentHeader(byte[] header, byte version)
    {
        if (header[0] != PlusMarker || header[1] != version || version is not (1 or WordByteCount or CotpDataHeaderLength or SystemEventVersion))
        {
            throw new IOException("Invalid S7Plus fragment header.");
        }

        return (header[WordByteCount] << BitsPerByte) | header[CotpDataHeaderLength];
    }

    /// <summary>Write bytes async.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="bytes">The bytes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    private static Task WriteBytesAsync(Stream stream, byte[] bytes, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        return stream.WriteAsync(bytes.AsMemory(), cancellationToken).AsTask();
#else
        return stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
#endif
    }

    /// <summary>Read bytes async.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="bytes">The bytes.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    private static Task<int> ReadBytesAsync(Stream stream, byte[] bytes, int offset, int length, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        return stream.ReadAsync(bytes.AsMemory(offset, length), cancellationToken).AsTask();
#else
        return stream.ReadAsync(bytes, offset, length, cancellationToken);
#endif
    }

    /// <summary>Closes the transport and drains admitted operations.</summary>
    /// <returns>A task completing after transport resources are released.</returns>
    private async Task DisposeCoreAsync()
    {
        _ = Interlocked.Exchange(ref _disposed, 1);

        var receiveShutdown = _receiveGate.DisposeAsync().AsTask();
        var writeShutdown = _writeGate.DisposeAsync().AsTask();

#if NET8_0_OR_GREATER
        await _lifetime.CancelAsync().ConfigureAwait(false);
#else
        _lifetime.Cancel();
#endif
        _socket?.Close();
        await Task.WhenAll(receiveShutdown, writeShutdown).ConfigureAwait(false);
        _stream?.Dispose();
        lock (_tlsGate)
        {
            _tlsClient?.ClearExporter();
            _tls = null;
            _tlsClient = null;
            _plaintext.Clear();
        }

        _lifetime.Dispose();
    }

    /// <summary>Operation token.</summary>
    /// <param name="caller">The caller.</param>
    /// <returns>The operation result.</returns>
    private CancellationTokenSource OperationToken(CancellationToken caller)
    {
        var token = CancellationTokenSource.CreateLinkedTokenSource(caller, _lifetime.Token);
        token.CancelAfter(_options.Timeout);
        return token;
    }

    /// <summary>Throw if disposed.</summary>
    private void ThrowIfDisposed()
    {
#if NETFRAMEWORK
        if (Volatile.Read(ref _disposed) == 0)
        {
            return;
        }

        throw new ObjectDisposedException(nameof(S7PlusTransport));
#else
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
#endif
    }

    /// <summary>Read plain async.</summary>
    /// <param name="length">The length.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    private async Task<byte[]> ReadPlainAsync(int length, CancellationToken cancellationToken)
    {
        while (_plaintext.Count < length)
        {
            if (_tls is null)
            {
                foreach (var item in await ReadDtAsync(cancellationToken).ConfigureAwait(false))
                {
                    _plaintext.Enqueue(item);
                }
            }
            else
            {
                await FeedTlsAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        var bytes = new byte[length];
        for (var index = 0; index < length; index++)
        {
            bytes[index] = _plaintext.Dequeue();
        }

        return bytes;
    }

    /// <summary>Feed tls async.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    private async Task FeedTlsAsync(CancellationToken cancellationToken)
    {
        var input = await ReadDtAsync(cancellationToken).ConfigureAwait(false);
        lock (_tlsGate)
        {
            _tls!.OfferInput(input);
            var available = _tls.GetAvailableInputBytes();
            if (available > 0)
            {
                var bytes = new byte[available];
                var read = _tls.ReadInput(bytes, 0, available);
                for (var index = 0; index < read; index++)
                {
                    _plaintext.Enqueue(bytes[index]);
                }
            }
        }

        await FlushTlsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Flush tls async.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    private async Task FlushTlsAsync(CancellationToken cancellationToken)
    {
        using var writeLease = await _writeGate.AcquireAsync(cancellationToken).ConfigureAwait(false);
        byte[] bytes;
        lock (_tlsGate)
        {
            bytes = DrainTlsOutput();
        }

        await WriteCiphertextAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Drain tls output.</summary>
    /// <returns>The operation result.</returns>
    private byte[] DrainTlsOutput()
    {
        var bytes = new byte[_tls!.GetAvailableOutputBytes()];
        _ = _tls.ReadOutput(bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>Write ciphertext async.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    private async Task WriteCiphertextAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        for (var offset = 0; offset < bytes.Length;)
        {
            var count = Math.Min(_tpduSize - CotpDataHeaderLength, bytes.Length - offset);
            var part = new byte[count];
            Buffer.BlockCopy(bytes, offset, part, 0, count);
            await WriteDtAsync(part, cancellationToken).ConfigureAwait(false);
            offset += count;
        }
    }

    /// <summary>Write dt async.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    private async Task WriteDtAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        var packet = new byte[bytes.Length + CotpConnectionHeaderLength];
        packet[0] = CotpDataHeaderLength;
        packet[WordByteCount] = (byte)(packet.Length >> BitsPerByte);
        packet[CotpDataHeaderLength] = (byte)packet.Length;
        packet[PlusHeaderLength] = WordByteCount;
        packet[DefaultTimeoutSeconds] = DataTpduCode;
        packet[CotpClassOffset] = EndOfTpduMask;
        Buffer.BlockCopy(bytes, 0, packet, CotpConnectionHeaderLength, bytes.Length);
        await WriteBytesAsync(_stream ?? throw new InvalidOperationException("Transport is not connected."), packet, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Read dt async.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    private async Task<byte[]> ReadDtAsync(CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        await using MemoryStream assembled = new();
#else
        using MemoryStream assembled = new();
#endif
        bool final;
        do
        {
            var packet = await ReadTpktAsync(cancellationToken).ConfigureAwait(false);
            if (packet.Length < CotpDataHeaderLength || packet[0] != WordByteCount || packet[1] != DataTpduCode || (packet[WordByteCount] & TpduNumberMask) != 0 || packet.Length > _tpduSize)
            {
                throw new IOException("Invalid COTP data packet.");
            }

            assembled.Write(packet, CotpDataHeaderLength, packet.Length - CotpDataHeaderLength);
            if (assembled.Length > MaximumMessage)
            {
                throw new IOException("COTP data exceeds transport limit.");
            }

            final = (packet[WordByteCount] & EndOfTpduMask) != 0;
        }
        while (!final);
        return assembled.ToArray();
    }

    /// <summary>Read tpkt async.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    private async Task<byte[]> ReadTpktAsync(CancellationToken cancellationToken)
    {
        var header = await ReadExactlyAsync(PlusHeaderLength, cancellationToken).ConfigureAwait(false);
        var length = (header[WordByteCount] << BitsPerByte) | header[CotpDataHeaderLength];
        if (header[0] != CotpDataHeaderLength || header[1] != 0 || length < CotpConnectionHeaderLength)
        {
            throw new IOException("Invalid TPKT header.");
        }

        return await ReadExactlyAsync(length - PlusHeaderLength, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Read exactly async.</summary>
    /// <param name="length">The length.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    private async Task<byte[]> ReadExactlyAsync(int length, CancellationToken cancellationToken)
    {
        var stream = _stream ?? throw new InvalidOperationException("Transport is not connected.");
        var bytes = new byte[length];
        for (var offset = 0; offset < length;)
        {
            var count = await ReadBytesAsync(stream, bytes, offset, length - offset, cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                throw new EndOfStreamException("Controller closed the transport.");
            }

            offset += count;
        }

        return bytes;
    }
}
