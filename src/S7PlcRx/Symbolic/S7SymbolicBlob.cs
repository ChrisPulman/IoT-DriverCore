// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
namespace IoT.Driver.S7PlcRx.Symbolic;
#endif

/// <summary>Contains an opaque S7Plus blob.</summary>
public sealed class S7SymbolicBlob
{
    /// <summary>Initializes a new instance of the <see cref="S7SymbolicBlob"/> class.</summary>
    /// <param name="rootId">The root id.</param>
    /// <param name="data">The data.</param>
    public S7SymbolicBlob(uint rootId, byte[] data)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(data);
#else
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }
#endif
        RootId = rootId;
        Data = (byte[])data.Clone();
    }

    /// <summary>Gets the blob root identifier.</summary>
    public uint RootId { get; }

    /// <summary>Gets the blob payload.</summary>
    public byte[] Data { get; }
}
