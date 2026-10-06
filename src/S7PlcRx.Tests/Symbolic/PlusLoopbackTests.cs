// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;
using System.Security.Authentication;
using IoT.Driver.S7PlcRx.Symbolic;
using TAssert = TUnit.Assertions.Assert;

namespace IoT.Driver.S7PlcRx.Tests.Symbolic;

/// <summary>Verifies secure symbolic workflows against an independent TLS 1.3 and COTP loopback peer.</summary>
public sealed class PlusLoopbackTests
{
    /// <summary>The deterministic controller authentication credential.</summary>
    private const string TestPassword = "conformance-password";

    /// <summary>The independently specified wire fixture value 30.</summary>
    private const int WireValue30 = 30;

    /// <summary>The independently specified wire fixture value 305419896.</summary>
    private const uint WireValue305419896uint = 0x12345678U;

    /// <summary>The independently specified wire fixture value 591751049.</summary>
    private const uint WireValue591751049uint = 0x23456789U;

    /// <summary>The independently specified wire fixture value 2.</summary>
    private const int WireValue2 = 2;

    /// <summary>The independently specified wire fixture value 2316173313.</summary>
    private const uint WireValue2316173313uint = 0x8a0e0001U;

    /// <summary>The independently specified wire fixture value 2550.</summary>
    private const uint WireValue2550uint = 2550U;

    /// <summary>The independently specified wire fixture value 10.</summary>
    private const int WireValue10 = 10;

    /// <summary>The independently specified wire fixture value 287454020.</summary>
    private const uint WireValue287454020uint = 0x11223344U;

    /// <summary>The independently specified wire fixture value 3.</summary>
    private const uint WireValue3uint = 3U;

    /// <summary>The independently specified wire fixture value 20.</summary>
    private const int WireValue20 = 20;

    /// <summary>The independently specified wire fixture value 42.</summary>
    private const uint WireValue42uint = 42U;

    /// <summary>The independently specified wire fixture value 291.</summary>
    private const ulong WireValue291ulong = 0x123UL;

    /// <summary>The independently specified wire fixture value 55.</summary>
    private const uint WireValue55uint = 55U;

    /// <summary>The independently specified wire fixture value 100.</summary>
    private const uint WireValue100uint = 100U;

    /// <summary>The independently specified wire fixture value 200.</summary>
    private const uint WireValue200uint = 200U;

    /// <summary>The independently specified wire fixture value 300.</summary>
    private const uint WireValue300uint = 300U;

    /// <summary>The independently specified wire fixture value 1110.</summary>
    private const ulong WireValue1110ulong = 0x456UL;

    /// <summary>The independently specified wire fixture value 15.</summary>
    private const int WireValue15 = 15;

    /// <summary>The independently specified wire fixture value 64.</summary>
    private const int WireValue64 = 64;

    /// <summary>The independently specified wire fixture value 5.</summary>
    private const int WireValue5 = 5;

    /// <summary>The independently specified wire fixture value 1226.</summary>
    private const int WireValue1226 = 0x04ca;

    /// <summary>The independently specified wire fixture value 288.</summary>
    private const int WireValue288 = 288;

    /// <summary>The independently specified wire fixture value 54.</summary>
    private const int WireValue54 = 0x36;

    /// <summary>The independently specified wire fixture value 285.</summary>
    private const int WireValue285 = 285;

    /// <summary>The independently specified wire fixture value 4.</summary>
    private const int WireValue4 = 4;

    /// <summary>The independently specified wire fixture value 211.</summary>
    private const int WireValue211 = 211;

    /// <summary>The independently specified wire fixture value 287.</summary>
    private const int WireValue287 = 287;

    /// <summary>The independently specified wire fixture value 300.</summary>
    private const int WireValue300 = 300;

    /// <summary>The independently specified wire fixture value 18.</summary>
    private const int WireValue18 = 0x12;

    /// <summary>The independently specified wire fixture value 2160314625.</summary>
    private const uint WireValue2160314625uint = 0x80c3c901;

    /// <summary>The independently specified wire fixture value 255.</summary>
    private const int WireValue255 = 255;

    /// <summary>The independently specified wire fixture value 305419896.</summary>
    private const int WireValue305419896 = 0x12345678;

    /// <summary>The independently specified wire fixture value 591751049.</summary>
    private const int WireValue591751049 = 0x23456789;

    /// <summary>The independently specified wire fixture value 306.</summary>
    private const int WireValue306 = 306;

    /// <summary>The independently specified wire fixture value 1346.</summary>
    private const int WireValue1346 = 0x0542;

    /// <summary>The independently specified wire fixture value 52.</summary>
    private const int WireValue52 = 0x34;

    /// <summary>The independently specified wire fixture value 1356.</summary>
    private const int WireValue1356 = 0x054c;

    /// <summary>The independently specified wire fixture value 201.</summary>
    private const int WireValue201 = 201;

    /// <summary>The independently specified wire fixture value 1037.</summary>
    private const int WireValue1037 = 1037;

    /// <summary>The independently specified wire fixture value 1000.</summary>
    private const int WireValue1000 = 1000;

    /// <summary>The independently specified wire fixture value 1001.</summary>
    private const int WireValue1001 = 1001;

    /// <summary>The independently specified wire fixture value 2.</summary>
    private const uint WireValue2uint = 2U;

