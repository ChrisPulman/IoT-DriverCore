// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using IoT.DriverCore.ModbusRx.Reactive.Data;
#else
using IoT.DriverCore.ModbusRx.Data;
#endif
#if REACTIVE_SHIM
using IoT.DriverCore.ModbusRx.Reactive.Device;
#else
using IoT.DriverCore.ModbusRx.Device;
#endif
#if REACTIVE_SHIM
using IoT.DriverCore.ModbusRx.Reactive.Utility;
#else
using IoT.DriverCore.ModbusRx.Utility;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive;
#else
namespace IoT.DriverCore.ModbusRx;
#endif

/// <summary>Enhanced reactive extensions for ModbusServer with performance optimizations.</summary>
public static class EnhancedModbusServerExtensions
{
    /// <summary>Observes data-store writes without polling or elapsed-time dependencies.</summary>
    /// <param name="server">The extension receiver.</param>
    /// <returns>An observable that emits one snapshot for each completed data-store write.</returns>
    public static IObservable<ModbusServerDataSnapshot> ObserveDataChangesEventDriven(ModbusServer server) =>
        ObserveDataChangesEventDriven(server, TimeProvider.System, null);

    /// <summary>Observes data-store writes without polling or elapsed-time dependencies.</summary>
    /// <param name="server">The extension receiver.</param>
    /// <param name="timeProvider">The time provider used for snapshot timestamps.</param>
    /// <param name="metrics">Optional deterministic observation counters.</param>
    /// <returns>An observable that emits one snapshot for each completed data-store write.</returns>
    public static IObservable<ModbusServerDataSnapshot> ObserveDataChangesEventDriven(
        ModbusServer server,
        TimeProvider? timeProvider,
        ModbusObservationMetrics? metrics)
    {
        if (server is null)
        {
            throw new ArgumentNullException(nameof(server));
        }

        var dataStore = server.DataStore ?? throw new InvalidOperationException("The server data store is not initialized.");
        var snapshotTimeProvider = timeProvider ?? TimeProvider.System;

        return Observable.Create<ModbusServerDataSnapshot>(observer =>
        {
            void OnDataStoreWritten(object? sender, DataStoreEventArgs eventArgs)
            {
                try
                {
                    _ = sender;
                    metrics?.RecordWriteNotification(eventArgs);
                    var snapshot = CreateSnapshot(server, snapshotTimeProvider);
                    metrics?.RecordSnapshotCreated();
                    observer.OnNext(snapshot);
                    metrics?.RecordSnapshotEmitted();
                }
                catch (Exception exception)
                {
                    observer.OnError(exception);
                }
            }

            dataStore.DataStoreWrittenTo += OnDataStoreWritten;
            return Disposable.Create(() => dataStore.DataStoreWrittenTo -= OnDataStoreWritten);
        });
    }

    /// <summary>Observes data changes in the server with high-performance optimizations.</summary>
    /// <param name="server">The extension receiver.</param>
    /// <param name="interval">The observation interval in milliseconds.</param>
    /// <returns>An observable of data changes.</returns>
    public static IObservable<ModbusServerDataSnapshot> ObserveDataChangesOptimized(ModbusServer server, int interval) =>
        ObserveDataChangesOptimized(server, interval, TimeProvider.System);

    /// <summary>Observes data changes in the server with high-performance optimizations.</summary>
    /// <param name="server">The extension receiver.</param>
    /// <param name="interval">The observation interval in milliseconds.</param>
    /// <param name="timeProvider">The time provider used for snapshot timestamps.</param>
    /// <returns>An observable of data changes.</returns>
    public static IObservable<ModbusServerDataSnapshot> ObserveDataChangesOptimized(
        ModbusServer server,
        int interval,
        TimeProvider? timeProvider)
    {
        if (server is null)
        {
            throw new ArgumentNullException(nameof(server));
        }

        var snapshotTimeProvider = timeProvider ?? TimeProvider.System;

        return Observable.Create<ModbusServerDataSnapshot>(observer =>
        {
            var disposables = new CompositeDisposable();
            var lastSnapshot = new ModbusServerDataSnapshot();
            var hasChanged = false;

            // Subscribe to data store events for immediate change detection
            if (server.DataStore is not null)
            {
                var writeSubscription = Observable.FromEventPattern<DataStoreEventArgs>(
                    handler => server.DataStore.DataStoreWrittenTo += handler,
                    handler => server.DataStore.DataStoreWrittenTo -= handler)
                    .Select(pattern => pattern.EventArgs)
                    .Subscribe(_ => hasChanged = true);

                disposables.Add(writeSubscription);
            }

            // Periodic snapshot with change detection
            var timerSubscription = Observable.Interval(TimeSpan.FromMilliseconds(interval))
                .Where(_ => hasChanged || lastSnapshot.IsEmpty)
                .Select(_ => CreateSnapshot(server, snapshotTimeProvider))
                .Where(snapshot => !snapshot.Equals(lastSnapshot))
                .Subscribe(snapshot =>
                {
                    lastSnapshot = snapshot;
                    hasChanged = false;
                    observer.OnNext(snapshot);
                });

            disposables.Add(timerSubscription);

            return disposables;
        });
    }

