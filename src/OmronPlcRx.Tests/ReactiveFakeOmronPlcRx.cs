// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using CP.IoT.Core;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
using ReactiveIOmronPlcRx = global::OmronPlcRx.Reactive.IOmronPlcRx;
using ReactiveIPlcTag = global::OmronPlcRx.Reactive.Tags.IPlcTag;
using ReactiveOmronPLCException = global::OmronPlcRx.Reactive.OmronPLCException;
using ReactivePLCType = global::OmronPlcRx.Reactive.Enums.PlcType;
using ReactiveReadClockResult = global::OmronPlcRx.Reactive.Results.ReadClockResult;
using ReactiveReadCycleTimeResult = global::OmronPlcRx.Reactive.Results.ReadCycleTimeResult;
using ReactiveWriteClockResult = global::OmronPlcRx.Reactive.Results.WriteClockResult;

namespace OmronPlcRx.Reactive.Tests;

/// <summary>In-memory reactive PLC test double used by generated stream tests.</summary>
public sealed class ReactiveFakeOmronPlcRx : ReactiveIOmronPlcRx
{
    /// <summary>Publishes PLC errors.</summary>
    private readonly Signal<ReactiveOmronPLCException?> _errors = new();

    /// <summary>Stores per-tag value subjects.</summary>
    private readonly Dictionary<string, BehaviorSignal<object?>> _subjects = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Stores the latest per-tag values.</summary>
    private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Publishes aggregate tag change notifications.</summary>
    private readonly Signal<ReactiveIPlcTag?> _all = new();

    /// <inheritdoc />
    public IObservable<ReactiveIPlcTag?> ObserveAll => _all;

    /// <inheritdoc />
    public IObservable<ReactiveOmronPLCException?> Errors => _errors;

    /// <inheritdoc />
    public ReactivePLCType PlcType => ReactivePLCType.Unknown;

    /// <inheritdoc />
    public string? ControllerModel => null;

    /// <inheritdoc />
    public string? ControllerVersion => null;

    /// <summary>Gets a value indicating whether this fake PLC is disposed.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>Gets the tag registrations captured by this fake PLC.</summary>
    public List<Registration> Registrations { get; } = [];

    /// <summary>Gets the writes captured by this fake PLC.</summary>
    public List<Write> Writes { get; } = [];

    /// <inheritdoc />
    public void AddUpdateTagItem<T>(global::OmronPlcRx.Reactive.Tags.PlcTag<T> tag)
    {
        Registrations.Add(new(tag.TagName, tag.Address, typeof(T)));
        _ = GetSubject(tag.TagName);
    }

    /// <inheritdoc />
    public bool RemoveTagItem(string tagName)
    {
        var removedSubject = _subjects.Remove(tagName);
        var removedValue = _values.Remove(tagName);
        return removedSubject || removedValue;
    }

    /// <inheritdoc />
    public IObservable<T?> Observe<T>(LogicalTagKey<T> tag) =>
        GetSubject(tag.Name).Select(value => value is null ? default : (T?)value);

    /// <inheritdoc />
    public T? GetValue<T>(LogicalTagKey<T> tag) =>
        _values.TryGetValue(tag.Name, out var value) && value is T typed
            ? typed
            : default;

    /// <inheritdoc />
    public Task<T?> ReadValueAsync<T>(
        LogicalTagKey<T> tag,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetValue(tag));
    }

    /// <inheritdoc />
    public void SetValue<T>(LogicalTagKey<T> tag, T? value)
    {
        Writes.Add(new(tag.Name, value, typeof(T)));
        Publish(tag.Name, value);
    }

    /// <inheritdoc />
    public Task WriteValueAsync<T>(
        LogicalTagKey<T> tag,
        T? value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetValue(tag, value);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ReactiveReadClockResult> ReadClockAsync(CancellationToken cancellationToken) =>
        Task.FromResult(default(ReactiveReadClockResult));

    /// <inheritdoc />
    public Task<ReactiveWriteClockResult> WriteClockAsync(
        DateTimeOffset newDateTime,
        CancellationToken cancellationToken) => Task.FromResult(default(ReactiveWriteClockResult));

    /// <inheritdoc />
    public Task<ReactiveWriteClockResult> WriteClockAsync(
        DateTimeOffset newDateTime,
        int newDayOfWeek,
        CancellationToken cancellationToken) => WriteClockAsync(newDateTime, cancellationToken);

    /// <inheritdoc />
    public Task<ReactiveReadCycleTimeResult> ReadCycleTimeAsync(CancellationToken cancellationToken) =>
        Task.FromResult(default(ReactiveReadCycleTimeResult));

    /// <inheritdoc />
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        foreach (var subject in _subjects.Values)
        {
            subject.OnCompleted();
            subject.Dispose();
        }

        _all.OnCompleted();
        _all.Dispose();
        _errors.OnCompleted();
        _errors.Dispose();
    }

    /// <summary>Publishes a tag value to observers.</summary>
    /// <typeparam name="T">The tag value type.</typeparam>
    /// <param name="tagName">The tag name.</param>
    /// <param name="value">The tag value.</param>
    public void Publish<T>(string tagName, T? value)
    {
        _values[tagName] = value;
        GetSubject(tagName).OnNext(value);
        _all.OnNext(null);
    }

    /// <summary>Gets or creates the subject for a tag.</summary>
    /// <param name="tagName">The tag name.</param>
    /// <returns>The tag value subject.</returns>
    private BehaviorSignal<object?> GetSubject(string tagName)
    {
        if (_subjects.TryGetValue(tagName, out var subject))
        {
            return subject;
        }

        subject = new(default);
        _subjects.Add(tagName, subject);
        return subject;
    }

    /// <summary>Captures a registered PLC tag.</summary>
    /// <param name="TagName">The tag name.</param>
    /// <param name="Address">The PLC address.</param>
    /// <param name="TagType">The tag value type.</param>
    public sealed record Registration(string TagName, string Address, Type TagType);

    /// <summary>Captures a PLC tag write.</summary>
    /// <param name="TagName">The tag name.</param>
    /// <param name="Value">The written value.</param>
    /// <param name="TagType">The tag value type.</param>
    public sealed record Write(string TagName, object? Value, Type TagType);
}
