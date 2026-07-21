// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
#if WINDOWS
using System.Runtime.InteropServices;
using System.ServiceProcess;
#endif
#if REACTIVE_SHIM
using CP.TwinCatRx.Core.Reactive;
using CoreTwinCatRxExtensions = CP.TwinCatRx.Core.Reactive.TwinCatRxExtensions;
using RxNotification = CP.TwinCatRx.Core.Reactive.INotification;
#else
using CP.TwinCatRx.Core;
using CoreTwinCatRxExtensions = CP.TwinCatRx.Core.TwinCatRxExtensions;
using RxNotification = CP.TwinCatRx.Core.INotification;
#endif
using TwinCAT.Ads;

#if REACTIVE_SHIM
namespace CP.TwinCatRx.Reactive;
#else
namespace CP.TwinCatRx;
#endif

/// <summary>Observable TwinCAT ADS Client.</summary>
public partial class RxTcAdsClient
{
    /// <summary>Gets the active connection lifetime.</summary>
    private CompositeDisposable ConnectionLifetime =>
        _cleanup ?? throw new InvalidOperationException("The TwinCAT connection lifetime is not initialized.");

    /// <summary>Reads one native ADS value.</summary>
    /// <param name="client">The native ADS client.</param>
    /// <param name="handle">The ADS variable handle.</param>
    /// <param name="type">The native value type.</param>
    /// <param name="length">The optional array or string length.</param>
    /// <returns>The native value, or null when no handle is available.</returns>
    private static object? ReadNativeValue(AdsClient client, uint? handle, Type type, int length)
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
    /// <returns>The PLC initialization observable sequence.</returns>
    [RequiresUnreferencedCode("Invokes dynamic code generation and reflection to materialize PLC types.")]
    [RequiresDynamicCode("Invokes dynamic code generation and reflection to materialize PLC types.")]
    private IObservable<Unit> InitPLC() =>
        CoreTwinCatRxExtensions.OnErrorRetry<Unit, Exception>(
            Observable.Create<Unit>(InitializeConnection),
            _errorReceived.OnNext,
            TimeSpan.FromSeconds(ConnectionRetryDelaySeconds)).Publish().RefCount();

    /// <summary>Creates and composes the current ADS connection monitors.</summary>
    /// <param name="observer">The initialization observer.</param>
    /// <returns>The active connection lifetime.</returns>
    [RequiresUnreferencedCode("Invokes dynamic code generation and reflection to materialize PLC types.")]
    [RequiresDynamicCode("Invokes dynamic code generation and reflection to materialize PLC types.")]
    private IDisposable InitializeConnection(IObserver<Unit> observer)
    {
        ResetConnectionState();
        var client = new AdsClient();
        _ = client.DisposeWith(ConnectionLifetime);
        var codeGenerator = new CodeGenerator();
        _codeGenerator = codeGenerator;
        _ = codeGenerator.DisposeWith(ConnectionLifetime);
        ConnectAdsClient(client, codeGenerator, observer);
        MonitorServiceStatus(observer);
        MonitorAdsState(client, observer);
        MonitorInitialization(client, observer);
        MonitorWrites(client);
        MonitorReads(client);
        ScheduleNotifications(client);
        return ConnectionLifetime;
    }

    /// <summary>Resets all state owned by one ADS connection.</summary>
    private void ResetConnectionState()
    {
        _cleanup = [];
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
    private void ConnectAdsClient(AdsClient client, CodeGenerator codeGenerator, IObserver<Unit> observer)
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

            _ = codeGenerator.LoadSymbols(settings.AdsAddress, settings.Port);
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
        Connected = false;
        _errorReceived.OnNext(error);
        observer.OnError(error);
    }

    /// <summary>Monitors the TwinCAT Windows service where available.</summary>
    /// <param name="observer">The initialization observer.</param>
    private void MonitorServiceStatus(IObserver<Unit> observer)
    {
#if WINDOWS
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            MonitorWindowsService(observer);
            return;
        }
#else
        _ = observer;
#endif
        _serviceStatus.OnNext(ServiceStatus.Running);
    }

