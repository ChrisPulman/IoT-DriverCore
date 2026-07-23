// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
using IoT.DriverCore.OmronPlcRx.Core;
using IoT.DriverCore.OmronPlcRx.Core.Channels;
using IoT.DriverCore.OmronPlcRx.Enums;
using TUnit.Core;

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>Exercises injected connection constructor, initialization, and single-value convenience paths.</summary>
public sealed class OmronConnectionResidualCoverageTests
{
    /// <summary>Gets the local FINS node.</summary>
    private const byte LocalNode = 1;

    /// <summary>Gets the remote FINS node.</summary>
    private const byte RemoteNode = 2;

    /// <summary>Gets the deterministic timeout.</summary>
    private const int TimeoutMilliseconds = 100;

    /// <summary>Gets the deterministic address.</summary>
    private const ushort Address = 10;

    /// <summary>Gets the deterministic bit index.</summary>
    private const byte BitIndex = 3;

    /// <summary>Gets the first invalid bit index.</summary>
    private const byte InvalidBitIndex = 16;

    /// <summary>Gets the last valid bit index.</summary>
    private const byte LastBitIndex = 15;

    /// <summary>Gets the first invalid work-memory address.</summary>
    private const ushort InvalidWorkAddress = 512;

    /// <summary>Gets the first invalid holding-memory address.</summary>
    private const ushort InvalidHoldingAddress = 1_536;

    /// <summary>Gets the first invalid CJ2 auxiliary-memory address.</summary>
    private const ushort InvalidCj2AuxiliaryAddress = 11_536;

    /// <summary>Gets the first invalid NX data-memory address.</summary>
    private const ushort InvalidNxDataMemoryAddress = 16_000;

    /// <summary>Verifies injected constructor guards, model limits, and initialized short circuit.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Connection_ValidatesInjectedOptionsAndModelLimitsAsync()
    {
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(
                () => new OmronPLCConnection(
                    null!,
                    new CoreProtocolCoverageTests.TestChannel(),
                    PlcType.CJ2,
                    null,
                    null,
                    true)));
        await AssertInvalidOptionsAsync(CreateOptions(0, 0));
        await AssertInvalidOptionsAsync(CreateOptions(TimeoutMilliseconds, -1));

        using var cp1 = CreateConnection(new CoreProtocolCoverageTests.TestChannel(), PlcType.CP1, true);
        using var nj = CreateConnection(new CoreProtocolCoverageTests.TestChannel(), PlcType.NJ101, true);
        await cp1.InitializeAsync(CancellationToken.None);

