// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;
using BcCertificateRequest = Org.BouncyCastle.Tls.CertificateRequest;

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic.Transport;
#else
namespace IoT.Driver.S7PlcRx.Symbolic.Transport;
#endif

/// <summary>Provides s7tls client.</summary>
internal sealed class S7TlsClient : DefaultTlsClient
{
    /// <summary>The TLS exporter label required by the S7Plus authentication protocol.</summary>
    private const string OmsExporterLabel = "EXPERIMENTAL_OMS";

    /// <summary>The S7Plus authentication exporter byte count.</summary>
    private const int OmsExporterLength = 32;

    /// <summary>Stores the word byte count.</summary>
    private const int HexDigitsPerByte = 2;

    /// <summary>Stores the remote tsap length.</summary>
    private const int HexRadix = 16;

    /// <summary>Stores the options.</summary>
    private readonly S7SymbolicConnectionOptions _options;

    /// <summary>Stores the authentication.</summary>
    private readonly Authentication _authentication;

    /// <summary>Stores the protocol exporter derived when the TLS handshake completes.</summary>
    private byte[]? _omsExporter;

    /// <summary>Initializes a new instance of the <see cref="S7TlsClient"/> class.</summary>
    /// <param name="options">The options.</param>
    internal S7TlsClient(S7SymbolicConnectionOptions options)
        : base(new BcTlsCrypto(new SecureRandom()))
    {
        _options = options;
        _authentication = new(options);
    }

    /// <summary>Gets the handshake complete.</summary>
    internal bool HandshakeComplete { get; private set; }

    /// <summary>Get authentication.</summary>
    /// <returns>The operation result.</returns>
    public override TlsAuthentication GetAuthentication() => _authentication;

    /// <summary>Get protocol versions.</summary>
    /// <returns>The operation result.</returns>
    public override ProtocolVersion[] GetProtocolVersions() => [ProtocolVersion.TLSv13];

    /// <summary>Get cipher suites.</summary>
    /// <returns>The operation result.</returns>
    public override int[] GetCipherSuites() => [CipherSuite.TLS_AES_128_GCM_SHA256, CipherSuite.TLS_AES_256_GCM_SHA384];

    /// <summary>Notify handshake complete.</summary>
    public override void NotifyHandshakeComplete()
    {
        var exporter = m_context.ExportKeyingMaterial(OmsExporterLabel, null, OmsExporterLength);
        try
        {
            base.NotifyHandshakeComplete();
        }
        catch
        {
            Array.Clear(exporter, 0, exporter.Length);
            throw;
        }

        _omsExporter = exporter;
        HandshakeComplete = true;
    }

    /// <summary>Get client extensions.</summary>
    /// <returns>The operation result.</returns>
    public override IDictionary<int, byte[]> GetClientExtensions()
    {
        var extensions = base.GetClientExtensions();
        var host = _options.TargetHost ?? _options.Host;
        if (!IPAddress.TryParse(host, out _))
        {
            TlsExtensionsUtilities.AddServerNameExtensionClient(extensions, [new ServerName(NameType.host_name, Encoding.ASCII.GetBytes(host))]);
        }

        return extensions;
    }

    /// <summary>Validate certificate.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <param name="intermediates">The intermediates.</param>
    /// <param name="options">The options.</param>
    /// <param name="timeProvider">The certificate validity clock.</param>
    /// <returns>The operation result.</returns>
    internal static bool ValidateCertificate(X509Certificate2 certificate, byte[][] intermediates, S7SymbolicConnectionOptions options, TimeProvider? timeProvider = null)
    {
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        if (now < certificate.NotBefore.ToUniversalTime() || now > certificate.NotAfter.ToUniversalTime())
        {
            return false;
        }

        if (options.CertificateSha256 is not null)
        {
            var digest = CertificateDigest(certificate.RawData);
            var pin = options.CertificateSha256;
            var difference = 0;
            for (var index = 0; index < digest.Length; index++)
            {
                difference |= digest[index] ^ Convert.ToByte(pin[(index * HexDigitsPerByte)..((index + 1) * HexDigitsPerByte)], HexRadix);
            }

            return difference == 0;
        }

        if (options.CertificateValidation is not null)
        {
            return options.CertificateValidation(certificate);
        }

        if (!MatchesHost(certificate, options.TargetHost ?? options.Host))
        {
            return false;
        }

        return ValidateChain(certificate, intermediates, options.Timeout);
    }

    /// <summary>Matches host.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <param name="host">The host.</param>
    /// <returns>The operation result.</returns>
    internal static bool MatchesHost(X509Certificate2 certificate, string host)
    {
        var ip = IPAddress.TryParse(host, out var address);
        var hasDnsNames = false;
        foreach (System.Security.Cryptography.X509Certificates.X509Extension extension in certificate.Extensions)
        {
            if (extension.Oid?.Value != "2.5.29.17")
            {
                continue;
            }

            foreach (var name in GeneralNames.GetInstance(Asn1Object.FromByteArray(extension.RawData)).GetNames())
            {
                if (MatchesAlternativeName(name, host, address, out var dnsName))
                {
                    return true;
                }

                hasDnsNames |= dnsName;
            }
        }

        return !ip && !hasDnsNames && MatchesDns(certificate.GetNameInfo(X509NameType.SimpleName, false), host);
    }

