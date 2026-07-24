// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
using TUnit.Core;

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>Verifies logical observation lifecycle and subscription failure behavior.</summary>
public sealed class OmronLogicalObservationCoverageTests
{
    /// <summary>Gets the deterministic boolean tag address.</summary>
    private const string BooleanAddress = "D400.0";

    /// <summary>Gets the completed observation tag name.</summary>
    private const string CompletionTag = "Complete";

    /// <summary>Gets the failed observation tag name.</summary>
    private const string FailureTag = "Failure";

    /// <summary>Gets the completed asynchronous observation tag name.</summary>
    private const string AsyncCompletionTag = "AsyncComplete";

    /// <summary>Gets the failed asynchronous observation tag name.</summary>
    private const string AsyncFailureTag = "AsyncFailure";

    /// <summary>Gets the first merged observation tag name.</summary>
    private const string FirstTag = "First";

    /// <summary>Gets the second merged observation tag name.</summary>
    private const string SecondTag = "Second";

    /// <summary>Verifies typed notifications forward values, errors, completion, and null guards.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Observe_ForwardsTypedLifecycleAndRejectsNullObserverAsync()
    {
        using var completionPlc = new FakeOmronPlcRx();
        using var completionClient = CreateClient(completionPlc, CompletionTag);
        var completionSource = completionClient.Observe(CompletionTag);
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(
                () => completionSource.Subscribe(
                    (IObserver<LogicalTagValue>)null!)));
        var completionObserver = new RecordingObserver();
        using var completionSubscription = completionSource.Subscribe(completionObserver);
        completionPlc.Publish(CompletionTag, true);
        completionPlc.Dispose();

        using var failurePlc = new FakeOmronPlcRx();
        using var failureClient = CreateClient(failurePlc, FailureTag);
        var failureObserver = new RecordingObserver();
        using var failureSubscription = failureClient
            .Observe(FailureTag)
            .Subscribe(failureObserver);
        var expected = new InvalidOperationException("deterministic observation failure");
        failurePlc.Fail(FailureTag, expected);

        await Assert.That(completionObserver.NextCount).IsGreaterThan(0);
        await Assert.That(completionObserver.CompletedCount).IsEqualTo(1);
        await Assert.That(failureObserver.Error).IsSameReferenceAs(expected);
    }

    /// <summary>Verifies async observation forwards completion and source errors through its channel.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ObserveAsync_ForwardsCompletionAndSourceErrorsAsync()
    {
        using var completionPlc = new FakeOmronPlcRx();
        using var completionClient = CreateClient(completionPlc, AsyncCompletionTag);
        await using var completion = completionClient
            .ObserveAsync(AsyncCompletionTag, CancellationToken.None)
            .GetAsyncEnumerator();
        await Assert.That(await completion.MoveNextAsync()).IsTrue();
        completionPlc.Dispose();
        await Assert.That(await completion.MoveNextAsync()).IsFalse();

        using var failurePlc = new FakeOmronPlcRx();
        using var failureClient = CreateClient(failurePlc, AsyncFailureTag);
        await using var failure = failureClient
            .ObserveAsync(AsyncFailureTag, CancellationToken.None)
            .GetAsyncEnumerator();
        await Assert.That(await failure.MoveNextAsync()).IsTrue();
        failurePlc.Fail(
            AsyncFailureTag,
            new InvalidOperationException("deterministic async failure"));
        await AssertThrowsAsync<InvalidOperationException>(
            async () => _ = await failure.MoveNextAsync());
    }

    /// <summary>Verifies merged subscriptions clean up partial and completed subscription groups.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ObserveMany_DisposesPartialAndCompletedSubscriptionGroupsAsync()
    {
        using var failingPlc = new FakeOmronPlcRx();
        using var failingClient = CreateClient(failingPlc, FirstTag, SecondTag);
        _ = failingPlc.TagsThrowingOnSubscribe.Add(SecondTag);
        var observer = new RecordingObserver();
        var failing = failingClient.ObserveMany([FirstTag, SecondTag]);
        await AssertThrowsAsync<InvalidOperationException>(
            () => Task.Run(() => failing.Subscribe(observer)));

        using var normalPlc = new FakeOmronPlcRx();
        using var normalClient = CreateClient(normalPlc, "Third", "Fourth");
        var subscription = normalClient
            .ObserveMany(["Third", "Fourth"])
            .Subscribe(observer);
        subscription.Dispose();
        subscription.Dispose();

        await Assert.That(observer.NextCount).IsGreaterThan(0);
    }

    /// <summary>Creates a logical client and registers deterministic Boolean tags.</summary>
    /// <param name="plc">Injected fake PLC.</param>
    /// <param name="tagNames">Tag names to register.</param>
    /// <returns>The configured logical client.</returns>
    private static OmronLogicalTagClient CreateClient(
        FakeOmronPlcRx plc,
        params string[] tagNames)
    {
        var client = new OmronLogicalTagClient(plc);
        foreach (var tagName in tagNames)
        {
            client.RegisterTag(
                new LogicalTag(tagName, BooleanAddress, typeof(bool).FullName!));
        }

        return client;
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

    /// <summary>Records logical observation lifecycle events.</summary>
    private sealed class RecordingObserver : IObserver<LogicalTagValue>
    {
        /// <summary>Gets the completion count.</summary>
        internal int CompletedCount { get; private set; }

        /// <summary>Gets the forwarded error.</summary>
        internal Exception? Error { get; private set; }

        /// <summary>Gets the next-value count.</summary>
        internal int NextCount { get; private set; }

        /// <inheritdoc />
        public void OnCompleted() => CompletedCount++;

        /// <inheritdoc />
        public void OnError(Exception error) => Error = error;

        /// <inheritdoc />
        public void OnNext(LogicalTagValue value) => NextCount++;
    }
}
