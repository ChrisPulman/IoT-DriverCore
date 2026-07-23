// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.S7PlcRx.Mock;

/// <summary>Represents one queued, single-use simulator fault.</summary>
public sealed class S7ServerFault
{
    /// <summary>Initializes a new instance of the <see cref="S7ServerFault"/> class.</summary>
    /// <param name="kind">The fault behavior.</param>
    public S7ServerFault(S7ServerFaultKind kind)
        : this(kind, S7ServerOperation.Any, TimeSpan.Zero, 0x05)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="S7ServerFault"/> class.</summary>
    /// <param name="kind">The fault behavior.</param>
    /// <param name="operation">The operation that consumes the fault.</param>
    public S7ServerFault(S7ServerFaultKind kind, S7ServerOperation operation)
        : this(kind, operation, TimeSpan.Zero, 0x05)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="S7ServerFault"/> class.</summary>
    /// <param name="kind">The fault behavior.</param>
    /// <param name="operation">The operation that consumes the fault.</param>
    /// <param name="delay">The delay used by <see cref="S7ServerFaultKind.Delay"/>.</param>
    /// <param name="returnCode">The S7 item code used by <see cref="S7ServerFaultKind.ReturnCode"/>.</param>
    public S7ServerFault(
        S7ServerFaultKind kind,
        S7ServerOperation operation,
        TimeSpan delay,
        byte returnCode)
    {
        Kind = kind;
        Operation = operation;
        Delay = delay;
        ReturnCode = returnCode;
    }

    /// <summary>Gets the fault behavior.</summary>
    public S7ServerFaultKind Kind { get; }

    /// <summary>Gets the matching operation.</summary>
    public S7ServerOperation Operation { get; }

    /// <summary>Gets the configured delay.</summary>
    public TimeSpan Delay { get; }

    /// <summary>Gets the configured S7 return code.</summary>
    public byte ReturnCode { get; }
}