        await Assert.That(cp1.IsCSeries).IsTrue();
        await Assert.That(cp1.IsNSeries).IsFalse();
        await Assert.That(nj.IsNSeries).IsTrue();
        await Assert.That(nj.IsCSeries).IsFalse();
        await Assert.That(cp1.MaximumReadWordLength < nj.MaximumReadWordLength).IsTrue();
        await Assert.That(cp1.MaximumWriteWordLength < nj.MaximumWriteWordLength).IsTrue();
    }

    /// <summary>Verifies channel initialization exceptions are translated consistently.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Connection_TranslatesInitializationFailuresAsync()
    {
        await AssertInitializationFailureAsync(new ObjectDisposedException("channel"));
        await AssertInitializationFailureAsync(new TimeoutException("timeout"));
        await AssertInitializationFailureAsync(
            new SocketException((int)SocketError.ConnectionRefused));
    }

    /// <summary>Verifies the single-bit and single-word convenience operations.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Connection_ProcessesSingleValueConvenienceOperationsAsync()
    {
        var channel = new CoreProtocolCoverageTests.TestChannel();
        using var connection = CreateConnection(channel, PlcType.CJ2, true);
        channel.SetResponseData([1]);
        var bit = await connection.ReadBitAsync(
            Address,
            BitIndex,
            MemoryBitDataType.CommonIO,
            CancellationToken.None);
        channel.SetResponseData([0, RemoteNode]);
        var word = await connection.ReadWordAsync(
            Address,
            MemoryWordDataType.DataMemory,
            CancellationToken.None);
        channel.SetResponseData([]);
        var writeBit = await connection.WriteBitAsync(
            true,
            Address,
            BitIndex,
            MemoryBitDataType.CommonIO,
            CancellationToken.None);
        var writeWord = await connection.WriteWordAsync(
            RemoteNode,
            Address,
            MemoryWordDataType.DataMemory,
            CancellationToken.None);

        await Assert.That(bit.Values.Single()).IsTrue();
        await Assert.That(word.Values.Single()).IsEqualTo(RemoteNode);
        await Assert.That(writeBit.PacketsSent).IsEqualTo(1);
        await Assert.That(writeWord.PacketsSent).IsEqualTo(1);
    }

    /// <summary>Verifies write validation rejects invalid word areas and start addresses.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Connection_RejectsInvalidWordWriteAreaAndAddressAsync()
    {
        using var connection = CreateConnection(
            new CoreProtocolCoverageTests.TestChannel(),
            PlcType.CJ2,
            true);
        await AssertThrowsAsync<ArgumentException>(
            () => connection.WriteWordsAsync(
                [1],
                Address,
                (MemoryWordDataType)byte.MaxValue,
                CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => connection.WriteWordsAsync(
                [LocalNode, RemoteNode],
                ushort.MaxValue,
                MemoryWordDataType.DataMemory,
                CancellationToken.None));
    }

    /// <summary>Verifies the option-only constructor composes each supported transport.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Connection_ComposesConfiguredTransportChannelsAsync()
    {
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(() => new OmronPLCConnection(null!)));

        using var tcp = new OmronPLCConnection(
            new OmronConnectionOptions(
                LocalNode,
                RemoteNode,
                ConnectionMethod.TCP,
                IPAddress.Loopback.ToString()));
        using var udp = new OmronPLCConnection(
            new OmronConnectionOptions(
                LocalNode,
                RemoteNode,
                ConnectionMethod.UDP,
                IPAddress.Loopback.ToString()));
        using var serial = new OmronPLCConnection(
            new OmronConnectionOptions(
                LocalNode,
                RemoteNode,
                ConnectionMethod.Serial,
                "SIM-COM"));

        await Assert.That(tcp.Channel).IsTypeOf<TCPChannel>();
        await Assert.That(udp.Channel).IsTypeOf<UDPChannel>();
        await Assert.That(serial.Channel).IsTypeOf<SerialHostLinkFinsChannel>();
    }

    /// <summary>Verifies every bit validation area, boundary, and argument guard.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Connection_ValidatesEveryBitMemoryAreaAsync()
    {
        var channel = new CoreProtocolCoverageTests.TestChannel();
        using var cj2 = CreateConnection(channel, PlcType.CJ2, true);
        foreach (var dataType in new[]
        {
            MemoryBitDataType.DataMemory,
            MemoryBitDataType.CommonIO,
            MemoryBitDataType.Work,
            MemoryBitDataType.Holding,
            MemoryBitDataType.Auxiliary,
        })
        {
            channel.SetResponseData([]);
            var result = await cj2.WriteBitsAsync(
                [true],
                Address,
                0,
                dataType,
                CancellationToken.None);
            await Assert.That(result.PacketsSent).IsEqualTo(1);
        }

        await AssertBitArgumentFailuresAsync(cj2);

        using var cp1 = CreateConnection(new CoreProtocolCoverageTests.TestChannel(), PlcType.CP1, true);
        await AssertThrowsAsync<ArgumentException>(
            () => cp1.WriteBitsAsync([true], Address, 0, MemoryBitDataType.DataMemory, CancellationToken.None));
        using var nx = CreateConnection(new CoreProtocolCoverageTests.TestChannel(), PlcType.NX1P2, true);
        await AssertThrowsAsync<ArgumentException>(
            () => nx.WriteBitsAsync([true], Address, 0, MemoryBitDataType.Auxiliary, CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => nx.WriteBitsAsync(
                [true],
                InvalidNxDataMemoryAddress,
                0,
                MemoryBitDataType.DataMemory,
                CancellationToken.None));
    }

    /// <summary>Verifies every supported word-memory address branch.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Connection_ValidatesEveryWordMemoryAreaAsync()
    {
        var channel = new CoreProtocolCoverageTests.TestChannel();
        using var cj2 = CreateConnection(channel, PlcType.CJ2, true);
        foreach (var dataType in new[]
        {
            MemoryWordDataType.DataMemory,
            MemoryWordDataType.CommonIO,
            MemoryWordDataType.Work,
            MemoryWordDataType.Holding,
            MemoryWordDataType.Auxiliary,
        })
        {
            channel.SetResponseData([]);
            var result = await cj2.WriteWordsAsync(
                [1],
                Address,
                dataType,
                CancellationToken.None);
            await Assert.That(result.PacketsSent).IsEqualTo(1);
        }

        using var nx = CreateConnection(new CoreProtocolCoverageTests.TestChannel(), PlcType.NX1P2, true);
        await AssertThrowsAsync<ArgumentException>(
            () => nx.WriteWordsAsync([1], Address, MemoryWordDataType.Auxiliary, CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => nx.WriteWordsAsync(
                [1],
                InvalidNxDataMemoryAddress,
                MemoryWordDataType.DataMemory,
                CancellationToken.None));
    }

    /// <summary>Asserts argument and CJ2 address failures for bit writes.</summary>
    /// <param name="connection">Initialized CJ2 connection.</param>
    /// <returns>A task that represents the assertions.</returns>
    private static async Task AssertBitArgumentFailuresAsync(OmronPLCConnection connection)
    {
        await AssertThrowsAsync<ArgumentNullException>(
            () => connection.WriteBitsAsync(null!, Address, 0, MemoryBitDataType.CommonIO, CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => connection.WriteBitsAsync(
                [true],
                Address,
                InvalidBitIndex,
                MemoryBitDataType.CommonIO,
                CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => connection.WriteBitsAsync([], Address, 0, MemoryBitDataType.CommonIO, CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => connection.WriteBitsAsync(
                [true, false],
                Address,
                LastBitIndex,
                MemoryBitDataType.CommonIO,
                CancellationToken.None));
        await AssertThrowsAsync<ArgumentException>(
            () => connection.WriteBitsAsync(
                [true],
                Address,
                0,
                (MemoryBitDataType)byte.MaxValue,
                CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => connection.WriteBitsAsync(
                [true],
                InvalidWorkAddress,
                0,
                MemoryBitDataType.Work,
                CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => connection.WriteBitsAsync(
                [true],
                InvalidHoldingAddress,
                0,
                MemoryBitDataType.Holding,
                CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => connection.WriteBitsAsync(
                [true],
                InvalidCj2AuxiliaryAddress,
                0,
                MemoryBitDataType.Auxiliary,
                CancellationToken.None));
    }

    /// <summary>Asserts invalid injected options fail construction.</summary>
    /// <param name="options">Invalid options.</param>
    /// <returns>A task that represents the assertion.</returns>
    private static Task AssertInvalidOptionsAsync(OmronConnectionOptions options) =>
        AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => Task.Run(
                () => new OmronPLCConnection(
                    options,
                    new CoreProtocolCoverageTests.TestChannel(),
                    PlcType.CJ2,
                    null,
                    null,
                    true)));

    /// <summary>Asserts one injected channel initialization failure is translated.</summary>
    /// <param name="exception">Initialization exception.</param>
    /// <returns>A task that represents the assertion.</returns>
    private static async Task AssertInitializationFailureAsync(Exception exception)
    {
        var channel = new CoreProtocolCoverageTests.TestChannel
        {
            InitializeException = exception,
        };
        using var connection = CreateConnection(channel, PlcType.CJ2, false);
        await AssertThrowsAsync<OmronPLCException>(
            () => connection.InitializeAsync(CancellationToken.None));
    }

    /// <summary>Creates valid connection options.</summary>
    /// <param name="timeout">Request timeout.</param>
    /// <param name="retries">Request retry count.</param>
    /// <returns>Valid deterministic options.</returns>
    private static OmronConnectionOptions CreateOptions(int timeout, int retries) =>
        new(
            LocalNode,
            RemoteNode,
            ConnectionMethod.UDP,
            IPAddress.Loopback.ToString())
        {
            Timeout = timeout,
            Retries = retries,
        };

    /// <summary>Creates an injected connection.</summary>
    /// <param name="channel">Deterministic channel.</param>
    /// <param name="plcType">PLC model family.</param>
    /// <param name="initialized">Whether the connection starts initialized.</param>
    /// <returns>The connection.</returns>
    private static OmronPLCConnection CreateConnection(
        CoreProtocolCoverageTests.TestChannel channel,
        PlcType plcType,
        bool initialized) =>
        new(
            CreateOptions(TimeoutMilliseconds, 0),
            channel,
            plcType,
            plcType.ToString(),
            "1.0",
            initialized);

    /// <summary>Captures and verifies an asynchronous exception.</summary>
    /// <typeparam name="TException">Expected exception type.</typeparam>
    /// <param name="action">Action to invoke.</param>
    /// <returns>A task that represents the assertion.</returns>
    private static async Task AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException exception)
        {
            await Assert.That(exception).IsNotNull();
            return;
        }

        throw new InvalidOperationException($"Expected {nameof(TException)}.");
    }
}
