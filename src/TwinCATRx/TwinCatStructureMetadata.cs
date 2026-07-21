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

/// <summary>Reads the interface-based client link stored on a TwinCAT structure table.</summary>
internal static class TwinCatStructureMetadata
{
    /// <summary>Gets the ADS client associated with a structure table.</summary>
    /// <param name="table">The structure table.</param>
    /// <returns>The associated ADS client, or null.</returns>
    internal static IRxTcAdsClient? GetClient(HashTableRx table) =>
        table.Tag?[nameof(IRxTcAdsClient)] as IRxTcAdsClient ??
        table.Tag?[nameof(RxTcAdsClient)] as IRxTcAdsClient;
}
