// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET9_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using IoT.Driver.TwinCATRx.Core;
using TwinCAT.Ads;
using TwinCAT.TypeSystem;
using CoreExtensions = IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions;
using LeanBridge = IoT.Driver.TwinCATRx.ObservableBridgeExtensions;

namespace IoT.Driver.TwinCATRx.Tests.Rx;

/// <summary>Verifies ADS event notifications using composed production dependencies.</summary>
public sealed partial class RxTcAdsClientCompositionTests
{
    /// <summary>The ADS event cycle time.</summary>
    private const int AdsCycleTime = 250;

    /// <summary>The default ADS event cycle time.</summary>
    private const int AdsDefaultCycleTime = 100;

    /// <summary>The configured ADS maximum delay.</summary>
    private const int AdsMaxDelay = 30;

    /// <summary>Verifies default and explicit ADS settings in both package variants.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task AdsNotification_Settings_Preserve_Defaults_And_Custom_OptionsAsync()
    {
        var settings = new Settings();
        CoreExtensions.AddAdsNotification(settings, ValueVariable);
        CoreExtensions.AddAdsNotification(settings, ScalarVariable, AdsTransMode.Cyclic, AdsCycleTime, AdsMaxDelay);
        await TUnitAssert.That(settings.Notifications[0] is IAdsNotification).IsTrue();
        await TUnitAssert.That(settings.Notifications[1] is IAdsNotification).IsTrue();
        var defaults = (IAdsNotification)settings.Notifications[0];
        var custom = (IAdsNotification)settings.Notifications[1];
        await TUnitAssert.That(defaults.Variable).IsEqualTo(ValueVariable);
        await TUnitAssert.That(defaults.AdsTransMode).IsEqualTo(AdsTransMode.OnChange);
        await TUnitAssert.That(defaults.CycleTime).IsEqualTo(AdsDefaultCycleTime);
        await TUnitAssert.That(defaults.MaxDelay).IsEqualTo(0);
        await TUnitAssert.That(custom.AdsTransMode).IsEqualTo(AdsTransMode.Cyclic);
        await TUnitAssert.That(custom.CycleTime).IsEqualTo(AdsCycleTime);
        await TUnitAssert.That(custom.MaxDelay).IsEqualTo(AdsMaxDelay);

        var reactive = new IoT.Driver.TwinCATRx.Core.Reactive.Settings();
        IoT.Driver.TwinCATRx.Core.Reactive.TwinCatRxExtensions.AddAdsNotification(reactive, ValueVariable);
        await TUnitAssert.That(reactive.Notifications[0] is IoT.Driver.TwinCATRx.Core.Reactive.IAdsNotification).IsTrue();
        var reactiveDefault = (IoT.Driver.TwinCATRx.Core.Reactive.IAdsNotification)reactive.Notifications[0];
        await TUnitAssert.That(reactiveDefault.CycleTime).IsEqualTo(defaults.CycleTime);
        await TUnitAssert.That(reactiveDefault.AdsTransMode).IsEqualTo(defaults.AdsTransMode);
        await TUnitAssert.That(reactiveDefault.MaxDelay).IsEqualTo(defaults.MaxDelay);
    }

    /// <summary>Verifies invalid ADS settings fail before changing the configuration.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task AdsNotification_Settings_Reject_Invalid_OptionsAsync()
    {
        var settings = new Settings();
        await TUnitAssert.That(() => CoreExtensions.AddAdsNotification(settings, string.Empty)).Throws<ArgumentException>();
        await TUnitAssert.That(() => CoreExtensions.AddAdsNotification(settings, ValueVariable, cycleTime: -1))
            .Throws<ArgumentOutOfRangeException>();
        await TUnitAssert.That(() => CoreExtensions.AddAdsNotification(settings, ValueVariable, AdsTransMode.OnChange, AdsDefaultCycleTime, -1))
            .Throws<ArgumentOutOfRangeException>();
        CoreExtensions.AddAdsNotification(null, ValueVariable);
        await TUnitAssert.That(settings.Notifications).IsEmpty();
    }

