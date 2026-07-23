// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using IoT.DriverCore.Core;
#if REACTIVE_SHIM
using IoT.DriverCore.OmronPlcRx.Reactive.Tags;
using IoT.DriverCore.Serial.Reactive;
#else
using IoT.DriverCore.OmronPlcRx.Tags;
using IoT.DriverCore.Serial;
#endif
using ReactiveUI.Primitives.Async;

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Async;
#else
namespace IoT.DriverCore.OmronPlcRx.Async;
#endif

/// <summary>Bridges Omron PLC classic Rx streams into ReactiveUI.Primitives.Async observables.</summary>
public static class OmronPlcRxAsyncObservableExtensions
{
    /// <summary>Observes a typed PLC tag as an async observable.</summary>
    /// <typeparam name="T">The tag value type.</typeparam>
    /// <param name="plc">The PLC reactive facade.</param>
    /// <param name="tag">The registered typed tag.</param>
    /// <returns>An async observable that can use ReactiveUI.Primitives.Async operators.</returns>
    public static IObservableAsync<T?> ObserveAsAsyncObservable<T>(
        IOmronPlcRx plc,
        LogicalTagKey<T> tag)
    {
        if (plc is null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        return ObservableAsyncBridgeExtensions.ToAsyncObservable(plc.Observe(tag));
    }

    /// <summary>Observes every changed PLC tag as an async observable.</summary>
    /// <param name="plc">The PLC reactive facade.</param>
    /// <returns>An async observable of all changed tags.</returns>
    public static IObservableAsync<IPlcTag?> ObserveAllAsAsyncObservable(IOmronPlcRx plc)
    {
        if (plc is null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        return ObservableAsyncBridgeExtensions.ToAsyncObservable(plc.ObserveAll);
    }

    /// <summary>Observes PLC operational errors as an async observable.</summary>
    /// <param name="plc">The PLC reactive facade.</param>
    /// <returns>An async observable of PLC errors.</returns>
    public static IObservableAsync<OmronPLCException?> ErrorsAsAsyncObservable(IOmronPlcRx plc)
    {
        if (plc is null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        return ObservableAsyncBridgeExtensions.ToAsyncObservable(plc.Errors);
    }

    /// <summary>Observes a typed PLC tag as an async enumerable.</summary>
    /// <typeparam name="T">The tag value type.</typeparam>
    /// <param name="plc">The PLC reactive facade.</param>
    /// <param name="tag">The registered typed tag.</param>
    /// <param name="cancellationToken">Cancellation token for the async enumeration.</param>
    /// <returns>An async enumerable of tag values.</returns>
    public static IAsyncEnumerable<T?> ObserveValuesAsync<T>(
        IOmronPlcRx plc,
        LogicalTagKey<T> tag,
        CancellationToken cancellationToken) =>
        ObserveAsAsyncObservable(plc, tag)
            .TakeUntil(cancellationToken)
            .ToAsyncEnumerable(static () => System.Threading.Channels.Channel.CreateUnbounded<T?>());
}
