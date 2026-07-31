// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM
namespace IoT.Driver.ModbusRx.Reactive;
#else
namespace IoT.Driver.ModbusRx;
#endif
/// <summary>Marks a property as a holding-register point.</summary>
/// <param name="address">The zero-based Modbus address.</param>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class HoldingRegisterAttribute(ushort address = 0) : Attribute
{
    /// <summary>Gets the zero-based Modbus address.</summary>
    public ushort Address { get; } = address;

    /// <summary>Gets or sets the number of registers to read.</summary>
    public ushort Count { get; set; }

    /// <summary>Gets or sets the value representation.</summary>
    public ModbusReactiveDataType DataType { get; set; }

    /// <summary>Gets or sets a value indicating whether multi-register values swap words.</summary>
    public bool SwapWords { get; set; } = true;

    /// <summary>Gets or sets the optional logical tag name.</summary>
    public string? TagName { get; set; }
}
