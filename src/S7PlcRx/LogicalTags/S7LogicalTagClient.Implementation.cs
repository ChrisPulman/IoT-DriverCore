// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;
using IoT.DriverCore.Core;
#if REACTIVE_SHIM
namespace IoT.DriverCore.S7PlcRx.Reactive.LogicalTags;

#else
namespace IoT.DriverCore.S7PlcRx.LogicalTags;

#endif

/// <summary>Contains the runtime implementation of <see cref="S7LogicalTagClient"/>.</summary>
public sealed partial class S7LogicalTagClient
{
    /// <summary>Enumerates a source while honoring cancellation.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cancellable sequence.</returns>
    private static async IAsyncEnumerable<T> WithCancellationToken<T>(
        IAsyncEnumerable<T> source,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var enumerator = source.GetAsyncEnumerator(cancellationToken);
        while (await enumerator.MoveNextAsync().ConfigureAwait(false))
        {
            yield return enumerator.Current;
        }
    }

    /// <summary>Validates and normalizes a required string value.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <returns>The normalized value.</returns>
    private static string Required(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A non-empty value is required.", parameterName)
            : value.Trim();

    /// <summary>Creates a logical tag value with the current timestamp.</summary>
    /// <param name="name">The tag name.</param>
    /// <param name="value">The tag value.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <returns>The logical tag value.</returns>
    private static LogicalTagValue CreateValue(string name, object? value, TimeProvider timeProvider) =>
        new(name, value, timeProvider.GetUtcNow(), "Good");

    /// <summary>Resolves the runtime type represented by a logical tag.</summary>
    /// <param name="tag">The logical tag.</param>
    /// <returns>The runtime type.</returns>
    private static Type ResolveType(LogicalTag tag)
    {
        var normalized = tag.DataType.Trim().ToUpperInvariant().Replace("SYSTEM.", string.Empty);
        var array = normalized.EndsWith("[]", StringComparison.Ordinal);
        var scalar = ResolveScalarType(
            array ? normalized[..^ArrayTypeSuffixLength] : normalized,
            tag.DataType);
        return array && scalar != typeof(object) ? scalar.MakeArrayType() : scalar;
    }

    /// <summary>Resolves a non-array S7 data type.</summary>
    /// <param name="normalized">The normalized type name.</param>
    /// <param name="originalDataType">The original type name.</param>
    /// <returns>The runtime type.</returns>
    private static Type ResolveScalarType(string normalized, string originalDataType) =>
        TypeMap.TryGetValue(normalized, out var scalar)
            ? scalar
            : Type.GetType(originalDataType, throwOnError: false, ignoreCase: true)
                ?? typeof(object);

    /// <summary>Gets the configured length for an array tag.</summary>
    /// <param name="tag">The logical tag.</param>
    /// <returns>The configured length, if present.</returns>
    private static int? GetArrayLength(LogicalTag tag) =>
        tag.Metadata.TryGetValue("ArrayLength", out var value)
        && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var length)
        && length > 0
            ? length
            : null;

