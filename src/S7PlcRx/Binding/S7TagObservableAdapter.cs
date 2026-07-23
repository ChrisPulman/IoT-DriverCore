// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.S7PlcRx.Reactive.Binding;

#else
namespace IoT.DriverCore.S7PlcRx.Binding;

#endif

/// <summary>Bridges generated classic observables to async enumeration.</summary>
public static class S7TagObservableAdapter
{
    /// <summary>Adapts a classic observable to an async sequence which ends when cancelled.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="source">The classic observable.</param>
    /// <returns>An async sequence.</returns>
    public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(IObservable<T> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return new ObservableAsyncEnumerable<T>(source);
    }

    /// <summary>Creates an async enumerator for an observable.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    private sealed class ObservableAsyncEnumerable<T> : IAsyncEnumerable<T>
    {
        /// <summary>Contains the source observable.</summary>
        private readonly IObservable<T> _source;

        /// <summary>Initializes a new instance of the <see cref="ObservableAsyncEnumerable{T}"/> class.</summary>
        /// <param name="source">The source observable.</param>
        public ObservableAsyncEnumerable(IObservable<T> source) => _source = source;

        /// <inheritdoc />
        public IAsyncEnumerator<T> GetAsyncEnumerator(
            CancellationToken cancellationToken = default) => Enumerator.Create(_source, cancellationToken);

        /// <summary>Buffers observable values for one async consumer.</summary>
        private sealed class Enumerator : IAsyncEnumerator<T>, IObserver<T>
        {
            /// <summary>Contains pending values.</summary>
            private readonly Queue<T> _values = new();

            /// <summary>Signals the arrival of a value or terminal notification.</summary>
            private readonly SemaphoreSlim _signal = new(0);

            /// <summary>Contains the consumer cancellation token.</summary>
            private readonly CancellationToken _cancellationToken;

            /// <summary>Contains the cancellation registration.</summary>
            private CancellationTokenRegistration _registration;

            /// <summary>Contains the source subscription.</summary>
            private IDisposable? _subscription;

            /// <summary>Contains the terminal source error.</summary>
            private Exception? _error;

            /// <summary>Indicates that the source completed.</summary>
            private bool _completed;

            /// <summary>Indicates that the enumerator was disposed.</summary>
            private bool _disposed;

            /// <summary>Initializes a new instance of the <see cref="Enumerator"/> class.</summary>
            /// <param name="cancellationToken">The consumer cancellation token.</param>
            private Enumerator(CancellationToken cancellationToken)
            {
                _cancellationToken = cancellationToken;
            }

            /// <inheritdoc />
            public T Current { get; private set; } = default!;

            /// <summary>Creates and activates an enumerator.</summary>
            /// <param name="source">The source observable.</param>
            /// <param name="cancellationToken">The consumer cancellation token.</param>
            /// <returns>The activated enumerator.</returns>
            public static Enumerator Create(
                IObservable<T> source,
                CancellationToken cancellationToken)
            {
                var enumerator = new Enumerator(cancellationToken);
                enumerator._subscription = source.Subscribe(enumerator);
                enumerator._registration = cancellationToken.Register(enumerator.Wake);
                return enumerator;
            }

            /// <inheritdoc />
            public async ValueTask<bool> MoveNextAsync()
            {
                while (true)
                {
                    _cancellationToken.ThrowIfCancellationRequested();
                    lock (_values)
                    {
                        if (_values.TryDequeue(out var value))
                        {
                            Current = value;
                            return true;
                        }

                        if (_error is not null)
                        {
                            throw _error;
                        }

                        if (_completed || _disposed)
                        {
                            return false;
                        }
                    }

                    await _signal.WaitAsync(_cancellationToken).ConfigureAwait(false);
                }
            }

            /// <inheritdoc />
            public ValueTask DisposeAsync()
            {
                if (_disposed)
                {
                    return default;
                }

                _disposed = true;
                _registration.Dispose();
                _subscription?.Dispose();
                _signal.Dispose();
                return default;
            }

            /// <inheritdoc />
            public void OnCompleted()
            {
                lock (_values)
                {
                    _completed = true;
                }

                Wake();
            }

            /// <inheritdoc />
            public void OnError(Exception error)
            {
                lock (_values)
                {
                    _error = error;
                }

                Wake();
            }

            /// <inheritdoc />
            public void OnNext(T value)
            {
                lock (_values)
                {
                    if (_completed || _disposed)
                    {
                        return;
                    }

                    _values.Enqueue(value);
                }

                Wake();
            }

            /// <summary>Signals the consumer when the enumerator is active.</summary>
            private void Wake()
            {
                if (_disposed)
                {
                    return;
                }

                try
                {
                    _ = _signal.Release();
                }
                catch (ObjectDisposedException)
                {
                    // Cancellation can race asynchronous disposal.
                }
            }
        }
    }
}
