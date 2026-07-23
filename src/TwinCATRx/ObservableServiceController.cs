// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.ComponentModel;
using System.ServiceProcess;
#if !NETFRAMEWORK
using System.Runtime.Versioning;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.TwinCATRx.Reactive;
#else
namespace IoT.DriverCore.TwinCATRx;
#endif

/// <summary>Observable Service Controller.</summary>
#if !NETFRAMEWORK
[SupportedOSPlatform("windows")]
#endif
public class ObservableServiceController : IObservableServiceController
{
    /// <summary>Stores the default service refresh interval in seconds.</summary>
    private const double DefaultRefreshIntervalSeconds = 0.5;

    /// <summary>Stores disposable resources owned by this instance.</summary>
    private readonly CompositeDisposable _cleanup = [];

    /// <summary>Publishes service status changes.</summary>
    private readonly Signal<ServiceControllerStatus> _statusChanged = new();

    /// <summary>Stores the wrapped service controller.</summary>
    private IServiceControllerRuntime? _serviceController;

    /// <summary>Initializes a new instance of the <see cref="ObservableServiceController"/> class.</summary>
    /// <param name="service">The service.</param>
    public ObservableServiceController(ServiceController service)
        : this(service, TimeSpan.FromSeconds(DefaultRefreshIntervalSeconds))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ObservableServiceController"/> class.</summary>
    /// <param name="service">The service.</param>
    /// <param name="interval">The interval.</param>
    public ObservableServiceController(ServiceController service, TimeSpan interval)
        : this(new ServiceControllerRuntime(service), Observable.Interval(interval))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ObservableServiceController"/> class.</summary>
    /// <param name="service">The replaceable service runtime.</param>
    /// <param name="refreshTicks">The refresh trigger sequence.</param>
    internal ObservableServiceController(
        IServiceControllerRuntime service,
        IObservable<long> refreshTicks) =>
        CreateObject(service, refreshTicks);

    /// <summary>Gets a value indicating whether the is disposed.</summary>
    public bool IsDisposed => _cleanup.IsDisposed;

    /// <summary>Gets a value indicating whether this instance can stop.</summary>
    /// <value><c>true</c> if this instance can stop; otherwise, <c>false</c>.</value>
    public bool CanStop => _serviceController?.CanStop == true;

    /// <summary>Gets the display name.</summary>
    /// <value>The display name.</value>
    public string DisplayName => _serviceController is null ? string.Empty : _serviceController.DisplayName;

    /// <summary>Gets the name of the service.</summary>
    /// <value>The name of the service.</value>
    public string ServiceName => _serviceController is null ? string.Empty : _serviceController.ServiceName;

    /// <summary>Gets the status.</summary>
    /// <value>The status.</value>
    public ServiceControllerStatus Status =>
        _serviceController is null ? ServiceControllerStatus.Stopped : _serviceController.Status;

    /// <summary>Gets the status.</summary>
    /// <value>The status.</value>
    public IObservable<ServiceControllerStatus> StatusObserver => _statusChanged;

    /// <summary>Gets the services.</summary>
    /// <returns>A Value.</returns>
    public static IObservable<ObservableServiceController> GetServices() =>
        GetServices(
            ServiceControllerSource.Instance,
            TimeSpan.FromSeconds(DefaultRefreshIntervalSeconds));

    /// <summary>Releases managed and unmanaged resources.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Restarts this instance.</summary>
    public void Restart()
    {
        if (IsDisposed)
        {
            return;
        }

        if (_serviceController?.CanStop == true &&
            (_serviceController.Status == ServiceControllerStatus.Running ||
             _serviceController.Status == ServiceControllerStatus.Paused))
        {
            Stop();
            _serviceController.WaitForStatus(ServiceControllerStatus.Stopped);
        }

        if (_serviceController?.Status != ServiceControllerStatus.Stopped)
        {
            return;
        }

        Start();
        _serviceController.WaitForStatus(ServiceControllerStatus.Running);
    }

    /// <summary>Starts this instance.</summary>
    public void Start()
    {
        if (IsDisposed)
        {
            return;
        }

        _serviceController?.Start();
        _serviceController?.WaitForStatus(ServiceControllerStatus.Running);
    }

    /// <summary>Stops this instance.</summary>
    public void Stop()
    {
        if (IsDisposed)
        {
            return;
        }

        _serviceController?.Stop();
        _serviceController?.WaitForStatus(ServiceControllerStatus.Stopped);
    }

    /// <summary>Gets observable service wrappers from a replaceable source.</summary>
    /// <param name="source">The service source.</param>
    /// <param name="refreshInterval">The refresh interval.</param>
    /// <returns>The service wrappers.</returns>
    internal static IObservable<ObservableServiceController> GetServices(
        IServiceControllerSource source,
        TimeSpan refreshInterval) =>
        Observable.Create<ObservableServiceController>(o =>
            {
                var d = new CompositeDisposable();
                try
                {
                    foreach (var serviceRuntime in source.GetServices())
                    {
                        var service = new ObservableServiceController(
                            serviceRuntime,
                            Observable.Interval(refreshInterval));
                        _ = service.DisposeWith(d);
                        o.OnNext(service);
                    }
                }
                catch (PlatformNotSupportedException)
                {
                    // ServiceController may not be supported on certain Windows environments (e.g., Nano/containers).
                    // Treat as no services available and complete the sequence gracefully.
                    o.OnCompleted();
                }

                return d;
            });

    /// <summary>Releases unmanaged and - optionally - managed resources.</summary>
    /// <param name="disposing">
    /// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only
    /// unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (_cleanup.IsDisposed || !disposing)
        {
            return;
        }

        _serviceController?.Dispose();
        _cleanup.Dispose();
        _statusChanged.Dispose();
    }

    /// <summary>Creates the object.</summary>
    /// <param name="service">The service.</param>
    /// <param name="refreshTicks">The refresh trigger sequence.</param>
    private void CreateObject(IServiceControllerRuntime service, IObservable<long> refreshTicks)
    {
        _serviceController = service;
        _ = _serviceController.DisposeWith(_cleanup);
        var serviceControllerIsDisposed = false;
        _serviceController.Disposed += (e, o) => serviceControllerIsDisposed = true;

        _ = ObservableBridgeExtensions.SubscribeTo(refreshTicks.Retry(int.MaxValue), _ =>
        {
            try
            {
                if (!serviceControllerIsDisposed)
                {
                    var currentStatus = _serviceController?.Status;
                    _serviceController?.Refresh();

                    if (_serviceController is not null &&
                        currentStatus.HasValue &&
                        currentStatus.Value != _serviceController.Status)
                    {
                        _statusChanged.OnNext(_serviceController.Status);
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                _statusChanged.OnError(ex);
            }
            catch (Win32Exception ex)
            {
                _statusChanged.OnError(ex);
            }
        }).DisposeWith(_cleanup);
    }
}
