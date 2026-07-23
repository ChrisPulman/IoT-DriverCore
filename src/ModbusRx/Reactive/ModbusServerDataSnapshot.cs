// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive;
#else
namespace IoT.DriverCore.ModbusRx;
#endif

/// <summary>Represents a snapshot of Modbus server data at a point in time.</summary>
public sealed class ModbusServerDataSnapshot : IEquatable<ModbusServerDataSnapshot>
{
#if NETFRAMEWORK
    /// <summary>Multiplier used by the .NET Framework hash accumulator.</summary>
    private const int HashMultiplier = 23;
#endif

    /// <summary>Initializes a new instance of the <see cref="ModbusServerDataSnapshot"/> class.</summary>
    public ModbusServerDataSnapshot()
        : this([], [], [], [], DateTimeOffset.MinValue)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ModbusServerDataSnapshot"/> class.</summary>
    /// <param name="holdingRegisters">The holding-register values.</param>
    /// <param name="inputRegisters">The input-register values.</param>
    /// <param name="coils">The coil values.</param>
    /// <param name="inputs">The input values.</param>
    /// <param name="timestamp">The time at which the snapshot was captured.</param>
    public ModbusServerDataSnapshot(
        ushort[] holdingRegisters,
        ushort[] inputRegisters,
        bool[] coils,
        bool[] inputs,
        DateTimeOffset timestamp)
    {
        HoldingRegisters = Array.AsReadOnly((ushort[])holdingRegisters.Clone());
        InputRegisters = Array.AsReadOnly((ushort[])inputRegisters.Clone());
        Coils = Array.AsReadOnly((bool[])coils.Clone());
        Inputs = Array.AsReadOnly((bool[])inputs.Clone());
        Timestamp = timestamp;
    }

    /// <summary>Gets the holding registers data.</summary>
    public IReadOnlyList<ushort> HoldingRegisters { get; }

    /// <summary>Gets the input registers data.</summary>
    public IReadOnlyList<ushort> InputRegisters { get; }

    /// <summary>Gets the coils data.</summary>
    public IReadOnlyList<bool> Coils { get; }

    /// <summary>Gets the inputs data.</summary>
    public IReadOnlyList<bool> Inputs { get; }

    /// <summary>Gets the timestamp of this snapshot.</summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>Gets a value indicating whether this snapshot is empty.</summary>
    public bool IsEmpty => HoldingRegisters.Count == 0 && InputRegisters.Count == 0 &&
                           Coils.Count == 0 && Inputs.Count == 0;

    /// <summary>Determines whether two snapshots are equal.</summary>
    /// <param name="left">The first snapshot to compare.</param>
    /// <param name="right">The second snapshot to compare.</param>
    /// <returns>True if the snapshots are equal; otherwise, false.</returns>
    public static bool operator ==(ModbusServerDataSnapshot? left, ModbusServerDataSnapshot? right) =>
        Equals(left, right);

    /// <summary>Determines whether two snapshots are not equal.</summary>
    /// <param name="left">The first snapshot to compare.</param>
    /// <param name="right">The second snapshot to compare.</param>
    /// <returns>True if the snapshots are not equal; otherwise, false.</returns>
    public static bool operator !=(ModbusServerDataSnapshot? left, ModbusServerDataSnapshot? right) =>
        !Equals(left, right);

    /// <summary>Determines whether the specified snapshot is equal to the current snapshot.</summary>
    /// <param name="other">The snapshot to compare with the current snapshot.</param>
    /// <returns>True if the specified snapshot is equal to the current snapshot; otherwise, false.</returns>
    public bool Equals(ModbusServerDataSnapshot? other)
    {
        if (other is null)
        {
            return false;
        }

        return ReferenceEquals(this, other) ? true : ArraysEqual(HoldingRegisters, other.HoldingRegisters) &&
               ArraysEqual(InputRegisters, other.InputRegisters) &&
               ArraysEqual(Coils, other.Coils) &&
               ArraysEqual(Inputs, other.Inputs);
    }

    /// <summary>Determines whether the specified object is equal to the current snapshot.</summary>
    /// <param name="obj">The object to compare with the current snapshot.</param>
    /// <returns>True if the specified object is equal to the current snapshot; otherwise, false.</returns>
    public override bool Equals(object? obj) => Equals(obj as ModbusServerDataSnapshot);

    /// <summary>Returns the hash code for this snapshot.</summary>
    /// <returns>A hash code for this snapshot.</returns>
    public override int GetHashCode()
    {
#if NETFRAMEWORK
        var hash = 17;
        AddValuesToHash(ref hash, HoldingRegisters);
        AddValuesToHash(ref hash, InputRegisters);
        AddValuesToHash(ref hash, Coils);
        AddValuesToHash(ref hash, Inputs);
        return hash;
#else
        var hash = default(HashCode);
        AddValuesToHash(ref hash, HoldingRegisters);
        AddValuesToHash(ref hash, InputRegisters);
        AddValuesToHash(ref hash, Coils);
        AddValuesToHash(ref hash, Inputs);
        return hash.ToHashCode();
#endif
    }

    /// <summary>Executes the Arrays Equal operation.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="array1">The array1 value.</param>
    /// <param name="array2">The array2 value.</param>
    /// <returns>The result.</returns>
    private static bool ArraysEqual<T>(IReadOnlyList<T> array1, IReadOnlyList<T> array2)
        where T : IEquatable<T>
    {
        if (array1.Count != array2.Count)
        {
            return false;
        }

        for (var i = 0; i < array1.Count; i++)
        {
            if (!array1[i].Equals(array2[i]))
            {
                return false;
            }
        }

        return true;
    }

#if NETFRAMEWORK
    /// <summary>Adds the values to a .NET Framework hash accumulator.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="hash">The hash accumulator.</param>
    /// <param name="values">The values to add.</param>
    private static void AddValuesToHash<T>(ref int hash, IReadOnlyList<T> values)
    {
        for (var index = 0; index < values.Count; index++)
        {
            hash = (hash * HashMultiplier) + EqualityComparer<T>.Default.GetHashCode(values[index]);
        }
    }
#else
    /// <summary>Adds the values to a hash accumulator.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="hash">The hash accumulator.</param>
    /// <param name="values">The values to add.</param>
    private static void AddValuesToHash<T>(ref HashCode hash, IReadOnlyList<T> values)
    {
        for (var index = 0; index < values.Count; index++)
        {
            hash.Add(values[index]);
        }
    }
#endif
}
