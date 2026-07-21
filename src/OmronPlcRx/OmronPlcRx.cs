// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CP.IoT.Core;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
#if REACTIVE_SHIM
using OmronPlcRx.Reactive.Core;
using OmronPlcRx.Reactive.Enums;
using OmronPlcRx.Reactive.Results;
using OmronPlcRx.Reactive.Tags;
#else
using OmronPlcRx.Core;
using OmronPlcRx.Enums;
using OmronPlcRx.Results;
using OmronPlcRx.Tags;
#endif

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive;

#else
namespace OmronPlcRx;

#endif

/// <summary>
/// Provides typed tag storage, polling, and observation for an Omron PLC connection.
/// Implements <see cref="IOmronPlcRx"/> so individual tag streams and a multiplex stream can be consumed.
/// </summary>
public sealed partial class OmronPlcRx : IOmronPlcRx
{
    /// <summary>Stores the p lc value.</summary>
    private readonly OmronPLCConnection _plc;

    /// <summary>Stores the p ol li nt er va l value.</summary>
    private readonly TimeSpan _pollInterval;

    /// <summary>Executes the e nt ri es operation.</summary>
    private readonly ConcurrentDictionary<string, ITagEntry> _entries = new(
        StringComparer.OrdinalIgnoreCase);

    /// <summary>Executes the s ub je ct s operation.</summary>
    private readonly ConcurrentDictionary<string, BehaviorSignal<object?>> _subjects = new(
        StringComparer.OrdinalIgnoreCase);

    /// <summary>Executes the t ag ch an ge d operation.</summary>
    private readonly Signal<IPlcTag?> _tagChanged = new();

    /// <summary>Executes the e rr or s operation.</summary>
    private readonly Signal<OmronPLCException?> _errors = new();

    /// <summary>Executes the c ts operation.</summary>
    private readonly CancellationTokenSource _cts = new();

    /// <summary>Stores the p ol ll oo p value.</summary>
    private readonly Task _pollLoop;

    /// <summary>Stores the d is po se d value.</summary>
    private bool _disposed;

    /// <summary>Stores the p lc in it ia li ze d value.</summary>
    private volatile bool _plcInitialized;

    /// <summary>Initializes a new instance of the <see cref="OmronPlcRx" /> class.</summary>
    /// <param name="options">Transport and request options.</param>
    /// <param name="pollInterval">Polling interval (default 100 ms).</param>
    public OmronPlcRx(
        OmronConnectionOptions options,
        TimeSpan? pollInterval)
    {
        _plc = new(options);
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(ProtocolConstants.OneHundred);
        _pollLoop = Task.Run(PollLoopAsync);
    }

    /// <summary>Initializes a new instance of the <see cref="OmronPlcRx"/> class.</summary>
    /// <param name="localNodeId">The local node identifier.</param>
    /// <param name="remoteNodeId">The remote node identifier.</param>
    /// <param name="serialOptions">The serial FINS options.</param>
    /// <param name="timeout">The timeout.</param>
    /// <param name="retries">The retries.</param>
    /// <param name="pollInterval">Polling interval (default 100 ms).</param>
    public OmronPlcRx(
        byte localNodeId,
        byte remoteNodeId,
        OmronSerialOptions serialOptions,
            int timeout,
            int retries,
            TimeSpan? pollInterval)
    {
        if (serialOptions is null)
        {
            throw new ArgumentNullException(nameof(serialOptions));
        }

        _plc = new(
            new OmronConnectionOptions(
                localNodeId,
                remoteNodeId,
                ConnectionMethod.Serial,
                serialOptions.PortName)
            {
                Port = 0,
                Timeout = timeout,
                Retries = retries,
                SerialOptions = serialOptions,
            });
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(ProtocolConstants.OneHundred);
        _pollLoop = Task.Run(PollLoopAsync);
    }

    /// <inheritdoc />
    public IObservable<IPlcTag?> ObserveAll => _tagChanged;

    /// <inheritdoc />
    public IObservable<OmronPLCException?> Errors => _errors;

