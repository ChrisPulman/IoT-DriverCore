// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IoT.DriverCore.Core;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
#if REACTIVE_SHIM
using IoT.DriverCore.OmronPlcRx.Reactive.Enums;
using IoT.DriverCore.OmronPlcRx.Reactive.Results;
using IoT.DriverCore.OmronPlcRx.Reactive.Tags;
#else
using IoT.DriverCore.OmronPlcRx.Enums;
using IoT.DriverCore.OmronPlcRx.Results;
using IoT.DriverCore.OmronPlcRx.Tags;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive;
#else
namespace IoT.DriverCore.OmronPlcRx;
#endif

/// <summary>Provides a deterministic, in-memory Omron PLC through <see cref="IOmronPlcRx"/>.</summary>
public sealed class OmronPlcSimulator : IOmronPlcRx
{
    /// <summary>Stores registered typed tags.</summary>
    private readonly ConcurrentDictionary<string, IPlcTag> _tags =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Stores current tag values independently from the caller-owned tag instances.</summary>
    private readonly ConcurrentDictionary<string, object?> _values =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Stores per-tag observable signals.</summary>
    private readonly ConcurrentDictionary<string, BehaviorSignal<object?>> _subjects =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Stores queued deterministic faults.</summary>
    private readonly ConcurrentDictionary<OmronSimulatorOperation, ConcurrentQueue<SimulatorFault>> _faults = new();

    /// <summary>Stores completed operation records.</summary>
    private readonly ConcurrentQueue<OmronSimulatorOperationRecord> _operations = new();

    /// <summary>Publishes aggregate tag changes.</summary>
    private readonly Signal<IPlcTag?> _all = new();

    /// <summary>Publishes operation errors.</summary>
    private readonly Signal<OmronPLCException?> _errors = new();

    /// <summary>Synchronizes connection and disposal state.</summary>
    private readonly object _stateGate = new();

    /// <summary>Stores the next operation sequence number.</summary>
    private long _sequence;

    /// <summary>Stores whether the simulated transport is connected.</summary>
    private bool _connected;

    /// <summary>Stores whether the simulator has been disposed.</summary>
    private bool _disposed;

    /// <summary>Stores the simulated PLC clock.</summary>
    private DateTimeOffset _clock;

