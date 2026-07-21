// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace ABPlcRx.Reactive;
#else
namespace ABPlcRx;
#endif

/// <summary>Provides assembly-local value assignment without automatic write-through.</summary>
internal interface IPlcTagLocalValue
{
    /// <summary>Sets the local value without invoking PLC IO.</summary>
    /// <param name="value">The local value.</param>
    void SetLocalValue(object? value);
}