    /// <inheritdoc />
    public bool IsDisposed => _disposed;

    /// <summary>Gets the plc type value.</summary>
    /// <value>
    /// The type of the PLC.
    /// </value>
    public PlcType PlcType => _plc.PlcType;

    /// <summary>Gets the controller model value.</summary>
    public string? ControllerModel => _plc.ControllerModel;

    /// <summary>Gets the controller version value.</summary>
    public string? ControllerVersion => _plc.ControllerVersion;

    /// <summary>Reads the PLC real-time clock via the underlying connection.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Clock read result.</returns>
    public Task<ReadClockResult> ReadClockAsync(CancellationToken cancellationToken) =>
        _plc.ReadClockAsync(cancellationToken);

    /// <summary>Writes the PLC real-time clock (day-of-week inferred) via the underlying connection.</summary>
    /// <param name="newDateTime">New date/time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Clock write result.</returns>
    public Task<WriteClockResult> WriteClockAsync(
        DateTimeOffset newDateTime,
        CancellationToken cancellationToken) => _plc.WriteClockAsync(newDateTime, cancellationToken);

    /// <summary>Writes the PLC real-time clock with explicit day-of-week via the underlying connection.</summary>
    /// <param name="newDateTime">New date/time.</param>
    /// <param name="newDayOfWeek">Day of week (0-6).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Clock write result.</returns>
    public Task<WriteClockResult> WriteClockAsync(
        DateTimeOffset newDateTime,
        int newDayOfWeek,
        CancellationToken cancellationToken) => _plc.WriteClockAsync(newDateTime, newDayOfWeek, cancellationToken);

    /// <summary>Reads PLC scan cycle time statistics via the underlying connection.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cycle time statistics.</returns>
    public Task<ReadCycleTimeResult> ReadCycleTimeAsync(
        CancellationToken cancellationToken) => _plc.ReadCycleTimeAsync(cancellationToken);

    /// <inheritdoc />
    /// <typeparam name="T">The t value type.</typeparam>
    /// <param name="tag">The typed PLC tag.</param>
    public void AddUpdateTagItem<T>(PlcTag<T> tag)
    {
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        var entry = new TagEntry<T>(tag);
        _ = _entries.AddOrUpdate(tag.TagName, entry, (_, __) => entry);
        _ = _subjects.GetOrAdd(tag.TagName, _ => new(default));
    }

    /// <inheritdoc />
    public bool RemoveTagItem(string tagName)
    {
        ThrowIfNullOrWhiteSpace(tagName, nameof(tagName));
        return _entries.TryRemove(tagName, out _);
    }

    /// <inheritdoc />
    /// <typeparam name="T">The t value type.</typeparam>
    /// <param name="tag">The typed PLC tag.</param>
    public IObservable<T?> Observe<T>(LogicalTagKey<T> tag)
    {
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        var subject = _subjects.GetOrAdd(tag.Name, _ => new(default));
        return subject.Select(v => v is null ? default : (T?)ConvertTo<T>(v));
    }

    /// <inheritdoc />
    /// <typeparam name="T">The t value type.</typeparam>
    /// <param name="tag">The typed PLC tag.</param>
    public T? GetValue<T>(LogicalTagKey<T> tag)
    {
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        return !_entries.TryGetValue(tag.Name, out var entry)
            || entry is not TagEntry<T> typed
            || typed.Tag is not PlcTag<T> plcTag
            ? default
            : plcTag.Value;
    }

    /// <inheritdoc />
    public async Task<T?> ReadValueAsync<T>(
        LogicalTagKey<T> tag,
        CancellationToken cancellationToken)
    {
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        if (!_entries.TryGetValue(tag.Name, out var entry) || entry is not TagEntry<T> typed)
        {
            throw new KeyNotFoundException($"Tag '{tag.Name}' not found or incorrect type.");
        }

        var changed = await typed.ReadAsync(_plc, cancellationToken).ConfigureAwait(false);
        PublishChangedTag(tag.Name, typed, changed);
        return typed.Tag is PlcTag<T> plcTag ? plcTag.Value : default;
    }

