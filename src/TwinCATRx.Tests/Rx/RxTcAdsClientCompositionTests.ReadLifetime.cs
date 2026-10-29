// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
#if !NETFRAMEWORK
using System.Diagnostics.CodeAnalysis;
#endif
using IoT.Driver.TwinCATRx.Core;
using TwinCAT.TypeSystem;
using CompositeDisposable = ReactiveUI.Primitives.Disposables.MultipleDisposable;
using LeanBridge = IoT.Driver.TwinCATRx.ObservableBridgeExtensions;

namespace IoT.Driver.TwinCATRx.Tests.Rx;

/// <summary>Exercises connection lifetime during native reads.</summary>
public sealed partial class RxTcAdsClientCompositionTests
{
    /// <summary>Requests a pause from the reset observer.</summary>
    private const string PauseReaction = "pause";

    /// <summary>Requests a pause during retirement of the previous connection.</summary>
    private const string RetirementPauseReaction = "retirement-pause";

    /// <summary>Verifies retry retires a pause and honors reentrant state changes from its reset publication.</summary>
    /// <param name="reaction">The action taken by the reset observer.</param>
    /// <param name="cancellationToken">The test timeout token.</param>
    /// <returns>The test task.</returns>
    [Test]
    [Timeout(60_000)]
    [Arguments("none")]
    [Arguments(PauseReaction)]
    [Arguments("disconnect")]
    [Arguments(RetirementPauseReaction)]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("Exercises composed PLC initialization.")]
    [RequiresUnreferencedCode("Exercises composed PLC initialization.")]
#endif
    public async Task Retry_Resets_Pause_And_Honors_Observer_ReentrancyAsync(string reaction, CancellationToken cancellationToken)
    {
        const int resetPublicationCount = 2;
        const int reentrantPausePublicationCount = 3;
        var original = new FakeAdsClient { Port = TwinCat3Port };
        var replacement = new FakeAdsClient { Port = TwinCat3Port };
        using var platform = new FakePlatform(original);
        using var client = new RxTcAdsClient(TimeProvider.System, platform);
        client.Connect(new Settings { Port = TwinCat3Port });
        await DriveInitializationWithoutDelaysAsync(client, platform.Ticks, cancellationToken);
        client.Pause(TimeSpan.FromHours(1));
        var states = new List<bool>();
        using var pauses = LeanBridge.SubscribeTo(client.IsPausedObservable, paused =>
        {
            states.Add(paused);
            if (paused)
            {
                return;
            }

            if (reaction == PauseReaction)
            {
                client.Pause(TimeSpan.FromHours(1));
            }
            else if (reaction == "disconnect")
            {
                client.Disconnect();
            }
        });
        if (reaction == RetirementPauseReaction)
        {
            var resources = ClientConnectionTestState.Get<CompositeDisposable>(client, "_cleanup");
            _ = new RetirementPause(client).DisposeWith(resources);
        }

        var created = 0;
        platform.AdsClientFactory = () =>
        {
            created++;
            return replacement;
        };
        using var observer = new Signal<Unit>();
        var subscription = ClientConnectionTestState.Get<IDisposable>(client, "_plcCleanup");
        using var retry = (IDisposable)ClientConnectionTestState.Invoke(client, "InitializeConnection", observer, subscription)!;

        await TUnitAssert.That(client.IsPaused).IsEqualTo(reaction is PauseReaction or RetirementPauseReaction);
        await TUnitAssert.That(states.Count).IsEqualTo(reaction == PauseReaction ? reentrantPausePublicationCount : resetPublicationCount);
        await TUnitAssert.That(states[0]).IsTrue();
        await TUnitAssert.That(states[1]).IsEqualTo(reaction == RetirementPauseReaction);
        await TUnitAssert.That(created).IsEqualTo(reaction == "disconnect" ? 0 : 1);
        await TUnitAssert.That(original.Disposed.Task.IsCompleted).IsTrue();
    }

    /// <summary>Verifies an observer continuation uses the replacement connection for public requests.</summary>
    /// <param name="cancellationToken">The test timeout token.</param>
    /// <returns>The test task.</returns>
    [Test]
    [Timeout(60_000)]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("Exercises composed PLC initialization.")]
    [RequiresUnreferencedCode("Exercises composed PLC initialization.")]
