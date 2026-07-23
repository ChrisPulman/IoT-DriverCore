// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using IoT.DriverCore.Core;
#if REACTIVE_SHIM
using IoT.DriverCore.OmronPlcRx.Reactive.Core.Types;
using IoT.DriverCore.OmronPlcRx.Reactive.Tags;
#else
using IoT.DriverCore.OmronPlcRx.Core.Types;
using IoT.DriverCore.OmronPlcRx.Tags;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive;

#else
namespace IoT.DriverCore.OmronPlcRx;

#endif

/// <summary>Contains typed protocol dispatch for the logical-tag client.</summary>
public sealed partial class OmronLogicalTagClient
{
    /// <summary>Converts a logical value to a typed PLC value.</summary>
    /// <typeparam name="T">Target value type.</typeparam>
    /// <param name="value">Logical value.</param>
    /// <returns>The converted value.</returns>
    private static T? ConvertValue<T>(object? value)
    {
        if (value is null)
        {
            return default;
        }

        if (value is T typed)
        {
            return typed;
        }

        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T?)
            Convert.ChangeType(
                value,
                targetType,
                System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Gets the normalized logical data type key.</summary>
    /// <param name="tag">Logical tag.</param>
    /// <returns>The normalized type key.</returns>
    private static string GetTypeKey(LogicalTag tag)
    {
        var name = tag.DataType.Trim();
        var separator = name.LastIndexOf('.');
        return (separator >= 0 ? name.Substring(separator + 1) : name).ToUpperInvariant();
    }

    /// <summary>Determines whether an access mode permits an operation.</summary>
    /// <param name="actual">Configured access mode.</param>
    /// <param name="requested">Requested access mode.</param>
    /// <returns>True when the operation is permitted; otherwise false.</returns>
    private static bool Allows(LogicalTagAccessMode actual, LogicalTagAccessMode requested) =>
        actual == LogicalTagAccessMode.ReadWrite || actual == requested;

    /// <summary>Boxes the result of a typed asynchronous read.</summary>
    /// <typeparam name="T">PLC value type.</typeparam>
    /// <param name="task">Typed read task.</param>
    /// <returns>The boxed read value.</returns>
    private static async Task<object?> BoxAsync<T>(Task<T?> task) =>
        await task.ConfigureAwait(false);

    /// <summary>Adapts an observable to an asynchronous sequence.</summary>
    /// <param name="source">Logical value observable.</param>
    /// <param name="cancellationToken">Token used to cancel enumeration.</param>
    /// <returns>The asynchronous logical value sequence.</returns>
    private static async IAsyncEnumerable<LogicalTagValue> ToAsyncEnumerable(
        IObservable<LogicalTagValue> source,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<LogicalTagValue>();
        using var subscription = source.Subscribe(new ChannelObserver(channel.Writer));
        await foreach (
            var value in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)
        )
        {
            yield return value;
        }
    }

    /// <summary>Resolves and validates a logical tag.</summary>
    /// <param name="tagName">Logical tag name.</param>
    /// <param name="requested">Requested access mode.</param>
    /// <param name="tag">Resolved logical tag.</param>
    /// <param name="failure">Failure result when validation fails.</param>
    /// <returns>True when the tag is valid; otherwise false.</returns>
    private bool TryGetTag(
        string tagName,
        LogicalTagAccessMode requested,
        out LogicalTag? tag,
        out TagOperationResult<LogicalTagValue> failure)
    {
        ThrowIfDisposed();
        if (!Catalog.TryGet(tagName, out tag) || tag is null)
        {
            failure = TagOperationResult<LogicalTagValue>.Failure(
                $"Logical tag '{tagName}' is not registered.");
            return false;
        }

        if (!Allows(tag.AccessMode, requested))
        {
            failure = TagOperationResult<LogicalTagValue>.Failure(
                $"Logical tag '{tagName}' does not allow {requested.ToString().ToLowerInvariant()} operations.");
            return false;
        }

        failure = TagOperationResult<LogicalTagValue>.Failure("The operation was not executed.");
        return true;
    }

