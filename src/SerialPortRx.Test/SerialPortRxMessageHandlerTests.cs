// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Serial.Tests;

/// <summary>Tests for command/response message handling.</summary>
public sealed class SerialPortRxMessageHandlerTests
{
    /// <summary>The representative status command.</summary>
    private const string StatusCommand = "STATUS";

    /// <summary>Verifies commands cannot be sent when the port is closed.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task SendAndRequest_WhenPortIsClosed_ThrowAsync()
    {
        using var port = new SerialPortRx();
        using var handler = new SerialPortRxMessageHandler(port);

        await Assert.That(() => handler.SendCommandAsync("G0")).Throws<InvalidOperationException>();
        await Assert.That(() => handler.RequestAsync("G0")).Throws<InvalidOperationException>();
        await Assert.That(() => handler.RequestAsync("G0", _ => { })).Throws<InvalidOperationException>();
    }

    /// <summary>Verifies null polling actions are ignored.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task WithPollingStopped_WhenActionIsNull_ReturnsAsync()
    {
        using var port = new SerialPortRx();
        using var handler = new SerialPortRxMessageHandler(port);

        await handler.WithPollingStoppedAsync(null);

        await Assert.That(handler.PollingTasks is null).IsTrue();
    }

    /// <summary>Verifies command echoes are ignored and pending requests remain queued.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnLineReceived_WhenLineIsEcho_IgnoresResponseAsync()
    {
        using var port = new SerialPortRx();
        using var handler = new SerialPortRxMessageHandler(port);
        var completion = new TaskCompletionSource<bool>();
        Enqueue(handler, new PendingRequest("MOVE X", _ => { }, completion));

        InvokeLine(handler, "MOVE X");

        await Assert.That(completion.Task.IsCompleted).IsFalse();
        await Assert.That(PendingCount(handler)).IsEqualTo(1);
    }

    /// <summary>Verifies normal responses are applied and complete pending commands.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnLineReceived_WhenLineIsResponse_AppliesAndCompletesAsync()
    {
        using var port = new SerialPortRx();
        using var handler = new SerialPortRxMessageHandler(port);
        var completion = new TaskCompletionSource<bool>();
        var applied = string.Empty;
        Enqueue(handler, new PendingRequest("READ", value => applied = value, completion));

        InvokeLine(handler, "42");

        await completion.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await Assert.That(applied).IsEqualTo("42");
        await Assert.That(PendingCount(handler)).IsEqualTo(0);
    }

    /// <summary>Verifies response prefixes are stripped before applying values.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnLineReceived_WithResponsePrefix_StripsPrefixAsync()
    {
        using var port = new SerialPortRx();
        using var handler = new SerialPortRxMessageHandler(port) { ResponsePrefix = "1" };
        var completion = new TaskCompletionSource<bool>();
        var applied = string.Empty;
        Enqueue(handler, new PendingRequest(StatusCommand, value => applied = value, completion));

        InvokeLine(handler, "1 OK");

        await completion.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await Assert.That(applied).IsEqualTo("OK");
    }

    /// <summary>Verifies error responses fault pending commands.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnLineReceived_WhenLineIsError_FaultsPendingRequestAsync()
    {
        using var port = new SerialPortRx();
        using var handler = new SerialPortRxMessageHandler(port);
        var completion = new TaskCompletionSource<bool>();
        Enqueue(handler, new PendingRequest("READ", _ => { }, completion));

        InvokeLine(handler, "ERR bad", "ERR");

        await Assert.That(() => completion.Task).Throws<InvalidOperationException>();
        await Assert.That(PendingCount(handler)).IsEqualTo(0);
    }

    /// <summary>Verifies polling can be started and stopped when no polling task is configured.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task StartAndStopPolling_WithoutPollingTask_CompletesAsync()
    {
        using var port = new SerialPortRx();
        using var handler = new SerialPortRxMessageHandler(port);

        handler.StartPolling();
        await Task.Delay(TwentyFive);
        handler.StopPolling();

        await Assert.That(handler.PollingTasks is null).IsTrue();
    }

    /// <summary>Verifies real paired port lines correlate command echoes and normal responses.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task RequestAsync_OverInMemoryPair_CorrelatesEchoAndResponseAsync()
    {
        using var pair = new InMemoryPortRxPair();
        using var handler = new SerialPortRxMessageHandler(pair.First);
        await pair.First.OpenAsync();
        await pair.Second.OpenAsync();
        using var responder = pair.Second.Lines.Subscribe(command =>
        {
            pair.Second.WriteLine(command);
            pair.Second.WriteLine("42");
        });
        var response = string.Empty;

        await handler.RequestAsync("READ", value => response = value);

        await Assert.That(response).IsEqualTo("42");
        await Assert.That(PendingCount(handler)).IsEqualTo(0);
    }

    /// <summary>Verifies prefixed echoes and values are normalized over the paired port.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task RequestAsync_WithResponsePrefix_NormalizesEchoAndValueAsync()
    {
        using var pair = new InMemoryPortRxPair();
        using var handler = new SerialPortRxMessageHandler(pair.First) { ResponsePrefix = "1" };
        await pair.First.OpenAsync();
        await pair.Second.OpenAsync();
        using var responder = pair.Second.Lines.Subscribe(_ =>
        {
            pair.Second.WriteLine(StatusCommand);
            pair.Second.WriteLine("1 OK");
        });
        var response = string.Empty;

        await handler.RequestAsync($"1 {StatusCommand}", value => response = value);

        await Assert.That(response).IsEqualTo("OK");
    }

