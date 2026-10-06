// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic.Protocol;
#else
namespace IoT.Driver.S7PlcRx.Symbolic.Protocol;
#endif
/// <summary>Encodes the controller authentication challenges mandated by S7Plus.</summary>
internal static class S7PlusAuthentication
{
    /// <summary>The legacy challenge size.</summary>
    private const int ChallengeLength = 20;

    /// <summary>The CBC initialization vector size.</summary>
    private const int InitializationVectorLength = 16;

    /// <summary>The authentication payload structure identifier.</summary>
    private const uint PayloadTypeId = 40_400;

    /// <summary>The authentication mode field identifier.</summary>
    private const uint ModeFieldId = 40_401;

    /// <summary>The username field identifier.</summary>
    private const uint UsernameFieldId = 40_402;

    /// <summary>The credential field identifier.</summary>
    private const uint CredentialFieldId = 40_403;

    /// <summary>The username and credential authentication mode.</summary>
    private const uint UsernameMode = 2;

    /// <summary>The maximum authentication envelope overhead before its byte payloads.</summary>
    private const int EnvelopeCapacity = 0x40;

    /// <summary>Encodes the protocol SHA1 digest XOR challenge response.</summary>
    /// <param name = "encodedCredential">The UTF-8 credential bytes.</param>
    /// <param name = "challenge">The controller challenge.</param>
    /// <returns>The twenty byte response.</returns>
    internal static byte[] Legacy(byte[] encodedCredential, byte[] challenge)
    {
        if (challenge.Length != ChallengeLength)
        {
            throw new ArgumentException("The controller challenge must contain twenty bytes.", nameof(challenge));
        }

        // SHA1 is the controller wire algorithm; this codec does not persist credentials.
        var response = Digest(new Sha1Digest(), encodedCredential);
        for (var index = 0; index < response.Length; index++)
        {
            response[index] ^= challenge[index];
        }

        return response;
    }

    /// <summary>Encodes the TLS exporter bound controller authentication payload.</summary>
    /// <param name = "exporter">The TLS exporter secret.</param>
    /// <param name = "challenge">The controller challenge.</param>
    /// <param name = "username">The UTF-8 username.</param>
    /// <param name = "encodedCredential">The UTF-8 credential.</param>
    /// <returns>The encrypted payload.</returns>
    internal static byte[] Modern(byte[] exporter, byte[] challenge, byte[] username, byte[] encodedCredential)
    {
        if (challenge.Length != ChallengeLength)
        {
            throw new ArgumentException("The controller challenge must contain twenty bytes.", nameof(challenge));
        }

        var userAuthentication = username.Length != 0;
        var credential = Array.Empty<byte>();
        var key = Array.Empty<byte>();
        var plaintext = Array.Empty<byte>();
        S7SymbolicBlob? credentialBlob = null;
        S7SymbolicBlob? usernameBlob = null;
        PlusWriter? writer = null;
        try
        {
            credential = userAuthentication ? (byte[])encodedCredential.Clone() : Digest(new Sha1Digest(), encodedCredential);
            key = Digest(new Sha256Digest(), exporter);
            writer = new(checked(username.Length + credential.Length + EnvelopeCapacity));
            credentialBlob = new(0, credential);
            usernameBlob = new(0, username);
            var fields = new Dictionary<uint, S7SymbolicValue>
            {
                [ModeFieldId] = new(S7SymbolicDataType.UDInt, userAuthentication ? UsernameMode : 1U),
                [UsernameFieldId] = new(S7SymbolicDataType.Blob, usernameBlob),
                [CredentialFieldId] = new(S7SymbolicDataType.Blob, credentialBlob),
            };
            new S7SymbolicValue(S7SymbolicDataType.Struct, new S7SymbolicStruct(PayloadTypeId, fields)).WriteTo(writer);
            plaintext = writer.ToArray();
            var iv = new byte[InitializationVectorLength];
            Array.Copy(challenge, iv, iv.Length);

            // The controller mandates CBC with the challenge prefix as IV; TLS protects this exchange.
            var cipher = CipherUtilities.GetCipher("AES/CBC/PKCS7Padding");
            cipher.Init(true, new ParametersWithIV(new KeyParameter(key), iv));
            return cipher.DoFinal(plaintext);
        }
        finally
        {
            Array.Clear(key, 0, key.Length);
            Array.Clear(credential, 0, credential.Length);
            Array.Clear(plaintext, 0, plaintext.Length);
            ClearOwnedBlob(credentialBlob);
            ClearOwnedBlob(usernameBlob);
            writer?.ClearSensitive();
        }
    }

    /// <summary>Erases the temporary blob copy owned by the authentication encoder.</summary>
    /// <param name="blob">The temporary blob, if constructed.</param>
    private static void ClearOwnedBlob(S7SymbolicBlob? blob)
    {
        if (blob is null)
        {
            return;
        }

        Array.Clear(blob.Data, 0, blob.Data.Length);
    }

    /// <summary>Calculates a protocol digest.</summary>
    /// <param name = "digest">The wire digest algorithm.</param>
    /// <param name = "input">The input bytes.</param>
    /// <returns>The computed digest.</returns>
    private static byte[] Digest(IDigest digest, byte[] input)
    {
        digest.BlockUpdate(input, 0, input.Length);
        var output = new byte[digest.GetDigestSize()];
        _ = digest.DoFinal(output, 0);
        return output;
    }
}