#endif
    public async Task Observer_Continuation_Uses_Current_Connection_After_ReconnectAsync(CancellationToken cancellationToken)
    {
        var oldRuntime = new FakeAdsClient { Port = TwinCat3Port };
        oldRuntime.ValuesByVariable[ValueVariable] = ScalarPayload;
        var replacementRuntime = new FakeAdsClient { Port = TwinCat3Port };
        replacementRuntime.ValuesByVariable[ValueVariable] = WritePayload;
        using var platform = new FakePlatform(oldRuntime);
        platform.AddSymbol(ValueSymbolName, "DINT", DataTypeCategory.Primitive);
        var settings = new Settings { Port = TwinCat3Port };
        settings.WriteVariables.Add(new WriteVariable(ValueVariable));
        using var client = new RxTcAdsClient(TimeProvider.System, platform);
        var observerStarted = CreatePublicationSource();
        var releaseContinuation = CreatePublicationSource();
        var freshPublication = CreatePublicationSource();
        var freshWrite = CreatePublicationSource();
        Task continuation = Task.CompletedTask;
        using var writeSubscription = LeanBridge.SubscribeTo(client.OnWrite, id =>
        {
            if (id != "Success,continued-write")
            {
                return;
            }

            _ = freshWrite.TrySetResult(true);
        });
        using var dataSubscription = LeanBridge.SubscribeTo(client.DataReceived, item =>
        {
            if (item.Id == "continued-read")
            {
                _ = freshPublication.TrySetResult(true);
            }

            if (item.Id != "original")
            {
                return;
            }

            continuation = Task.Run(() => ContinuePublicRequestsAsync(() => IssuePublicRequests(client), releaseContinuation.Task), cancellationToken);
            _ = observerStarted.TrySetResult(true);
        });
        client.Connect(settings);
        await DriveInitializationWithoutDelaysAsync(client, platform.Ticks, cancellationToken);
        client.Read(ValueVariable, "original");
        await TUnitAssert.That(await WaitForReadPublicationAsync(observerStarted.Task, cancellationToken)).IsTrue();
        client.Disconnect();
        platform.AdsClientFactory = () => replacementRuntime;
        client.Connect(settings);
        await DriveInitializationWithoutDelaysAsync(client, platform.Ticks, cancellationToken);
        _ = releaseContinuation.TrySetResult(true);
        await AwaitReadLifecycleOperationAsync(continuation, cancellationToken);
        await TUnitAssert.That(await WaitForReadPublicationAsync(freshPublication.Task, cancellationToken)).IsTrue();
        await TUnitAssert.That(await WaitForReadPublicationAsync(freshWrite.Task, cancellationToken)).IsTrue();
        await TUnitAssert.That(replacementRuntime.ValuesByVariable[ValueVariable]).IsEqualTo(ScalarPayload);
        await TUnitAssert.That(client.IsPaused).IsTrue();
    }

    /// <summary>Verifies observers can end a connection during read publication without deadlocking.</summary>
    /// <param name="dispose">Whether the observer disposes the whole client.</param>
    /// <param name="cancellationToken">The test timeout token.</param>
    /// <returns>The test task.</returns>
    [Test]
    [Timeout(60_000)]
    [Arguments(false)]
    [Arguments(true)]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("Exercises composed PLC initialization.")]
    [RequiresUnreferencedCode("Exercises composed PLC initialization.")]
