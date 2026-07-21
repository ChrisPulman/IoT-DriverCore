// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.IoT.Core;

/// <summary>Asynchronously observes value changes for logical tags.</summary>
public interface ILogicalTagObserver
{
    /// <summary>Observes a logical tag using the classic observable contract.</summary>
    /// <param name="tagName">The tag name to observe.</param>
    /// <returns>An observable sequence of tag values.</returns>
    IObservable<LogicalTagValue> Observe(string tagName);

    /// <summary>Observes a collection of tags using the classic observable contract.</summary>
    /// <param name="tagNames">The tag names to observe.</param>
    /// <returns>An observable sequence merging all observed tag values.</returns>
    IObservable<LogicalTagValue> ObserveMany(IReadOnlyCollection<string> tagNames);

    /// <summary>Observes a logical tag until cancellation using an async-enumerable sequence.</summary>
    /// <param name="tagName">The tag name to observe.</param>
    /// <param name="cancellationToken">A token that stops observation.</param>
    /// <returns>An async-enumerable sequence of tag values.</returns>
    IAsyncEnumerable<LogicalTagValue> ObserveAsync(string tagName, CancellationToken cancellationToken);

    /// <summary>Observes a collection of tags until cancellation using an async-enumerable sequence.</summary>
    /// <param name="tagNames">The tag names to observe.</param>
    /// <param name="cancellationToken">A token that stops observation.</param>
    /// <returns>An async-enumerable sequence merging all observed tag values.</returns>
    IAsyncEnumerable<LogicalTagValue> ObserveManyAsync(
        IReadOnlyCollection<string> tagNames,
        CancellationToken cancellationToken);
}
