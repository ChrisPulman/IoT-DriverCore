// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.ServiceProcess;
#if !NETFRAMEWORK
using System.Runtime.Versioning;
#endif
#if REACTIVE_SHIM
using IoT.Driver.TwinCATRx.Core.Reactive;
using CoreTwinCatRxExtensions = IoT.Driver.TwinCATRx.Core.Reactive.TwinCatRxExtensions;
using RxNotification = IoT.Driver.TwinCATRx.Core.Reactive.INotification;
#else
using IoT.Driver.TwinCATRx.Core;
using CoreTwinCatRxExtensions = IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions;
using RxNotification = IoT.Driver.TwinCATRx.Core.INotification;
#endif
using TwinCAT.Ads;
#if REACTIVE_SHIM
using NotificationSequencer = System.Reactive.Concurrency.TaskPoolScheduler;
#else
using NotificationSequencer = ReactiveUI.Primitives.Concurrency.TaskPoolSequencer;
#endif

#if REACTIVE_SHIM
namespace IoT.Driver.TwinCATRx.Reactive;
#else
namespace IoT.Driver.TwinCATRx;
#endif

/// <summary>Observable TwinCAT ADS Client.</summary>
public partial class RxTcAdsClient
{
    /// <summary>Gets or sets the notification delivery lifetime, which is disposed before the native connection.</summary>
    private CompositeDisposable? _adsNotificationLifetime { get => ConnectionState.Notifications; set => ConnectionState.SetNotifications(value); }

    /// <summary>Gets the active connection lifetime.</summary>
    private CompositeDisposable ConnectionLifetime =>
        (_executingConnection.Value ?? _connection)?.Resources ??
        throw new InvalidOperationException("The TwinCAT connection lifetime is not initialized.");

    /// <summary>Reads one native ADS value.</summary>
    /// <param name="client">The native ADS client.</param>
    /// <param name="handle">The ADS variable handle.</param>
    /// <param name="type">The native value type.</param>
    /// <param name="length">The optional array or string length.</param>
    /// <returns>The native value, or null when no handle is available.</returns>
    private static object? ReadNativeValue(IAdsClientRuntime client, uint? handle, Type type, int length)
    {
        if (handle is null)
        {
            return null;
        }

        return (type.IsArray || type == typeof(string)) && length > 0
            ? client.ReadAny(handle.Value, type, [length])
            : client.ReadAny(handle.Value, type);
    }

    /// <summary>Initializes the PLC connection and reactive read/write loops.</summary>
    /// <param name="subscription">The owning connect subscription.</param>
    /// <returns>The PLC initialization observable sequence.</returns>
    [RequiresUnreferencedCode("Invokes dynamic code generation and reflection to materialize PLC types.")]
    [RequiresDynamicCode("Invokes dynamic code generation and reflection to materialize PLC types.")]
    private IObservable<Unit> InitPLC(IDisposable subscription) =>
        CoreTwinCatRxExtensions.OnErrorRetry<Unit, Exception>(
            Observable.Create<Unit>(observer => InitializeConnection(observer, subscription)),
            PublishError,
            TimeSpan.FromSeconds(ConnectionRetryDelaySeconds)).Publish().RefCount();