#endif
    public async Task Read_Observer_Ends_Connection_ReentrantlyAsync(bool dispose, CancellationToken cancellationToken)
    {
        var runtime = new FakeAdsClient { Port = TwinCat3Port };
        runtime.ValuesByVariable[ValueVariable] = ScalarPayload;
        using var platform = new FakePlatform(runtime);
        platform.AddSymbol(ValueSymbolName, "DINT", DataTypeCategory.Primitive);
        var settings = new Settings { Port = TwinCat3Port };
        settings.WriteVariables.Add(new WriteVariable(ValueVariable));
        using var client = new RxTcAdsClient(TimeProvider.System, platform);
        var observerReturned = CreatePublicationSource();
        var errors = new ConcurrentQueue<Exception>();
        using var errorSubscription = LeanBridge.SubscribeTo(client.ErrorReceived, errors.Enqueue);
        using var dataSubscription = LeanBridge.SubscribeTo(client.DataReceived, item =>
        {
            _ = item;
            if (dispose)
            {
                client.Dispose();
            }
            else
            {
                client.Disconnect();
            }

            _ = observerReturned.TrySetResult(true);
        });
        client.Connect(settings);
        await DriveInitializationWithoutDelaysAsync(client, platform.Ticks, cancellationToken);
        var readTask = Task.Run(() => client.Read(ValueVariable, "reentrant"), cancellationToken);

        await TUnitAssert.That(await WaitForReadPublicationAsync(observerReturned.Task, cancellationToken)).IsTrue();
        await TUnitAssert.That(await WaitForReadPublicationAsync(runtime.Disposed.Task, cancellationToken)).IsTrue();
        await AwaitReadLifecycleOperationAsync(readTask, cancellationToken);
        await TUnitAssert.That(client.Connected).IsFalse();
        await TUnitAssert.That(client.IsDisposed).IsEqualTo(dispose);
        await TUnitAssert.That(errors).IsEmpty();
    }

    /// <summary>Verifies late native reads cannot outlive their connection's publication rights.</summary>
    /// <param name="transition">The transition performed while the read is blocked.</param>
    /// <param name="failRead">Whether the old native read fails after release.</param>
    /// <param name="cancellationToken">The test timeout token.</param>
    /// <returns>The test task.</returns>
    [Test]
    [Timeout(60_000)]
    [Arguments("dispose", false)]
    [Arguments("dispose", true)]
    [Arguments("disconnect", false)]
    [Arguments("disconnect", true)]
    [Arguments("reconnect", false)]
    [Arguments("reconnect", true)]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("Exercises composed PLC initialization.")]
    [RequiresUnreferencedCode("Exercises composed PLC initialization.")]
