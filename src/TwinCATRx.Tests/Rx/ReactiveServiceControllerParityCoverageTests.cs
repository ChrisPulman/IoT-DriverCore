// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using System.ServiceProcess;
#if !NETFRAMEWORK
using System.Runtime.Versioning;
#endif
using ReactiveServiceController = IoT.Driver.TwinCATRx.Reactive.ObservableServiceController;
using ReactiveServiceControllerRuntime = IoT.Driver.TwinCATRx.Reactive.IServiceControllerRuntime;

namespace IoT.Driver.TwinCATRx.Tests.Rx;

/// <summary>Non-live lifecycle parity tests for the Reactive service wrapper.</summary>
#if !NETFRAMEWORK
[SupportedOSPlatform("windows")]
#endif
public class ReactiveServiceControllerParityCoverageTests
{
    /// <summary>The bounded time allowed for race-test coordination.</summary>
    private static readonly TimeSpan RaceTimeout = TimeSpan.FromSeconds(5);

    /// <summary>The interval used to prove disposal is waiting for an in-flight poll.</summary>
    private static readonly TimeSpan RaceObservationInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>Verifies the Reactive build serializes status publication with disposal.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Dispose_During_A_Failing_Status_Read_Is_Serialized_With_Reactive_PollingAsync()
    {
        using var statusReadEntered = new ManualResetEventSlim();
        using var allowStatusRead = new ManualResetEventSlim();
        using var disposalTaskStarted = new ManualResetEventSlim();
        var expectedError = new InvalidOperationException("service disposed during reactive status read");
        var runtime = new BlockingServiceControllerRuntime(
            statusReadEntered,
            allowStatusRead,
            expectedError);
        var ticks = new ManualObservable<long>();
        using var controller = new ReactiveServiceController(runtime, ticks);
        var statuses = new RecordingObserver<ServiceControllerStatus>();
        using var subscription = controller.StatusObserver.Subscribe(statuses);

        var pollingTask = Task.Run(() => ticks.Emit(0));
        var statusReadWasObserved = statusReadEntered.Wait(RaceTimeout);
        Task? disposeTask = null;
        var disposalTaskWasObserved = false;
        var disposalWaitedForPolling = false;
        if (statusReadWasObserved)
        {
            disposeTask = Task.Run(() =>
            {
                disposalTaskStarted.Set();
                controller.Dispose();
            });
            disposalTaskWasObserved = disposalTaskStarted.Wait(RaceTimeout);
            if (disposalTaskWasObserved)
            {
                var firstCompleted = await Task.WhenAny(disposeTask, Task.Delay(RaceObservationInterval));
                disposalWaitedForPolling = !ReferenceEquals(firstCompleted, disposeTask);
            }
        }

        allowStatusRead.Set();
        Exception? pollingException = null;
        try
        {
            await pollingTask;
        }
        catch (Exception ex)
        {
            pollingException = ex;
        }

        if (disposeTask is not null)
        {
            await disposeTask;
        }

        await TUnitAssert.That(statusReadWasObserved).IsTrue();
        await TUnitAssert.That(disposalTaskWasObserved).IsTrue();
        await TUnitAssert.That(disposalWaitedForPolling).IsTrue();
        await TUnitAssert.That(pollingException).IsNull();
        await TUnitAssert.That(statuses.Errors).Count().IsEqualTo(1);
        await TUnitAssert.That(statuses.Errors[0]).IsSameReferenceAs(expectedError);
    }

    /// <summary>Verifies null-state getters and disposed commands never query Windows services.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Null_State_And_Disposed_Commands_Do_Not_Query_Service_ManagerAsync()
    {
        var service = new ServiceController();
        var controller = new TestObservableServiceController(service, TimeSpan.FromHours(1));
        SetWrappedService(controller, null);

        await TUnitAssert.That(controller.CanStop).IsFalse();
        await TUnitAssert.That(controller.DisplayName).IsEmpty();
        await TUnitAssert.That(controller.ServiceName).IsEmpty();
        await TUnitAssert.That(controller.Status).IsEqualTo(ServiceControllerStatus.Stopped);
        await TUnitAssert.That(controller.StatusObserver).IsNotNull();
        await TUnitAssert.That(controller.IsDisposed).IsFalse();

        controller.ExposeDispose(false);
        await TUnitAssert.That(controller.IsDisposed).IsFalse();

        controller.Dispose();
        controller.Start();
        controller.Stop();
        controller.Restart();
        controller.Dispose();

        await TUnitAssert.That(controller.IsDisposed).IsTrue();
    }