    /// <summary>Creates and composes the current ADS connection monitors.</summary>
    /// <param name="observer">The initialization observer.</param>
    /// <param name="subscription">The owning connect subscription.</param>
    /// <returns>The active connection lifetime.</returns>
    [RequiresUnreferencedCode("Invokes dynamic code generation and reflection to materialize PLC types.")]
    [RequiresDynamicCode("Invokes dynamic code generation and reflection to materialize PLC types.")]
    private IDisposable InitializeConnection(IObserver<Unit> observer, IDisposable subscription)
    {
        var connection = new ConnectionScope { Settings = _settings };
        ConnectionScope? previous;
        ConnectionScope? placeholder;
        bool wasPaused;
        long pauseVersion;
        lock (_lifecycleGate)
        {
            if (IsDisposed || !ReferenceEquals(_plcCleanup, subscription))
            {
                connection.Closed = true;
                return connection;
            }

            placeholder = !ReferenceEquals(_lastConnection, _connection) && _lastConnection.ActiveOperations == 0
                ? _lastConnection
                : null;
            previous = CloseConnectionLocked(_connection);
            _connection = connection;
            _lastConnection = connection;
            _cleanup = connection.Resources;
            wasPaused = IsPaused;
            IsPaused = false;
            pauseVersion = ++_pauseVersion;
        }

        previous?.Dispose();
        placeholder?.Dispose();

        if (wasPaused)
        {
            RunConnection(connection, () => Publish(_isPausedSubject, false, () => _pauseVersion == pauseVersion));
        }

        RunConnection(connection, () =>
        {
            ResetConnectionState();
            _adsNotificationLifetime = [];
            _ = _adsNotificationLifetime.DisposeWith(ConnectionLifetime);
            var client = _platform.CreateAdsClient();
            _ = client.DisposeWith(ConnectionLifetime);
            var codeGenerator = _platform.CreateCodeGenerator();
            _codeGenerator = codeGenerator;
            _ = codeGenerator.DisposeWith(ConnectionLifetime);
            ConnectAdsClient(client, codeGenerator, observer);
            MonitorServiceStatus(observer);
            MonitorAdsState(client, observer);
            MonitorWrites(client);
            MonitorReads(client);
            MonitorInitialization(client, observer);
            ScheduleNotifications(client);
        });
        return new ConnectionSubscription(this, connection);
    }

    /// <summary>Resets all state owned by one ADS connection.</summary>
    private void ResetConnectionState()
    {
        _initialized = false;
        _code.Clear();
        ReadWriteHandleInfo.Clear();
        _typeInfo.Clear();
        WriteHandleInfo.Clear();
        _readWriteVariablesByHandle.Clear();
        _writeVariablesByHandle.Clear();
    }

    /// <summary>Connects the native ADS client and loads its symbols.</summary>
    /// <param name="client">The native ADS client.</param>
    /// <param name="codeGenerator">The PLC symbol code generator.</param>
    /// <param name="observer">The initialization observer.</param>
    [RequiresUnreferencedCode("Loads ADS symbols for dynamic PLC type generation.")]
    private void ConnectAdsClient(
        IAdsClientRuntime client,
        ICodeGenerator codeGenerator,
        IObserver<Unit> observer)
    {
        try
        {
            var settings = Settings ?? throw new InvalidOperationException("TwinCAT settings are not configured.");
            if (string.IsNullOrWhiteSpace(settings.AdsAddress))
            {
                client.Connect(settings.Port);
            }
            else
            {
                client.Connect(settings.AdsAddress, settings.Port);
            }

            _platform.LoadSymbols(codeGenerator, settings.AdsAddress, settings.Port);
        }
        catch (Exception error)
        {
            PublishConnectionError(error, observer);
        }
    }

    /// <summary>Publishes a connection error to both reactive error channels.</summary>
    /// <param name="error">The connection error.</param>
    /// <param name="observer">The initialization observer.</param>
    private void PublishConnectionError(Exception error, IObserver<Unit> observer)
    {
        lock (_lifecycleGate)
        {
            if (!IsCurrentConnection(_executingConnection.Value))
            {
                return;
            }

            Connected = false;
        }

        Publish(_errorReceived, error);
        lock (_lifecycleGate)
        {
            if (!IsCurrentConnection(_executingConnection.Value))
            {
                return;
            }
        }

        observer.OnError(error);
    }

    /// <summary>Monitors the TwinCAT Windows service where available.</summary>
    /// <param name="observer">The initialization observer.</param>
    private void MonitorServiceStatus(IObserver<Unit> observer)
    {
#if NETFRAMEWORK
        if (_platform.IsWindowsServiceMonitoringSupported)
#else
        if (OperatingSystem.IsWindows() && _platform.IsWindowsServiceMonitoringSupported)
#endif
        {
            MonitorWindowsService(observer);
            return;
        }

        QueueConnection(() => _serviceStatus.OnNext(ServiceStatus.Running));
    }

