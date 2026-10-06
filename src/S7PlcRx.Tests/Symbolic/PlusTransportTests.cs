// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using IoT.Driver.S7PlcRx.Symbolic;
using IoT.Driver.S7PlcRx.Symbolic.Protocol;
using IoT.Driver.S7PlcRx.Symbolic.Transport;
using TAssert = TUnit.Assertions.Assert;

namespace IoT.Driver.S7PlcRx.Tests.Symbolic;

/// <summary>Provides plus transport tests.</summary>
public sealed class PlusTransportTests
{
    /// <summary>Stores the connection request length.</summary>
    private const int ConnectionRequestLength = 36;

    /// <summary>Stores the cotp data header length.</summary>
    private const int CotpDataHeaderLength = 3;

    /// <summary>Stores the plus header length.</summary>
    private const int PlusHeaderLength = 4;

    /// <summary>Stores the connection request indicator.</summary>
    private const int ConnectionRequestIndicator = 31;

    /// <summary>Stores the remote tsap offset.</summary>
    private const int RemoteTsapOffset = 20;

    /// <summary>Stores the remote tsap length.</summary>
    private const int RemoteTsapLength = 16;

    /// <summary>Stores the size parameter offset.</summary>
    private const int SizeParameterOffset = 9;

    /// <summary>Stores the connection confirm code.</summary>
    private const int ConnectionConfirmCode = 0xd0;

    /// <summary>Stores the word byte count.</summary>
    private const int WordByteCount = 2;

    /// <summary>Stores the tpdu size parameter.</summary>
    private const int TpduSizeParameter = 0xc0;

    /// <summary>Stores the negotiated tpdu size.</summary>
    private const int NegotiatedTpduSize = 512;

    /// <summary>Stores the bits per byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>Stores the certificate rsa key size.</summary>
    private const int CertificateRsaKeySize = 2048;

    /// <summary>Stores the certificate pin hex length.</summary>
    private const int CertificatePinHexLength = 64;

    /// <summary>Stores the cotp request length.</summary>
    private const int CotpRequestLength = 32;

    /// <summary>Stores the requested tpdu exponent.</summary>
    private const int RequestedTpduExponent = 10;

    /// <summary>Stores the fragmented payload length.</summary>
    private const int FragmentedPayloadLength = 2000;

    /// <summary>Stores the fragment tpdu length.</summary>
    private const int FragmentTpduLength = 993;

    /// <summary>Stores the plus marker.</summary>
    private const int PlusMarker = 0x72;

    /// <summary>Stores the cotp class offset.</summary>
    private const int CotpClassOffset = 6;

    /// <summary>The port used for disconnected transport lifecycle tests.</summary>
    private const int LifecycleTestPort = 1;

    /// <summary>Stores the certificate test host.</summary>
    private const string CertificateHost = "localhost";

    /// <summary>Connection request uses complete root tsap.</summary>
    /// <returns>The operation result.</returns>
    [Test]
    public async Task ConnectionRequestUsesCompleteRootTsap()
    {
        var request = S7PlusTransport.ConnectionRequest();
        await TAssert.That(request.Length).IsEqualTo(ConnectionRequestLength);
        await TAssert.That(request[CotpDataHeaderLength]).IsEqualTo((byte)ConnectionRequestLength);
        await TAssert.That(request[PlusHeaderLength]).IsEqualTo((byte)ConnectionRequestIndicator);
        await TAssert.That(System.Text.Encoding.ASCII.GetString(request, RemoteTsapOffset, RemoteTsapLength))
            .IsEqualTo("SIMATIC-ROOT-HMI");
    }

    /// <summary>Confirmation negotiates size and rejects truncation.</summary>
    /// <returns>The operation result.</returns>
    [Test]
    public async Task ConfirmationNegotiatesSizeAndRejectsTruncation()
    {
        await TAssert.That(S7PlusTransport.ValidateConnectionConfirm(
            [SizeParameterOffset, ConnectionConfirmCode, 0, 1, 0, WordByteCount, 0, TpduSizeParameter, 1, SizeParameterOffset]))
            .IsEqualTo(NegotiatedTpduSize);
        await TAssert.That(static () => S7PlusTransport.ValidateConnectionConfirm(
            [BitsPerByte, ConnectionConfirmCode, 0, 1, 0, WordByteCount, 0, TpduSizeParameter, 1]))
            .Throws<IOException>();
    }

    /// <summary>Options reject malformed certificate pin.</summary>
    /// <returns>The operation result.</returns>
    [Test]
    public async Task OptionsRejectMalformedCertificatePin()
    {
        await TAssert.That(static () => new S7PlusTransport(CreateCertificateOptions("00")))
            .Throws<ArgumentException>();
    }

