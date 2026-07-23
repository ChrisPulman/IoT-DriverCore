// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiTagGroupSnapshot record.</summary>
/// <param name="GroupName">The GroupName parameter.</param>
/// <param name="Values">The Values parameter.</param>
public sealed record MitsubishiTagGroupSnapshot(
    string GroupName,
    IReadOnlyDictionary<string, object?> Values)
{
    /// <summary>Gets or sets the TagNames property.</summary>
    public IReadOnlyList<string> TagNames => Values.Keys.ToArray();

    /// <summary>Executes the GetRequired operation.</summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="tag">The typed logical tag key.</param>
    /// <returns>The GetRequired operation result.</returns>
    public T GetRequired<T>(LogicalTagKey<T> tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        var tagName = tag.Name;
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        if (!Values.TryGetValue(tagName, out var value))
        {
            throw new KeyNotFoundException(
                $"Tag '{tagName}' was not present in snapshot '{GroupName}'.");
        }

        return MitsubishiTagValueConverter.Require(value, tag);
    }

    /// <summary>Executes the GetOptional operation.</summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="tag">The typed logical tag key.</param>
    /// <returns>The GetOptional operation result.</returns>
    public T? GetOptional<T>(LogicalTagKey<T> tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        var tagName = tag.Name;
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        if (!Values.TryGetValue(tagName, out var value))
        {
            return default;
        }

        return value is T ? MitsubishiTagValueConverter.Require(value, tag) : default;
    }
}
