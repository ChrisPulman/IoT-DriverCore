// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
namespace IoT.Driver.S7PlcRx.Symbolic;
#endif

/// <summary>Pairs an address and a value for a symbolic write.</summary>
public sealed class S7SymbolicWriteItem
{
    /// <summary>Initializes a new instance of the <see cref="S7SymbolicWriteItem"/> class.</summary>
    /// <param name="address">The address.</param>
    /// <param name="value">The value.</param>
    public S7SymbolicWriteItem(S7SymbolicAddress address, S7SymbolicValue value)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(address);
#else
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }
#endif
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(value);
#else
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }
#endif
        Address = address;
        Value = value;
    }

    /// <summary>Gets the destination address.</summary>
    public S7SymbolicAddress Address { get; }

    /// <summary>Gets the value to write.</summary>
    public S7SymbolicValue Value { get; }
}
