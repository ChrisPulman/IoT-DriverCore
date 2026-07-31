// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using IoT.Driver.Core;

#if REACTIVE_SHIM

namespace IoT.Driver.MitsubishiRx.Reactive;

#else

namespace IoT.Driver.MitsubishiRx;

#endif

/// <summary>
/// Composes <see cref="ILogicalTagClient"/> with <see cref="MitsubishiRx"/> while retaining the
/// driver's typed conversions, device-address handling, grouped scans, and file reload APIs.
/// </summary>
public sealed partial class MitsubishiLogicalTagClient : IManagedLogicalTagClient, IDisposable
{
    /// <summary>Stores the Mitsubishi owner.</summary>
    private readonly MitsubishiRx _owner;

    /// <summary>Stores the fallback scan interval.</summary>
    private readonly TimeSpan _defaultScanInterval;

    /// <summary>Stores the optional SQLite store.</summary>
    private readonly LogicalTagSqliteStore? _store;

    /// <summary>Stores whether this instance owns the catalog.</summary>
    private readonly bool _ownsCatalog;

    /// <summary>Stores the time provider.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>Stores whether this instance is disposed.</summary>
    private bool _disposed;

    /// <summary>Stores the number of eligible read plans created.</summary>
    private long _bulkReadPlanCount;

    /// <summary>Stores the number of eligible write plans created.</summary>
    private long _bulkWritePlanCount;

    /// <summary>Stores the number of eligible word reads planned.</summary>
    private long _bulkReadItemCount;

    /// <summary>Stores the number of eligible word writes planned.</summary>
    private long _bulkWriteItemCount;

    /// <summary>Stores the number of contiguous read ranges produced by the planner.</summary>
    private long _bulkReadRangeCount;

    /// <summary>Stores the number of contiguous write ranges produced by the planner.</summary>
    private long _bulkWriteRangeCount;

    /// <summary>Stores the number of grouped read protocol calls issued.</summary>
    private long _bulkReadProtocolCallCount;

    /// <summary>Stores the number of grouped write protocol calls issued.</summary>
    private long _bulkWriteProtocolCallCount;

