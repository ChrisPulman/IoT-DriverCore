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

/// <summary>Enumerates service controllers through the Windows service manager.</summary>
#if !NETFRAMEWORK
[SupportedOSPlatform("windows")]
#endif
internal sealed class ServiceControllerSource : IServiceControllerSource
{
    /// <summary>Gets the shared stateless source.</summary>
    internal static ServiceControllerSource Instance { get; } = new();

    /// <inheritdoc/>
    public IEnumerable<IServiceControllerRuntime> GetServices() =>
        ServiceController.GetServices().Select(static service => new ServiceControllerRuntime(service));
}
