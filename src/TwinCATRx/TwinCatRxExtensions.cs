// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
#if REACTIVE_SHIM
using CP.Collections.Reactive;
#else
using CP.Collections;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.TwinCATRx.Reactive;
#else
namespace IoT.DriverCore.TwinCATRx;
#endif

/// <summary>Observable TwinCAT extensions.</summary>
public static class TwinCatRxExtensions
{
    /// <summary>Stores the metadata key for a PLC variable name.</summary>
    private const string VariableTagKey = "Variable";

    /// <summary>Stores the delay before a structure is considered ready.</summary>
    private const int StructureReadyDelaySeconds = 2;

    /// <summary>Stores the first TwinCAT 3 ADS port.</summary>
    private const int TwinCat3Port = 851;

    /// <summary>Writes values from a cloned HashTableRx structure.</summary>
    /// <param name="hashTable">The HashTableRx instance.</param>
    /// <param name="setValues">The set values.</param>
    /// <returns>True if successful.</returns>
    [RequiresUnreferencedCode("May use reflection if the structure contains fields or properties.")]
    public static bool WriteValues(HashTableRx hashTable, Action<HashTableRx> setValues)
    {
        if (hashTable is null || setValues is null)
        {
            return false;
        }

        var plc = TwinCatStructureMetadata.GetClient(hashTable);
        if (plc is null || hashTable.Tag?[VariableTagKey] is not string variable)
        {
            return false;
        }

        using var clone = CreateClone(hashTable);
        setValues(clone);
        var structure = clone.Structure;
        if (structure is null)
        {
            return false;
        }

        plc.Write(variable, structure);
        return true;
    }

    /// <summary>Writes values asynchronously from a cloned HashTableRx structure.</summary>
    /// <param name="hashTable">The HashTableRx instance.</param>
    /// <param name="setValues">The set values.</param>
    /// <param name="time">The time to delay between writes.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation.</returns>
    [RequiresUnreferencedCode("May use reflection if the structure contains fields or properties.")]
    public static async Task<bool> WriteValuesAsync(
        HashTableRx hashTable,
        Action<HashTableRx> setValues,
        TimeSpan time)
    {
        if (hashTable is null || setValues is null)
        {
            return false;
        }

        var plc = TwinCatStructureMetadata.GetClient(hashTable);
        if (plc is null || hashTable.Tag?[VariableTagKey] is not string variable)
        {
            return false;
        }

        if (plc.IsPaused)
        {
            var completion = new TaskCompletionSource<bool>();
            using var subscription = ObservableBridgeExtensions.SubscribeTo(plc.IsPausedObservable, isPaused =>
            {
                if (isPaused)
                {
                    return;
                }

                _ = completion.TrySetResult(true);
            });
            _ = await completion.Task.ConfigureAwait(false);
        }
        else
        {
            plc.Pause(time);
        }

        using var clone = CreateClone(hashTable);
        setValues(clone);
        var structure = clone.Structure;
        if (structure is null)
        {
            return false;
        }

        plc.Write(variable, structure);
        return true;
    }

    /// <summary>Returns an observable that fires when the structure is ready.</summary>
    /// <param name="hashTable">The HashTableRx instance.</param>
    /// <returns>An observable when values have been set.</returns>
    public static IObservable<HashTableRx> StructureReady(HashTableRx hashTable)
    {
        if (hashTable is null)
        {
            throw new ArgumentNullException(nameof(hashTable));
        }

        return hashTable.ObserveAll
            .Where(_ => hashTable.Count > 0)
            .Take(1)
            .Delay(TimeSpan.FromSeconds(StructureReadyDelaySeconds))
            .Select(_ => hashTable);
    }

    /// <summary>Clones the specified HashTableRx.</summary>
    /// <param name="hashTable">The HashTableRx instance.</param>
    /// <returns>A HashTableRx.</returns>
    [RequiresUnreferencedCode("May use reflection if the structure contains fields or properties.")]
    public static HashTableRx CreateClone(HashTableRx hashTable)
    {
        if (hashTable is null)
        {
            throw new ArgumentNullException(nameof(hashTable));
        }

        var clone = new HashTableRx(hashTable.UseUpperCase);
        var structure = hashTable.Structure;
        if (structure is not null)
        {
            clone.SetStructure(structure);
        }

        return clone;
    }

