// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.ExceptionServices;
using ReactiveUI.Primitives.Async;

#if REACTIVE_SHIM
namespace IoT.DriverCore.TwinCATRx.Reactive;
#else
namespace IoT.DriverCore.TwinCATRx;
#endif

/// <summary>Observable bridge helpers.</summary>
public static class ObservableBridgeExtensions
{
    /// <summary>Converts an observable sequence to a ReactiveUI.Primitives async observable sequence.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <returns>An async observable that subscribes to the source observable.</returns>
    public static IObservableAsync<T> ToAsyncObservable<T>(IObservable<T> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return new ObservableAsyncAdapter<T>(source);
    }

    /// <summary>Subscribes to an observable sequence without handling values.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <returns>The subscription.</returns>
    public static IDisposable SubscribeTo<T>(IObservable<T> source) =>
        SubscribeTo(source, static _ => { });

    /// <summary>Subscribes to an observable sequence using an action for values.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="onNext">The value handler.</param>
    /// <returns>The subscription.</returns>
    public static IDisposable SubscribeTo<T>(IObservable<T> source, Action<T> onNext) =>
        SubscribeTo(source, onNext, ThrowObservableError, static () => { });

    /// <summary>Subscribes to an observable sequence using actions for all notifications.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="onNext">The value handler.</param>
    /// <param name="onError">The error handler.</param>
    /// <param name="onCompleted">The completion handler.</param>
    /// <returns>The subscription.</returns>
    public static IDisposable SubscribeTo<T>(
        IObservable<T> source,
        Action<T> onNext,
        Action<Exception> onError,
        Action onCompleted)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (onNext is null)
        {
            throw new ArgumentNullException(nameof(onNext));
        }

        if (onError is null)
        {
            throw new ArgumentNullException(nameof(onError));
        }

        if (onCompleted is null)
        {
            throw new ArgumentNullException(nameof(onCompleted));
        }

        return source.Subscribe(new ActionObserver<T>(onNext, onError, onCompleted));
    }

    /// <summary>Throws an observable error while preserving the original stack trace.</summary>
    /// <param name="error">The observable error.</param>
    private static void ThrowObservableError(Exception error) => ExceptionDispatchInfo.Capture(error).Throw();
}
