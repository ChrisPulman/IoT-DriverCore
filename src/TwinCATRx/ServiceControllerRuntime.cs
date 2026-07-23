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

/// <summary>Adapts <see cref="ServiceController"/> to a replaceable service runtime.</summary>
#if !NETFRAMEWORK
[SupportedOSPlatform("windows")]
#endif
internal sealed class ServiceControllerRuntime : IServiceControllerRuntime
{
    /// <summary>Stores the wrapped service controller.</summary>
    private readonly ServiceController _service;

    /// <summary>Initializes a new instance of the <see cref="ServiceControllerRuntime"/> class.</summary>
    /// <param name="service">The wrapped service.</param>
    internal ServiceControllerRuntime(ServiceController service) =>
        _service = service ?? throw new ArgumentNullException(nameof(service));

    /// <inheritdoc/>
    public event EventHandler? Disposed
    {
        add => _service.Disposed += value;
        remove => _service.Disposed -= value;
    }

    /// <inheritdoc/>
    public bool CanStop => _service.CanStop;

    /// <inheritdoc/>
    public string DisplayName => _service.DisplayName;

    /// <inheritdoc/>
    public string ServiceName => _service.ServiceName;

    /// <inheritdoc/>
    public ServiceControllerStatus Status => _service.Status;

    /// <inheritdoc/>
    public void Dispose() => _service.Dispose();

    /// <inheritdoc/>
    public void Refresh() => _service.Refresh();

    /// <inheritdoc/>
    public void Start() => _service.Start();

    /// <inheritdoc/>
    public void Stop() => _service.Stop();

    /// <inheritdoc/>
    public void WaitForStatus(ServiceControllerStatus status) => _service.WaitForStatus(status);
}
