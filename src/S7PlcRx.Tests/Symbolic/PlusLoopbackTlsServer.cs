// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;
using Org.BouncyCastle.X509;

namespace IoT.Driver.S7PlcRx.Tests.Symbolic;

/// <summary>Provides an independently generated TLS 1.3 server identity for wire conformance tests.</summary>
internal sealed class PlusLoopbackTlsServer : DefaultTlsServer
{
    /// <summary>The independently specified wire fixture value 2048.</summary>
    private const int WireValue2048 = 2048;

    /// <summary>The independently specified wire fixture value 32.</summary>
    private const int WireValue32 = 32;

    /// <summary>Owns the ephemeral signing key.</summary>
    private readonly AsymmetricKeyParameter _key;

    /// <summary>Owns the TLS certificate chain.</summary>
    private readonly Certificate _certificate;

    /// <summary>Retains only the protocol exporter captured while the server handshake context exposes its secret.</summary>
    private byte[] _exporter = [];

    /// <summary>Initializes a new instance of the <see cref="PlusLoopbackTlsServer"/> class.</summary>
    /// <param name="timeProvider">Supplies certificate validity times.</param>
    internal PlusLoopbackTlsServer(TimeProvider timeProvider)
        : base(new BcTlsCrypto(new SecureRandom()))
    {
        var generator = new RsaKeyPairGenerator();
        generator.Init(new(new SecureRandom(), WireValue2048));
        var pair = generator.GenerateKeyPair();
        _key = pair.Private;
        var certificate = new X509V3CertificateGenerator();
        var name = new X509Name("CN=localhost");
        certificate.SetSerialNumber(BigInteger.One);
        certificate.SetIssuerDN(name);
        certificate.SetSubjectDN(name);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        certificate.SetNotBefore(now.AddMinutes(-1));
        certificate.SetNotAfter(now.AddHours(1));
        certificate.SetPublicKey(pair.Public);
        CertificateBytes = certificate.Generate(new Asn1SignatureFactory("SHA256WITHRSA", _key)).GetEncoded();
        _certificate = new(
            [],
            [new CertificateEntry(Crypto.CreateCertificate(CertificateBytes), null)]);
    }

    /// <summary>Gets the public DER certificate used to calculate the client pin.</summary>
    internal byte[] CertificateBytes { get; }

    /// <summary>Gets whether both peers completed the TLS handshake.</summary>
    internal bool HandshakeComplete { get; private set; }

    /// <summary>Selects the TLS 1.3 RSA-PSS signing credential.</summary>
    /// <returns>The ephemeral server signing credential.</returns>
    public override TlsCredentials GetCredentials() => new BcDefaultTlsCredentialedSigner(
        new TlsCryptoParameters(m_context),
        (BcTlsCrypto)Crypto,
        _key,
        _certificate,
        SignatureAndHashAlgorithm.GetInstance(HashAlgorithm.Intrinsic, SignatureAlgorithm.rsa_pss_rsae_sha256));

    /// <summary>Restricts negotiation to TLS 1.3.</summary>
    /// <returns>The supported version.</returns>
    public override ProtocolVersion[] GetProtocolVersions() => [ProtocolVersion.TLSv13];

    /// <summary>Selects an authenticated encryption suite supported by the PLC client.</summary>
    /// <returns>The supported cipher suite.</returns>
    public override int[] GetCipherSuites() => [CipherSuite.TLS_AES_128_GCM_SHA256];

    /// <summary>Records completion after TLS validates the client Finished message.</summary>
    public override void NotifyHandshakeComplete()
    {
        base.NotifyHandshakeComplete();
        _exporter = m_context.ExportKeyingMaterial("EXPERIMENTAL_OMS", null, WireValue32);
        HandshakeComplete = true;
    }

    /// <summary>Obtains keying material from the independently negotiated server context.</summary>
    /// <returns>The OMS authentication exporter.</returns>
    internal byte[] Export() => HandshakeComplete
        ? (byte[])_exporter.Clone()
        : throw new InvalidOperationException("The independent server TLS handshake is incomplete.");

    /// <summary>Erases the cached exporter when the peer releases its connection.</summary>
    internal void ClearExporter() => Array.Clear(_exporter, 0, _exporter.Length);
}
