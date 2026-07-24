// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using IoT.DriverCore.Core;
using IoT.DriverCore.OmronPlcRx.Core;
using IoT.DriverCore.OmronPlcRx.Core.Channels;
using IoT.DriverCore.OmronPlcRx.Core.Requests;
using IoT.DriverCore.OmronPlcRx.Core.Responses;
using IoT.DriverCore.OmronPlcRx.Core.Results;
using IoT.DriverCore.OmronPlcRx.Enums;
using IoT.DriverCore.OmronPlcRx.Tags;
using TUnit.Core;
using OmronTcpClient = IoT.DriverCore.OmronPlcRx.Core.TcpClient;

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>Closes reachable defensive and lifecycle branches in the Omron runtime.</summary>
public sealed class OmronRuntimeResidualBranchCoverageTests
{
    /// <summary>Gets the local FINS node identifier.</summary>
    private const byte LocalNode = 1;

    /// <summary>Gets the remote FINS node identifier.</summary>
    private const byte RemoteNode = 2;

    /// <summary>Gets the test transport port.</summary>
    private const int TestPort = 9600;

    /// <summary>Gets the deterministic remote host.</summary>
    private const string RemoteHost = "127.0.0.1";

    /// <summary>Gets the deterministic request timeout.</summary>
    private const int TimeoutMilliseconds = 100;

    /// <summary>Gets the FINS service identifier offset.</summary>
    private const int ServiceIdOffset = 9;

    /// <summary>Gets the TCP command-code offset.</summary>
    private const int TcpCommandCodeOffset = 11;

    /// <summary>Gets the Omron TCP error command identifier.</summary>
    private const byte TcpErrorCommand = 3;

    /// <summary>Gets the TCP error-code offset.</summary>
    private const int TcpErrorCodeOffset = 15;

    /// <summary>Gets the minimum TCP error frame size.</summary>
    private const int TcpErrorFrameLength = 16;

    /// <summary>Gets the expected converted integer.</summary>
    private const int ExpectedConvertedInteger = 42;

    /// <summary>Gets the Boolean word tag name.</summary>
    private const string BooleanWordTag = "BooleanWord";

    /// <summary>Gets the integer tag name.</summary>
    private const string IntegerTag = "IntegerTag";

    /// <summary>Stores the Unix epoch in a form supported by every target framework.</summary>
    private static readonly DateTimeOffset Epoch = DateTimeOffset.FromUnixTimeSeconds(0);

    /// <summary>Verifies logical client guards and no-store behavior.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LogicalClient_CoversDefensiveBranchesAsync()
    {
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(() => _ = new OmronLogicalTagClient(null!)));