    /// <summary>The independently specified wire fixture value 1842.</summary>
    private const int WireValue1842 = 1842;

    /// <summary>The independently specified wire fixture value 3.</summary>
    private const int WireValue3 = 3;

    /// <summary>The independently specified wire fixture value 303.</summary>
    private const int WireValue303 = 303;

    /// <summary>The independently specified wire fixture value 16.</summary>
    private const int WireValue16 = 0x10;

    /// <summary>The independently specified wire fixture value 1266.</summary>
    private const int WireValue1266 = 0x04f2;

    /// <summary>The independently specified wire fixture value 1846.</summary>
    private const uint WireValue1846uint = 1846U;

    /// <summary>The independently specified wire fixture value 304.</summary>
    private const uint WireValue304uint = 304U;

    /// <summary>The independently specified wire fixture value 1414.</summary>
    private const int WireValue1414 = 0x0586;

    /// <summary>The independently specified wire fixture value 32.</summary>
    private const int WireValue32 = 0x20;

    /// <summary>The independently specified wire fixture value 233.</summary>
    private const int WireValue233 = 233;

    /// <summary>The independently specified wire fixture value 2574.</summary>
    private const int WireValue2574 = 2574;

    /// <summary>The independently specified wire fixture value 2550.</summary>
    private const int WireValue2550 = 2550;

    /// <summary>The independently specified wire fixture value 2147549185.</summary>
    private const uint WireValue2147549185uint = 0x80010001;

    /// <summary>The independently specified wire fixture value 2147549186.</summary>
    private const uint WireValue2147549186uint = 0x80010002;

    /// <summary>The independently specified wire fixture value 6.</summary>
    private const int WireValue6 = 6;

    /// <summary>The independently specified wire fixture value 8.</summary>
    private const int WireValue8 = 8;

    /// <summary>The independently specified wire fixture value 7.</summary>
    private const int WireValue7 = 7;

    /// <summary>The independently specified wire fixture value 1211.</summary>
    private const int WireValue1211 = 0x04bb;

    /// <summary>The independently specified wire fixture value 2415984640.</summary>
    private const uint WireValue2415984640uint = 0x90010000;

    /// <summary>The independently specified wire fixture value 2416050176.</summary>
    private const uint WireValue2416050176uint = 0x90020000;

    /// <summary>The independently specified wire fixture value 2416115712.</summary>
    private const uint WireValue2416115712uint = 0x90030000;

    /// <summary>The independently specified wire fixture value 2416312320.</summary>
    private const uint WireValue2416312320uint = 0x90060000;

    /// <summary>The independently specified wire fixture value 2416246784.</summary>
    private const uint WireValue2416246784uint = 0x90050000;

    /// <summary>The independently specified wire fixture value 171.</summary>
    private const int WireValue171 = 0xab;

    /// <summary>The independently specified wire fixture value 42.</summary>
    private const int WireValue42 = 42;

    /// <summary>The independently specified wire fixture value 291.</summary>
    private const int WireValue291 = 0x123;

    /// <summary>The independently specified wire fixture value 13.</summary>
    private const int WireValue13 = 13;

    /// <summary>The independently specified wire fixture value 55.</summary>
    private const int WireValue55 = 55;

    /// <summary>The independently specified wire fixture value 14.</summary>
    private const int WireValue14 = 14;

    /// <summary>The independently specified wire fixture value 100.</summary>
    private const int WireValue100 = 100;

    /// <summary>The independently specified wire fixture value 200.</summary>
    private const int WireValue200 = 200;

    /// <summary>The independently specified wire fixture value 1110.</summary>
    private const int WireValue1110 = 0x456;

    /// <summary>The independently specified wire fixture value 1236.</summary>
    private const int WireValue1236 = 0x04d4;

    /// <summary>Stores the UTF-8 bytes of the deterministic conformance credential.</summary>
    private static readonly byte[] PasswordBytes = "conformance-password"u8.ToArray();

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly uint[] WireFixture1 = [WireValue10, WireValue30];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly uint[] WireFixture2 = [WireValue20, 1];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly string[] WireFixture3 = ["\"Plant.DB\".Motor.Speed", "\"Plant.DB\".Samples[-1]", "\"Plant.DB\".Samples[0]"];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture4 = [0, WireValue4, 0];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture5 = new byte[WireValue4];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture6 = [0, WireValue18];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture7 = new byte[WireValue4];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture8 = [0, WireValue2];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture9 = [1, 1];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture10 = [1];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture11 = [0];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture12 = new byte[WireValue4];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture13 = [0];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly uint[] WireFixture14 = [0, WireValue201, WireValue1037, WireValue1000];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly uint[] WireFixture15 = [0, WireValue201, WireValue1037, WireValue1001];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture16 = [0, 0];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture17 = [0, 1];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture18 = [0, 1, WireValue16, WireValue2, WireValue20];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture19 = [WireValue16, WireValue2, WireValue20];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture20 = [0, 1];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture21 = new byte[WireValue4];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture22 = [0];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture23 = [WireValue32, WireValue4, 1];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture24 = [0, 1];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture25 = new byte[WireValue4];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly uint[] WireFixture26 = [WireValue233];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly uint[] WireFixture27 = [0, WireValue2316173313uint, WireValue2550, 1];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture28 = [0, 1];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture29 = [0, 0];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture30 = new byte[WireValue5];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture31 = [0];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture32 = new byte[WireValue4];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly uint[] WireFixture33 = [0, WireValue2316173313uint, WireValue2550, WireValue10, WireValue30];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly uint[] WireFixture34 = [0, WireValue2316173313uint, WireValue2550, WireValue20, 1];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture35 = [0, 1];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture36 = [0, WireValue2];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture37 = [0];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly uint[] WireFixture38 = [0, WireValue2316173313uint, WireValue2550, WireValue20, WireValue2];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture39 = [0, 1];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture40 = [0, 0];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly uint[] WireFixture41 = [0, WireValue2316173313uint, WireValue2550, WireValue10, WireValue30];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly uint[] WireFixture42 = [0, WireValue2316173313uint, WireValue2550, WireValue20, 1];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture43 = [1];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture44 = [WireValue2];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture45 = [0];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture46 = [WireValue2];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture47 = new byte[WireValue4];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture48 = [0, WireValue2];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture49 = [0];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly uint[] WireFixture50 = [0, WireValue2316173313uint, WireValue2550, WireValue20, WireValue2];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture51 = [1];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture52 = [0];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture53 = [WireValue3];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture54 = new byte[WireValue4];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture55 = [0, 0];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture56 = new byte[WireValue4];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture57 = [0];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture58 = new byte[WireValue4];

