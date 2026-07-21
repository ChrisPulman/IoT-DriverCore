// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using CP.IoT.Core;
using OmronPlcRx.Enums;
using OmronPlcRx.Results;
using OmronPlcRx.Tags;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace OmronPlcRx.Tests;

/// <summary>In-memory PLC test double used by generated stream tests.</summary>
public sealed class FakeOmronPlcRx : IOmronPlcRx
{
    /// <summary>Publishes PLC errors.</summary>
    private readonly Signal<OmronPLCException?> _errors = new();

    /// <summary>Stores per-tag value subjects.</summary>
    private readonly Dictionary<string, BehaviorSignal<object?>> _subjects = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Stores the latest per-tag values.</summary>
    private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Publishes aggregate tag change notifications.</summary>
    private readonly Signal<IPlcTag?> _all = new();

    /// <inheritdoc />
    public IObservable<IPlcTag?> ObserveAll => _all;

    /// <inheritdoc />
    public IObservable<OmronPLCException?> Errors => _errors;

    /// <inheritdoc />
    public PlcType PlcType => PlcType.Unknown;

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
    public void AddUpdateTagItem<T>(PlcTag<T> tag)
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
    public Task<ReadClockResult> ReadClockAsync(CancellationToken cancellationToken) =>
        Task.FromResult(default(ReadClockResult));

    /// <inheritdoc />
    public Task<WriteClockResult> WriteClockAsync(
        DateTimeOffset newDateTime,
        CancellationToken cancellationToken) => Task.FromResult(default(WriteClockResult));

    /// <inheritdoc />
    public Task<WriteClockResult> WriteClockAsync(
        DateTimeOffset newDateTime,
        int newDayOfWeek,
        CancellationToken cancellationToken) => WriteClockAsync(newDateTime, cancellationToken);

    /// <inheritdoc />
    public Task<ReadCycleTimeResult> ReadCycleTimeAsync(CancellationToken cancellationToken) =>
        Task.FromResult(default(ReadCycleTimeResult));

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