    /// <summary>Verifies configured device error lines fault real pending requests.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task RequestAsync_WhenDeviceReturnsError_FaultsAsync()
    {
        using var pair = new InMemoryPortRxPair();
        using var handler = new SerialPortRxMessageHandler(pair.First, "ERR");
        await pair.First.OpenAsync();
        await pair.Second.OpenAsync();
        using var responder = pair.Second.Lines.Subscribe(_ => pair.Second.WriteLine("ERR rejected"));

        await Assert.That(() => handler.RequestAsync("WRITE")).Throws<InvalidOperationException>();
        await Assert.That(PendingCount(handler)).IsEqualTo(0);
    }

    /// <summary>Verifies polling faults complete pending requests and polling can restart around an action.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task PollingFault_CompletesPendingAndWithPollingStoppedRestartsAsync()
    {
        using var pair = new InMemoryPortRxPair();
        using var handler = new SerialPortRxMessageHandler(pair.First);
        await pair.First.OpenAsync();
        await pair.Second.OpenAsync();
        var polls = 0;
        handler.PollingTasks = () =>
        {
            polls++;
            throw new IOException("poll failed");
        };
        handler.StartPolling();

        await Assert.That(() => handler.RequestAsync("WAIT")).Throws<IOException>();

        var actionRan = false;
        await handler.WithPollingStoppedAsync(() =>
        {
            actionRan = true;
            return Task.CompletedTask;
        });
        handler.StopPolling();

        await Assert.That(actionRan).IsTrue();
        await Assert.That(polls).IsGreaterThanOrEqualTo(1);
    }

    /// <summary>Verifies the command-only request overload completes and its timeout cancels deterministically.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task RequestAsync_CommandOnly_CompletesAndTimesOutAsync()
    {
        using var successPair = new InMemoryPortRxPair();
        using var successHandler = new SerialPortRxMessageHandler(successPair.First);
        await successPair.First.OpenAsync();
        await successPair.Second.OpenAsync();
        using var responder = successPair.Second.Lines.Subscribe(_ => successPair.Second.WriteLine("OK"));

        await successHandler.RequestAsync("PING");

        using var timeoutPair = new InMemoryPortRxPair();
        timeoutPair.First.ReadTimeout = TwentyFive;
        using var timeoutHandler = new SerialPortRxMessageHandler(timeoutPair.First);
        await timeoutPair.First.OpenAsync();
        await timeoutPair.Second.OpenAsync();

        await Assert.That(() => timeoutHandler.RequestAsync("WAIT")).Throws<OperationCanceledException>();
    }

    /// <summary>Verifies blank, unsolicited, invalid-pending, prefix-mismatch, and apply-fault line branches.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnLineReceived_EdgeBranches_PreserveQueueSemanticsAsync()
    {
        using var port = new SerialPortRx();
        using var handler = new SerialPortRxMessageHandler(port) { ResponsePrefix = "1" };

        InvokeLine(handler, " ");
        InvokeLine(handler, "unsolicited");
        InvokeLine(handler, "ERR no pending", "ERR");

        var emptyCommandCompletion = new TaskCompletionSource<bool>();
        Enqueue(handler, new PendingRequest(string.Empty, _ => { }, emptyCommandCompletion));
        InvokeLine(handler, "first");
        await emptyCommandCompletion.Task;

        var prefixMismatchCompletion = new TaskCompletionSource<bool>();
        Enqueue(handler, new PendingRequest(StatusCommand, _ => { }, prefixMismatchCompletion));
        InvokeLine(handler, "second");
        await prefixMismatchCompletion.Task;

        var applyFailureCompletion = new TaskCompletionSource<bool>();
        Enqueue(
            handler,
            new PendingRequest(
                "READ",
                _ => throw new IOException("apply failed"),
                applyFailureCompletion));
        InvokeLine(handler, "third", string.Empty);

        await Assert.That(() => applyFailureCompletion.Task).Throws<IOException>();
        await Assert.That(PendingCount(handler)).IsEqualTo(0);
    }

    /// <summary>Enqueues a pending request into the handler's private queue.</summary>
    /// <param name="handler">The message handler.</param>
    /// <param name="request">The pending request to enqueue.</param>
    private static void Enqueue(SerialPortRxMessageHandler handler, PendingRequest request) =>
        PendingQueue(handler).Enqueue(request);

    /// <summary>Returns the number of pending requests in the handler.</summary>
    /// <param name="handler">The message handler.</param>
    /// <returns>The number of pending requests.</returns>
    private static int PendingCount(SerialPortRxMessageHandler handler) =>
        PendingQueue(handler).Count;

    /// <summary>Returns the handler's private pending request queue.</summary>
    /// <param name="handler">The message handler.</param>
    /// <returns>The pending request queue.</returns>
    private static ConcurrentQueue<PendingRequest> PendingQueue(SerialPortRxMessageHandler handler)
    {
        var field = typeof(SerialPortRxMessageHandler).GetField(
            "_pending",
            BindingFlags.NonPublic | BindingFlags.Instance) ??
            throw new InvalidOperationException("The pending request queue field was not found.");
        return field.GetValue(handler) as ConcurrentQueue<PendingRequest> ??
            throw new InvalidOperationException("The pending request queue has an unexpected type.");
    }

    /// <summary>Invokes the private line-processing method.</summary>
    /// <param name="handler">The message handler.</param>
    /// <param name="line">The line to process.</param>
    /// <param name="errorLines">The configured error prefixes.</param>
    private static void InvokeLine(SerialPortRxMessageHandler handler, string line, params string[] errorLines)
    {
        var method = typeof(SerialPortRxMessageHandler).GetMethod(
            "OnLineReceived",
            BindingFlags.NonPublic | BindingFlags.Instance) ??
            throw new InvalidOperationException("The line-processing method was not found.");
        _ = method.Invoke(handler, [line, errorLines]);
    }
}
