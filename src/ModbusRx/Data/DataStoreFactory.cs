// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ModbusRx.Reactive.Data;
#else
namespace ModbusRx.Data;
#endif

/// <summary>Data story factory.</summary>
public static class DataStoreFactory
{
    /// <summary>Creates a default data store with zeroed registers and false discrete values.</summary>
    /// <returns>A DataStore.</returns>
    public static DataStore CreateDefaultDataStore() =>
        CreateDefaultDataStore(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue);

    /// <summary>Creates a default data store with zeroed registers and false discrete values.</summary>
    /// <param name="coilsCount">Number of discrete coils.</param>
    /// <param name="inputsCount">Number of discrete inputs.</param>
    /// <param name="holdingRegistersCount">Number of holding registers.</param>
    /// <param name="inputRegistersCount">Number of input registers.</param>
    /// <returns>New instance of Data store with defined inputs/outputs.</returns>
    public static DataStore CreateDefaultDataStore(
        ushort coilsCount,
        ushort inputsCount,
        ushort holdingRegistersCount,
        ushort inputRegistersCount)
    {
        var coils = new bool[coilsCount];
        var inputs = new bool[inputsCount];
        var holdingRegs = new ushort[holdingRegistersCount];
        var inputRegs = new ushort[inputRegistersCount];

        return new DataStore(coils, inputs, holdingRegs, inputRegs);
    }

    /// <summary>Factory method for test data store.</summary>
    /// <returns>The result.</returns>
    internal static DataStore CreateTestDataStore()
    {
        var dataStore = new DataStore();

        for (var i = 1; i < ThreeThousand; i++)
        {
            var value = i % Two > 0;
            dataStore.CoilDiscretes.Add(value);
            dataStore.InputDiscretes.Add(!value);
            dataStore.HoldingRegisters.Add((ushort)i);
            dataStore.InputRegisters.Add((ushort)(i * Ten));
        }

        return dataStore;
    }
}
