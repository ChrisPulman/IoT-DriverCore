// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace IoT.DriverCore.Core;

/// <summary>Implements logical-tag contracts over a composed memory image, transfer planner, script, and clock.</summary>
public sealed class SimulatorLogicalTagClient : ILogicalTagClient
{
    /// <summary>The quality assigned to successful values.</summary>
    private const string GoodQuality = "Good";

    /// <summary>Contains bindings keyed by ordinal logical tag name.</summary>
    private readonly ReadOnlyDictionary<string, SimulatorTagBinding> _bindings;

    /// <summary>Provides timestamps for logical results.</summary>
    private readonly ISimulatorClock _clock;

    /// <summary>Initializes a new instance of the <see cref="SimulatorLogicalTagClient"/> class.</summary>
    /// <param name="bindings">The logical-tag bindings exposed by the client.</param>
    /// <param name="memory">The shared simulator memory image.</param>
    /// <param name="planner">The capability-aware planner used for every batch.</param>
    public SimulatorLogicalTagClient(
        IEnumerable<SimulatorTagBinding> bindings,
        SimulatorMemoryImage memory,
        TagTransferPlanner planner)
        : this(bindings, memory, planner, new SimulatorScript(), SystemSimulatorClock.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SimulatorLogicalTagClient"/> class.</summary>
    /// <param name="bindings">The logical-tag bindings exposed by the client.</param>
    /// <param name="memory">The shared simulator memory image.</param>
    /// <param name="planner">The capability-aware planner used for every batch.</param>
    /// <param name="script">The deterministic operation script.</param>
    /// <param name="clock">The clock used to timestamp logical reads and writes.</param>
    public SimulatorLogicalTagClient(
        IEnumerable<SimulatorTagBinding> bindings,
        SimulatorMemoryImage memory,
        TagTransferPlanner planner,
        SimulatorScript script,
        ISimulatorClock clock)
    {
        if (bindings is null)
        {
            throw new ArgumentNullException(nameof(bindings));
        }

        Memory = memory ?? throw new ArgumentNullException(nameof(memory));
        Planner = planner ?? throw new ArgumentNullException(nameof(planner));
        Script = script ?? throw new ArgumentNullException(nameof(script));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        var copy = new Dictionary<string, SimulatorTagBinding>(StringComparer.Ordinal);
        foreach (var binding in bindings)
        {
            if (binding is null)
            {
                throw new ArgumentException("Bindings cannot contain null entries.", nameof(bindings));
            }

            if (copy.ContainsKey(binding.Tag.Name))
            {
                throw new ArgumentException($"The tag '{binding.Tag.Name}' is bound more than once.", nameof(bindings));
            }

            copy.Add(binding.Tag.Name, binding);
        }

        _bindings = new(copy);
    }

    /// <summary>Gets the shared simulator memory image.</summary>
    public SimulatorMemoryImage Memory { get; }

    /// <summary>Gets the capability-aware transfer planner.</summary>
    public TagTransferPlanner Planner { get; }

    /// <summary>Gets the deterministic latency and fault script.</summary>
    public SimulatorScript Script { get; }

    /// <summary>Gets the logical-tag bindings keyed with ordinal tag-name comparison.</summary>
    public IReadOnlyDictionary<string, SimulatorTagBinding> Bindings => _bindings;

    /// <inheritdoc/>
    public async Task<TagOperationResult<LogicalTagValue>> ReadAsync(
        string tagName,
        CancellationToken cancellationToken)
    {
        var results = await ReadManyAsync([tagName], cancellationToken).ConfigureAwait(false);
        return results[0];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> ReadManyAsync(
        IReadOnlyCollection<string> tagNames,
        CancellationToken cancellationToken)
    {
        var names = ValidateNames(tagNames, nameof(tagNames));
        cancellationToken.ThrowIfCancellationRequested();
        var results = new TagOperationResult<LogicalTagValue>?[names.Length];
        var pending = new List<PendingRead>();
        for (var index = 0; index < names.Length; index++)
        {
            if (!_bindings.TryGetValue(names[index], out var binding))
            {
                results[index] = Failure(names[index], "is not registered");
            }
            else if (binding.Tag.AccessMode == LogicalTagAccessMode.Write)
            {
                results[index] = Failure(names[index], "does not permit reads");
            }
            else
            {
                pending.Add(new(index, binding));
            }
        }

        var plan = Planner.Plan(pending.Select(item =>
            new TagTransferRequest(item.Binding.Tag.Name, item.Binding.GetAddress(TagTransferAccess.Read))));
        foreach (var range in plan.Ranges)
        {
            var outcome = await Script.NextAsync(SimulatorOperationKind.Read, cancellationToken).ConfigureAwait(false);
            foreach (var item in range.Items)
            {
                var operation = pending[item.InputIndex];
                results[operation.OutputIndex] = outcome.Succeeded
                    ? ReadSuccess(operation.Binding)
                    : TagOperationResult<LogicalTagValue>.Failure(outcome.Error);
            }
        }

        return ToReadOnlyResults(results);
    }

    /// <inheritdoc/>
    public async Task<TagOperationResult<LogicalTagValue>> WriteAsync(
        LogicalTagValue value,
        CancellationToken cancellationToken)
    {
        var results = await WriteManyAsync([value], cancellationToken).ConfigureAwait(false);
        return results[0];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> WriteManyAsync(
        IReadOnlyCollection<LogicalTagValue> values,
        CancellationToken cancellationToken)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var inputs = values.ToArray();
        var results = new TagOperationResult<LogicalTagValue>?[inputs.Length];
        var pending = new List<PendingWrite>();
        for (var index = 0; index < inputs.Length; index++)
        {
            var value = inputs[index]
                ?? throw new ArgumentException("Values cannot contain null entries.", nameof(values));
            if (!_bindings.TryGetValue(value.TagName, out var binding))
            {
                results[index] = Failure(value.TagName, "is not registered");
            }
            else if (binding.Tag.AccessMode == LogicalTagAccessMode.Read)
            {
                results[index] = Failure(value.TagName, "does not permit writes");
            }
            else
            {
                pending.Add(new(index, binding, value));
            }
        }

        var plan = Planner.Plan(pending.Select(item =>
            new TagTransferRequest(item.Binding.Tag.Name, item.Binding.GetAddress(TagTransferAccess.Write))));
        foreach (var range in plan.Ranges)
        {
            var outcome = await Script.NextAsync(SimulatorOperationKind.Write, cancellationToken).ConfigureAwait(false);
            foreach (var item in range.Items)
            {
                var operation = pending[item.InputIndex];
                if (outcome.Succeeded)
                {
                    var accepted = new LogicalTagValue(
                        operation.Binding.Tag.Name,
                        operation.Value.Value,
                        _clock.UtcNow,
                        GoodQuality);
                    _ = Memory.Write(
                        operation.Binding.GetAddress(TagTransferAccess.Write),
                        operation.Binding.Encode(operation.Value.Value));
                    results[operation.OutputIndex] = TagOperationResult<LogicalTagValue>.Success(accepted);
                }
                else
                {
                    results[operation.OutputIndex] = TagOperationResult<LogicalTagValue>.Failure(outcome.Error);
                }
            }
        }

        return ToReadOnlyResults(results);
    }

    /// <inheritdoc/>
    public IObservable<LogicalTagValue> Observe(string tagName) => ObserveMany([tagName]);

    /// <inheritdoc/>
    public IObservable<LogicalTagValue> ObserveMany(IReadOnlyCollection<string> tagNames)
    {
        var names = ValidateNames(tagNames, nameof(tagNames));
        var bindings = new SimulatorTagBinding[names.Length];
        for (var index = 0; index < names.Length; index++)
        {
            if (!_bindings.TryGetValue(names[index], out var binding))
            {
                throw new ArgumentException($"Tag '{names[index]}' is not registered.", nameof(tagNames));
            }

            bindings[index] = binding;
        }

        return new LogicalChangeObservable(Memory, bindings);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<LogicalTagValue> ObserveAsync(
        string tagName,
        CancellationToken cancellationToken) =>
        new ObservableAsyncEnumerable<LogicalTagValue>(Observe(tagName), cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<LogicalTagValue> ObserveManyAsync(
        IReadOnlyCollection<string> tagNames,
        CancellationToken cancellationToken) =>
        new ObservableAsyncEnumerable<LogicalTagValue>(ObserveMany(tagNames), cancellationToken);

    /// <summary>Validates and materializes a logical tag-name collection.</summary>
    /// <param name="tagNames">The source collection.</param>
    /// <param name="parameterName">The source parameter name.</param>
    /// <returns>The validated names.</returns>
    private static string[] ValidateNames(IReadOnlyCollection<string> tagNames, string parameterName)
    {
        if (tagNames is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        return tagNames.Select(name => LogicalTag.Required(name, parameterName)).ToArray();
    }

    /// <summary>Creates a consistent logical-tag failure result.</summary>
    /// <param name="tagName">The tag name.</param>
    /// <param name="reason">The failure reason.</param>
    /// <returns>The failed result.</returns>
    private static TagOperationResult<LogicalTagValue> Failure(string tagName, string reason) =>
        TagOperationResult<LogicalTagValue>.Failure($"Tag '{tagName}' {reason}.");

    /// <summary>Converts a populated result array to an immutable collection.</summary>
    /// <param name="results">The populated result array.</param>
    /// <returns>The immutable results.</returns>
    private static ReadOnlyCollection<TagOperationResult<LogicalTagValue>> ToReadOnlyResults(
        TagOperationResult<LogicalTagValue>?[] results) =>
        new ReadOnlyCollection<TagOperationResult<LogicalTagValue>>(
            results.Select(result => result
                ?? throw new InvalidOperationException("A simulator operation did not produce a result.")).ToArray());

    /// <summary>Reads and decodes one successful logical result.</summary>
    /// <param name="binding">The source binding.</param>
    /// <returns>The successful result.</returns>
    private TagOperationResult<LogicalTagValue> ReadSuccess(SimulatorTagBinding binding)
    {
        var bytes = Memory.Read(binding.GetAddress(TagTransferAccess.Read));
        var value = binding.Decode(bytes);
        return TagOperationResult<LogicalTagValue>.Success(
            new(binding.Tag.Name, value, _clock.UtcNow, GoodQuality));
    }

    /// <summary>Correlates one planned read with its output position.</summary>
    private sealed class PendingRead
    {
        /// <summary>Initializes a new instance of the <see cref="PendingRead"/> class.</summary>
        /// <param name="outputIndex">The output position.</param>
        /// <param name="binding">The tag binding.</param>
        internal PendingRead(int outputIndex, SimulatorTagBinding binding)
        {
            OutputIndex = outputIndex;
            Binding = binding;
        }

        /// <summary>Gets the tag binding.</summary>
        internal SimulatorTagBinding Binding { get; }

        /// <summary>Gets the output position.</summary>
        internal int OutputIndex { get; }
    }

    /// <summary>Correlates one planned write with its output position and value.</summary>
    private sealed class PendingWrite
    {
        /// <summary>Initializes a new instance of the <see cref="PendingWrite"/> class.</summary>
        /// <param name="outputIndex">The output position.</param>
        /// <param name="binding">The tag binding.</param>
        /// <param name="value">The value to write.</param>
        internal PendingWrite(int outputIndex, SimulatorTagBinding binding, LogicalTagValue value)
        {
            OutputIndex = outputIndex;
            Binding = binding;
            Value = value;
        }

        /// <summary>Gets the tag binding.</summary>
        internal SimulatorTagBinding Binding { get; }

        /// <summary>Gets the output position.</summary>
        internal int OutputIndex { get; }

        /// <summary>Gets the value to write.</summary>
        internal LogicalTagValue Value { get; }
    }

    /// <summary>Creates independently filtered logical subscriptions over memory changes.</summary>
    private sealed class LogicalChangeObservable : IObservable<LogicalTagValue>
    {
        /// <summary>The bindings included in the subscription.</summary>
        private readonly SimulatorTagBinding[] _bindings;

        /// <summary>The source memory image.</summary>
        private readonly SimulatorMemoryImage _memory;

        /// <summary>Initializes a new instance of the <see cref="LogicalChangeObservable"/> class.</summary>
        /// <param name="memory">The source memory image.</param>
        /// <param name="bindings">The filtered bindings.</param>
        internal LogicalChangeObservable(SimulatorMemoryImage memory, SimulatorTagBinding[] bindings)
        {
            _memory = memory;
            _bindings = bindings;
        }

        /// <inheritdoc/>
        public IDisposable Subscribe(IObserver<LogicalTagValue> observer)
        {
            if (observer is null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            return _memory.Subscribe(new LogicalChangeObserver(observer, _bindings));
        }
    }

    /// <summary>Decodes matching memory changes for one logical observer.</summary>
    private sealed class LogicalChangeObserver : IObserver<SimulatorMemoryChange>
    {
        /// <summary>The filtered bindings.</summary>
        private readonly SimulatorTagBinding[] _bindings;

        /// <summary>The destination logical observer.</summary>
        private readonly IObserver<LogicalTagValue> _observer;

        /// <summary>Initializes a new instance of the <see cref="LogicalChangeObserver"/> class.</summary>
        /// <param name="observer">The destination observer.</param>
        /// <param name="bindings">The filtered bindings.</param>
        internal LogicalChangeObserver(
            IObserver<LogicalTagValue> observer,
            SimulatorTagBinding[] bindings)
        {
            _observer = observer;
            _bindings = bindings;
        }

        /// <inheritdoc/>
        public void OnCompleted() => _observer.OnCompleted();

        /// <inheritdoc/>
        public void OnError(Exception error) => _observer.OnError(error);

        /// <inheritdoc/>
        public void OnNext(SimulatorMemoryChange value)
        {
            foreach (var binding in _bindings)
            {
                if (SameLocation(binding.Address, value.Address))
                {
                    _observer.OnNext(new(
                        binding.Tag.Name,
                        binding.Decode(value.CurrentBytes),
                        value.TimestampUtc,
                        GoodQuality));
                }
            }
        }

        /// <summary>Returns whether two addresses identify the same exact physical byte range.</summary>
        /// <param name="left">The first address.</param>
        /// <param name="right">The second address.</param>
        /// <returns><see langword="true"/> when the physical locations match.</returns>
        private static bool SameLocation(TagTransportAddress left, TagTransportAddress right) =>
            StringComparer.Ordinal.Equals(left.TransportPartition, right.TransportPartition)
            && StringComparer.Ordinal.Equals(left.MemoryArea, right.MemoryArea)
            && StringComparer.Ordinal.Equals(left.Route, right.Route)
            && left.Offset == right.Offset
            && left.Length == right.Length;
    }
}
