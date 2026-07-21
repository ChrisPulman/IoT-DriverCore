// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.PlcTypes;
#else
namespace S7PlcRx.PlcTypes;
#endif

/// <summary>Provides compatibility entry points for binary and numeric conversion helpers.</summary>
internal static class Conversion
{
    /// <summary>Sets a bit value on the byte at the specified bit index.</summary>
    /// <param name="data">The data to be modified.</param>
    /// <param name="index">The zero-based index of the bit to set.</param>
    /// <param name="value">The Boolean value to assign to the bit.</param>
    internal static void SetBit(ref byte data, int index, bool value) =>
        ConversionExtensions.SetBit(ref data, index, value);
}
