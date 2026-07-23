// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ServiceProcess;
#if !NETFRAMEWORK
using System.Runtime.Versioning;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.TwinCATRx.Reactive;
#else
namespace IoT.DriverCore.TwinCATRx;
#endif

/// <summary>Defines the Windows service operations consumed by <see cref="ObservableServiceController"/>.</summary>
#if !NETFRAMEWORK
[SupportedOSPlatform("windows")]
#endif
internal interface IServiceControllerRuntime : IDisposable
{
    /// <summary>Occurs when the underlying service controller is disposed.</summary>
    event EventHandler? Disposed;

    /// <summary>Gets a value indicating whether the service can stop.</summary>
    bool CanStop { get; }

    /// <summary>Gets the service display name.</summary>
    string DisplayName { get; }

    /// <summary>Gets the service name.</summary>
    string ServiceName { get; }

    /// <summary>Gets the current service status.</summary>
    ServiceControllerStatus Status { get; }

    /// <summary>Refreshes service metadata.</summary>
    void Refresh();

    /// <summary>Starts the service.</summary>
    void Start();

    /// <summary>Stops the service.</summary>
    void Stop();

    /// <summary>Waits for the requested service status.</summary>
    /// <param name="status">The requested status.</param>
    void WaitForStatus(ServiceControllerStatus status);
}
