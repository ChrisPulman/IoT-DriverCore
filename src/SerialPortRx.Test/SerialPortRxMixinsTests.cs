// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.IO.Ports.Tests;

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
