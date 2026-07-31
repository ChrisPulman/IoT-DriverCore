// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.Serial.Reactive;
#else
namespace IoT.Driver.Serial;
#endif

/// <summary>Compatibility bridge between classic observables and ReactiveUI.Primitives async observables.</summary>
public static class ObservableAsyncBridgeExtensions
{
    /// <summary>Converts a classic observable into an async observable.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <returns>An async observable that forwards source notifications.</returns>
    public static IObservableAsync<T> ToAsyncObservable<T>(IObservable<T>? source)
    {
        ArgumentGuard.ThrowIfNull(source, nameof(source));

        return new ObservableAsyncAdapter<T>(source);
    }

    /// <summary>Converts an async observable into a classic observable.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="source">The source async observable.</param>
    /// <returns>A classic observable that forwards async source notifications.</returns>
    public static IObservable<T> ToObservable<T>(IObservableAsync<T>? source)
    {
        ArgumentGuard.ThrowIfNull(source, nameof(source));

        return Observable.CreateWithState<T, IObservableAsync<T>>(
            source,
            static (asyncSource, observer) => new AsyncObservableSubscription<T>(asyncSource, observer));
    }

    /// <summary>Adapts a classic disposable subscription to an async disposable subscription.</summary>
    /// <param name="subscription">The wrapped subscription.</param>
    private readonly struct AsyncSubscription(IDisposable subscription) : IAsyncDisposable
    {
        /// <summary>Disposes the wrapped subscription.</summary>
        /// <returns>A completed value task.</returns>
        public ValueTask DisposeAsync()
        {
            subscription.Dispose();
            return default;
        }
    }

    /// <summary>Adapts a classic observable to the async observable contract.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="source">The source observable.</param>
    private sealed class ObservableAsyncAdapter<T>(IObservable<T> source) : IObservableAsync<T>
    {
        /// <summary>Subscribes an async observer to the adapted observable.</summary>
        /// <param name="observer">The async observer.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The async subscription.</returns>
        public ValueTask<IAsyncDisposable> SubscribeAsync(
            IObserverAsync<T> observer,
            CancellationToken cancellationToken)
        {
            ArgumentGuard.ThrowIfNull(observer, nameof(observer));

            var subscription = source.Subscribe(
                value => Complete(observer.OnNextAsync(value, cancellationToken)),
                error => Complete(observer.OnErrorResumeAsync(error, cancellationToken)),
                () => Complete(observer.OnCompletedAsync(Result.Success)));

            IAsyncDisposable asyncSubscription = new AsyncSubscription(subscription);
            return new(asyncSubscription);
        }

        /// <summary>Completes a value task synchronously when required.</summary>
        /// <param name="valueTask">The value task to complete.</param>
        private static void Complete(in ValueTask valueTask)
        {
            if (valueTask.IsCompletedSuccessfully)
            {
                return;
            }

            _ = CompleteAsync(valueTask);
        }

        /// <summary>Awaits an asynchronous observer notification without blocking the source callback.</summary>
        /// <param name="valueTask">The asynchronous observer notification.</param>
        /// <returns>A task that completes after the notification is processed.</returns>
        private static async Task CompleteAsync(ValueTask valueTask) =>
            await valueTask.ConfigureAwait(false);
    }

    /// <summary>Owns an async subscription exposed through the synchronous observable contract.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    private sealed class AsyncObservableSubscription<T> : IDisposable
    {
        /// <summary>Provides the asynchronous sequence being adapted.</summary>
        private readonly IObservableAsync<T> _source;

        /// <summary>Receives the synchronous sequence notifications.</summary>
        private readonly IObserver<T> _observer;

        /// <summary>Cancels the asynchronous subscription when the synchronous subscription ends.</summary>
        private readonly CancellationTokenSource _cancellation = new();

        /// <summary>Stores the asynchronous subscription after it has been established.</summary>
        private IAsyncDisposable? _subscription;

        /// <summary>Tracks whether disposal has begun.</summary>
        private int _disposed;

        /// <summary>Initializes a new instance of the <see cref="AsyncObservableSubscription{T}"/> class.</summary>
        /// <param name="source">The asynchronous sequence being adapted.</param>
        /// <param name="observer">The synchronous observer receiving notifications.</param>
        internal AsyncObservableSubscription(IObservableAsync<T> source, IObserver<T> observer)
        {
            _source = source;
            _observer = observer;
            _ = SubscribeAsync();
        }

        /// <summary>Disposes the source subscription without blocking the calling observer.</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _cancellation.Cancel();
            var subscription = Interlocked.Exchange(ref _subscription, null);
            if (subscription is not null)
            {
                _ = DisposeSubscriptionAsync(subscription);
            }

            _cancellation.Dispose();
        }

        /// <summary>Disposes an asynchronous subscription without blocking the caller.</summary>
        /// <param name="subscription">The subscription to dispose.</param>
        /// <returns>A task that completes after the subscription is disposed.</returns>
        private static async Task DisposeSubscriptionAsync(IAsyncDisposable subscription) =>
            await subscription.DisposeAsync().ConfigureAwait(false);

        /// <summary>Establishes and tracks the asynchronous source subscription.</summary>
        /// <returns>A task that completes after subscription establishment or failure.</returns>
        private async Task SubscribeAsync()
        {
            try
            {
                var subscription = await _source.SubscribeAsync(
                        new ObserverAsyncAdapter<T>(_observer),
                        _cancellation.Token)
                    .ConfigureAwait(false);
                if (Interlocked.CompareExchange(ref _subscription, subscription, null) is not null ||
                    Volatile.Read(ref _disposed) != 0)
                {
                    await subscription.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
            {
            }
            catch (Exception error)
            {
                if (Volatile.Read(ref _disposed) == 0)
                {
                    _observer.OnError(error);
                }
            }
        }
    }

    /// <summary>Adapts a classic observer to the async observer contract.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="observer">The wrapped observer.</param>
    private sealed class ObserverAsyncAdapter<T>(IObserver<T> observer) : IObserverAsync<T>
    {
        /// <summary>Disposes the observer adapter.</summary>
        /// <returns>A completed value task.</returns>
        public ValueTask DisposeAsync() => default;

        /// <summary>Forwards completion or failure to the wrapped observer.</summary>
        /// <param name="result">The completion result.</param>
        /// <returns>A completed value task.</returns>
        public ValueTask OnCompletedAsync(Result result)
        {
            if (result.IsFailure)
            {
                observer.OnError(result.Exception);
            }
            else
            {
                observer.OnCompleted();
            }

            return default;
        }

        /// <summary>Forwards an error to the wrapped observer.</summary>
        /// <param name="error">The observed error.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A completed value task.</returns>
        public ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken)
        {
            observer.OnError(error);
            return default;
        }

        /// <summary>Forwards a value to the wrapped observer.</summary>
        /// <param name="value">The observed value.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A completed value task.</returns>
        public ValueTask OnNextAsync(T value, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return default;
            }

            observer.OnNext(value);
            return default;
        }
    }
}
