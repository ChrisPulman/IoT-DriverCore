// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.IO;
#else
namespace IoT.DriverCore.ModbusRx.IO;
#endif

/// <summary>Provides deterministic operation counters for a <see cref="ModbusBufferManager"/>.</summary>
public sealed class ModbusBufferMetrics
{
    /// <summary>Initializes a new instance of the <see cref="ModbusBufferMetrics"/> class.</summary>
    /// <param name="rentOperations">The number of successful rents.</param>
    /// <param name="returnOperations">The number of successful returns.</param>
    /// <param name="dedicatedAllocations">The number of arrays allocated instead of rented.</param>
    /// <param name="copyOperations">The number of tracked copy operations.</param>
    /// <param name="copiedElements">The number of elements copied by tracked operations.</param>
    public ModbusBufferMetrics(
        long rentOperations,
        long returnOperations,
        long dedicatedAllocations,
        long copyOperations,
        long copiedElements)
    {
        RentOperations = rentOperations;
        ReturnOperations = returnOperations;
        DedicatedAllocations = dedicatedAllocations;
        CopyOperations = copyOperations;
        CopiedElements = copiedElements;
    }

    /// <summary>Gets the number of successful rents.</summary>
    public long RentOperations { get; }

    /// <summary>Gets the number of successful returns.</summary>
    public long ReturnOperations { get; }

    /// <summary>Gets the number of arrays allocated instead of rented.</summary>
    public long DedicatedAllocations { get; }

    /// <summary>Gets the number of tracked copy operations.</summary>
    public long CopyOperations { get; }

    /// <summary>Gets the number of elements copied by tracked copy operations.</summary>
    public long CopiedElements { get; }
}