    /// <summary>Registers a logical tag with the typed Omron API.</summary>
    /// <param name="tag">Logical tag to register.</param>
    private void RegisterWithPlc(LogicalTag tag)
    {
        _ = GetTypeKey(tag) switch
        {
            "BOOLEAN" or "BOOL" => RegisterTyped<bool>(tag),
            "BYTE" => RegisterTyped<byte>(tag),
            "INT16" or "SHORT" => RegisterTyped<short>(tag),
            "UINT16" or "USHORT" => RegisterTyped<ushort>(tag),
            "INT32" or "INT" => RegisterTyped<int>(tag),
            "UINT32" or "UINT" => RegisterTyped<uint>(tag),
            "SINGLE" or "FLOAT" => RegisterTyped<float>(tag),
            "DOUBLE" => RegisterTyped<double>(tag),
            "STRING" => RegisterTyped<string>(tag),
            "BCD16" => RegisterTyped<Bcd16>(tag),
            "BCDU16" => RegisterTyped<BcdU16>(tag),
            "BCD32" => RegisterTyped<Bcd32>(tag),
            "BCDU32" => RegisterTyped<BcdU32>(tag),
            _ => throw new NotSupportedException(
                $"Logical tag data type '{tag.DataType}' is not supported by OmronPlcRx."),
        };
    }

    /// <summary>Registers one typed logical tag.</summary>
    /// <typeparam name="T">PLC value type.</typeparam>
    /// <param name="tag">Logical tag to register.</param>
    /// <returns>True after registration completes.</returns>
    private bool RegisterTyped<T>(LogicalTag tag)
    {
        _plc.AddUpdateTagItem(new PlcTag<T>(tag.Name, tag.Address));
        return true;
    }

    /// <summary>Reads a typed value from the Omron API and boxes it.</summary>
    /// <param name="tag">Logical tag to read.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The boxed PLC value.</returns>
    private Task<object?> ReadFromPlcAsync(LogicalTag tag, CancellationToken cancellationToken) =>
        GetTypeKey(tag) switch
        {
            "BOOLEAN" or "BOOL" => BoxAsync(
                _plc.ReadValueAsync(new LogicalTagKey<bool>(tag.Name), cancellationToken)),
            "BYTE" => BoxAsync(
                _plc.ReadValueAsync(new LogicalTagKey<byte>(tag.Name), cancellationToken)),
            "INT16" or "SHORT" => BoxAsync(
                _plc.ReadValueAsync(new LogicalTagKey<short>(tag.Name), cancellationToken)),
            "UINT16" or "USHORT" => BoxAsync(
                _plc.ReadValueAsync(new LogicalTagKey<ushort>(tag.Name), cancellationToken)),
            "INT32" or "INT" => BoxAsync(
                _plc.ReadValueAsync(new LogicalTagKey<int>(tag.Name), cancellationToken)),
            "UINT32" or "UINT" => BoxAsync(
                _plc.ReadValueAsync(new LogicalTagKey<uint>(tag.Name), cancellationToken)),
            "SINGLE" or "FLOAT" => BoxAsync(
                _plc.ReadValueAsync(new LogicalTagKey<float>(tag.Name), cancellationToken)),
            "DOUBLE" => BoxAsync(
                _plc.ReadValueAsync(new LogicalTagKey<double>(tag.Name), cancellationToken)),
            "STRING" => BoxAsync(
                _plc.ReadValueAsync(new LogicalTagKey<string>(tag.Name), cancellationToken)),
            "BCD16" => BoxAsync(
                _plc.ReadValueAsync(new LogicalTagKey<Bcd16>(tag.Name), cancellationToken)),
            "BCDU16" => BoxAsync(
                _plc.ReadValueAsync(new LogicalTagKey<BcdU16>(tag.Name), cancellationToken)),
            "BCD32" => BoxAsync(
                _plc.ReadValueAsync(new LogicalTagKey<Bcd32>(tag.Name), cancellationToken)),
            "BCDU32" => BoxAsync(
                _plc.ReadValueAsync(new LogicalTagKey<BcdU32>(tag.Name), cancellationToken)),
            _ => Task.FromException<object?>(
                new NotSupportedException(
                    $"Logical tag data type '{tag.DataType}' is not supported by OmronPlcRx.")),
        };

