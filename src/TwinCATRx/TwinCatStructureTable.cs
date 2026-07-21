// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using CP.Collections.Reactive;
#else
using CP.Collections;
#endif

#if REACTIVE_SHIM
namespace CP.TwinCatRx.Reactive;
#else
namespace CP.TwinCatRx;
#endif

/// <summary>Owns the ADS subscription that feeds a structured HashTableRx instance.</summary>
/// <param name="useUpperCase">Whether structure paths use TwinCAT 2 upper-case normalization.</param>
internal sealed class TwinCatStructureTable(bool useUpperCase) : HashTableRx(useUpperCase)
{
    /// <summary>Stores the owned ADS source subscription.</summary>
    private IDisposable? _sourceSubscription;

    /// <summary>Transfers ownership of the source subscription to this table.</summary>
    /// <param name="subscription">The ADS subscription.</param>
    internal void SetSourceSubscription(IDisposable subscription)
    {
        if (Interlocked.CompareExchange(ref _sourceSubscription, subscription, null) is null)
        {
            return;
        }

        subscription.Dispose();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Interlocked.Exchange(ref _sourceSubscription, null)?.Dispose();
        }

        base.Dispose(disposing);
    }
}
