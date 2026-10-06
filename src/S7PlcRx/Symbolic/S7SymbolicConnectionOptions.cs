// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
namespace IoT.Driver.S7PlcRx.Symbolic;
#endif

/// <summary>Connection and certificate policy for an S7 symbolic session.</summary>
public sealed class S7SymbolicConnectionOptions
{
    /// <summary>Stores the default timeout seconds.</summary>
    private const int DefaultTimeoutSeconds = 5;

    /// <summary>Stores the maximum port.</summary>
    private const int MaximumPort = 65_535;

    /// <summary>Stores the certificate pin hex length.</summary>
    private const int CertificatePinHexLength = 64;

    /// <summary>Initializes a new instance of the <see cref="S7SymbolicConnectionOptions"/> class.</summary>
    /// <param name="host">Controller DNS name or IP address.</param>
    public S7SymbolicConnectionOptions(string host)
    {
#if NETFRAMEWORK
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("Controller host must not be empty.", nameof(host));
        }
#else
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
#endif
        Host = host;
    }

    /// <summary>Gets the controller host.</summary>
    public string Host { get; }

    /// <summary>Gets the ISO on TCP port.</summary>
    public int Port { get; init; } = 102;

    /// <summary>Gets the timeout for a transport operation.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(DefaultTimeoutSeconds);

    /// <summary>Gets the optional session username.</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>Gets the optional session password.</summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>Gets an optional 64 digit SHA256 certificate pin.</summary>
    public string? CertificateSha256 { get; init; }

    /// <summary>Gets the TLS hostname, defaulting to the controller host.</summary>
    public string? TargetHost { get; init; }

    /// <summary>Gets an explicit certificate trust callback.</summary>
    public Func<X509Certificate2, bool>? CertificateValidation { get; init; }

    /// <summary>Gets the requested authentication exchange.</summary>
    public S7SymbolicAuthenticationMode AuthenticationMode { get; init; }

    /// <summary>Validates the connection options.</summary>
    internal void Validate()
    {
        if (Port is < 1 or > MaximumPort)
        {
            throw new ArgumentOutOfRangeException(nameof(Port), "Port must be between 1 and 65_535.");
        }

        if (Timeout <= TimeSpan.Zero || Timeout.TotalMilliseconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(Timeout), "Timeout must be positive and fit in milliseconds.");
        }

        ValidateCredentials();

        if (AuthenticationMode is not (S7SymbolicAuthenticationMode.Auto or S7SymbolicAuthenticationMode.Legacy or S7SymbolicAuthenticationMode.Modern))
        {
            throw new ArgumentOutOfRangeException(nameof(AuthenticationMode));
        }

        ValidatePin();
    }

    /// <summary>Validates credentials and the optional TLS host.</summary>
    private void ValidateCredentials()
    {
#if NETFRAMEWORK
        if (Username is null)
        {
            throw new ArgumentNullException(nameof(Username));
        }

        if (Password is null)
        {
            throw new ArgumentNullException(nameof(Password));
        }

        if (!string.IsNullOrWhiteSpace(TargetHost ?? Host))
        {
            return;
        }

        throw new ArgumentException("TLS host must not be empty.", nameof(TargetHost));
#else
        ArgumentNullException.ThrowIfNull(Username);
        ArgumentNullException.ThrowIfNull(Password);
        ArgumentException.ThrowIfNullOrWhiteSpace(TargetHost ?? Host);
#endif
    }

    /// <summary>Validate pin.</summary>
    private void ValidatePin()
    {
        if (CertificateSha256 is null)
        {
            return;
        }

        if (CertificateSha256.Length != CertificatePinHexLength)
        {
            throw new ArgumentException("Certificate pin must contain 64 hexadecimal digits.", nameof(CertificateSha256));
        }

        foreach (var digit in CertificateSha256)
        {
            if (!Uri.IsHexDigit(digit))
            {
                throw new ArgumentException("Certificate pin must contain hexadecimal digits.", nameof(CertificateSha256));
            }
        }
    }
}