    /// <summary>Writes a converted logical value through the typed Omron API.</summary>
    /// <param name="tag">Logical tag to write.</param>
    /// <param name="value">Logical value.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    /// <returns>The boxed written value.</returns>
    private Task<object?> WriteToPlcAsync(
        LogicalTag tag,
        object? value,
        CancellationToken cancellationToken) =>
        GetTypeKey(tag) switch
        {
            "BOOLEAN" or "BOOL" => WriteConvertedAsync<bool>(tag, value, cancellationToken),
            "BYTE" => WriteConvertedAsync<byte>(tag, value, cancellationToken),
            "INT16" or "SHORT" => WriteConvertedAsync<short>(tag, value, cancellationToken),
            "UINT16" or "USHORT" => WriteConvertedAsync<ushort>(tag, value, cancellationToken),
            "INT32" or "INT" => WriteConvertedAsync<int>(tag, value, cancellationToken),
            "UINT32" or "UINT" => WriteConvertedAsync<uint>(tag, value, cancellationToken),
            "SINGLE" or "FLOAT" => WriteConvertedAsync<float>(tag, value, cancellationToken),
            "DOUBLE" => WriteConvertedAsync<double>(tag, value, cancellationToken),
            "STRING" => WriteConvertedAsync<string>(tag, value, cancellationToken),
            "BCD16" => WriteConvertedAsync<Bcd16>(tag, value, cancellationToken),
            "BCDU16" => WriteConvertedAsync<BcdU16>(tag, value, cancellationToken),
            "BCD32" => WriteConvertedAsync<Bcd32>(tag, value, cancellationToken),
            "BCDU32" => WriteConvertedAsync<BcdU32>(tag, value, cancellationToken),
            _ => throw new NotSupportedException(
                $"Logical tag data type '{tag.DataType}' is not supported by OmronPlcRx."),
        };

    /// <summary>Observes a typed value from the Omron API.</summary>
    /// <param name="tag">Logical tag to observe.</param>
    /// <returns>The logical value stream.</returns>
    private IObservable<LogicalTagValue> ObserveFromPlc(LogicalTag tag) =>
        GetTypeKey(tag) switch
        {
            "BOOLEAN" or "BOOL" => ObserveTyped<bool>(tag),
            "BYTE" => ObserveTyped<byte>(tag),
            "INT16" or "SHORT" => ObserveTyped<short>(tag),
            "UINT16" or "USHORT" => ObserveTyped<ushort>(tag),
            "INT32" or "INT" => ObserveTyped<int>(tag),
            "UINT32" or "UINT" => ObserveTyped<uint>(tag),
            "SINGLE" or "FLOAT" => ObserveTyped<float>(tag),
            "DOUBLE" => ObserveTyped<double>(tag),
            "STRING" => ObserveTyped<string>(tag),
            "BCD16" => ObserveTyped<Bcd16>(tag),
            "BCDU16" => ObserveTyped<BcdU16>(tag),
            "BCD32" => ObserveTyped<Bcd32>(tag),
            "BCDU32" => ObserveTyped<BcdU32>(tag),
            _ => throw new NotSupportedException(
                $"Logical tag data type '{tag.DataType}' is not supported by OmronPlcRx."),
        };

    /// <summary>Adapts a typed PLC stream to logical values.</summary>
    /// <typeparam name="T">PLC value type.</typeparam>
    /// <param name="tag">Logical tag to observe.</param>
    /// <returns>The adapted observable.</returns>
    private TagValueObservable<T> ObserveTyped<T>(LogicalTag tag) =>
        new(_plc.Observe(new LogicalTagKey<T>(tag.Name)), tag.Name, _timeProvider);

    /// <summary>Writes one typed PLC value.</summary>
    /// <typeparam name="T">PLC value type.</typeparam>
    /// <param name="tag">Logical tag to write.</param>
    /// <param name="value">Value to write.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    /// <returns>The boxed written value.</returns>
    private async Task<object?> WriteTypedAsync<T>(
        LogicalTag tag,
        T? value,
        CancellationToken cancellationToken)
    {
        await _plc
            .WriteValueAsync(new LogicalTagKey<T>(tag.Name), value, cancellationToken)
            .ConfigureAwait(false);
        return value;
    }