    /// <summary>Initializes a new instance of the <see cref="OmronPlcSimulator"/> class.</summary>
    public OmronPlcSimulator()
        : this(
            PlcType.NJ501,
            "NJ501-SIM",
            "1.0-SIM",
            true,
            new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="OmronPlcSimulator"/> class.</summary>
    /// <param name="initiallyConnected">Whether the simulated transport starts connected.</param>
    public OmronPlcSimulator(bool initiallyConnected)
        : this(
            PlcType.NJ501,
            "NJ501-SIM",
            "1.0-SIM",
            initiallyConnected,
            new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="OmronPlcSimulator"/> class.</summary>
    /// <param name="plcType">PLC model family reported to callers.</param>
    /// <param name="controllerModel">Controller model reported to callers.</param>
    /// <param name="controllerVersion">Controller version reported to callers.</param>
    /// <param name="initiallyConnected">Whether the simulated transport starts connected.</param>
    /// <param name="initialClock">Optional deterministic initial clock.</param>
    public OmronPlcSimulator(
        PlcType plcType,
        string controllerModel,
        string controllerVersion,
        bool initiallyConnected,
        DateTimeOffset initialClock)
    {
        ArgumentGuards.ThrowIfNullOrWhiteSpace(controllerModel, nameof(controllerModel));
        ArgumentGuards.ThrowIfNullOrWhiteSpace(controllerVersion, nameof(controllerVersion));

        PlcType = plcType;
        ControllerModel = controllerModel;
        ControllerVersion = controllerVersion;
        _connected = initiallyConnected;
        _clock = initialClock;
    }

    /// <inheritdoc />
    public IObservable<IPlcTag?> ObserveAll => _all;

    /// <inheritdoc />
    public IObservable<OmronPLCException?> Errors => _errors;

    /// <inheritdoc />
    public PlcType PlcType { get; }

    /// <inheritdoc />
    public string ControllerModel { get; }

    /// <inheritdoc />
    public string ControllerVersion { get; }

    /// <inheritdoc />
    public bool IsDisposed
    {
        get
        {
            lock (_stateGate)
            {
                return _disposed;
            }
        }
    }

    /// <summary>Gets a value indicating whether the simulated transport is connected.</summary>
    public bool IsConnected
    {
        get
        {
            lock (_stateGate)
            {
                return _connected && !_disposed;
            }
        }
    }

    /// <summary>Gets the number of successful reconnections.</summary>
    public int ReconnectCount { get; private set; }

    /// <summary>Gets a snapshot of completed simulator operations.</summary>
    public IReadOnlyList<OmronSimulatorOperationRecord> Operations => _operations.ToArray();

    /// <summary>Gets or sets simulated minimum cycle time in milliseconds.</summary>
    public double MinimumCycleTime { get; set; } = 1;

    /// <summary>Gets or sets simulated maximum cycle time in milliseconds.</summary>
    public double MaximumCycleTime { get; set; } = 3;

    /// <summary>Gets or sets simulated average cycle time in milliseconds.</summary>
    public double AverageCycleTime { get; set; } = 2;

    /// <summary>Connects the simulated transport.</summary>
    /// <returns>A task that represents the operation.</returns>
    public Task ConnectAsync() => ConnectAsync(CancellationToken.None);

    /// <summary>Connects the simulated transport.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents the operation.</returns>
    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        ThrowQueuedFault(OmronSimulatorOperation.Connect, null, null);

        lock (_stateGate)
        {
            _connected = true;
        }

        Record(OmronSimulatorOperation.Connect, null, null, true);
        return Task.CompletedTask;
    }

    /// <summary>Disconnects the simulated transport while retaining memory and registrations.</summary>
    public void Disconnect()
    {
        ThrowIfDisposed();
        lock (_stateGate)
        {
            _connected = false;
        }
    }

    /// <summary>Reconnects the simulated transport while retaining memory and registrations.</summary>
    /// <returns>A task that represents the operation.</returns>
    public Task ReconnectAsync() => ReconnectAsync(CancellationToken.None);

    /// <summary>Reconnects the simulated transport while retaining memory and registrations.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents the operation.</returns>
    public async Task ReconnectAsync(CancellationToken cancellationToken)
    {
        Disconnect();
        await ConnectAsync(cancellationToken).ConfigureAwait(false);
        ReconnectCount++;
    }

    /// <summary>Queues one disconnecting failure for the selected operation.</summary>
    /// <param name="operation">Operation to fail.</param>
    /// <param name="exception">Failure to wrap and publish.</param>
    public void QueueFault(OmronSimulatorOperation operation, Exception exception) =>
        QueueFault(operation, exception, 1, true);

    /// <summary>Queues deterministic failures for the selected operation.</summary>
    /// <param name="operation">Operation to fail.</param>
    /// <param name="exception">Failure to wrap and publish.</param>
    /// <param name="occurrences">Number of consecutive failures to queue.</param>
    /// <param name="disconnect">Whether each failure disconnects the simulated transport.</param>
    public void QueueFault(
        OmronSimulatorOperation operation,
        Exception exception,
        int occurrences,
        bool disconnect)
    {
        ThrowIfDisposed();
        if (exception is null)
        {
            throw new ArgumentNullException(nameof(exception));
        }

        if (occurrences <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(occurrences));
        }

        var queue = _faults.GetOrAdd(operation, static _ => new());
        for (var index = 0; index < occurrences; index++)
        {
            queue.Enqueue(new(exception, disconnect));
        }
    }

    /// <summary>Registers and seeds a typed tag in one deterministic operation.</summary>
    /// <typeparam name="T">Tag value type.</typeparam>
    /// <param name="tag">Tag definition.</param>
    /// <param name="value">Initial value.</param>
    public void Seed<T>(PlcTag<T> tag, T? value)
    {
        AddUpdateTagItem(tag);
        PublishValue(tag.TagName, value);
    }

    /// <inheritdoc />
    public void AddUpdateTagItem<T>(PlcTag<T> tag)
    {
        ThrowIfDisposed();
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        _tags[tag.TagName] = tag;
        _ = _subjects.GetOrAdd(
            tag.TagName,
            name => new BehaviorSignal<object?>(
                _values.TryGetValue(name, out var current) ? current : default));
        if (!_values.TryGetValue(tag.TagName, out var value))
        {
            return;
        }

        tag.Value = ConvertValue<T>(value);
    }