    /// <summary>Stores an immutable-by-convention independent wire fixture.</summary>
    private static readonly byte[] WireFixture59 = [0];

    /// <summary>Authenticates with TLS exporter material and resolves optimized nested and array symbols before batched read/write.</summary>
    /// <param name="cancellationToken">Cancels the conformance exchange.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Connect_ModernTls13_ResolvesSymbolsAndPreservesBatchErrors(CancellationToken cancellationToken)
    {
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(TimeSpan.FromSeconds(WireValue30));
        var token = bounded.Token;
        await using var peer = new PlusLoopbackPeer();
        await using var client = new S7SymbolicClient(Options(peer, true));
        var serving = ServeConnectionAsync(peer, true, token);
        await AwaitPeersAsync(client.ConnectAsync(token), serving);
        await TAssert.That(peer.HandshakeComplete).IsTrue();
        await TAssert.That(client.Session.SessionId).IsEqualTo(WireValue305419896uint);
        await TAssert.That(client.Session.SubscriptionSessionId).IsEqualTo(WireValue591751049uint);
        await TAssert.That(client.Session.ReadLimit).IsEqualTo(WireValue2);
        await TAssert.That(client.Session.WriteLimit).IsEqualTo(WireValue2);

        serving = ServeSymbolsAsync(peer, token);
        var speed = await client.ResolveAsync("\"Plant.DB\".Motor.Speed", token);
        var array = await client.ResolveAsync("\"Plant.DB\".Samples", token);
        var element = await client.ResolveAsync("\"Plant.DB\".Samples[-1]", token);
        await serving;
        await TAssert.That(speed.Address.AccessArea).IsEqualTo(WireValue2316173313uint);
        await TAssert.That(speed.Address.SubArea).IsEqualTo(WireValue2550uint);
        await TAssert.That(PlusLoopbackWire.Equal(speed.Address.LocalIds, WireFixture1)).IsTrue();
        await TAssert.That(speed.IsOptimized).IsTrue();
        await TAssert.That(speed.IsAccessible).IsTrue();
        await TAssert.That(speed.IsReadOnly).IsFalse();
        await TAssert.That(speed.SymbolCrc).IsEqualTo(WireValue287454020uint);
        await TAssert.That(array.ArrayLowerBounds[0]).IsEqualTo(-WireValue2);
        await TAssert.That(array.ArrayLengths[0]).IsEqualTo(WireValue3uint);
        await TAssert.That(PlusLoopbackWire.Equal(element.Address.LocalIds, WireFixture2)).IsTrue();

        serving = ServeGlobalBrowseAsync(peer, token);
        var symbols = await client.BrowseAsync(token);
        await serving;
        await TAssert.That(symbols.Length).IsEqualTo(WireValue2);
        await TAssert.That(Array.Exists(symbols, static symbol => symbol.Path == "\"Plant.DB\".\"Motor\".\"Speed\"")).IsTrue();
        await TAssert.That(Array.Exists(symbols, static symbol => symbol.Path == "\"Plant.DB\".\"Samples\"")).IsTrue();

        serving = ServeBatchReadsAsync(peer, token);
        var reads = await client.ReadManyAsync(WireFixture3, token);
        await serving;
        await TAssert.That(reads[0].Value!.Value).IsEqualTo(WireValue42uint);
        await TAssert.That(reads[1].ErrorCode).IsEqualTo(WireValue291ulong);
        await TAssert.That(reads[1].Value).IsNull();
        await TAssert.That(reads[WireValue2].Value!.Value).IsEqualTo(WireValue55uint);

        serving = ServeBatchWritesAsync(peer, token);
        var values = new Dictionary<string, S7SymbolicValue>
        {
            ["\"Plant.DB\".Motor.Speed"] = new(S7SymbolicDataType.UDInt, WireValue100uint),
            ["\"Plant.DB\".Samples[-1]"] = new(S7SymbolicDataType.UDInt, WireValue200uint),
            ["\"Plant.DB\".Samples[0]"] = new(S7SymbolicDataType.UDInt, WireValue300uint),
        };
        var writes = await client.WriteManyAsync(values, token);
        await serving;
        await TAssert.That(writes[0].IsSuccess).IsTrue();
        await TAssert.That(writes[1].ErrorCode).IsEqualTo(WireValue1110ulong);
        await TAssert.That(writes[WireValue2].IsSuccess).IsTrue();
        serving = ServeDisconnectAsync(peer, WireValue4, token);
        await AwaitPeersAsync(client.DisconnectAsync(token), serving);
    }