    /// <inheritdoc />
    /// <typeparam name="T">The t value type.</typeparam>
    /// <param name="tag">The typed PLC tag.</param>
    /// <param name="value">The value to write.</param>
    public void SetValue<T>(LogicalTagKey<T> tag, T? value)
    {
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        if (!_entries.TryGetValue(tag.Name, out var entry) || entry is not TagEntry<T> typed)
        {
            throw new KeyNotFoundException($"Tag '{tag.Name}' not found or incorrect type.");
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await WriteValueAsync(typed, value, _cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _errors.OnNext(new OmronPLCException($"Failed to write tag '{tag.Name}'", ex));
            }
        });
    }

    /// <inheritdoc />
    public async Task WriteValueAsync<T>(
        LogicalTagKey<T> tag,
        T? value,
        CancellationToken cancellationToken)
    {
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        if (!_entries.TryGetValue(tag.Name, out var entry) || entry is not TagEntry<T> typed)
        {
            throw new KeyNotFoundException($"Tag '{tag.Name}' not found or incorrect type.");
        }

        await WriteValueAsync(typed, value, cancellationToken).ConfigureAwait(false);
        if (typed.Tag is not PlcTag<T> plcTag)
        {
            return;
        }

        plcTag.Value = value;
        if (_subjects.TryGetValue(tag.Name, out var subject))
        {
            subject.OnNext(value);
        }

        _tagChanged.OnNext(plcTag);
    }

    /// <summary>Dispose pattern.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        try
        {
            _ = _pollLoop.Wait(TimeSpan.FromSeconds(ProtocolConstants.Two));
        }
        catch (AggregateException ex)
        {
            ex.Handle(static inner => inner is OperationCanceledException);
        }

        foreach (var bs in _subjects.Values)
        {
            bs.OnCompleted();
            bs.Dispose();
        }

        _tagChanged.OnCompleted();
        _errors.OnCompleted();
        _tagChanged.Dispose();
        _errors.Dispose();
        _plc.Dispose();
        _cts.Dispose();
    }

    /// <summary>Initializes a new instance of the <see cref="PollLoopAsync"/> class.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task PollLoopAsync()
    {
        await InitializePlcForPollingAsync().ConfigureAwait(false);

        while (!_cts.IsCancellationRequested)
        {
            await PollEntriesOnceAsync().ConfigureAwait(false);
            if (!await DelayUntilNextPollAsync().ConfigureAwait(false))
            {
                break;
            }
        }
    }

    /// <summary>Initializes the PLC before polling starts.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task InitializePlcForPollingAsync()
    {
        if (_plcInitialized)
        {
            return;
        }

        try
        {
            await _plc.InitializeAsync(_cts.Token).ConfigureAwait(false);
            _plcInitialized = true;
        }
        catch (Exception ex)
        {
            _errors.OnNext(new OmronPLCException("PLC initialization failed", ex));
        }
    }

    /// <summary>Polls all registered tag entries once.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task PollEntriesOnceAsync()
    {
        try
        {
            foreach (var kvp in _entries)
            {
                if (_cts.IsCancellationRequested)
                {
                    break;
                }

                await PollEntryAsync(kvp.Key, kvp.Value).ConfigureAwait(false);
            }
        }
        catch (Exception loopEx)
        {
            _errors.OnNext(new OmronPLCException("Polling loop failure", loopEx));
        }
    }

    /// <summary>Polls one tag entry.</summary>
    /// <param name="name">The tag name.</param>
    /// <param name="entry">The tag entry.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task PollEntryAsync(string name, ITagEntry entry)
    {
        try
        {
            var changed = await entry.ReadAsync(_plc, _cts.Token).ConfigureAwait(false);
            PublishChangedTag(name, entry, changed);
        }
        catch (OmronPLCException ex)
        {
            _errors.OnNext(new OmronPLCException(ex.Message, ex));
        }
        catch (Exception ex)
        {
            _errors.OnNext(new OmronPLCException($"Unexpected error reading tag '{name}'", ex));
        }
    }

    /// <summary>Publishes changed tag values to observers.</summary>
    /// <param name="name">The tag name.</param>
    /// <param name="entry">The tag entry.</param>
    /// <param name="changed">A value indicating whether the entry changed.</param>
    private void PublishChangedTag(string name, ITagEntry entry, bool changed)
    {
        if (!changed || entry.Tag is not IPlcTag tag)
        {
            return;
        }

        if (_subjects.TryGetValue(name, out var subject))
        {
            subject.OnNext(tag.Value);
        }

        _tagChanged.OnNext(tag);
    }

    /// <summary>Delays until the next poll interval.</summary>
    /// <returns>A value indicating whether polling should continue.</returns>
    private async Task<bool> DelayUntilNextPollAsync()
    {
        try
        {
            await Task.Delay(_pollInterval, _cts.Token).ConfigureAwait(false);
            return true;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>Executes the w ri te va lu ea sy nc operation.</summary>
    /// <typeparam name="T">The t value type.</typeparam>
    /// <param name="entry">The e nt ry value.</param>
    /// <param name="value">The v al ue value.</param>
    /// <param name="ct">The c t value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task WriteValueAsync<T>(TagEntry<T> entry, T? value, CancellationToken ct)
    {
        if (value is null)
        {
            return;
        }

        if (typeof(T) == typeof(string))
        {
            await WriteStringValueAsync(entry.Tag.Address, value, ct).ConfigureAwait(false);
            return;
        }

        var (area, addr, bitIndex) = ParseAddress(entry.Tag.Address);
        await WriteNonStringValueAsync(typeof(T), (object)value!, area, addr, bitIndex, ct)
            .ConfigureAwait(false);
    }

    /// <summary>Writes a string value to word memory.</summary>
    /// <param name="address">The tag address.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task WriteStringValueAsync(string address, object value, CancellationToken ct)
    {
        var (baseAddr, length) = ExtractStringMeta(address);
        var (area, addr, bitIndex) = ParseAddress(baseAddr);
        PlcTagValueCodec.ThrowIfBitIndexedString(bitIndex);
        var words = PlcTagValueCodec.GetStringWords(value, length);
        await _plc.WriteWordsAsync(words, addr, ToWordType(area), ct).ConfigureAwait(false);
    }

    /// <summary>Writes a non-string value to PLC memory.</summary>
    /// <param name="type">The value type.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="area">The memory area.</param>
    /// <param name="addr">The memory address.</param>
    /// <param name="bitIndex">The optional bit index.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task WriteNonStringValueAsync(
        Type type,
        object value,
        string area,
        ushort addr,
        byte? bitIndex,
        CancellationToken ct)
    {
        if (type == typeof(bool))
        {
            await WriteBooleanValueAsync(Convert.ToBoolean(value), area, addr, bitIndex, ct)
                .ConfigureAwait(false);
            return;
        }

        if (PlcTagValueCodec.TryGetSingleWord(type, value, out var word))
        {
            await _plc.WriteWordsAsync([word], addr, ToWordType(area), ct).ConfigureAwait(false);
            return;
        }

        if (!PlcTagValueCodec.TryGetWordArray(type, value, out var words))
        {
            return;
        }

        await _plc.WriteWordsAsync(words, addr, ToWordType(area), ct).ConfigureAwait(false);
    }

    /// <summary>Writes a Boolean value as either a bit or word.</summary>
    /// <param name="value">The Boolean value.</param>
    /// <param name="area">The memory area.</param>
    /// <param name="addr">The memory address.</param>
    /// <param name="bitIndex">The optional bit index.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task WriteBooleanValueAsync(
        bool value,
        string area,
        ushort addr,
        byte? bitIndex,
        CancellationToken ct)
    {
        if (bitIndex is null)
        {
            await _plc.WriteWordsAsync([(short)(value ? 1 : 0)], addr, ToWordType(area), ct)
                .ConfigureAwait(false);
            return;
        }

        await _plc.WriteBitsAsync([value], addr, bitIndex.Value, ToBitType(area), ct)
            .ConfigureAwait(false);
    }
}