    /// <summary>Observes holding register changes with range filtering.</summary>
    /// <param name="server">The extension receiver.</param>
    /// <param name="startAddress">The start address to observe.</param>
    /// <param name="count">The number of registers to observe.</param>
    /// <param name="interval">The observation interval in milliseconds.</param>
    /// <returns>An observable of register values.</returns>
    public static IObservable<ushort[]> ObserveHoldingRegistersOptimized(
        ModbusServer server,
        ushort startAddress,
        ushort count,
        int interval)
    {
        if (server is null)
        {
            throw new ArgumentNullException(nameof(server));
        }

        return Observable.Create<ushort[]>(observer =>
        {
            var lastValues = new ushort[count];
            var hasChanged = false;

            var disposables = new CompositeDisposable();

            // Subscribe to relevant data store changes
            if (server.DataStore is not null)
            {
                var writeSubscription = Observable.FromEventPattern<DataStoreEventArgs>(
                    handler => server.DataStore.DataStoreWrittenTo += handler,
                    handler => server.DataStore.DataStoreWrittenTo -= handler)
                    .Select(pattern => pattern.EventArgs)
                    .Where(args => IsAddressInRange(args.StartAddress, GetDataLength(args), startAddress, count))
                    .Subscribe(_ => hasChanged = true);

                disposables.Add(writeSubscription);
            }

            // Periodic observation with change detection
            var timerSubscription = Observable.Interval(TimeSpan.FromMilliseconds(interval))
                .Where(_ => hasChanged || IsArrayEmpty(lastValues))
                .Subscribe(_ =>
                {
                    try
                    {
                        var currentValues = server.DataStore is { } dataStore
                            ? DataStoreExtensions.ReadHoldingRegistersOptimized(dataStore, startAddress, count)
                            : new ushort[count];

                        if (!ArraysEqual(currentValues, lastValues))
                        {
                            Array.Copy(currentValues, lastValues, count);
                            hasChanged = false;
                            observer.OnNext(currentValues);
                        }
                    }
                    catch (Exception ex)
                    {
                        observer.OnError(ex);
                    }
                });

            disposables.Add(timerSubscription);

            return disposables;
        });
    }

    /// <summary>Observes coil changes with range filtering.</summary>
    /// <param name="server">The extension receiver.</param>
    /// <param name="startAddress">The start address to observe.</param>
    /// <param name="count">The number of coils to observe.</param>
    /// <param name="interval">The observation interval in milliseconds.</param>
    /// <returns>An observable of coil values.</returns>
    public static IObservable<bool[]> ObserveCoilsOptimized(
        ModbusServer server,
        ushort startAddress,
        ushort count,
        int interval)
    {
        if (server is null)
        {
            throw new ArgumentNullException(nameof(server));
        }

        return Observable.Create<bool[]>(observer =>
        {
            var lastValues = new bool[count];
            var hasChanged = false;

            var disposables = new CompositeDisposable();

            // Subscribe to relevant data store changes
            if (server.DataStore is not null)
            {
                var writeSubscription = Observable.FromEventPattern<DataStoreEventArgs>(
                    handler => server.DataStore.DataStoreWrittenTo += handler,
                    handler => server.DataStore.DataStoreWrittenTo -= handler)
                    .Select(pattern => pattern.EventArgs)
                    .Where(args => IsAddressInRange(args.StartAddress, GetDataLength(args), startAddress, count))
                    .Subscribe(_ => hasChanged = true);

                disposables.Add(writeSubscription);
            }

            // Periodic observation with change detection
            var timerSubscription = Observable.Interval(TimeSpan.FromMilliseconds(interval))
                .Where(_ => hasChanged || IsArrayEmpty(lastValues))
                .Subscribe(_ =>
                {
                    try
                    {
                        var currentValues = server.DataStore is { } dataStore
                            ? DataStoreExtensions.ReadCoilsOptimized(dataStore, startAddress, count)
                            : new bool[count];

                        if (!ArraysEqual(currentValues, lastValues))
                        {
                            Array.Copy(currentValues, lastValues, count);
                            hasChanged = false;
                            observer.OnNext(currentValues);
                        }
                    }
                    catch (Exception ex)
                    {
                        observer.OnError(ex);
                    }
                });

            disposables.Add(timerSubscription);

            return disposables;
        });
    }

    /// <summary>Creates a buffered observable with change detection and batching.</summary>
    /// <param name="server">The extension receiver.</param>
    /// <param name="bufferSize">The buffer size for batching changes.</param>
    /// <param name="bufferTimeMilliseconds">The buffer time window in milliseconds.</param>
    /// <returns>An observable of batched data changes.</returns>
    public static IObservable<ModbusServerDataSnapshot[]> ObserveDataChangesBuffered(
        ModbusServer server,
        int bufferSize,
        int bufferTimeMilliseconds) =>
        ObserveDataChangesBuffered(server, bufferSize, bufferTimeMilliseconds, TimeProvider.System);