    /// <summary>Initializes a new instance of the <see cref="MitsubishiLogicalTagClient"/> class.</summary>
    /// <param name="owner">The Mitsubishi client.</param>
    /// <param name="catalog">An optional shared catalog.</param>
    /// <param name="defaultScanInterval">The fallback observation interval.</param>
    /// <param name="store">The optional SQLite store.</param>
    public MitsubishiLogicalTagClient(
        MitsubishiRx owner,
        ILogicalTagCatalog? catalog,
        TimeSpan? defaultScanInterval,
        LogicalTagSqliteStore? store)
        : this(owner, catalog, defaultScanInterval, store, TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MitsubishiLogicalTagClient"/> class.</summary>
    /// <param name="owner">The Mitsubishi client.</param>
    /// <param name="catalog">An optional shared catalog.</param>
    /// <param name="defaultScanInterval">The fallback observation interval.</param>
    /// <param name="store">The optional SQLite store.</param>
    /// <param name="timeProvider">The time provider used for timestamping read and write results.</param>
    public MitsubishiLogicalTagClient(
        MitsubishiRx owner,
        ILogicalTagCatalog? catalog,
        TimeSpan? defaultScanInterval,
        LogicalTagSqliteStore? store,
        TimeProvider timeProvider)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Catalog = catalog ?? new LogicalTagCatalog();
        _ownsCatalog = catalog is null;
        _defaultScanInterval = defaultScanInterval ?? TimeSpan.FromSeconds(1);
        _store = store;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        if (_defaultScanInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultScanInterval));
        }

        Catalog.Changed += OnCatalogChanged;
        foreach (var tag in Catalog.List())
        {
            ApplyToMitsubishiDatabase(tag);
        }
    }

    /// <summary>Gets the shared catalog used for registrations and persistence.</summary>
    public ILogicalTagCatalog Catalog { get; }

    /// <summary>Gets an immutable snapshot of deterministic grouped bulk operation counts.</summary>
    public MitsubishiLogicalTagBulkOperationMetrics BulkOperationMetrics =>
        new(
            new MitsubishiLogicalTagBulkDirectionMetrics(
                Interlocked.Read(ref _bulkReadPlanCount),
                Interlocked.Read(ref _bulkReadItemCount),
                Interlocked.Read(ref _bulkReadRangeCount),
                Interlocked.Read(ref _bulkReadProtocolCallCount)),
            new MitsubishiLogicalTagBulkDirectionMetrics(
                Interlocked.Read(ref _bulkWritePlanCount),
                Interlocked.Read(ref _bulkWriteItemCount),
                Interlocked.Read(ref _bulkWriteRangeCount),
                Interlocked.Read(ref _bulkWriteProtocolCallCount)));

    /// <summary>Adds or replaces a logical tag and makes it available to Mitsubishi typed APIs.</summary>
    /// <param name="tag">The tag to register.</param>
    public void RegisterTag(LogicalTag tag)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(tag);
        Catalog.Upsert(tag);
    }

    /// <summary>Compatibility alias for <see cref="RegisterTag"/>.</summary>
    /// <param name="tag">The tag to register.</param>
    public void Register(LogicalTag tag) => RegisterTag(tag);

    /// <summary>Creates and registers a logical Mitsubishi tag.</summary>
    /// <param name="registration">The logical tag registration.</param>
    /// <returns>The registered immutable tag.</returns>
    public LogicalTag CreateTag(MitsubishiLogicalTagRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        var tag = registration.ToLogicalTag();
        RegisterTag(tag);
        return tag;
    }

    /// <summary>Removes a tag from the common catalog.</summary>
    /// <param name="name">The logical name.</param>
    /// <returns><see langword="true"/> when the tag existed.</returns>
    public bool RemoveTag(string name) => Catalog.TryRemove(name, out _);

    /// <summary>Adds or replaces a collection of logical tags.</summary>
    /// <param name="tags">The tags to register.</param>
    public void RegisterRange(IEnumerable<LogicalTag> tags)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(tags);
        foreach (var tag in tags)
        {
            RegisterTag(tag);
        }
    }

    /// <summary>Imports the shared RFC 4180 CSV format and registers every tag.</summary>
    /// <param name="reader">The CSV reader.</param>
    /// <param name="delimiter">The field delimiter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The imported tags.</returns>
    public async Task<IReadOnlyList<LogicalTag>> ImportCsvAsync(
        TextReader reader,
        char delimiter,
        CancellationToken cancellationToken)
    {
        var tags = await LogicalTagCsv
            .ImportAsync(reader, delimiter, cancellationToken)
            .ConfigureAwait(false);
        RegisterRange(tags);
        return tags;
    }

    /// <summary>Exports the current catalog using the shared RFC 4180 CSV format.</summary>
    /// <param name="writer">The CSV writer.</param>
    /// <param name="delimiter">The field delimiter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the catalog is written.</returns>
    public Task ExportCsvAsync(
        TextWriter writer,
        char delimiter,
        CancellationToken cancellationToken) =>
            LogicalTagCsv.ExportAsync(Catalog.List(), writer, delimiter, cancellationToken);

    /// <summary>Loads and registers all tags from the common SQLite store.</summary>
    /// <param name="store">The initialized or uninitialized store.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The loaded tags.</returns>
    public async Task<IReadOnlyList<LogicalTag>> LoadFromSqliteAsync(
        LogicalTagSqliteStore store,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        await store.InitializeAsync(cancellationToken).ConfigureAwait(false);
        var tags = await store.ListTagsAsync(cancellationToken).ConfigureAwait(false);
        RegisterRange(tags);
        return tags;
    }

    /// <summary>Initializes the configured common SQLite store.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the schema exists.</returns>
    public Task InitializeStoreAsync(CancellationToken cancellationToken) =>
        GetStore().InitializeAsync(cancellationToken);

    /// <summary>Loads the configured common SQLite store into this client.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The loaded tags.</returns>
    public Task<IReadOnlyList<LogicalTag>> LoadTagsAsync(
        CancellationToken cancellationToken) => LoadFromSqliteAsync(GetStore(), cancellationToken);

    /// <summary>Gets a persisted tag from the configured SQLite store.</summary>
    /// <param name="name">The logical name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted tag, if found.</returns>
    public Task<LogicalTag?> GetTagAsync(
        string name,
        CancellationToken cancellationToken) => GetStore().GetTagAsync(name, cancellationToken);

    /// <summary>Inserts or replaces a persisted tag in the configured SQLite store.</summary>
    /// <param name="tag">The tag to persist.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the tag is persisted.</returns>
    public async Task UpsertTagAsync(LogicalTag tag, CancellationToken cancellationToken)
    {
        await GetStore().UpsertTagAsync(tag, cancellationToken).ConfigureAwait(false);
        RegisterTag(tag);
    }

    /// <summary>Edits a persisted tag in the configured SQLite store.</summary>
    /// <param name="tag">The replacement tag.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the persisted tag existed.</returns>
    public async Task<bool> EditTagAsync(
        LogicalTag tag,
        CancellationToken cancellationToken)
    {
        var edited = await GetStore().EditTagAsync(tag, cancellationToken).ConfigureAwait(false);
        if (edited)
        {
            RegisterTag(tag);
        }

        return edited;
    }

    /// <summary>Deletes a persisted tag from the configured SQLite store and catalog.</summary>
    /// <param name="name">The logical name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the persisted tag existed.</returns>
    public async Task<bool> DeleteTagAsync(
        string name,
        CancellationToken cancellationToken)
    {
        var deleted = await GetStore()
            .DeleteTagAsync(name, cancellationToken)
            .ConfigureAwait(false);
        if (deleted)
        {
            _ = RemoveTag(name);
        }

        return deleted;
    }

    /// <inheritdoc/>
    public async Task<TagOperationResult<LogicalTagValue>> ReadAsync(
        string tagName,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetReadableTag(tagName, out var tag, out var failure))
        {
            return failure!;
        }

        try
        {
            var response = await _owner
                .ReadTagAsync(tag!.Name, cancellationToken)
                .ConfigureAwait(false);
            return response.IsSucceed
                ? TagOperationResult<LogicalTagValue>.Success(
                    new(tag.Name, response.Value, _timeProvider.GetUtcNow(), "Good"))
                : TagOperationResult<LogicalTagValue>.Failure(GetError(response));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return TagOperationResult<LogicalTagValue>.Failure(ex.Message);
        }
    }

    /// <summary>Reads and type-checks one logical tag.</summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="tag">The typed logical tag key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The typed operation result.</returns>
    public async Task<TagOperationResult<T>> ReadAsync<T>(
        LogicalTagKey<T> tag,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tag);
        var result = await ReadAsync(tag.Name, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || result.Value is null)
        {
            return TagOperationResult<T>.Failure(result.Error);
        }

        try
        {
            return TagOperationResult<T>.Success(
                MitsubishiTagValueConverter.Require(result.Value.Value, tag));
        }
        catch (InvalidCastException ex)
        {
            return TagOperationResult<T>.Failure(ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> ReadManyAsync(
        IReadOnlyCollection<string> tagNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tagNames);
        var names = new string[tagNames.Count];
        var nameIndex = 0;
        foreach (var tagName in tagNames)
        {
            names[nameIndex] = tagName;
            nameIndex++;
        }

        var results = new TagOperationResult<LogicalTagValue>[names.Length];
        var bulkRequests = new List<BulkWordRequest>(names.Length);
        var individualRequests = new List<IndexedTagName>();
        for (var index = 0; index < names.Length; index++)
        {
            var tagName = names[index];
            if (!TryGetReadableTag(tagName, out var tag, out var failure))
            {
                results[index] = CreateIndexedFailure(
                    BulkReadOperation,
                    index,
                    tagName,
                    failure!.Error);
            }
            else if (TryCreateBulkWordRequest(index, tag!, null, out var request))
            {
                bulkRequests.Add(request);
            }
            else
            {
                individualRequests.Add(new(index, tagName));
            }
        }

        await ExecuteBulkReadsAsync(bulkRequests, results, cancellationToken).ConfigureAwait(false);
        foreach (var request in individualRequests)
        {
            var result = await ReadAsync(request.TagName, cancellationToken).ConfigureAwait(false);
            results[request.Index] = result.Succeeded
                ? result
                : CreateIndexedFailure(
                    BulkReadOperation,
                    request.Index,
                    request.TagName,
                    result.Error);
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<TagOperationResult<LogicalTagValue>> WriteAsync(
        LogicalTagValue value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!TryGetWritableTag(value.TagName, out var tag, out var failure))
        {
            return failure!;
        }

        try
        {
            var response = await _owner
                .WriteTagAsync(tag!.Name, value.Value, cancellationToken)
                .ConfigureAwait(false);
            return response.IsSucceed
                ? TagOperationResult<LogicalTagValue>.Success(
                    new(tag.Name, value.Value, _timeProvider.GetUtcNow(), "Good"))
                : TagOperationResult<LogicalTagValue>.Failure(GetError(response));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return TagOperationResult<LogicalTagValue>.Failure(ex.Message);
        }
    }

    /// <summary>Writes one typed logical tag.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="tagName">The logical name.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The typed operation result.</returns>
    public async Task<TagOperationResult<T>> WriteAsync<T>(
        string tagName,
        T value,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        var tagValue = new LogicalTagValue(tagName.Trim(), value, _timeProvider.GetUtcNow(), "Good");
        var result = await WriteAsync(tagValue, cancellationToken).ConfigureAwait(false);
        return result.Succeeded
            ? TagOperationResult<T>.Success(value)
            : TagOperationResult<T>.Failure(result.Error);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> WriteManyAsync(
        IReadOnlyCollection<LogicalTagValue> values,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        var items = new LogicalTagValue[values.Count];
        var valueIndex = 0;
        foreach (var value in values)
        {
            items[valueIndex] = value;
            valueIndex++;
        }

        var results = new TagOperationResult<LogicalTagValue>[items.Length];
        var bulkRequests = new List<BulkWordRequest>(items.Length);
        var individualRequests = new List<IndexedTagValue>();
        for (var index = 0; index < items.Length; index++)
        {
            var value = items[index] ??
                throw new ArgumentException("Values cannot contain null entries.", nameof(values));
            if (!TryGetWritableTag(value.TagName, out var tag, out var failure))
            {
                results[index] = CreateIndexedFailure(
                    BulkWriteOperation,
                    index,
                    value.TagName,
                    failure!.Error);
            }
            else if (TryCreateBulkWordRequest(index, tag!, value, out var request))
            {
                bulkRequests.Add(request);
            }
            else
            {
                individualRequests.Add(new(index, value));
            }
        }

        await ExecuteBulkWritesAsync(bulkRequests, results, cancellationToken).ConfigureAwait(false);
        foreach (var request in individualRequests)
        {
            var result = await WriteAsync(request.Value, cancellationToken).ConfigureAwait(false);
            results[request.Index] = result.Succeeded
                ? result
                : CreateIndexedFailure(
                    BulkWriteOperation,
                    request.Index,
                    request.Value.TagName,
                    result.Error);
        }

        return results;
    }

    /// <inheritdoc/>
    public IObservable<LogicalTagValue> Observe(string tagName)
    {
        var tag = GetReadableTag(tagName);
        var scanInterval = tag.ScanInterval ?? _defaultScanInterval;
        return _owner
            .ObserveReactiveTag(new LogicalTagKey<object?>(tag.Name), scanInterval, null)
            .Select(value => new LogicalTagValue(
                tag.Name,
                value.Value,
                value.TimestampUtc,
                value.Quality.ToString()));
    }

    /// <summary>Observes one logical tag and type-checks each value.</summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="tag">The typed logical tag key.</param>
    /// <returns>The typed value stream.</returns>
    public IObservable<T> Observe<T>(LogicalTagKey<T> tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        return new TypedObservable<T>(Observe(tag.Name), tag);
    }

    /// <inheritdoc/>
    public IObservable<LogicalTagValue> ObserveMany(IReadOnlyCollection<string> tagNames)
    {
        ArgumentNullException.ThrowIfNull(tagNames);
        var sources = new List<IObservable<LogicalTagValue>>(tagNames.Count);
        foreach (var tagName in tagNames)
        {
            sources.Add(Observe(tagName));
        }

        return new ManyObservable(sources);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<LogicalTagValue> ObserveAsync(
        string tagName,
        CancellationToken cancellationToken = default) => ToAsyncEnumerable(Observe(tagName), cancellationToken);

    /// <summary>Observes one logical tag asynchronously and type-checks each value.</summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="tag">The typed logical tag key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The typed asynchronous value stream.</returns>
    public IAsyncEnumerable<T> ObserveAsync<T>(
        LogicalTagKey<T> tag,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tag);
        return ObserveTypedAsync(tag, cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<LogicalTagValue> ObserveManyAsync(
        IReadOnlyCollection<string> tagNames,
        CancellationToken cancellationToken = default) => ToAsyncEnumerable(ObserveMany(tagNames), cancellationToken);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Catalog.Changed -= OnCatalogChanged;
        if (_ownsCatalog && Catalog is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _disposed = true;
    }

    /// <summary>Adapts a classic observable to an asynchronous stream.</summary>
    /// <param name="source">The classic source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The asynchronous value stream.</returns>
    private static async IAsyncEnumerable<LogicalTagValue> ToAsyncEnumerable(
        IObservable<LogicalTagValue> source,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<LogicalTagValue>();
        await using var registration = cancellationToken.Register(
            static state => ((ChannelWriter<LogicalTagValue>)state!).TryComplete(),
            channel.Writer);
        using var subscription = source.Subscribe(new ChannelObserver(channel.Writer));
        await foreach (
            var value in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return value;
        }
    }

    /// <summary>Returns the most useful Mitsubishi error text.</summary>
    /// <param name="response">The failed response.</param>
    /// <returns>The error text.</returns>
    private static string GetError(Responce response) =>
        !string.IsNullOrWhiteSpace(response.Err)
            ? response.Err
            : "The Mitsubishi tag operation failed.";

    /// <summary>Creates a caller-indexed logical-tag failure.</summary>
    /// <param name="operation">The bulk operation name.</param>
    /// <param name="index">The caller-defined item index.</param>
    /// <param name="tagName">The logical tag name.</param>
    /// <param name="error">The underlying failure text.</param>
    /// <returns>The indexed failure result.</returns>
    private static TagOperationResult<LogicalTagValue> CreateIndexedFailure(
        string operation,
        int index,
        string tagName,
        string error) =>
        TagOperationResult<LogicalTagValue>.Failure(
            $"Mitsubishi bulk {operation} item [{index}] ('{tagName}') failed: {error}");

    /// <summary>Creates one successful result from a random or contiguous protocol word.</summary>
    /// <param name="request">The indexed bulk request.</param>
    /// <param name="word">The raw protocol word.</param>
    /// <param name="timestamp">The shared protocol-operation timestamp.</param>
    /// <returns>The successful logical-tag result.</returns>
    private static TagOperationResult<LogicalTagValue> CreateBulkReadSuccess(
        BulkWordRequest request,
        ushort word,
        DateTimeOffset timestamp)
    {
        object value = string.Equals(
            request.Definition.DataType,
            Int16DataType,
            StringComparison.Ordinal)
            ? unchecked((short)word)
            : word;
        return TagOperationResult<LogicalTagValue>.Success(
            new(request.Tag.Name, value, timestamp, "Good"));
    }

    /// <summary>Determines whether a database definition is a single protocol word.</summary>
    /// <param name="definition">The database definition.</param>
    /// <returns><see langword="true"/> when grouped word I/O preserves the declared type.</returns>
    private static bool IsSingleWordDefinition(MitsubishiTagDefinition definition) =>
        definition.DataType is null or WordDataType or UInt16DataType or Int16DataType;

    /// <summary>Attempts to encode one declared scalar value into a protocol word.</summary>
    /// <param name="definition">The database definition.</param>
    /// <param name="value">The logical value.</param>
    /// <param name="word">The encoded protocol word.</param>
    /// <returns><see langword="true"/> when the value matches the declared type.</returns>
    private static bool TryEncodeBulkWord(
        MitsubishiTagDefinition definition,
        object? value,
        out ushort word)
    {
        if (string.Equals(definition.DataType, Int16DataType, StringComparison.Ordinal)
            && value is short signed)
        {
            word = unchecked((ushort)signed);
            return true;
        }

        if ((definition.DataType is null or WordDataType or UInt16DataType)
            && value is ushort unsigned)
        {
            word = unsigned;
            return true;
        }

        word = default;
        return false;
    }

    /// <summary>Stores one indexed failure for every request affected by a protocol failure.</summary>
    /// <param name="operation">The bulk operation name.</param>
    /// <param name="requests">The affected requests.</param>
    /// <param name="error">The underlying failure text.</param>
    /// <param name="results">The caller-ordered result array.</param>
    private static void SetBulkFailures(
        string operation,
        IEnumerable<BulkWordRequest> requests,
        string error,
        TagOperationResult<LogicalTagValue>[] results)
    {
        foreach (var request in requests)
        {
            results[request.Index] = CreateIndexedFailure(
                operation,
                request.Index,
                request.Tag.Name,
                error);
        }
    }

    /// <summary>Determines whether the configured frame supports a genuine random-word command.</summary>
    /// <returns><see langword="true"/> when random word I/O is encoded as one protocol request.</returns>
    private bool SupportsRandomWordCommands() =>
        _owner.Options.TransportKind == MitsubishiTransportKind.Serial
            ? _owner.Options.FrameType is MitsubishiFrameType.ThreeC or MitsubishiFrameType.FourC
            : _owner.Options.FrameType is MitsubishiFrameType.ThreeE or MitsubishiFrameType.FourE;

    /// <summary>Creates a planner-backed word request when the tag and optional value are eligible.</summary>
    /// <param name="index">The caller-defined result index.</param>
    /// <param name="tag">The common logical tag.</param>
    /// <param name="value">The optional write value.</param>
    /// <param name="request">The eligible bulk request.</param>
    /// <returns><see langword="true"/> when the request can use grouped word I/O.</returns>
    private bool TryCreateBulkWordRequest(
        int index,
        LogicalTag tag,
        LogicalTagValue? value,
        out BulkWordRequest request)
    {
        var database = _owner.TagDatabase;
        if (database is null
            || !database.TryGet(tag.Name, out var definition)
            || !IsSingleWordDefinition(definition))
        {
            request = null!;
            return false;
        }

        MitsubishiDeviceAddress address;
        try
        {
            address = MitsubishiDeviceAddress.Parse(
                definition.Address,
                _owner.Options.XyNotation);
        }
        catch (Exception ex) when (
            ex is FormatException
            or NotSupportedException
            or OverflowException)
        {
            request = null!;
            return false;
        }

        ushort word = default;
        if (address.Descriptor.Kind != DeviceValueKind.Word
            || (value is not null && !TryEncodeBulkWord(definition, value.Value, out word)))
        {
            request = null!;
            return false;
        }

        request = new(
            index,
            tag,
            definition,
            address,
            value,
            value is null ? null : word);
        return true;
    }

    /// <summary>Builds the common planner input for eligible Mitsubishi word requests.</summary>
    /// <param name="requests">The eligible requests.</param>
    /// <param name="access">The requested transfer direction.</param>
    /// <returns>The deterministic, memory-area-aware transfer plan.</returns>
    private TagTransferPlan CreateBulkTransferPlan(
        IReadOnlyList<BulkWordRequest> requests,
        TagTransferAccess access)
    {
        var options = _owner.Options;
        var partition =
            $"{options.TransportKind}:{options.Host}:{options.Port}:{options.FrameType}";
        var transferRequests = new List<TagTransferRequest>(requests.Count);
        foreach (var request in requests)
        {
            transferRequests.Add(new(
                request.Tag.Name,
                new TagTransportAddress(
                    partition,
                    request.Address.Symbol,
                    WordDataType,
                    access,
                    string.Empty,
                    request.Address.Number,
                    1)));
        }

        return BulkTransferPlanner.Plan(transferRequests);
    }

    /// <summary>Enumerates and converts one typed logical tag stream.</summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="tag">The typed logical tag key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The typed asynchronous stream.</returns>
    private async IAsyncEnumerable<T> ObserveTypedAsync<T>(
        LogicalTagKey<T> tag,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var value in ObserveAsync(tag.Name, cancellationToken).ConfigureAwait(false))
        {
            yield return MitsubishiTagValueConverter.Require(value.Value, tag);
        }
    }

    /// <summary>Gets the configured SQLite store.</summary>
    /// <returns>The configured store.</returns>
    private LogicalTagSqliteStore GetStore() =>
        _store
        ?? throw new InvalidOperationException(
            "A LogicalTagSqliteStore must be supplied when the logical client is created.");

    /// <summary>Validates a readable tag lookup.</summary>
    /// <param name="tagName">The logical name.</param>
    /// <param name="tag">The resolved tag.</param>
    /// <param name="failure">The failure result.</param>
    /// <returns><see langword="true"/> when the tag may be read.</returns>
    private bool TryGetReadableTag(
        string tagName,
        out LogicalTag? tag,
        out TagOperationResult<LogicalTagValue>? failure)
    {
        if (!Catalog.TryGet(tagName, out tag) || tag is null)
        {
            failure = TagOperationResult<LogicalTagValue>.Failure(
                $"Logical tag '{tagName}' is not registered.");
            return false;
        }

        if (tag.AccessMode == LogicalTagAccessMode.Write)
        {
            failure = TagOperationResult<LogicalTagValue>.Failure(
                $"Logical tag '{tagName}' is write-only.");
            return false;
        }

        failure = null;
        return true;
    }

    /// <summary>Validates a writable tag lookup.</summary>
    /// <param name="tagName">The logical name.</param>
    /// <param name="tag">The resolved tag.</param>
    /// <param name="failure">The failure result.</param>
    /// <returns><see langword="true"/> when the tag may be written.</returns>
    private bool TryGetWritableTag(
        string tagName,
        out LogicalTag? tag,
        out TagOperationResult<LogicalTagValue>? failure)
    {
        if (!Catalog.TryGet(tagName, out tag) || tag is null)
        {
            failure = TagOperationResult<LogicalTagValue>.Failure(
                $"Logical tag '{tagName}' is not registered.");
            return false;
        }

        if (tag.AccessMode == LogicalTagAccessMode.Read)
        {
            failure = TagOperationResult<LogicalTagValue>.Failure(
                $"Logical tag '{tagName}' is read-only.");
            return false;
        }

        failure = null;
        return true;
    }

    /// <summary>Gets a registered readable tag.</summary>
    /// <param name="tagName">The logical name.</param>
    /// <returns>The readable tag.</returns>
    private LogicalTag GetReadableTag(string tagName)
    {
        if (TryGetReadableTag(tagName, out var tag, out var failure))
        {
            return tag!;
        }

        throw new InvalidOperationException(failure!.Error);
    }

    /// <summary>Synchronizes catalog additions and updates with the rich Mitsubishi database.</summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="args">The catalog change.</param>
    private void OnCatalogChanged(object? sender, LogicalTagChangedEventArgs args)
    {
        if (args.Kind == LogicalTagChangeKind.Removed)
        {
            return;
        }

        ApplyToMitsubishiDatabase(args.Tag);
    }

    /// <summary>Applies a common tag to the rich Mitsubishi database.</summary>
    /// <param name="tag">The common tag.</param>
    private void ApplyToMitsubishiDatabase(LogicalTag tag)
    {
        var database = _owner.TagDatabase ??= new MitsubishiTagDatabase([]);
        database.Add(ToMitsubishiTag(tag));
        foreach (var groupName in GetGroupNames(tag))
        {
            var tagNames = new List<string>();
            var knownTagNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (database.TryGetGroup(groupName, out var group))
            {
                foreach (var existingTagName in group.ResolvedTagNames)
                {
                    if (knownTagNames.Add(existingTagName))
                    {
                        tagNames.Add(existingTagName);
                    }
                }
            }

            if (knownTagNames.Add(tag.Name))
            {
                tagNames.Add(tag.Name);
            }

            database.AddGroup(new(groupName, tagNames));
        }
    }

    /// <summary>Forwards observable notifications into a channel.</summary>
    /// <param name="writer">The destination writer.</param>
    private sealed class ChannelObserver(ChannelWriter<LogicalTagValue> writer)
        : IObserver<LogicalTagValue>
    {
        /// <inheritdoc/>
        public void OnCompleted() => writer.TryComplete();

        /// <inheritdoc/>
        public void OnError(Exception error) => writer.TryComplete(error);

        /// <inheritdoc/>
        public void OnNext(LogicalTagValue value) => writer.TryWrite(value);
    }

    /// <summary>Subscribes one observer to multiple source streams.</summary>
    /// <param name="sources">The source streams.</param>
    private sealed class ManyObservable(IReadOnlyList<IObservable<LogicalTagValue>> sources)
        : IObservable<LogicalTagValue>
    {
        /// <inheritdoc/>
        public IDisposable Subscribe(IObserver<LogicalTagValue> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            var subscriptions = new IDisposable[sources.Count];
            for (var index = 0; index < sources.Count; index++)
            {
                subscriptions[index] = sources[index].Subscribe(observer);
            }

            return new SubscriptionSet(subscriptions);
        }
    }

    /// <summary>Type-checks values from a logical-tag stream.</summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="source">The untyped source.</param>
    /// <param name="tag">The typed logical tag key.</param>
    private sealed class TypedObservable<T>(
        IObservable<LogicalTagValue> source,
        LogicalTagKey<T> tag) : IObservable<T>
    {
        /// <inheritdoc/>
        public IDisposable Subscribe(IObserver<T> observer) =>
            source.Subscribe(new TypedObserver<T>(observer, tag));
    }

    /// <summary>Type-checks one logical-tag observer.</summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="observer">The typed destination.</param>
    /// <param name="tag">The typed logical tag key.</param>
    private sealed class TypedObserver<T>(
        IObserver<T> observer,
        LogicalTagKey<T> tag) : IObserver<LogicalTagValue>
    {
        /// <inheritdoc/>
        public void OnCompleted() => observer.OnCompleted();

        /// <inheritdoc/>
        public void OnError(Exception error) => observer.OnError(error);

        /// <inheritdoc/>
        public void OnNext(LogicalTagValue value)
        {
            try
            {
                observer.OnNext(MitsubishiTagValueConverter.Require(value.Value, tag));
            }
            catch (InvalidCastException ex)
            {
                observer.OnError(ex);
            }
        }
    }

    /// <summary>Disposes a set of source subscriptions.</summary>
    /// <param name="subscriptions">The source subscriptions.</param>
    private sealed class SubscriptionSet(IReadOnlyList<IDisposable> subscriptions) : IDisposable
    {
        /// <inheritdoc/>
        public void Dispose()
        {
            foreach (var subscription in subscriptions)
            {
                subscription.Dispose();
            }
        }
    }
}
