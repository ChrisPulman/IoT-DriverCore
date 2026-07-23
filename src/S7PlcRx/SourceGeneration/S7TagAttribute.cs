// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.S7PlcRx.Reactive.SourceGeneration;

#else
namespace IoT.DriverCore.S7PlcRx.SourceGeneration;

#endif

/// <summary>Marks a partial property as a PLC tag binding target.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class S7TagAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="S7TagAttribute"/> class.</summary>
    /// <param name="address">The PLC tag address.</param>
    public S7TagAttribute(string address) => Address = address;

    /// <summary>Gets the PLC tag address.</summary>
    public string Address { get; }

    /// <summary>Gets or sets the polling interval in milliseconds.</summary>
    public int PollIntervalMs { get; set; } = 100;

    /// <summary>Gets or sets the binding direction.</summary>
    public S7TagDirection Direction { get; set; }

    /// <summary>Gets or sets the PLC array length.</summary>
    public int ArrayLength { get; set; } = 1;
}