    /// <summary>Converts a value to the specified target type.</summary>
    /// <param name="value">The value to convert.</param>
    /// <param name="targetType">The target type.</param>
    /// <returns>The converted value.</returns>
    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value is null || targetType == typeof(object) || targetType.IsInstanceOfType(value))
        {
            return value;
        }

        if (targetType.IsArray)
        {
            throw new InvalidCastException(
                $"A value of type '{value.GetType().FullName}' cannot be converted to '{targetType.FullName}'.");
        }

        return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
    }

    /// <summary>Creates the pending items for a multi-tag read.</summary>
    /// <param name="names">The requested names.</param>
    /// <param name="results">The result buffer.</param>
    /// <returns>The pending read items.</returns>
    private List<(int Index, LogicalTag Definition, Tag RuntimeTag)> CreatePendingReads(
        string[] names,
        TagOperationResult<LogicalTagValue>[] results)
    {
        var pending = new List<(int Index, LogicalTag Definition, Tag RuntimeTag)>();
        for (var index = 0; index < names.Length; index++)
        {
            if (
                !TryGetTag(
                    names[index],
                    LogicalTagAccessMode.Write,
                    out var definition,
                    out var failure))
            {
                results[index] = failure!;
                continue;
            }

            var runtimeTag = _plc.TagList[definition!.Name];
            results[index] = runtimeTag is null
                ? TagOperationResult<LogicalTagValue>.Failure(
                    $"S7 tag '{definition.Name}' is not registered.")
                : default!;
            if (runtimeTag is not null)
            {
                pending.Add((index, definition, runtimeTag));
            }
        }

        return pending;
    }

    /// <summary>Reads pending tag items using the S7 multi-variable operation.</summary>
    /// <param name="batchOperations">The S7 batch-operation adapter.</param>
    /// <param name="pending">The pending read items.</param>
    /// <param name="results">The result buffer.</param>
    /// <returns>The populated result buffer.</returns>
    private TagOperationResult<LogicalTagValue>[] ReadMultiple(
        IS7LogicalBatchOperations batchOperations,
        IReadOnlyList<(int Index, LogicalTag Definition, Tag RuntimeTag)> pending,
        TagOperationResult<LogicalTagValue>[] results)
    {
        try
        {
            var values = batchOperations.ReadMultiple(
                pending.Select(static item => item.RuntimeTag).ToArray());
            foreach (var item in pending)
            {
                var value =
                    values is not null && values.TryGetValue(item.Definition.Name, out var found)
                        ? found
                        : item.RuntimeTag.Value;
                results[item.Index] = TagOperationResult<LogicalTagValue>.Success(
                    CreateValue(item.Definition.Name, value, _timeProvider));
            }
        }
        catch (Exception ex)
        {
            foreach (var item in pending)
            {
                results[item.Index] = TagOperationResult<LogicalTagValue>.Failure(
                    ex.GetBaseException().Message);
            }
        }

        return results;
    }

    /// <summary>Reads pending tag items one at a time.</summary>
    /// <param name="pending">The pending read items.</param>
    /// <param name="results">The result buffer.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The populated result buffer.</returns>
    private async Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> ReadIndividuallyAsync(
        IReadOnlyList<(int Index, LogicalTag Definition, Tag RuntimeTag)> pending,
        TagOperationResult<LogicalTagValue>[] results,
        CancellationToken cancellationToken)
    {
        foreach (var item in pending)
        {
            results[item.Index] = await ReadAsync(item.Definition.Name, cancellationToken)
                .ConfigureAwait(false);
        }

        return results;
    }

    /// <summary>Creates the pending items for a multi-tag write.</summary>
    /// <param name="values">The requested values.</param>
    /// <param name="results">The result buffer.</param>
    /// <param name="parameterName">The caller parameter name.</param>
    /// <returns>The pending write items.</returns>
    private List<(
        int Index,
        LogicalTagValue Requested,
        object? Converted,
        Tag RuntimeTag)> CreatePendingWrites(
        LogicalTagValue[] values,
        TagOperationResult<LogicalTagValue>[] results,
        string parameterName)
    {
        var pending =
            new List<(int Index, LogicalTagValue Requested, object? Converted, Tag RuntimeTag)>();
        for (var index = 0; index < values.Length; index++)
        {
            var value =
                values[index]
                ?? throw new ArgumentException(
                    "Values cannot contain null entries.",
                    parameterName);
            if (
                !TryGetTag(
                    value.TagName,
                    LogicalTagAccessMode.Read,
                    out var definition,
                    out var failure))
            {
                results[index] = failure!;
                continue;
            }

            try
            {
                var runtimeTag =
                    _plc.TagList[definition!.Name]
                    ?? throw new InvalidOperationException(
                        $"S7 tag '{definition.Name}' is not registered.");
                var converted = ConvertValue(value.Value, ResolveType(definition));
                runtimeTag.NewValue = converted;
                pending.Add((index, value, converted, runtimeTag));
            }
            catch (Exception ex)
            {
                results[index] = TagOperationResult<LogicalTagValue>.Failure(
                    ex.GetBaseException().Message);
            }
        }

        return pending;
    }

    /// <summary>Writes pending tag items using the S7 multi-variable operation.</summary>
    /// <param name="batchOperations">The S7 batch-operation adapter.</param>
    /// <param name="pending">The pending write items.</param>
    /// <param name="results">The result buffer.</param>
    /// <returns>The populated result buffer.</returns>
    private TagOperationResult<LogicalTagValue>[] WriteMultiple(
        IS7LogicalBatchOperations batchOperations,
        IReadOnlyList<(
            int Index,
            LogicalTagValue Requested,
            object? Converted,
            Tag RuntimeTag)> pending,
        TagOperationResult<LogicalTagValue>[] results)
    {
        var succeeded = batchOperations.WriteMultiple(
            pending.Select(static item => item.RuntimeTag).ToArray());
        foreach (var item in pending)
        {
            results[item.Index] = succeeded
                ? TagOperationResult<LogicalTagValue>.Success(
                    CreateValue(item.Requested.TagName, item.Converted, _timeProvider))
                : TagOperationResult<LogicalTagValue>.Failure(
                    $"S7 batch write failed for '{item.Requested.TagName}'.");
        }

        return results;
    }

    /// <summary>Writes pending tag items one at a time.</summary>
    /// <param name="pending">The pending write items.</param>
    /// <param name="results">The result buffer.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The populated result buffer.</returns>
    private async Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> WriteIndividuallyAsync(
        IReadOnlyList<(
            int Index,
            LogicalTagValue Requested,
            object? Converted,
            Tag RuntimeTag)> pending,
        TagOperationResult<LogicalTagValue>[] results,
        CancellationToken cancellationToken)
    {
        foreach (var item in pending)
        {
            results[item.Index] = await WriteAsync(
                    new LogicalTagValue(
                        item.Requested.TagName,
                        item.Converted,
                        _timeProvider.GetUtcNow()),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return results;
    }

    /// <summary>Registers a logical tag with the S7 connection.</summary>
    /// <param name="tag">The logical tag.</param>
    private void RegisterWithPlc(LogicalTag tag)
    {
        var runtimeType = ResolveType(tag);
        _ = TagOperations.AddUpdateTagItem(_plc, runtimeType, tag.Name, tag.Address, GetArrayLength(tag))
            .SetPolling(tag.AccessMode != LogicalTagAccessMode.Write);
        lock (_registeredTags)
        {
            _ = _registeredTags.Add(tag.Name);
        }
    }

    /// <summary>Synchronizes S7 registrations when the logical catalog changes.</summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The change event arguments.</param>
    private void OnCatalogChanged(object? sender, LogicalTagChangedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        if (e.Kind == LogicalTagChangeKind.Removed)
        {
            lock (_registeredTags)
            {
                if (_registeredTags.Remove(e.Tag.Name))
                {
                    TagOperations.RemoveTagItem(_plc, e.Tag.Name);
                }
            }

            return;
        }

        RegisterWithPlc(e.Tag);
    }

    /// <summary>Attempts to resolve a tag that allows the requested operation.</summary>
    /// <param name="name">The tag name.</param>
    /// <param name="forbiddenMode">The forbidden access mode.</param>
    /// <param name="tag">The resolved tag.</param>
    /// <param name="failure">The operation failure, if any.</param>
    /// <returns>True when a permitted tag was found.</returns>
    private bool TryGetTag(
        string name,
        LogicalTagAccessMode forbiddenMode,
        out LogicalTag? tag,
        out TagOperationResult<LogicalTagValue>? failure)
    {
        name = Required(name, nameof(name));
        if (!Catalog.TryGet(name, out tag))
        {
            failure = TagOperationResult<LogicalTagValue>.Failure(
                $"Logical tag '{name}' was not found.");
            return false;
        }

        if (tag!.AccessMode == forbiddenMode)
        {
            failure = TagOperationResult<LogicalTagValue>.Failure(
                $"Logical tag '{name}' does not permit this operation.");
            return false;
        }

        failure = null;
        return true;
    }

    /// <summary>Reads the current value for a logical tag.</summary>
    /// <param name="tag">The logical tag.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current tag value.</returns>
    private async Task<object?> ReadValueAsync(LogicalTag tag, CancellationToken cancellationToken)
    {
        if (_plc is RxS7 rx)
        {
            var runtimeTag =
                _plc.TagList[tag.Name]
                ?? throw new InvalidOperationException($"S7 tag '{tag.Name}' is not registered.");
            var values = rx.ReadMultiVar([runtimeTag]);
            return values is not null && values.TryGetValue(tag.Name, out var value)
                ? value
                : runtimeTag.Value;
        }

        var method = typeof(IRxS7)
            .GetMethods()
            .Single(static method =>
                method.Name == nameof(IRxS7.ReadAsync)
                && method.IsGenericMethodDefinition
                && method.GetParameters().Length == 2);
        var valueType = ResolveType(tag);
        var keyType = typeof(LogicalTagKey<>).MakeGenericType(valueType);
        var key = Activator.CreateInstance(keyType, tag.Name)
            ?? throw new InvalidOperationException("The logical tag key could not be created.");
        var task = method.MakeGenericMethod(valueType).Invoke(_plc, [key, cancellationToken])
            as Task
            ?? throw new InvalidOperationException("The S7 value operation did not return a task.");
        await task.ConfigureAwait(false);
        return task.GetType()
            .GetProperty(nameof(Task<>.Result), BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(task);
    }

    /// <summary>Writes a logical tag value using the generic S7 write operation.</summary>
    /// <param name="tag">The logical tag.</param>
    /// <param name="value">The value to write.</param>
    private void InvokeWrite(LogicalTag tag, object? value)
    {
        var method = typeof(IRxS7)
            .GetMethods()
            .Single(static method =>
                method.Name == nameof(IRxS7.Value)
                && method.IsGenericMethodDefinition
                && method.ReturnType == typeof(void));
        _ = method.MakeGenericMethod(ResolveType(tag)).Invoke(_plc, [tag.Name, value]);
    }

    /// <summary>Gets the configured persistence store.</summary>
    /// <returns>The configured persistence store.</returns>
    private LogicalTagSqliteStore GetStore()
    {
        ThrowIfDisposed();
        return _store
            ?? throw new InvalidOperationException(
                "No SQLite store is configured. Call InitializeStoreAsync first or pass a store to the constructor.");
    }

    /// <summary>Throws when the client has been disposed.</summary>
    private void ThrowIfDisposed()
    {
        if (!_disposed)
        {
            return;
        }

        throw new ObjectDisposedException(nameof(S7LogicalTagClient));
    }
}