    /// <inheritdoc />
    public bool RemoveTagItem(string tagName)
    {
        ThrowIfDisposed();
        ArgumentGuards.ThrowIfNullOrWhiteSpace(tagName, nameof(tagName));
        var removedTag = _tags.TryRemove(tagName, out _);
        var removedValue = _values.TryRemove(tagName, out _);
        if (_subjects.TryRemove(tagName, out var subject))
        {
            subject.OnCompleted();
            subject.Dispose();
        }

        return removedTag || removedValue;
    }

    /// <inheritdoc />
    public IObservable<T?> Observe<T>(LogicalTagKey<T> tag)
    {
        ThrowIfDisposed();
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        var subject = _subjects.GetOrAdd(
            tag.Name,
            name => new BehaviorSignal<object?>(
                _values.TryGetValue(name, out var current) ? current : default));
        return subject.Select(ConvertValue<T>);
    }

    /// <inheritdoc />
    public T? GetValue<T>(LogicalTagKey<T> tag)
    {
        ThrowIfDisposed();
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        return _values.TryGetValue(tag.Name, out var value) ? ConvertValue<T>(value) : default;
    }

    /// <inheritdoc />
    public Task<T?> ReadValueAsync<T>(
        LogicalTagKey<T> tag,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        EnsureRegistered(tag);
        EnsureConnected();
        ThrowQueuedFault(OmronSimulatorOperation.Read, tag.Name, null);

        var value = GetValue(tag);
        Record(OmronSimulatorOperation.Read, tag.Name, value, true);
        return Task.FromResult(value);
    }

