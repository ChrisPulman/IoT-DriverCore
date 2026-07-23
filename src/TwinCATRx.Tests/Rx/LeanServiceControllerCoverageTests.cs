// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Reflection;
using System.ServiceProcess;
#if !NETFRAMEWORK
using System.Runtime.Versioning;
#endif
using IoT.DriverCore.TwinCATRx;

namespace IoT.DriverCore.TwinCATRx.Tests.Rx;

/// <summary>Non-live lifecycle tests for the observable service-controller wrapper.</summary>
#if !NETFRAMEWORK
[SupportedOSPlatform("windows")]
#endif
public class LeanServiceControllerCoverageTests
{
    /// <summary>The expected number of paired service operations.</summary>
    private const int ExpectedOperationCount = 2;

    /// <summary>The TwinCAT system service name.</summary>
    private const string TwinCatServiceName = "TcSysSrv";

    /// <summary>Verifies composed service commands and deterministic polling.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Composed_Service_Runtime_Covers_Commands_And_PollingAsync()
    {
        var runtime = new FakeServiceControllerRuntime
        {
            CanStop = true,
            Status = ServiceControllerStatus.Running,
        };
        var ticks = new ManualObservable<long>();
        using var controller = new ObservableServiceController(runtime, ticks);
        var statuses = new RecordingObserver<ServiceControllerStatus>();
        using var subscription = controller.StatusObserver.Subscribe(statuses);

        controller.Restart();
        runtime.Status = ServiceControllerStatus.Paused;
        controller.Restart();
        runtime.Status = ServiceControllerStatus.StartPending;
        controller.Restart();
        runtime.Status = ServiceControllerStatus.Stopped;
        runtime.RefreshedStatus = ServiceControllerStatus.Running;
        ticks.Emit(0);
        ticks.Emit(1);
        runtime.RaiseDisposed();
        runtime.RefreshedStatus = ServiceControllerStatus.Stopped;
        ticks.Emit(ExpectedOperationCount);

        await TUnitAssert.That(controller.CanStop).IsTrue();
        await TUnitAssert.That(controller.DisplayName).IsEqualTo("TwinCAT System");
        await TUnitAssert.That(controller.ServiceName).IsEqualTo(TwinCatServiceName);
        await TUnitAssert.That(runtime.StartCount).IsEqualTo(ExpectedOperationCount);
        await TUnitAssert.That(runtime.StopCount).IsEqualTo(ExpectedOperationCount);
        await TUnitAssert.That(runtime.WaitedStatuses).Contains(ServiceControllerStatus.Running);
        await TUnitAssert.That(runtime.WaitedStatuses).Contains(ServiceControllerStatus.Stopped);
        await TUnitAssert.That(statuses.Values).Contains(ServiceControllerStatus.Running);
        await TUnitAssert.That(runtime.RefreshCount).IsEqualTo(ExpectedOperationCount);
    }

    /// <summary>Verifies polling errors and service enumeration seams.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Composed_Service_Runtime_Covers_Errors_And_EnumerationAsync()
    {
        var invalidRuntime = new FakeServiceControllerRuntime();
        var invalidTicks = new ManualObservable<long>();
        using var invalidController = new ObservableServiceController(invalidRuntime, invalidTicks);
        var invalidStatuses = new RecordingObserver<ServiceControllerStatus>();
        using var invalidSubscription = invalidController.StatusObserver.Subscribe(invalidStatuses);
        invalidRuntime.StatusError = new InvalidOperationException("invalid service");
        invalidTicks.Emit(0);

        var win32Runtime = new FakeServiceControllerRuntime();
        var win32Ticks = new ManualObservable<long>();
        using var win32Controller = new ObservableServiceController(win32Runtime, win32Ticks);
        var win32Statuses = new RecordingObserver<ServiceControllerStatus>();
        using var win32Subscription = win32Controller.StatusObserver.Subscribe(win32Statuses);
        win32Runtime.StatusError = new Win32Exception("service manager");
        win32Ticks.Emit(0);

        var enumeratedRuntime = new FakeServiceControllerRuntime();
        var source = new FakeServiceControllerSource(enumeratedRuntime);
        var services = new RecordingObserver<ObservableServiceController>();
        using var servicesSubscription = ObservableServiceController
            .GetServices(source, TimeSpan.FromHours(1))
            .Subscribe(services);

        var unsupported = new RecordingObserver<ObservableServiceController>();
        using var unsupportedSubscription = ObservableServiceController
            .GetServices(new UnsupportedServiceControllerSource(), TimeSpan.FromHours(1))
            .Subscribe(unsupported);

        await TUnitAssert.That(invalidStatuses.Errors).Count().IsEqualTo(1);
        await TUnitAssert.That(win32Statuses.Errors).Count().IsEqualTo(1);
        await TUnitAssert.That(services.Values).Count().IsEqualTo(1);
        await TUnitAssert.That(services.Values.Single().ServiceName).IsEqualTo(TwinCatServiceName);
        await TUnitAssert.That(unsupported.Completed).IsTrue();
    }

