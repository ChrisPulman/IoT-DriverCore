// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using IoT.DriverCore.OmronPlcRx.Core;
using IoT.DriverCore.OmronPlcRx.Core.Channels;
using IoT.DriverCore.OmronPlcRx.Core.Requests;
using IoT.DriverCore.OmronPlcRx.Enums;
using IoT.DriverCore.Serial;
using TUnit.Core;

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>Exercises serial Omron FINS channels through a composed in-memory serial transport.</summary>
public sealed class OmronSerialChannelSimulatorTests
{
    /// <summary>Gets the request timeout used by deterministic serial tests.</summary>
    private const int TimeoutMilliseconds = 100;

    /// <summary>Gets the short timeout used by serial error tests.</summary>
    private const int ShortTimeoutMilliseconds = 30;

    /// <summary>Gets the local FINS node.</summary>
    private const byte LocalNode = 1;

    /// <summary>Gets the remote FINS node.</summary>
    private const byte RemoteNode = 2;

    /// <summary>Gets the minimum complete FINS response length.</summary>
    private const int FinsResponseLength = 14;

    /// <summary>Gets the start offset of a Toolbus payload.</summary>
    private const int ToolbusPayloadOffset = 3;

    /// <summary>Gets the Toolbus checksum length.</summary>
    private const int ToolbusChecksumLength = 2;

    /// <summary>Gets the request service identifier offset.</summary>
    private const int ServiceIdOffset = 9;

    /// <summary>Gets the request command offset.</summary>
    private const int CommandOffset = 10;

    /// <summary>Gets the Host Link direct-frame service identifier offset.</summary>
    private const int HostLinkServiceIdOffset = 12;

    /// <summary>Gets the Host Link direct-frame command offset.</summary>
    private const int HostLinkCommandOffset = 14;

    /// <summary>Gets the Host Link frame trailer length.</summary>
    private const int HostLinkTrailerLength = 4;

    /// <summary>Gets the configured maximum for short-frame validation.</summary>
    private const int ShortMaximumFrameLength = 16;

    /// <summary>Verifies a Host Link serial peer processes an actual FINS request.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task HostLinkChannel_ProcessesFinsRequestThroughComposedSerialPortAsync()
    {
        var ports = new List<ScriptedOmronSerialPort>();
        using var channel = CreateChannel(
            new OmronSerialOptions("SIM-HOST"),
            CreateHostLinkResponse,
            ports);
        using var connection = CreateRequestConnection();

        await channel.InitializeAsync(TimeoutMilliseconds, CancellationToken.None);
        var request = ReadClockRequest.CreateNew(connection);
        var result = await channel.ProcessRequestAsync(
            request,
            TimeoutMilliseconds,
            0,
            CancellationToken.None);

        await Assert.That(result.Response.ServiceID).IsEqualTo(request.ServiceID);
        await Assert.That(result.BytesSent > 0).IsTrue();
        await Assert.That(result.BytesReceived > FinsResponseLength).IsTrue();
        await Assert.That(ports.Count).IsEqualTo(1);
        await Assert.That(ports[0].WasOpened).IsTrue();
        await Assert.That(ports[0].Writes.Count).IsEqualTo(1);
    }

    /// <summary>Verifies a Toolbus peer synchronizes before processing an actual FINS request.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ToolbusChannel_SynchronizesAndProcessesFinsRequestAsync()
    {
        var ports = new List<ScriptedOmronSerialPort>();
        using var channel = CreateChannel(
            OmronSerialOptions.CreateToolbus("SIM-TOOLBUS"),
            CreateToolbusResponse,
            ports);
        using var connection = CreateRequestConnection();

        await channel.InitializeAsync(TimeoutMilliseconds, CancellationToken.None);
        var request = ReadClockRequest.CreateNew(connection);
        var result = await channel.ProcessRequestAsync(
            request,
            TimeoutMilliseconds,
            0,
            CancellationToken.None);

        await Assert.That(result.Response.ServiceID).IsEqualTo(request.ServiceID);
        await Assert.That(ports[0].Writes.Count).IsEqualTo(ToolbusChecksumLength);
        await Assert.That(ports[0].RtsEnable).IsTrue();
        await Assert.That(ports[0].DtrEnable).IsFalse();
    }

