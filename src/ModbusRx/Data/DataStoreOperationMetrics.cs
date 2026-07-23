// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.Data;
#else
namespace IoT.DriverCore.ModbusRx.Data;
#endif

/// <summary>Provides deterministic operation counters for data-store range operations.</summary>
public sealed class DataStoreOperationMetrics
{
    /// <summary>Initializes a new instance of the <see cref="DataStoreOperationMetrics"/> class.</summary>
    /// <param name="readOperations">The number of completed range reads.</param>
    /// <param name="writeOperations">The number of completed range writes.</param>
    /// <param name="elementCopies">The number of elements copied between data-store ranges and results.</param>
    /// <param name="resultCollectionAllocations">The number of result collections created for reads.</param>
    /// <param name="inputMaterializations">The number of non-indexable write inputs materialized once.</param>
    public DataStoreOperationMetrics(
        long readOperations,
        long writeOperations,
        long elementCopies,
        long resultCollectionAllocations,
        long inputMaterializations)
    {
        ReadOperations = readOperations;
        WriteOperations = writeOperations;
        ElementCopies = elementCopies;
        ResultCollectionAllocations = resultCollectionAllocations;
        InputMaterializations = inputMaterializations;
    }

    /// <summary>Gets the number of completed range reads.</summary>
    public long ReadOperations { get; }

    /// <summary>Gets the number of completed range writes.</summary>
    public long WriteOperations { get; }

    /// <summary>Gets the number of elements copied by range operations.</summary>
    public long ElementCopies { get; }

    /// <summary>Gets the number of result collections created by reads.</summary>
    public long ResultCollectionAllocations { get; }

    /// <summary>Gets the number of non-indexable write inputs materialized exactly once.</summary>
    public long InputMaterializations { get; }
}
