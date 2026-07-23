// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.ABPlcRx.Tests;

/// <summary>Provides deterministic test operations across the declared test target frameworks.</summary>
internal static class TestCompatibility
{
    /// <summary>Waits for a task without relying on the newer Task.WaitAsync API.</summary>
    /// <typeparam name="T">The task result type.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="timeout">The maximum wait interval.</param>
    /// <returns>The completed task result.</returns>
    internal static async Task<T> WaitAsync<T>(Task<T> task, TimeSpan timeout)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
        if (completed != task)
        {
            throw new TimeoutException("The expected asynchronous test result was not produced.");
        }

        return await task.ConfigureAwait(false);
    }

    /// <summary>Cancels a source through the common synchronous cancellation contract.</summary>
    /// <param name="source">The source to cancel.</param>
    /// <returns>A completed task after cancellation has been requested.</returns>
    internal static Task CancelAsync(CancellationTokenSource source)
    {
        source.Cancel();
        return Task.CompletedTask;
    }
}
