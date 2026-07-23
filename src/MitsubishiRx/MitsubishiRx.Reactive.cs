// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using IoT.DriverCore.Core;

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiRx type.</summary>
public sealed partial class MitsubishiRx
{
    /// <summary>Stores the reactiveStreamsGate field.</summary>
    private readonly object _reactiveStreamsGate = new();

    /// <summary>Stores the reactiveStreams field.</summary>
    private readonly Dictionary<string, object> _reactiveStreams = new(
        StringComparer.OrdinalIgnoreCase);

    /// <summary>Executes the ObserveReactiveWords operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="minimumUpdateSpacing">The minimumUpdateSpacing parameter.</param>
    /// <returns>The ObserveReactiveWords operation result.</returns>
    public IObservable<MitsubishiReactiveValue<ushort[]>> ObserveReactiveWords(
        string address,
        int points,
        TimeSpan pollInterval,
        TimeSpan? minimumUpdateSpacing)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);
        var spacing = minimumUpdateSpacing ?? TimeSpan.Zero;
        var key = $"words|{address}|{points}|{pollInterval.Ticks}|{spacing.Ticks}";
        return GetOrCreateSharedReactiveStream(
            key,
            emitInitial =>
                ApplyReactiveSpacing(
                    BuildPollingTrigger(pollInterval, emitInitial)
                        .SelectAsyncSequential(async _ =>
                            MitsubishiReactiveValue.FromResponse(
                                await ReadWordsAsync(address, points, CancellationToken.None)
                                    .ConfigureAwait(false),
                                _scheduler.Now,
                                $"Read words {address}")),
                    spacing));
    }

    /// <summary>Executes the ObserveReactiveTag operation.</summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="tagKey">The typed logical tag key.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="minimumUpdateSpacing">The minimumUpdateSpacing parameter.</param>
    /// <returns>The ObserveReactiveTag operation result.</returns>
    public IObservable<MitsubishiReactiveValue<T>> ObserveReactiveTag<T>(
        LogicalTagKey<T> tagKey,
        TimeSpan pollInterval,
        TimeSpan? minimumUpdateSpacing)
    {
        ArgumentNullException.ThrowIfNull(tagKey);
        var tagName = tagKey.Name;
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        var tag = GetRequiredTag(tagName);
        var spacing = minimumUpdateSpacing ?? TimeSpan.Zero;
        if (string.Equals(tag.DataType, "Bit", StringComparison.OrdinalIgnoreCase))
        {
            return ObserveReactiveBits(tag.Address, 1, pollInterval, spacing)
                .Select(value =>
                    MapReactiveValue(
                        value,
                        $"Tag:{tagName}",
                        bits => MitsubishiTagValueConverter.Require(bits[0], tagKey)));
        }

        var wordCount = GetReactiveWordCount(tag);
        return ObserveReactiveWords(tag.Address, wordCount, pollInterval, spacing)
            .Select(value =>
                MapReactiveValue(
                    value,
                    $"Tag:{tagName}",
                    words => MitsubishiTagValueConverter.Require(
                        ConvertTagWordsToObject(tag, words),
                        tagKey)));
    }

    /// <summary>Executes the ObserveReactiveTagGroup operation.</summary>
    /// <param name="groupName">The groupName parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="minimumUpdateSpacing">The minimumUpdateSpacing parameter.</param>
    /// <returns>The ObserveReactiveTagGroup operation result.</returns>
    public IObservable<MitsubishiReactiveValue<MitsubishiTagGroupSnapshot>> ObserveReactiveTagGroup(
        string groupName,
        TimeSpan pollInterval,
        TimeSpan? minimumUpdateSpacing)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);
        var spacing = minimumUpdateSpacing ?? TimeSpan.Zero;
        var key = $"group|{groupName}|{pollInterval.Ticks}|{spacing.Ticks}";
        return TryCreateContiguousWordGroupPlan(groupName, out var plan)
            ? ObserveContiguousReactiveTagGroup(key, groupName, plan, pollInterval, spacing)
            : ObserveIndividualReactiveTagGroup(key, groupName, pollInterval, spacing);
    }

    /// <summary>Executes the GetReactiveWordCount operation.</summary>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The GetReactiveWordCount operation result.</returns>
    private static int GetReactiveWordCount(MitsubishiTagDefinition tag) =>
        tag.DataType switch
        {
            "String" => tag.Length
                ?? throw new InvalidOperationException(
                    $"Tag '{tag.Name}' must define Length before reactive string observation can be used."),
            "Float" or "DWord" or "UInt32" or "Int32" => MitsubishiNumericConstants.Two,
            "Bit" => 1,
            _ when HasEngineeringMetadata(tag) => GetWordCountForScaledRead(tag),
            _ => 1,
        };

    /// <summary>Executes the MapReactiveValue operation.</summary>
    /// <typeparam name="TInput">The TInput type parameter.</typeparam>
    /// <typeparam name="TOutput">The TOutput type parameter.</typeparam>
    /// <param name="value">The value parameter.</param>
    /// <param name="source">The source parameter.</param>
    /// <param name="projector">The projector parameter.</param>
    /// <returns>The MapReactiveValue operation result.</returns>
    private static MitsubishiReactiveValue<TOutput> MapReactiveValue<TInput, TOutput>(
        MitsubishiReactiveValue<TInput> value,
        string source,
        Func<TInput, TOutput> projector)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(projector);
        if (value.Quality == MitsubishiReactiveQuality.Error)
        {
            return new MitsubishiReactiveValue<TOutput>(
                default,
                value.TimestampUtc,
                value.Quality,
                value.IsHeartbeat,
                value.IsStale,
                source,
                value.Error,
                value.ErrorCode,
                value.Exception);
        }

        if (value.Value is null)
        {
            return new MitsubishiReactiveValue<TOutput>(
                default,
                value.TimestampUtc,
                MitsubishiReactiveQuality.Error,
                Source: source,
                Error: $"Reactive source '{source}' produced a null payload.");
        }

        try
        {
            return new MitsubishiReactiveValue<TOutput>(
                projector(value.Value),
                value.TimestampUtc,
                value.Quality,
                value.IsHeartbeat,
                value.IsStale,
                source,
                value.Error,
                value.ErrorCode,
                value.Exception);
        }
        catch (Exception ex)
        {
            return new MitsubishiReactiveValue<TOutput>(
                default,
                value.TimestampUtc,
                MitsubishiReactiveQuality.Error,
                Source: source,
                Error: ex.Message,
                Exception: ex);
        }
    }

    /// <summary>Executes the ConvertTagWordsToObject operation.</summary>
    /// <param name="tag">The tag parameter.</param>
    /// <param name="words">The words parameter.</param>
    /// <returns>The ConvertTagWordsToObject operation result.</returns>
    private static object? ConvertTagWordsToObject(MitsubishiTagDefinition tag, ushort[] words) =>
        tag.DataType switch
        {
            "String" => DecodeStringFromWords(words, tag),
            "Float" => ConvertToFloat(words, tag),
            "DWord" or "UInt32" => ConvertToUInt32(words, tag),
            "Int32" => ConvertToInt32(words, tag),
            "Int16" => unchecked((short)words[0]),
            "UInt16" => words[0],
            _ when HasEngineeringMetadata(tag) => ApplyScaleAndOffset(
                ReadNumericTagValue(tag, words),
                tag),
            null or "Word" => tag.Signed ? unchecked((short)words[0]) : words[0],
            _ => words[0],
        };

    /// <summary>Executes the BuildContiguousWordGroupSnapshot operation.</summary>
    /// <param name="plan">The plan parameter.</param>
    /// <param name="words">The words parameter.</param>
    /// <returns>The BuildContiguousWordGroupSnapshot operation result.</returns>
    private static MitsubishiTagGroupSnapshot BuildContiguousWordGroupSnapshot(
        ReactiveWordGroupPlan plan,
        ushort[] words)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in plan.Items)
        {
            if (words.Length < item.WordOffset + item.WordCount)
            {
                throw new InvalidOperationException(
                    $"Reactive scan for group '{plan.GroupName}' did not return enough words " +
                    $"for tag '{item.TagName}'.");
            }

            var slice = words.Skip(item.WordOffset).Take(item.WordCount).ToArray();
            values[item.TagName] = ConvertTagWordsToObject(item.Tag, slice);
        }

        return new MitsubishiTagGroupSnapshot(plan.GroupName, values);
    }

    /// <summary>Builds a contiguous read plan from ordered candidates.</summary>
    /// <param name="group">The group definition.</param>
    /// <param name="ordered">The ordered candidates.</param>
    /// <param name="plan">The resulting plan.</param>
    /// <returns><see langword="true"/> when all candidates are contiguous.</returns>
    private static bool TryBuildContiguousWordGroupPlan(
        MitsubishiTagGroupDefinition group,
        IReadOnlyList<ReactiveWordGroupCandidate> ordered,
        [NotNullWhen(true)] out ReactiveWordGroupPlan? plan)
    {
        var first = ordered[0];
        if (
            ordered.Any(item =>
                !string.Equals(
                    item.Address.Descriptor.Symbol,
                    first.Address.Descriptor.Symbol,
                    StringComparison.OrdinalIgnoreCase)
                || item.Address.Descriptor.BinaryCode != first.Address.Descriptor.BinaryCode))
        {
            plan = null;
            return false;
        }

        var expectedNumber = first.Address.Number;
        var offsets = new Dictionary<string, ReactiveWordGroupItem>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var item in ordered)
        {
            if (item.Address.Number != expectedNumber)
            {
                plan = null;
                return false;
            }

            offsets[item.TagName] = new(
                item.TagName,
                item.Tag,
                item.Address.Number - first.Address.Number,
                item.WordCount);
            expectedNumber += item.WordCount;
        }

        var items = group.ResolvedTagNames.Select(tagName => offsets[tagName]).ToArray();
        plan = new(group.Name, first.Address, expectedNumber - first.Address.Number, items);
        return true;
    }

    /// <summary>Executes the ObserveReactiveBits operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="minimumUpdateSpacing">The minimumUpdateSpacing parameter.</param>
    /// <returns>The ObserveReactiveBits operation result.</returns>
    private IObservable<MitsubishiReactiveValue<bool[]>> ObserveReactiveBits(
        string address,
        int points,
        TimeSpan pollInterval,
        TimeSpan minimumUpdateSpacing)
    {
        var key = $"bits|{address}|{points}|{pollInterval.Ticks}|{minimumUpdateSpacing.Ticks}";
        return GetOrCreateSharedReactiveStream(
            key,
            initial =>
                ApplyReactiveSpacing(
                    BuildPollingTrigger(pollInterval, initial)
                        .SelectAsyncSequential(async _ =>
                            MitsubishiReactiveValue.FromResponse(
                                await ReadBitsAsync(address, points, CancellationToken.None)
                                    .ConfigureAwait(false),
                                _scheduler.Now,
                                $"Read bits {address}")),
                    minimumUpdateSpacing));
    }

    /// <summary>Executes the GetOrCreateSharedReactiveStream operation.</summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="key">The key parameter.</param>
    /// <param name="streamFactory">The streamFactory parameter.</param>
    /// <returns>The GetOrCreateSharedReactiveStream operation result.</returns>
    private IObservable<MitsubishiReactiveValue<T>> GetOrCreateSharedReactiveStream<T>(
        string key,
        Func<bool, IObservable<MitsubishiReactiveValue<T>>> streamFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(streamFactory);
        lock (_reactiveStreamsGate)
        {
            if (_reactiveStreams.TryGetValue(key, out var existing))
            {
                return ((SharedReactiveStream<T>)existing).Stream;
            }

            var created = new SharedReactiveStream<T>(streamFactory);
            _reactiveStreams[key] = created;
            return created.Stream;
        }
    }

    /// <summary>Executes the DisposeReactiveStreams operation.</summary>
    private void DisposeReactiveStreams()
    {
        lock (_reactiveStreamsGate)
        {
            foreach (var stream in _reactiveStreams.Values.OfType<IDisposable>())
            {
                stream.Dispose();
            }

            _reactiveStreams.Clear();
        }
    }

    /// <summary>Executes the TryCreateContiguousWordGroupPlan operation.</summary>
    /// <param name="groupName">The groupName parameter.</param>
    /// <param name="plan">The plan parameter.</param>
    /// <returns>The TryCreateContiguousWordGroupPlan operation result.</returns>
    private bool TryCreateContiguousWordGroupPlan(
        string groupName,
        [NotNullWhen(true)] out ReactiveWordGroupPlan? plan)
    {
        var database = GetRequiredTagDatabase();
        var group = database.GetRequiredGroup(groupName);
        var sortable = CreateReactiveWordGroupCandidates(database, group);
        if (sortable is null || sortable.Count == 0)
        {
            plan = null;
            return false;
        }

        var ordered = sortable
            .OrderBy(
                static item => item.Address.Descriptor.Symbol,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.Address.Number)
            .ToArray();
        return TryBuildContiguousWordGroupPlan(group, ordered, out plan);
    }

    /// <summary>Creates candidate word ranges for a reactive group.</summary>
    /// <param name="database">The tag database.</param>
    /// <param name="group">The group definition.</param>
    /// <returns>The candidates, or <see langword="null"/> when the group cannot be combined.</returns>
    private List<ReactiveWordGroupCandidate>? CreateReactiveWordGroupCandidates(
        MitsubishiTagDatabase database,
        MitsubishiTagGroupDefinition group)
    {
        var candidates = new List<ReactiveWordGroupCandidate>();
        foreach (var tagName in group.ResolvedTagNames)
        {
            var tag = database.GetRequired(tagName);
            if (string.Equals(tag.DataType, "Bit", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var parsed = MitsubishiDeviceAddress.Parse(tag.Address, Options.XyNotation);
            if (parsed.Descriptor.Kind != DeviceValueKind.Word)
            {
                return null;
            }

            candidates.Add(new(tagName, tag, parsed, GetReactiveWordCount(tag)));
        }

        return candidates;
    }

    /// <summary>Observes a contiguous word group through one PLC request.</summary>
    /// <param name="key">The shared stream key.</param>
    /// <param name="groupName">The group name.</param>
    /// <param name="plan">The contiguous read plan.</param>
    /// <param name="pollInterval">The poll interval.</param>
    /// <param name="spacing">The minimum update spacing.</param>
    /// <returns>The shared group stream.</returns>
    private IObservable<MitsubishiReactiveValue<MitsubishiTagGroupSnapshot>>
        ObserveContiguousReactiveTagGroup(
            string key,
            string groupName,
            ReactiveWordGroupPlan plan,
            TimeSpan pollInterval,
            TimeSpan spacing) =>
        GetOrCreateSharedReactiveStream(
            key,
            emitInitial =>
                ApplyReactiveSpacing(
                    BuildPollingTrigger(pollInterval, emitInitial)
                        .SelectAsyncSequential(_ =>
                            ReadContiguousReactiveTagGroupAsync(groupName, plan)),
                    spacing));

    /// <summary>Observes a non-contiguous group through individual tag reads.</summary>
    /// <param name="key">The shared stream key.</param>
    /// <param name="groupName">The group name.</param>
    /// <param name="pollInterval">The poll interval.</param>
    /// <param name="spacing">The minimum update spacing.</param>
    /// <returns>The shared group stream.</returns>
    private IObservable<MitsubishiReactiveValue<MitsubishiTagGroupSnapshot>>
        ObserveIndividualReactiveTagGroup(
            string key,
            string groupName,
            TimeSpan pollInterval,
            TimeSpan spacing) =>
        GetOrCreateSharedReactiveStream(
            key,
            emitInitial =>
                ApplyReactiveSpacing(
                    BuildPollingTrigger(pollInterval, emitInitial)
                        .SelectAsyncSequential(async _ =>
                            MitsubishiReactiveValue.FromResponse(
                                await ReadTagGroupSnapshotAsync(
                                        groupName,
                                        CancellationToken.None)
                                    .ConfigureAwait(false),
                                _scheduler.Now,
                                $"Group:{groupName}")),
                    spacing));

    /// <summary>Reads and projects one contiguous reactive group sample.</summary>
    /// <param name="groupName">The group name.</param>
    /// <param name="plan">The contiguous read plan.</param>
    /// <returns>The projected reactive value.</returns>
    private async Task<MitsubishiReactiveValue<MitsubishiTagGroupSnapshot>>
        ReadContiguousReactiveTagGroupAsync(
            string groupName,
            ReactiveWordGroupPlan plan)
    {
        var raw = await ExecuteObservableAsync(
                () => EncodeWordReadRequest(plan.StartAddress, plan.TotalWords),
                GetOneEExpectedLength(
                    MitsubishiNumericConstants.Two +
                    (plan.TotalWords * MitsubishiNumericConstants.Two)),
                $"Reactive scan {groupName}",
                CancellationToken.None)
            .ConfigureAwait(false);
        var words = ParseWords(raw, GetSerialExpectedWordCount(plan.TotalWords));
        if (!words.IsSucceed || words.Value is null)
        {
            return MitsubishiReactiveValue.FromResponse(
                new Responce<MitsubishiTagGroupSnapshot>(words),
                _scheduler.Now,
                $"Group:{groupName}");
        }

        try
        {
            var snapshot = BuildContiguousWordGroupSnapshot(plan, words.Value);
            return MitsubishiReactiveValue.FromResponse(
                new Responce<MitsubishiTagGroupSnapshot>(words, snapshot),
                _scheduler.Now,
                $"Group:{groupName}");
        }
        catch (Exception ex)
        {
            return new MitsubishiReactiveValue<MitsubishiTagGroupSnapshot>(
                null,
                _scheduler.Now,
                MitsubishiReactiveQuality.Error,
                Source: $"Group:{groupName}",
                Error: ex.Message,
                Exception: ex);
        }
    }

    /// <summary>Executes the ApplyReactiveSpacing operation.</summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="source">The source parameter.</param>
    /// <param name="spacing">The spacing parameter.</param>
    /// <returns>The ApplyReactiveSpacing operation result.</returns>
    private IObservable<MitsubishiReactiveValue<T>> ApplyReactiveSpacing<T>(
        IObservable<MitsubishiReactiveValue<T>> source,
        TimeSpan spacing)
    {
        ArgumentNullException.ThrowIfNull(source);
        return spacing > TimeSpan.Zero ? source.Conflate(spacing, _scheduler) : source;
    }

    /// <summary>Provides the SharedReactiveStream type.</summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="streamFactory">Creates the underlying stream.</param>
    private sealed class SharedReactiveStream<T>(
        Func<bool, IObservable<MitsubishiReactiveValue<T>>> streamFactory) : IDisposable
    {
        /// <summary>Stores the gate field.</summary>
        private readonly object _gate = new();

        /// <summary>Stores the subject field.</summary>
        private readonly ReplaySignal<MitsubishiReactiveValue<T>> _subject = new(1);

        /// <summary>Stores the streamFactory field.</summary>
        private readonly Func<bool, IObservable<MitsubishiReactiveValue<T>>> _streamFactory =
            streamFactory;

        /// <summary>Stores the connection field.</summary>
        private IDisposable? _connection;

        /// <summary>Stores the subscriberCount field.</summary>
        private int _subscriberCount;

        /// <summary>Stores the hasCachedValue field.</summary>
        private bool _hasCachedValue;

        /// <summary>Stores the disposed field.</summary>
        private bool _disposed;

        /// <summary>Gets or sets the Stream property.</summary>
        public IObservable<MitsubishiReactiveValue<T>> Stream =>
            Observable.Create<MitsubishiReactiveValue<T>>(Subscribe);

        /// <summary>Executes the Dispose operation.</summary>
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _connection?.Dispose();
                _connection = null;
                _subscriberCount = 0;
            }

            _subject.OnCompleted();
            _subject.Dispose();
        }

        /// <summary>Subscribes an observer to the shared stream.</summary>
        /// <param name="observer">The destination observer.</param>
        /// <returns>The subscription.</returns>
        private IDisposable Subscribe(IObserver<MitsubishiReactiveValue<T>> observer)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var subscription = _subject.Subscribe(observer);
            StartIfNeeded();
            return Disposable.Create(() =>
            {
                subscription.Dispose();
                StopIfUnused();
            });
        }

        /// <summary>Executes the StartIfNeeded operation.</summary>
        private void StartIfNeeded()
        {
            bool emitInitial = false;
            bool shouldStart = false;
            lock (_gate)
            {
                _subscriberCount++;
                if (_connection is null)
                {
                    emitInitial = !_hasCachedValue;
                    shouldStart = true;
                }
            }

            if (!shouldStart)
            {
                return;
            }

            var connection = _streamFactory(emitInitial)
                .Do(_ =>
                {
                    lock (_gate)
                    {
                        _hasCachedValue = true;
                    }
                })
                .Subscribe(_subject);
            lock (_gate)
            {
                if (_disposed || _connection is not null)
                {
                    connection.Dispose();
                    return;
                }

                _connection = connection;
            }
        }

        /// <summary>Executes the StopIfUnused operation.</summary>
        private void StopIfUnused()
        {
            lock (_gate)
            {
                _subscriberCount--;
                if (_subscriberCount <= 0)
                {
                    _subscriberCount = 0;
                    _connection?.Dispose();
                    _connection = null;
                }
            }
        }
    }

    /// <summary>Provides the ReactiveWordGroupPlan record.</summary>
    /// <param name="GroupName">The GroupName parameter.</param>
    /// <param name="StartAddress">The StartAddress parameter.</param>
    /// <param name="TotalWords">The TotalWords parameter.</param>
    /// <param name="Items">The Items parameter.</param>
    private sealed record ReactiveWordGroupPlan(
        string GroupName,
        MitsubishiDeviceAddress StartAddress,
        int TotalWords,
        IReadOnlyList<ReactiveWordGroupItem> Items);

    /// <summary>Provides the ReactiveWordGroupItem record.</summary>
    /// <param name="TagName">The TagName parameter.</param>
    /// <param name="Tag">The Tag parameter.</param>
    /// <param name="WordOffset">The WordOffset parameter.</param>
    /// <param name="WordCount">The WordCount parameter.</param>
    private sealed record ReactiveWordGroupItem(
        string TagName,
        MitsubishiTagDefinition Tag,
        int WordOffset,
        int WordCount);

    /// <summary>Provides one candidate range for a combined reactive group read.</summary>
    /// <param name="TagName">The logical tag name.</param>
    /// <param name="Tag">The tag definition.</param>
    /// <param name="Address">The parsed device address.</param>
    /// <param name="WordCount">The number of words occupied by the tag.</param>
    private sealed record ReactiveWordGroupCandidate(
        string TagName,
        MitsubishiTagDefinition Tag,
        MitsubishiDeviceAddress Address,
        int WordCount);
}
