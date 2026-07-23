// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if !NETFRAMEWORK
using System.Runtime.Versioning;
#endif

#if REACTIVE_SHIM
using IoT.DriverCore.TwinCATRx.Core.Reactive;
#else
using IoT.DriverCore.TwinCATRx.Core;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.TwinCATRx.Reactive;
#else
namespace IoT.DriverCore.TwinCATRx;
#endif

/// <summary>Provides replaceable runtime dependencies for <see cref="RxTcAdsClient"/>.</summary>
internal interface IRxTcAdsPlatform
{
    /// <summary>Gets a value indicating whether Windows service monitoring is available.</summary>
    bool IsWindowsServiceMonitoringSupported { get; }

    /// <summary>Creates one ADS client runtime.</summary>
    /// <returns>The runtime.</returns>
    IAdsClientRuntime CreateAdsClient();

    /// <summary>Creates one PLC code generator.</summary>
    /// <returns>The code generator.</returns>
    ICodeGenerator CreateCodeGenerator();

    /// <summary>Creates a periodic sequence.</summary>
    /// <param name="period">The period.</param>
    /// <returns>The periodic sequence.</returns>
    IObservable<long> Interval(TimeSpan period);

    /// <summary>Loads symbols into a code generator.</summary>
    /// <param name="codeGenerator">The code generator.</param>
    /// <param name="adsAddress">The ADS address.</param>
    /// <param name="port">The ADS port.</param>
    void LoadSymbols(ICodeGenerator codeGenerator, string adsAddress, int port);

    /// <summary>Enumerates observable Windows services.</summary>
    /// <returns>The service sequence.</returns>
#if !NETFRAMEWORK
    [SupportedOSPlatform("windows")]
#endif
    IObservable<IObservableServiceController> GetServices();
}
