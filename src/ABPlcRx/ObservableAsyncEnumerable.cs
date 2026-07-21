// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

#if REACTIVELIST_REACTIVE
namespace ABPlcRx.Reactive;
#else
namespace ABPlcRx;
#endif

/// <summary>Converts classic observables to cancellation-aware asynchronous sequences.</summary>
internal static class ObservableAsyncEnumerable
{
    /// <summary>Converts a classic observable to an asynchronous sequence.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="cancellationToken">A token that stops enumeration.</param>
    /// <returns>The asynchronous sequence.</returns>
    internal static async IAsyncEnumerable<T> Create<T>(
        IObservable<T> source,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var buffer = ObservableBuffer<T>.Create(source);
        while (await buffer.WaitForValueAsync(cancellationToken).ConfigureAwait(false))
        {
            while (buffer.TryTake(out var value))
            {
                yield return value;
            }
        }
    }

    /// <summary>Buffers synchronous notifications for asynchronous consumption.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    private sealed class ObservableBuffer<T> : IDisposable
    {
        /// <summary>Buffered values.</summary>
        private readonly Queue<T> _queue = new();

        /// <summary>Signals buffered or terminal notifications.</summary>
        private readonly SemaphoreSlim _signal = new(0);

        /// <summary>Synchronizes buffer state.</summary>
        private readonly object _syncRoot = new();

        /// <summary>The source subscription.</summary>
        private readonly MultipleDisposable _subscription = [];

        /// <summary>The optional source error.</summary>
        private Exception? _error;

        /// <summary>Indicates whether the source terminated.</summary>
        private bool _completed;

        /// <summary>Initializes a new instance of the <see cref="ObservableBuffer{T}"/> class.</summary>
        private ObservableBuffer()
        {
        }

        /// <summary>Creates a fully initialized buffer and then subscribes it to a source.</summary>
        /// <param name="source">The source observable.</param>
        /// <returns>The subscribed buffer.</returns>
        public static ObservableBuffer<T> Create(IObservable<T> source)
        {
            var buffer = new ObservableBuffer<T>();
            buffer._subscription.Add(source.Subscribe(new BufferObserver(buffer)));
            return buffer;
        }

        /// <summary>Tries to take the next buffered value.</summary>
        /// <param name="value">The buffered value.</param>
        /// <returns>True when a value was available.</returns>
        public bool TryTake(out T value)
        {
            lock (_syncRoot)
            {
                if (_queue.Count != 0)
                {
                    value = _queue.Dequeue();
                    return true;
                }
            }

            value = default!;
            return false;
        }

        /// <summary>Waits until a value or terminal notification is available.</summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>True when values can be consumed.</returns>
        public async Task<bool> WaitForValueAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                lock (_syncRoot)
                {
                    if (_queue.Count != 0)
                    {
                        return true;
                    }

                    if (_completed)
                    {
                        return CompleteWait();
                    }
                }

                await _signal.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _subscription.Dispose();
            _signal.Dispose();
        }

        /// <summary>Completes a terminal wait or rethrows the source error.</summary>
        /// <returns>False when the source completed successfully.</returns>
        private bool CompleteWait()
        {
            return _error is null ? false : throw _error;
        }

        /// <summary>Records a terminal notification.</summary>
        /// <param name="error">The optional source error.</param>
        private void Complete(Exception? error)
        {
            lock (_syncRoot)
            {
                _error = error;
                _completed = true;
            }

            _ = _signal.Release();
        }

        /// <summary>Buffers one source value.</summary>
        /// <param name="value">The value.</param>
        private void OnNext(T value)
        {
            lock (_syncRoot)
            {
                _queue.Enqueue(value);
            }

            _ = _signal.Release();
        }

        /// <summary>Forwards source notifications to the owning buffer.</summary>
        /// <param name="owner">The owning buffer.</param>
        private sealed class BufferObserver(ObservableBuffer<T> owner) : IObserver<T>
        {
            /// <inheritdoc/>
            public void OnCompleted() => owner.Complete(error: null);

            /// <inheritdoc/>
            public void OnError(Exception error) => owner.Complete(error);

            /// <inheritdoc/>
            public void OnNext(T value) => owner.OnNext(value);
        }
    }
}
