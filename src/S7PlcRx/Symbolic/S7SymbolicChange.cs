// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
namespace IoT.Driver.S7PlcRx.Symbolic;
#endif

/// <summary>A value change or access error reported by the PLC.</summary>
public sealed class S7SymbolicChange
{
    /// <summary>Initializes a new instance of the <see cref="S7SymbolicChange"/> class.</summary>
    /// <param name="path">The path.</param>
    /// <param name="address">The address.</param>
    /// <param name="value">The value.</param>
    /// <param name="errorCode">The error code.</param>
    /// <param name="timestamp">The timestamp.</param>
    /// <param name="sequence">The sequence.</param>
    internal S7SymbolicChange(string path, S7SymbolicAddress address, S7SymbolicValue? value, byte errorCode, DateTimeOffset? timestamp, uint sequence)
    {
        Path = path;
        Address = address;
        Value = value;
        ErrorCode = errorCode;
        Timestamp = timestamp;
        Sequence = sequence;
    }

    /// <summary>Gets the subscribed path.</summary>
    /// <value>The stored value.</value>
    public string Path { get; }

    /// <summary>Gets the subscribed address.</summary>
    /// <value>The stored value.</value>
    public S7SymbolicAddress Address { get; }

    /// <summary>Gets the value when access succeeded.</summary>
    /// <value>The stored value.</value>
    public S7SymbolicValue? Value { get; }

    /// <summary>Gets the PLC quality or access status; zero means success.</summary>
    /// <value>The stored value.</value>
    public byte ErrorCode { get; }

    /// <summary>Gets the PLC timestamp when supplied by this controller.</summary>
    /// <value>The stored value.</value>
    public DateTimeOffset? Timestamp { get; }

    /// <summary>Gets the PLC notification sequence.</summary>
    /// <value>The stored value.</value>
    public uint Sequence { get; }
}
