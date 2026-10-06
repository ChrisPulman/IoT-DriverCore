// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using IoT.Driver.Core;
#if REACTIVE_SHIM
using IoT.Driver.S7PlcRx.Reactive.Symbolic;
namespace IoT.Driver.S7PlcRx.Reactive.LogicalTags;
#else
using IoT.Driver.S7PlcRx.Symbolic;
namespace IoT.Driver.S7PlcRx.LogicalTags;
#endif

/// <summary>Composes native symbolic access with a logical alias catalog.</summary>
public sealed class S7SymbolicLogicalTagClient : ILogicalTagClient
{
    /// <summary>Defines the default controller notification interval.</summary>
    private const int DefaultCycleMilliseconds = 100;

    /// <summary>Provides native symbolic operations.</summary>
    private readonly S7SymbolicClient _client;

    /// <summary>Specifies the requested controller notification cycle.</summary>
    private readonly TimeSpan _cycle;

    /// <summary>Provides fallback timestamps when the controller sends none.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="S7SymbolicLogicalTagClient"/> class.</summary>
    /// <param name="client">The symbolic connection, owned by the caller.</param>
    /// <param name="catalog">The alias catalog, whose addresses are symbolic paths.</param>
    public S7SymbolicLogicalTagClient(S7SymbolicClient client, ILogicalTagCatalog catalog)
        : this(client, catalog, TimeSpan.FromMilliseconds(DefaultCycleMilliseconds), TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="S7SymbolicLogicalTagClient"/> class.</summary>
    /// <param name="client">The symbolic connection, owned by the caller.</param>
    /// <param name="catalog">The alias catalog, whose addresses are symbolic paths.</param>
    /// <param name="cycle">The requested notification interval.</param>
    public S7SymbolicLogicalTagClient(S7SymbolicClient client, ILogicalTagCatalog catalog, TimeSpan? cycle)
        : this(client, catalog, cycle ?? TimeSpan.FromMilliseconds(DefaultCycleMilliseconds), TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="S7SymbolicLogicalTagClient"/> class.</summary>
    /// <param name="client">The symbolic connection, owned by the caller.</param>
    /// <param name="catalog">The alias catalog, whose addresses are symbolic paths.</param>
    /// <param name="cycle">The requested controller notification interval.</param>
    /// <param name="timeProvider">The fallback timestamp provider.</param>
    public S7SymbolicLogicalTagClient(S7SymbolicClient client, ILogicalTagCatalog catalog, TimeSpan cycle, TimeProvider timeProvider)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _cycle = cycle;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _ = cycle > TimeSpan.Zero
            ? cycle
            : throw new ArgumentOutOfRangeException(nameof(cycle));
    }

    /// <summary>Gets the catalog used to resolve logical aliases.</summary>
    public ILogicalTagCatalog Catalog { get; }

    /// <inheritdoc/>
    public async Task<TagOperationResult<LogicalTagValue>> ReadAsync(string tagName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var tag = GetTag(tagName, false);
            var value = await _client.ReadAsync(new LogicalTagKey<object>(tag.Address), cancellationToken).ConfigureAwait(false);
            return TagOperationResult<LogicalTagValue>.Success(new(tag.Name, value, _timeProvider.GetUtcNow()));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return TagOperationResult<LogicalTagValue>.Failure(exception.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<TagOperationResult<LogicalTagValue>> WriteAsync(LogicalTagValue value, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(value, nameof(value));
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var tag = GetTag(value.TagName, true);
            await _client.WriteAsync(tag.Address, value.Value, cancellationToken).ConfigureAwait(false);
            return TagOperationResult<LogicalTagValue>.Success(new(tag.Name, value.Value, _timeProvider.GetUtcNow()));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return TagOperationResult<LogicalTagValue>.Failure(exception.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> ReadManyAsync(IReadOnlyCollection<string> tagNames, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(tagNames, nameof(tagNames));
        cancellationToken.ThrowIfCancellationRequested();
        var results = new List<TagOperationResult<LogicalTagValue>>(tagNames.Count);
        foreach (var name in tagNames)
        {
            results.Add(await ReadAsync(name, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> WriteManyAsync(IReadOnlyCollection<LogicalTagValue> values, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(values, nameof(values));
        cancellationToken.ThrowIfCancellationRequested();
        var results = new List<TagOperationResult<LogicalTagValue>>(values.Count);

        // Sequential writes preserve repeated aliases and overlapping symbolic paths.
        foreach (var value in values)
        {
            results.Add(await WriteAsync(value, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    /// <inheritdoc/>
    public IObservable<LogicalTagValue> Observe(string tagName) => ObserveMany([tagName]);

    /// <inheritdoc/>
    public IObservable<LogicalTagValue> ObserveMany(IReadOnlyCollection<string> tagNames)
    {
        Guard.NotNull(tagNames, nameof(tagNames));
        var names = CopyNames(tagNames);
        foreach (var name in names)
        {
            _ = GetTag(name, false);
        }

        return new ChangeObservable(this, names);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<LogicalTagValue> ObserveAsync(string tagName, CancellationToken cancellationToken = default) => ObserveManyAsync([tagName], cancellationToken);

    /// <inheritdoc/>
    public async IAsyncEnumerable<LogicalTagValue> ObserveManyAsync(IReadOnlyCollection<string> tagNames, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guard.NotNull(tagNames, nameof(tagNames));
        cancellationToken.ThrowIfCancellationRequested();
        var aliases = new Dictionary<string, List<LogicalTag>>(StringComparer.Ordinal);
        var symbols = new Dictionary<string, S7Symbol>(StringComparer.Ordinal);
        await MapAliasesAsync(tagNames, aliases, symbols, cancellationToken).ConfigureAwait(false);

        if (aliases.Count == 0)
        {
            yield break;
        }

        var subscription = await _client.SubscribeAsync(CopyNames(aliases.Keys), _cycle, cancellationToken).ConfigureAwait(false);
        await using (subscription.ConfigureAwait(false))
        {
            await foreach (var change in subscription.Changes.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (!aliases.TryGetValue(change.Path, out var tags))
                {
                    continue;
                }

                var payload = change.Value is null ? null : S7SymbolicTypeConversion.ToClr<object>(change.Value, symbols[change.Path].SoftDataType);
                var quality = change.ErrorCode == 0 ? "Good" : $"S7CommPlus:{change.ErrorCode.ToString(CultureInfo.InvariantCulture)}";
                foreach (var tag in tags)
                {
                    yield return new(tag.Name, payload, change.Timestamp ?? _timeProvider.GetUtcNow(), quality);
                }
            }
        }
    }

    /// <summary>Copies aliases without retaining the caller's mutable collection.</summary>
    /// <param name="names">The source aliases.</param>
    /// <returns>The copied aliases.</returns>
    private static string[] CopyNames(IReadOnlyCollection<string> names)
    {
        var copy = new string[names.Count];
        var index = 0;
        foreach (var name in names)
        {
            copy[index] = name;
            index++;
        }

        return copy;
    }

    /// <summary>Determines whether an alias is already mapped to a native path.</summary>
    /// <param name="tags">The aliases mapped to the path.</param>
    /// <param name="name">The alias name.</param>
    /// <returns>Whether the alias exists.</returns>
    private static bool ContainsAlias(List<LogicalTag> tags, string name)
    {
        foreach (var tag in tags)
        {
            if (tag.Name == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Resolves controller metadata for each unique native path.</summary>
    /// <param name="tagNames">The aliases to observe.</param>
    /// <param name="aliases">The destination alias mappings.</param>
    /// <param name="symbols">The destination native metadata.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The metadata resolution task.</returns>
    private async Task MapAliasesAsync(
        IReadOnlyCollection<string> tagNames,
        Dictionary<string, List<LogicalTag>> aliases,
        Dictionary<string, S7Symbol> symbols,
        CancellationToken cancellationToken)
    {
        foreach (var name in tagNames)
        {
            var tag = GetTag(name, false);
            if (!aliases.TryGetValue(tag.Address, out var list))
            {
                list = [];
                aliases.Add(tag.Address, list);
                symbols.Add(tag.Address, await _client.ResolveAsync(tag.Address, cancellationToken).ConfigureAwait(false));
            }

            if (!ContainsAlias(list, tag.Name))
            {
                list.Add(tag);
            }
        }
    }

    /// <summary>Resolves an alias and enforces its local access mode.</summary>
    /// <param name="name">The alias name.</param>
    /// <param name="write">Whether the operation writes a value.</param>
    /// <returns>The logical definition.</returns>
    private LogicalTag GetTag(string name, bool write)
    {
        if (!Catalog.TryGet(name, out var tag) || tag is null)
        {
            throw new KeyNotFoundException($"Logical tag '{name}' was not found.");
        }

        if (tag.AccessMode == (write ? LogicalTagAccessMode.Read : LogicalTagAccessMode.Write))
        {
            throw new InvalidOperationException($"Logical tag '{name}' does not permit this operation.");
        }

        return tag;
    }

    /// <summary>Initializes a new instance of the <see cref="ChangeObservable"/> class.</summary>
    /// <param name="owner">The logical adapter.</param>
    /// <param name="names">The logical aliases.</param>
    private sealed class ChangeObservable(S7SymbolicLogicalTagClient owner, string[] names) : IObservable<LogicalTagValue>
    {
        /// <inheritdoc/>
        public IDisposable Subscribe(IObserver<LogicalTagValue> observer)
        {
            Guard.NotNull(observer, nameof(observer));
            var lifetime = new ObserverLifetime();
            _ = PumpAsync(observer, lifetime);
            return lifetime;
        }

        /// <summary>Forwards controller changes and observes terminal errors.</summary>
        /// <param name="observer">The destination observer.</param>
        /// <param name="lifetime">The cancellation lifetime.</param>
        /// <returns>The observation task.</returns>
        private async Task PumpAsync(IObserver<LogicalTagValue> observer, ObserverLifetime lifetime)
        {
            try
            {
                await foreach (var value in owner.ObserveManyAsync(names, lifetime.Token).ConfigureAwait(false))
                {
                    observer.OnNext(value);
                }

                if (!lifetime.Token.IsCancellationRequested)
                {
                    observer.OnCompleted();
                }
            }
            catch (OperationCanceledException) when (lifetime.Token.IsCancellationRequested)
            {
                // Cancellation terminates the native subscription through the iterator disposal.
                System.Diagnostics.Trace.TraceInformation("Symbolic logical observation canceled.");
            }
            catch (Exception exception)
            {
                // Observer callback failures must not escape the detached pump task.
                try
                {
                    if (!lifetime.Token.IsCancellationRequested)
                    {
                        observer.OnError(exception);
                    }
                }
                catch (Exception callbackException)
                {
                    System.Diagnostics.Trace.TraceError("Symbolic logical observer failed: {0}", callbackException);
                }
            }
            finally
            {
                lifetime.Complete();
            }
        }
    }

    /// <summary>Coordinates synchronous cancellation with asynchronous cleanup.</summary>
    private sealed class ObserverLifetime : IDisposable
    {
        /// <summary>Serializes cancellation and token source cleanup.</summary>
        private readonly Lock _gate = new();

        /// <summary>Cancels the subscription pump.</summary>
        private readonly CancellationTokenSource _source = new();

        /// <summary>Indicates that asynchronous cleanup has completed.</summary>
        private bool _completed;

        /// <summary>Initializes a new instance of the <see cref="ObserverLifetime"/> class.</summary>
        public ObserverLifetime() => Token = _source.Token;

        /// <summary>Gets the pump cancellation token.</summary>
        public CancellationToken Token { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (_gate)
            {
                if (_completed)
                {
                    _source.Dispose();
                }
                else
                {
                    _source.Cancel();
                }
            }
        }

        /// <summary>Disposes the token source after the pump stops.</summary>
        public void Complete()
        {
            lock (_gate)
            {
                _completed = true;
            }

            Dispose();
        }
    }
}
