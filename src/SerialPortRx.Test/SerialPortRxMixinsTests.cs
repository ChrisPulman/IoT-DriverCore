// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Serial.Tests;

/// <summary>Tests for SerialPortRx mixin helpers.</summary>
public sealed class SerialPortRxMixinsTests
{
    /// <summary>Verifies scalar observable helpers emit converted characters.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task AsObservable_ForNumericValues_EmitsCharactersAsync()
    {
        var byteValue = await FirstValueAsync(SerialPortRxMixins.AsObservable(ByteLetterA));
        var intValue = await FirstValueAsync(SerialPortRxMixins.AsObservable(LetterB));
        var shortValue = await FirstValueAsync(SerialPortRxMixins.AsObservable(ShortLetterC));

        await Assert.That(byteValue).IsEqualTo('A');
        await Assert.That(intValue).IsEqualTo('B');
        await Assert.That(shortValue).IsEqualTo('C');
    }

    /// <summary>Verifies scalar async observable helpers emit converted characters.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task AsObservableAsync_ForNumericValues_EmitsCharactersAsync()
    {
        var byteValue = await FirstValueAsync(
            ObservableAsyncBridgeExtensions.ToObservable(SerialPortRxMixins.AsAsyncObservable(ByteLetterA)));
        var intValue = await FirstValueAsync(
            ObservableAsyncBridgeExtensions.ToObservable(SerialPortRxMixins.AsAsyncObservable(LetterB)));
        var shortValue = await FirstValueAsync(
            ObservableAsyncBridgeExtensions.ToObservable(SerialPortRxMixins.AsAsyncObservable(ShortLetterC)));

        await Assert.That(byteValue).IsEqualTo('A');
        await Assert.That(intValue).IsEqualTo('B');
        await Assert.That(shortValue).IsEqualTo('C');
    }

    /// <summary>Verifies async extension methods reject null ports.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task AsyncPortExtensions_WhenPortIsNull_ThrowAsync()
    {
        IPortRx? port = null;
        ISerialPortRx? serialPort = null;

        await Assert.That(() => SerialPortRxMixins.BytesReceivedAsyncObservable(port))
            .Throws<ArgumentNullException>();
        await Assert.That(() => SerialPortRxMixins.DataReceivedAsyncObservable(serialPort))
            .Throws<ArgumentNullException>();
        await Assert.That(() => SerialPortRxMixins.DataReceivedBytesAsyncObservable(serialPort))
            .Throws<ArgumentNullException>();
        await Assert.That(() => SerialPortRxMixins.LinesAsyncObservable(serialPort))
            .Throws<ArgumentNullException>();
        await Assert.That(() => SerialPortRxMixins.ErrorReceivedAsyncObservable(serialPort))
            .Throws<ArgumentNullException>();
        await Assert.That(() => SerialPortRxMixins.IsOpenAsyncObservable(serialPort))
            .Throws<ArgumentNullException>();
    }

    /// <summary>Verifies BufferUntil emits text between configured markers.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task BufferUntil_WhenMarkersAreFound_EmitsBufferedTextAsync()
    {
        using var source = new ReplaySignal<char>(0);
        var values = new List<string>();
        using var subscription = SerialPortRxMixins
            .BufferUntil(source, Observable.Return('['), Observable.Return(']'), Hundred)
            .Subscribe(values.Add);

        source.OnNext('x');
        source.OnNext('[');
        source.OnNext('A');
        source.OnNext(']');

        await Assert.That(values.Count).IsEqualTo(1);
        await Assert.That(values[0]).IsEqualTo("[A]");
    }

    /// <summary>Verifies ObservableAsync.Return emits a single value.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task ObservableAsyncReturn_EmitsSingleValueAsync()
    {
        var value = await FirstValueAsync(
            ObservableAsyncBridgeExtensions.ToObservable(ObservableAsync.Return(OneHundredTwentyThree)));

        await Assert.That(value).IsEqualTo(OneHundredTwentyThree);
    }

