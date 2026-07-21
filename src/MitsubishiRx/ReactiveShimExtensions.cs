// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

using System.Reactive.Concurrency;
using ReactiveUI.Primitives.Extensions;

namespace MitsubishiRx.Reactive;

/// <summary>
/// Provides disambiguation shim extension members that resolve CS0121 ambiguity arising when
/// both <c>ReactiveUI.Primitives.Extensions</c> and <c>ReactiveUI.Primitives.Extensions.Reactive</c>
/// namespaces are active as global usings in the reactive compilation unit.
/// Methods forward to <c>ReactiveUI.Primitives.Extensions.Reactive.ReactiveExtensions</c>
/// using fully-qualified names so the correct overload is always selected.
/// </summary>
internal static class ReactiveShimExtensions
{
    /// <summary>Provides reactive observable extension shims for <see cref="IObservable{T}"/>.</summary>
    /// <typeparam name="T">The type of elements in the observable sequence.</typeparam>
    /// <param name="source">The observable source sequence.</param>
    extension<T>(IObservable<T> source)
        where T : notnull
    {
        /// <summary>Invokes an action on subscription to the observable sequence.</summary>
        /// <param name="action">The action to invoke on subscription.</param>
        /// <returns>The source sequence with a subscription side effect applied.</returns>
        internal IObservable<T> DoOnSubscribe(Action action) =>
            ReactiveUI.Primitives.Extensions.Reactive.ReactiveExtensions.DoOnSubscribe(source, action);

        /// <summary>Invokes an action when the subscription to the observable sequence is disposed.</summary>
        /// <param name="action">The action to invoke on disposal.</param>
        /// <returns>The source sequence with a disposal side effect applied.</returns>
        internal IObservable<T> DoOnDispose(Action action) =>
            ReactiveUI.Primitives.Extensions.Reactive.ReactiveExtensions.DoOnDispose(source, action);

        /// <summary>Conflates elements of the observable sequence over a time interval so that only the latest element in each window is forwarded.</summary>
        /// <param name="interval">The conflation window duration.</param>
        /// <param name="scheduler">The scheduler to use for conflation timing.</param>
        /// <returns>A conflated observable sequence emitting at most one element per interval.</returns>
        internal IObservable<T> Conflate(TimeSpan interval, IScheduler scheduler) =>
            ReactiveUI.Primitives.Extensions.Reactive.ReactiveExtensions.Conflate(source, interval, scheduler);

        /// <summary>Emits a repeated heartbeat element at a regular interval whenever the source produces no element within that interval.</summary>
        /// <param name="interval">The heartbeat interval.</param>
        /// <param name="scheduler">The scheduler to use for heartbeat timing.</param>
        /// <returns>An observable sequence with injected <see cref="Heartbeat{T}"/> elements.</returns>
        internal IObservable<Heartbeat<T>> Heartbeat(TimeSpan interval, IScheduler scheduler) =>
            ReactiveUI.Primitives.Extensions.Reactive.ReactiveExtensions.Heartbeat(source, interval, scheduler);

        /// <summary>Detects staleness by emitting a notification when no new element has been received within the specified timeout.</summary>
        /// <param name="timeout">The staleness detection timeout duration.</param>
        /// <param name="scheduler">The scheduler to use for staleness detection timing.</param>
        /// <returns>An observable sequence that signals when the source becomes stale via <see cref="Stale{T}"/> elements.</returns>
        internal IObservable<Stale<T>> DetectStale(TimeSpan timeout, IScheduler scheduler) =>
            ReactiveUI.Primitives.Extensions.Reactive.ReactiveExtensions.DetectStale(source, timeout, scheduler);

        /// <summary>Samples the latest element of the source observable whenever the sampler observable emits a signal.</summary>
        /// <param name="sampler">The sampler observable that triggers each sample.</param>
        /// <returns>An observable sequence containing the latest sampled element on each sampler signal.</returns>
        internal IObservable<T> SampleLatest(IObservable<object> sampler) =>
            ReactiveUI.Primitives.Extensions.Reactive.ReactiveExtensions.SampleLatest(source, sampler);

        /// <summary>Projects each element sequentially through an asynchronous selector, awaiting completion before processing the next element.</summary>
        /// <typeparam name="TOut">The type of the projected result elements.</typeparam>
        /// <param name="selector">The asynchronous projection function applied to each element.</param>
        /// <returns>An observable sequence of projected elements in sequential order.</returns>
        internal IObservable<TOut> SelectAsyncSequential<TOut>(Func<T, Task<TOut>> selector)
            where TOut : notnull =>
            ReactiveUI.Primitives.Extensions.Reactive.ReactiveExtensions.SelectAsyncSequential(source, selector);

        /// <summary>Projects each element through an asynchronous selector, cancelling the in-flight projection when a new element arrives.</summary>
        /// <typeparam name="TOut">The type of the projected result elements.</typeparam>
        /// <param name="selector">The asynchronous projection function applied to each element.</param>
        /// <returns>An observable sequence of the latest projected elements.</returns>
        internal IObservable<TOut> SelectLatestAsync<TOut>(Func<T, Task<TOut>> selector)
            where TOut : notnull =>
            ReactiveUI.Primitives.Extensions.Reactive.ReactiveExtensions.SelectLatestAsync(source, selector);
    }
}

#endif
