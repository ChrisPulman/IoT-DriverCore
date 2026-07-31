// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.ModbusRx.Reactive;
#else
namespace IoT.Driver.ModbusRx;
#endif

/// <summary>Marks a partial class for Modbus reactive stream generation.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ModbusReactiveDeviceAttribute : Attribute
{
    /// <summary>Gets or sets the observable Modbus master connection member.</summary>
    public string ConnectionMember { get; set; } = "MasterStream";

    /// <summary>Gets or sets the optional logical-tag client member.</summary>
    public string? TagClientMember { get; set; }

    /// <summary>Gets or sets the Modbus slave address.</summary>
    public byte SlaveAddress { get; set; } = 1;

    /// <summary>Gets or sets the default polling interval in milliseconds.</summary>
    public double DefaultInterval { get; set; } = 1000.0;

    /// <summary>Gets or sets the Modbus master kind.</summary>
    public ModbusReactiveMasterKind MasterKind { get; set; }
}
