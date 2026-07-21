// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive.Core.Types;
#else
namespace OmronPlcRx.Core.Types;
#endif

/// <summary>Signed 32-bit BCD numeric wrapper.</summary>
public readonly record struct Bcd32
{
    /// <summary>Initializes a new instance of the <see cref="Bcd32"/> struct.</summary>
    /// <param name="value">The signed value.</param>
    public Bcd32(int value) => Value = value;

    /// <summary>Gets the numeric value.</summary>
    public int Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();
}