    /// <summary>Verifies serial no-data, timeout, and oversized-response diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task HostLinkChannel_ReportsDeterministicReceiveFailuresAsync()
    {
        using var connection = CreateRequestConnection();
        var request = ReadClockRequest.CreateNew(connection);

        await AssertChannelFailureAsync(new OmronSerialOptions("SIM-NONE"), [], request);
        await AssertChannelFailureAsync(
            new OmronSerialOptions("SIM-PARTIAL"),
            Encoding.ASCII.GetBytes("@"),
            request);
        await AssertChannelFailureAsync(
            new OmronSerialOptions("SIM-LONG")
            {
                MaximumFrameLength = ShortMaximumFrameLength,
            },
            new byte[ShortMaximumFrameLength + 1],
            request);
    }

    /// <summary>Verifies oversized requests and factory argument validation.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task SerialChannel_RejectsOversizedRequestsAndInvalidFactoryAsync()
    {
        using var connection = CreateRequestConnection();
        var request = ReadClockRequest.CreateNew(connection);
        var options = new OmronSerialOptions("SIM-SHORT")
        {
            MaximumFrameLength = ShortMaximumFrameLength,
        };
        using var channel = CreateChannel(options, static _ => [], []);
        await channel.InitializeAsync(TimeoutMilliseconds, CancellationToken.None);

        await AssertThrowsAsync<OmronPLCException>(
            () => channel.ProcessRequestAsync(
                request,
                TimeoutMilliseconds,
                0,
                CancellationToken.None));
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.FromResult(
                new SerialHostLinkFinsChannel(options, null!)));
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.FromResult(
                new OmronSerialPortAdapter(null!, TimeoutMilliseconds)));
    }

    /// <summary>Verifies the production serial adapter through connected in-memory serial endpoints.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task SerialPortAdapter_DelegatesThroughInMemorySerialPairAsync()
    {
        using var pair = new InMemoryPortRxPair("OMRON-A", "OMRON-B");
        pair.First.EnableAutoDataReceive = false;
        pair.Second.EnableAutoDataReceive = false;
        using var adapter = new OmronSerialPortAdapter(pair.First);
        await pair.Second.OpenAsync();
        await adapter.OpenAsync();
        adapter.RtsEnable = true;
        adapter.DtrEnable = true;

        adapter.Write([1], 0, 1);
        var peerBuffer = new byte[1];
        var peerRead = pair.Second.Read(peerBuffer, 0, peerBuffer.Length);
        pair.Second.Write([RemoteNode], 0, 1);
        var response = new byte[1];
        var received = adapter.Read(response, 0, response.Length);
        adapter.DiscardInBuffer();

        await Assert.That(peerRead).IsEqualTo(1);
        await Assert.That(received).IsEqualTo(1);
        await Assert.That(response[0]).IsEqualTo(RemoteNode);
        await Assert.That(adapter.BytesToRead).IsEqualTo(0);
        await Assert.That(adapter.RtsEnable).IsTrue();
        await Assert.That(adapter.DtrEnable).IsTrue();
        adapter.Close();

        using var closedAdapter = new OmronSerialPortAdapter(
            new OmronSerialOptions("SIM-CLOSED"),
            ShortTimeoutMilliseconds);
        closedAdapter.RtsEnable = true;
        closedAdapter.DtrEnable = true;
        var bytesToReadError = CaptureException(() => _ = closedAdapter.BytesToRead);
        var discardError = CaptureException(closedAdapter.DiscardInBuffer);
        var writeError = CaptureException(() => closedAdapter.Write([1], 0, 1));
        var readError = CaptureException(() => closedAdapter.Read(new byte[1], 0, 1));
        closedAdapter.Close();

        await Assert.That(closedAdapter.RtsEnable).IsTrue();
        await Assert.That(closedAdapter.DtrEnable).IsTrue();
        await Assert.That(
            new[] { bytesToReadError, discardError, writeError, readError }
                .Where(static error => error is not null)
                .All(static error => !string.IsNullOrEmpty(error!.Message)))
            .IsTrue();
    }

    /// <summary>Verifies serial channel reinitialization, purge, and uninitialized guards.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task SerialChannel_ReinitializesPurgesAndRejectsUninitializedSendAsync()
    {
        using var connection = CreateRequestConnection();
        var request = ReadClockRequest.CreateNew(connection);
        var ports = new List<ScriptedOmronSerialPort>();
        using var channel = CreateChannel(
            new OmronSerialOptions("SIM-REINIT"),
            CreateHostLinkResponse,
            ports);

        await AssertThrowsAsync<OmronPLCException>(
            () => channel.ProcessRequestAsync(
                request,
                TimeoutMilliseconds,
                0,
                CancellationToken.None));
        await channel.InitializeAsync(TimeoutMilliseconds, CancellationToken.None);
        await InvokePrivateTaskAsync(
            channel,
            "PurgeReceiveBufferAsync",
            TimeoutMilliseconds,
            CancellationToken.None);
        await InvokePrivateTaskAsync(
            channel,
            "DestroyAndInitializeClientAsync",
            TimeoutMilliseconds,
            CancellationToken.None);

        await Assert.That(ports.Count).IsEqualTo(ToolbusChecksumLength);
        await Assert.That(ports[0].WasClosed).IsTrue();
        await Assert.That(ports[1].WasOpened).IsTrue();
    }

    /// <summary>Verifies Toolbus receive validation and synchronization timeout paths.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ToolbusChannel_ReportsDeterministicFrameAndSynchronizationFailuresAsync()
    {
        using var connection = CreateRequestConnection();
        var request = ReadClockRequest.CreateNew(connection);
        await AssertToolbusResponseFailureAsync([], request);
        await AssertToolbusResponseFailureAsync([0xAB], request);
        await AssertToolbusResponseFailureAsync([0xAB, 0, 1], request);
        await AssertToolbusResponseFailureAsync([0xAB, byte.MaxValue, byte.MaxValue], request);

        var syncOptions = OmronSerialOptions.CreateToolbus("SIM-SYNC-TIMEOUT");
        using var syncFailure = CreateChannel(
            syncOptions,
            _ => Enumerable.Repeat((byte)0x55, syncOptions.MaximumFrameLength + 1).ToArray(),
            []);
        await AssertThrowsAsync<OmronPLCException>(
            () => syncFailure.InitializeAsync(
                ShortTimeoutMilliseconds,
                CancellationToken.None));

        using var uninitialized = CreateChannel(
            OmronSerialOptions.CreateToolbus("SIM-UNINITIALIZED"),
            static _ => [],
            []);
        await AssertThrowsAsync<OmronPLCException>(
            () => InvokePrivateTaskAsync(
                uninitialized,
                "SynchronizeToolbusAsync",
                ShortTimeoutMilliseconds,
                CancellationToken.None));
        var expiredStart = TimeProvider.System.GetUtcNow().AddSeconds(-1);
        var waitResult = await InvokePrivateTaskAsync<bool>(
            uninitialized,
            "WaitForSerialDataAsync",
            expiredStart,
            ShortTimeoutMilliseconds,
            CancellationToken.None);
        await Assert.That(waitResult).IsFalse();
    }

    /// <summary>Creates a serial channel and captures every composed port instance.</summary>
    /// <param name="options">Serial channel options.</param>
    /// <param name="responseFactory">Response factory used by each serial port.</param>
    /// <param name="ports">Destination for created serial ports.</param>
    /// <returns>The composed serial channel.</returns>
    private static SerialHostLinkFinsChannel CreateChannel(
        OmronSerialOptions options,
        Func<byte[], byte[]> responseFactory,
        List<ScriptedOmronSerialPort> ports) =>
        new(
            options,
            _ =>
            {
                var port = new ScriptedOmronSerialPort(responseFactory);
                ports.Add(port);
                return port;
            });

    /// <summary>Asserts a serial channel fails for a scripted response.</summary>
    /// <param name="options">Serial options.</param>
    /// <param name="response">Response bytes returned after a request.</param>
    /// <param name="request">FINS request.</param>
    /// <returns>A task that represents the assertion.</returns>
    private static async Task AssertChannelFailureAsync(
        OmronSerialOptions options,
        byte[] response,
        FINSRequest request)
    {
        using var channel = CreateChannel(options, _ => response, []);
        await channel.InitializeAsync(TimeoutMilliseconds, CancellationToken.None);
        await AssertThrowsAsync<OmronPLCException>(
            () => channel.ProcessRequestAsync(
                request,
                ShortTimeoutMilliseconds,
                0,
                CancellationToken.None));
    }

    /// <summary>Asserts one post-synchronization Toolbus response fails validation.</summary>
    /// <param name="response">Response returned after the FINS request.</param>
    /// <param name="request">FINS request.</param>
    /// <returns>A task that represents the assertion.</returns>
    private static async Task AssertToolbusResponseFailureAsync(
        byte[] response,
        FINSRequest request)
    {
        var options = OmronSerialOptions.CreateToolbus("SIM-TOOLBUS-FAIL");
        using var channel = CreateChannel(
            options,
            write => write.AsSpan().SequenceEqual(ToolbusFinsFrameCodec.SynchronizationFrame.Span)
                ? ToolbusFinsFrameCodec.SynchronizationFrame.ToArray()
                : response,
            []);
        await channel.InitializeAsync(TimeoutMilliseconds, CancellationToken.None);
        await AssertThrowsAsync<OmronPLCException>(
            () => channel.ProcessRequestAsync(
                request,
                ShortTimeoutMilliseconds,
                0,
                CancellationToken.None));
    }

    /// <summary>Creates a valid direct Host Link response for an encoded request.</summary>
    /// <param name="requestBytes">ASCII Host Link request.</param>
    /// <returns>ASCII Host Link response.</returns>
    private static byte[] CreateHostLinkResponse(byte[] requestBytes)
    {
        var request = Encoding.ASCII.GetString(requestBytes);
        var serviceId = request.Substring(HostLinkServiceIdOffset, ToolbusChecksumLength);
        var commandLength = request.Length - HostLinkCommandOffset - HostLinkTrailerLength;
        var command = request.Substring(HostLinkCommandOffset, commandLength);
        var body = $"@00FA00C00000{serviceId}{command}0000";
        return Encoding.ASCII.GetBytes($"{body}{HostLinkFinsFrameCodec.CalculateFcs(body)}*\r");
    }

    /// <summary>Creates a synchronization echo or valid Toolbus response for a serial write.</summary>
    /// <param name="request">Toolbus serial request.</param>
    /// <returns>Toolbus response bytes.</returns>
    private static byte[] CreateToolbusResponse(byte[] request)
    {
        if (request.AsSpan().SequenceEqual(ToolbusFinsFrameCodec.SynchronizationFrame.Span))
        {
            return ToolbusFinsFrameCodec.SynchronizationFrame.ToArray();
        }

        var declaredLength = (request[1] << 8) | request[ToolbusChecksumLength];
        var finsLength = declaredLength - ToolbusChecksumLength;
        var finsRequest = request
            .AsSpan(ToolbusPayloadOffset, finsLength)
            .ToArray();
        return EncodeToolbusResponse(CreateFinsResponse(finsRequest));
    }

    /// <summary>Creates a successful empty FINS response for a request.</summary>
    /// <param name="request">Binary FINS request.</param>
    /// <returns>Binary FINS response.</returns>
    private static byte[] CreateFinsResponse(byte[] request)
    {
        var response = new byte[FinsResponseLength];
        response[0] = 0xC0;
        response[ServiceIdOffset] = request[ServiceIdOffset];
        response[CommandOffset] = request[CommandOffset];
        response[CommandOffset + 1] = request[CommandOffset + 1];
        return response;
    }

    /// <summary>Encodes a binary FINS response in a Toolbus envelope.</summary>
    /// <param name="finsResponse">Binary FINS response.</param>
    /// <returns>Complete Toolbus frame.</returns>
    private static byte[] EncodeToolbusResponse(byte[] finsResponse)
    {
        var declaredLength = finsResponse.Length + ToolbusChecksumLength;
        var frame = new byte[ToolbusPayloadOffset + declaredLength];
        frame[0] = 0xAB;
        frame[1] = (byte)(declaredLength >> 8);
        frame[ToolbusChecksumLength] = (byte)declaredLength;
        finsResponse.CopyTo(frame, ToolbusPayloadOffset);
        var checksum = ToolbusFinsFrameCodec.CalculateChecksum(
            frame.AsSpan(0, frame.Length - ToolbusChecksumLength));
        frame[^ToolbusChecksumLength] = (byte)(checksum >> 8);
        frame[^1] = (byte)checksum;
        return frame;
    }

    /// <summary>Creates an initialized connection used to construct FINS requests.</summary>
    /// <returns>The request connection.</returns>
    private static OmronPLCConnection CreateRequestConnection()
    {
        var channel = new CoreProtocolCoverageTests.TestChannel();
        return new(
            new OmronConnectionOptions(
                LocalNode,
                RemoteNode,
                ConnectionMethod.UDP,
                IPAddress.Loopback.ToString())
            {
                Timeout = TimeoutMilliseconds,
                Retries = 0,
            },
            channel,
            PlcType.CJ2,
            "CJ2M",
            "1.0",
            true);
    }

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
        catch (TException ex)
        {
            await Assert.That(ex.Message).IsNotEmpty();
            return;
        }

        throw new InvalidOperationException(
            string.Create(CultureInfo.InvariantCulture, $"Expected {nameof(TException)}."));
    }

    /// <summary>Captures any synchronous exception from a closed native serial port operation.</summary>
    /// <param name="action">Action to invoke.</param>
    /// <returns>The captured exception, or null when the operation succeeds.</returns>
    private static Exception? CaptureException(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    /// <summary>Invokes a private asynchronous method.</summary>
    /// <param name="target">Invocation target.</param>
    /// <param name="methodName">Method name.</param>
    /// <param name="arguments">Method arguments.</param>
    /// <returns>A task representing the invocation.</returns>
    private static async Task InvokePrivateTaskAsync(
        object target,
        string methodName,
        params object?[] arguments)
    {
        var method = target
            .GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(target.GetType().FullName, methodName);
        var task = (Task)method.Invoke(target, arguments)!;
        await task.ConfigureAwait(false);
    }

    /// <summary>Invokes a private asynchronous method returning a value.</summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="target">Invocation target.</param>
    /// <param name="methodName">Method name.</param>
    /// <param name="arguments">Method arguments.</param>
    /// <returns>The invocation result.</returns>
    private static async Task<T> InvokePrivateTaskAsync<T>(
        object target,
        string methodName,
        params object?[] arguments)
    {
        var method = target
            .GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(target.GetType().FullName, methodName);
        var task = (Task<T>)method.Invoke(target, arguments)!;
        return await task.ConfigureAwait(false);
    }
}
