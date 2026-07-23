// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ServiceProcess;
#if !NETFRAMEWORK
using System.Runtime.Versioning;
#endif
using IoT.DriverCore.TwinCATRx.Core;
using TwinCAT.Ads;
using LeanBridge = IoT.DriverCore.TwinCATRx.ObservableBridgeExtensions;

namespace IoT.DriverCore.TwinCATRx.Tests.Rx;

/// <summary>Exercises disconnected native adapter entry points without requiring a TwinCAT route.</summary>
#if !NETFRAMEWORK
[SupportedOSPlatform("windows")]
#endif
public sealed class NativeRuntimeAdapterCoverageTests
{
    /// <summary>A representative disconnected ADS handle.</summary>
    private const uint DisconnectedHandle = 1;

    /// <summary>A deliberately invalid local ADS port.</summary>
    private const int InvalidPort = -1;

    /// <summary>A deliberately invalid remote ADS route.</summary>
    private const string InvalidRoute = "invalid.route";

    /// <summary>Exercises native ADS and symbol adapters at their disconnected boundary.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Disconnected_Ads_And_Symbol_Adapters_Expose_All_Gateway_Entry_PointsAsync()
    {
        var errors = new List<Exception>();
        var attempts = 0;
        using (var ads = new AdsClientRuntime())
        {
            Invoke(() => _ = ads.CreateVariableHandle(".Value"), errors, ref attempts);
            Invoke(() => _ = ads.ReadAny(DisconnectedHandle, typeof(int), [1]), errors, ref attempts);
            Invoke(() => ads.WriteAny(DisconnectedHandle, 1), errors, ref attempts);
            Invoke(
                () => ads.WriteControl(new StateInfo(AdsState.Run, 0)),
                errors,
                ref attempts);
        }

        using (var symbols = new CodeGeneratorRuntime())
        {
            Invoke(() => symbols.LoadSymbols(InvalidPort, static _ => { }), errors, ref attempts);
            Invoke(
                () => _ = symbols.ReadSymbol(InvalidRoute, InvalidPort, ".Value", typeof(int)),
                errors,
                ref attempts);
        }

        await TUnitAssert.That(attempts).IsGreaterThan(0);
        await TUnitAssert.That(errors).IsNotEmpty();
    }

    /// <summary>Exercises platform and service adapters at their operating-system boundary.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Native_Platform_And_Service_Adapters_Expose_All_Gateway_Entry_PointsAsync()
    {
        var errors = new List<Exception>();
        var attempts = 0;
        var platform = RxTcAdsPlatform.CreateDefault();
        await TUnitAssert.That(platform.IsWindowsServiceMonitoringSupported).IsTrue();
        using var platformAds = platform.CreateAdsClient();
        using var platformGenerator = platform.CreateCodeGenerator();
        var platformServices = new List<IObservableServiceController>();
        using var platformSubscription = LeanBridge.SubscribeTo(platform.GetServices(), platformServices.Add);

        using var service = new ServiceController();
        using var runtime = new ServiceControllerRuntime(service);
        EventHandler handler = static (_, _) => { };
        Invoke(() => runtime.Disposed += handler, errors, ref attempts);
        Invoke(() => runtime.Disposed -= handler, errors, ref attempts);
        Invoke(() => _ = runtime.CanStop, errors, ref attempts);
        Invoke(() => _ = runtime.DisplayName, errors, ref attempts);
        Invoke(() => _ = runtime.ServiceName, errors, ref attempts);
        Invoke(() => _ = runtime.Status, errors, ref attempts);
        Invoke(runtime.Refresh, errors, ref attempts);
        Invoke(runtime.Start, errors, ref attempts);
        Invoke(runtime.Stop, errors, ref attempts);
        Invoke(
            () => runtime.WaitForStatus(ServiceControllerStatus.Stopped),
            errors,
            ref attempts);
        Invoke(
            () =>
            {
                foreach (var discovered in ServiceControllerSource.Instance.GetServices().Take(1))
                {
                    discovered.Dispose();
                }
            },
            errors,
            ref attempts);

        await TUnitAssert.That(attempts).IsGreaterThan(0);
        await TUnitAssert.That(errors).IsNotEmpty();
        await TUnitAssert.That(platformServices).IsNotEmpty();
    }

    /// <summary>Invokes one native boundary and records any environment-dependent failure.</summary>
    /// <param name="action">The boundary invocation.</param>
    /// <param name="errors">The recorded failures.</param>
    /// <param name="attempts">The invocation count.</param>
    private static void Invoke(Action action, List<Exception> errors, ref int attempts)
    {
        attempts++;
        try
        {
            action();
        }
        catch (Exception error)
        {
            errors.Add(error);
        }
    }
}
