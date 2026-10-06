// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;
using IoT.Driver.S7PlcRx.Symbolic;
using IoT.Driver.S7PlcRx.Symbolic.Protocol;
using Org.BouncyCastle.Utilities.Encoders;
using TAssert = TUnit.Assertions.Assert;

namespace IoT.Driver.S7PlcRx.Tests.Symbolic;

/// <summary>Verifies legal empty native type metadata during symbolic browsing.</summary>
public sealed class PlusEmptyTypeTests
{
    /// <summary>The global program scope requested by a public browse.</summary>
    private const uint ProgramScope = 3;

    /// <summary>The native input area type relation.</summary>
    private const uint InputAreaType = 0x90010000;

    /// <summary>The native output area type relation.</summary>
    private const uint OutputAreaType = 0x90020000;

    /// <summary>The native marker area type relation.</summary>
    private const uint MarkerAreaType = 0x90030000;

    /// <summary>The native counter area type relation.</summary>
    private const uint CounterAreaType = 0x90060000;

    /// <summary>The native timer area type relation.</summary>
    private const uint TimerAreaType = 0x90050000;

    /// <summary>The number of scopes explored by a complete global browse.</summary>
    private const int ExpectedExploreCount = 6;

    /// <summary>The expected global and native scopes requested during browsing.</summary>
    private static readonly uint[] ExpectedExploredScopes =
    [
        ProgramScope,
        InputAreaType,
        OutputAreaType,
        MarkerAreaType,
        CounterAreaType,
        TimerAreaType
    ];

    /// <summary>Verifies that empty native area metadata produces an empty browse result.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Browse_EmptyNativeTypes_ReturnsNoSymbols()
    {
        var transport = new PlusSessionTests.FakeTransport();
        await using var session = new S7PlusSession(new S7SymbolicConnectionOptions("fake"), transport);
        await using var client = new S7SymbolicClient(session);
        await client.ConnectAsync();

        var explored = new List<uint>();
        var peer = new BrowsePeer(explored);
        transport.ReplyOverride = peer.ExploreReply;

        var symbols = await client.BrowseAsync();

        await TAssert.That(symbols).IsEmpty();
        await TAssert.That(explored.Count).IsEqualTo(ExpectedExploreCount);
        await TAssert.That(explored).IsEquivalentTo(ExpectedExploredScopes);
    }

    /// <summary>Verifies that an empty type with no names decodes as an empty member list.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Decode_EmptyTypeAndNames_ReturnsNoMembers()
    {
        var members = PlusTypeDecoder.Decode([], []);

        await TAssert.That(members).IsEmpty();
    }

    /// <summary>Verifies that names without matching encoded members remain malformed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Decode_EmptyTypeWithNames_ThrowsInvalidData()
    {
        await TAssert.That(static () => PlusTypeDecoder.Decode([], ["Unexpected"])).Throws<InvalidDataException>();
    }

    /// <summary>Encodes deterministic Explore replies while recording the requested scopes.</summary>
    /// <param name="explored">The scopes observed in request order.</param>
    private sealed class BrowsePeer(List<uint> explored)
    {
        /// <summary>The response protocol version.</summary>
        private const byte ProtocolVersion = 2;

        /// <summary>The offset of an operation body in its request packet.</summary>
        private const int RequestBodyOffset = 14;

        /// <summary>The number of prefix bytes before the request operation identifier.</summary>
        private const int RequestPrefixBytes = 3;

        /// <summary>The number of Explore request option bytes.</summary>
        private const int RequestOptionBytes = 4;

        /// <summary>The number of reserved bytes between the function and sequence fields.</summary>
        private const int ReservedHeaderBytes = 2;

        /// <summary>The native Explore operation identifier.</summary>
        private const ushort ExploreFunction = 0x04bb;

        /// <summary>The canonical scope returned by the simulated controller.</summary>
        private const uint CanonicalScope = 1;

        /// <summary>The native type object class returned by Explore.</summary>
        private const uint NativeTypeObjectClass = 511;

        /// <summary>Encodes a valid Explore response with empty native type metadata.</summary>
        /// <param name="request">The request packet.</param>
        /// <returns>The response packet.</returns>
        internal PlusMessage ExploreReply(PlusMessage request)
        {
            var header = new PlusReader(request.Payload);
            header.Skip(RequestPrefixBytes);
            var function = header.UInt16();
            header.Skip(ReservedHeaderBytes);
            var sequence = header.UInt16();
            if (function != ExploreFunction)
            {
                throw new InvalidDataException("The browse issued an unexpected operation.");
            }

            var reader = new PlusReader(PlusLoopbackWire.Slice(request.Payload, RequestBodyOffset));
            var id = reader.UInt32();
            _ = reader.VarUInt32();
            reader.Skip(RequestOptionBytes);
            var attributeCount = reader.VarUInt32();
            for (var index = 0; index < attributeCount; index++)
            {
                _ = reader.VarUInt32();
            }

            var integrity = reader.VarUInt32();
            explored.Add(id);

            var objects = id == ProgramScope
                ? Array.Empty<byte>()
                : PlusLoopbackWire.Object(id, NativeTypeObjectClass, []);
            var body = PlusLoopbackWire.Join(
                Hex.Decode("00"),
                PlusLoopbackWire.Identifier(CanonicalScope),
                PlusLoopbackWire.Unsigned(sequence + integrity),
                objects,
                new byte[4]);
            return new(ProtocolVersion, PlusLoopbackWire.Join(
                Hex.Decode("320000"),
                [(byte)(function >> 8), (byte)function, 0, 0, (byte)(sequence >> 8), (byte)sequence, 0x34],
                body));
        }
    }
}
