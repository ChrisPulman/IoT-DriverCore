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
    /// <summary>Stores the connectionStates field.</summary>
    private readonly StateSignal<MitsubishiConnectionState> _connectionStates = new(
        MitsubishiConnectionState.Disconnected);

    /// <summary>Stores the operationLogs field.</summary>
    private readonly Signal<MitsubishiOperationLog> _operationLogs = new();

    /// <summary>Stores the requestGate field.</summary>
    private readonly SemaphoreSlim _requestGate = new(1, 1);

    /// <summary>Stores the transport field.</summary>
    private readonly IMitsubishiTransport _transport;

    /// <summary>Stores the scheduler field.</summary>
    private readonly IScheduler _scheduler;

    /// <summary>Stores the time provider field.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>Stores the serialOneCMonitorAddresses field.</summary>
    private string[]? _serialOneCMonitorAddresses;

    /// <summary>Stores the disposed field.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the MitsubishiRx class.</summary>
    /// <param name="cpuType">The cpuType parameter.</param>
    /// <param name="ip">The ip parameter.</param>
    /// <param name="port">The port parameter.</param>
    /// <param name="timeout">The timeout parameter.</param>
    public MitsubishiRx(CpuType cpuType, string ip, int port, int timeout)
        : this(
            new MitsubishiClientOptions(
                ip,
                port,
                cpuType is CpuType.ASeries or CpuType.Fx3
                    ? MitsubishiFrameType.OneE
                    : MitsubishiFrameType.ThreeE,
                CommunicationDataCode.Binary,
                MitsubishiTransportKind.Tcp,
                Timeout: TimeSpan.FromMilliseconds(timeout),
                CpuType: cpuType),
            null,
            null) { }

    /// <summary>Initializes a new instance of the MitsubishiRx class.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="transport">The transport parameter.</param>
    /// <param name="scheduler">The scheduler parameter.</param>
    public MitsubishiRx(
        MitsubishiClientOptions options,
        IMitsubishiTransport? transport,
        IScheduler? scheduler)
        : this(options, transport, scheduler, TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the MitsubishiRx class.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="transport">The transport parameter.</param>
    /// <param name="scheduler">The scheduler parameter.</param>
    /// <param name="timeProvider">The time provider.</param>
    public MitsubishiRx(
        MitsubishiClientOptions options,
        IMitsubishiTransport? transport,
        IScheduler? scheduler,
        TimeProvider timeProvider)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _transport = transport ?? CreateDefaultTransport(options);
        _scheduler = scheduler ?? Scheduler.Default;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        if (options.TransportKind == MitsubishiTransportKind.Serial)
        {
            return;
        }

        _ = BuildEndPoint(options);
    }

    /// <summary>Gets or sets the Options property.</summary>
    public MitsubishiClientOptions Options { get; }

    /// <summary>Gets or sets the TagDatabase property.</summary>
    public MitsubishiTagDatabase? TagDatabase { get; set; }

    /// <summary>Gets or sets the Connected property.</summary>
    public bool Connected => _transport.IsConnected;

    /// <summary>Gets or sets the ConnectionStates property.</summary>
    public IObservable<MitsubishiConnectionState> ConnectionStates =>
        _connectionStates.AsObservable().DistinctUntilChanged();

    /// <summary>Gets or sets the OperationLogs property.</summary>
    public IObservable<MitsubishiOperationLog> OperationLogs => _operationLogs.AsObservable();

    /// <summary>Composes the common logical-tag contract with this Mitsubishi client.</summary>
    /// <param name="catalog">An optional shared catalog.</param>
    /// <param name="defaultScanInterval">The fallback observation interval.</param>
    /// <param name="store">The optional SQLite store.</param>
    /// <returns>A logical-tag client retaining Mitsubishi-specific operations.</returns>
    public MitsubishiLogicalTagClient CreateLogicalTagClient(
        CP.IoT.Core.ILogicalTagCatalog? catalog,
        TimeSpan? defaultScanInterval,
        CP.IoT.Core.LogicalTagSqliteStore? store) => new(this, catalog, defaultScanInterval, store, _timeProvider);

    /// <summary>Executes the ReadGeneratedBitTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadGeneratedBitTagAsync operation result.</returns>
    public async Task<Responce<bool>> ReadGeneratedBitTagAsync(
        string tagName,
        CancellationToken cancellationToken)
    {
        var raw = await ReadBitsByTagAsync(tagName, 1, cancellationToken).ConfigureAwait(false);
        return !raw.IsSucceed || raw.Value is null
            ? new Responce<bool>(raw)
            : new Responce<bool>(raw, raw.Value[0]);
    }

    /// <summary>Executes the WriteGeneratedBitTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteGeneratedBitTagAsync operation result.</returns>
    public Task<Responce> WriteGeneratedBitTagAsync(
        string tagName,
        bool value,
        CancellationToken cancellationToken) => WriteBitsByTagAsync(tagName, [value], cancellationToken);

    /// <summary>Executes the ReadWordsByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadWordsByTagAsync operation result.</returns>
    public Task<Responce<ushort[]>> ReadWordsByTagAsync(
        string tagName,
        int points,
        CancellationToken cancellationToken) => ReadWordsAsync(ResolveTagAddress(tagName), points, cancellationToken);

    /// <summary>Executes the ReadBitsByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadBitsByTagAsync operation result.</returns>
    public Task<Responce<bool[]>> ReadBitsByTagAsync(
        string tagName,
        int points,
        CancellationToken cancellationToken) => ReadBitsAsync(ResolveTagAddress(tagName), points, cancellationToken);

    /// <summary>Executes the WriteWordsByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteWordsByTagAsync operation result.</returns>
    public Task<Responce> WriteWordsByTagAsync(
        string tagName,
        IReadOnlyList<ushort> values,
        CancellationToken cancellationToken) => WriteWordsAsync(ResolveTagAddress(tagName), values, cancellationToken);

    /// <summary>Executes the WriteBitsByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteBitsByTagAsync operation result.</returns>
    public Task<Responce> WriteBitsByTagAsync(
        string tagName,
        IReadOnlyList<bool> values,
        CancellationToken cancellationToken) => WriteBitsAsync(ResolveTagAddress(tagName), values, cancellationToken);

    /// <summary>Executes the RandomReadWordsByTagAsync operation.</summary>
    /// <param name="tagNames">The tagNames parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The RandomReadWordsByTagAsync operation result.</returns>
    public Task<Responce<ushort[]>> RandomReadWordsByTagAsync(
        IEnumerable<string> tagNames,
        CancellationToken cancellationToken) => RandomReadWordsAsync(ResolveTagAddresses(tagNames), cancellationToken);

    /// <summary>Executes the RandomWriteWordsByTagAsync operation.</summary>
    /// <param name="values">The values parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The RandomWriteWordsByTagAsync operation result.</returns>
    public Task<Responce> RandomWriteWordsByTagAsync(
        IEnumerable<KeyValuePair<string, ushort>> values,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        return RandomWriteWordsAsync(
            values.Select(pair => new KeyValuePair<string, ushort>(
                ResolveTagAddress(pair.Key),
                pair.Value)),
            cancellationToken);
    }

    /// <summary>Executes the ReadInt16ByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadInt16ByTagAsync operation result.</returns>
    public async Task<Responce<short>> ReadInt16ByTagAsync(
        string tagName,
        CancellationToken cancellationToken)
    {
        var raw = await ReadWordsByTagAsync(tagName, 1, cancellationToken).ConfigureAwait(false);
        return ConvertWords(raw, words => unchecked((short)words[0]));
    }

    /// <summary>Executes the WriteInt16ByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteInt16ByTagAsync operation result.</returns>
    public Task<Responce> WriteInt16ByTagAsync(
        string tagName,
        short value,
        CancellationToken cancellationToken) =>
            WriteWordsByTagAsync(tagName, [unchecked((ushort)value)], cancellationToken);

    /// <summary>Executes the ReadUInt16ByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadUInt16ByTagAsync operation result.</returns>
    public async Task<Responce<ushort>> ReadUInt16ByTagAsync(
        string tagName,
        CancellationToken cancellationToken)
    {
        var raw = await ReadWordsByTagAsync(tagName, 1, cancellationToken).ConfigureAwait(false);
        return ConvertWords(raw, words => words[0]);
    }

    /// <summary>Executes the WriteUInt16ByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteUInt16ByTagAsync operation result.</returns>
    public Task<Responce> WriteUInt16ByTagAsync(
        string tagName,
        ushort value,
        CancellationToken cancellationToken) => WriteWordsByTagAsync(tagName, [value], cancellationToken);

    /// <summary>Executes the ReadInt32ByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadInt32ByTagAsync operation result.</returns>
    public async Task<Responce<int>> ReadInt32ByTagAsync(
        string tagName,
        CancellationToken cancellationToken)
    {
        var tag = GetRequiredTag(tagName);
        var raw = await ReadWordsByTagAsync(
                tagName,
                MitsubishiNumericConstants.Two,
                cancellationToken)
            .ConfigureAwait(false);
        return ConvertWords(raw, words => ConvertToInt32(words, tag));
    }

    /// <summary>Executes the WriteInt32ByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteInt32ByTagAsync operation result.</returns>
    public Task<Responce> WriteInt32ByTagAsync(
        string tagName,
        int value,
        CancellationToken cancellationToken)
    {
        var tag = GetRequiredTag(tagName);
        return WriteWordsByTagAsync(tagName, ConvertFromInt32(value, tag), cancellationToken);
    }

    /// <summary>Executes the ReadDWordByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadDWordByTagAsync operation result.</returns>
    public async Task<Responce<uint>> ReadDWordByTagAsync(
        string tagName,
        CancellationToken cancellationToken)
    {
        var tag = GetRequiredTag(tagName);
        var raw = await ReadWordsByTagAsync(
                tagName,
                MitsubishiNumericConstants.Two,
                cancellationToken)
            .ConfigureAwait(false);
        return ConvertWords(raw, words => ConvertToUInt32(words, tag));
    }

    /// <summary>Executes the WriteDWordByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteDWordByTagAsync operation result.</returns>
    public Task<Responce> WriteDWordByTagAsync(
        string tagName,
        uint value,
        CancellationToken cancellationToken)
    {
        var tag = GetRequiredTag(tagName);
        return WriteWordsByTagAsync(tagName, ConvertFromUInt32(value, tag), cancellationToken);
    }

    /// <summary>Executes the ReadFloatByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadFloatByTagAsync operation result.</returns>
    public async Task<Responce<float>> ReadFloatByTagAsync(
        string tagName,
        CancellationToken cancellationToken)
    {
        var tag = GetRequiredTag(tagName);
        var raw = await ReadWordsByTagAsync(
                tagName,
                MitsubishiNumericConstants.Two,
                cancellationToken)
            .ConfigureAwait(false);
        return ConvertWords(raw, words => ConvertToFloat(words, tag));
    }

    /// <summary>Executes the WriteFloatByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteFloatByTagAsync operation result.</returns>
    public Task<Responce> WriteFloatByTagAsync(
        string tagName,
        float value,
        CancellationToken cancellationToken)
    {
        var tag = GetRequiredTag(tagName);
        return WriteWordsByTagAsync(tagName, ConvertFromFloat(value, tag), cancellationToken);
    }

    /// <summary>Executes the ReadScaledDoubleByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadScaledDoubleByTagAsync operation result.</returns>
    public async Task<Responce<double>> ReadScaledDoubleByTagAsync(
        string tagName,
        CancellationToken cancellationToken)
    {
        var tag = GetRequiredTag(tagName);
        var wordCount = GetWordCountForScaledRead(tag);
        var raw = await ReadWordsByTagAsync(tagName, wordCount, cancellationToken)
            .ConfigureAwait(false);
        return ConvertWords(
            raw,
            words => ApplyScaleAndOffset(ReadNumericTagValue(tag, words), tag));
    }
}
