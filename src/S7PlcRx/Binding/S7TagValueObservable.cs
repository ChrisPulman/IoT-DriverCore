// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Binding;

#else
namespace S7PlcRx.Binding;

#endif

/// <summary>Provides generated bindings with a small replaying observable that has no subject dependency.</summary>
/// <typeparam name="T">The generated property value type.</typeparam>
public sealed class S7TagValueObservable<T> : IObservable<T>
{
    /// <summary>Synchronizes access to the observer collection and replay value.</summary>
    private readonly Lock _gate = new();

    /// <summary>Contains the active observers.</summary>
    private readonly List<IObserver<T>> _observers = [];

    /// <summary>Contains the latest published value.</summary>
    private T? _value;

    /// <summary>Indicates whether a value is available for replay.</summary>
    private bool _hasValue;

    /// <inheritdoc />
    public IDisposable Subscribe(IObserver<T> observer)
    {
        if (observer is null)
        {
            throw new ArgumentNullException(nameof(observer));
        }

        T? value;
        bool hasValue;
        lock (_gate)
        {
            _observers.Add(observer);
            value = _value;
            hasValue = _hasValue;
        }

        if (hasValue)
        {
            observer.OnNext(value!);
        }

        return new Subscription(this, observer);
    }

    /// <summary>Publishes a property value and retains it for later subscribers.</summary>
    /// <param name="value">The new property value.</param>
    public void Publish(T value)
    {
        IObserver<T>[] observers;
        lock (_gate)
        {
            _value = value;
            _hasValue = true;
            observers = _observers.ToArray();
        }

        foreach (var observer in observers)
        {
            observer.OnNext(value);
        }
    }

    /// <summary>Removes an observer from the active collection.</summary>
    /// <param name="observer">The observer to remove.</param>
    private void Unsubscribe(IObserver<T> observer)
    {
        lock (_gate)
        {
            _ = _observers.Remove(observer);
        }
    }

    /// <summary>Owns one subscription to the observable.</summary>
    private sealed class Subscription : IDisposable
    {
        /// <summary>Contains the observable until disposal.</summary>
        private S7TagValueObservable<T>? _owner;

        /// <summary>Contains the observer until disposal.</summary>
        private IObserver<T>? _observer;

        /// <summary>Initializes a new instance of the <see cref="Subscription"/> class.</summary>
        /// <param name="owner">The observable that owns the subscription.</param>
        /// <param name="observer">The subscribed observer.</param>
        public Subscription(S7TagValueObservable<T> owner, IObserver<T> observer) =>
            (_owner, _observer) = (owner, observer);

        /// <inheritdoc />
        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            var observer = Interlocked.Exchange(ref _observer, null);
            if (owner is null || observer is null)
            {
                return;
            }

            owner.Unsubscribe(observer);
        }
    }
}
