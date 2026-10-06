// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace IoT.Driver.S7PlcRx.Tests.Symbolic;

/// <summary>Encodes a bounded conformance fixture independently of production Plus serializers.</summary>
internal static class PlusLoopbackWire
{
    /// <summary>The independently specified wire fixture value 4.</summary>
    private const int WireValue4 = 4;

    /// <summary>The independently specified wire fixture value 8.</summary>
    private const int WireValue8 = 8;

    /// <summary>The independently specified wire fixture value 5.</summary>
    private const int WireValue5 = 5;

    /// <summary>The independently specified wire fixture value 128.</summary>
    private const int WireValue128 = 128;

    /// <summary>The independently specified wire fixture value 127.</summary>
    private const int WireValue127 = 127;

    /// <summary>The independently specified wire fixture value 24.</summary>
    private const int WireValue24 = 24;

    /// <summary>The independently specified wire fixture value 16.</summary>
    private const int WireValue16 = 16;

    /// <summary>The independently specified wire fixture value 21.</summary>
    private const int WireValue21 = 0x15;

    /// <summary>The independently specified wire fixture value 20.</summary>
    private const int WireValue20 = 0x14;

    /// <summary>The independently specified wire fixture value 161.</summary>
    private const int WireValue161 = 0xa1;

    /// <summary>The independently specified wire fixture value 162.</summary>
    private const int WireValue162 = 0xa2;

    /// <summary>The independently specified wire fixture value 163.</summary>
    private const int WireValue163 = 0xa3;

    /// <summary>The independently specified wire fixture value 232.</summary>
    private const int WireValue232 = 0xe8;

    /// <summary>The independently specified wire fixture value 137.</summary>
    private const int WireValue137 = 0x89;

    /// <summary>The independently specified wire fixture value 105.</summary>
    private const int WireValue105 = 0x69;

    /// <summary>The independently specified wire fixture value 18.</summary>
    private const int WireValue18 = 0x12;

    /// <summary>The independently specified wire fixture value 106.</summary>
    private const int WireValue106 = 0x6a;

    /// <summary>The independently specified wire fixture value 19.</summary>
    private const int WireValue19 = 0x13;

    /// <summary>The independently specified wire fixture value 107.</summary>
    private const int WireValue107 = 0x6b;

    /// <summary>The independently specified wire fixture value 23.</summary>
    private const int WireValue23 = 0x17;

    /// <summary>The independently specified wire fixture value 256.</summary>
    private const int WireValue256 = 0x100;

    /// <summary>The independently specified wire fixture value 319.</summary>
    private const int WireValue319 = 319;

    /// <summary>The independently specified wire fixture value 40400.</summary>
    private const int WireValue40400 = 40_400;

    /// <summary>The independently specified wire fixture value 40401.</summary>
    private const int WireValue40401 = 40_401;

    /// <summary>The independently specified wire fixture value 2.</summary>
    private const int WireValue2 = 2;

    /// <summary>The independently specified wire fixture value 40402.</summary>
    private const int WireValue40402 = 40_402;

    /// <summary>The independently specified wire fixture value 40403.</summary>
    private const int WireValue40403 = 40_403;

    /// <summary>The independently specified wire fixture value 287454020.</summary>
    private const int WireValue287454020 = 0x11223344;

    /// <summary>The independently specified wire fixture value 12.</summary>
    private const int WireValue12 = 12;

    /// <summary>The independently specified wire fixture value 640.</summary>
    private const int WireValue640 = 0x280;

    /// <summary>The independently specified wire fixture value 3.</summary>
    private const int WireValue3 = 3;

    /// <summary>The independently specified wire fixture value 171.</summary>
    private const int WireValue171 = 0xab;