    /// <summary>Exports TLS key material.</summary>
    /// <param name="label">The label.</param>
    /// <param name="length">The length.</param>
    /// <returns>The operation result.</returns>
    internal byte[] Export(string label, int length)
    {
        if (!string.Equals(label, OmsExporterLabel, StringComparison.Ordinal) || length != OmsExporterLength)
        {
            throw new ArgumentException("Only the S7Plus authentication exporter is supported.");
        }

        if (!HandshakeComplete || _omsExporter is null)
        {
            throw new InvalidOperationException("TLS handshake is not complete.");
        }

        return (byte[])_omsExporter.Clone();
    }

    /// <summary>Clears the protocol exporter when the transport session ends.</summary>
    internal void ClearExporter()
    {
        var exporter = _omsExporter;
        _omsExporter = null;
        HandshakeComplete = false;
        if (exporter is null)
        {
            return;
        }

        Array.Clear(exporter, 0, exporter.Length);
    }

    /// <summary>Validates the certificate chain for TLS server use.</summary>
    /// <param name="certificate">The leaf certificate.</param>
    /// <param name="intermediates">The intermediate certificate encodings.</param>
    /// <param name="timeout">The revocation lookup timeout.</param>
    /// <returns>Whether the operating system trusts the chain.</returns>
    private static bool ValidateChain(X509Certificate2 certificate, byte[][] intermediates, TimeSpan timeout)
    {
        using X509Chain chain = new();
        List<X509Certificate2> added = new();
        try
        {
            foreach (var bytes in intermediates)
            {
                var intermediate = LoadCertificate(bytes);
                added.Add(intermediate);
                _ = chain.ChainPolicy.ExtraStore.Add(intermediate);
            }

            _ = chain.ChainPolicy.ApplicationPolicy.Add(new("1.3.6.1.5.5.7.3.1"));
            chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
            chain.ChainPolicy.UrlRetrievalTimeout = timeout;
            return chain.Build(certificate);
        }
        finally
        {
            foreach (var intermediate in added)
            {
                intermediate.Dispose();
            }
        }
    }

    /// <summary>Load certificate.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The operation result.</returns>
    private static X509Certificate2 LoadCertificate(byte[] bytes)
    {
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadCertificate(bytes);
#else
        return new(bytes);
#endif
    }

    /// <summary>Matches dns.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="host">The host.</param>
    /// <returns>The operation result.</returns>
    private static bool MatchesDns(string pattern, string host)
    {
        if (string.Equals(pattern, host, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!pattern.StartsWith("*.", StringComparison.Ordinal) || pattern.IndexOf('*', 1) >= 0)
        {
            return false;
        }

        var dot = host.IndexOf('.');
        return dot > 0 && string.Equals(host[dot..], pattern[1..], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Matches alternative name.</summary>
    /// <param name="name">The name.</param>
    /// <param name="host">The host.</param>
    /// <param name="address">The address.</param>
    /// <param name="dnsName">The dns name.</param>
    /// <returns>The operation result.</returns>
    private static bool MatchesAlternativeName(GeneralName name, string host, IPAddress? address, out bool dnsName)
    {
        dnsName = address is null && name.TagNo == GeneralName.DnsName;
        return dnsName
            ? MatchesDns(DerIA5String.GetInstance(name.Name).GetString(), host)
            : address is not null && name.TagNo == GeneralName.IPAddress &&
                new IPAddress(Asn1OctetString.GetInstance(name.Name).GetOctets()).Equals(address);
    }

    /// <summary>Certificate digest.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <returns>The operation result.</returns>
    private static byte[] CertificateDigest(byte[] certificate)
    {
#if NET8_0_OR_GREATER
        return SHA256.HashData(certificate);
#else
        using var sha = SHA256.Create();
        return sha.ComputeHash(certificate);
#endif
    }

    /// <summary>Provides authentication.</summary>
    private sealed class Authentication : TlsAuthentication
    {
        /// <summary>Stores the options.</summary>
        private readonly S7SymbolicConnectionOptions _options;

        /// <summary>Initializes a new instance of the <see cref="Authentication"/> class.</summary>
        /// <param name="options">The options.</param>
        internal Authentication(S7SymbolicConnectionOptions options) => _options = options;

        /// <summary>Get client credentials.</summary>
        /// <param name="certificateRequest">The certificate request.</param>
        /// <returns>The operation result.</returns>
        public TlsCredentials? GetClientCredentials(BcCertificateRequest certificateRequest) => null;

        /// <summary>Notify server certificate.</summary>
        /// <param name="serverCertificate">The server certificate.</param>
        public void NotifyServerCertificate(TlsServerCertificate serverCertificate)
        {
            var certificates = serverCertificate.Certificate.GetCertificateList();
            if (certificates.Length == 0)
            {
                throw new TlsFatalAlert(AlertDescription.bad_certificate, new AuthenticationException("Controller did not provide a certificate."));
            }

            using var certificate = LoadCertificate(certificates[0].GetEncoded());
            var intermediates = new byte[certificates.Length - 1][];
            for (var index = 1; index < certificates.Length; index++)
            {
                intermediates[index - 1] = certificates[index].GetEncoded();
            }

            if (ValidateCertificate(certificate, intermediates, _options))
            {
                return;
            }

            throw new TlsFatalAlert(AlertDescription.bad_certificate, new AuthenticationException("Controller certificate validation failed."));
        }
    }
}
