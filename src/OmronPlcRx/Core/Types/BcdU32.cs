// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Core.Types;
#else
namespace IoT.DriverCore.OmronPlcRx.Core.Types;
#endif

/// <summary>Unsigned 32-bit BCD numeric wrapper.</summary>
public readonly record struct BcdU32
{
    /// <summary>Initializes a new instance of the <see cref="BcdU32"/> struct.</summary>
    /// <param name="value">The unsigned value.</param>
    public BcdU32(uint value) => Value = value;

    /// <summary>Gets the numeric value.</summary>
    public uint Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();
}