    /// <summary>Monitors the TwinCAT system service on Windows.</summary>
    /// <param name="observer">The initialization observer.</param>
#if !NETFRAMEWORK
    [SupportedOSPlatform("windows")]
#endif
    private void MonitorWindowsService(IObserver<Unit> observer)
    {
        var statuses = new Dictionary<string, ServiceControllerStatus>(StringComparer.OrdinalIgnoreCase);
        var services = _platform.GetServices()
            .Where(static service => string.Equals(service.ServiceName, "TcSysSrv", StringComparison.OrdinalIgnoreCase))
            .Retry(int.MaxValue);
        _ = SubscribeConnection(
            services,
            service => ObserveService(service, statuses, observer)).DisposeWith(ConnectionLifetime);
        _ = SubscribeConnection(
            _platform.Interval(TimeSpan.FromSeconds(1)).Retry(int.MaxValue),
            _ => PublishServiceStatus(statuses)).DisposeWith(ConnectionLifetime);
    }

    /// <summary>Observes one matching system service.</summary>
    /// <param name="service">The service to observe.</param>
    /// <param name="statuses">The current service statuses.</param>
    /// <param name="observer">The initialization observer.</param>
#if !NETFRAMEWORK
    [SupportedOSPlatform("windows")]
#endif
    private void ObserveService(
        IObservableServiceController service,
        Dictionary<string, ServiceControllerStatus> statuses,
        IObserver<Unit> observer)
    {
        statuses[service.ServiceName] = service.Status;
        EnsureServiceRunning(service, service.Status, observer);
        _ = SubscribeConnection(service.StatusObserver.Retry(int.MaxValue), status =>
        {
            statuses[service.ServiceName] = status;
            EnsureServiceRunning(service, status, observer);
        }).DisposeWith(ConnectionLifetime);
    }

    /// <summary>Starts a stopped TwinCAT service and publishes its fault.</summary>
    /// <param name="service">The service to start.</param>
    /// <param name="status">The current service status.</param>
    /// <param name="observer">The initialization observer.</param>
#if !NETFRAMEWORK
    [SupportedOSPlatform("windows")]
#endif
    private void EnsureServiceRunning(
        IObservableServiceController service,
        ServiceControllerStatus status,
        IObserver<Unit> observer)
    {
        if (status == ServiceControllerStatus.Running)
        {
            return;
        }

        service.Start();
        PublishConnectionError(new InvalidOperationException("Service Fault"), observer);
    }

    /// <summary>Publishes the current TwinCAT service state.</summary>
    /// <param name="statuses">The current service statuses.</param>
#if !NETFRAMEWORK
    [SupportedOSPlatform("windows")]
#endif
    private void PublishServiceStatus(Dictionary<string, ServiceControllerStatus> statuses)
    {
        var running = !statuses.TryGetValue("TcSysSrv", out var status) ||
            status == ServiceControllerStatus.Running;
        QueueConnection(() => _serviceStatus.OnNext(running ? ServiceStatus.Running : ServiceStatus.Faulted));
    }

    /// <summary>Polls and publishes the native ADS client state.</summary>
    /// <param name="client">The native ADS client.</param>
    /// <param name="observer">The initialization observer.</param>
    private void MonitorAdsState(IAdsClientRuntime client, IObserver<Unit> observer)
    {
        _ = SubscribeConnection(
            _platform.Interval(TimeSpan.FromSeconds(1)).Retry(int.MaxValue),
            _ => ReadAdsState(client, observer)).DisposeWith(ConnectionLifetime);
    }

    /// <summary>Reads and publishes the native ADS client state.</summary>
    /// <param name="client">The native ADS client.</param>
    /// <param name="observer">The initialization observer.</param>
    private void ReadAdsState(IAdsClientRuntime client, IObserver<Unit> observer)
    {
        try
        {
            var state = client.IsConnected ? client.ReadState().AdsState : AdsState.Invalid;
            QueueConnection(() => _clientState.OnNext(state));
        }
        catch (Exception innerError)
        {
            QueueConnection(() => _clientState.OnNext(AdsState.Invalid));
            PublishConnectionError(new InvalidOperationException("Ads Fault", innerError), observer);
        }
    }

