// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using IoT.DriverCore.TwinCATRx;
using IoT.DriverCore.TwinCATRx.Core;

namespace IoT.DriverCore.TwinCATRx.Tests.Rx;

/// <summary>Simple fake implementation of IRxTcAdsClient for testing extensions.</summary>
internal sealed class RxFakeClient : IRxTcAdsClient
{
    /// <summary>The default TwinCAT 3 ADS port used by the fake client.</summary>
    private const int TwinCat3Port = 851;

    /// <summary>Stores fake write notifications.</summary>
    private readonly Signal<string?> _onWrite = new();

    /// <summary>Tracks whether the fake client has been canceled or disposed.</summary>
    private bool _canceled;

    /// <summary>Initializes a new instance of the <see cref="RxFakeClient"/> class.</summary>
    /// <param name="data">The data stream.</param>
    public RxFakeClient(IObservable<(string Variable, object? Data, string? Id)> data)
    {
        DataReceived = data;
        Settings = new Settings { Port = TwinCat3Port, AdsAddress = string.Empty, SettingsId = "Default" };
    }

    /// <inheritdoc/>
    public IObservable<string[]> Code => Observable.Empty<string[]>();

    /// <inheritdoc/>
    public IObservable<Unit> InitializeComplete => Observable.Return(Unit.Default);

    /// <inheritdoc/>
    public IObservableAsync<Unit> InitializeCompleteAsync =>
        ObservableBridgeExtensions.ToAsyncObservable(InitializeComplete);

    /// <inheritdoc/>
    public IObservable<(string Variable, object? Data, string? Id)> DataReceived { get; }

    /// <inheritdoc/>
    public IObservableAsync<(string Variable, object? Data, string? Id)> DataReceivedAsync =>
        ObservableBridgeExtensions.ToAsyncObservable(DataReceived);

    /// <inheritdoc/>
    public IObservable<Exception> ErrorReceived => Observable.Empty<Exception>();

    /// <inheritdoc/>
    public IObservableAsync<Exception> ErrorReceivedAsync =>
        ObservableBridgeExtensions.ToAsyncObservable(ErrorReceived);

    /// <inheritdoc/>
    public IObservable<string?> OnWrite => _onWrite;

    /// <inheritdoc/>
    public IObservableAsync<string?> OnWriteAsync => ObservableBridgeExtensions.ToAsyncObservable(OnWrite);

    /// <inheritdoc/>
    public IDictionary<string, uint?> ReadWriteHandleInfo { get; } = new Dictionary<string, uint?>();

    /// <inheritdoc/>
    public ISettings? Settings { get; private set; }

    /// <inheritdoc/>
    public IDictionary<string, (uint? Handle, int ArrayLength)> WriteHandleInfo { get; } =
        new Dictionary<string, (uint? Handle, int ArrayLength)>();

    /// <inheritdoc/>
    public bool IsPaused { get; private set; }

    /// <inheritdoc/>
    public IObservable<bool> IsPausedObservable => Observable.Return(IsPaused);

    /// <inheritdoc/>
    public IObservableAsync<bool> IsPausedObservableAsync =>
        ObservableBridgeExtensions.ToAsyncObservable(IsPausedObservable);

    /// <inheritdoc/>
    public bool IsDisposed => _canceled;

    /// <summary>Gets recorded read calls.</summary>
    internal List<(string Variable, int? ArrayLength, string? Id)> ReadCalls { get; } = [];

    /// <summary>Gets recorded write calls.</summary>
    internal List<(string Variable, object Value, string? Id)> WriteCalls { get; } = [];

    /// <summary>Gets a value indicating whether cancellation was requested.</summary>
    internal bool IsCancellationRequested => _canceled;

    /// <inheritdoc/>
    public void Pause(TimeSpan time) => IsPaused = true;

    /// <inheritdoc/>
    public void Connect(ISettings settings) => Settings = settings;

    /// <inheritdoc/>
    public void Disconnect()
    {
    }

    /// <inheritdoc/>
    public void Read(string variable) => Read(variable, null, null);

    /// <inheritdoc/>
    public void Read(string variable, string? id) => Read(variable, null, id);

    /// <inheritdoc/>
    public void Read(string variable, int? arrayLength) => Read(variable, arrayLength, null);

    /// <inheritdoc/>
    public void Read(string variable, int? arrayLength, string? id)
    {
        ReadCalls.Add((variable, arrayLength, id));
    }

    /// <inheritdoc/>
    public void Write(string variable, object value) => Write(variable, value, null);

    /// <inheritdoc/>
    public void Write(string variable, object value, string? id)
    {
        WriteCalls.Add((variable, value, id));
        _onWrite.OnNext(string.IsNullOrWhiteSpace(id) ? "Success" : $"Success,{id}");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _canceled = true;
        _onWrite.Dispose();
    }

    /// <summary>Cancels the fake client.</summary>
    internal void Cancel() => _canceled = true;
}
