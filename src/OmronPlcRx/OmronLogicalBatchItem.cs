// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive;
#else
namespace IoT.DriverCore.OmronPlcRx;
#endif

/// <summary>Describes one indexed logical tag supplied to a grouped FINS operation.</summary>
/// <param name="inputIndex">Original caller position.</param>
/// <param name="tagName">Logical tag name.</param>
/// <param name="address">Omron memory address.</param>
/// <param name="valueType">Runtime tag value type.</param>
/// <param name="value">Converted write value, or <see langword="null"/> for a read.</param>
internal sealed class OmronLogicalBatchItem(
    int inputIndex,
    string tagName,
    string address,
    Type valueType,
    object? value)
{
    /// <summary>Gets the original caller position.</summary>
    internal int InputIndex { get; } = inputIndex;

    /// <summary>Gets the logical tag name.</summary>
    internal string TagName { get; } = tagName ?? throw new ArgumentNullException(nameof(tagName));

    /// <summary>Gets the Omron memory address.</summary>
    internal string Address { get; } = address ?? throw new ArgumentNullException(nameof(address));

    /// <summary>Gets the runtime tag value type.</summary>
    internal Type ValueType { get; } = valueType ?? throw new ArgumentNullException(nameof(valueType));

    /// <summary>Gets the converted write value, or <see langword="null"/> for a read.</summary>
    internal object? Value { get; } = value;
}