#endif
    public async Task Read_Overlapping_Connection_Transition_Retains_Runtime_And_Drops_Late_ResultAsync(
        string transition,
        bool failRead,
        CancellationToken cancellationToken)
    {
        var oldRuntime = new FakeAdsClient { Port = TwinCat3Port };
        var replacementRuntime = new FakeAdsClient { Port = TwinCat3Port };
        replacementRuntime.ValuesByVariable[ValueVariable] = WritePayload;
        using var platform = new FakePlatform(oldRuntime);
        platform.AddSymbol(ValueSymbolName, "DINT", DataTypeCategory.Primitive);
        var settings = new Settings { Port = TwinCat3Port };
        settings.WriteVariables.Add(new WriteVariable(ValueVariable));
        using var client = new RxTcAdsClient(TimeProvider.System, platform);
        var data = new ConcurrentQueue<(string Variable, object? Data, string? Id)>();
        var errors = new ConcurrentQueue<Exception>();
        var freshPublication = CreatePublicationSource();
        using var dataSubscription = LeanBridge.SubscribeTo(
            client.DataReceived,
            item => RecordReadPublication(data, freshPublication, item));
        using var errorSubscription = LeanBridge.SubscribeTo(client.ErrorReceived, errors.Enqueue);
        client.Connect(settings);
        await DriveInitializationWithoutDelaysAsync(client, platform.Ticks, cancellationToken);
        await TUnitAssert.That(client.Connected).IsTrue();

        using var releaseRead = new ManualResetEventSlim();
        var enteredRead = CreatePublicationSource();
        oldRuntime.ReadOverride = () => PerformBlockedRead(releaseRead, enteredRead, failRead, cancellationToken);
        var readTask = Task.Run(() => client.Read(ValueVariable, "stale"), cancellationToken);
        try
        {
            await TUnitAssert.That(await WaitForReadPublicationAsync(enteredRead.Task, cancellationToken)).IsTrue();
            await TransitionDuringReadAsync(
                transition,
                client,
                (oldRuntime, replacementRuntime),
                platform,
                settings,
                freshPublication.Task,
                cancellationToken);
        }
        finally
        {
            releaseRead.Set();
            await AwaitReadLifecycleOperationAsync(readTask, CancellationToken.None);
        }

        await TUnitAssert.That(await WaitForReadPublicationAsync(oldRuntime.Disposed.Task, cancellationToken)).IsTrue();
        await AssertReadPublicationsAsync(transition, data, replacementRuntime);
        await TUnitAssert.That(errors).IsEmpty();
    }

    /// <summary>Issues public requests after the replacement connection is ready.</summary>
    /// <param name="requests">The public requests to issue.</param>
    /// <param name="release">The replacement connection signal.</param>
    /// <returns>The continuation task.</returns>
    private static async Task ContinuePublicRequestsAsync(Action requests, Task release)
    {
        await release;
        requests();
    }

    /// <summary>Exercises the public request entry points.</summary>
    /// <param name="client">The client under test.</param>
    private static void IssuePublicRequests(RxTcAdsClient client)
    {
        client.Read(ValueVariable, "continued-read");
        client.Write(ValueVariable, ScalarPayload, "continued-write");
        client.Pause(TimeSpan.FromMinutes(1));
    }

    /// <summary>Blocks a native read until released, then returns or fails as configured.</summary>
    /// <param name="releaseRead">The signal releasing the read.</param>
    /// <param name="enteredRead">The signal indicating the read has started.</param>
    /// <param name="failRead">Whether the read fails after release.</param>
    /// <param name="cancellationToken">The test timeout token.</param>
    /// <returns>The read payload.</returns>
    private static int PerformBlockedRead(
        ManualResetEventSlim releaseRead,
        TaskCompletionSource<bool> enteredRead,
        bool failRead,
        CancellationToken cancellationToken)
    {
        _ = enteredRead.TrySetResult(true);
        if (!releaseRead.Wait(PublicationTimeout, cancellationToken))
        {
            throw new TimeoutException("The controlled native read was not released.");
        }

        if (failRead)
        {
            throw new IOException(ReadFailureMessage);
        }

        return ScalarPayload;
    }

    /// <summary>Records a publication and signals when the replacement connection publishes.</summary>
    /// <param name="data">The collected publications.</param>
    /// <param name="freshPublication">The replacement publication signal.</param>
    /// <param name="item">The published item.</param>
    private static void RecordReadPublication(
        ConcurrentQueue<(string Variable, object? Data, string? Id)> data,
        TaskCompletionSource<bool> freshPublication,
        (string Variable, object? Data, string? Id) item)
    {
        data.Enqueue(item);
        if (item.Id != "fresh")
        {
            return;
        }

        _ = freshPublication.TrySetResult(true);
    }

    /// <summary>Performs the requested transition while the native read remains blocked.</summary>
    /// <param name="transition">The transition to perform.</param>
    /// <param name="client">The client under test.</param>
    /// <param name="runtimes">The old and replacement runtimes.</param>
    /// <param name="platform">The deterministic platform.</param>
    /// <param name="settings">The connection settings.</param>
    /// <param name="freshPublication">The replacement publication signal.</param>
    /// <param name="cancellationToken">The test timeout token.</param>
    /// <returns>The transition task.</returns>
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("Exercises composed PLC initialization.")]
    [RequiresUnreferencedCode("Exercises composed PLC initialization.")]
