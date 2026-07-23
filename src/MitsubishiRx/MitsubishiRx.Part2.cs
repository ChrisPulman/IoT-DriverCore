// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiRx type.</summary>
public sealed partial class MitsubishiRx : IDisposable, IAsyncDisposable
{
    /// <summary>Executes the WriteScaledDoubleByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteScaledDoubleByTagAsync operation result.</returns>
    public Task<Responce> WriteScaledDoubleByTagAsync(
        string tagName,
        double value,
        CancellationToken cancellationToken)
    {
        var tag = GetRequiredTag(tagName);
        var rawValue = RemoveScaleAndOffset(value, tag);
        return tag.DataType switch
        {
            null or "Word" => WriteWordsByTagAsync(
                tagName,
                [checked((ushort)Math.Round(rawValue, MidpointRounding.AwayFromZero))],
                cancellationToken),
            "DWord" => WriteDWordByTagAsync(
                tagName,
                checked((uint)Math.Round(rawValue, MidpointRounding.AwayFromZero)),
                cancellationToken),
            "Float" => WriteFloatByTagAsync(tagName, (float)rawValue, cancellationToken),
            _ => Task.FromResult(
                new Responce().Fail(
                    $"Scaled access is not supported for tag '{tagName}' with DataType '{tag.DataType}'.")),
        };
    }

    /// <summary>Executes the ReadStringByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadStringByTagAsync operation result.</returns>
    public Task<Responce<string>> ReadStringByTagAsync(
        string tagName,
        CancellationToken cancellationToken)
    {
        var tag = GetRequiredTag(tagName);
        var wordLength =
            tag.Length
            ?? throw new InvalidOperationException(
                $"Tag '{tagName}' must define Length before ReadStringByTagAsync(tagName) can be used.");
        return ReadStringByTagAsync(tagName, wordLength, cancellationToken);
    }

    /// <summary>Executes the ReadStringByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="wordLength">The wordLength parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadStringByTagAsync operation result.</returns>
    public async Task<Responce<string>> ReadStringByTagAsync(
        string tagName,
        int wordLength,
        CancellationToken cancellationToken)
    {
        if (wordLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(wordLength));
        }

