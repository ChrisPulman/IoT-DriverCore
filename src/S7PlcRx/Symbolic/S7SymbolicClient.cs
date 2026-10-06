// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Globalization;
using System.IO;
#if REACTIVE_SHIM
using IoT.Driver.S7PlcRx.Reactive.Symbolic.Protocol;
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
using IoT.Driver.S7PlcRx.Symbolic.Protocol;

namespace IoT.Driver.S7PlcRx.Symbolic;
#endif

/// <summary>Browses and accesses controller symbols through S7CommPlus.</summary>
public sealed partial class S7SymbolicClient : IAsyncDisposable
{
    /// <summary>Identifies the PLC DATE_AND_TIME soft datatype.</summary>
    private const uint DateAndTimeSoftType = 14;

    /// <summary>Identifies the PLC STRING soft datatype.</summary>
    private const uint StringSoftType = 19;

    /// <summary>Identifies the PLC WSTRING soft datatype.</summary>
    private const uint WideStringSoftType = 62;

    /// <summary>Selects the symbolic variable name attribute.</summary>
    private const uint VariableNameAttribute = 233;

    /// <summary>Identifies the PLC program root object.</summary>
    private const uint ProgramObjectId = 3;

    /// <summary>Identifies DB objects in the PLC program.</summary>
    private const uint DataBlockClassId = 2574;

    /// <summary>Selects the actual values of a DB.</summary>
    private const uint DataBlockActualSubArea = 2550;

    /// <summary>Selects actual values in native memory areas.</summary>
    private const uint NativeActualSubArea = 3736;

    /// <summary>Identifies the symbolic input memory area.</summary>
    private const uint InputAccessArea = 80;

    /// <summary>Identifies the symbolic output memory area.</summary>
    private const uint OutputAccessArea = 81;

    /// <summary>Identifies the symbolic marker memory area.</summary>
    private const uint MarkerAccessArea = 82;

    /// <summary>Identifies the symbolic counter memory area.</summary>
    private const uint CounterAccessArea = 83;

    /// <summary>Identifies the symbolic timer memory area.</summary>
    private const uint TimerAccessArea = 84;

    /// <summary>Limits the number of symbol declarations collected during a browse.</summary>
    private const int BrowseSymbolLimit = 100_000;

    /// <summary>Owns the authenticated protocol session.</summary>
    private readonly S7PlusSession _session;

    /// <summary>Serializes cache discovery and symbol resolution.</summary>
    private readonly S7OperationGate _browseGate;

    /// <summary>Caches root areas discovered for the current session.</summary>
    private readonly Dictionary<string, SymbolRoot> _roots = [with(comparer: StringComparer.Ordinal)];

    /// <summary>Caches decoded PLC type metadata by relation identifier.</summary>
    private readonly Dictionary<uint, PlusTypeMember[]> _types = [];

    /// <summary>Caches resolved native symbolic access paths.</summary>
    private readonly Dictionary<string, S7Symbol> _symbols = [with(comparer: StringComparer.Ordinal)];

    /// <summary>Protects creation of the shared disposal operation.</summary>
    private readonly Lock _disposalGate = new();

    /// <summary>Completes when the session and admitted cache operations have drained.</summary>
    private Task? _disposal;

    /// <summary>Initializes a new instance of the <see cref="S7SymbolicClient"/> class.</summary>
    /// <param name="options">The controller connection and authentication options.</param>
    public S7SymbolicClient(S7SymbolicConnectionOptions options)
    {
        _session = new(options);
        _browseGate = new();
    }

    /// <summary>Initializes a new instance of the <see cref="S7SymbolicClient"/> class with a composed protocol session.</summary>
    /// <param name="session">The protocol session owned by the client.</param>
    internal S7SymbolicClient(S7PlusSession session)
    {
#if NETFRAMEWORK
        _ = session ?? throw new ArgumentNullException(nameof(session));
#else
        ArgumentNullException.ThrowIfNull(session);
#endif
        _session = session;
        _browseGate = new();
    }

    /// <summary>Initializes a new instance of the <see cref="S7SymbolicClient"/> class with a controlled cache gate.</summary>
    /// <param name="options">The controller connection and authentication options.</param>
    /// <param name="browseGate">The cache gate owned by the client.</param>
    internal S7SymbolicClient(S7SymbolicConnectionOptions options, S7OperationGate browseGate)
    {
#if NETFRAMEWORK
        _ = browseGate ?? throw new ArgumentNullException(nameof(browseGate));
#else
        ArgumentNullException.ThrowIfNull(browseGate);
#endif
        _session = new(options);
        _browseGate = browseGate;
    }