    /// <summary>Checks the legacy SHA-1 challenge response through the same genuine TLS 1.3 tunnel.</summary>
    /// <param name="cancellationToken">Cancels the conformance exchange.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Connect_LegacyChallenge_UsesSha1XorOverTls13(CancellationToken cancellationToken)
    {
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(TimeSpan.FromSeconds(WireValue30));
        await using var peer = new PlusLoopbackPeer();
        await using var client = new S7SymbolicClient(Options(peer, false));
        await AwaitPeersAsync(client.ConnectAsync(bounded.Token), ServeConnectionAsync(peer, false, bounded.Token));
        await TAssert.That(peer.HandshakeComplete).IsTrue();
        await TAssert.That(client.Session.IsConnected).IsTrue();
        await AwaitPeersAsync(client.DisconnectAsync(bounded.Token), ServeDisconnectAsync(peer, WireValue2, bounded.Token));
    }

    /// <summary>Rejects a mismatching certificate pin during a real TLS handshake before allocating a Plus session.</summary>
    /// <param name="cancellationToken">Cancels the conformance exchange.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Connect_WrongCertificatePin_RejectsSecureSession(CancellationToken cancellationToken)
    {
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(TimeSpan.FromSeconds(WireValue15));
        await using var peer = new PlusLoopbackPeer();
        await using var client = new S7SymbolicClient(new S7SymbolicConnectionOptions("127.0.0.1")
        {
            Port = peer.Port,
            CertificateSha256 = new('0', WireValue64),
            Timeout = TimeSpan.FromSeconds(WireValue5),
        });
        var serving = ObserveRejectedHandshakeAsync(peer, bounded.Token);
        Exception? rejection = null;
        try
        {
            await client.ConnectAsync(bounded.Token);
        }
        catch (Exception exception) when (exception is AuthenticationException or IOException)
        {
            rejection = exception;
        }

        await serving;
        await TAssert.That(rejection).IsNotNull();
        await TAssert.That(rejection is OperationCanceledException).IsFalse();
        await TAssert.That(peer.HandshakeComplete).IsFalse();
        await TAssert.That(client.Session.IsConnected).IsFalse();
        await TAssert.That(client.Session.SessionId).IsEqualTo(0U);
    }

    /// <summary>Observes both peer tasks and preserves client and server failures when both exchanges fault.</summary>
    /// <param name="client">The client operation.</param>
    /// <param name="server">The independent server exchange.</param>
    /// <returns>A task representing both completed exchanges.</returns>
    private static async Task AwaitPeersAsync(Task client, Task server)
    {
        try
        {
            await Task.WhenAll(client, server);
        }
        catch when (client.Exception is { } clientFailure && server.Exception is { } serverFailure)
        {
            throw new AggregateException(clientFailure, serverFailure);
        }
    }

    /// <summary>Creates options that require the ephemeral peer certificate and automatic firmware authentication selection.</summary>
    /// <param name="peer">The independent peer.</param>
    /// <param name="modern">Whether to supply modern username authentication.</param>
    /// <returns>The pinned connection options.</returns>
    private static S7SymbolicConnectionOptions Options(PlusLoopbackPeer peer, bool modern) => new("127.0.0.1")
    {
        Port = peer.Port,
        CertificateSha256 = peer.CertificatePin,
        Username = modern ? "operator" : string.Empty,
        Password = TestPassword,
        Timeout = TimeSpan.FromSeconds(WireValue10),
    };

