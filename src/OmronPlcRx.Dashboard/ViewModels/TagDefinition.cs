// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using ReactiveUI;

namespace IoT.DriverCore.OmronPlcRx.Dashboard.ViewModels;

/// <summary>Represents a PLC tag definition and its current value.</summary>
/// <param name="name">The tag name.</param>
/// <param name="address">The PLC address.</param>
/// <param name="valueType">The tag value type.</param>
public sealed class TagDefinition(string name, string address, Type valueType) : ReactiveObject
{
    /// <summary>Gets the name value.</summary>
    public string Name { get; } = name;

    /// <summary>Gets the address value.</summary>
    public string Address { get; } = address;

    /// <summary>Gets the value type value.</summary>
    public Type ValueType { get; } = valueType;

    /// <summary>Gets or sets the current value.</summary>
    public object? Value
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }
}