    /// <summary>Creates a buffered observable with change detection and batching.</summary>
    /// <param name="server">The extension receiver.</param>
    /// <param name="bufferSize">The buffer size for batching changes.</param>
    /// <param name="bufferTimeMilliseconds">The buffer time window in milliseconds.</param>
    /// <param name="timeProvider">The time provider used for snapshot timestamps.</param>
    /// <returns>An observable of batched data changes.</returns>
    public static IObservable<ModbusServerDataSnapshot[]> ObserveDataChangesBuffered(
        ModbusServer server,
        int bufferSize,
        int bufferTimeMilliseconds,
        TimeProvider? timeProvider)
    {
        if (server is null)
        {
            throw new ArgumentNullException(nameof(server));
        }

        return ObserveDataChangesOptimized(server, OneHundred, timeProvider)
            .Buffer(bufferSize)
            .Select(CopyBufferedSnapshots)
            .Where(buffer => buffer.Length > 0);
    }

    /// <summary>Executes the Create Snapshot operation.</summary>
    /// <param name="server">The server value.</param>
    /// <param name="timeProvider">The time provider value.</param>
    /// <returns>The result.</returns>
    private static ModbusServerDataSnapshot CreateSnapshot(ModbusServer server, TimeProvider timeProvider)
    {
        try
        {
            var dataStore = server.DataStore;
            if (dataStore is null)
            {
                return new ModbusServerDataSnapshot();
            }

            // Read a reasonable range of data for snapshot
            const ushort maxRegisters = 100;
            const ushort maxCoils = 100;

            var holdingRegisters = DataStoreExtensions.ReadHoldingRegistersOptimized(
                dataStore,
                1,
                Math.Min(maxRegisters, (ushort)Math.Max(1, dataStore.HoldingRegisters.Count - 1)));
            var inputRegisters = DataStoreExtensions.ReadInputRegistersOptimized(
                dataStore,
                1,
                Math.Min(maxRegisters, (ushort)Math.Max(1, dataStore.InputRegisters.Count - 1)));
            var coils = DataStoreExtensions.ReadCoilsOptimized(
                dataStore,
                1,
                Math.Min(maxCoils, (ushort)Math.Max(1, dataStore.CoilDiscretes.Count - 1)));
            var inputs = DataStoreExtensions.ReadInputsOptimized(
                dataStore,
                1,
                Math.Min(maxCoils, (ushort)Math.Max(1, dataStore.InputDiscretes.Count - 1)));

            return new ModbusServerDataSnapshot(
                holdingRegisters,
                inputRegisters,
                coils,
                inputs,
                timeProvider.GetUtcNow());
        }
        catch
        {
            return new ModbusServerDataSnapshot();
        }
    }

    /// <summary>Executes the Get Data Length operation.</summary>
    /// <param name="args">The args value.</param>
    /// <returns>The result.</returns>
    private static int GetDataLength(DataStoreEventArgs args)
    {
        return args.Data is null ? 0 : args.Data.Option switch
        {
            DiscriminatedUnionOption.A => args.Data.A?.Count ?? 0,
            DiscriminatedUnionOption.B => args.Data.B?.Count ?? 0,
            _ => 0
        };
    }

    /// <summary>Executes the Is Address In Range operation.</summary>
    /// <param name="startAddress">The start Address value.</param>
    /// <param name="length">The length value.</param>
    /// <param name="observeStart">The observe Start value.</param>
    /// <param name="observeCount">The observe Count value.</param>
    /// <returns>The result.</returns>
    private static bool IsAddressInRange(ushort startAddress, int length, ushort observeStart, ushort observeCount)
    {
        var endAddress = startAddress + length - 1;
        var observeEnd = observeStart + observeCount - 1;

        return !(endAddress < observeStart || startAddress > observeEnd);
    }

    /// <summary>Executes the Arrays Equal operation.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="array1">The array1 value.</param>
    /// <param name="array2">The array2 value.</param>
    /// <returns>The result.</returns>
    private static bool ArraysEqual<T>(T[] array1, T[] array2)
        where T : IEquatable<T>
    {
        if (array1.Length != array2.Length)
        {
            return false;
        }

        for (var i = 0; i < array1.Length; i++)
        {
            if (!array1[i].Equals(array2[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Executes the Is Array Empty operation.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="array">The array value.</param>
    /// <returns>The result.</returns>
    private static bool IsArrayEmpty<T>(T[] array)
        where T : struct
    {
        for (var i = 0; i < array.Length; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(array[i], default))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Copies buffered snapshots into an array.</summary>
    /// <param name="snapshots">The buffered snapshots.</param>
    /// <returns>The copied snapshot array.</returns>
    private static ModbusServerDataSnapshot[] CopyBufferedSnapshots(IList<ModbusServerDataSnapshot> snapshots)
    {
        var result = new ModbusServerDataSnapshot[snapshots.Count];
        for (var i = 0; i < snapshots.Count; i++)
        {
            result[i] = snapshots[i];
        }

        return result;
    }
}