    /// <summary>Gets the protocol session used by subscriptions and alarms.</summary>
    internal S7PlusSession Session => _session;

    /// <summary>Establishes an authenticated symbolic session.</summary>
    /// <returns>The operation result.</returns>
    public Task ConnectAsync() => ConnectAsync(CancellationToken.None);

    /// <summary>Establishes an authenticated symbolic session.</summary>
    /// <param name="cancellationToken">Cancels the controller operation.</param>
    /// <returns>The operation result.</returns>
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        using var lease = await _browseGate.AcquireAsync(cancellationToken).ConfigureAwait(false);
        if (_session.IsConnected)
        {
            return;
        }

        await _session.ConnectAsync(cancellationToken).ConfigureAwait(false);
        _roots.Clear();
        _types.Clear();
        _symbols.Clear();
    }

    /// <summary>Closes the controller session.</summary>
    /// <returns>The operation result.</returns>
    public Task DisconnectAsync() => DisconnectAsync(CancellationToken.None);

    /// <summary>Closes the controller session.</summary>
    /// <param name="cancellationToken">Cancels the controller operation.</param>
    /// <returns>The operation result.</returns>
    public Task DisconnectAsync(CancellationToken cancellationToken) => _session.DisconnectAsync(cancellationToken);

    /// <summary>Browses scalar symbols and array declarations without expanding array elements.</summary>
    /// <returns>The operation result.</returns>
    public Task<S7Symbol[]> BrowseAsync() => BrowseAsync(CancellationToken.None);

    /// <summary>Browses scalar symbols and array declarations without expanding array elements.</summary>
    /// <param name="cancellationToken">Cancels the controller operation.</param>
    /// <returns>The operation result.</returns>
    public async Task<S7Symbol[]> BrowseAsync(CancellationToken cancellationToken)
    {
        using var lease = await _browseGate.AcquireAsync(cancellationToken).ConfigureAwait(false);
        await LoadRootsAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<S7Symbol>();
        foreach (var root in _roots.Values)
        {
            await BrowseTypeAsync(root, root.TypeId, Quote(root.Name), [], new(), result, cancellationToken).ConfigureAwait(false);
        }

        return result.ToArray();
    }

    /// <summary>Resolves a quoted or indexed PLC symbolic path.</summary>
    /// <param name="path">The PLC symbolic path.</param>
    /// <returns>The operation result.</returns>
    public Task<S7Symbol> ResolveAsync(string path) => ResolveAsync(path, CancellationToken.None);

    /// <summary>Resolves a quoted or indexed PLC symbolic path.</summary>
    /// <param name="path">The PLC symbolic path.</param>
    /// <param name="cancellationToken">Cancels the controller operation.</param>
    /// <returns>The operation result.</returns>
    public Task<S7Symbol> ResolveAsync(string path, CancellationToken cancellationToken) => ResolveSymbolAsync(path, cancellationToken);

    /// <summary>Reads a symbolic value.</summary>
    /// <param name="path">The PLC symbolic path.</param>
    /// <returns>The operation result.</returns>
    public Task<S7SymbolicValue> ReadAsync(string path) => ReadAsync(path, CancellationToken.None);

    /// <summary>Reads a symbolic value.</summary>
    /// <param name="path">The PLC symbolic path.</param>
    /// <param name="cancellationToken">Cancels the controller operation.</param>
    /// <returns>The operation result.</returns>
    public async Task<S7SymbolicValue> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var symbol = await ResolveSymbolAsync(path, cancellationToken).ConfigureAwait(false);
        var result = (await _session.ReadAsync([symbol.Address], cancellationToken).ConfigureAwait(false))[0];
        if (!result.IsSuccess || result.Value is null)
        {
            throw new S7SymbolicException("The controller rejected a symbolic read.", result.ErrorCode);
        }

        return result.Value;
    }

    /// <summary>Reads a value using its PLC datatype to map to a CLR type.</summary>
    /// <typeparam name="T">The expected CLR value type.</typeparam>
    /// <returns>The operation result.</returns>
    /// <param name="tag">The symbolic path and expected CLR value type.</param>
    public Task<T> ReadAsync<T>(LogicalTagKey<T> tag) => ReadAsync(tag, CancellationToken.None);

    /// <summary>Reads a value using its PLC datatype to map to a CLR type.</summary>
    /// <param name="cancellationToken">Cancels the controller operation.</param>
    /// <typeparam name="T">The expected CLR value type.</typeparam>
    /// <returns>The operation result.</returns>
    /// <param name="tag">The symbolic path and expected CLR value type.</param>
    public async Task<T> ReadAsync<T>(LogicalTagKey<T> tag, CancellationToken cancellationToken)
    {
#if NETFRAMEWORK
        _ = tag ?? throw new ArgumentNullException(nameof(tag));
#else
        ArgumentNullException.ThrowIfNull(tag);
#endif
        var path = tag.Name;
        var symbol = await ResolveSymbolAsync(path, cancellationToken).ConfigureAwait(false);
        return ShouldExpandEncodedArray(symbol, typeof(T))
            ? (T)await ReadEncodedArrayAsync(symbol, typeof(T), cancellationToken).ConfigureAwait(false)
            : S7SymbolicTypeConversion.ToClr<T>(await ReadAsync(path, cancellationToken).ConfigureAwait(false), symbol.SoftDataType);
    }

    /// <summary>Writes a symbolic value.</summary>
    /// <param name="path">The PLC symbolic path.</param>
    /// <param name="value">The value to write.</param>
    /// <returns>The operation result.</returns>
    public Task WriteAsync(string path, S7SymbolicValue value) => WriteAsync(path, value, CancellationToken.None);

    /// <summary>Writes a symbolic value.</summary>
    /// <param name="path">The PLC symbolic path.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="cancellationToken">Cancels the controller operation.</param>
    /// <returns>The operation result.</returns>
    public async Task WriteAsync(string path, S7SymbolicValue value, CancellationToken cancellationToken)
    {
#if NETFRAMEWORK
        _ = value ?? throw new ArgumentNullException(nameof(value));
#else
        ArgumentNullException.ThrowIfNull(value);
#endif
        var symbol = await ResolveSymbolAsync(path, cancellationToken).ConfigureAwait(false);
        EnsureWritable(symbol);
        var result = (await _session.WriteAsync([new S7SymbolicWriteItem(symbol.Address, value)], cancellationToken).ConfigureAwait(false))[0];
        if (result.IsSuccess)
        {
            return;
        }

        throw new S7SymbolicException("The controller rejected a symbolic write.", result.ErrorCode);
    }

    /// <summary>Maps a CLR value using the PLC datatype and writes it.</summary>
    /// <param name="path">The PLC symbolic path.</param>
    /// <param name="value">The value to write.</param>
    /// <typeparam name="T">The expected CLR value type.</typeparam>
    /// <returns>The operation result.</returns>
    public Task WriteAsync<T>(string path, T value) => WriteAsync(path, value, CancellationToken.None);

    /// <summary>Maps a CLR value using the PLC datatype and writes it.</summary>
    /// <param name="path">The PLC symbolic path.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="cancellationToken">Cancels the controller operation.</param>
    /// <typeparam name="T">The expected CLR value type.</typeparam>
    /// <returns>The operation result.</returns>
    public async Task WriteAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        var symbol = await ResolveSymbolAsync(path, cancellationToken).ConfigureAwait(false);
        if (value is Array array && IsEncodedScalarType(symbol.SoftDataType))
        {
            await WriteEncodedArrayAsync(symbol, array, cancellationToken).ConfigureAwait(false);
            return;
        }

        await WriteAsync(path, S7SymbolicTypeConversion.FromClr(value!, symbol.SoftDataType, symbol.MaximumStringLength), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads multiple paths and retains each PLC error.</summary>
    /// <param name="paths">The PLC symbolic paths in request order.</param>
    /// <returns>The operation result.</returns>
    public Task<S7SymbolicReadResult[]> ReadManyAsync(string[] paths) => ReadManyAsync(paths, CancellationToken.None);

    /// <summary>Reads multiple paths and retains each PLC error.</summary>
    /// <param name="paths">The PLC symbolic paths in request order.</param>
    /// <param name="cancellationToken">Cancels the controller operation.</param>
    /// <returns>The operation result.</returns>
    public async Task<S7SymbolicReadResult[]> ReadManyAsync(string[] paths, CancellationToken cancellationToken)
    {
#if NETFRAMEWORK
        _ = paths ?? throw new ArgumentNullException(nameof(paths));
#else
        ArgumentNullException.ThrowIfNull(paths);
#endif
        var addresses = new S7SymbolicAddress[paths.Length];
        for (var i = 0; i < paths.Length; i++)
        {
            addresses[i] = (await ResolveSymbolAsync(paths[i], cancellationToken).ConfigureAwait(false)).Address;
        }

        return await _session.ReadAsync(addresses, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes multiple paths and retains each PLC error.</summary>
    /// <param name="values">The paths and values to write.</param>
    /// <returns>The operation result.</returns>
    public Task<S7SymbolicWriteResult[]> WriteManyAsync(Dictionary<string, S7SymbolicValue> values) => WriteManyAsync(values, CancellationToken.None);

    /// <summary>Writes multiple paths and retains each PLC error.</summary>
    /// <param name="values">The paths and values to write.</param>
    /// <param name="cancellationToken">Cancels the controller operation.</param>
    /// <returns>The operation result.</returns>
    public async Task<S7SymbolicWriteResult[]> WriteManyAsync(Dictionary<string, S7SymbolicValue> values, CancellationToken cancellationToken)
    {
#if NETFRAMEWORK
        _ = values ?? throw new ArgumentNullException(nameof(values));
#else
        ArgumentNullException.ThrowIfNull(values);
#endif
        var items = new List<S7SymbolicWriteItem>();
        foreach (var entry in values)
        {
            var symbol = await ResolveSymbolAsync(entry.Key, cancellationToken).ConfigureAwait(false);
            EnsureWritable(symbol);
            items.Add(new(symbol.Address, entry.Value));
        }

        return await _session.WriteAsync(items.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Releases the symbolic session.</summary>
    /// <returns>The operation result.</returns>
    public ValueTask DisposeAsync()
    {
        Task disposal;
        lock (_disposalGate)
        {
            _disposal ??= DisposeCoreAsync();
            disposal = _disposal;
        }

        return new(disposal);
    }

    /// <summary>Resolves a PLC path through cached or lazily explored native metadata.</summary>
    /// <param name="path">The PLC symbolic path.</param>
    /// <param name="cancellationToken">Cancels the controller operation.</param>
    /// <returns>The operation result.</returns>
    internal async Task<S7Symbol> ResolveSymbolAsync(string path, CancellationToken cancellationToken)
    {
#if NETFRAMEWORK
        _ = path ?? throw new ArgumentNullException(nameof(path));
#else
        ArgumentNullException.ThrowIfNull(path);
#endif
        var parts = S7SymbolPath.Parse(path);
        if (parts[0].Indices.Length != 0 || parts.Length < 2)
        {
            throw new ArgumentException("A member symbol is required.", nameof(path));
        }

        using var lease = await _browseGate.AcquireAsync(cancellationToken).ConfigureAwait(false);
        if (_symbols.TryGetValue(path, out var cached))
        {
            return cached;
        }

        await LoadRootsAsync(cancellationToken).ConfigureAwait(false);
        if (!_roots.TryGetValue(parts[0].Name, out var root))
        {
            throw new KeyNotFoundException($"The PLC root symbol was not found: {parts[0].Name}");
        }

        var typeId = root.TypeId;
        var ids = new List<uint>();
        PlusTypeMember? member = null;
        for (var i = 1; i < parts.Length; i++)
        {
            var members = await GetTypeAsync(typeId, cancellationToken).ConfigureAwait(false);
            member = FindMember(members, parts[i].Name);
            AppendCoordinates(member, parts[i], ids, i < parts.Length - 1);
            typeId = member.Relation;
        }

        var resolved = MakeSymbol(path, root, ids.ToArray(), member!, parts[^1].Indices.Length != 0);
        _symbols.Add(path, resolved);
        return resolved;
    }

    /// <summary>Recognizes PLC scalars that use an array-shaped wire representation.</summary>
    /// <param name="softType">The PLC soft datatype.</param>
    /// <returns>True for encoded string and BCD-date values.</returns>
    private static bool IsEncodedScalarType(uint softType) => softType is DateAndTimeSoftType or StringSoftType or WideStringSoftType;

    /// <summary>Determines whether a requested CLR type needs per-element access for nested wire arrays.</summary>
    /// <param name="symbol">The resolved PLC array declaration.</param>
    /// <param name="target">The requested CLR value type.</param>
    /// <returns>True when a STRING, WSTRING, or DATE_AND_TIME array needs element access.</returns>
    private static bool ShouldExpandEncodedArray(S7Symbol symbol, Type target) =>
        (target.IsArray || target == typeof(object)) && symbol.ArrayLengths.Count != 0 && IsEncodedScalarType(symbol.SoftDataType);

    /// <summary>Appends the flattened element offset and the struct-array member marker.</summary>
    /// <param name="member">The decoded array declaration.</param>
    /// <param name="part">The path coordinates to validate.</param>
    /// <param name="ids">The native identifier sequence being assembled.</param>
    private static void AppendArrayCoordinates(PlusTypeMember member, S7SymbolPath part, List<uint> ids)
    {
        ids.Add(member.Flatten(part.Indices));
        if (member.Relation == 0)
        {
            return;
        }

        ids.Add(1);
    }

    /// <summary>Finds the exact member name in decoded type metadata.</summary>
    /// <param name="members">The decoded member declarations.</param>
    /// <param name="name">The member name to resolve.</param>
    /// <returns>The matching member declaration.</returns>
    private static PlusTypeMember FindMember(PlusTypeMember[] members, string name)
    {
        foreach (var member in members)
        {
            if (!string.Equals(member.Name, name, StringComparison.Ordinal))
            {
                continue;
            }

            return member;
        }

        throw new KeyNotFoundException($"The PLC member was not found: {name}");
    }

    /// <summary>Appends a member and its optional array index to a native access path.</summary>
    /// <param name="member">The decoded member declaration.</param>
    /// <param name="part">The parsed member name and coordinates.</param>
    /// <param name="ids">The access sequence being assembled.</param>
    /// <param name="hasChild">Whether another member follows this part.</param>
    private static void AppendCoordinates(PlusTypeMember member, S7SymbolPath part, List<uint> ids, bool hasChild)
    {
        ids.Add(member.LocalId);
        if (part.Indices.Length == 0)
        {
            if (member.Lengths.Length != 0 && hasChild)
            {
                throw new ArgumentException("An array index is required before accessing a member.", nameof(part));
            }
        }
        else
        {
            AppendArrayCoordinates(member, part, ids);
        }

        if (!hasChild || member.Relation != 0)
        {
            return;
        }

        throw new ArgumentException("The symbol has no members.", nameof(part));
    }

    /// <summary>Rejects symbols whose HMI metadata prohibits writes.</summary>
    /// <param name="symbol">The resolved symbol metadata.</param>
    private static void EnsureWritable(S7Symbol symbol)
    {
        if (!symbol.IsReadOnly && symbol.IsAccessible)
        {
            return;
        }

        throw new InvalidOperationException("The symbol does not permit HMI writes.");
    }

    /// <summary>Combines root access information and member metadata into a symbol.</summary>
    /// <param name="path">The PLC symbolic path.</param>
    /// <param name="root">The native area containing the member.</param>
    /// <param name="ids">The resolved local identifier sequence.</param>
    /// <param name="member">The decoded member declaration.</param>
    /// <param name="element">Whether the address identifies one array element.</param>
    /// <returns>The operation result.</returns>
    private static S7Symbol MakeSymbol(string path, SymbolRoot root, uint[] ids, PlusTypeMember member, bool element) => new(
        path,
        new(root.Area, root.SubArea, ids),
        member.SoftType,
        member.Flags,
        member.Crc,
        (element ? [] : member.LowerBounds, element ? [] : member.Lengths, member.MaximumStringLength));

    /// <summary>Quotes a PLC name and escapes embedded quotes.</summary>
    /// <param name="name">The unquoted PLC name.</param>
    /// <returns>The operation result.</returns>
    private static string Quote(string name) => $"\"{name.Replace("\"", "\"\"")}\"";

    /// <summary>Visits explored objects and their descendants in wire order.</summary>
    /// <param name="objects">The explored object roots.</param>
    /// <returns>The operation result.</returns>
    private static IEnumerable<PlusObject> Walk(IEnumerable<PlusObject> objects)
    {
        foreach (var item in objects)
        {
            yield return item;
            foreach (var child in Walk(item.Children))
            {
                yield return child;
            }
        }
    }

    /// <summary>Writes string or BCD-date array elements with validated PLC dimensions.</summary>
    /// <param name="symbol">The resolved PLC array declaration.</param>
    /// <param name="array">The CLR array containing the values to write.</param>
    /// <param name="cancellationToken">Cancels the indexed writes.</param>
    /// <returns>A task completing after all element writes have succeeded.</returns>
    private async Task WriteEncodedArrayAsync(S7Symbol symbol, Array array, CancellationToken cancellationToken)
    {
        if (array.Rank != 1 || symbol.ArrayLengths.Count != 1 || array.Length != symbol.ArrayLengths[0])
        {
            throw new ArgumentException("The CLR array must match the PLC array shape.", nameof(array));
        }

        EnsureWritable(symbol);
        for (var i = 0; i < array.Length; i++)
        {
            var path = $"{symbol.Path}[{checked(symbol.ArrayLowerBounds[0] + i).ToString(CultureInfo.InvariantCulture)}]";
            var item = array.GetValue(array.GetLowerBound(0) + i) ?? throw new InvalidDataException("Array elements cannot be null.");
            var wire = S7SymbolicTypeConversion.FromClr(item, symbol.SoftDataType, symbol.MaximumStringLength);
            await WriteAsync(path, wire, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Reads encoded-string or BCD-date array elements using their native indexed paths.</summary>
    /// <param name="symbol">The resolved PLC array declaration.</param>
    /// <param name="target">The requested CLR array type, or object for natural values.</param>
    /// <param name="cancellationToken">Cancels the indexed controller reads.</param>
    /// <returns>The decoded CLR array.</returns>
    private async Task<object> ReadEncodedArrayAsync(S7Symbol symbol, Type target, CancellationToken cancellationToken)
    {
        if (symbol.ArrayLengths.Count != 1 || (target.IsArray && target.GetArrayRank() != 1))
        {
            throw new NotSupportedException("Encoded string and date arrays require one dimension.");
        }

        var elementType = target.IsArray ? target.GetElementType()! : typeof(object);
        var array = Array.CreateInstance(elementType, checked((int)symbol.ArrayLengths[0]));
        for (var i = 0; i < array.Length; i++)
        {
            var path = $"{symbol.Path}[{checked(symbol.ArrayLowerBounds[0] + i).ToString(CultureInfo.InvariantCulture)}]";
            var wire = await ReadAsync(path, cancellationToken).ConfigureAwait(false);
            array.SetValue(S7SymbolicTypeConversion.ToClr<object>(wire, symbol.SoftDataType), i);
        }

        return array;
    }

    /// <summary>Discovers loaded DB type identifiers and native controller areas.</summary>
    /// <param name="cancellationToken">Cancels the controller operation.</param>
    /// <returns>The operation result.</returns>
    private async Task LoadRootsAsync(CancellationToken cancellationToken)
    {
        if (!_session.IsConnected)
        {
            throw new InvalidOperationException("Connect the symbolic client first.");
        }

        if (_roots.Count != 0)
        {
            return;
        }

        var objects = await _session.ExploreAsync(ProgramObjectId, true, [VariableNameAttribute], cancellationToken).ConfigureAwait(false);
        var discovered = new List<SymbolRoot>();
        foreach (var item in Walk(objects))
        {
            var root = await ReadDataBlockRootAsync(item, cancellationToken).ConfigureAwait(false);
            if (root is not null)
            {
                discovered.Add(root);
            }
        }

        foreach (var root in discovered)
        {
            _roots.Add(root.Name, root);
        }

        _roots.Add("IArea", new("IArea", InputAccessArea, NativeActualSubArea, 0x90010000));
        _roots.Add("QArea", new("QArea", OutputAccessArea, NativeActualSubArea, 0x90020000));
        _roots.Add("MArea", new("MArea", MarkerAccessArea, NativeActualSubArea, 0x90030000));
        _roots.Add("S7Counters", new("S7Counters", CounterAccessArea, NativeActualSubArea, 0x90060000));
        _roots.Add("S7Timers", new("S7Timers", TimerAccessArea, NativeActualSubArea, 0x90050000));
    }

    /// <summary>Reads the loaded type relation for an explored DB object.</summary>
    /// <param name="item">The explored controller object.</param>
    /// <param name="cancellationToken">Cancels the controller read.</param>
    /// <returns>The loaded DB root, or null for another object or an unavailable DB.</returns>
    private async Task<SymbolRoot?> ReadDataBlockRootAsync(PlusObject item, CancellationToken cancellationToken)
    {
        if (item.ClassId != DataBlockClassId || (item.Id >> 16) != 0x8a0e || !item.Attributes.TryGetValue(VariableNameAttribute, out var name))
        {
            return null;
        }

        var read = (await _session.ReadAsync([new S7SymbolicAddress(item.Id, DataBlockActualSubArea, [1])], cancellationToken).ConfigureAwait(false))[0];
        if (!read.IsSuccess || read.Value?.Value is not uint typeId || typeId == 0)
        {
            return null;
        }

        var rootName = name.Value as string ?? throw new InvalidDataException("The DB name was not a string.");
        return new(rootName, item.Id, DataBlockActualSubArea, typeId);
    }

    /// <summary>Explores and caches the exact type relation requested by a symbol.</summary>
    /// <param name="id">The exact type relation identifier.</param>
    /// <param name="cancellationToken">Cancels the controller operation.</param>
    /// <returns>The operation result.</returns>
    private async Task<PlusTypeMember[]> GetTypeAsync(uint id, CancellationToken cancellationToken)
    {
        if (_types.TryGetValue(id, out var members))
        {
            return members;
        }

        var objects = await _session.ExploreAsync(id, false, [], cancellationToken).ConfigureAwait(false);
        PlusObject? type = null;
        foreach (var item in Walk(objects))
        {
            if (item.Id == id)
            {
                type = item;
                break;
            }
        }

        if (type is null)
        {
            throw new InvalidDataException("The PLC returned no type metadata for the symbol.");
        }

        members = PlusTypeDecoder.Decode(type.TypeInformation, type.Names);
        _types.Add(id, members);
        return members;
    }

    /// <summary>Visits scalar leaves and array declarations while checking recursive types.</summary>
    /// <param name="root">The native area containing the member.</param>
    /// <param name="id">The exact type relation identifier.</param>
    /// <param name="path">The PLC symbolic path.</param>
    /// <param name="prefix">The parent member local identifier sequence.</param>
    /// <param name="ancestors">The active type relations used to detect recursion.</param>
    /// <param name="result">The collected symbol declarations.</param>
    /// <param name="cancellationToken">Cancels the controller operation.</param>
    /// <returns>The operation result.</returns>
    private async Task BrowseTypeAsync(SymbolRoot root, uint id, string path, uint[] prefix, HashSet<uint> ancestors, List<S7Symbol> result, CancellationToken cancellationToken)
    {
        if (!ancestors.Add(id))
        {
            throw new InvalidDataException("Recursive PLC type metadata cannot be expanded.");
        }

        try
        {
            foreach (var member in await GetTypeAsync(id, cancellationToken).ConfigureAwait(false))
            {
                if (result.Count >= BrowseSymbolLimit)
                {
                    throw new InvalidDataException("The symbol browse limit was exceeded.");
                }

                var ids = new uint[prefix.Length + 1];
                Array.Copy(prefix, ids, prefix.Length);
                ids[^1] = member.LocalId;
                var memberPath = $"{path}.{Quote(member.Name)}";
                if (member.Relation != 0 && member.Lengths.Length == 0)
                {
                    await BrowseTypeAsync(root, member.Relation, memberPath, ids, ancestors, result, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    result.Add(MakeSymbol(memberPath, root, ids, member, false));
                }
            }
        }
        finally
        {
            _ = ancestors.Remove(id);
        }
    }

    /// <summary>Closes the session and drains admitted cache operations.</summary>
    /// <returns>The shared disposal operation.</returns>
    private async Task DisposeCoreAsync()
    {
        var gateShutdown = _browseGate.DisposeAsync();
        try
        {
            await _session.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            await gateShutdown.ConfigureAwait(false);
        }
    }

    /// <summary>Retains the native area and type relation for a named root.</summary>
    /// <param name="name">The root symbol name.</param>
    /// <param name="area">The native access area.</param>
    /// <param name="subArea">The actual-value subarea.</param>
    /// <param name="typeId">The loaded type relation identifier.</param>
    private sealed class SymbolRoot(string name, uint area, uint subArea, uint typeId)
    {
        /// <summary>Gets the root symbolic name.</summary>
        public string Name { get; } = name;

        /// <summary>Gets the native root access area.</summary>
        public uint Area { get; } = area;

        /// <summary>Gets the actual-value access subarea.</summary>
        public uint SubArea { get; } = subArea;

        /// <summary>Gets the root type relation identifier.</summary>
        public uint TypeId { get; } = typeId;
    }
}
