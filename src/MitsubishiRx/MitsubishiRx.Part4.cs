// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;

#else

namespace MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiRx type.</summary>
public sealed partial class MitsubishiRx : IDisposable, IAsyncDisposable
{
    /// <summary>Executes the ExecuteMonitorAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ExecuteMonitorAsync operation result.</returns>
    public Task<Responce<byte[]>> ExecuteMonitorAsync(
        CancellationToken cancellationToken) =>
        IsSerialOneC()
            ? ExecuteMonitorOneCAsync(cancellationToken)
            : ExecuteObservableAsync(
                () =>
                    Options.TransportKind == MitsubishiTransportKind.Serial
                        ? MitsubishiSerialProtocolEncoding.EncodeExecuteMonitorRequest(Options)
                        : MitsubishiProtocolEncoding.EncodeExecuteMonitor(Options),
                null,
                "Execute monitor",
                cancellationToken);

    /// <summary>Executes the ReadBlocksAsync operation.</summary>
    /// <param name="request">The request parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadBlocksAsync operation result.</returns>
    public Task<Responce<byte[]>> ReadBlocksAsync(
        MitsubishiBlockRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (IsSerialOneC())
        {
            return ReadBlocksOneCAsync(request, cancellationToken);
        }

        return ExecuteObservableAsync(
            () =>
                Options.TransportKind == MitsubishiTransportKind.Serial
                    ? MitsubishiSerialProtocolEncoding.EncodeBlockReadRequest(Options, request)
                    : MitsubishiProtocolEncoding.EncodeBlockRead(Options, request),
            null,
            "Block read",
            cancellationToken);
    }