    /// <summary>Converts and writes one PLC value.</summary>
    /// <typeparam name="T">PLC value type.</typeparam>
    /// <param name="tag">Logical tag to write.</param>
    /// <param name="value">Logical value to convert.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    /// <returns>The boxed written value.</returns>
    private Task<object?> WriteConvertedAsync<T>(
        LogicalTag tag,
        object? value,
        CancellationToken cancellationToken) => WriteTypedAsync(tag, ConvertValue<T>(value), cancellationToken);

    /// <summary>Gets the configured SQLite store.</summary>
    /// <returns>The configured store.</returns>
    private LogicalTagSqliteStore GetStore()
    {
        ThrowIfDisposed();
        return _store
            ?? throw new InvalidOperationException(
                "This logical-tag client was not configured with a SQLite store.");
    }

    /// <summary>Throws when the client has been disposed.</summary>
    private void ThrowIfDisposed()
    {
        if (!_disposed)
        {
            return;
        }

        throw new ObjectDisposedException(nameof(OmronLogicalTagClient));
    }

    /// <summary>Adapts a typed tag observable to logical tag values.</summary>
    /// <typeparam name="T">PLC value type.</typeparam>
    /// <param name="source">Typed source observable.</param>
    /// <param name="tagName">Logical tag name.</param>
    /// <param name="timeProvider">Time provider used to stamp tag values.</param>
    private sealed class TagValueObservable<T>(IObservable<T?> source, string tagName, TimeProvider timeProvider)
        : IObservable<LogicalTagValue>
    {
        /// <inheritdoc />
        public IDisposable Subscribe(IObserver<LogicalTagValue> observer)
        {
            if (observer is null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            return source.Subscribe(new TagValueObserver<T>(observer, tagName, timeProvider));
        }
    }

    /// <summary>Adapts typed observer notifications to logical tag values.</summary>
    /// <typeparam name="T">PLC value type.</typeparam>
    /// <param name="observer">Logical value observer.</param>
    /// <param name="tagName">Logical tag name.</param>
    /// <param name="timeProvider">Time provider used to stamp tag values.</param>
    private sealed class TagValueObserver<T>(IObserver<LogicalTagValue> observer, string tagName, TimeProvider timeProvider)
        : IObserver<T?>
    {
        /// <inheritdoc />
        public void OnCompleted() => observer.OnCompleted();

        /// <inheritdoc />
        public void OnError(Exception error) => observer.OnError(error);

        /// <inheritdoc />
        public void OnNext(T? value) =>
            observer.OnNext(new LogicalTagValue(tagName, value, timeProvider.GetUtcNow()));
    }

    /// <summary>Writes observer notifications to an asynchronous channel.</summary>
    /// <param name="writer">Logical value channel writer.</param>
    private sealed class ChannelObserver(ChannelWriter<LogicalTagValue> writer)
        : IObserver<LogicalTagValue>
    {
        /// <inheritdoc />
        public void OnCompleted() => writer.TryComplete();

        /// <inheritdoc />
        public void OnError(Exception error) => writer.TryComplete(error);

        /// <inheritdoc />
        public void OnNext(LogicalTagValue value) => writer.TryWrite(value);
    }

    /// <summary>Merges multiple logical tag observables.</summary>
    /// <param name="sources">Logical value source observables.</param>
    private sealed class MergedObservable(IReadOnlyList<IObservable<LogicalTagValue>> sources)
        : IObservable<LogicalTagValue>
    {
        /// <inheritdoc />
        public IDisposable Subscribe(IObserver<LogicalTagValue> observer)
        {
            var subscriptions = new List<IDisposable>(sources.Count);
            try
            {
                foreach (var source in sources)
                {
                    subscriptions.Add(source.Subscribe(observer));
                }

                return new CompositeSubscription(subscriptions);
            }
            catch
            {
                foreach (var subscription in subscriptions)
                {
                    subscription.Dispose();
                }

                throw;
            }
        }
    }

    /// <summary>Disposes a collection of subscriptions together.</summary>
    /// <param name="subscriptions">Subscriptions to dispose.</param>
    private sealed class CompositeSubscription(IReadOnlyList<IDisposable> subscriptions)
        : IDisposable
    {
        /// <summary>Tracks whether the subscription group is disposed.</summary>
        private int _disposed;

        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            foreach (var subscription in subscriptions)
            {
                subscription.Dispose();
            }
        }
    }
}
