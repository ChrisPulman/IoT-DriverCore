// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using CP.IoT.Core;

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;

#else

namespace MitsubishiRx;

#endif

/// <summary>Converts dynamically read tag values to declared tag types.</summary>
public static class MitsubishiTagValueConverter
{
    /// <summary>Requires a value to match the supplied tag type.</summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="value">The dynamically read value.</param>
    /// <param name="tag">The typed logical tag key.</param>
    /// <returns>The typed value.</returns>
    /// <exception cref="InvalidCastException">The value does not match the expected type.</exception>
    public static T Require<T>(object? value, LogicalTagKey<T> tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        if (value is T typed)
        {
            return typed;
        }

        throw new InvalidCastException(
            $"Tag '{tag.Name}' returned '{value?.GetType().FullName ?? "null"}', " +
            $"not '{typeof(T).FullName}'.");
    }
}