    /// <summary>The independently specified wire fixture value 172.</summary>
    private const int WireValue172 = 0xac;

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture1 = [0, WireValue4];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture2 = [0, WireValue21];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture3 = [0, WireValue20, 0];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture4 = [WireValue161];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture5 = [0, 0];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture6 = [WireValue162];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture7 = [WireValue163];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture8 = [0, WireValue23];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture9 = [0];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture10 = [0, WireValue23];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture11 = [0];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture12 = new byte[WireValue4];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture13 = new byte[WireValue12];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture14 = new byte[WireValue16];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture15 = [WireValue171];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture16 = [0, 0, WireValue172];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture17 = [0, 0];

    /// <summary>Reads a big-endian fixed-width relation identifier.</summary>
    /// <param name="data">The incoming packet.</param>
    /// <param name="position">The advancing cursor.</param>
    /// <returns>The decoded identifier.</returns>
    internal static uint Fixed(byte[] data, ref int position)
    {
        uint result = 0;
        for (var index = 0; index < WireValue4; index++)
        {
            result = (result << WireValue8) | data[position];
            position++;
        }

        return result;
    }

    /// <summary>Reads a bounded most-significant-group-first unsigned VLQ.</summary>
    /// <param name="data">The incoming packet.</param>
    /// <param name="position">The advancing cursor.</param>
    /// <returns>The decoded integer.</returns>
    internal static uint Read(byte[] data, ref int position)
    {
        uint result = 0;
        for (var group = 0; group < WireValue5; group++)
        {
            var octet = data[position];
            position++;
            result = checked((result * WireValue128) + (uint)(octet & WireValue127));
            if (octet < WireValue128)
            {
                return result;
            }
        }

        throw new InvalidDataException("The test peer received an oversized VLQ.");
    }

    /// <summary>Encodes a most-significant-group-first unsigned VLQ.</summary>
    /// <param name="value">The integer.</param>
    /// <returns>The independently encoded integer.</returns>
    internal static byte[] Unsigned(uint value)
    {
        var result = new List<byte> { (byte)(value & WireValue127) };
        while ((value /= WireValue128) != 0)
        {
            result.Insert(0, (byte)((value & WireValue127) | WireValue128));
        }

        return result.ToArray();
    }

    /// <summary>Encodes a fixed-width big-endian identifier.</summary>
    /// <param name="value">The identifier.</param>
    /// <returns>The encoded identifier.</returns>
    internal static byte[] Identifier(uint value) => [(byte)(value >> WireValue24), (byte)(value >> WireValue16), (byte)(value >> WireValue8), (byte)value];

    /// <summary>Joins wire fragments in declaration order.</summary>
    /// <param name="parts">The independently generated fragments.</param>
    /// <returns>The joined packet.</returns>
    internal static byte[] Join(params byte[][] parts)
    {
        var length = 0;
        foreach (var part in parts)
        {
            length += part.Length;
        }

        var result = new byte[length];
        var offset = 0;
        foreach (var part in parts)
        {
            Buffer.BlockCopy(part, 0, result, offset, part.Length);
            offset += part.Length;
        }

        return result;
    }

    /// <summary>Copies a bounded packet section without LINQ or production helpers.</summary>
    /// <param name="bytes">The source packet.</param>
    /// <param name="offset">The first byte.</param>
    /// <param name="length">The byte count.</param>
    /// <returns>The independent packet section.</returns>
    internal static byte[] Slice(byte[] bytes, int offset, int length)
    {
        var result = new byte[length];
        Buffer.BlockCopy(bytes, offset, result, 0, length);
        return result;
    }

    /// <summary>Copies the remaining packet bytes.</summary>
    /// <param name="bytes">The source packet.</param>
    /// <param name="offset">The first byte.</param>
    /// <returns>The remaining packet bytes.</returns>
    internal static byte[] Slice(byte[] bytes, int offset) => Slice(bytes, offset, bytes.Length - offset);

