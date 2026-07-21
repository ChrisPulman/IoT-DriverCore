// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using CP.IoT.Core;

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;

#else

namespace MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiRx type.</summary>
public sealed partial class MitsubishiRx
{
    /// <summary>Executes the CreateReactiveWordWritePipeline operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="mode">The mode parameter.</param>
    /// <param name="coalescingWindow">The coalescingWindow parameter.</param>
    /// <returns>The CreateReactiveWordWritePipeline operation result.</returns>
    public MitsubishiReactiveWritePipeline<IReadOnlyList<ushort>> CreateReactiveWordWritePipeline(
        string address,
        MitsubishiReactiveWriteMode mode,
        TimeSpan? coalescingWindow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        return new MitsubishiReactiveWritePipeline<IReadOnlyList<ushort>>(
            _scheduler,
            $"Words:{address}",
            mode,
            payload => WriteWordsAsync(address, payload, CancellationToken.None),
            coalescingWindow);
    }

    /// <summary>Executes the CreateReactiveTagWritePipeline operation.</summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="tag">The typed logical tag key.</param>
    /// <param name="mode">The mode parameter.</param>
    /// <param name="coalescingWindow">The coalescingWindow parameter.</param>
    /// <returns>The CreateReactiveTagWritePipeline operation result.</returns>
    public MitsubishiReactiveWritePipeline<T> CreateReactiveTagWritePipeline<T>(
        LogicalTagKey<T> tag,
        MitsubishiReactiveWriteMode mode,
        TimeSpan? coalescingWindow)
    {
        ArgumentNullException.ThrowIfNull(tag);
        var tagName = tag.Name;
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        _ = GetRequiredTag(tagName);
        return new MitsubishiReactiveWritePipeline<T>(
            _scheduler,
            $"Tag:{tagName}",
            mode,
            payload => WriteTagValueAsync(tagName, payload, CancellationToken.None),
            coalescingWindow);
    }
}
