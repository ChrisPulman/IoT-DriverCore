// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.IO.Ports.Tests;

/// <summary>Tests for observable/async-observable bridge helpers.</summary>
public sealed class ObservableAsyncBridgeExtensionsTests
{
    /// <summary>Gets the values used to verify observable forwarding order.</summary>
    private static int[] TestValues { get; } = [1, Two, Three];

    /// <summary>Verifies null observable arguments are rejected.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task ToAsyncObservable_WhenSourceIsNull_ThrowsAsync()
    {
        IObservable<int>? source = null;

        await Assert.That(() => ObservableAsyncBridgeExtensions.ToAsyncObservable(source))
            .Throws<ArgumentNullException>();
    }

    /// <summary>Verifies null async observable arguments are rejected.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task ToObservable_WhenSourceIsNull_ThrowsAsync()
    {
        IObservableAsync<int>? source = null;

        await Assert.That(() => ObservableAsyncBridgeExtensions.ToObservable(source))
            .Throws<ArgumentNullException>();
    }

    /// <summary>Verifies null async observers are rejected.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task SubscribeAsync_WhenObserverIsNull_ThrowsAsync()
    {
        var source = ObservableAsyncBridgeExtensions.ToAsyncObservable(Observable.Return(Answer));

        async Task Act()
        {
            dynamic? observer = null;
            await source.SubscribeAsync(observer, CancellationToken.None);
        }

        await Assert.That(Act).Throws<ArgumentNullException>();
    }

    /// <summary>Verifies observable values are forwarded to async observers.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task ToAsyncObservable_ForwardsValuesAndCompletionAsync()
    {
        var observer = new RecordingAsyncObserver<int>();
        var source = ObservableAsyncBridgeExtensions.ToAsyncObservable(Observable.FromEnumerable(TestValues));

        await using var subscription = await source.SubscribeAsync(observer, CancellationToken.None);

        await Assert.That(observer.Values.Count).IsEqualTo(Three);
        await Assert.That(observer.Values[0]).IsEqualTo(1);
        await Assert.That(observer.Values[1]).IsEqualTo(Two);
        await Assert.That(observer.Values[2]).IsEqualTo(Three);
        await Assert.That(observer.IsCompleted).IsTrue();
        await Assert.That(observer.Error).IsNull();
    }

    /// <summary>Verifies canceled subscriptions forward the token to async observers.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task ToAsyncObservable_WhenCanceled_ForwardsCanceledTokenAsync()
    {
        var observer = new RecordingAsyncObserver<int>();
        var source = ObservableAsyncBridgeExtensions.ToAsyncObservable(Observable.Return(Answer));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await using var subscription = await source.SubscribeAsync(observer, cancellation.Token);

        await Assert.That(observer.Values.Count).IsEqualTo(1);
        await Assert.That(observer.LastOnNextCancellationRequested).IsTrue();
        await Assert.That(observer.IsCompleted).IsTrue();
    }

    /// <summary>Verifies async observable values are forwarded to classic observers.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task ToObservable_ForwardsValuesAndCompletionAsync()
    {
        var values = new List<int>();
        var completed = false;
        var source = new ManualAsyncObservable<int>(async (observer, token) =>
        {
            await observer.OnNextAsync(Seven, token);
            await observer.OnCompletedAsync(Result.Success);
        });

        using var subscription = ObservableAsyncBridgeExtensions.ToObservable(source).Subscribe(
            values.Add,
            _ => { },
            () => completed = true);

        await Assert.That(values.Count).IsEqualTo(1);
        await Assert.That(values[0]).IsEqualTo(Seven);
        await Assert.That(completed).IsTrue();
    }

    /// <summary>Verifies async observable errors are forwarded to classic observers.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task ToObservable_ForwardsErrorsAsync()
    {
        var receivedErrors = new List<Exception>();
        var expected = new InvalidOperationException("boom");
        var source = new ManualAsyncObservable<int>((observer, token) => observer.OnErrorResumeAsync(expected, token));

        using var subscription = ObservableAsyncBridgeExtensions.ToObservable(source).Subscribe(
            _ => { },
            receivedErrors.Add);

        await Assert.That(receivedErrors.Count).IsEqualTo(1);
        await Assert.That(receivedErrors[0]).IsEqualTo(expected);
    }

    /// <summary>Records async observer notifications for assertions.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    private sealed class RecordingAsyncObserver<T> : IObserverAsync<T>
    {
        /// <summary>Gets the values received by the observer.</summary>
        public List<T> Values { get; } = [];

        /// <summary>Gets a value indicating whether completion was observed.</summary>
        public bool IsCompleted { get; private set; }

        /// <summary>Gets the last observed error.</summary>
        public Exception? Error { get; private set; }

        /// <summary>Gets a value indicating whether the last OnNext token was canceled.</summary>
        public bool LastOnNextCancellationRequested { get; private set; }

        /// <summary>Disposes the observer.</summary>
        /// <returns>A completed value task.</returns>
        public ValueTask DisposeAsync() => default;

        /// <summary>Records completion.</summary>
        /// <param name="result">The completion result.</param>
        /// <returns>A completed value task.</returns>
        public ValueTask OnCompletedAsync(Result result)
        {
            IsCompleted = result.IsSuccess;
            Error = result.Exception;
            return default;
        }

        /// <summary>Records a resumable error.</summary>
        /// <param name="error">The observed error.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A completed value task.</returns>
        public ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken)
        {
            Error = error;
            return default;
        }

        /// <summary>Records the next value.</summary>
        /// <param name="value">The observed value.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A completed value task.</returns>
        public ValueTask OnNextAsync(T value, CancellationToken cancellationToken)
        {
            LastOnNextCancellationRequested = cancellationToken.IsCancellationRequested;
            Values.Add(value);
            return default;
        }
    }

    /// <summary>Manual async observable used to publish controlled notifications.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="publish">The delegate that publishes notifications.</param>
    private sealed class ManualAsyncObservable<T>(
        Func<IObserverAsync<T>, CancellationToken, ValueTask> publish)
        : IObservableAsync<T>
    {
        /// <summary>Subscribes the observer and publishes configured notifications.</summary>
        /// <param name="observer">The async observer.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A disposable subscription.</returns>
        public async ValueTask<IAsyncDisposable> SubscribeAsync(
            IObserverAsync<T> observer,
            CancellationToken cancellationToken)
        {
            await publish(observer, cancellationToken);
            return NoopAsyncDisposable.Instance;
        }
    }

    /// <summary>No-op async disposable used by manual observables.</summary>
    private sealed class NoopAsyncDisposable : IAsyncDisposable
    {
        /// <summary>Gets the singleton no-op async disposable.</summary>
        public static NoopAsyncDisposable Instance { get; } = new();

        /// <summary>Disposes the no-op resource.</summary>
        /// <returns>A completed value task.</returns>
        public ValueTask DisposeAsync() => default;
    }
}
