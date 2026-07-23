// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Adapts an observable to an independently subscribed asynchronous enumeration.</summary>
/// <typeparam name="T">The observed value type.</typeparam>
internal sealed class ObservableAsyncEnumerable<T> : IAsyncEnumerable<T>
{
    /// <summary>The cancellation token supplied by the logical observer method.</summary>
    private readonly CancellationToken _cancellationToken;

    /// <summary>The source observable.</summary>
    private readonly IObservable<T> _source;

    /// <summary>Initializes a new instance of the <see cref="ObservableAsyncEnumerable{T}"/> class.</summary>
    /// <param name="source">The source observable.</param>
    /// <param name="cancellationToken">The source cancellation token.</param>
    internal ObservableAsyncEnumerable(IObservable<T> source, CancellationToken cancellationToken)
    {
        _source = source;
        _cancellationToken = cancellationToken;
    }

    /// <inheritdoc/>
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        Enumerator.Create(_source, _cancellationToken, cancellationToken);

    /// <summary>Queues observable notifications for one asynchronous enumerator.</summary>
    private sealed class Enumerator : IAsyncEnumerator<T>, IObserver<T>
    {
        /// <summary>The effective cancellation token.</summary>
        private readonly CancellationToken _cancellationToken;

        /// <summary>The optional linked cancellation source.</summary>
        private readonly CancellationTokenSource? _linkedCancellation;

        /// <summary>Protects mutable enumerator state.</summary>
        private readonly Lock _gate = new();

        /// <summary>Contains queued values.</summary>
        private readonly Queue<T> _values = [];

        /// <summary>The cancellation registration.</summary>
        private CancellationTokenRegistration _registration;

        /// <summary>The observable subscription.</summary>
        private IDisposable? _subscription;

        /// <summary>The terminal error.</summary>
        private Exception? _error;

        /// <summary>Signals that the queue or terminal state changed.</summary>
        private TaskCompletionSource<bool>? _signal;

        /// <summary>Indicates that enumeration has reached a terminal state.</summary>
        private bool _stopped;

        /// <summary>Initializes a new instance of the <see cref="Enumerator"/> class.</summary>
        /// <param name="sourceCancellation">The source cancellation token.</param>
        /// <param name="enumerationCancellation">The enumeration cancellation token.</param>
        internal Enumerator(
            CancellationToken sourceCancellation,
            CancellationToken enumerationCancellation)
        {
            if (sourceCancellation.CanBeCanceled && enumerationCancellation.CanBeCanceled)
            {
                _linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    sourceCancellation,
                    enumerationCancellation);
                _cancellationToken = _linkedCancellation.Token;
            }
            else
            {
                _cancellationToken = sourceCancellation.CanBeCanceled
                    ? sourceCancellation
                    : enumerationCancellation;
            }

            _registration = _cancellationToken.Register(Cancel);
        }

        /// <inheritdoc/>
        public T Current { get; private set; } = default!;

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            Stop(null);
            _registration.Dispose();
            _linkedCancellation?.Dispose();
            return default;
        }

        /// <inheritdoc/>
        public ValueTask<bool> MoveNextAsync() => new(MoveNextCoreAsync());

        /// <inheritdoc/>
        public void OnCompleted() => Stop(null);

        /// <inheritdoc/>
        public void OnError(Exception error) => Stop(error ?? throw new ArgumentNullException(nameof(error)));

        /// <inheritdoc/>
        public void OnNext(T value)
        {
            TaskCompletionSource<bool>? signal;
            lock (_gate)
            {
                if (_stopped)
                {
                    return;
                }

                _values.Enqueue(value);
                signal = _signal;
                _signal = null;
            }

            _ = signal?.TrySetResult(true);
        }

        /// <summary>Creates and subscribes a fully constructed enumerator.</summary>
        /// <param name="source">The source observable.</param>
        /// <param name="sourceCancellation">The source cancellation token.</param>
        /// <param name="enumerationCancellation">The enumeration cancellation token.</param>
        /// <returns>The subscribed enumerator.</returns>
        internal static Enumerator Create(
            IObservable<T> source,
            CancellationToken sourceCancellation,
            CancellationToken enumerationCancellation)
        {
            var enumerator = new Enumerator(sourceCancellation, enumerationCancellation);
            enumerator.Start(source);
            return enumerator;
        }

        /// <summary>Subscribes this fully constructed enumerator to its source.</summary>
        /// <param name="source">The source observable.</param>
        private void Start(IObservable<T> source)
        {
            var subscription = source.Subscribe(this);
            lock (_gate)
            {
                if (_stopped)
                {
                    subscription.Dispose();
                }
                else
                {
                    _subscription = subscription;
                }
            }
        }

        /// <summary>Waits for and dequeues the next value or terminal state.</summary>
        /// <returns>Whether a value was dequeued.</returns>
        private async Task<bool> MoveNextCoreAsync()
        {
            while (true)
            {
                Task<bool>? wait;
                lock (_gate)
                {
                    if (_values.Count != 0)
                    {
                        Current = _values.Dequeue();
                        return true;
                    }

                    if (_stopped)
                    {
                        if (_error is not null)
                        {
                            throw _error;
                        }

                        return false;
                    }

                    _signal ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
                    wait = _signal.Task;
                }

                _ = await wait.ConfigureAwait(false);
            }
        }

        /// <summary>Transitions to a cancelled terminal state.</summary>
        private void Cancel() => Stop(new OperationCanceledException(_cancellationToken));

        /// <summary>Transitions to a terminal state exactly once.</summary>
        /// <param name="error">The optional terminal error.</param>
        private void Stop(Exception? error)
        {
            TaskCompletionSource<bool>? signal;
            IDisposable? subscription;
            lock (_gate)
            {
                if (_stopped)
                {
                    return;
                }

                _stopped = true;
                _error = error;
                signal = _signal;
                _signal = null;
                subscription = _subscription;
                _subscription = null;
            }

            subscription?.Dispose();
            _ = signal?.TrySetResult(true);
        }
    }
}