    /// <summary>Executes the WriteBlocksAsync operation.</summary>
    /// <param name="request">The request parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteBlocksAsync operation result.</returns>
    public async Task<Responce> WriteBlocksAsync(
        MitsubishiBlockRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (IsSerialOneC())
        {
            return await WriteBlocksOneCAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var raw = await ExecuteObservableAsync(
                () =>
                    Options.TransportKind == MitsubishiTransportKind.Serial
                        ? MitsubishiSerialProtocolEncoding.EncodeBlockWriteRequest(Options, request)
                        : MitsubishiProtocolEncoding.EncodeBlockWrite(Options, request),
                null,
                "Block write",
                cancellationToken)
            .ConfigureAwait(false);
        return raw.ToBaseResponse();
    }

    /// <summary>Executes the ReadTypeNameAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadTypeNameAsync operation result.</returns>
    public async Task<Responce<MitsubishiTypeName>> ReadTypeNameAsync(
        CancellationToken cancellationToken)
    {
        var raw = await ExecuteObservableAsync(
                () =>
                    Options.TransportKind == MitsubishiTransportKind.Serial
                        ? MitsubishiSerialProtocolEncoding.EncodeReadTypeNameRequest(Options)
                        : MitsubishiProtocolEncoding.EncodeReadTypeName(Options),
                null,
                "Read type name",
                cancellationToken)
            .ConfigureAwait(false);
        return ParseTypeName(raw);
    }

    /// <summary>Executes the RemoteRunAsync operation.</summary>
    /// <param name="force">The force parameter.</param>
    /// <param name="clearMode">The clearMode parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The RemoteRunAsync operation result.</returns>
    public Task<Responce> RemoteRunAsync(
        bool force,
        bool clearMode,
        CancellationToken cancellationToken) =>
            ExecuteControlAsync(MitsubishiCommandCodes.RemoteRun, cancellationToken, force, clearMode);

    /// <summary>Executes the RemoteStopAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The RemoteStopAsync operation result.</returns>
    public Task<Responce> RemoteStopAsync(CancellationToken cancellationToken) =>
        ExecuteControlAsync(MitsubishiCommandCodes.RemoteStop, cancellationToken);

    /// <summary>Executes the RemotePauseAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The RemotePauseAsync operation result.</returns>
    public Task<Responce> RemotePauseAsync(CancellationToken cancellationToken) =>
        ExecuteControlAsync(MitsubishiCommandCodes.RemotePause, cancellationToken);

    /// <summary>Executes the RemoteLatchClearAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The RemoteLatchClearAsync operation result.</returns>
    public Task<Responce> RemoteLatchClearAsync(CancellationToken cancellationToken) =>
        ExecuteControlAsync(MitsubishiCommandCodes.RemoteLatchClear, cancellationToken);

    /// <summary>Executes the RemoteResetAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The RemoteResetAsync operation result.</returns>
    public Task<Responce> RemoteResetAsync(CancellationToken cancellationToken) =>
        ExecuteControlAsync(MitsubishiCommandCodes.RemoteReset, cancellationToken);

    /// <summary>Executes the UnlockAsync operation.</summary>
    /// <param name="password">The password parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The UnlockAsync operation result.</returns>
    public Task<Responce> UnlockAsync(
        string password,
        CancellationToken cancellationToken) =>
            ExecutePasswordAsync(MitsubishiCommandCodes.Unlock, password, cancellationToken);

    /// <summary>Executes the LockAsync operation.</summary>
    /// <param name="password">The password parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The LockAsync operation result.</returns>
    public Task<Responce> LockAsync(
        string password,
        CancellationToken cancellationToken) =>
            ExecutePasswordAsync(MitsubishiCommandCodes.Lock, password, cancellationToken);

    /// <summary>Executes the ClearErrorAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ClearErrorAsync operation result.</returns>
    public async Task<Responce> ClearErrorAsync(CancellationToken cancellationToken)
    {
        var raw = await ExecuteObservableAsync(
                () =>
                    MitsubishiProtocolEncoding.Encode(
                        Options,
                        new MitsubishiRawCommandRequest(
                            MitsubishiCommandCodes.ClearError,
                            0x0000,
                            [],
                            "Clear error")),
                null,
                "Clear error",
                cancellationToken)
            .ConfigureAwait(false);
        return raw.ToBaseResponse();
    }

    /// <summary>Executes the LoopbackAsync operation.</summary>
    /// <param name="data">The data parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The LoopbackAsync operation result.</returns>
    public async Task<Responce<byte[]>> LoopbackAsync(
        byte[] data,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(data);
        var raw = await ExecuteObservableAsync(
                () => EncodeLoopbackRequest(data),
                GetOneEExpectedLength(MitsubishiNumericConstants.Four + data.Length),
                "Loopback",
                cancellationToken)
            .ConfigureAwait(false);
        return ParseLoopback(raw);
    }

    /// <summary>Executes the ReadMemoryAsync operation.</summary>
    /// <param name="command">The command parameter.</param>
    /// <param name="address">The address parameter.</param>
    /// <param name="length">The length parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadMemoryAsync operation result.</returns>
    public async Task<Responce<ushort[]>> ReadMemoryAsync(
        ushort command,
        ushort address,
        int length,
        CancellationToken cancellationToken)
    {
        var raw = await ExecuteObservableAsync(
                () =>
                    Options.TransportKind == MitsubishiTransportKind.Serial
                        ? MitsubishiSerialProtocolEncoding.EncodeMemoryAccessRequest(
                            Options,
                            command,
                            address,
                            length,
                            Array.Empty<ushort>())
                        : MitsubishiProtocolEncoding.EncodeMemoryAccess(
                            Options,
                            command,
                            address,
                            length,
                            Array.Empty<ushort>()),
                null,
                $"Read memory {command:X4}",
                cancellationToken)
            .ConfigureAwait(false);
        return ParseWords(raw, expectedWordCount: length);
    }

    /// <summary>Executes the WriteMemoryAsync operation.</summary>
    /// <param name="command">The command parameter.</param>
    /// <param name="address">The address parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteMemoryAsync operation result.</returns>
    public async Task<Responce> WriteMemoryAsync(
        ushort command,
        ushort address,
        IReadOnlyList<ushort> values,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        var raw = await ExecuteObservableAsync(
                () =>
                    Options.TransportKind == MitsubishiTransportKind.Serial
                        ? MitsubishiSerialProtocolEncoding.EncodeMemoryAccessRequest(
                            Options,
                            command,
                            address,
                            values.Count,
                            values.ToArray())
                        : MitsubishiProtocolEncoding.EncodeMemoryAccess(
                            Options,
                            command,
                            address,
                            values.Count,
                            values.ToArray()),
                null,
                $"Write memory {command:X4}",
                cancellationToken)
            .ConfigureAwait(false);
        return raw.ToBaseResponse();
    }

    /// <summary>Executes the ObserveWords operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="minimumUpdateSpacing">The minimumUpdateSpacing parameter.</param>
    /// <param name="pollTimeout">The pollTimeout parameter.</param>
    /// <returns>The ObserveWords operation result.</returns>
    public IObservable<Responce<ushort[]>> ObserveWords(
        string address,
        int points,
        TimeSpan pollInterval,
        TimeSpan? minimumUpdateSpacing,
        TimeSpan? pollTimeout) =>
        BuildPollingTrigger(pollInterval)
            .SelectAsyncSequential(_ => ReadWordsAsync(address, points, CancellationToken.None))
            .Conflate(minimumUpdateSpacing ?? TimeSpan.FromMilliseconds(MitsubishiNumericConstants.Ten), _scheduler)
            .DoOnSubscribe(() =>
                PublishOperation(
                    $"Observe words {address} subscribed",
                    true,
                    Array.Empty<byte>(),
                    Array.Empty<byte>()))
            .DoOnDispose(() =>
                PublishOperation(
                    $"Observe words {address} disposed",
                    true,
                    Array.Empty<byte>(),
                    Array.Empty<byte>()));

    /// <summary>Executes the ObserveBits operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="minimumUpdateSpacing">The minimumUpdateSpacing parameter.</param>
    /// <returns>The ObserveBits operation result.</returns>
    public IObservable<Responce<bool[]>> ObserveBits(
        string address,
        int points,
        TimeSpan pollInterval,
        TimeSpan? minimumUpdateSpacing) =>
        BuildPollingTrigger(pollInterval)
            .SelectAsyncSequential(_ => ReadBitsAsync(address, points, CancellationToken.None))
            .Conflate(minimumUpdateSpacing ?? TimeSpan.FromMilliseconds(MitsubishiNumericConstants.Ten), _scheduler)
            .DoOnSubscribe(() =>
                PublishOperation(
                    $"Observe bits {address} subscribed",
                    true,
                    Array.Empty<byte>(),
                    Array.Empty<byte>()))
            .DoOnDispose(() =>
                PublishOperation(
                    $"Observe bits {address} disposed",
                    true,
                    Array.Empty<byte>(),
                    Array.Empty<byte>()));

    /// <summary>Executes the ObserveWordsHeartbeat operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="heartbeatAfter">The heartbeatAfter parameter.</param>
    /// <param name="minimumUpdateSpacing">The minimumUpdateSpacing parameter.</param>
    /// <param name="pollTimeout">The pollTimeout parameter.</param>
    /// <returns>The ObserveWordsHeartbeat operation result.</returns>
    public IObservable<Heartbeat<Responce<ushort[]>>> ObserveWordsHeartbeat(
        string address,
        int points,
        TimeSpan pollInterval,
        TimeSpan heartbeatAfter,
        TimeSpan? minimumUpdateSpacing,
        TimeSpan? pollTimeout) =>
        ObserveWords(address, points, pollInterval, minimumUpdateSpacing, pollTimeout)
            .Heartbeat(heartbeatAfter, _scheduler);

    /// <summary>Executes the ObserveWordsStale operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="staleAfter">The staleAfter parameter.</param>
    /// <param name="minimumUpdateSpacing">The minimumUpdateSpacing parameter.</param>
    /// <returns>The ObserveWordsStale operation result.</returns>
    public IObservable<Stale<Responce<ushort[]>>> ObserveWordsStale(
        string address,
        int points,
        TimeSpan pollInterval,
        TimeSpan staleAfter,
        TimeSpan? minimumUpdateSpacing) =>
        ObserveWords(address, points, pollInterval, minimumUpdateSpacing, null)
            .DetectStale(staleAfter, _scheduler);

    /// <summary>Executes the ObserveWordsLatest operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="trigger">The trigger parameter.</param>
    /// <returns>The ObserveWordsLatest operation result.</returns>
    public IObservable<Responce<ushort[]>> ObserveWordsLatest(
        string address,
        int points,
        IObservable<Unit> trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        return trigger.SelectLatestAsync(_ =>
            ReadWordsAsync(address, points, CancellationToken.None));
    }
}