    /// <summary>Verifies pending request records store constructor values.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task PendingRequest_StoresConstructorValuesAsync()
    {
        var completion = new TaskCompletionSource<bool>();
        var request = new PendingRequest("G0", _ => { }, completion);

        await Assert.That(request.Command).IsEqualTo("G0");
        await Assert.That(request.Completion).IsEqualTo(completion);
    }

    /// <summary>Verifies every async buffer overload forwards marker-delimited values.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task BufferUntil_AsyncOverloads_ForwardDelimitedValuesAsync()
    {
        using var source = new ReplaySignal<char>(0);
        var asyncSource = ObservableAsyncBridgeExtensions.ToAsyncObservable(source);
        var asyncStart = ObservableAsync.Return('[');
        var asyncEnd = ObservableAsync.Return(']');
        var asyncDefault = ObservableAsync.Return("default");
        var values = new List<string>();
        using var subscriptions = new CompositeDisposable
        {
            ObservableAsyncBridgeExtensions.ToObservable(
                SerialPortRxMixins.BufferUntil(asyncSource, asyncStart, asyncEnd, Hundred)).Subscribe(values.Add),
        };
        subscriptions.Add(ObservableAsyncBridgeExtensions.ToObservable(
            SerialPortRxMixins.BufferUntil(asyncSource, asyncStart, asyncEnd, Hundred, null)).Subscribe(values.Add));
        subscriptions.Add(ObservableAsyncBridgeExtensions.ToObservable(
            SerialPortRxMixins.BufferUntil(asyncSource, asyncStart, asyncEnd, asyncDefault, Hundred))
            .Subscribe(values.Add));
        subscriptions.Add(ObservableAsyncBridgeExtensions.ToObservable(
            SerialPortRxMixins.BufferUntil(asyncSource, asyncStart, asyncEnd, asyncDefault, Hundred, null))
            .Subscribe(values.Add));

        source.OnNext('x');
        source.OnNext('[');
        source.OnNext('A');
        source.OnNext(']');

        await Assert.That(values.Count).IsEqualTo(Four);
        await Assert.That(values.All(value => value == "[A]")).IsTrue();
    }

    /// <summary>Verifies the default-value buffer overload emits after its timeout.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task BufferUntil_WithDefaultValue_EmitsOnTimeoutAsync()
    {
        using var source = new ReplaySignal<char>(0);
        var values = new List<string>();
        using var subscription = SerialPortRxMixins.BufferUntil(
            source,
            Observable.Return('['),
            Observable.Return(']'),
            Observable.Return("timeout"),
            1).Subscribe(values.Add);
        using var resetSource = new ReplaySignal<char>(0);
        using var resetSubscription = SerialPortRxMixins.BufferUntil(
            resetSource,
            Observable.Return('['),
            Observable.Return(']'),
            1).Subscribe();
        resetSource.OnNext('[');
        resetSource.OnNext('A');

        await Task.Delay(TwentyFive);

        await Assert.That(values).Contains("timeout");
    }

