// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using CP.IoT.Core;

#if REACTIVE_SHIM
namespace CP.TwinCatRx.Reactive;
#else
namespace CP.TwinCatRx;
#endif

/// <summary>Bridges logical-tag observables into async sequences.</summary>
internal static class LogicalTagObservableAsyncBridge
{
    /// <summary>Creates an async sequence over a classic observable.</summary>
    /// <param name="source">The source observable.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The async sequence.</returns>
    internal static async IAsyncEnumerable<LogicalTagValue> ObserveAsync(
        IObservable<LogicalTagValue> source,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var queue = new Queue<LogicalTagValue>();
        using var signal = new SemaphoreSlim(0);
        Exception? sourceError = null;
        var completed = false;
        using IDisposable subscription = ObservableBridgeExtensions.SubscribeTo(
            source,
            value =>
            {
                lock (queue)
                {
                    queue.Enqueue(value);
                }

                _ = signal.Release();
            },
            error =>
            {
                sourceError = error;
                completed = true;
                _ = signal.Release();
            },
            () =>
            {
                completed = true;
                _ = signal.Release();
            });

        while (true)
        {
            LogicalTagValue? value = null;
            lock (queue)
            {
                if (queue.Count > 0)
                {
                    value = queue.Dequeue();
                }
            }

            if (value is not null)
            {
                yield return value;
                continue;
            }

            if (!completed)
            {
                await signal.WaitAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (sourceError is not null)
            {
                throw sourceError;
            }

            yield break;
        }
    }
}