        var tag = GetRequiredTag(tagName);
        var raw = await ReadWordsByTagAsync(tagName, wordLength, cancellationToken)
            .ConfigureAwait(false);
        return ConvertWords(raw, words => DecodeStringFromWords(words, tag));
    }

    /// <summary>Executes the WriteStringByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteStringByTagAsync operation result.</returns>
    public Task<Responce> WriteStringByTagAsync(
        string tagName,
        string value,
        CancellationToken cancellationToken)
    {
        var tag = GetRequiredTag(tagName);
        var wordLength =
            tag.Length
            ?? throw new InvalidOperationException(
                $"Tag '{tagName}' must define Length before WriteStringByTagAsync(tagName, value) can be used.");
        return WriteStringByTagAsync(tagName, value, wordLength, cancellationToken);
    }

    /// <summary>Executes the WriteStringByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="wordLength">The wordLength parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteStringByTagAsync operation result.</returns>
    public Task<Responce> WriteStringByTagAsync(
        string tagName,
        string value,
        int wordLength,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (wordLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(wordLength));
        }

        var tag = GetRequiredTag(tagName);
        return WriteWordsByTagAsync(
            tagName,
            EncodeStringWords(value, wordLength, tag),
            cancellationToken);
    }

    /// <summary>Reads a tag using the data type declared in <see cref="TagDatabase"/>.</summary>
    /// <param name="tagName">The logical tag name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The untyped tag value response.</returns>
    public Task<Responce<object?>> ReadTagAsync(
        string tagName,
        CancellationToken cancellationToken) => ReadTagValueAsync(tagName, cancellationToken);

    /// <summary>Writes a tag using the data type declared in <see cref="TagDatabase"/>.</summary>
    /// <param name="tagName">The logical tag name.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The write response.</returns>
    public Task<Responce> WriteTagAsync(
        string tagName,
        object? value,
        CancellationToken cancellationToken) => WriteTagValueAsync(tagName, value, cancellationToken);

    /// <summary>Executes the ValidateTagDatabase operation.</summary>
    /// <returns>The ValidateTagDatabase operation result.</returns>
    public Responce ValidateTagDatabase() => ValidateTagDatabase(TagDatabase);

    /// <summary>Executes the LoadAndValidateTagDatabase operation.</summary>
    /// <param name="path">The path parameter.</param>
    /// <returns>The LoadAndValidateTagDatabase operation result.</returns>
    public Responce<MitsubishiTagDatabase> LoadAndValidateTagDatabase(string path) =>
        LoadAndValidateTagDatabase(path, MitsubishiTagRolloutPolicy.AllowAll);

    /// <summary>Executes the LoadAndValidateTagDatabase operation.</summary>
    /// <param name="path">The path parameter.</param>
    /// <param name="policy">The policy parameter.</param>
    /// <returns>The LoadAndValidateTagDatabase operation result.</returns>
    public Responce<MitsubishiTagDatabase> LoadAndValidateTagDatabase(
        string path,
        MitsubishiTagRolloutPolicy policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            var database = MitsubishiTagDatabase.Load(path);
            var validation = ValidateTagDatabase(database);
            if (!validation.IsSucceed)
            {
                return new Responce<MitsubishiTagDatabase>(validation);
            }

            var diff = (TagDatabase ?? new MitsubishiTagDatabase([])).CompareWith(database);
            var policyResult = ValidateRolloutPolicy(diff, policy);
            if (!policyResult.IsSucceed)
            {
                return new Responce<MitsubishiTagDatabase>(policyResult, database);
            }

            TagDatabase = database;
            return new Responce<MitsubishiTagDatabase>(policyResult, database);
        }
        catch (Exception ex)
        {
            return new Responce<MitsubishiTagDatabase>().Fail(ex.Message, exception: ex);
        }
    }

    /// <summary>Executes the PreviewTagDatabaseDiff operation.</summary>
    /// <param name="path">The path parameter.</param>
    /// <returns>The PreviewTagDatabaseDiff operation result.</returns>
    public Responce<MitsubishiTagDatabaseDiff> PreviewTagDatabaseDiff(string path) =>
        PreviewTagDatabaseDiff(path, MitsubishiTagRolloutPolicy.AllowAll);

    /// <summary>Executes the PreviewTagDatabaseDiff operation.</summary>
    /// <param name="path">The path parameter.</param>
    /// <param name="policy">The policy parameter.</param>
    /// <returns>The PreviewTagDatabaseDiff operation result.</returns>
    public Responce<MitsubishiTagDatabaseDiff> PreviewTagDatabaseDiff(
        string path,
        MitsubishiTagRolloutPolicy policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            var database = MitsubishiTagDatabase.Load(path);
            var validation = ValidateTagDatabase(database);
            if (!validation.IsSucceed)
            {
                return new Responce<MitsubishiTagDatabaseDiff>(validation);
            }

            var diff = (TagDatabase ?? new MitsubishiTagDatabase([])).CompareWith(database);
            var policyResult = ValidateRolloutPolicy(diff, policy);
            return new Responce<MitsubishiTagDatabaseDiff>(policyResult, diff);
        }
        catch (Exception ex)
        {
            return new Responce<MitsubishiTagDatabaseDiff>().Fail(ex.Message, exception: ex);
        }
    }

    /// <summary>Executes the ObserveTagDatabaseDiff operation.</summary>
    /// <param name="path">The path parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="emitInitial">The emitInitial parameter.</param>
    /// <returns>The ObserveTagDatabaseDiff operation result.</returns>
    public IObservable<Responce<MitsubishiTagDatabaseDiff>> ObserveTagDatabaseDiff(
        string path,
        TimeSpan pollInterval,
        bool emitInitial) =>
        ObserveTagDatabaseDiff(
            path,
            pollInterval,
            emitInitial,
            MitsubishiTagRolloutPolicy.AllowAll);

    /// <summary>Executes the ObserveTagDatabaseDiff operation.</summary>
    /// <param name="path">The path parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="emitInitial">The emitInitial parameter.</param>
    /// <param name="policy">The policy parameter.</param>
    /// <returns>The ObserveTagDatabaseDiff operation result.</returns>
    public IObservable<Responce<MitsubishiTagDatabaseDiff>> ObserveTagDatabaseDiff(
        string path,
        TimeSpan pollInterval,
        bool emitInitial,
        MitsubishiTagRolloutPolicy policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (pollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollInterval));
        }

        var ticks = Observable.Interval(pollInterval, _scheduler);
        var trigger = emitInitial ? ticks.StartWith(0L) : ticks;
        string? lastFingerprint = null;
        return trigger
            .Select(_ => GetSchemaFingerprint(path))
            .Where(fingerprint =>
                !string.Equals(fingerprint, lastFingerprint, StringComparison.Ordinal))
            .Do(fingerprint => lastFingerprint = fingerprint)
            .Select(_ =>
            {
                var preview = PreviewTagDatabaseDiff(path, policy);
                if (!preview.IsSucceed)
                {
                    return preview;
                }

                var load = LoadAndValidateTagDatabase(path, policy);
                return load.IsSucceed && preview.Value is not null
                    ? new Responce<MitsubishiTagDatabaseDiff>(load, preview.Value)
                    : new Responce<MitsubishiTagDatabaseDiff>(preview);
            })
            .DoOnSubscribe(() =>
                PublishOperation(
                    $"Observe tag database diff {path} subscribed",
                    true,
                    Array.Empty<byte>(),
                    Array.Empty<byte>()))
            .DoOnDispose(() =>
                PublishOperation(
                    $"Observe tag database diff {path} disposed",
                    true,
                    Array.Empty<byte>(),
                    Array.Empty<byte>()));
    }

    /// <summary>Executes the ObserveTagDatabaseReload operation.</summary>
    /// <param name="path">The path parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="emitInitial">The emitInitial parameter.</param>
    /// <returns>The ObserveTagDatabaseReload operation result.</returns>
    public IObservable<Responce<MitsubishiTagDatabase>> ObserveTagDatabaseReload(
        string path,
        TimeSpan pollInterval,
        bool emitInitial) =>
        ObserveTagDatabaseReload(
            path,
            pollInterval,
            emitInitial,
            MitsubishiTagRolloutPolicy.AllowAll);

    /// <summary>Executes the ObserveTagDatabaseReload operation.</summary>
    /// <param name="path">The path parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="emitInitial">The emitInitial parameter.</param>
    /// <param name="policy">The policy parameter.</param>
    /// <returns>The ObserveTagDatabaseReload operation result.</returns>
    public IObservable<Responce<MitsubishiTagDatabase>> ObserveTagDatabaseReload(
        string path,
        TimeSpan pollInterval,
        bool emitInitial,
        MitsubishiTagRolloutPolicy policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (pollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollInterval));
        }

        var ticks = Observable.Interval(pollInterval, _scheduler);
        var trigger = emitInitial ? ticks.StartWith(0L) : ticks;
        string? lastFingerprint = null;
        return trigger
            .Select(_ => GetSchemaFingerprint(path))
            .Where(fingerprint =>
                !string.Equals(fingerprint, lastFingerprint, StringComparison.Ordinal))
            .Do(fingerprint => lastFingerprint = fingerprint)
            .Select(_ => LoadAndValidateTagDatabase(path, policy))
            .DoOnSubscribe(() =>
                PublishOperation(
                    $"Observe tag database reload {path} subscribed",
                    true,
                    Array.Empty<byte>(),
                    Array.Empty<byte>()))
            .DoOnDispose(() =>
                PublishOperation(
                    $"Observe tag database reload {path} disposed",
                    true,
                    Array.Empty<byte>(),
                    Array.Empty<byte>()));
    }

    /// <summary>Executes the ReadTagGroupSnapshotAsync operation.</summary>
    /// <param name="groupName">The groupName parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadTagGroupSnapshotAsync operation result.</returns>
    public async Task<Responce<MitsubishiTagGroupSnapshot>> ReadTagGroupSnapshotAsync(
        string groupName,
        CancellationToken cancellationToken)
    {
        var database =
            TagDatabase
            ?? throw new InvalidOperationException(
                MitsubishiMessages.TagDatabaseRequiredForTagApis);
        var group = database.GetRequiredGroup(groupName);
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var tagName in group.ResolvedTagNames)
        {
            var valueResult = await ReadTagValueAsync(tagName, cancellationToken)
                .ConfigureAwait(false);
            if (!valueResult.IsSucceed)
            {
                return new Responce<MitsubishiTagGroupSnapshot>(valueResult);
            }

            values[tagName] = valueResult.Value;
        }

        return new Responce<MitsubishiTagGroupSnapshot>(
            new MitsubishiTagGroupSnapshot(group.Name, values));
    }
}
