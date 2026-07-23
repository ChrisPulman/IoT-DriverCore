// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.ModbusRx.UnitTests;

/// <summary>Portable helpers shared by deterministic tests across every supported target framework.</summary>
internal static class TestFrameworkCompatibilityExtensions
{
    /// <summary>Gets the Unix epoch on targets that predate the built-in Unix epoch property.</summary>
    internal static DateTimeOffset UnixEpoch { get; } =
        new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Returns all values of an enum using the API available on the current target.</summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <returns>All declared enum values.</returns>
    internal static TEnum[] GetEnumValues<TEnum>()
        where TEnum : struct, Enum
    {
#if NETFRAMEWORK
        return (TEnum[])Enum.GetValues(typeof(TEnum));
#else
        return Enum.GetValues<TEnum>();
#endif
    }

#if NETFRAMEWORK
    /// <summary>Waits for a task with a timeout on targets that predate <c>Task.WaitAsync</c>.</summary>
    /// <param name="task">The task to await.</param>
    /// <param name="timeout">The maximum wait duration.</param>
    /// <returns>A task representing the bounded wait.</returns>
    private static async Task WaitCoreAsync(Task task, TimeSpan timeout)
    {
        var timeoutTask = Task.Delay(timeout);
        if (await Task.WhenAny(task, timeoutTask).ConfigureAwait(false) != task)
        {
            throw new TimeoutException();
        }

        await task.ConfigureAwait(false);
    }

    /// <summary>Adds portable timeout support to non-generic tasks.</summary>
    /// <param name="task">The task receiver.</param>
    extension(Task task)
    {
        /// <summary>Waits for a task with a timeout.</summary>
        /// <param name="timeout">The maximum wait duration.</param>
        /// <returns>A task representing the bounded wait.</returns>
        internal Task WaitAsync(TimeSpan timeout) => WaitCoreAsync(task, timeout);
    }

    /// <summary>Adds portable timeout support to result tasks.</summary>
    /// <typeparam name="T">The task result type.</typeparam>
    /// <param name="task">The result-task receiver.</param>
    extension<T>(Task<T> task)
    {
        /// <summary>Waits for a result task with a timeout.</summary>
        /// <param name="timeout">The maximum wait duration.</param>
        /// <returns>The completed task result.</returns>
        internal async Task<T> WaitAsync(TimeSpan timeout)
        {
            await WaitCoreAsync(task, timeout).ConfigureAwait(false);
            return await task.ConfigureAwait(false);
        }
    }
#endif
}