    /// <inheritdoc />
    public void SetValue<T>(LogicalTagKey<T> tag, T? value)
    {
        WriteValueAsync(tag, value, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public Task WriteValueAsync<T>(
        LogicalTagKey<T> tag,
        T? value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        EnsureRegistered(tag);
        EnsureConnected();
        ThrowQueuedFault(OmronSimulatorOperation.Write, tag.Name, value);

        PublishValue(tag.Name, value);
        Record(OmronSimulatorOperation.Write, tag.Name, value, true);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ReadClockResult> ReadClockAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        EnsureConnected();
        ThrowQueuedFault(OmronSimulatorOperation.ReadClock, null, null);

        var result = new ReadClockResult
        {
            PacketsSent = 1,
            PacketsReceived = 1,
            Clock = _clock,
            DayOfWeek = (int)_clock.DayOfWeek,
        };
        Record(OmronSimulatorOperation.ReadClock, null, result.Clock, true);
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<WriteClockResult> WriteClockAsync(
        DateTimeOffset newDateTime,
        CancellationToken cancellationToken) =>
        WriteClockAsync(newDateTime, (int)newDateTime.DayOfWeek, cancellationToken);

    /// <inheritdoc />
    public Task<WriteClockResult> WriteClockAsync(
        DateTimeOffset newDateTime,
        int newDayOfWeek,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        EnsureConnected();
        if (newDayOfWeek is < 0 or > ProtocolConstants.Six)
        {
            throw new ArgumentOutOfRangeException(nameof(newDayOfWeek));
        }

        ThrowQueuedFault(OmronSimulatorOperation.WriteClock, null, newDateTime);

        _clock = newDateTime.AddDays(newDayOfWeek - (int)newDateTime.DayOfWeek);
        Record(OmronSimulatorOperation.WriteClock, null, _clock, true);
        return Task.FromResult(new WriteClockResult
        {
            PacketsSent = 1,
            PacketsReceived = 1,
        });
    }

    /// <inheritdoc />
    public Task<ReadCycleTimeResult> ReadCycleTimeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        EnsureConnected();
        ThrowQueuedFault(OmronSimulatorOperation.ReadCycleTime, null, null);

        var minimumCycleTime = MinimumCycleTime;
        var maximumCycleTime = MaximumCycleTime;
        var averageCycleTime = AverageCycleTime;
        var result = new ReadCycleTimeResult
        {
            PacketsSent = 1,
            PacketsReceived = 1,
            MinimumCycleTime = minimumCycleTime,
            MaximumCycleTime = maximumCycleTime,
            AverageCycleTime = averageCycleTime,
        };
        Record(OmronSimulatorOperation.ReadCycleTime, null, result, true);
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_stateGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _connected = false;
        }

        foreach (var subject in _subjects.Values)
        {
            subject.OnCompleted();
            subject.Dispose();
        }

        _subjects.Clear();
        _all.OnCompleted();
        _errors.OnCompleted();
        _all.Dispose();
        _errors.Dispose();
    }

    /// <summary>Publishes and caches a tag value.</summary>
    /// <typeparam name="T">Tag value type.</typeparam>
    /// <param name="tagName">Logical tag name.</param>
    /// <param name="value">New value.</param>
    private void PublishValue<T>(string tagName, T? value)
    {
        _values[tagName] = value;
        if (_tags.TryGetValue(tagName, out var tag) && tag is PlcTag<T> typed)
        {
            typed.Value = value;
            _all.OnNext(typed);
        }

        _subjects.GetOrAdd(tagName, static _ => new(default)).OnNext(value);
    }

    /// <summary>Consumes and raises one queued fault.</summary>
    /// <param name="operation">Operation kind.</param>
    /// <param name="tagName">Optional logical tag name.</param>
    /// <param name="value">Optional operation value.</param>
    private void ThrowQueuedFault(OmronSimulatorOperation operation, string? tagName, object? value)
    {
        if (!_faults.TryGetValue(operation, out var queue) || !queue.TryDequeue(out var fault))
        {
            return;
        }

        if (fault.Disconnect)
        {
            lock (_stateGate)
            {
                _connected = false;
            }
        }

        var exception = fault.Exception as OmronPLCException
            ?? new OmronPLCException($"Simulated {operation} failure.", fault.Exception);
        Record(operation, tagName, value, false);
        _errors.OnNext(exception);
        throw exception;
    }

    /// <summary>Ensures that a logical tag exists with the requested type.</summary>
    /// <typeparam name="T">Tag value type.</typeparam>
    /// <param name="tag">Logical tag key.</param>
    private void EnsureRegistered<T>(LogicalTagKey<T> tag)
    {
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        _ = _tags.TryGetValue(tag.Name, out var registered) && registered.TagType == typeof(T)
            ? 0
            : throw new KeyNotFoundException($"Tag '{tag.Name}' not found or incorrect type.");
    }

    /// <summary>Ensures that the simulated transport is connected.</summary>
    private void EnsureConnected() =>
        _ = IsConnected
            ? 0
            : throw new OmronPLCException("The simulated Omron PLC is disconnected.");

    /// <summary>Throws when the simulator has been disposed.</summary>
    private void ThrowIfDisposed() =>
        _ = IsDisposed
            ? throw new ObjectDisposedException(nameof(OmronPlcSimulator))
            : 0;

    /// <summary>Records one completed operation.</summary>
    /// <param name="operation">Operation kind.</param>
    /// <param name="tagName">Optional logical tag name.</param>
    /// <param name="value">Optional operation value.</param>
    /// <param name="succeeded">Whether the operation succeeded.</param>
    private void Record(
        OmronSimulatorOperation operation,
        string? tagName,
        object? value,
        bool succeeded) =>
        _operations.Enqueue(new(
            Interlocked.Increment(ref _sequence),
            operation,
            tagName,
            value,
            succeeded));

    /// <summary>Converts a cached simulator value to a requested tag type.</summary>
    /// <typeparam name="T">Requested tag type.</typeparam>
    /// <param name="value">Cached value.</param>
    /// <returns>The typed value.</returns>
    private T? ConvertValue<T>(object? value) =>
        value switch
        {
            null => default,
            T typed => typed,
            _ => (T)Convert.ChangeType(value, typeof(T)),
        };

    /// <summary>Provides framework-compatible simulator argument validation.</summary>
    private static class ArgumentGuards
    {
        /// <summary>Throws when a required string is null, empty, or whitespace.</summary>
        /// <param name="value">Value to validate.</param>
        /// <param name="parameterName">Public parameter name.</param>
        internal static void ThrowIfNullOrWhiteSpace(string? value, string parameterName)
        {
#if NET6_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
#else
            switch (value)
            {
                case null:
                    throw new ArgumentNullException(parameterName);
                case var text when text.Trim().Length == 0:
                    throw new ArgumentException("The value cannot be empty or whitespace.", parameterName);
            }
#endif
        }
    }

    /// <summary>Stores one queued deterministic fault.</summary>
    /// <param name="Exception">Failure to raise.</param>
    /// <param name="Disconnect">Whether the failure disconnects the simulator.</param>
    private sealed record SimulatorFault(Exception Exception, bool Disconnect);
}