    /// <summary>Exchanges session setup, advertised limits, and independently checked authentication.</summary>
    /// <param name="peer">The independent peer.</param>
    /// <param name="modern">Whether to advertise modern authentication.</param>
    /// <param name="token">Bounds the exchange.</param>
    /// <returns>A task representing the server workflow.</returns>
    private static async Task ServeConnectionAsync(PlusLoopbackPeer peer, bool modern, CancellationToken token)
    {
        await peer.AcceptAsync(token);
        var body = await peer.ReceiveAsync(WireValue1226, WireValue288, WireValue54, token);
        var expected = PlusLoopbackWire.Join(
            PlusLoopbackWire.Identifier(WireValue285),
            WireFixture4,
            WireFixture5,
            PlusLoopbackWire.Object(
                WireValue211,
                WireValue287,
                PlusLoopbackWire.Join(
                PlusLoopbackWire.Attribute(WireValue300, PlusLoopbackWire.Join(WireFixture6, PlusLoopbackWire.Identifier(WireValue2160314625uint))),
                PlusLoopbackWire.Object(WireValue211, WireValue255, []))),
            WireFixture7);
        await TAssert.That(PlusLoopbackWire.Equal(body, expected)).IsTrue();
        var version = PlusLoopbackWire.Version(modern);
        var createdResponse = PlusLoopbackWire.Join(
            WireFixture8,
            PlusLoopbackWire.Unsigned(WireValue305419896),
            PlusLoopbackWire.Unsigned(WireValue591751049),
            PlusLoopbackWire.Object(WireValue211, WireValue287, PlusLoopbackWire.Attribute(WireValue306, version)));
        await peer.RespondAsync(createdResponse, token);
        body = await peer.ReceiveAsync(WireValue1346, WireValue305419896, WireValue52, token);
        expected = PlusLoopbackWire.Join(
            PlusLoopbackWire.Identifier(WireValue305419896),
            WireFixture9,
            PlusLoopbackWire.Unsigned(WireValue306),
            WireFixture10,
            version,
            WireFixture11,
            PlusLoopbackWire.Qualifier(),
            WireFixture12);
        await TAssert.That(PlusLoopbackWire.Equal(body, expected)).IsTrue();
        await peer.RespondAsync(WireFixture13, token);
        await ServeResourcesAsync(peer, token);
        await ServeAuthenticationAsync(peer, modern, token);
    }

    /// <summary>Advertises independently checked session read/write resource limits.</summary>
    /// <param name="peer">The independent peer.</param>
    /// <param name="token">Bounds the exchange.</param>
    /// <returns>A task representing resource negotiation.</returns>
    private static async Task ServeResourcesAsync(PlusLoopbackPeer peer, CancellationToken token)
    {
        var body = await peer.ReceiveAsync(WireValue1356, WireValue305419896, WireValue52, token);
        var positions = await AddressesAsync(body, WireValue5);
        await TAssert.That(PlusLoopbackWire.Equal(positions[0], WireFixture14)).IsTrue();
        await TAssert.That(PlusLoopbackWire.Equal(positions[1], WireFixture15)).IsTrue();
        for (var index = WireValue2; index < WireValue5; index++)
        {
            uint[] expectedAddress = [0, WireValue201, WireValue1037, (uint)index - WireValue2];
            await TAssert.That(PlusLoopbackWire.Equal(positions[index], expectedAddress)).IsTrue();
        }

        var limits = new List<byte> { 0 };
        for (uint index = 1; index <= WireValue5; index++)
        {
            limits.Add((byte)index);
            limits.AddRange(PlusLoopbackWire.Number(index <= WireValue2 ? WireValue2uint : WireValue100uint));
        }

        limits.AddRange(WireFixture16);
        limits.AddRange(PlusLoopbackWire.Unsigned(peer.Integrity(1)));
        await peer.RespondAsync(limits.ToArray(), token);
    }

    /// <summary>Checks protection, challenge, and the authenticated controller response.</summary>
    /// <param name="peer">The independent peer.</param>
    /// <param name="modern">Whether to check TLS exporter authentication.</param>
    /// <param name="token">Bounds the exchange.</param>
    /// <returns>A task representing challenge authentication.</returns>
    private static async Task ServeAuthenticationAsync(PlusLoopbackPeer peer, bool modern, CancellationToken token)
    {
        await ReceiveAttributeAsync(peer, WireValue1842, WireValue2, token);
        await peer.RespondAsync(PlusLoopbackWire.Join(WireFixture17, PlusLoopbackWire.Number(WireValue3), PlusLoopbackWire.Unsigned(peer.Integrity(WireValue2))), token);
        await ReceiveAttributeAsync(peer, WireValue303, WireValue3, token);
        var challenge = new byte[WireValue20];
        for (var index = 0; index < challenge.Length; index++)
        {
            challenge[index] = (byte)(index + 1);
        }

        await peer.RespondAsync(PlusLoopbackWire.Join(WireFixture18, challenge, PlusLoopbackWire.Unsigned(peer.Integrity(WireValue3))), token);
        var body = await peer.ReceiveAsync(WireValue1266, WireValue305419896, WireValue52, token);
        var cursor = 0;
        await TAssert.That(PlusLoopbackWire.Fixed(body, ref cursor)).IsEqualTo(WireValue305419896uint);
        await TAssert.That(PlusLoopbackWire.Read(body, ref cursor)).IsEqualTo(1U);
        await TAssert.That(PlusLoopbackWire.Read(body, ref cursor)).IsEqualTo(modern ? WireValue1846uint : WireValue304uint);
        cursor = modern
            ? await VerifyModernAuthenticationAsync(peer, body, cursor, challenge)
            : await VerifyLegacyAuthenticationAsync(body, cursor, challenge);

        await TAssert.That(PlusLoopbackWire.Equal(PlusLoopbackWire.Slice(body, cursor), PlusLoopbackWire.Join(PlusLoopbackWire.Qualifier(), WireFixture20, WireFixture21))).IsTrue();
        await peer.RespondAsync(PlusLoopbackWire.Join(WireFixture22, PlusLoopbackWire.Unsigned(peer.Integrity(1))), token);
    }

