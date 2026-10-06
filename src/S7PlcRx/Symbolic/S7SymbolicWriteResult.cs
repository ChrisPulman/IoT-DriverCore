// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
namespace IoT.Driver.S7PlcRx.Symbolic;
#endif

/// <summary>Contains the PLC result of a symbolic write.</summary>
public sealed class S7SymbolicWriteResult
{
    /// <summary>Initializes a new instance of the <see cref="S7SymbolicWriteResult"/> class.</summary>
    /// <param name="address">The address.</param>
    /// <param name="errorCode">The error code.</param>
    public S7SymbolicWriteResult(S7SymbolicAddress address, ulong errorCode)
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
        ErrorCode = errorCode;
    }

    /// <summary>Gets the destination address.</summary>
    public S7SymbolicAddress Address { get; }

    /// <summary>Gets the PLC error code.</summary>
    public ulong ErrorCode { get; }

    /// <summary>Gets whether the PLC reported success.</summary>
    public bool IsSuccess => ErrorCode == 0;
}