    /// <summary>Compares independent wire fixtures element by element.</summary>
    /// <typeparam name="T">The fixture element type.</typeparam>
    /// <param name="left">The received values.</param>
    /// <param name="right">The expected values.</param>
    /// <returns>Whether both sequences match exactly.</returns>
    internal static bool Equal<T>(T[] left, T[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Length; index++)
        {
            if (!EqualityComparer<T>.Default.Equals(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Calculates the digest explicitly mandated by the controller authentication protocol.</summary>
    /// <param name="bytes">The authentication or exporter bytes.</param>
    /// <param name="legacy">Whether to use the legacy twenty-byte fingerprint.</param>
    /// <returns>The wire digest.</returns>
    internal static byte[] Digest(byte[] bytes, bool legacy)
    {
        IDigest digest = DigestUtilities.GetDigest(legacy ? "SHA-1" : "SHA-256");
        digest.BlockUpdate(bytes, 0, bytes.Length);
        var result = new byte[digest.GetDigestSize()];
        _ = digest.DoFinal(result, 0);
        return result;
    }

    /// <summary>Decrypts the controller-mandated authentication envelope using the independent server exporter.</summary>
    /// <param name="exporter">The server's TLS exporter.</param>
    /// <param name="iv">The server challenge prefix.</param>
    /// <param name="ciphertext">The client's submitted authentication payload.</param>
    /// <returns>The decrypted payload for byte-level conformance comparison.</returns>
    internal static byte[] DecryptAuthentication(byte[] exporter, byte[] iv, byte[] ciphertext)
    {
        var cipher = CipherUtilities.GetCipher("AES/CBC/PKCS7Padding");
        cipher.Init(false, new ParametersWithIV(new KeyParameter(Digest(exporter, false)), iv));
        return cipher.DoFinal(ciphertext);
    }

    /// <summary>Encodes an identifier list using the independent VLQ codec.</summary>
    /// <param name="ids">The identifiers.</param>
    /// <returns>The encoded identifiers.</returns>
    internal static byte[] Identifiers(uint[] ids)
    {
        var result = new List<byte>();
        foreach (var id in ids)
        {
            result.AddRange(Unsigned(id));
        }

        return result.ToArray();
    }

    /// <summary>Encodes a scalar UDInt value with flags and datatype.</summary>
    /// <param name="value">The scalar.</param>
    /// <returns>The typed wire value.</returns>
    internal static byte[] Number(uint value) => Join(WireFixture1, Unsigned(value));

    /// <summary>Encodes a UTF-8 WString value.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The typed text.</returns>
    internal static byte[] Text(string value)
    {
        var encoded = Encoding.UTF8.GetBytes(value);
        return Join(WireFixture2, Unsigned((uint)encoded.Length), encoded);
    }

    /// <summary>Encodes a root-zero blob value.</summary>
    /// <param name="value">The bytes.</param>
    /// <returns>The typed blob.</returns>
    internal static byte[] Blob(byte[] value) => Join(WireFixture3, Unsigned((uint)value.Length), value);

    /// <summary>Encodes an object with caller-supplied attributes and metadata.</summary>
    /// <param name="id">The object relation.</param>
    /// <param name="classId">The object class.</param>
    /// <param name="contents">The already encoded entries.</param>
    /// <returns>The complete object.</returns>
    internal static byte[] Object(uint id, uint classId, byte[] contents) => Join(WireFixture4, Identifier(id), Unsigned(classId), WireFixture5, contents, WireFixture6);

    /// <summary>Encodes an object attribute.</summary>
    /// <param name="id">The attribute identifier.</param>
    /// <param name="value">The typed value.</param>
    /// <returns>The complete attribute entry.</returns>
    internal static byte[] Attribute(uint id, byte[] value) => Join(WireFixture7, Unsigned(id), value);

    /// <summary>Returns the protocol qualifier fixture, including reserved zero values.</summary>
    /// <returns>The independently specified qualifier bytes.</returns>
    internal static byte[] Qualifier() =>
    [
        0,
        0,
        WireValue4,
        WireValue232,
        WireValue137,
        WireValue105,
        0,
        WireValue18,
        0,
        0,
        0,
        0,
        WireValue137,
        WireValue106,
        0,
        WireValue19,
        0,
        WireValue137,
        WireValue107,
        0,
        WireValue4,
        0,
        0,
    ];

    /// <summary>Encodes the server's firmware version structure.</summary>
    /// <param name="modern">Whether the model advertises modern authentication.</param>
    /// <returns>The typed version.</returns>
    internal static byte[] Version(bool modern) => Join(
        WireFixture8,
        Identifier(WireValue256),
        Unsigned(WireValue319),
        Text(modern ? "1;6ES7 510-1DJ01-0AB0;V3.1.0" : "1;6ES7 510-1DJ01-0AB0;V2.9.0"),
        WireFixture9);

    /// <summary>Encodes the expected modern username/password authentication structure.</summary>
    /// <param name="username">The username.</param>
    /// <param name="password">The clear password.</param>
    /// <returns>The pre-encryption authentication payload.</returns>
    internal static byte[] Authentication(string username, byte[] password) => Join(
        WireFixture10,
        Identifier(WireValue40400),
        Unsigned(WireValue40401),
        Number(WireValue2),
        Unsigned(WireValue40402),
        Blob(Encoding.UTF8.GetBytes(username)),
        Unsigned(WireValue40403),
        Blob(password),
        WireFixture11);

    /// <summary>Encodes one little-endian native type declaration.</summary>
    /// <param name="localId">The symbolic local identifier.</param>
    /// <param name="kind">The metadata declaration kind.</param>
    /// <param name="relation">The nested type relation, or zero.</param>
    /// <returns>The native type declaration.</returns>
    internal static byte[] Member(uint localId, int kind, uint relation)
    {
        var result = new List<byte>(Little(localId));
        result.AddRange(Little(WireValue287454020));
        result.Add(WireValue5);
        var flags = (ushort)((kind << WireValue12) | WireValue640);
        result.Add((byte)(flags >> WireValue8));
        result.Add((byte)flags);
        result.Add(0);
        if (kind == WireValue8)
        {
            result.AddRange(WireFixture12);
        }
        else
        {
            result.AddRange(WireFixture13);
            if (kind == WireValue3)
            {
                result.AddRange(Little(unchecked((uint)-WireValue2)));
                result.AddRange(Little(WireValue3));
            }
            else
            {
                result.AddRange(Little(relation));
                result.AddRange(WireFixture14);
            }
        }

        return result.ToArray();
    }

    /// <summary>Encodes AB/AC chunked type and name metadata.</summary>
    /// <param name="members">The native type declarations.</param>
    /// <param name="names">The corresponding names.</param>
    /// <returns>The chunked metadata entries.</returns>
    internal static byte[] Metadata(byte[] members, params string[] names)
    {
        var types = Join(Little(1), members);
        var encodedNames = new List<byte>();
        foreach (var name in names)
        {
            var bytes = Encoding.UTF8.GetBytes(name);
            encodedNames.Add((byte)bytes.Length);
            encodedNames.AddRange(bytes);
            encodedNames.Add(0);
        }

        return Join(WireFixture15, Chunk(types), WireFixture16, Chunk(encodedNames.ToArray()), WireFixture17);
    }

    /// <summary>Encodes one length-prefixed metadata chunk.</summary>
    /// <param name="bytes">The chunk payload.</param>
    /// <returns>The chunk.</returns>
    private static byte[] Chunk(byte[] bytes) => Join([(byte)(bytes.Length >> WireValue8), (byte)bytes.Length], bytes);

    /// <summary>Encodes a native little-endian 32-bit value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The four bytes.</returns>
    private static byte[] Little(uint value) => [(byte)value, (byte)(value >> WireValue8), (byte)(value >> WireValue16), (byte)(value >> WireValue24)];
}
