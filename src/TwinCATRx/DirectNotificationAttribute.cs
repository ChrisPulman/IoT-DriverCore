// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.TwinCATRx.Reactive;
#else
namespace IoT.Driver.TwinCATRx;
#endif

/// <summary>Marks a directly observed TwinCAT property.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class DirectNotificationAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="DirectNotificationAttribute"/> class.</summary>
    /// <param name="address">The TwinCAT variable address.</param>
    public DirectNotificationAttribute(string address) => Address = address;

    /// <summary>Gets the TwinCAT variable address.</summary>
    public string Address { get; }

    /// <summary>Gets or sets the notification cycle time.</summary>
    public int CycleTime { get; set; } = 100;

    /// <summary>Gets or sets the array size.</summary>
    public int ArraySize { get; set; } = -1;

    /// <summary>Gets or sets the optional correlation identifier.</summary>
    public string? Id { get; set; }

    /// <summary>Gets or sets the generated observable name.</summary>
    public string? ObservableName { get; set; }

    /// <summary>Gets or sets a value indicating whether writes are generated.</summary>
    public bool CanWrite { get; set; } = true;

    /// <summary>Gets or sets an optional alternate write address.</summary>
    public string? WriteAddress { get; set; }
}