    /// <summary>Certificate policy rejects untrusted and wrong pins.</summary>
    /// <returns>The operation result.</returns>
    [Test]
    public async Task CertificatePolicyRejectsUntrustedAndWrongPins()
    {
        Org.BouncyCastle.Crypto.Generators.RsaKeyPairGenerator keys = new();
        Org.BouncyCastle.Security.SecureRandom random = new();
        keys.Init(new(random, CertificateRsaKeySize));
        var pair = keys.GenerateKeyPair();
        Org.BouncyCastle.X509.X509V3CertificateGenerator generator = new();
        generator.SetSerialNumber(Org.BouncyCastle.Math.BigInteger.One);
        Org.BouncyCastle.Asn1.X509.X509Name name = new("CN=localhost");
        generator.SetIssuerDN(name);
        generator.SetSubjectDN(name);
        generator.SetNotBefore(TimeProvider.System.GetUtcNow().UtcDateTime.AddMinutes(-1));
        generator.SetNotAfter(TimeProvider.System.GetUtcNow().UtcDateTime.AddHours(1));
        generator.SetPublicKey(pair.Public);
        var encoded = generator.Generate(
            new Org.BouncyCastle.Crypto.Operators.Asn1SignatureFactory("SHA256WITHRSA", pair.Private)).GetEncoded();
#if NET9_0_OR_GREATER
        using var certificate = X509CertificateLoader.LoadCertificate(encoded);
#else
        using X509Certificate2 certificate = new(encoded);
#endif
        await TAssert.That(S7TlsClient.ValidateCertificate(certificate, [], CreateCertificateOptions())).IsFalse();
        await TAssert.That(S7TlsClient.ValidateCertificate(
            certificate,
            [],
            CreateCertificateOptions(new('0', CertificatePinHexLength))))
            .IsFalse();
        var pin = Convert.ToHexString(SHA256.HashData(encoded));
        await TAssert.That(S7TlsClient.ValidateCertificate(certificate, [], CreateCertificateOptions(pin))).IsTrue();
        await TAssert.That(S7TlsClient.MatchesHost(certificate, "other-controller")).IsFalse();
        await TAssert.That(S7TlsClient.ValidateCertificate(
            certificate,
            [],
            CreateCertificateOptions(validation: static _ => true)))
            .IsTrue();
    }

    /// <summary>Loopback fragments and reassembles messages.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    [Test]
    public async Task LoopbackFragmentsAndReassemblesMessages(CancellationToken cancellationToken)
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        using var listenerLifetime = NetworkCompatibility.StopOnDispose(listener);
        listener.Start();
        try
        {
            await using S7PlusTransport transport = new(CreateLoopbackOptions(((IPEndPoint)listener.LocalEndpoint).Port));
            var connecting = transport.ConnectAsync(cancellationToken);
#if NET6_0_OR_GREATER
            using var peer = await listener.AcceptTcpClientAsync(cancellationToken);
#else
            using var acceptRegistration = cancellationToken.Register(static state => ((TcpListener)state!).Stop(), listener);
            using var peer = await listener.AcceptTcpClientAsync();
#endif
            var stream = peer.GetStream();
            var request = await ReadPacket(stream, cancellationToken);
            await TAssert.That(request.Length).IsEqualTo(CotpRequestLength);
            await SendPacket(stream, [SizeParameterOffset, ConnectionConfirmCode, 0, 1, 0, WordByteCount, 0, TpduSizeParameter, 1, RequestedTpduExponent], cancellationToken);
            await connecting;
            var payload = new byte[FragmentedPayloadLength];
            for (var i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)i;
            }

