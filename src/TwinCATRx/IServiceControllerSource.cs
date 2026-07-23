// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if !NETFRAMEWORK
using System.Runtime.Versioning;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.TwinCATRx.Reactive;
#else
namespace IoT.DriverCore.TwinCATRx;
#endif

/// <summary>Enumerates Windows service runtimes.</summary>
#if !NETFRAMEWORK
[SupportedOSPlatform("windows")]
#endif
internal interface IServiceControllerSource
{
    /// <summary>Gets all installed services.</summary>
    /// <returns>The service runtimes.</returns>
    IEnumerable<IServiceControllerRuntime> GetServices();
}