    /// <summary>Observes the specified variable.</summary>
    /// <typeparam name="T">The converted tag value type.</typeparam>
    /// <param name="client">The reactive TwinCAT client.</param>
    /// <param name="variable">The PLC variable name.</param>
    /// <param name="converter">The native value converter.</param>
    /// <returns>The converted tag value sequence.</returns>
    public static IObservable<T> Observe<T>(
        IRxTcAdsClient client,
        string variable,
        Func<object?, T> converter) =>
        client.DataReceived
            .Where(x => string.Equals(x.Variable, variable, StringComparison.OrdinalIgnoreCase) && x.Data is not null)
            .Select(x => converter(x.Data));

    /// <summary>Observes the specified variable and identifier.</summary>
    /// <typeparam name="T">The converted tag value type.</typeparam>
    /// <param name="client">The reactive TwinCAT client.</param>
    /// <param name="variable">The PLC variable name.</param>
    /// <param name="id">The correlation identifier.</param>
    /// <param name="converter">The native value converter.</param>
    /// <returns>The correlated converted tag value sequence.</returns>
    public static IObservable<T> Observe<T>(
        IRxTcAdsClient client,
        string variable,
        string id,
        Func<object?, T> converter) =>
        client.DataReceived
            .Where(x => string.Equals(x.Id, id, StringComparison.Ordinal) &&
                string.Equals(x.Variable, variable, StringComparison.OrdinalIgnoreCase) &&
                x.Data is not null)
            .Select(x => converter(x.Data));

    /// <summary>Observes the specified variable as an async observable.</summary>
    /// <typeparam name="T">The converted tag value type.</typeparam>
    /// <param name="client">The reactive TwinCAT client.</param>
    /// <param name="variable">The PLC variable name.</param>
    /// <param name="converter">The native value converter.</param>
    /// <returns>The converted asynchronous tag value sequence.</returns>
    public static IObservableAsync<T> ObserveAsyncObservable<T>(
        IRxTcAdsClient client,
        string variable,
        Func<object?, T> converter) =>
        ObservableBridgeExtensions.ToAsyncObservable(Observe(client, variable, converter));

    /// <summary>Observes the specified variable and identifier as an async observable.</summary>
    /// <typeparam name="T">The converted tag value type.</typeparam>
    /// <param name="client">The reactive TwinCAT client.</param>
    /// <param name="variable">The PLC variable name.</param>
    /// <param name="id">The correlation identifier.</param>
    /// <param name="converter">The native value converter.</param>
    /// <returns>The correlated converted asynchronous tag value sequence.</returns>
    public static IObservableAsync<T> ObserveAsyncObservable<T>(
        IRxTcAdsClient client,
        string variable,
        string id,
        Func<object?, T> converter) =>
        ObservableBridgeExtensions.ToAsyncObservable(Observe(client, variable, id, converter));

    /// <summary>Creates the structure.</summary>
    /// <param name="client">The reactive TwinCAT client.</param>
    /// <param name="variable">The variable.</param>
    /// <returns>A HashTableRx with a link to the PLC.</returns>
    [RequiresUnreferencedCode("HashTableRx.SetStructure may use reflection over fields and properties.")]
    public static HashTableRx? CreateStruct(IRxTcAdsClient client, string variable)
    {
        if (client is null)
        {
            return default;
        }

        var table = new TwinCatStructureTable(client.Settings?.Port < TwinCat3Port);
        table.Tag.Add(nameof(IRxTcAdsClient), client);
        table.Tag.Add(nameof(RxTcAdsClient), client);
        table.Tag.Add(VariableTagKey, variable);
        table.SetSourceSubscription(ObservableBridgeExtensions.SubscribeTo(
            client.DataReceived.Where(
                x => string.Equals(x.Variable, variable, StringComparison.OrdinalIgnoreCase) && x.Data is not null),
            x => table.SetStructure(x.Data)));
        return table;
    }
}
