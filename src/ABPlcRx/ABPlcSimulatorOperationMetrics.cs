// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace IoT.DriverCore.ABPlcRx.Reactive;
#else
namespace IoT.DriverCore.ABPlcRx;
#endif

/// <summary>Immutable, deterministic native-operation counts captured by an <see cref="ABPlcSimulator"/>.</summary>
public sealed class ABPlcSimulatorOperationMetrics
{
    /// <summary>Initializes a new instance of the <see cref="ABPlcSimulatorOperationMetrics"/> class.</summary>
    /// <param name="operations">The simulator operation snapshot.</param>
    internal ABPlcSimulatorOperationMetrics(IEnumerable<ABPlcSimulatorLogEntry> operations)
    {
        long totalOperations = 0;
        long createOperations = 0;
        long destroyOperations = 0;
        long readOperations = 0;
        long writeOperations = 0;
        long failedOperations = 0;
        foreach (var operation in operations)
        {
            totalOperations++;
            if (operation.StatusCode != PlcTagStatus.StatusOK)
            {
                failedOperations++;
            }

            switch (operation.Operation)
            {
                case ABPlcSimulatorOperation.Create:
                {
                    createOperations++;
                    break;
                }

                case ABPlcSimulatorOperation.Destroy:
                {
                    destroyOperations++;
                    break;
                }

                case ABPlcSimulatorOperation.Read:
                {
                    readOperations++;
                    break;
                }

                case ABPlcSimulatorOperation.Write:
                {
                    writeOperations++;
                    break;
                }

                default:
                {
                    break;
                }
            }
        }

        TotalOperations = totalOperations;
        CreateOperations = createOperations;
        DestroyOperations = destroyOperations;
        ReadOperations = readOperations;
        WriteOperations = writeOperations;
        FailedOperations = failedOperations;
    }

    /// <summary>Gets the number of native operations recorded.</summary>
    public long TotalOperations { get; }

    /// <summary>Gets the number of create operations.</summary>
    public long CreateOperations { get; }

    /// <summary>Gets the number of destroy operations.</summary>
    public long DestroyOperations { get; }

    /// <summary>Gets the number of native reads.</summary>
    public long ReadOperations { get; }

    /// <summary>Gets the number of native writes.</summary>
    public long WriteOperations { get; }

    /// <summary>Gets the number of non-success native operations.</summary>
    public long FailedOperations { get; }
}
