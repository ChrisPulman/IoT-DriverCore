// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
namespace IoT.Driver.S7PlcRx.Symbolic;
#endif

/// <summary>Contains a value or the PLC error returned for a symbolic read.</summary>
public sealed class S7SymbolicReadResult
{
    /// <summary>Initializes a new instance of the <see cref="S7SymbolicReadResult"/> class.</summary>
    /// <param name="address">The address.</param>
    /// <param name="value">The value.</param>
    /// <param name="errorCode">The error code.</param>
    public S7SymbolicReadResult(S7SymbolicAddress address, S7SymbolicValue? value, ulong errorCode)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(address);
#else
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }
#endif
        Address = address;
        Value = value;
        ErrorCode = errorCode;
    }

    /// <summary>Gets the requested address.</summary>
    public S7SymbolicAddress Address { get; }

    /// <summary>Gets the returned value.</summary>
    public S7SymbolicValue? Value { get; }

    /// <summary>Gets the PLC error code.</summary>
    public ulong ErrorCode { get; }

    /// <summary>Gets whether the PLC reported success.</summary>
    public bool IsSuccess => ErrorCode == 0;
}
