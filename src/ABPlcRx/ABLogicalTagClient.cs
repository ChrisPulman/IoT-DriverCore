// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;

#if REACTIVE_SHIM
using SignalFactory = ReactiveUI.Primitives.Reactive.Signals.Signal;
#else
using SignalFactory = ReactiveUI.Primitives.Signals.Signal;
#endif

#if REACTIVELIST_REACTIVE
namespace IoT.DriverCore.ABPlcRx.Reactive;
#else
namespace IoT.DriverCore.ABPlcRx;
#endif

/// <summary>Adapts an Allen-Bradley controller to shared logical-tag contracts through composition.</summary>
public sealed partial class ABLogicalTagClient : IManagedLogicalTagClient, IDisposable
{
    /// <summary>Maximum number of addressable bits in supported integral values.</summary>
    private const int MaximumIntegralBitCount = 64;

    /// <summary>Maps PLC and CLR aliases to supported registration types.</summary>
    private static readonly Dictionary<string, string> DataTypeAliases =
        new(StringComparer.Ordinal)
        {
            ["boolean"] = "bool",
            ["system.boolean"] = "bool",
            ["uint8"] = "byte",
            ["system.byte"] = "byte",
            ["int8"] = "sbyte",
            ["system.sbyte"] = "sbyte",
            ["int16"] = "short",
            ["system.int16"] = "short",
            ["uint16"] = "ushort",
            ["system.uint16"] = "ushort",
            ["dint"] = "int",
            ["int32"] = "int",
            ["system.int32"] = "int",
            ["udint"] = "uint",
            ["uint32"] = "uint",
            ["system.uint32"] = "uint",
            ["lint"] = "long",
            ["int64"] = "long",
            ["system.int64"] = "long",
            ["ulint"] = "ulong",
            ["uint64"] = "ulong",
            ["system.uint64"] = "ulong",
            ["real"] = "float",
            ["single"] = "float",
            ["system.single"] = "float",
            ["lreal"] = "double",
            ["float64"] = "double",
            ["system.double"] = "double",
            ["system.string"] = "string",
        };

    /// <summary>The composed Allen-Bradley controller.</summary>
    private readonly IABPlcRx _controller;

    /// <summary>Indicates whether this adapter owns the catalog lifetime.</summary>
    private readonly bool _ownsCatalog;

    /// <summary>Time provider used to stamp logical tag values.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>The configured SQLite store.</summary>
    private LogicalTagSqliteStore? _store;

