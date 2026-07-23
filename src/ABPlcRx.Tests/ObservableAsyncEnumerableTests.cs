// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using TUnit.Assertions;
using TUnit.Core;

namespace IoT.DriverCore.ABPlcRx.Tests;

/// <summary>Tests classic observable consumption through asynchronous enumeration.</summary>
public sealed class ObservableAsyncEnumerableTests
{
    /// <summary>First buffered sample.</summary>
    private const int FirstValue = 17;

    /// <summary>Second buffered sample.</summary>
    private const int SecondValue = 29;

    /// <summary>Verifies synchronously buffered values drain before source completion.</summary>
    /// <returns><see cref="Task"/> representing the test.</returns>
    [Test]
    internal async Task CreateDrainsSynchronousValuesAndCompletionAsync()
    {
        var source = new SynchronousObservable<int>([FirstValue, SecondValue], error: null);
        var values = new List<int>();

        await foreach (var value in ObservableAsyncEnumerable.Create(source, CancellationToken.None))
        {
            values.Add(value);
        }

        await Assert.That(values).IsEquivalentTo([FirstValue, SecondValue]);
        await Assert.That(source.DisposeCount).IsEqualTo(1);
    }

    /// <summary>Verifies asynchronous notifications wake the pending consumer and complete it.</summary>
    /// <returns><see cref="Task"/> representing the test.</returns>
    [Test]
    internal async Task CreateWaitsForAsynchronousValuesAndCompletionAsync()
    {
        var source = new ManualObservable<int>();
        await using var enumerator = ObservableAsyncEnumerable
            .Create(source, CancellationToken.None)
            .GetAsyncEnumerator();
        var firstMove = enumerator.MoveNextAsync().AsTask();
        source.Next(FirstValue);

        await Assert.That(await firstMove).IsTrue();
        await Assert.That(enumerator.Current).IsEqualTo(FirstValue);

        var completionMove = enumerator.MoveNextAsync().AsTask();
        source.Completed();

        await Assert.That(await completionMove).IsFalse();
        await Assert.That(source.SubscriberCount).IsEqualTo(0);
    }

    /// <summary>Verifies source errors are rethrown by asynchronous enumeration.</summary>
    /// <returns><see cref="Task"/> representing the test.</returns>
    [Test]
    internal async Task CreateRethrowsSourceErrorsAsync()
    {
        var expected = new InvalidOperationException("source failure");
        var source = new SynchronousObservable<int>([], expected);
        await using var enumerator = ObservableAsyncEnumerable
            .Create(source, CancellationToken.None)
            .GetAsyncEnumerator();

        await Assert.That(async () => await enumerator.MoveNextAsync())
            .Throws<InvalidOperationException>();
    }

    /// <summary>Verifies cancellation stops a pending wait and enumeration disposal unsubscribes.</summary>
    /// <returns><see cref="Task"/> representing the test.</returns>
    [Test]
    internal async Task CreateCancelsPendingWaitAndUnsubscribesAsync()
    {
        var source = new ManualObservable<int>();
        using var cancellation = new CancellationTokenSource();
        await using var enumerator = ObservableAsyncEnumerable
            .Create(source, cancellation.Token)
            .GetAsyncEnumerator();
        var move = enumerator.MoveNextAsync().AsTask();
        await TestCompatibility.CancelAsync(cancellation);

        await Assert.That(async () => await move).Throws<OperationCanceledException>();
        await enumerator.DisposeAsync();
        await Assert.That(source.SubscriberCount).IsEqualTo(0);
    }

    /// <summary>Manual observable used to control asynchronous notifications.</summary>
    /// <typeparam name="T">The notification type.</typeparam>
    private sealed class ManualObservable<T> : IObservable<T>
    {
        /// <summary>Subscribed observers.</summary>
        private readonly List<IObserver<T>> _observers = [];

        /// <summary>Gets the active subscriber count.</summary>
        internal int SubscriberCount => _observers.Count;

        /// <inheritdoc/>
        public IDisposable Subscribe(IObserver<T> observer)
        {
            _observers.Add(observer);
            return new ActionDisposable(() => _ = _observers.Remove(observer));
        }

        /// <summary>Publishes a value.</summary>
        /// <param name="value">The value.</param>
        internal void Next(T value)
        {
            foreach (var observer in _observers.ToArray())
            {
                observer.OnNext(value);
            }
        }

        /// <summary>Publishes completion.</summary>
        internal void Completed()
        {
            foreach (var observer in _observers.ToArray())
            {
                observer.OnCompleted();
            }
        }
    }

    /// <summary>Synchronous observable that emits during subscription.</summary>
    /// <typeparam name="T">The notification type.</typeparam>
    /// <param name="values">Values to publish.</param>
    /// <param name="error">Optional terminal error.</param>
    private sealed class SynchronousObservable<T>(IReadOnlyCollection<T> values, Exception? error) : IObservable<T>
    {
        /// <summary>Gets the disposal count.</summary>
        internal int DisposeCount { get; private set; }

        /// <inheritdoc/>
        public IDisposable Subscribe(IObserver<T> observer)
        {
            foreach (var value in values)
            {
                observer.OnNext(value);
            }

            if (error is null)
            {
                observer.OnCompleted();
            }
            else
            {
                observer.OnError(error);
            }

            return new ActionDisposable(() => DisposeCount++);
        }
    }

    /// <summary>Disposable wrapper around one teardown action.</summary>
    /// <param name="dispose">The teardown action.</param>
    private sealed class ActionDisposable(Action dispose) : IDisposable
    {
        /// <inheritdoc/>
        public void Dispose() => dispose();
    }
}
