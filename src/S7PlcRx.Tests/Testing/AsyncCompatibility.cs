// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.S7PlcRx.Tests.Testing;

/// <summary>Provides target-independent asynchronous cancellation and timeout helpers.</summary>
internal static class AsyncCompatibility
{
    /// <summary>Cancels a token source and returns a completed task.</summary>
    /// <param name="source">The cancellation source.</param>
    /// <returns>A completed task after synchronous cancellation finishes.</returns>
    internal static Task CancelAsync(CancellationTokenSource source)
    {
        Guard.NotNull(source, nameof(source));
        source.Cancel();
        return Task.CompletedTask;
    }

    /// <summary>Waits for a task to complete within the specified timeout.</summary>
    /// <param name="task">The task to await.</param>
    /// <param name="timeout">The maximum wait duration.</param>
    /// <returns>A task representing the bounded wait.</returns>
    /// <exception cref="TimeoutException">Thrown when the timeout elapses first.</exception>
    internal static async Task WaitAsync(Task task, TimeSpan timeout)
    {
        Guard.NotNull(task, nameof(task));
        if (!ReferenceEquals(await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false), task))
        {
            throw new TimeoutException();
        }

        await task.ConfigureAwait(false);
    }

    /// <summary>Waits for a result-producing task to complete within the specified timeout.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="timeout">The maximum wait duration.</param>
    /// <returns>The completed task result.</returns>
    /// <exception cref="TimeoutException">Thrown when the timeout elapses first.</exception>
    internal static async Task<T> WaitAsync<T>(Task<T> task, TimeSpan timeout)
    {
        Guard.NotNull(task, nameof(task));
        if (!ReferenceEquals(await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false), task))
        {
            throw new TimeoutException();
        }

        return await task.ConfigureAwait(false);
    }
}