            var sending = transport.SendAsync(new(CotpDataHeaderLength, payload), cancellationToken);
            var first = await ReadPacket(stream, cancellationToken);
            var second = await ReadPacket(stream, cancellationToken);
            var final = await ReadPacket(stream, cancellationToken);
            await sending;
            await TAssert.That(first.Length).IsEqualTo(FragmentTpduLength);
            await TAssert.That(second.Length).IsEqualTo(FragmentTpduLength);
            await TAssert.That(final[^PlusHeaderLength]).IsEqualTo((byte)PlusMarker);
            await SendPacket(stream, first, cancellationToken);
            await SendPacket(stream, second, cancellationToken);
            await SendPacket(stream, final, cancellationToken);
            var response = await transport.ReceiveAsync(cancellationToken);
            await TAssert.That(response.Version).IsEqualTo((byte)CotpDataHeaderLength);
            await TAssert.That(response.Payload.SequenceEqual(payload)).IsTrue();
            await transport.DisconnectAsync(cancellationToken);
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>Loopback blocked receive honors cancellation.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    [Test]
    public async Task LoopbackBlockedReceiveHonorsCancellation(CancellationToken cancellationToken)
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        using var listenerLifetime = NetworkCompatibility.StopOnDispose(listener);
        listener.Start();
        try
        {
            await using S7PlusTransport transport = new(CreateLoopbackOptions(((IPEndPoint)listener.LocalEndpoint).Port));
            var connecting = transport.ConnectAsync(cancellationToken);
#if NET6_0_OR_GREATER
            using var peer = await listener.AcceptTcpClientAsync(cancellationToken);
#else
            using var acceptRegistration = cancellationToken.Register(static state => ((TcpListener)state!).Stop(), listener);
            using var peer = await listener.AcceptTcpClientAsync();
#endif
            var stream = peer.GetStream();
            await ReadPacket(stream, cancellationToken);
            await SendPacket(stream, [CotpClassOffset, ConnectionConfirmCode, 0, 1, 0, WordByteCount, 0], cancellationToken);
            await connecting;
            using CancellationTokenSource cancel = new();
            var receiving = transport.ReceiveAsync(cancel.Token);
            await cancel.CancelAsync();
            await TAssert.That(async () => await receiving).Throws<OperationCanceledException>();
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>Disconnect waits for queued receive and send operations to leave their gates.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DisconnectWaitsForQueuedOperations(CancellationToken cancellationToken)
    {
        S7OperationGate writeGate = new();
        S7OperationGate receiveGate = new();
        await using S7PlusTransport transport = new(CreateLoopbackOptions(LifecycleTestPort), writeGate, receiveGate);
        using var receiveLease = await receiveGate.AcquireAsync(cancellationToken);
        using var writeLease = await writeGate.AcquireAsync(cancellationToken);

        var disconnecting = transport.DisconnectAsync(CancellationToken.None);
        await TAssert.That(disconnecting.IsCompleted).IsFalse();
        receiveLease.Dispose();
        await TAssert.That(disconnecting.IsCompleted).IsFalse();
        writeLease.Dispose();
        await disconnecting;

        using var reopenedReceiveLease = await receiveGate.AcquireAsync(cancellationToken);
        using var reopenedWriteLease = await writeGate.AcquireAsync(cancellationToken);
    }

    /// <summary>Repeated disposal calls await the same drain and queued receives are rejected.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DisposeWaitsForActiveOperationsAndRejectsQueuedOperations(CancellationToken cancellationToken)
    {
        S7OperationGate writeGate = new();
        S7OperationGate receiveGate = new();
        S7PlusTransport transport = new(CreateLoopbackOptions(LifecycleTestPort), writeGate, receiveGate);
        using var receiveLease = await receiveGate.AcquireAsync(cancellationToken);
        var queuedReceive = receiveGate.AcquireAsync(CancellationToken.None);
        await TAssert.That(queuedReceive.IsCompleted).IsFalse();

        var firstDispose = transport.DisposeAsync().AsTask();
        var secondDispose = transport.DisposeAsync().AsTask();
        await TAssert.That(firstDispose.IsCompleted).IsFalse();
        await TAssert.That(secondDispose.IsCompleted).IsFalse();
        await TAssert.That(async () => await queuedReceive).Throws<ObjectDisposedException>();

        receiveLease.Dispose();
        await Task.WhenAll(firstDispose, secondDispose);
    }

    /// <summary>Creates loopback connection options for a test listener.</summary>
    /// <param name="port">The listener port.</param>
    /// <returns>The loopback connection options.</returns>
    private static S7SymbolicConnectionOptions CreateLoopbackOptions(int port) =>
        new("127.0.0.1") { Port = port };

    /// <summary>Creates certificate validation options for a test case.</summary>
    /// <param name="certificateSha256">The optional certificate pin.</param>
    /// <param name="validation">The optional custom validation callback.</param>
    /// <returns>The certificate connection options.</returns>
    private static S7SymbolicConnectionOptions CreateCertificateOptions(
        string? certificateSha256 = null,
        Func<X509Certificate2, bool>? validation = null) =>
        new(CertificateHost)
        {
            CertificateSha256 = certificateSha256,
            CertificateValidation = validation,
        };

    /// <summary>Read packet.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    private static async Task<byte[]> ReadPacket(Stream stream, CancellationToken cancellationToken)
    {
        var header = await ReadExact(stream, PlusHeaderLength, cancellationToken);
        return await ReadExact(stream, ((header[WordByteCount] << BitsPerByte) | header[CotpDataHeaderLength]) - PlusHeaderLength, cancellationToken);
    }

    /// <summary>Read exact.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="length">The length.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    private static async Task<byte[]> ReadExact(Stream stream, int length, CancellationToken cancellationToken)
    {
        var bytes = new byte[length];
        for (var offset = 0; offset < length;)
        {
            var count = await stream.ReadAsync(bytes.AsMemory(offset, length - offset), cancellationToken);
            if (count == 0)
            {
                throw new EndOfStreamException();
            }

            offset += count;
        }

        return bytes;
    }

    /// <summary>Send packet.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="cotp">The cotp.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    private static async Task SendPacket(Stream stream, byte[] cotp, CancellationToken cancellationToken)
    {
        var packet = new byte[cotp.Length + PlusHeaderLength];
        packet[0] = CotpDataHeaderLength;
        packet[WordByteCount] = (byte)(packet.Length >> BitsPerByte);
        packet[CotpDataHeaderLength] = (byte)packet.Length;
        Buffer.BlockCopy(cotp, 0, packet, PlusHeaderLength, cotp.Length);
        await stream.WriteAsync(packet.AsMemory(), cancellationToken);
    }
}
