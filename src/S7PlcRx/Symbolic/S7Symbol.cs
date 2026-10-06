// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
namespace IoT.Driver.S7PlcRx.Symbolic;
#endif

/// <summary>Describes a PLC symbol and its native symbolic access path.</summary>
public sealed class S7Symbol
{
    /// <summary>The array lower bounds.</summary>
    private readonly IReadOnlyList<int> _lowerBounds;

    /// <summary>The array lengths.</summary>
    private readonly IReadOnlyList<uint> _lengths;

    /// <summary>Initializes a new instance of the <see cref="S7Symbol"/> class.</summary>
    /// <param name="path">The symbolic path.</param>
    /// <param name="address">The native address.</param>
    /// <param name="softDataType">The PLC soft datatype.</param>
    /// <param name="flags">The PLC symbol flags.</param>
    /// <param name="symbolCrc">The symbol checksum.</param>
    /// <param name="arrayMetadata">The array dimensions and maximum string length.</param>
    internal S7Symbol(
        string path,
        S7SymbolicAddress address,
        uint softDataType,
        ushort flags,
        uint symbolCrc,
        (int[] LowerBounds, uint[] Lengths, int MaximumStringLength) arrayMetadata)
    {
        Path = path;
        Address = address;
        SoftDataType = softDataType;
        IsReadOnly = (flags & 0x400) != 0;
        IsAccessible = (flags & 0x200) != 0;
        IsOptimized = (flags & 0x80) != 0;
        SymbolCrc = symbolCrc;
        _lowerBounds = Array.AsReadOnly((int[])arrayMetadata.LowerBounds.Clone());
        _lengths = Array.AsReadOnly((uint[])arrayMetadata.Lengths.Clone());
        MaximumStringLength = arrayMetadata.MaximumStringLength;
    }

    /// <summary>Gets the symbolic path.</summary>
    public string Path { get; }

    /// <summary>Gets the native access address.</summary>
    public S7SymbolicAddress Address { get; }

    /// <summary>Gets the PLC soft datatype.</summary>
    public uint SoftDataType { get; }

    /// <summary>Gets whether HMI writes are prohibited.</summary>
    public bool IsReadOnly { get; }

    /// <summary>Gets whether HMI access is allowed.</summary>
    public bool IsAccessible { get; }

    /// <summary>Gets whether optimized access is advertised.</summary>
    public bool IsOptimized { get; }

    /// <summary>Gets the symbol checksum advertised by the PLC.</summary>
    public uint SymbolCrc { get; }

    /// <summary>Gets the array lower bounds in path coordinate order.</summary>
    public IReadOnlyList<int> ArrayLowerBounds => _lowerBounds;

    /// <summary>Gets the array lengths in path coordinate order.</summary>
    public IReadOnlyList<uint> ArrayLengths => _lengths;

    /// <summary>Gets the maximum declared string length.</summary>
    public int MaximumStringLength { get; }
}
