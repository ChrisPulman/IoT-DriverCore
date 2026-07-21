// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using CP.IoT.Core;

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;

#else

namespace MitsubishiRx;

#endif

/// <summary>
/// Composes <see cref="ILogicalTagClient"/> with <see cref="MitsubishiRx"/> while retaining the
/// driver's typed conversions, device-address handling, grouped scans, and file reload APIs.
/// </summary>
public sealed class MitsubishiLogicalTagClient : ILogicalTagClient, IDisposable
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
                    new LogicalTagValue(tag.Name, response.Value, _timeProvider.GetUtcNow(), "Good"))
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
        var results = new List<TagOperationResult<LogicalTagValue>>(tagNames.Count);
        foreach (var tagName in tagNames)
        {
            results.Add(await ReadAsync(tagName, cancellationToken).ConfigureAwait(false));
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
                    new LogicalTagValue(tag.Name, value.Value, _timeProvider.GetUtcNow(), "Good"))
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
        var results = new List<TagOperationResult<LogicalTagValue>>(values.Count);
        foreach (var value in values)
        {
            results.Add(await WriteAsync(value, cancellationToken).ConfigureAwait(false));
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
        return new ManyObservable(tagNames.Select(Observe).ToArray());
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

    /// <summary>Maps a common tag to its Mitsubishi definition.</summary>
    /// <param name="tag">The common tag.</param>
    /// <returns>The Mitsubishi definition.</returns>
    private static MitsubishiTagDefinition ToMitsubishiTag(LogicalTag tag) =>
        new(
            tag.Name,
            tag.Address,
            tag.DataType,
            EmptyToNull(tag.Description),
            ParseDouble(tag.Metadata, "Scale", 1.0),
            ParseDouble(tag.Metadata, "Offset", 0.0),
            ParseNullableInt(tag.Metadata, "Length"),
            GetMetadata(tag.Metadata, "Encoding"),
            GetMetadata(tag.Metadata, "Units"),
            ParseBool(tag.Metadata, "Signed"),
            GetMetadata(tag.Metadata, "ByteOrder"),
            GetMetadata(tag.Metadata, "Notes"));

    /// <summary>Gets all declared group names.</summary>
    /// <param name="tag">The common tag.</param>
    /// <returns>The distinct group names.</returns>
    private static string[] GetGroupNames(LogicalTag tag)
    {
        var names = new List<string>();
        if (!string.IsNullOrWhiteSpace(tag.GroupName))
        {
            names.Add(tag.GroupName);
        }

        if (tag.Metadata.TryGetValue("Groups", out var groups))
        {
            names.AddRange(
                groups
                    .Split('|', StringSplitOptions.RemoveEmptyEntries)
                    .Select(Uri.UnescapeDataString));
        }

        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>Gets optional metadata.</summary>
    /// <param name="metadata">The metadata dictionary.</param>
    /// <param name="name">The metadata key.</param>
    /// <returns>The optional value.</returns>
    private static string? GetMetadata(IReadOnlyDictionary<string, string> metadata, string name) =>
        metadata.TryGetValue(name, out var value) ? EmptyToNull(value) : null;

    /// <summary>Converts empty values to null.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The non-empty value or null.</returns>
    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>Parses floating-point metadata.</summary>
    /// <param name="metadata">The metadata dictionary.</param>
    /// <param name="name">The metadata key.</param>
    /// <param name="defaultValue">The fallback value.</param>
    /// <returns>The parsed value.</returns>
    private static double ParseDouble(
        IReadOnlyDictionary<string, string> metadata,
        string name,
        double defaultValue) =>
        metadata.TryGetValue(name, out var value)
        && double.TryParse(
            value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var result)
            ? result
            : defaultValue;

    /// <summary>Parses optional integer metadata.</summary>
    /// <param name="metadata">The metadata dictionary.</param>
    /// <param name="name">The metadata key.</param>
    /// <returns>The parsed value or null.</returns>
    private static int? ParseNullableInt(
        IReadOnlyDictionary<string, string> metadata,
        string name) =>
        metadata.TryGetValue(name, out var value)
        && int.TryParse(
            value,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var result)
            ? result
            : null;

    /// <summary>Parses Boolean metadata.</summary>
    /// <param name="metadata">The metadata dictionary.</param>
    /// <param name="name">The metadata key.</param>
    /// <returns>The parsed value.</returns>
    private static bool ParseBool(IReadOnlyDictionary<string, string> metadata, string name) =>
        metadata.TryGetValue(name, out var value) && bool.TryParse(value, out var result) && result;

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
            var tagNames = database.TryGetGroup(groupName, out var group)
                ? group
                    .ResolvedTagNames.Concat([tag.Name])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : [tag.Name];
            database.AddGroup(new MitsubishiTagGroupDefinition(groupName, tagNames));
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
            var subscriptions = sources.Select(source => source.Subscribe(observer)).ToArray();
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
