// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
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

/// <summary>Provides the default operating-system and Beckhoff dependencies.</summary>
internal sealed class RxTcAdsPlatform : IRxTcAdsPlatform
{
    /// <inheritdoc/>
    public bool IsWindowsServiceMonitoringSupported =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <inheritdoc/>
    public IAdsClientRuntime CreateAdsClient() => new AdsClientRuntime();

    /// <inheritdoc/>
    public ICodeGenerator CreateCodeGenerator() => new CodeGenerator();

    /// <inheritdoc/>
    public IObservable<long> Interval(TimeSpan period) => Observable.Interval(period);

    /// <inheritdoc/>
    public void LoadSymbols(ICodeGenerator codeGenerator, string adsAddress, int port) =>
        _ = codeGenerator.LoadSymbols(adsAddress, port);

    /// <inheritdoc/>
#if !NETFRAMEWORK
    [SupportedOSPlatform("windows")]
#endif
    public IObservable<IObservableServiceController> GetServices() =>
        ObservableServiceController.GetServices().Select(
            static service => (IObservableServiceController)service);

    /// <summary>Creates the default stateless platform.</summary>
    /// <returns>The default platform.</returns>
    internal static RxTcAdsPlatform CreateDefault() => new();
}
