// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using IoT.Driver.S7PlcRx.Reactive.Symbolic.Protocol;
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
using IoT.Driver.S7PlcRx.Symbolic.Protocol;
namespace IoT.Driver.S7PlcRx.Symbolic;
#endif

/// <summary>Identifies a symbolic value through its access area and local identifier path.</summary>
public sealed class S7SymbolicAddress
{
    /// <summary>Provides local ids.</summary>
    private readonly uint[] _localIds;

    /// <summary>Initializes a new instance of the <see cref="S7SymbolicAddress"/> class.</summary>
    /// <param name="accessArea">The access area.</param>
    /// <param name="subArea">The sub area.</param>
    /// <param name="localIds">The local ids.</param>
    public S7SymbolicAddress(uint accessArea, uint subArea, uint[] localIds)
        : this(accessArea, subArea, localIds, 0)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="S7SymbolicAddress"/> class.</summary>
    /// <param name="accessArea">The access area.</param>
    /// <param name="subArea">The sub area.</param>
    /// <param name="localIds">The local ids.</param>
    /// <param name="symbolCrc">The symbol crc.</param>
    public S7SymbolicAddress(uint accessArea, uint subArea, uint[] localIds, uint symbolCrc)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(localIds);
#else
        if (localIds is null)
        {
            throw new ArgumentNullException(nameof(localIds));
        }
#endif
        if (localIds.Length > 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(localIds));
        }

        AccessArea = accessArea;
        SubArea = subArea;
        _localIds = (uint[])localIds.Clone();
        SymbolCrc = symbolCrc;
    }

    /// <summary>Gets the access area.</summary>
    public uint AccessArea { get; }

    /// <summary>Gets the subarea.</summary>
    public uint SubArea { get; }

    /// <summary>Gets the local identifier path.</summary>
    public uint[] LocalIds => _localIds;

    /// <summary>Gets the symbol checksum.</summary>
    public uint SymbolCrc { get; }

    /// <summary>Writes to.</summary>
    /// <param name="writer">The writer.</param>
    internal void WriteTo(PlusWriter writer)
    {
        writer.VarUInt32(SymbolCrc);
        writer.VarUInt32(AccessArea);
        writer.VarUInt32((uint)_localIds.Length + 1);
        writer.VarUInt32(SubArea);
        foreach (var id in _localIds)
        {
            writer.VarUInt32(id);
        }
    }
}
