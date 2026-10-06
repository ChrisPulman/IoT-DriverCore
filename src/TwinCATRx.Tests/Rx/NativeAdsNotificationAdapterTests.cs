// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using TwinCAT.Ads;
using TwinCAT.TypeSystem;
using LeanBridge = IoT.Driver.TwinCATRx.ObservableBridgeExtensions;

namespace IoT.Driver.TwinCATRx.Tests.Rx;

/// <summary>Tests the production Beckhoff event bridge against a controlled symbol event.</summary>
public sealed class NativeAdsNotificationAdapterTests
{
    /// <summary>The cycle time.</summary>
    private const int CycleTime = 250;

    /// <summary>The default cycle time.</summary>
    private const int DefaultCycleTime = 100;

    /// <summary>The maximum delay.</summary>
    private const int MaxDelay = 10;

    /// <summary>The initial payload.</summary>
    private const int InitialValue = 42;

    /// <summary>The updated payload.</summary>
    private const int UpdatedValue = 73;

    /// <summary>The subscription count.</summary>
    private const int SubscriptionCount = 2;

    /// <summary>Verifies lazy shared loader creation and native runtime ownership of hooks.</summary>
    /// <returns>The test task.</returns>
    [Test]
    [RequiresDynamicCode("Creates controlled Beckhoff interface proxies.")]
    [RequiresUnreferencedCode("Creates controlled Beckhoff interface proxies.")]
    public async Task AdsNotification_Native_Runtime_Shares_Loader_And_Disposes_HooksAsync()
    {
        var symbol = DispatchProxy.Create<IValueSymbol, ValueSymbolProxy>();
        var proxy = (ValueSymbolProxy)(object)symbol;
        var loader = DispatchProxy.Create<ISymbolLoader, LoaderProxy>();
        var loaderProxy = (LoaderProxy)(object)loader;
        loaderProxy.Symbol = symbol;
        var creations = 0;
        using var runtime = new AdsClientRuntime(_ =>
        {
            creations++;
            return loader;
        });
        await TUnitAssert.That(creations).IsEqualTo(0);
        var first = runtime.ObserveValue(".First", AdsTransMode.OnChange, CycleTime, 0);
        var second = runtime.ObserveValue(".Second", AdsTransMode.Cyclic, DefaultCycleTime, MaxDelay);
        await TUnitAssert.That(creations).IsEqualTo(1);
        using var firstSubscription = LeanBridge.SubscribeTo(first, static _ => { });
        using var secondSubscription = LeanBridge.SubscribeTo(second, static _ => { });
        await TUnitAssert.That(creations).IsEqualTo(1);
        await TUnitAssert.That(proxy.AttachCount).IsEqualTo(SubscriptionCount);
        runtime.Dispose();
        await TUnitAssert.That(proxy.DetachCount).IsEqualTo(SubscriptionCount);
        await TUnitAssert.That(loaderProxy.DisposeCount).IsEqualTo(1);
    }

    /// <summary>Verifies the event callback only transfers the value and disposal removes its hook.</summary>
    /// <returns>The test task.</returns>
    [Test]
    [RequiresDynamicCode("Creates a controlled interface proxy for the Beckhoff symbol contract.")]
    [RequiresUnreferencedCode("Creates a controlled interface proxy for the Beckhoff symbol contract.")]
    public async Task AdsNotification_Native_Event_Bridge_Transfers_And_UnhooksAsync()
    {
        var symbol = DispatchProxy.Create<IValueSymbol, ValueSymbolProxy>();
        var proxy = (ValueSymbolProxy)(object)symbol;
        var values = new List<object>();
        using var subscription = LeanBridge.SubscribeTo(AdsClientRuntime.ObserveSymbolValue(symbol), values.Add);
        await TUnitAssert.That(proxy.AttachCount).IsEqualTo(1);
        proxy.Emit(symbol, InitialValue);
        await TUnitAssert.That(values.Count).IsEqualTo(1);
        await TUnitAssert.That(values[0]).IsEqualTo(InitialValue);
        subscription.Dispose();
        await TUnitAssert.That(proxy.DetachCount).IsEqualTo(1);
        proxy.Emit(symbol, UpdatedValue);
        await TUnitAssert.That(values.Count).IsEqualTo(1);

        using var reactive = LeanBridge.SubscribeTo(
            IoT.Driver.TwinCATRx.Reactive.AdsClientRuntime.ObserveSymbolValue(symbol),
            values.Add);
        proxy.Emit(symbol, UpdatedValue);
        await TUnitAssert.That(values.Count).IsEqualTo(SubscriptionCount);
        await TUnitAssert.That(values[1]).IsEqualTo(UpdatedValue);
        reactive.Dispose();
        await TUnitAssert.That(proxy.DetachCount).IsEqualTo(SubscriptionCount);
    }

    /// <summary>Provides a controlled Beckhoff symbol event implementation.</summary>
    public class ValueSymbolProxy : DispatchProxy
    {
        /// <summary>Stores the subscribed Beckhoff event handlers.</summary>
        private EventHandler<ValueChangedEventArgs>? _handlers;

        /// <summary>Gets the event attachment count.</summary>
        public int AttachCount { get; private set; }

        /// <summary>Gets the event removal count.</summary>
        public int DetachCount { get; private set; }

        /// <summary>Raises one real Beckhoff event argument.</summary>
        /// <param name="symbol">The event source.</param>
        /// <param name="value">The transferred value.</param>
        public void Emit(IValueSymbol symbol, object value) =>
            _handlers?.Invoke(symbol, new ValueChangedEventArgs(symbol, value, DateTimeOffset.UnixEpoch));

        /// <inheritdoc/>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "set_NotificationSettings")
            {
                return null;
            }

            if (targetMethod?.Name == "add_ValueChanged")
            {
                _handlers += (EventHandler<ValueChangedEventArgs>)args![0]!;
                AttachCount++;
                return null;
            }

            if (targetMethod?.Name == "remove_ValueChanged")
            {
                _handlers -= (EventHandler<ValueChangedEventArgs>)args![0]!;
                DetachCount++;
                return null;
            }

            throw new InvalidOperationException($"Unexpected symbol access: {targetMethod?.Name}");
        }
    }

    /// <summary>Provides a controlled loader and symbol collection.</summary>
    public class LoaderProxy : DispatchProxy, IDisposable
    {
        /// <summary>Gets or sets the controlled symbol.</summary>
        public IValueSymbol? Symbol { get; set; }

        /// <summary>Gets the number of loader disposals.</summary>
        public int DisposeCount { get; private set; }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Records loader disposal.</summary>
        /// <param name="disposing">Whether disposal is explicit.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            DisposeCount++;
        }

        /// <inheritdoc/>
        [RequiresDynamicCode("Creates a controlled symbol collection proxy.")]
        [RequiresUnreferencedCode("Creates a controlled symbol collection proxy.")]
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            _ = args;
            if (targetMethod?.Name == "get_Symbols")
            {
                var collection = DispatchProxy.Create(targetMethod.ReturnType, typeof(LoaderProxy));
                ((LoaderProxy)collection).Symbol = Symbol;
                return collection;
            }

            if (targetMethod?.Name == "get_Item")
            {
                return Symbol;
            }

            throw new InvalidOperationException($"Unexpected loader access: {targetMethod?.Name}");
        }
    }
}
#endif