        using var fake = new FakeOmronPlcRx();
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(
                () => _ = new OmronLogicalTagClient(
                    fake,
                    (ILogicalTagCatalog)null!)));
        using var client = new OmronLogicalTagClient(fake);
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(
                () => _ = client.CreateTag((PlcTag<int>)null!)));
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(() => client.RegisterTag(null!)));
        await AssertThrowsAsync<InvalidOperationException>(
            () => client.InitializeStoreAsync(CancellationToken.None));
    }

    /// <summary>Verifies empty native batches and defensive batch-result completion.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LogicalClient_CoversEmptyBatchBranchesAsync()
    {
        var channel = new ResidualTestChannel();
        using var connection = CreateConnection(channel);
        using var driver = new OmronPlcRx(
            connection,
            TimeSpan.FromMilliseconds(TimeoutMilliseconds),
            false);
        using var batchClient = new OmronLogicalTagClient(driver);
        var reads = await batchClient.ReadManyAsync(
            ["MissingReadOne", "MissingReadTwo"],
            CancellationToken.None);
        var writes = await batchClient.WriteManyAsync(
        [
            new LogicalTagValue("MissingWriteOne", 1, Epoch),
            new LogicalTagValue("MissingWriteTwo", RemoteNode, Epoch),
        ],
            CancellationToken.None);

        var completeResults = GetPrivateMethod(
            typeof(OmronLogicalTagClient),
            "CompleteResults",
            BindingFlags.Static);
        var pendingResults = new TagOperationResult<LogicalTagValue>?[1];
        var completed = (TagOperationResult<LogicalTagValue>[])completeResults.Invoke(
            null,
            [pendingResults])!;

        var applyResults = GetPrivateMethod(
            typeof(OmronLogicalTagClient),
            "ApplyBatchResults",
            BindingFlags.Instance);
        var fallbackResults = new TagOperationResult<LogicalTagValue>?[1];
        _ = applyResults.Invoke(
            batchClient,
            [
                new[] { new OmronLogicalBatchResult(0, false, null, null) },
                new[] { new OmronLogicalBatchItem(0, "Fallback", "D0", typeof(short), null) },
                fallbackResults,
                null,
            ]);

        await Assert.That(reads.All(static result => !result.Succeeded)).IsTrue();
        await Assert.That(writes.All(static result => !result.Succeeded)).IsTrue();
        await Assert.That(completed[0].Succeeded).IsFalse();
        await Assert.That(fallbackResults[0]!.Error).Contains("grouped FINS operation failed");
    }

    /// <summary>Verifies base-channel contention and request-identifier rollover.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BaseChannel_CoversContentionAndRolloverBranchesAsync()
    {
        var channel = new ResidualTestChannel();
        var baseType = typeof(BaseChannel);

        var requestId = baseType.GetField(
            "_requestId",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(baseType.FullName, "_requestId");
        requestId.SetValue(channel, byte.MaxValue);
        var getNextRequestId = GetPrivateMethod(
            baseType,
            "GetNextRequestId",
            BindingFlags.Instance);
        var rolledOver = (byte)getNextRequestId.Invoke(channel, null)!;
        var incremented = (byte)getNextRequestId.Invoke(channel, null)!;

        var semaphore = (SemaphoreSlim)(baseType.GetProperty(
            nameof(Semaphore),
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMemberException(baseType.FullName, nameof(Semaphore)))
            .GetValue(channel)!;
        await semaphore.WaitAsync();
        var waitForChannel = GetPrivateMethod(
            baseType,
            "WaitForChannelAsync",
            BindingFlags.Instance);
        var pendingWait = (Task)waitForChannel.Invoke(
            channel,
            [CancellationToken.None])!;
        await Assert.That(pendingWait.IsCompleted).IsFalse();
        _ = semaphore.Release();
        await pendingWait;
        _ = semaphore.Release();

        await Assert.That(rolledOver).IsEqualTo(byte.MinValue);
        await Assert.That(incremented).IsEqualTo((byte)1);
    }

    /// <summary>Verifies base-channel retry and service-identifier mismatch boundaries.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BaseChannel_CoversRetryAndMismatchBranchesAsync()
    {
        var channel = new ResidualTestChannel();
        using var connection = CreateConnection(channel);
        var request = ReadClockRequest.CreateNew(connection);
        var baseType = typeof(BaseChannel);
        var purgeMismatch = GetPrivateMethod(
            baseType,
            "PurgeReceiveBufferWhenServiceIdMismatchAsync",
            BindingFlags.Instance);
        await InvokeTaskAsync(
            purgeMismatch,
            channel,
            new FINSException("Different validation failure"),
            Memory<byte>.Empty,
            request,
            TimeoutMilliseconds,
            CancellationToken.None);
        await InvokeTaskAsync(
            purgeMismatch,
            channel,
            new FINSException("Service ID mismatch"),
            new Memory<byte>(new byte[ServiceIdOffset]),
            request,
            TimeoutMilliseconds,
            CancellationToken.None);
        var equalServiceResponse = new byte[ServiceIdOffset + 1];
        equalServiceResponse[ServiceIdOffset] = request.ServiceID;
        await InvokeTaskAsync(
            purgeMismatch,
            channel,
            new FINSException("Service ID mismatch"),
            new Memory<byte>(equalServiceResponse),
            request,
            TimeoutMilliseconds,
            CancellationToken.None);

        await AssertThrowsAsync<OmronPLCException>(
            () => channel.ProcessRequestAsync(
                request,
                TimeoutMilliseconds,
                -1,
                CancellationToken.None));

        var disposeCore = baseType.GetMethod(
            "Dispose",
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            [typeof(bool)],
            null)
            ?? throw new MissingMethodException(baseType.FullName, "Dispose");
        _ = disposeCore.Invoke(channel, [false]);
    }

    /// <summary>Verifies word-based Boolean reads, fallback conversion, and tag null updates.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Driver_CoversWordBooleanAndPrivateValueBranchesAsync()
    {
        var channel = new ResidualTestChannel();
        channel.SetResponseData([0, 1]);
        using var connection = CreateConnection(channel);
        using var driver = new OmronPlcRx(
            connection,
            TimeSpan.FromMilliseconds(TimeoutMilliseconds),
            false);
        driver.AddUpdateTagItem(new PlcTag<bool>(BooleanWordTag, "D10"));
        driver.AddUpdateTagItem(new PlcTag<int>(IntegerTag, "D11"));

        var value = await driver.ReadValueAsync(
            new LogicalTagKey<bool>(BooleanWordTag),
            CancellationToken.None);

        var convertTo = GetPrivateMethod(
            typeof(OmronPlcRx),
            "ConvertTo",
            BindingFlags.Static)
            .MakeGenericMethod(typeof(int));
        var converted = (int)convertTo.Invoke(null, ["42"])!;

        var entries = typeof(OmronPlcRx).GetField(
            "_entries",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(driver)
            ?? throw new MissingFieldException(typeof(OmronPlcRx).FullName, "_entries");
        var entry = entries.GetType().GetProperty("Item")?.GetValue(entries, [IntegerTag])
            ?? throw new InvalidOperationException("The deterministic integer tag entry was not found.");
        var updateValue = entry.GetType().GetMethod("UpdateValue")
            ?? throw new MissingMethodException(entry.GetType().FullName, "UpdateValue");
        var changed = (bool)updateValue.Invoke(entry, [null])!;

        var initialized = typeof(OmronPlcRx).GetField(
            "_plcInitialized",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(OmronPlcRx).FullName, "_plcInitialized");
        initialized.SetValue(driver, true);
        await InvokeTaskAsync(
            GetPrivateMethod(
                typeof(OmronPlcRx),
                "InitializePlcForPollingAsync",
                BindingFlags.Instance),
            driver);

        await Assert.That(value).IsTrue();
        await Assert.That(converted).IsEqualTo(ExpectedConvertedInteger);
        await Assert.That(changed).IsTrue();
    }

    /// <summary>Verifies nested-operation and socket-channel defensive branches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TransportAndBatchAdapters_CoverNullAndTcpErrorBranchesAsync()
    {
        var nestedType = typeof(OmronPlcRx).GetNestedType(
            "ConnectionMemoryAreaOperations",
            BindingFlags.NonPublic)
            ?? throw new TypeLoadException("Connection memory-area adapter was not found.");
        await AssertReflectionThrowsAsync<ArgumentNullException>(
            () => Activator.CreateInstance(
                nestedType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                [null],
                null));

        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(
                () => _ = new OmronTcpClient(null!)));

        using var tcpChannel = new TCPChannel(RemoteHost, TestPort);
        var receiveTcp = GetPrivateMethod(
            typeof(TCPChannel),
            "ReceiveTcpCommandAsync",
            BindingFlags.Instance);
        await AssertThrowsAsync<OmronPLCException>(
            () => (Task)receiveTcp.Invoke(
                tcpChannel,
                [
                    TcpCommandCode.FINSFrame,
                    TimeoutMilliseconds,
                    CancellationToken.None,
                ])!);

        var throwIfTcpError = GetPrivateMethod(
            typeof(TCPChannel),
            "ThrowIfTcpError",
            BindingFlags.Instance);
        var invalidCommand = new byte[TcpErrorFrameLength];
        invalidCommand[TcpCommandCodeOffset] = TcpErrorCommand;
        await AssertReflectionThrowsAsync<OmronPLCException>(
            () => throwIfTcpError.Invoke(tcpChannel, [invalidCommand.ToList()]));
        var explicitError = new byte[TcpErrorFrameLength];
        explicitError[TcpErrorCodeOffset] = 1;
        await AssertReflectionThrowsAsync<OmronPLCException>(
            () => throwIfTcpError.Invoke(tcpChannel, [explicitError.ToList()]));

        using var udpChannel = new UDPChannel(RemoteHost, TestPort);
        var receiveUdp = typeof(UDPChannel).GetMethod(
            "ReceiveMessageAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(UDPChannel).FullName, "ReceiveMessageAsync");
        await AssertThrowsAsync<OmronPLCException>(
            () => (Task)receiveUdp.Invoke(
                udpChannel,
                [TimeoutMilliseconds, CancellationToken.None])!);
    }

    /// <summary>Creates a deterministic initialized FINS connection.</summary>
    /// <param name="channel">Injected deterministic channel.</param>
    /// <returns>The connection.</returns>
    private static OmronPLCConnection CreateConnection(BaseChannel channel) =>
        new(
            new OmronConnectionOptions(
                LocalNode,
                RemoteNode,
                ConnectionMethod.UDP,
                RemoteHost)
            {
                Port = TestPort,
                Timeout = TimeoutMilliseconds,
                Retries = 0,
            },
            channel,
            PlcType.CJ2,
            "CJ2M",
            "1.0",
            true);

    /// <summary>Gets a uniquely named private method.</summary>
    /// <param name="type">Declaring type.</param>
    /// <param name="name">Method name.</param>
    /// <param name="scope">Instance or static scope.</param>
    /// <returns>The reflected method.</returns>
    private static MethodInfo GetPrivateMethod(
        Type type,
        string name,
        BindingFlags scope) =>
        type.GetMethod(
            name,
            scope | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(type.FullName, name);

    /// <summary>Invokes and awaits a reflected task-returning method.</summary>
    /// <param name="method">Reflected method.</param>
    /// <param name="target">Optional instance target.</param>
    /// <param name="arguments">Method arguments.</param>
    /// <returns>A task representing the invocation.</returns>
    private static Task InvokeTaskAsync(
        MethodInfo method,
        object? target,
        params object?[] arguments) =>
        (Task)(method.Invoke(target, arguments)
            ?? throw new InvalidOperationException($"Method '{method.Name}' did not return a task."));

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

    /// <summary>Captures and verifies an exception wrapped by reflection.</summary>
    /// <typeparam name="TException">Expected semantic exception type.</typeparam>
    /// <param name="action">Reflected action to invoke.</param>
    /// <returns>A task representing the assertion.</returns>
    private static async Task AssertReflectionThrowsAsync<TException>(Func<object?> action)
        where TException : Exception
    {
        try
        {
            _ = action();
        }
        catch (TargetInvocationException exception)
            when (exception.InnerException is TException expected)
        {
            await Assert.That(expected).IsNotNull();
            return;
        }

        throw new InvalidOperationException($"Expected {nameof(TException)}.");
    }

    /// <summary>Provides a portable deterministic FINS channel for every declared test target.</summary>
    private sealed class ResidualTestChannel : BaseChannel
    {
        /// <summary>Gets the FINS function-code offset.</summary>
        private const int FinsFunctionCodeOffset = 10;

        /// <summary>Stores the last request sent through the channel.</summary>
        private byte[] _lastSent = [];

        /// <summary>Stores deterministic response payload bytes.</summary>
        private byte[] _responseData = [];

        /// <summary>Initializes a new instance of the <see cref="ResidualTestChannel"/> class.</summary>
        internal ResidualTestChannel()
            : base(OmronRuntimeResidualBranchCoverageTests.RemoteHost, TestPort)
        {
        }

        /// <summary>Copies the payload to return with the next response.</summary>
        /// <param name="responseData">Response payload bytes.</param>
        internal void SetResponseData(byte[] responseData) =>
            _responseData = (byte[])responseData.Clone();

        /// <inheritdoc />
        internal override Task InitializeAsync(
            int timeout,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        /// <inheritdoc />
        protected override Task DestroyAndInitializeClientAsync(
            int timeout,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        /// <inheritdoc />
        protected override Task<SendMessageResult> SendMessageAsync(
            ReadOnlyMemory<byte> message,
            int timeout,
            CancellationToken cancellationToken)
        {
            _lastSent = message.ToArray();
            return Task.FromResult(
                new SendMessageResult
                {
                    Bytes = _lastSent.Length,
                    Packets = 1,
                });
        }

        /// <inheritdoc />
        protected override Task<ReceiveMessageResult> ReceiveMessageAsync(
            int timeout,
            CancellationToken cancellationToken)
        {
            var response = new byte[
                FINSResponse.HeaderLength
                + FINSResponse.CommandLength
                + FINSResponse.ResponseCodeLength
                + _responseData.Length];
            response[ServiceIdOffset] = _lastSent[ServiceIdOffset];
            response[FinsFunctionCodeOffset] = _lastSent[FinsFunctionCodeOffset];
            response[TcpCommandCodeOffset] = _lastSent[TcpCommandCodeOffset];
            Array.Copy(
                _responseData,
                0,
                response,
                FINSResponse.HeaderLength
                    + FINSResponse.CommandLength
                    + FINSResponse.ResponseCodeLength,
                _responseData.Length);
            return Task.FromResult(
                new ReceiveMessageResult
                {
                    Bytes = response.Length,
                    Packets = 1,
                    Message = response,
                });
        }

        /// <inheritdoc />
        protected override Task PurgeReceiveBufferAsync(
            int timeout,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
