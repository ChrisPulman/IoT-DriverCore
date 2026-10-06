// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
namespace IoT.Driver.S7PlcRx.Symbolic;
#endif

/// <summary>Contains a type identifier and its typed fields.</summary>
public sealed class S7SymbolicStruct
{
    /// <summary>Initializes a new instance of the <see cref="S7SymbolicStruct"/> class.</summary>
    /// <param name="typeId">The type id.</param>
    /// <param name="fields">The fields.</param>
    public S7SymbolicStruct(uint typeId, Dictionary<uint, S7SymbolicValue> fields)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(fields);
#else
        if (fields is null)
        {
            throw new ArgumentNullException(nameof(fields));
        }
#endif
        TypeId = typeId;
        Fields = new(fields);
    }

    /// <summary>Gets the structure type identifier.</summary>
    public uint TypeId { get; }

    /// <summary>Gets the field values indexed by local identifier.</summary>
    public Dictionary<uint, S7SymbolicValue> Fields { get; }
}