    /// <summary>Decrypts and compares the modern username/password payload using only server-negotiated key material.</summary>
    /// <param name="peer">The independent peer.</param>
    /// <param name="body">The client's authentication body.</param>
    /// <param name="cursor">The typed value position.</param>
    /// <param name="challenge">The independent server challenge.</param>
    /// <returns>The first byte after the authentication value.</returns>
    private static async Task<int> VerifyModernAuthenticationAsync(PlusLoopbackPeer peer, byte[] body, int cursor, byte[] challenge)
    {
        await TAssert.That(body[cursor]).IsEqualTo((byte)0);
        cursor++;
        await TAssert.That(body[cursor]).IsEqualTo((byte)WireValue20);
        cursor++;
        await TAssert.That(PlusLoopbackWire.Read(body, ref cursor)).IsEqualTo(0U);
        var length = checked((int)PlusLoopbackWire.Read(body, ref cursor));
        var ciphertext = PlusLoopbackWire.Slice(body, cursor, length);
        var iv = PlusLoopbackWire.Slice(challenge, 0, WireValue16);
        var plaintext = PlusLoopbackWire.DecryptAuthentication(peer.Export(), iv, ciphertext);
        var expected = PlusLoopbackWire.Authentication("operator", PasswordBytes);
        await TAssert.That(PlusLoopbackWire.Equal(plaintext, expected)).IsTrue();
        return cursor + length;
    }

    /// <summary>Compares the legacy controller-mandated fingerprint XOR challenge payload.</summary>
    /// <param name="body">The client's authentication body.</param>
    /// <param name="cursor">The typed value position.</param>
    /// <param name="challenge">The independent server challenge.</param>
    /// <returns>The first byte after the authentication value.</returns>
    private static async Task<int> VerifyLegacyAuthenticationAsync(byte[] body, int cursor, byte[] challenge)
    {
        var digest = PlusLoopbackWire.Digest(PasswordBytes, true);
        for (var index = 0; index < digest.Length; index++)
        {
            digest[index] ^= challenge[index];
        }

        var expected = PlusLoopbackWire.Join(WireFixture19, digest);
        await TAssert.That(PlusLoopbackWire.Equal(PlusLoopbackWire.Slice(body, cursor, expected.Length), expected)).IsTrue();
        return cursor + expected.Length;
    }

    /// <summary>Checks an unprotected session attribute request byte-for-byte.</summary>
    /// <param name="peer">The independent peer.</param>
    /// <param name="attribute">The expected attribute.</param>
    /// <param name="integrity">The expected read integrity.</param>
    /// <param name="token">Bounds the exchange.</param>
    /// <returns>A task representing the verification.</returns>
    private static async Task ReceiveAttributeAsync(PlusLoopbackPeer peer, uint attribute, uint integrity, CancellationToken token)
    {
        var body = await peer.ReceiveAsync(WireValue1414, WireValue305419896, WireValue52, token);
        var expected = PlusLoopbackWire.Join(
            PlusLoopbackWire.Identifier(WireValue305419896),
            WireFixture23,
            PlusLoopbackWire.Unsigned(attribute),
            PlusLoopbackWire.Qualifier(),
            WireFixture24,
            PlusLoopbackWire.Unsigned(integrity),
            WireFixture25);
        await TAssert.That(PlusLoopbackWire.Equal(body, expected)).IsTrue();
    }

    /// <summary>Serves root discovery and optimized nested/array type metadata.</summary>
    /// <param name="peer">The independent peer.</param>
    /// <param name="token">Bounds the exchange.</param>
    /// <returns>A task representing metadata discovery.</returns>
    private static async Task ServeSymbolsAsync(PlusLoopbackPeer peer, CancellationToken token)
    {
        var rootObject = PlusLoopbackWire.Object(
            WireValue2316173313uint,
            WireValue2574,
            PlusLoopbackWire.Attribute(WireValue233, PlusLoopbackWire.Text("Plant.DB")));
        await ExploreAsync(peer, WireValue3, true, WireFixture26, WireValue4, rootObject, token);
        var body = await peer.ReceiveAsync(WireValue1356, WireValue305419896, WireValue52, token);
        var addresses = await AddressesAsync(body, 1);
        await TAssert.That(PlusLoopbackWire.Equal(addresses[0], WireFixture27)).IsTrue();
        await peer.RespondAsync(PlusLoopbackWire.Join(WireFixture28, PlusLoopbackWire.Number(WireValue2147549185uint), WireFixture29, PlusLoopbackWire.Unsigned(peer.Integrity(WireValue5))), token);
        var rootMembers = PlusLoopbackWire.Join(
            PlusLoopbackWire.Member(WireValue10, WireValue5, WireValue2147549186uint),
            PlusLoopbackWire.Member(WireValue20, WireValue3, 0));
        var metadata = PlusLoopbackWire.Metadata(rootMembers, "Motor", "Samples");
        await ExploreAsync(peer, WireValue2147549185uint, false, [], WireValue6, PlusLoopbackWire.Object(WireValue2147549185uint, 0, metadata), token);
        metadata = PlusLoopbackWire.Metadata(PlusLoopbackWire.Member(WireValue30, WireValue8, 0), "Speed");
        await ExploreAsync(peer, WireValue2147549186uint, false, [], WireValue7, PlusLoopbackWire.Object(WireValue2147549186uint, 0, metadata), token);
    }