    /// <summary>Verifies null-state getters and disposed command guards without querying Windows services.</summary>
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
    private static void SetWrappedService(ObservableServiceController controller, ServiceController? service) =>
        (typeof(ObservableServiceController)
            .GetField("_serviceController", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(ObservableServiceController).FullName, "_serviceController"))
            .SetValue(controller, service);

    /// <summary>Test wrapper that exposes the protected disposal overload.</summary>
    private sealed class TestObservableServiceController : ObservableServiceController
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

    /// <summary>Deterministic Windows service runtime.</summary>
    private sealed class FakeServiceControllerRuntime : IServiceControllerRuntime
    {
        /// <summary>Stores the current status.</summary>
        private ServiceControllerStatus _status = ServiceControllerStatus.Stopped;

        /// <inheritdoc/>
        public event EventHandler? Disposed;

        /// <inheritdoc/>
        public bool CanStop { get; set; }

        /// <inheritdoc/>
        public string DisplayName => "TwinCAT System";

        /// <inheritdoc/>
        public string ServiceName => TwinCatServiceName;

        /// <inheritdoc/>
        public ServiceControllerStatus Status
        {
            get
            {
                if (StatusError is not null)
                {
                    throw StatusError;
                }

                return _status;
            }

            set => _status = value;
        }

        /// <summary>Gets or sets the status assigned by refresh.</summary>
        public ServiceControllerStatus? RefreshedStatus { get; set; }

        /// <summary>Gets or sets the status query error.</summary>
        public Exception? StatusError { get; set; }

        /// <summary>Gets the refresh count.</summary>
        public int RefreshCount { get; private set; }

        /// <summary>Gets the start count.</summary>
        public int StartCount { get; private set; }

        /// <summary>Gets the stop count.</summary>
        public int StopCount { get; private set; }

        /// <summary>Gets statuses passed to wait operations.</summary>
        public List<ServiceControllerStatus> WaitedStatuses { get; } = [];

        /// <inheritdoc/>
        public void Dispose() => Disposed?.Invoke(this, EventArgs.Empty);

        /// <inheritdoc/>
        public void Refresh()
        {
            RefreshCount++;
            if (!RefreshedStatus.HasValue)
            {
                return;
            }

            _status = RefreshedStatus.Value;
        }

        /// <summary>Raises the disposed event.</summary>
        public void RaiseDisposed() => Dispose();

        /// <inheritdoc/>
        public void Start()
        {
            StartCount++;
            _status = ServiceControllerStatus.Running;
        }

        /// <inheritdoc/>
        public void Stop()
        {
            StopCount++;
            _status = ServiceControllerStatus.Stopped;
        }

        /// <inheritdoc/>
        public void WaitForStatus(ServiceControllerStatus status) => WaitedStatuses.Add(status);
    }

    /// <summary>Deterministic service source.</summary>
    /// <remarks>Initializes a new instance of the <see cref="FakeServiceControllerSource"/> class.</remarks>
    /// <param name="service">The service to enumerate.</param>
    private sealed class FakeServiceControllerSource(IServiceControllerRuntime service) : IServiceControllerSource
    {
        /// <inheritdoc/>
        public IEnumerable<IServiceControllerRuntime> GetServices() => [service];
    }

    /// <summary>Service source that models an unsupported service manager.</summary>
    private sealed class UnsupportedServiceControllerSource : IServiceControllerSource
    {
        /// <inheritdoc/>
        public IEnumerable<IServiceControllerRuntime> GetServices() =>
            throw new PlatformNotSupportedException();
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

    /// <summary>Records observable notifications.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    private sealed class RecordingObserver<T> : IObserver<T>
    {
        /// <summary>Gets observed values.</summary>
        public List<T> Values { get; } = [];

        /// <summary>Gets observed errors.</summary>
        public List<Exception> Errors { get; } = [];

        /// <summary>Gets a value indicating whether completion was observed.</summary>
        public bool Completed { get; private set; }

        /// <inheritdoc/>
        public void OnCompleted() => Completed = true;

        /// <inheritdoc/>
        public void OnError(Exception error) => Errors.Add(error);

        /// <inheritdoc/>
        public void OnNext(T value) => Values.Add(value);
    }
}