    /// <summary>Creates variables once ADS and the TwinCAT service are ready.</summary>
    /// <param name="client">The native ADS client.</param>
    /// <param name="observer">The initialization observer.</param>
    [RequiresUnreferencedCode("Invokes dynamic code generation and reflection to materialize PLC types.")]
    [RequiresDynamicCode("Invokes dynamic code generation and reflection to materialize PLC types.")]
    private void MonitorInitialization(IAdsClientRuntime client, IObserver<Unit> observer)
    {
        var statuses = _clientState.DistinctUntilChanged().CombineLatest(
            _serviceStatus.DistinctUntilChanged(),
            static (state, service) => (State: state, Service: service));
        _ = SubscribeConnection(statuses.Retry(int.MaxValue), status =>
        {
            if (!_initialized && status.Service == ServiceStatus.Running && status.State == AdsState.Run)
            {
                CompleteInitialization(client, observer);
            }
            else if (status.State != AdsState.Invalid && status.State != AdsState.Run)
            {
                TryStartPlcProgram(client, observer);
            }
        }).DisposeWith(ConnectionLifetime);
    }

    /// <summary>Creates notification/write handles and completes initialization.</summary>
    /// <param name="client">The native ADS client.</param>
    /// <param name="observer">The initialization observer.</param>
    [RequiresUnreferencedCode("Invokes dynamic code generation and reflection to materialize PLC types.")]
    [RequiresDynamicCode("Invokes dynamic code generation and reflection to materialize PLC types.")]
    private void CompleteInitialization(IAdsClientRuntime client, IObserver<Unit> observer)
    {
        try
        {
            var settings = Settings ?? throw new InvalidOperationException("TwinCAT settings are not configured.");
            var error = CreateNotificationVariables(settings.Notifications, client) ??
                CreateWriteVariables(settings.WriteVariables, client);
            if (error is not null)
            {
                throw error;
            }

            SubscribeAdsNotifications(client);

            var code = _code.ToArray();
            var connection = _executingConnection.Value!;
            _ = Task.Run(() => RunConnection(connection, () => Publish(_codeSubject, code)));
            _codeGenerator?.Dispose();
            lock (_lifecycleGate)
            {
                if (!IsCurrentConnection(connection))
                {
                    return;
                }

                _initialized = true;
                Connected = true;
            }

            Publish(_initCompleteSubject, Unit.Default);
        }
        catch (Exception error)
        {
            PublishConnectionError(error, observer);
        }
    }

    /// <summary>Requests the PLC run state when it is connected but stopped.</summary>
    /// <param name="client">The native ADS client.</param>
    /// <param name="observer">The initialization observer.</param>
    private void TryStartPlcProgram(IAdsClientRuntime client, IObserver<Unit> observer)
    {
        try
        {
            client.WriteControl(new(AdsState.Run, client.ReadState().DeviceState));
        }
        catch (Exception error)
        {
            PublishConnectionError(error, observer);
        }
    }

    /// <summary>Composes the queued ADS write loop.</summary>
    /// <param name="client">The native ADS client.</param>
    private void MonitorWrites(IAdsClientRuntime client)
    {
        _ = SubscribeConnection(_writePLC, request =>
        {
            if (!_initialized || !client.IsConnected || request.Handle is null)
            {
                return;
            }

            try
            {
                client.WriteAny(request.Handle.Value, request.Value);
                var result = string.IsNullOrWhiteSpace(request.Id) ? "Success" : $"Success,{request.Id}";
                Publish(_onWriteSubject, result);
            }
            catch (Exception error)
            {
                Publish(_onWriteSubject, error.ToString());
                Publish(_errorReceived, error);
            }
        }).DisposeWith(ConnectionLifetime);
    }

