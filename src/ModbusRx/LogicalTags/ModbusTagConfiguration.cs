// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using CP.IoT.Core;

#if REACTIVE_SHIM
namespace ModbusRx.Reactive.LogicalTags;
#else
namespace ModbusRx.LogicalTags;
#endif

/// <summary>Collects the required address and optional behavior of a Modbus logical tag.</summary>
public sealed class ModbusTagConfiguration
{
    /// <summary>Initializes a new instance of the <see cref="ModbusTagConfiguration"/> class.</summary>
    /// <param name="name">The unique logical name.</param>
    /// <param name="unitId">The Modbus unit identifier.</param>
    /// <param name="dataArea">The Modbus data area.</param>
    /// <param name="address">The zero-based Modbus address.</param>
    /// <param name="count">The number of Modbus points.</param>
    /// <param name="clrDataType">The exposed CLR data type.</param>
    public ModbusTagConfiguration(
        string name,
        byte unitId,
        ModbusDataArea dataArea,
        ushort address,
        ushort count,
        Type clrDataType)
    {
        Name = name;
        UnitId = unitId;
        DataArea = dataArea;
        Address = address;
        Count = count;
        ClrDataType = clrDataType;
    }

    /// <summary>Gets the unique logical name.</summary>
    public string Name { get; }

    /// <summary>Gets the Modbus unit identifier.</summary>
    public byte UnitId { get; }

    /// <summary>Gets the Modbus data area.</summary>
    public ModbusDataArea DataArea { get; }

    /// <summary>Gets the zero-based Modbus address.</summary>
    public ushort Address { get; }

    /// <summary>Gets the number of Modbus points.</summary>
    public ushort Count { get; }

    /// <summary>Gets the exposed CLR data type.</summary>
    public Type ClrDataType { get; }

    /// <summary>Gets or sets the register byte and word order.</summary>
    public ModbusByteOrder ByteOrder { get; set; } = ModbusByteOrder.BigEndian;

    /// <summary>Gets or sets the optional group name.</summary>
    public string? GroupName { get; set; }

    /// <summary>Gets or sets the optional description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets caller-defined metadata.</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; set; }

    /// <summary>Gets or sets the permitted access mode.</summary>
    public LogicalTagAccessMode AccessMode { get; set; } = LogicalTagAccessMode.ReadWrite;

    /// <summary>Gets or sets the preferred observation interval.</summary>
    public TimeSpan? ScanInterval { get; set; }
}