#endif
    private static async Task TransitionDuringReadAsync(
        string transition,
        RxTcAdsClient client,
        (FakeAdsClient Old, FakeAdsClient Replacement) runtimes,
        FakePlatform platform,
        Settings settings,
        Task<bool> freshPublication,
        CancellationToken cancellationToken)
    {
        var transitionTask = Task.Run(() => EndReadConnection(client, transition), cancellationToken);
        await AwaitReadLifecycleOperationAsync(transitionTask, cancellationToken);
        await TUnitAssert.That(runtimes.Old.Disposed.Task.IsCompleted).IsFalse();
        await TUnitAssert.That(client.Connected).IsFalse();
        if (transition != "reconnect")
        {
            return;
        }

        platform.AdsClientFactory = () => runtimes.Replacement;
        client.Connect(settings);
        await DriveInitializationWithoutDelaysAsync(client, platform.Ticks, cancellationToken);
        await TUnitAssert.That(client.Connected).IsTrue();
        client.Read(ValueVariable, "fresh");
        await TUnitAssert.That(await WaitForReadPublicationAsync(freshPublication, cancellationToken)).IsTrue();
    }

    /// <summary>Ends the current connection according to the requested transition.</summary>
    /// <param name="client">The client under test.</param>
    /// <param name="transition">The transition to perform.</param>
    private static void EndReadConnection(RxTcAdsClient client, string transition)
    {
        if (transition == "dispose")
        {
            client.Dispose();
            return;
        }

        client.Disconnect();
    }

    /// <summary>Checks that stale publications are absent and replacement publications remain valid.</summary>
    /// <param name="transition">The transition performed.</param>
    /// <param name="data">The collected publications.</param>
    /// <param name="replacementRuntime">The replacement runtime.</param>
    /// <returns>The assertion task.</returns>
    private static async Task AssertReadPublicationsAsync(
        string transition,
        ConcurrentQueue<(string Variable, object? Data, string? Id)> data,
        FakeAdsClient replacementRuntime)
    {
        foreach (var item in data)
        {
            await TUnitAssert.That(item.Id).IsNotEqualTo("stale");
        }

        if (transition != "reconnect")
        {
            return;
        }

        await TUnitAssert.That(data.Count).IsEqualTo(1);
        await TUnitAssert.That(data.TryPeek(out var publication)).IsTrue();
        await TUnitAssert.That(publication.Data).IsEqualTo(WritePayload);
        await TUnitAssert.That(replacementRuntime.Disposed.Task.IsCompleted).IsFalse();
    }

    /// <summary>Waits for a read publication with a bounded timeout.</summary>
    /// <param name="publication">The publication signal.</param>
    /// <param name="cancellationToken">The test timeout token.</param>
    /// <returns>Whether the publication arrived in time.</returns>
    private static async Task<bool> WaitForReadPublicationAsync(Task<bool> publication, CancellationToken cancellationToken)
    {
        var completed = await Task.WhenAny(publication, Task.Delay(PublicationTimeout, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        return ReferenceEquals(completed, publication) && await publication;
    }

    /// <summary>Bounds a native-read or teardown operation so regressions fail without hanging the runner.</summary>
    /// <param name="operation">The lifecycle operation.</param>
    /// <param name="cancellationToken">The test timeout token.</param>
    /// <returns>The completion task.</returns>
    private static async Task AwaitReadLifecycleOperationAsync(Task operation, CancellationToken cancellationToken)
    {
        var completed = await Task.WhenAny(operation, Task.Delay(PublicationTimeout, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        await TUnitAssert.That(ReferenceEquals(completed, operation)).IsTrue();
        await operation;
    }

    /// <summary>Drives manual state ticks while allowing scheduled initialization to run.</summary>
    /// <param name="client">The client being initialized.</param>
    /// <param name="ticks">The manual clock.</param>
    /// <param name="cancellationToken">The test timeout token.</param>
    /// <returns>The initialization task.</returns>
    private static async Task DriveInitializationWithoutDelaysAsync(
        RxTcAdsClient client,
        ManualObservable<long> ticks,
        CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(PublicationTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
        var tick = 0L;
        while (!client.Connected)
        {
            linked.Token.ThrowIfCancellationRequested();
            ticks.Emit(tick);
            tick++;
            await Task.Yield();
        }
    }

    /// <summary>Admits a new pause while retired connection resources are being released.</summary>
    /// <param name="client">The client whose replacement connection is paused.</param>
    private sealed class RetirementPause(RxTcAdsClient client) : IDisposable
    {
        /// <inheritdoc/>
        public void Dispose() => client.Pause(TimeSpan.FromHours(1));
    }
}