    /// <summary>Verifies async serial mixins forward every public in-memory port stream.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task AsyncPortMixins_ForwardInMemoryPortStreamsAsync()
    {
        using var pair = new InMemoryPortRxPair();
        using var readPair = new InMemoryPortRxPair();
        readPair.Second.EnableAutoDataReceive = false;
        var characters = new List<char>();
        var bytes = new List<byte>();
        var lines = new List<string>();
        var errors = new List<Exception>();
        var states = new List<bool>();
        var readBytes = new List<int>();
        using var subscriptions = new CompositeDisposable
        {
            ObservableAsyncBridgeExtensions.ToObservable(
                SerialPortRxMixins.DataReceivedAsyncObservable(pair.Second)).Subscribe(characters.Add),
        };
        subscriptions.Add(ObservableAsyncBridgeExtensions.ToObservable(
            SerialPortRxMixins.DataReceivedBytesAsyncObservable(pair.Second)).Subscribe(bytes.Add));
        subscriptions.Add(ObservableAsyncBridgeExtensions.ToObservable(
            SerialPortRxMixins.LinesAsyncObservable(pair.Second)).Subscribe(lines.Add));
        subscriptions.Add(ObservableAsyncBridgeExtensions.ToObservable(
            SerialPortRxMixins.ErrorReceivedAsyncObservable(pair.Second)).Subscribe(errors.Add));
        subscriptions.Add(ObservableAsyncBridgeExtensions.ToObservable(
            SerialPortRxMixins.IsOpenAsyncObservable(pair.Second)).Subscribe(states.Add));
        subscriptions.Add(ObservableAsyncBridgeExtensions.ToObservable(
            SerialPortRxMixins.BytesReceivedAsyncObservable(readPair.Second)).Subscribe(readBytes.Add));
        await pair.First.OpenAsync();
        await pair.Second.OpenAsync();
        await readPair.First.OpenAsync();
        await readPair.Second.OpenAsync();

        pair.First.WriteLine(HelloText);
        pair.InjectSecondError(new IOException("injected"));
        readPair.First.Write([ByteLetterA], 0, 1);
        _ = await readPair.Second.ReadAsync(new byte[1], 0, 1);

        await Assert.That(characters).Contains('H');
        await Assert.That(bytes).Contains(ByteLetterH);
        await Assert.That(lines).Contains(HelloText);
        await Assert.That(errors.Count).IsEqualTo(1);
        await Assert.That(states).Contains(true);
        await Assert.That(readBytes).Contains(LetterA);
    }

    /// <summary>Verifies interval, port-name, and raw system event observer helpers are subscribable.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task MonitoringMixins_EmitAndSystemObserversSubscribeAsync(CancellationToken cancellationToken)
    {
        using var pair = new InMemoryPortRxPair();
        await pair.First.OpenAsync();
        var openValue = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var openSubscription = SerialPortRxMixins.WhileIsOpen(pair.First, TimeSpan.FromMilliseconds(1))
            .Subscribe(value => openValue.TrySetResult(value));
        using var asyncOpenSubscription = ObservableAsyncBridgeExtensions.ToObservable(
            SerialPortRxMixins.WhileIsOpenAsyncObservable(pair.First, TimeSpan.FromMilliseconds(1)))
            .Subscribe();
        var names = await FirstValueAsync(
            ObservableAsyncBridgeExtensions.ToObservable(SerialPortRxMixins.PortNamesAsyncObservable(1, 1)));
        using var systemPort = new SerialPort();
        using var dataSubscription = SerialPortRxMixins.DataReceivedObserver(systemPort).Subscribe();
        using var errorSubscription = SerialPortRxMixins.ErrorReceivedObserver(systemPort).Subscribe();

        await Assert.That(await openValue.Task.WaitAsync(TimeSpan.FromSeconds(Two), cancellationToken)).IsTrue();
        await Assert.That(names.Length).IsGreaterThanOrEqualTo(1);
        await Assert.That(SerialPortRxMixins.PortNamesAsyncObservable()).IsNotNull();
        await Assert.That(SerialPortRxMixins.PortNamesAsyncObservable(Hundred)).IsNotNull();
    }

    /// <summary>Returns the first value observed from an observable sequence.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="observable">The observable sequence.</param>
    /// <returns>A task that completes with the first observed value.</returns>
    private static Task<T> FirstValueAsync<T>(IObservable<T> observable)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscription = observable.Subscribe(
            value => _ = completion.TrySetResult(value),
            exception => _ = completion.TrySetException(exception),
            () =>
            {
                if (completion.Task.IsCompleted)
                {
                    return;
                }

                _ = completion.TrySetException(new InvalidOperationException("Observable completed without a value."));
            });

        return completion.Task.ContinueWith(
            task =>
            {
                subscription.Dispose();
                return task.GetAwaiter().GetResult();
            },
            TaskScheduler.Default);
    }
}