    /// <summary>Replaces the wrapped controller for null-state branch validation.</summary>
    /// <param name="controller">The observable wrapper.</param>
    /// <param name="service">The replacement service.</param>
    private static void SetWrappedService(ReactiveServiceController controller, ServiceController? service) =>
        (typeof(ReactiveServiceController)
            .GetField("_serviceController", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(ReactiveServiceController).FullName, "_serviceController"))
            .SetValue(controller, service);

    /// <summary>Test wrapper exposing the protected disposal overload.</summary>
    private sealed class TestObservableServiceController : ReactiveServiceController
    {
        /// <summary>Initializes a new instance of the <see cref="TestObservableServiceController"/> class.</summary>
        /// <param name="service">The unconnected service-controller object.</param>
        /// <param name="interval">The polling interval.</param>
        public TestObservableServiceController(ServiceController service, TimeSpan interval)
            : base(service, interval)
        {
        }

        /// <summary>Invokes the protected disposal path.</summary>
        /// <param name="disposing">Whether managed resources should be disposed.</param>
        public void ExposeDispose(bool disposing) => Dispose(disposing);
    }

    /// <summary>Reactive service runtime that blocks a failing status read.</summary>
    /// <remarks>Initializes a new instance of the <see cref="BlockingServiceControllerRuntime"/> class.</remarks>
    /// <param name="statusReadEntered">The signal raised when a status read begins.</param>
    /// <param name="allowStatusRead">The gate that releases the status read.</param>
    /// <param name="statusError">The error raised after release.</param>
    private sealed class BlockingServiceControllerRuntime(
        ManualResetEventSlim statusReadEntered,
        ManualResetEventSlim allowStatusRead,
        Exception statusError) : ReactiveServiceControllerRuntime
    {
        /// <inheritdoc/>
        public event EventHandler? Disposed;

        /// <inheritdoc/>
        public bool CanStop => false;

        /// <inheritdoc/>
        public string DisplayName => "TwinCAT System";

        /// <inheritdoc/>
        public string ServiceName => "TcSysSrv";

        /// <inheritdoc/>
        public ServiceControllerStatus Status
        {
            get
            {
                statusReadEntered.Set();
                allowStatusRead.Wait();
                throw statusError;
            }
        }

        /// <inheritdoc/>
        public void Dispose() => Disposed?.Invoke(this, EventArgs.Empty);

        /// <inheritdoc/>
        public void Refresh()
        {
        }

        /// <inheritdoc/>
        public void Start()
        {
        }

        /// <inheritdoc/>
        public void Stop()
        {
        }

        /// <inheritdoc/>
        public void WaitForStatus(ServiceControllerStatus status) => _ = status;
    }

    /// <summary>Manually triggered observable sequence.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    private sealed class ManualObservable<T> : IObservable<T>
    {
        /// <summary>Stores observers.</summary>
        private readonly List<IObserver<T>> _observers = [];

        /// <summary>Emits one value.</summary>
        /// <param name="value">The value.</param>
        public void Emit(T value)
        {
            foreach (var observer in _observers.ToArray())
            {
                observer.OnNext(value);
            }
        }

        /// <inheritdoc/>
        public IDisposable Subscribe(IObserver<T> observer)
        {
            _observers.Add(observer);
            return new Subscription(_observers, observer);
        }

        /// <summary>Removes one observer.</summary>
        /// <remarks>Initializes a new instance of the <see cref="Subscription"/> class.</remarks>
        /// <param name="observers">The observer collection.</param>
        /// <param name="observer">The observer to remove.</param>
        private sealed class Subscription(List<IObserver<T>> observers, IObserver<T> observer) : IDisposable
        {
            /// <inheritdoc/>
            public void Dispose() => _ = observers.Remove(observer);
        }
    }

    /// <summary>Records observable errors.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    private sealed class RecordingObserver<T> : IObserver<T>
    {
        /// <summary>Gets observed errors.</summary>
        public List<Exception> Errors { get; } = [];

        /// <inheritdoc/>
        public void OnCompleted()
        {
        }

        /// <inheritdoc/>
        public void OnError(Exception error) => Errors.Add(error);

        /// <inheritdoc/>
        public void OnNext(T value) => _ = value;
    }
}
