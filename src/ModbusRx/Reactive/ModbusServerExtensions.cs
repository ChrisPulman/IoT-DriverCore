// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.ModbusRx.Reactive;
#else
namespace IoT.Driver.ModbusRx;
#endif

/// <summary>Reactive extensions for ModbusServer.</summary>
public static class ModbusServerExtensions
{
    /// <summary>Creates an observable stream of data changes from the server.</summary>
    /// <param name="server">The extension receiver.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable stream of server data.</returns>
    public static IObservable<(ushort[] HoldingRegisters, ushort[] InputRegisters, bool[] Coils, bool[] Inputs)>
        ObserveDataChanges(Device.ModbusServer server, double interval)
    {
        return Observable.CreateWithState<
            (ushort[] HoldingRegisters, ushort[] InputRegisters, bool[] Coils, bool[] Inputs),
            (Device.ModbusServer server, double interval)>((server, interval), static (state, observer) =>
        {
            var timer = Observable.Interval(TimeSpan.FromMilliseconds(state.interval))
                .Subscribe(_ =>
                {
                    try
                    {
                        var data = state.server.GetCurrentData();
                        observer.OnNext(data);
                    }
                    catch (Exception ex)
                    {
                        observer.OnError(ex);
                    }
                });

            return Disposable.Create(timer.Dispose);
        });
    }

    /// <summary>Observes changes to holding registers in the server data store.</summary>
    /// <param name="server">The extension receiver.</param>
    /// <param name="startAddress">The starting address to monitor.</param>
    /// <param name="count">The number of registers to monitor.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable stream of holding register values.</returns>
    public static IObservable<ushort[]> ObserveHoldingRegisters(
        Device.ModbusServer server,
        ushort startAddress,
        ushort count,
        double interval)
    {
        return ObserveDataChanges(server, interval)
            .Select(data => CopyRange(data.HoldingRegisters, startAddress, count))
            .DistinctUntilChanged(new ArrayEqualityComparer<ushort>());
    }

    /// <summary>Observes changes to input registers in the server data store.</summary>
    /// <param name="server">The extension receiver.</param>
    /// <param name="startAddress">The starting address to monitor.</param>
    /// <param name="count">The number of registers to monitor.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable stream of input register values.</returns>
    public static IObservable<ushort[]> ObserveInputRegisters(
        Device.ModbusServer server,
        ushort startAddress,
        ushort count,
        double interval)
    {
        return ObserveDataChanges(server, interval)
            .Select(data => CopyRange(data.InputRegisters, startAddress, count))
            .DistinctUntilChanged(new ArrayEqualityComparer<ushort>());
    }

    /// <summary>Observes changes to coils in the server data store.</summary>
    /// <param name="server">The extension receiver.</param>
    /// <param name="startAddress">The starting address to monitor.</param>
    /// <param name="count">The number of coils to monitor.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable stream of coil values.</returns>
    public static IObservable<bool[]> ObserveCoils(
        Device.ModbusServer server,
        ushort startAddress,
        ushort count,
        double interval)
    {
        return ObserveDataChanges(server, interval)
            .Select(data => CopyRange(data.Coils, startAddress, count))
            .DistinctUntilChanged(new ArrayEqualityComparer<bool>());
    }

    /// <summary>Observes changes to discrete inputs in the server data store.</summary>
    /// <param name="server">The extension receiver.</param>
    /// <param name="startAddress">The starting address to monitor.</param>
    /// <param name="count">The number of inputs to monitor.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable stream of discrete input values.</returns>
    public static IObservable<bool[]> ObserveDiscreteInputs(
        Device.ModbusServer server,
        ushort startAddress,
        ushort count,
        double interval)
    {
        return ObserveDataChanges(server, interval)
            .Select(data => CopyRange(data.Inputs, startAddress, count))
            .DistinctUntilChanged(new ArrayEqualityComparer<bool>());
    }

    /// <summary>Creates a reactive server that automatically starts and stops based on subscription.</summary>
    /// <param name="configureServer">Action to configure the server before starting.</param>
    /// <returns>An observable that represents the server lifecycle.</returns>
    public static IObservable<Device.ModbusServer> CreateReactiveServer(
        Action<Device.ModbusServer> configureServer)
    {
        return Observable.CreateWithState<Device.ModbusServer, Action<Device.ModbusServer>>(
            configureServer,
            static (configureServer, observer) =>
        {
            var server = new Device.ModbusServer();

            try
            {
                configureServer(server);
                server.Start();
                observer.OnNext(server);
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
                server.Dispose();
                return EmptyDisposable.Instance;
            }

            return new ServerSubscription(server);
        });
    }

    /// <summary>Copies a range from an array.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source values.</param>
    /// <param name="startAddress">The zero-based start address.</param>
    /// <param name="count">The requested count.</param>
    /// <returns>The copied range.</returns>
    private static T[] CopyRange<T>(T[] source, ushort startAddress, ushort count)
    {
        var start = (int)startAddress;
        if (start >= source.Length)
        {
            return [];
        }

        var length = Math.Min((int)count, source.Length - start);
        var result = new T[length];
        Array.Copy(source, start, result, 0, length);
        return result;
    }

    /// <summary>Stops and disposes a server when an owning subscription ends.</summary>
    /// <param name="server">The server owned by the subscription.</param>
    private sealed class ServerSubscription(Device.ModbusServer server) : IDisposable
    {
        /// <summary>Stores the server until it is disposed.</summary>
        private readonly Device.ModbusServer _server = server;

        /// <summary>Tracks whether disposal has already occurred.</summary>
        private int _disposed;

        /// <summary>Stops and disposes the server exactly once.</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _server.Stop();
            _server.Dispose();
        }
    }
}