    /// <summary>Checks exploration flags, attributes, integrity, and requested identity.</summary>
    /// <param name="peer">The independent peer.</param>
    /// <param name="id">The requested identity.</param>
    /// <param name="recursive">The expected recursive flag.</param>
    /// <param name="attributes">The expected attribute list.</param>
    /// <param name="integrity">The expected request integrity.</param>
    /// <param name="objects">The independently encoded response objects.</param>
    /// <param name="token">Bounds the exchange.</param>
    /// <returns>A task representing the exchange.</returns>
    private static async Task ExploreAsync(PlusLoopbackPeer peer, uint id, bool recursive, uint[] attributes, uint integrity, byte[] objects, CancellationToken token)
    {
        var body = await peer.ReceiveAsync(WireValue1211, WireValue305419896, WireValue52, token);
        var expected = PlusLoopbackWire.Join(
            PlusLoopbackWire.Identifier(id),
            [0, recursive ? (byte)1 : (byte)0, 1, 0, 0],
            PlusLoopbackWire.Unsigned((uint)attributes.Length),
            PlusLoopbackWire.Identifiers(attributes),
            PlusLoopbackWire.Unsigned(integrity),
            WireFixture30);
        await TAssert.That(PlusLoopbackWire.Equal(body, expected)).IsTrue();
        await peer.RespondAsync(PlusLoopbackWire.Join(WireFixture31, PlusLoopbackWire.Identifier(id), PlusLoopbackWire.Unsigned(peer.Integrity(integrity)), objects, WireFixture32), token);
    }

    /// <summary>Returns empty native-area declarations while the public browser reuses the discovered DB type cache.</summary>
    /// <param name="peer">The independent peer.</param>
    /// <param name="token">Bounds the exchange.</param>
    /// <returns>A task representing native-area exploration.</returns>
    private static async Task ServeGlobalBrowseAsync(PlusLoopbackPeer peer, CancellationToken token)
    {
        uint[] ids = [WireValue2415984640uint, WireValue2416050176uint, WireValue2416115712uint, WireValue2416312320uint, WireValue2416246784uint];
        for (var index = 0; index < ids.Length; index++)
        {
            byte[] metadata = [WireValue171, 0, WireValue4, 1, 0, 0, 0, 0, 0];
            await ExploreAsync(peer, ids[index], false, [], (uint)index + WireValue8, PlusLoopbackWire.Object(ids[index], 0, metadata), token);
        }
    }

    /// <summary>Checks that negotiated read limits split requests and retain per-item errors.</summary>
    /// <param name="peer">The independent peer.</param>
    /// <param name="token">Bounds the exchange.</param>
    /// <returns>A task representing batched reads.</returns>
    private static async Task ServeBatchReadsAsync(PlusLoopbackPeer peer, CancellationToken token)
    {
        var body = await peer.ReceiveAsync(WireValue1356, WireValue305419896, WireValue52, token);
        var addresses = await AddressesAsync(body, WireValue2);
        await TAssert.That(PlusLoopbackWire.Equal(addresses[0], WireFixture33)).IsTrue();
        await TAssert.That(PlusLoopbackWire.Equal(addresses[1], WireFixture34)).IsTrue();
        var firstResponse = PlusLoopbackWire.Join(
            WireFixture35,
            PlusLoopbackWire.Number(WireValue42),
            WireFixture36,
            PlusLoopbackWire.Unsigned(WireValue291),
            WireFixture37,
            PlusLoopbackWire.Unsigned(peer.Integrity(WireValue13)));
        await peer.RespondAsync(firstResponse, token);
        body = await peer.ReceiveAsync(WireValue1356, WireValue305419896, WireValue52, token);
        addresses = await AddressesAsync(body, 1);
        await TAssert.That(PlusLoopbackWire.Equal(addresses[0], WireFixture38)).IsTrue();
        await peer.RespondAsync(PlusLoopbackWire.Join(WireFixture39, PlusLoopbackWire.Number(WireValue55), WireFixture40, PlusLoopbackWire.Unsigned(peer.Integrity(WireValue14))), token);
    }

    /// <summary>Checks that negotiated write limits split requests and preserve their typed payloads.</summary>
    /// <param name="peer">The independent peer.</param>
    /// <param name="token">Bounds the exchange.</param>
    /// <returns>A task representing batched writes.</returns>
    private static async Task ServeBatchWritesAsync(PlusLoopbackPeer peer, CancellationToken token)
    {
        var body = await peer.ReceiveAsync(WireValue1346, WireValue305419896, WireValue52, token);
        var addresses = await AddressesAsync(body, WireValue2, false);
        await TAssert.That(PlusLoopbackWire.Equal(addresses[0], WireFixture41)).IsTrue();
        await TAssert.That(PlusLoopbackWire.Equal(addresses[1], WireFixture42)).IsTrue();
        var expectedValues = PlusLoopbackWire.Join(
            WireFixture43,
            PlusLoopbackWire.Number(WireValue100),
            WireFixture44,
            PlusLoopbackWire.Number(WireValue200),
            WireFixture45,
            PlusLoopbackWire.Qualifier(),
            WireFixture46,
            WireFixture47);
        await TAssert.That(PlusLoopbackWire.Equal(PlusLoopbackWire.Slice(body, AddressEnd(body)), expectedValues)).IsTrue();
        await peer.RespondAsync(PlusLoopbackWire.Join(WireFixture48, PlusLoopbackWire.Unsigned(WireValue1110), WireFixture49, PlusLoopbackWire.Unsigned(peer.Integrity(WireValue2))), token);
        body = await peer.ReceiveAsync(WireValue1346, WireValue305419896, WireValue52, token);
        addresses = await AddressesAsync(body, 1, false);
        await TAssert.That(PlusLoopbackWire.Equal(addresses[0], WireFixture50)).IsTrue();
        expectedValues = PlusLoopbackWire.Join(WireFixture51, PlusLoopbackWire.Number(WireValue300), WireFixture52, PlusLoopbackWire.Qualifier(), WireFixture53, WireFixture54);
        await TAssert.That(PlusLoopbackWire.Equal(PlusLoopbackWire.Slice(body, AddressEnd(body)), expectedValues)).IsTrue();
        await peer.RespondAsync(PlusLoopbackWire.Join(WireFixture55, PlusLoopbackWire.Unsigned(peer.Integrity(WireValue3))), token);
    }

