// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
#if REACTIVE_SHIM
using IoT.Driver.S7PlcRx.Reactive.Symbolic.Protocol;
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
using IoT.Driver.S7PlcRx.Symbolic.Protocol;

namespace IoT.Driver.S7PlcRx.Symbolic;
#endif

/// <summary>Handles the s7symbolic subscription queue.</summary>
/// <typeparam name="T">The streamed value type.</typeparam>
internal sealed class S7SymbolicSubscriptionQueue<T>
{
    /// <summary>The gate.</summary>
    private readonly Lock _gate = new();

    /// <summary>The items.</summary>
    private readonly Queue<T> _items = new();

    /// <summary>The capacity.</summary>
    private readonly int _capacity;

    /// <summary>The available.</summary>
    private TaskCompletionSource<bool> _available = NewSignal();

    /// <summary>The error.</summary>
    private Exception? _error;

    /// <summary>The completed.</summary>
    private bool _completed;

    /// <summary>Initializes a new instance of the <see cref="S7SymbolicSubscriptionQueue{T}"/> class.</summary>
    /// <param name="capacity">The capacity.</param>
    internal S7SymbolicSubscriptionQueue(int capacity) => _capacity = capacity;

    /// <summary>Handles the publish.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The operation result.</returns>
    internal bool Publish(T value)
    {
        lock (_gate)
        {
            if (_completed)
            {
                return false;
            }

            if (_items.Count == _capacity)
            {
                _error = new InvalidDataException("The subscription consumer exceeded the bounded notification buffer.");
                _completed = true;
            }
            else
            {
                _items.Enqueue(value);
            }

            _ = _available.TrySetResult(true);
            return !_completed;
        }
    }

    /// <summary>Handles the complete.</summary>
    /// <param name="failure">The failure.</param>
    internal void Complete(Exception? failure = null)
    {
        lock (_gate)
        {
            _completed = true;
            _error ??= failure;
            _ = _available.TrySetResult(true);
        }
    }

    /// <summary>Handles the take async.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    internal async ValueTask<T> TakeAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task wait;
            lock (_gate)
            {
                if (_error is not null)
                {
                    throw _error;
                }

                if (_items.Count > 0)
                {
                    return _items.Dequeue();
                }

                if (_completed)
                {
                    throw new OperationCanceledException("Subscription completed.");
                }

                wait = _available.Task;
            }

            var registration = cancellationToken.Register(
                static state =>
            {
                var pair = (Tuple<TaskCompletionSource<bool>, CancellationToken>)state!;
                _ = pair.Item1.TrySetCanceled(pair.Item2);
            },
                Tuple.Create(_available, cancellationToken));
#if NET8_0_OR_GREATER
            await using (registration.ConfigureAwait(false))
#else
            using (registration)
#endif
            {
                await wait.ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (_available.Task.IsCompleted && !_completed)
                {
                    _available = NewSignal();
                }
            }
        }
    }

    /// <summary>Handles the new signal.</summary>
    /// <returns>The operation result.</returns>
    private static TaskCompletionSource<bool> NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
