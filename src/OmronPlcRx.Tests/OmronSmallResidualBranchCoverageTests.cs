// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net.Sockets;
using System.Reflection;
using IoT.DriverCore.OmronPlcRx.Core;
using IoT.DriverCore.OmronPlcRx.Core.Enums;
using IoT.DriverCore.OmronPlcRx.Core.Responses;
using IoT.DriverCore.OmronPlcRx.Enums;
using TUnit.Core;

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>Closes small defensive branches across Omron value and metadata types.</summary>
public sealed class OmronSmallResidualBranchCoverageTests
{
    /// <summary>Gets the deterministic local FINS node identifier.</summary>
    private const byte LocalNode = 1;

    /// <summary>Gets the deterministic remote FINS node identifier.</summary>
    private const byte RemoteNode = 2;

    /// <summary>Gets the odd source string length used by the codec probe.</summary>
    private const int OddStringLength = 3;

    /// <summary>Gets the expected word count for the odd-length string probe.</summary>
    private const int ExpectedStringWordCount = 2;

    /// <summary>Gets the private FINS function-name helper.</summary>
    private const string SubFunctionNameMethod = "GetSubFunctionCodeName";

    /// <summary>Gets the reserved maximum FINS node identifier.</summary>
    private const byte ReservedNode = byte.MaxValue;

    /// <summary>Verifies constructor null guards and reserved metadata values.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Constructors_RejectNullAndReservedValuesAsync()
    {
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(
                () => _ = new OmronConnectionOptions(
                    LocalNode,
                    RemoteNode,
                    ConnectionMethod.UDP,
                    null!)));
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(
                () => _ = new OmronLogicalBatchItem(0, null!, "D0", typeof(int), null)));
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(
                () => _ = new OmronLogicalBatchItem(0, "Tag", null!, typeof(int), null)));
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(
                () => _ = new OmronLogicalBatchItem(0, "Tag", "D0", null!, null)));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => Task.Run(
                () => OmronPLCConnectionMetadata.ValidateNodeIdentifiers(
                    ReservedNode,
                    LocalNode,
                    ConnectionMethod.UDP)));

        await Assert.That(OmronPLCConnectionMetadata.GetPLCType("NX102"))
            .IsEqualTo(PlcType.NX102);
        await Assert.That(OmronPLCConnectionMetadata.GetPLCType("X"))
            .IsEqualTo(PlcType.Unknown);
    }

    /// <summary>Verifies socket setup normalizes the platform linger state.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TcpSocketConfiguration_NormalizesPlatformLingerStateAsync()
    {
        using var socket = new Socket(
            AddressFamily.InterNetwork,
            SocketType.Stream,
            ProtocolType.Tcp);

        TcpSocketConfiguration.ConfigureZeroLinger(socket);

        await Assert.That(socket.LingerState).IsNotNull();
        await Assert.That(socket.LingerState!.Enabled).IsTrue();
        await Assert.That(socket.LingerState.LingerTime).IsEqualTo(0);
    }

    /// <summary>Verifies FINS function naming includes every defined and unknown function code.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FinsResponse_MapsEveryFunctionCodeAndUnknownValueAsync()
    {
        var method = typeof(FINSResponse).GetMethod(
            SubFunctionNameMethod,
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("FINS function-name helper was not found.");
        var values = typeof(FunctionCode)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(
                static field => (FunctionCode)(field.GetValue(null)
                    ?? throw new InvalidOperationException("FINS function code has no value.")))
            .ToArray();
        foreach (var value in values)
        {
            _ = method.Invoke(null, [(byte)value, byte.MinValue]);
        }

        var unknown = method.Invoke(null, [byte.MaxValue, byte.MinValue]);

        await Assert.That(values).IsNotEmpty();
        await Assert.That(unknown).IsEqualTo("Unknown");
    }

    /// <summary>Verifies null string values become deterministic empty PLC word buffers.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PlcTagValueCodec_ConvertsNullStringToEmptyWordsAsync()
    {
        var words = PlcTagValueCodec.GetStringWords(null!, OddStringLength);

        await Assert.That(words.Length).IsEqualTo(ExpectedStringWordCount);
        await Assert.That(words.All(static word => word == 0)).IsTrue();
    }

    /// <summary>Captures and verifies an asynchronous exception.</summary>
    /// <typeparam name="TException">Expected exception type.</typeparam>
    /// <param name="action">Action to invoke.</param>
    /// <returns>A task representing the assertion.</returns>
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