    /// <summary>Verifies genuine event delivery, no native polling, errors, and subscription cleanup.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("Exercises production initialization and dynamic PLC type resolution.")]
    [RequiresUnreferencedCode("Exercises production initialization and dynamic PLC type resolution.")]
#endif
    public async Task AdsNotification_Pushes_Values_Without_Polling_And_Unhooks_On_DisposalAsync()
    {
        var ads = new FakeAdsClient { Port = TwinCat3Port };
        using var platform = new FakePlatform(ads);
        platform.AddSymbol(ValueSymbolName, "DINT", DataTypeCategory.Primitive);
        var settings = new Settings { Port = TwinCat3Port };
        CoreExtensions.AddAdsNotification(settings, ValueVariable, cycleTime: AdsCycleTime);
        using var client = new RxTcAdsClient(TimeProvider.System, platform);
        var initialized = CreatePublicationSource();
        var received = new TaskCompletionSource<(string Variable, object? Data, string? Id)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var errorReceived = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var ready = LeanBridge.SubscribeTo(client.InitializeComplete, _ => initialized.TrySetResult(true));
        using var data = LeanBridge.SubscribeTo(client.DataReceived, value => received.TrySetResult(value));
        using var errors = LeanBridge.SubscribeTo(client.ErrorReceived, error => errorReceived.TrySetResult(error));
        client.Connect(settings);
        await TUnitAssert.That(await DriveTicksUntilPublicationAsync(initialized.Task, platform.Ticks)).IsTrue();
        await TUnitAssert.That(ads.Observations.Count).IsEqualTo(1);
        await TUnitAssert.That(ads.Observations[0]).IsEqualTo((ValueVariable, AdsTransMode.OnChange, AdsCycleTime, 0));
        await TUnitAssert.That(ads.ReadAttempted.Task.IsCompleted).IsFalse();
        ads.ValueChanges.Emit(ScalarPayload);
        await TUnitAssert.That(await WaitForAdsPublicationAsync(received.Task)).IsTrue();
        var value = await received.Task;
        await TUnitAssert.That(value.Variable).IsEqualTo(ValueVariable);
        await TUnitAssert.That(value.Data).IsEqualTo(ScalarPayload);
        await TUnitAssert.That(value.Id).IsNull();
        var failure = new IOException("ADS event failure");
        ads.ValueChanges.Fail(failure);
        await TUnitAssert.That(await WaitForAdsPublicationAsync(errorReceived.Task)).IsTrue();
        await TUnitAssert.That(await errorReceived.Task).IsSameReferenceAs(failure);
        client.Dispose();
        await TUnitAssert.That(ads.ValueChanges.ObserverCount).IsEqualTo(0);
    }

    /// <summary>Verifies an explicit reconnect creates exactly one fresh ADS event subscription.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("Exercises production initialization and dynamic PLC type resolution.")]
    [RequiresUnreferencedCode("Exercises production initialization and dynamic PLC type resolution.")]
#endif
    public async Task AdsNotification_Reconnect_Rebuilds_Event_SubscriptionAsync()
    {
        var ads = new FakeAdsClient { Port = TwinCat3Port };
        using var platform = new FakePlatform(ads);
        platform.AddSymbol(ValueSymbolName, "DINT", DataTypeCategory.Primitive);
        var settings = new Settings { Port = TwinCat3Port };
        CoreExtensions.AddAdsNotification(settings, ValueVariable);
        using var client = new RxTcAdsClient(TimeProvider.System, platform);
        var firstReady = CreatePublicationSource();
        var secondReady = CreatePublicationSource();
        var readinessCount = 0;
        using var ready = LeanBridge.SubscribeTo(client.InitializeComplete, value =>
        {
            _ = value;
            readinessCount++;
            if (readinessCount == 1)
            {
                _ = firstReady.TrySetResult(true);
            }
            else
            {
                _ = secondReady.TrySetResult(true);
            }
        });
        client.Connect(settings);
        await TUnitAssert.That(await DriveTicksUntilPublicationAsync(firstReady.Task, platform.Ticks)).IsTrue();
        await TUnitAssert.That(ads.ValueChanges.ObserverCount).IsEqualTo(1);
        client.Disconnect();
        await TUnitAssert.That(ads.ValueChanges.ObserverCount).IsEqualTo(0);
        client.Connect(settings);
        await TUnitAssert.That(await DriveTicksUntilPublicationAsync(secondReady.Task, platform.Ticks)).IsTrue();
        await TUnitAssert.That(ads.Observations.Count).IsEqualTo(ExpectedNotificationHandleCount);
        await TUnitAssert.That(ads.ValueChanges.ObserverCount).IsEqualTo(1);
        client.Disconnect();
        await TUnitAssert.That(ads.ValueChanges.ObserverCount).IsEqualTo(0);
    }

    /// <summary>Waits for one event publication with a bounded deadline.</summary>
    /// <param name="publication">The expected publication.</param>
    /// <returns>Whether the publication completed before the deadline.</returns>
    private static async Task<bool> WaitForAdsPublicationAsync(Task publication) =>
        await Task.WhenAny(publication, Task.Delay(PublicationTimeout)) == publication;
}