    /// <summary>Composes the queued ADS read loop.</summary>
    /// <param name="client">The native ADS client.</param>
    private void MonitorReads(IAdsClientRuntime client)
    {
        _ = SubscribeConnection(_readPLC.Retry(int.MaxValue), request =>
        {
            try
            {
                var value = ReadNativeValue(client, request.Handle, request.Type, request.Length);
                PublishNativeValue(request.Handle, value, request.Id);
            }
            catch (Exception error)
            {
                Publish(_errorReceived, error);
            }
        }).DisposeWith(ConnectionLifetime);
    }

    /// <summary>Publishes a correlated native ADS value.</summary>
    /// <param name="handle">The ADS variable handle.</param>
    /// <param name="value">The native ADS value.</param>
    /// <param name="id">The optional correlation identifier.</param>
    private void PublishNativeValue(uint? handle, object? value, string? id)
    {
        string? variable;
        lock (_lifecycleGate)
        {
            if (!IsCurrentConnection(_executingConnection.Value))
            {
                return;
            }

            if (value is null || !handle.HasValue)
            {
                return;
            }

            if (!_readWriteVariablesByHandle.TryGetValue(handle.Value, out variable))
            {
                _ = _writeVariablesByHandle.TryGetValue(handle.Value, out variable);
            }

            if (string.IsNullOrWhiteSpace(variable))
            {
                return;
            }
        }

        Publish(_dataReceived, (Variable: variable, Data: value, id));
    }

    /// <summary>Schedules all configured notification reads.</summary>
    /// <param name="client">The native ADS client.</param>
    private void ScheduleNotifications(IAdsClientRuntime client)
    {
        foreach (var notification in Settings?.Notifications ?? [])
        {
            if (notification is IAdsNotification)
            {
                continue;
            }

            _ = SubscribeConnection(
                _platform.Interval(TimeSpan.FromMilliseconds(notification.UpdateRate)).Retry(int.MaxValue),
                _ => ReadNotification(client, notification)).DisposeWith(ConnectionLifetime);
        }
    }

    /// <summary>Transfers ADS event values onto the connection's data channel.</summary>
    /// <param name="client">The ADS runtime.</param>
    private void SubscribeAdsNotifications(IAdsClientRuntime client)
    {
        var lifetime = ConnectionLifetime;
        foreach (var notification in Settings?.Notifications ?? [])
        {
            if (notification is not IAdsNotification adsNotification ||
                notification.Variable is not { } variable)
            {
                continue;
            }

            _ = SubscribeConnection(
                client.ObserveValue(
                    variable,
                    adsNotification.AdsTransMode,
                    adsNotification.CycleTime,
                    adsNotification.MaxDelay).ObserveOn(NotificationSequencer.Default),
                value =>
                {
                    if (lifetime.IsDisposed || !ReferenceEquals(_cleanup, lifetime))
                    {
                        return;
                    }

                    Publish(_dataReceived, (variable, value, null));
                },
                PublishError,
                static () => { }).DisposeWith(
                    _adsNotificationLifetime ?? throw new InvalidOperationException("ADS notification lifetime is not initialized."));
        }
    }

    /// <summary>Reads one configured notification.</summary>
    /// <param name="client">The native ADS client.</param>
    /// <param name="notification">The configured notification.</param>
    private void ReadNotification(IAdsClientRuntime client, RxNotification notification)
    {
        if (notification.Variable is null ||
            !client.IsConnected ||
            !_typeInfo.TryGetValue(notification.Variable, out var type) ||
            !ReadWriteHandleInfo.TryGetValue(notification.Variable, out var handle))
        {
            return;
        }

        if (!type.IsArray && type != typeof(string))
        {
            ReadHandle(handle, type, null);
            return;
        }

        if (notification.ArraySize > 0)
        {
            ReadArrayHandle(handle, type, notification.ArraySize, null);
            return;
        }

        var kind = type == typeof(string) ? "String" : "Array";
        Publish(
            _errorReceived,
            new InvalidOperationException($"Please set Notification ArraySize to the {kind} length."));
    }
}