    /// <summary>Independently decodes and validates a multi-address request.</summary>
    /// <param name="body">The request body.</param>
    /// <param name="count">The expected item count.</param>
    /// <param name="reading">Whether to check the read qualifier and trailer.</param>
    /// <returns>The independently decoded address fields.</returns>
    private static async Task<uint[][]> AddressesAsync(byte[] body, int count, bool reading = true)
    {
        var cursor = 0;
        await TAssert.That(PlusLoopbackWire.Fixed(body, ref cursor)).IsEqualTo(0U);
        await TAssert.That(PlusLoopbackWire.Read(body, ref cursor)).IsEqualTo((uint)count);
        var fields = PlusLoopbackWire.Read(body, ref cursor);
        uint actualFields = 0;
        var addresses = new uint[count][];
        for (var item = 0; item < count; item++)
        {
            var crc = PlusLoopbackWire.Read(body, ref cursor);
            var area = PlusLoopbackWire.Read(body, ref cursor);
            var ids = PlusLoopbackWire.Read(body, ref cursor);
            actualFields += ids + WireValue3;
            var address = new List<uint> { crc, area };
            for (var field = 0; field < ids; field++)
            {
                address.Add(PlusLoopbackWire.Read(body, ref cursor));
            }

            addresses[item] = address.ToArray();
        }

        await TAssert.That(fields).IsEqualTo(actualFields);
        if (reading)
        {
            var qualifier = PlusLoopbackWire.Qualifier();
            await TAssert.That(PlusLoopbackWire.Equal(PlusLoopbackWire.Slice(body, cursor, qualifier.Length), qualifier)).IsTrue();
            cursor += qualifier.Length;
            _ = PlusLoopbackWire.Read(body, ref cursor);
            await TAssert.That(PlusLoopbackWire.Equal(PlusLoopbackWire.Slice(body, cursor), WireFixture56)).IsTrue();
        }

        return addresses;
    }

    /// <summary>Finds the typed-value section after independently walking an address list.</summary>
    /// <param name="body">The multi-address request.</param>
    /// <returns>The first byte after the addresses.</returns>
    private static int AddressEnd(byte[] body)
    {
        var cursor = WireValue4;
        var count = PlusLoopbackWire.Read(body, ref cursor);
        _ = PlusLoopbackWire.Read(body, ref cursor);
        for (var item = 0; item < count; item++)
        {
            _ = PlusLoopbackWire.Read(body, ref cursor);
            _ = PlusLoopbackWire.Read(body, ref cursor);
            var fields = PlusLoopbackWire.Read(body, ref cursor);
            for (var field = 0; field < fields; field++)
            {
                _ = PlusLoopbackWire.Read(body, ref cursor);
            }
        }

        return cursor;
    }

    /// <summary>Checks deletion of the allocated session during graceful disconnect.</summary>
    /// <param name="peer">The independent peer.</param>
    /// <param name="requestIntegrity">The expected mutation counter retained in the terminal request.</param>
    /// <param name="token">Bounds the exchange.</param>
    /// <returns>A task representing graceful disconnect.</returns>
    private static async Task ServeDisconnectAsync(PlusLoopbackPeer peer, uint requestIntegrity, CancellationToken token)
    {
        var body = await peer.ReceiveAsync(WireValue1236, WireValue305419896, WireValue52, token);
        var expected = PlusLoopbackWire.Join(
            PlusLoopbackWire.Identifier(WireValue305419896),
            WireFixture57,
            PlusLoopbackWire.Qualifier(),
            PlusLoopbackWire.Unsigned(requestIntegrity),
            WireFixture58);
        await TAssert.That(PlusLoopbackWire.Equal(body, expected)).IsTrue();
        await peer.RespondAsync(PlusLoopbackWire.Join(WireFixture59, PlusLoopbackWire.Identifier(WireValue305419896)), token);
    }

    /// <summary>Observes socket closure or a TLS alert caused by certificate rejection.</summary>
    /// <param name="peer">The independent peer.</param>
    /// <param name="token">Bounds the exchange.</param>
    /// <returns>A task representing the rejected handshake observation.</returns>
    private static async Task ObserveRejectedHandshakeAsync(PlusLoopbackPeer peer, CancellationToken token)
    {
        try
        {
            await peer.AcceptAsync(token);
        }
        catch (IOException)
        {
            // A rejected certificate closes the COTP tunnel before the TLS Finished message.
        }
    }
}
