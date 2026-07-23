// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Ports;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Async;
using TUnit.Core;
using ReactiveBcd16 = global::IoT.DriverCore.OmronPlcRx.Reactive.Core.Types.Bcd16;
using ReactiveOmronSerialOptions = global::IoT.DriverCore.OmronPlcRx.Reactive.OmronSerialOptions;
using ReactiveOmronSerialProtocol = global::IoT.DriverCore.OmronPlcRx.Reactive.OmronSerialProtocol;

namespace IoT.DriverCore.OmronPlcRx.Reactive.Tests;

/// <summary>Tests the shared-source reactive shim project.</summary>
public sealed class ReactiveShimProjectTests
{
    /// <summary>PLC tag name for the tank level.</summary>
    private const string TankLevelTagName = "TankLevel";

    /// <summary>Initial tank level published by reactive generated-stream tests.</summary>
    private const short InitialTankLevel = 123;

    /// <summary>Updated tank level written by reactive generated-stream tests.</summary>
    private const short UpdatedTankLevel = 456;

    /// <summary>BCD temperature used by reactive generated-stream tests.</summary>
    private const short BcdTemperature = 235;

    /// <summary>Toolbus baud rate used by serial option tests.</summary>
    private const int ToolbusBaudRate = 115_200;

    /// <summary>Toolbus data bits used by serial option tests.</summary>
    private const int ToolbusDataBits = 8;

    /// <summary>Toolbus maximum frame length used by serial option tests.</summary>
    private const int ToolbusMaximumFrameLength = 1004;

    /// <summary>Timeout used for asynchronous stream observations.</summary>
    private const int ObservationTimeoutSeconds = 5;

    /// <summary>Verifies generated bindings target the reactive namespace and facade.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ReactiveGeneratedStreams_RegisterBindUpdateAndWriteTagsAsync()
    {
        var plc = new ReactiveFakeOmronPlcRx();
        var state = new ReactiveGeneratedMachineState();
        var observedLevels = new List<short>();

        using var levelSubscription = state.TankLevelObservable.SubscribeSafe(
            observedLevels.Add,
            static exception => throw exception);
        using var binding = state.BindPlcTags(plc);

        plc.Publish(TankLevelTagName, InitialTankLevel);
        plc.Publish("BcdTemp", new ReactiveBcd16(BcdTemperature));

        var asyncValue = await FirstValueAsync(state.TankLevelObservableAsync);
        await state.WriteTankLevelAsync(UpdatedTankLevel, CancellationToken.None);

        await Assert.That(state.TankLevel).IsEqualTo(UpdatedTankLevel);
        await Assert.That(state.BcdTemp).IsEqualTo(new ReactiveBcd16(BcdTemperature));
        await Assert.That(asyncValue).IsEqualTo(InitialTankLevel);
        await Assert.That(observedLevels.Contains(InitialTankLevel)).IsTrue();
        await Assert.That(HasRegistration(plc.Registrations, TankLevelTagName, "D100", typeof(short))).IsTrue();
        await Assert.That(HasRegistration(plc.Registrations, "BcdTemp", "D700", typeof(ReactiveBcd16))).IsTrue();
        await Assert.That(HasWrite(plc.Writes, TankLevelTagName, UpdatedTankLevel)).IsTrue();
    }

    /// <summary>Verifies the reactive project exposes serial settings through the shared source namespace.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ReactiveSerialOptions_CreateToolbusUsesReactiveNamespaceAsync()
    {
        var options = ReactiveOmronSerialOptions.CreateToolbus("COM3");

        await Assert.That(options.PortName).IsEqualTo("COM3");
        await Assert.That(options.Protocol).IsEqualTo(ReactiveOmronSerialProtocol.Toolbus);
        await Assert.That(options.BaudRate).IsEqualTo(ToolbusBaudRate);
        await Assert.That(options.DataBits).IsEqualTo(ToolbusDataBits);
        await Assert.That(options.Parity).IsEqualTo(Parity.None);
        await Assert.That(options.StopBits).IsEqualTo(StopBits.One);
        await Assert.That(options.MaximumFrameLength).IsEqualTo(ToolbusMaximumFrameLength);
    }

    /// <summary>Gets the first value published by an async observable.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="source">The async observable source.</param>
    /// <returns>The first observed value.</returns>
    private static async Task<T> FirstValueAsync<T>(IObservableAsync<T> source)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(ObservationTimeoutSeconds));
        var observer = new FirstValueObserver<T>();
        await using var subscription = await source
            .SubscribeAsync(observer, timeout.Token)
            .ConfigureAwait(false);
        return await observer.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
    }

    /// <summary>Checks whether a registration was captured.</summary>
    /// <param name="registrations">The captured registrations.</param>
    /// <param name="tagName">The expected tag name.</param>
    /// <param name="address">The expected PLC address.</param>
    /// <param name="tagType">The expected tag type.</param>
    /// <returns><see langword="true"/> when the registration was found.</returns>
    private static bool HasRegistration(
        IEnumerable<ReactiveFakeOmronPlcRx.Registration> registrations,
        string tagName,
        string address,
        Type tagType)
    {
        foreach (var registration in registrations)
        {
            if (
                registration.TagName == tagName
                && registration.Address == address
                && registration.TagType == tagType
            )
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Checks whether a write was captured.</summary>
    /// <param name="writes">The captured writes.</param>
    /// <param name="tagName">The expected tag name.</param>
    /// <param name="value">The expected written value.</param>
    /// <returns><see langword="true"/> when the write was found.</returns>
    private static bool HasWrite(
        IEnumerable<ReactiveFakeOmronPlcRx.Write> writes,
        string tagName,
        object value)
    {
        foreach (var write in writes)
        {
            if (write.TagName == tagName && Equals(write.Value, value))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Observer that completes after the first async observable value.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    private sealed class FirstValueObserver<T> : IObserverAsync<T>
    {
        /// <summary>Completes when the first value, error, or completion arrives.</summary>
        private readonly TaskCompletionSource<T> _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the completion task.</summary>
        public Task<T> Task => _completion.Task;

        /// <inheritdoc />
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        /// <inheritdoc />
        public ValueTask OnCompletedAsync(Result result)
        {
            _ = _completion.TrySetException(
                new InvalidOperationException("The async observable completed without a value."));
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken)
        {
            _ = _completion.TrySetException(error);
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask OnNextAsync(T value, CancellationToken cancellationToken)
        {
            _ = _completion.TrySetResult(value);
            return ValueTask.CompletedTask;
        }
    }
}