#if WINDOWS
    /// <summary>Monitors the TwinCAT system service on Windows.</summary>
    /// <param name="observer">The initialization observer.</param>
    private void MonitorWindowsService(IObserver<Unit> observer)
    {
        var statuses = new Dictionary<string, ServiceControllerStatus>(StringComparer.OrdinalIgnoreCase);
        var services = ObservableServiceController.GetServices()
            .Where(service => string.Equals(service.ServiceName, "TcSysSrv", StringComparison.OrdinalIgnoreCase))
            .Retry(int.MaxValue);
        _ = ObservableBridgeExtensions.SubscribeTo(
            services,
            service => ObserveService(service, statuses, observer)).DisposeWith(ConnectionLifetime);
        _ = ObservableBridgeExtensions.SubscribeTo(
            Observable.Interval(TimeSpan.FromSeconds(1)).Retry(int.MaxValue),
            _ => PublishServiceStatus(statuses)).DisposeWith(ConnectionLifetime);
    }

    /// <summary>Observes one matching system service.</summary>
    /// <param name="service">The service to observe.</param>
    /// <param name="statuses">The current service statuses.</param>
    /// <param name="observer">The initialization observer.</param>
    private void ObserveService(
        ObservableServiceController service,
        Dictionary<string, ServiceControllerStatus> statuses,
        IObserver<Unit> observer)
    {
        statuses[service.ServiceName] = service.Status;
        EnsureServiceRunning(service, service.Status, observer);
        _ = ObservableBridgeExtensions.SubscribeTo(service.StatusObserver.Retry(int.MaxValue), status =>
        {
            statuses[service.ServiceName] = status;
            EnsureServiceRunning(service, status, observer);
        }).DisposeWith(ConnectionLifetime);
    }

    /// <summary>Starts a stopped TwinCAT service and publishes its fault.</summary>
    /// <param name="service">The service to start.</param>
    /// <param name="status">The current service status.</param>
    /// <param name="observer">The initialization observer.</param>
    private void EnsureServiceRunning(
        ObservableServiceController service,
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
    private void PublishServiceStatus(Dictionary<string, ServiceControllerStatus> statuses)
    {
        var running = !statuses.TryGetValue("TcSysSrv", out var status) ||
            status == ServiceControllerStatus.Running;
        _serviceStatus.OnNext(running ? ServiceStatus.Running : ServiceStatus.Faulted);
    }
#endif

    /// <summary>Polls and publishes the native ADS client state.</summary>
    /// <param name="client">The native ADS client.</param>
    /// <param name="observer">The initialization observer.</param>
    private void MonitorAdsState(AdsClient client, IObserver<Unit> observer)
    {
        _ = ObservableBridgeExtensions.SubscribeTo(
            Observable.Interval(TimeSpan.FromSeconds(1)).Retry(int.MaxValue),
            _ => ReadAdsState(client, observer)).DisposeWith(ConnectionLifetime);
    }

    /// <summary>Reads and publishes the native ADS client state.</summary>
    /// <param name="client">The native ADS client.</param>
    /// <param name="observer">The initialization observer.</param>
    private void ReadAdsState(AdsClient client, IObserver<Unit> observer)
    {
        try
        {
            _clientState.OnNext(client.IsConnected ? client.ReadState().AdsState : AdsState.Invalid);
        }
        catch (Exception innerError)
        {
            _clientState.OnNext(AdsState.Invalid);
            PublishConnectionError(new InvalidOperationException("Ads Fault", innerError), observer);
        }
    }

    /// <summary>Creates variables once ADS and the TwinCAT service are ready.</summary>
    /// <param name="client">The native ADS client.</param>
    /// <param name="observer">The initialization observer.</param>
    [RequiresUnreferencedCode("Invokes dynamic code generation and reflection to materialize PLC types.")]
    [RequiresDynamicCode("Invokes dynamic code generation and reflection to materialize PLC types.")]
    private void MonitorInitialization(AdsClient client, IObserver<Unit> observer)
    {
        var statuses = _clientState.DistinctUntilChanged().CombineLatest(
            _serviceStatus.DistinctUntilChanged(),
            (state, service) => (State: state, Service: service));
        _ = ObservableBridgeExtensions.SubscribeTo(statuses.Retry(int.MaxValue), status =>
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
    private void CompleteInitialization(AdsClient client, IObserver<Unit> observer)
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

            _ = Task.Run(() => _codeSubject.OnNext([.. _code]));
            _codeGenerator?.Dispose();
            _initialized = true;
            Connected = true;
            _initCompleteSubject.OnNext(Unit.Default);
        }
        catch (Exception error)
        {
            PublishConnectionError(error, observer);
        }
    }

    /// <summary>Requests the PLC run state when it is connected but stopped.</summary>
    /// <param name="client">The native ADS client.</param>
    /// <param name="observer">The initialization observer.</param>
    private void TryStartPlcProgram(AdsClient client, IObserver<Unit> observer)
    {
        try
        {
            client.WriteControl(new StateInfo(AdsState.Run, client.ReadState().DeviceState));
        }
        catch (Exception error)
        {
            PublishConnectionError(error, observer);
        }
    }

    /// <summary>Composes the queued ADS write loop.</summary>
    /// <param name="client">The native ADS client.</param>
    private void MonitorWrites(AdsClient client)
    {
        _ = ObservableBridgeExtensions.SubscribeTo(_writePLC, request =>
        {
            if (!_initialized || !client.IsConnected || request.Handle is null)
            {
                return;
            }

            try
            {
                client.WriteAny(request.Handle.Value, request.Value);
                var result = string.IsNullOrWhiteSpace(request.Id) ? "Success" : $"Success,{request.Id}";
                _onWriteSubject.OnNext(result);
            }
            catch (Exception error)
            {
                _onWriteSubject.OnNext(error.ToString());
                _errorReceived.OnNext(error);
            }
        }).DisposeWith(ConnectionLifetime);
    }

    /// <summary>Composes the queued ADS read loop.</summary>
    /// <param name="client">The native ADS client.</param>
    private void MonitorReads(AdsClient client)
    {
        _ = ObservableBridgeExtensions.SubscribeTo(_readPLC.Retry(int.MaxValue), request =>
        {
            try
            {
                var value = ReadNativeValue(client, request.Handle, request.Type, request.Length);
                PublishNativeValue(request.Handle, value, request.Id);
            }
            catch (Exception error)
            {
                _errorReceived.OnNext(error);
            }
        }).DisposeWith(ConnectionLifetime);
    }

    /// <summary>Publishes a correlated native ADS value.</summary>
    /// <param name="handle">The ADS variable handle.</param>
    /// <param name="value">The native ADS value.</param>
    /// <param name="id">The optional correlation identifier.</param>
    private void PublishNativeValue(uint? handle, object? value, string? id)
    {
        if (value is null || !handle.HasValue)
        {
            return;
        }

        if (!_readWriteVariablesByHandle.TryGetValue(handle.Value, out var variable))
        {
            _ = _writeVariablesByHandle.TryGetValue(handle.Value, out variable);
        }

        if (string.IsNullOrWhiteSpace(variable))
        {
            return;
        }

        _dataReceived.OnNext((Variable: variable, Data: value, id));
    }

    /// <summary>Schedules all configured notification reads.</summary>
    /// <param name="client">The native ADS client.</param>
    private void ScheduleNotifications(AdsClient client)
    {
        foreach (var notification in Settings?.Notifications ?? [])
        {
            _ = ObservableBridgeExtensions.SubscribeTo(
                Observable.Interval(TimeSpan.FromMilliseconds(notification.UpdateRate)).Retry(int.MaxValue),
                _ => ReadNotification(client, notification)).DisposeWith(ConnectionLifetime);
        }
    }

    /// <summary>Reads one configured notification.</summary>
    /// <param name="client">The native ADS client.</param>
    /// <param name="notification">The configured notification.</param>
    private void ReadNotification(AdsClient client, RxNotification notification)
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
        _errorReceived.OnNext(
            new InvalidOperationException($"Please set Notification ArraySize to the {kind} length."));
    }
}
