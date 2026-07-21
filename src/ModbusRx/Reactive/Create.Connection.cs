// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net.NetworkInformation;
#if REACTIVE_SHIM
using ModbusRx.Reactive.Device;
#else
using ModbusRx.Device;
#endif

#if REACTIVE_SHIM
namespace ModbusRx.Reactive;
#else
namespace ModbusRx;
#endif

/// <summary>Provides ModbusRx functionality.</summary>
public static partial class Create
{
    /// <summary>Creates a network master and monitors its connection.</summary>
    /// <param name="hostAddress">The host address.</param>
    /// <param name="masterFactory">Creates the protocol-specific master.</param>
    /// <returns>The master and connection-status stream.</returns>
    private static IObservable<(bool Connected, Exception? Error, ModbusIpMaster? Master)> NetworkIpMaster(
        string hostAddress,
        Func<ModbusIpMaster> masterFactory) =>
        Observable.Create<(bool Connected, Exception? Error, ModbusIpMaster? Master)>(observer =>
        {
            var resources = new CompositeDisposable();
            var pingSender = new Ping();
            var state = new MasterConnectionState<ModbusIpMaster>();
            resources.Add(pingSender);
            resources.Add(
                ObserveMasterConnection(
                    Observable.Timer(PingInterval, CheckConnectionInterval),
                    observer,
                    state));
            resources.Add(
                Observable.Timer(CheckConnectionInterval, PingInterval)
                    .Where(_ => !state.Connected)
                    .Select(_ => pingSender.SendPingAsync(hostAddress, OneThousand))
                    .Select(task => ProcessPingReply(task, observer, resources, state, masterFactory))
                    .Retry(int.MaxValue)
                    .Subscribe());
            return resources;
        }).Publish().RefCount();

    /// <summary>Processes a ping reply and creates a master after a successful reply.</summary>
    /// <param name="task">The ping task.</param>
    /// <param name="observer">The connection observer.</param>
    /// <param name="resources">The connection resources.</param>
    /// <param name="state">The connection state.</param>
    /// <param name="masterFactory">Creates the master.</param>
    /// <returns>The ping reply.</returns>
    private static PingReply? ProcessPingReply(
        Task<PingReply> task,
        IObserver<(bool Connected, Exception? Error, ModbusIpMaster? Master)> observer,
        CompositeDisposable resources,
        MasterConnectionState<ModbusIpMaster> state,
        Func<ModbusIpMaster> masterFactory)
    {
        var reply = task.Result;
        if (state.Master is not null || reply.Status != IPStatus.Success)
        {
            return reply;
        }

        TryCreateNetworkMaster(observer, resources, state, masterFactory);
        return reply;
    }

    /// <summary>Creates a network master and reports creation failures.</summary>
    /// <param name="observer">The connection observer.</param>
    /// <param name="resources">The connection resources.</param>
    /// <param name="state">The connection state.</param>
    /// <param name="masterFactory">Creates the master.</param>
    private static void TryCreateNetworkMaster(
        IObserver<(bool Connected, Exception? Error, ModbusIpMaster? Master)> observer,
        CompositeDisposable resources,
        MasterConnectionState<ModbusIpMaster> state,
        Func<ModbusIpMaster> masterFactory)
    {
        try
        {
            observer.OnNext((false, new ModbusCommunicationException(CreateMasterMessage), null));
            state.Master = masterFactory();
            resources.Add(state.Master);
            state.Connected = true;
            state.ConnectionMessageSent = false;
            observer.OnNext((true, null, state.Master));
        }
        catch (Exception ex)
        {
            state.Master?.Dispose();
            state.Master = null;
            state.Connected = false;
            observer.OnNext((false, new ModbusCommunicationException(MasterFaultMessage, ex), null));
        }
    }

    /// <summary>Monitors an existing master connection.</summary>
    /// <typeparam name="TMaster">The master type.</typeparam>
    /// <param name="timer">The connection-check timer.</param>
    /// <param name="observer">The connection observer.</param>
    /// <param name="state">The connection state.</param>
    /// <returns>The monitor subscription.</returns>
    private static IDisposable ObserveMasterConnection<TMaster>(
        IObservable<long> timer,
        IObserver<(bool Connected, Exception? Error, TMaster? Master)> observer,
        MasterConnectionState<TMaster> state)
        where TMaster : class =>
        timer.Subscribe(_ =>
        {
            if (state.Connected && state.Master is null)
            {
                observer.OnNext(
                    (false, new ModbusCommunicationException(ResetConnectedMasterIsNullMessage), null));
                state.Connected = false;
            }

            if (state.Connected || state.ConnectionMessageSent)
            {
                return;
            }

            state.ConnectionMessageSent = true;
            observer.OnNext(
                (false, new ModbusCommunicationException(LostCommunicationMessage), state.Master));
        });

    /// <summary>Stores mutable master connection state shared by composed observers.</summary>
    /// <typeparam name="TMaster">The master type.</typeparam>
    private sealed class MasterConnectionState<TMaster>
        where TMaster : class
    {
        /// <summary>Gets or sets the current master.</summary>
        public TMaster? Master { get; set; }

        /// <summary>Gets or sets a value indicating whether the master is connected.</summary>
        public bool Connected { get; set; }

        /// <summary>Gets or sets a value indicating whether the lost-connection message was sent.</summary>
        public bool ConnectionMessageSent { get; set; }
    }
}
