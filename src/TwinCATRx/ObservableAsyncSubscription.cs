// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Primitives.Async;

#if REACTIVE_SHIM
namespace IoT.Driver.TwinCATRx.Reactive;
#else
namespace IoT.Driver.TwinCATRx;
#endif

/// <summary>Coordinates an observable subscription with async observer callbacks.</summary>
/// <typeparam name="T">The value type.</typeparam>
internal sealed class ObservableAsyncSubscription<T> : IObserver<T>, IAsyncDisposable
{
    /// <summary>Stores the cancellation token registration.</summary>
    private readonly CancellationTokenRegistration _registration;

    /// <summary>Serializes queued async observer calls.</summary>
    private readonly Lock _gate = new();

    /// <summary>Stores the async observer.</summary>
    private readonly IObserverAsync<T> _observer;

    /// <summary>Signals disposal to pending observer calls.</summary>
    private readonly CancellationTokenSource _source = new();

    /// <summary>Stores whether disposal has already happened.</summary>
    private int _disposed;

    /// <summary>Stores the upstream observable subscription.</summary>
    private IDisposable? _sourceSubscription;

    /// <summary>Stores the tail of the serialized observer callback queue.</summary>
    private Task _pendingCallbacks = Task.CompletedTask;

    /// <summary>Initializes a new instance of the <see cref="ObservableAsyncSubscription{T}"/> class.</summary>
    /// <param name="observer">The async observer.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public ObservableAsyncSubscription(IObserverAsync<T> observer, CancellationToken cancellationToken)
    {
        _observer = observer;
        if (!cancellationToken.CanBeCanceled)
        {
            return;
        }

        _registration = cancellationToken.Register(Dispose);
    }

    /// <summary>Notifies the async observer that the sequence completed.</summary>
    public void OnCompleted() => QueueObserver(() => _observer.OnCompletedAsync(Result.Success));

    /// <summary>Notifies the async observer that the sequence failed.</summary>
    /// <param name="error">The observable error.</param>
    public void OnError(Exception error) => QueueObserver(() => _observer.OnErrorResumeAsync(error, _source.Token));

    /// <summary>Notifies the async observer that the sequence produced a value.</summary>
    /// <param name="value">The observable value.</param>
    public void OnNext(T value) => QueueObserver(() => _observer.OnNextAsync(value, _source.Token));

    /// <summary>Disposes the async subscription.</summary>
    /// <returns>The completed disposal operation.</returns>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }

    /// <summary>Sets the upstream observable subscription.</summary>
    /// <param name="sourceSubscription">The upstream observable subscription.</param>
    internal void SetSourceSubscription(IDisposable sourceSubscription)
    {
        _sourceSubscription = sourceSubscription;
        if (Volatile.Read(ref _disposed) == 0)
        {
            return;
        }

        sourceSubscription.Dispose();
    }

    /// <summary>Disposes the subscription resources.</summary>
    private void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _source.Cancel();
        _sourceSubscription?.Dispose();
        _registration.Dispose();
        _source.Dispose();
    }

    /// <summary>Queues an async observer callback under the subscription gate.</summary>
    /// <param name="callback">The observer callback.</param>
    private void QueueObserver(Func<ValueTask> callback)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        lock (_gate)
        {
            _pendingCallbacks = InvokeObserverAsync(_pendingCallbacks, callback);
        }
    }

    /// <summary>Invokes an observer callback after all previously queued callbacks have completed.</summary>
    /// <param name="previous">The preceding callback task.</param>
    /// <param name="callback">The observer callback.</param>
    /// <returns>The callback completion task.</returns>
    private async Task InvokeObserverAsync(Task previous, Func<ValueTask> callback)
    {
        try
        {
            await previous.ConfigureAwait(false);
            if (!_source.IsCancellationRequested)
            {
                await callback().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_source.IsCancellationRequested)
        {
        }
    }
}
