// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
namespace IoT.Driver.S7PlcRx.Symbolic;
#endif

/// <summary>Contains a system structure transmitted as packed bytes.</summary>
public sealed class S7SymbolicPackedStruct
{
    /// <summary>Initializes a new instance of the <see cref="S7SymbolicPackedStruct"/> class.</summary>
    /// <param name="typeId">The type id.</param>
    /// <param name="interfaceTimestamp">The interface timestamp.</param>
    /// <param name="transportFlags">The transport flags.</param>
    /// <param name="data">The data.</param>
    public S7SymbolicPackedStruct(uint typeId, ulong interfaceTimestamp, uint transportFlags, byte[] data)
        : this(typeId, interfaceTimestamp, transportFlags, data, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="S7SymbolicPackedStruct"/> class.</summary>
    /// <param name="typeId">The type id.</param>
    /// <param name="interfaceTimestamp">The interface timestamp.</param>
    /// <param name="transportFlags">The transport flags.</param>
    /// <param name="data">The data.</param>
    /// <param name="firstLength">The first length.</param>
    public S7SymbolicPackedStruct(uint typeId, ulong interfaceTimestamp, uint transportFlags, byte[] data, uint? firstLength)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(data);
#else
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }
#endif
        TypeId = typeId;
        InterfaceTimestamp = interfaceTimestamp;
        TransportFlags = transportFlags;
        Data = (byte[])data.Clone();
        FirstLength = firstLength ?? (uint)data.Length;
    }

    /// <summary>Gets the system type identifier.</summary>
    public uint TypeId { get; }

    /// <summary>Gets the interface timestamp.</summary>
    public ulong InterfaceTimestamp { get; }

    /// <summary>Gets the transport flags.</summary>
    public uint TransportFlags { get; }

    /// <summary>Gets the first transmitted length.</summary>
    public uint FirstLength { get; }

    /// <summary>Gets the packed payload.</summary>
    public byte[] Data { get; }
}
