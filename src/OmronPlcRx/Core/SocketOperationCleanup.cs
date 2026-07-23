// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Core;
#else
namespace IoT.DriverCore.OmronPlcRx.Core;
#endif

/// <summary>Provides cleanup helpers for timed socket operations.</summary>
internal static class SocketOperationCleanup
{
    /// <summary>Cancels a delay task and observes the expected cancellation.</summary>
    /// <param name="cancellationTokenSource">The cancellation token source.</param>
    /// <param name="delayTask">The delay task.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task CancelDelayAsync(CancellationTokenSource cancellationTokenSource, Task delayTask)
    {
#if NET8_0_OR_GREATER
        await cancellationTokenSource.CancelAsync().ConfigureAwait(false);
#else
        cancellationTokenSource.Cancel();
#endif
        try
        {
            await delayTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>Cancels a socket operation and observes expected shutdown exceptions.</summary>
    /// <param name="cancellationTokenSource">The cancellation token source.</param>
    /// <param name="operationTask">The socket operation task.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task CancelSocketOperationAsync(
        CancellationTokenSource cancellationTokenSource,
        Task operationTask)
    {
#if NET8_0_OR_GREATER
        await cancellationTokenSource.CancelAsync().ConfigureAwait(false);
#else
        cancellationTokenSource.Cancel();
#endif
        try
        {
            await operationTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException)
        {
        }
    }
}