    /// <summary>Tracks disposal state.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="ABLogicalTagClient"/> class.</summary>
    /// <param name="controller">The composed Allen-Bradley controller.</param>
    public ABLogicalTagClient(IABPlcRx controller)
        : this(controller, new LogicalTagCatalog(), store: null, ownsCatalog: true, TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ABLogicalTagClient"/> class.</summary>
    /// <param name="controller">The composed Allen-Bradley controller.</param>
    /// <param name="timeProvider">The time provider used to stamp logical tag values.</param>
    public ABLogicalTagClient(IABPlcRx controller, TimeProvider timeProvider)
        : this(controller, new LogicalTagCatalog(), store: null, ownsCatalog: true, timeProvider)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ABLogicalTagClient"/> class.</summary>
    /// <param name="controller">The composed Allen-Bradley controller.</param>
    /// <param name="catalog">The logical-tag catalog.</param>
    public ABLogicalTagClient(IABPlcRx controller, ILogicalTagCatalog catalog)
        : this(controller, catalog, store: null, ownsCatalog: false, TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ABLogicalTagClient"/> class.</summary>
    /// <param name="controller">The composed Allen-Bradley controller.</param>
    /// <param name="catalog">The logical-tag catalog.</param>
    /// <param name="timeProvider">The time provider used to stamp logical tag values.</param>
    public ABLogicalTagClient(IABPlcRx controller, ILogicalTagCatalog catalog, TimeProvider timeProvider)
        : this(controller, catalog, store: null, ownsCatalog: false, timeProvider)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ABLogicalTagClient"/> class.</summary>
    /// <param name="controller">The composed Allen-Bradley controller.</param>
    /// <param name="catalog">The logical-tag catalog.</param>
    /// <param name="store">The SQLite tag store.</param>
    public ABLogicalTagClient(
        IABPlcRx controller,
        ILogicalTagCatalog catalog,
        LogicalTagSqliteStore store)
        : this(controller, catalog, store, ownsCatalog: false, TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ABLogicalTagClient"/> class.</summary>
    /// <param name="controller">The composed Allen-Bradley controller.</param>
    /// <param name="catalog">The logical-tag catalog.</param>
    /// <param name="store">The SQLite tag store.</param>
    /// <param name="timeProvider">The time provider used to stamp logical tag values.</param>
    public ABLogicalTagClient(
        IABPlcRx controller,
        ILogicalTagCatalog catalog,
        LogicalTagSqliteStore store,
        TimeProvider timeProvider)
        : this(controller, catalog, store, ownsCatalog: false, timeProvider)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ABLogicalTagClient"/> class.</summary>
    /// <param name="controller">The composed Allen-Bradley controller.</param>
    /// <param name="catalog">The logical-tag catalog.</param>
    /// <param name="store">The optional SQLite store.</param>
    /// <param name="ownsCatalog">Whether the adapter owns the catalog.</param>
    /// <param name="timeProvider">The time provider used to stamp logical tag values.</param>
    private ABLogicalTagClient(
        IABPlcRx controller,
        ILogicalTagCatalog catalog,
        LogicalTagSqliteStore? store,
        bool ownsCatalog,
        TimeProvider timeProvider)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _store = store;
        _ownsCatalog = ownsCatalog;
        _timeProvider = timeProvider;
    }

    /// <summary>Gets the logical-tag catalog used by this adapter.</summary>
    public ILogicalTagCatalog Catalog { get; }

    /// <summary>Creates and registers a logical tag.</summary>
    /// <param name="name">The logical name.</param>
    /// <param name="address">The Allen-Bradley address.</param>
    /// <param name="dataType">The CLR or PLC data type name.</param>
    /// <returns>The registered definition.</returns>
    public LogicalTag CreateTag(string name, string address, string dataType) =>
        CreateTag(new LogicalTag(name, address, dataType));

    /// <summary>Creates and registers an existing logical tag definition.</summary>
    /// <param name="tag">The logical tag definition.</param>
    /// <returns>The registered definition.</returns>
    public LogicalTag CreateTag(LogicalTag tag)
    {
        RegisterTag(tag);
        return tag;
    }

    /// <summary>Registers or replaces a logical tag in the controller and catalog.</summary>
    /// <param name="tag">The logical tag definition.</param>
    public void RegisterTag(LogicalTag tag)
    {
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        ThrowIfDisposed();
        var groupName = string.IsNullOrWhiteSpace(tag.GroupName) ? "Default" : tag.GroupName;

        var dataType = NormalizeDataType(tag.DataType);
        if (dataType == "bool" && TryGetBit(tag, out _))
        {
            _controller.AddUpdateTagItem<short>(tag.Name, tag.Address, groupName, default);
            Catalog.Upsert(tag);
            return;
        }

        if (!TryRegisterBasicTag(_controller, tag, groupName, dataType))
        {
            RegisterExtendedTag(_controller, tag, groupName, dataType);
        }

        Catalog.Upsert(tag);
    }

    /// <summary>Removes a logical tag from the controller and catalog.</summary>
    /// <param name="tagName">The logical tag name.</param>
    /// <returns>True when either layer contained the tag.</returns>
    public bool RemoveTag(string tagName)
    {
        ThrowIfDisposed();
        var removedFromCatalog = Catalog.TryRemove(tagName, out _);
        var removedFromController = _controller.RemoveTagItem(tagName);
        return removedFromCatalog || removedFromController;
    }

    /// <inheritdoc/>
    public async Task<TagOperationResult<LogicalTagValue>> ReadAsync(
        string tagName,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!TryGetAccessibleTag(tagName, write: false, out var tag, out var failure))
        {
            return TagOperationResult<LogicalTagValue>.Failure(failure);
        }

        var results = await _controller
            .ReadManyAsync([tagName], cancellationToken)
            .ConfigureAwait(false);
        return results.Count == 0
            ? TagOperationResult<LogicalTagValue>.Failure($"Tag '{tagName}' is not registered in the controller.")
            : ToLogicalResult(tag!, results[0]);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> ReadManyAsync(
        IReadOnlyCollection<string> tagNames,
        CancellationToken cancellationToken = default)
    {
        if (tagNames is null)
        {
            throw new ArgumentNullException(nameof(tagNames));
        }

        ThrowIfDisposed();
        var names = tagNames.ToArray();
        var validNames = names
            .Where(name => TryGetAccessibleTag(name, write: false, out _, out _))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var plcResults = await _controller.ReadManyAsync(validNames, cancellationToken).ConfigureAwait(false);
        var byName = plcResults.ToDictionary(result => result.Tag.Variable, StringComparer.Ordinal);

        return names
            .Select(name =>
            {
                if (!TryGetAccessibleTag(name, write: false, out var tag, out var failure))
                {
                    return TagOperationResult<LogicalTagValue>.Failure(failure);
                }

                return byName.TryGetValue(name, out var result)
                    ? ToLogicalResult(tag!, result)
                    : TagOperationResult<LogicalTagValue>.Failure(
                        $"Tag '{name}' is not registered in the controller.");
            })
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<TagOperationResult<LogicalTagValue>> WriteAsync(
        LogicalTagValue value,
        CancellationToken cancellationToken = default)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        ThrowIfDisposed();
        if (!TryGetAccessibleTag(value.TagName, write: true, out _, out var failure))
        {
            return TagOperationResult<LogicalTagValue>.Failure(failure);
        }

        if (Catalog.TryGet(value.TagName, out var tag) && tag is not null && TryGetBit(tag, out var bit))
        {
            if (value.Value is not bool bitValue)
            {
                return TagOperationResult<LogicalTagValue>.Failure(
                    $"Bit tag '{value.TagName}' requires a Boolean value.");
            }

            var bitResult = await _controller
                .WriteValueAsync(value.TagName, bitValue, bit, cancellationToken)
                .ConfigureAwait(false);
            return bitResult.Succeeded
                ? TagOperationResult<LogicalTagValue>.Success(
                    new LogicalTagValue(value.TagName, bitValue, _timeProvider.GetUtcNow(), "Good"))
                : TagOperationResult<LogicalTagValue>.Failure(bitResult.Error);
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [value.TagName] = value.Value,
        };
        var results = await _controller.WriteManyAsync(values, cancellationToken).ConfigureAwait(false);
        return results.Count == 0
            ? TagOperationResult<LogicalTagValue>.Failure(
                $"Tag '{value.TagName}' is not registered in the controller.")
            : ToLogicalResult(value, results[0], _timeProvider);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> WriteManyAsync(
        IReadOnlyCollection<LogicalTagValue> values,
        CancellationToken cancellationToken = default)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        ThrowIfDisposed();
        var items = values.ToArray();
        var duplicateNames = items
            .GroupBy(item => item.TagName, StringComparer.Ordinal)
            .Where(group => group.Skip(1).Any())
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        var writeValues = items
            .Where(item =>
                !duplicateNames.Contains(item.TagName) &&
                TryGetAccessibleTag(item.TagName, write: true, out _, out _) &&
                (!Catalog.TryGet(item.TagName, out var tag) || tag is null || !TryGetBit(tag, out _)))
            .ToDictionary(item => item.TagName, item => item.Value, StringComparer.Ordinal);
        var plcResults = await _controller.WriteManyAsync(writeValues, cancellationToken).ConfigureAwait(false);
        var byName = plcResults.ToDictionary(result => result.Tag.Variable, StringComparer.Ordinal);
        var bitResults = new Dictionary<string, TagOperationResult<LogicalTagValue>>(StringComparer.Ordinal);
        foreach (var item in items.Where(
                     item => !duplicateNames.Contains(item.TagName) &&
                             Catalog.TryGet(item.TagName, out var tag) &&
                             tag is not null &&
                             TryGetBit(tag, out _)))
        {
            bitResults[item.TagName] = await WriteAsync(item, cancellationToken).ConfigureAwait(false);
        }

        return items
            .Select(item =>
            {
                if (duplicateNames.Contains(item.TagName))
                {
                    return TagOperationResult<LogicalTagValue>.Failure(
                        $"Tag '{item.TagName}' occurs more than once in the bulk write.");
                }

                if (!TryGetAccessibleTag(item.TagName, write: true, out _, out var failure))
                {
                    return TagOperationResult<LogicalTagValue>.Failure(failure);
                }

                if (bitResults.TryGetValue(item.TagName, out var bitResult))
                {
                    return bitResult;
                }

                return byName.TryGetValue(item.TagName, out var result)
                    ? ToLogicalResult(item, result, _timeProvider)
                    : TagOperationResult<LogicalTagValue>.Failure(
                        $"Tag '{item.TagName}' is not registered in the controller.");
            })
            .ToArray();
    }

    /// <inheritdoc/>
    public IObservable<LogicalTagValue> Observe(string tagName)
    {
        ThrowIfDisposed();
        if (!TryGetAccessibleTag(tagName, write: false, out _, out var failure))
        {
            throw new InvalidOperationException(failure);
        }

        return Catalog.TryGet(tagName, out var tag) && tag is not null && TryGetBit(tag, out var bit)
            ? _controller
                .Observe<bool>(tagName, default, bit)
                .Select(value => new LogicalTagValue(tagName, value, _timeProvider.GetUtcNow()))
            : _controller
                .Observe<object?>(tagName, default, -1)
                .Select(value => new LogicalTagValue(tagName, value, _timeProvider.GetUtcNow()));
    }

    /// <inheritdoc/>
    public IObservable<LogicalTagValue> ObserveMany(IReadOnlyCollection<string> tagNames)
    {
        if (tagNames is null)
        {
            throw new ArgumentNullException(nameof(tagNames));
        }

        ThrowIfDisposed();
        var streams = tagNames.Select(Observe).ToArray();
        return streams.Length == 0
            ? SignalFactory.Silent<LogicalTagValue>()
            : SignalFactory.Merge(streams);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<LogicalTagValue> ObserveAsync(
        string tagName,
        CancellationToken cancellationToken = default) =>
        ObservableAsyncEnumerable.Create(Observe(tagName), cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<LogicalTagValue> ObserveManyAsync(
        IReadOnlyCollection<string> tagNames,
        CancellationToken cancellationToken = default) =>
        ObservableAsyncEnumerable.Create(ObserveMany(tagNames), cancellationToken);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_ownsCatalog && Catalog is IDisposable disposableCatalog)
        {
            disposableCatalog.Dispose();
        }

        _disposed = true;
    }

    /// <summary>Normalizes a logical data type name.</summary>
    /// <param name="dataType">The configured data type.</param>
    /// <returns>The normalized CLR alias.</returns>
    private static string NormalizeDataType(string dataType)
    {
        var normalized = dataType.Trim().ToLowerInvariant();
        return DataTypeAliases.TryGetValue(normalized, out var alias) ? alias : normalized;
    }

    /// <summary>Registers a tag whose type is in the basic type set.</summary>
    /// <param name="controller">The composed controller.</param>
    /// <param name="tag">The logical tag.</param>
    /// <param name="groupName">The effective group name.</param>
    /// <param name="dataType">The normalized data type.</param>
    /// <returns>True when the type was registered.</returns>
    private static bool TryRegisterBasicTag(
        IABPlcRx controller,
        LogicalTag tag,
        string groupName,
        string dataType)
    {
        switch (dataType)
        {
            case "bool":
                {
                    controller.AddUpdateTagItem<bool>(tag.Name, tag.Address, groupName, default);
                    return true;
                }

            case "byte":
                {
                    controller.AddUpdateTagItem<byte>(tag.Name, tag.Address, groupName, default);
                    return true;
                }

            case "sbyte":
                {
                    controller.AddUpdateTagItem<sbyte>(tag.Name, tag.Address, groupName, default);
                    return true;
                }

            case "short":
                {
                    controller.AddUpdateTagItem<short>(tag.Name, tag.Address, groupName, default);
                    return true;
                }

            case "ushort":
                {
                    controller.AddUpdateTagItem<ushort>(tag.Name, tag.Address, groupName, default);
                    return true;
                }

            case "int":
                {
                    controller.AddUpdateTagItem<int>(tag.Name, tag.Address, groupName, default);
                    return true;
                }

            default:
                return false;
        }
    }

    /// <summary>Registers a tag whose type is in the extended type set.</summary>
    /// <param name="controller">The composed controller.</param>
    /// <param name="tag">The logical tag.</param>
    /// <param name="groupName">The effective group name.</param>
    /// <param name="dataType">The normalized data type.</param>
    private static void RegisterExtendedTag(
        IABPlcRx controller,
        LogicalTag tag,
        string groupName,
        string dataType)
    {
        switch (dataType)
        {
            case "uint":
                {
                    controller.AddUpdateTagItem<uint>(tag.Name, tag.Address, groupName, default);
                    break;
                }

            case "long":
                {
                    controller.AddUpdateTagItem<long>(tag.Name, tag.Address, groupName, default);
                    break;
                }

            case "ulong":
                {
                    controller.AddUpdateTagItem<ulong>(tag.Name, tag.Address, groupName, default);
                    break;
                }

            case "float":
                {
                    controller.AddUpdateTagItem<float>(tag.Name, tag.Address, groupName, default);
                    break;
                }

            case "double":
                {
                    controller.AddUpdateTagItem<double>(tag.Name, tag.Address, groupName, default);
                    break;
                }

            case "string":
                {
                    controller.AddUpdateTagItem<string>(tag.Name, tag.Address, groupName, default);
                    break;
                }

            default:
                throw new NotSupportedException(
                    $"Allen-Bradley logical data type '{tag.DataType}' is not supported.");
        }
    }

    /// <summary>Converts a PLC read result to a logical tag result.</summary>
    /// <param name="tag">The tag definition.</param>
    /// <param name="result">The PLC result.</param>
    /// <returns>The logical operation result.</returns>
    private static TagOperationResult<LogicalTagValue> ToLogicalResult(
        LogicalTag tag,
        PlcTagResult result) =>
        PlcTagStatus.IsError(result.StatusCode)
            ? TagOperationResult<LogicalTagValue>.Failure(PlcTagStatus.DecodeError(result.StatusCode))
            : TagOperationResult<LogicalTagValue>.Success(
                new LogicalTagValue(
                    tag.Name,
                    GetLogicalValue(tag, result.Tag),
                    result.Timestamp,
                    "Good"));

    /// <summary>Converts a PLC write result to a logical tag result.</summary>
    /// <param name="value">The requested logical value.</param>
    /// <param name="result">The PLC result.</param>
    /// <param name="timeProvider">The time provider used to stamp the result.</param>
    /// <returns>The logical operation result.</returns>
    private static TagOperationResult<LogicalTagValue> ToLogicalResult(
        LogicalTagValue value,
        PlcTagResult result,
        TimeProvider timeProvider) =>
        PlcTagStatus.IsError(result.StatusCode)
            ? TagOperationResult<LogicalTagValue>.Failure(PlcTagStatus.DecodeError(result.StatusCode))
            : TagOperationResult<LogicalTagValue>.Success(
                new LogicalTagValue(value.TagName, value.Value, timeProvider.GetUtcNow(), "Good"));

    /// <summary>Gets the public logical value, including integral bit projection.</summary>
    /// <param name="tag">The logical tag.</param>
    /// <param name="plcTag">The protocol tag.</param>
    /// <returns>The projected value.</returns>
    private static object? GetLogicalValue(LogicalTag tag, IPlcTag plcTag)
    {
        if (!TryGetBit(tag, out var bit))
        {
            return plcTag.Value;
        }

        var unsignedValue = plcTag.Value switch
        {
            byte or ushort or uint or ulong =>
                Convert.ToUInt64(plcTag.Value, System.Globalization.CultureInfo.InvariantCulture),
            sbyte value => unchecked((byte)value),
            short value => unchecked((ushort)value),
            int value => unchecked((uint)value),
            long value => unchecked((ulong)value),
            _ => 0UL,
        };
        return (unsignedValue & (1UL << bit)) != 0;
    }

    /// <summary>Tries to read integral bit metadata.</summary>
    /// <param name="tag">The logical tag.</param>
    /// <param name="bit">The parsed bit.</param>
    /// <returns>True when valid bit metadata exists.</returns>
    private static bool TryGetBit(LogicalTag tag, out int bit)
    {
        if (tag.Metadata.TryGetValue("Bit", out var value) &&
            int.TryParse(
                value,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out bit) &&
            bit >= 0 &&
            bit < MaximumIntegralBitCount)
        {
            return true;
        }

        bit = -1;
        return false;
    }

    /// <summary>Tries to resolve a tag and validate the requested access.</summary>
    /// <param name="tagName">The logical tag name.</param>
    /// <param name="write">True for write access; false for read access.</param>
    /// <param name="tag">The resolved tag.</param>
    /// <param name="failure">The validation failure.</param>
    /// <returns>True when access is allowed.</returns>
    private bool TryGetAccessibleTag(
        string tagName,
        bool write,
        out LogicalTag? tag,
        out string failure)
    {
        if (!Catalog.TryGet(tagName, out tag) || tag is null)
        {
            failure = $"Logical tag '{tagName}' was not found.";
            return false;
        }

        var denied = write
            ? tag.AccessMode == LogicalTagAccessMode.Read
            : tag.AccessMode == LogicalTagAccessMode.Write;
        if (denied)
        {
            failure = $"Logical tag '{tagName}' does not permit {(write ? "writes" : "reads")}.";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    /// <summary>Throws when this adapter has been disposed.</summary>
    private void ThrowIfDisposed() =>
        _ = !_disposed ? true : throw new ObjectDisposedException(nameof(ABLogicalTagClient));
}
