// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive;

#else
namespace IoT.DriverCore.OmronPlcRx;

#endif

/// <summary>Marks a field or property for PLC reactive stream source generation.</summary>
/// <param name="address">The a dd re ss value.</param>
[AttributeUsage(
    AttributeTargets.Field | AttributeTargets.Property,
    AllowMultiple = false,
    Inherited = false)]
public sealed class PlcTagAttribute(string address) : Attribute
{
    /// <summary>Gets the address value.</summary>
    public string Address { get; } = address;

    /// <summary>Gets or sets the tag name value.</summary>
    public string? TagName { get; set; }

    /// <summary>Gets or sets the register value.</summary>
    public bool Register { get; set; } = true;

    /// <summary>Gets or sets the observe value.</summary>
    public bool Observe { get; set; } = true;

    /// <summary>Gets or sets the writable value.</summary>
    public bool Writable { get; set; }
}
