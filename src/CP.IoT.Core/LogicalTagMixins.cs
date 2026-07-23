// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Provides typed convenience methods that compose implementations of logical tag contracts.</summary>
public static class LogicalTagMixins
{
    /// <summary>Extension members for <see cref="ILogicalTagReader"/> that add typed and batch-read helpers.</summary>
    /// <param name="reader">The reader instance to extend.</param>
    extension(ILogicalTagReader reader)
    {
        /// <summary>Reads and type-checks one typed logical tag value without cancellation.</summary>
        /// <typeparam name="T">The expected value type.</typeparam>
        /// <param name="tag">The typed tag key.</param>
        /// <returns>A typed result containing the value or an error message.</returns>
        public Task<TagOperationResult<T>> ReadAsync<T>(LogicalTagKey<T> tag) =>
            reader.ReadAsync(tag, CancellationToken.None);

        /// <summary>Reads and type-checks one typed logical tag value.</summary>
        /// <typeparam name="T">The expected value type.</typeparam>
        /// <param name="tag">The typed tag key.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>A typed result containing the value or an error message.</returns>
        public Task<TagOperationResult<T>> ReadAsync<T>(LogicalTagKey<T> tag, CancellationToken cancellationToken)
        {
            if (tag is null)
            {
                throw new ArgumentNullException(nameof(tag));
            }

            return ReadTypedAsync<T>(reader, tag.Name, cancellationToken);
        }

        /// <summary>Reads all named tags while preserving input order without cancellation.</summary>
        /// <param name="tagNames">The tag names to read.</param>
        /// <returns>The operation results in the same order as <paramref name="tagNames"/>.</returns>
        public Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> ReadAllAsync(IEnumerable<string> tagNames) =>
            reader.ReadAllAsync(tagNames, CancellationToken.None);

        /// <summary>Reads all named tags while preserving input order.</summary>
        /// <param name="tagNames">The tag names to read.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation results in the same order as <paramref name="tagNames"/>.</returns>
        public Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> ReadAllAsync(
            IEnumerable<string> tagNames,
            CancellationToken cancellationToken)
        {
            if (tagNames is null)
            {
                throw new ArgumentNullException(nameof(tagNames));
            }

            var names = tagNames.Select(name => LogicalTag.Required(name, nameof(tagNames))).ToArray();
            return reader.ReadManyAsync(names, cancellationToken);
        }
    }

    /// <summary>Extension members for <see cref="ILogicalTagWriter"/> that add typed and batch-write helpers.</summary>
    /// <param name="writer">The writer instance to extend.</param>
    extension(ILogicalTagWriter writer)
    {
        /// <summary>Writes one typed logical tag value and returns the accepted value without cancellation.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="tag">The typed tag key.</param>
        /// <param name="value">The value to write.</param>
        /// <returns>A typed result containing the accepted value or an error message.</returns>
        public Task<TagOperationResult<T>> WriteAsync<T>(LogicalTagKey<T> tag, T value) =>
            writer.WriteAsync(tag, value, CancellationToken.None);

        /// <summary>Writes one typed logical tag value and returns the accepted value.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="tag">The typed tag key.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>A typed result containing the accepted value or an error message.</returns>
        public Task<TagOperationResult<T>> WriteAsync<T>(LogicalTagKey<T> tag, T value, CancellationToken cancellationToken)
        {
            if (tag is null)
            {
                throw new ArgumentNullException(nameof(tag));
            }

            return WriteTypedAsync(writer, tag.Name, value, cancellationToken);
        }

        /// <summary>Writes all values while preserving input order without cancellation.</summary>
        /// <param name="values">The values to write.</param>
        /// <returns>The operation results in the same order as <paramref name="values"/>.</returns>
        public Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> WriteAllAsync(IEnumerable<LogicalTagValue> values) =>
            writer.WriteAllAsync(values, CancellationToken.None);

        /// <summary>Writes all values while preserving input order.</summary>
        /// <param name="values">The values to write.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation results in the same order as <paramref name="values"/>.</returns>
        public Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> WriteAllAsync(
            IEnumerable<LogicalTagValue> values,
            CancellationToken cancellationToken)
        {
            if (values is null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var materialized = values.ToArray();

            if (materialized.Any(static v => v is null))
            {
                throw new ArgumentException("Values cannot contain null entries.", nameof(values));
            }

            return writer.WriteManyAsync(materialized, cancellationToken);
        }
    }

    /// <summary>Reads a typed value from a reader by tag name.</summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="reader">The reader to use.</param>
    /// <param name="tagName">The tag name.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A typed result containing the value or an error message.</returns>
    private static async Task<TagOperationResult<T>> ReadTypedAsync<T>(
        ILogicalTagReader reader,
        string tagName,
        CancellationToken cancellationToken)
    {
        var result = await reader.ReadAsync(tagName, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return TagOperationResult<T>.Failure(result.Error);
        }

        if (result.Value is null || !TryGetValue(result.Value.Value, out T? value))
        {
            var actualType = result.Value?.Value?.GetType().FullName ?? "null";
            return TagOperationResult<T>.Failure($"Tag '{tagName}' returned {actualType}, not {typeof(T).FullName}.");
        }

        return TagOperationResult<T>.Success(value!);
    }

    /// <summary>Writes a typed value to a writer by tag name.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="writer">The writer to use.</param>
    /// <param name="tagName">The tag name.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A typed result containing the accepted value or an error message.</returns>
    private static async Task<TagOperationResult<T>> WriteTypedAsync<T>(
        ILogicalTagWriter writer,
        string tagName,
        T value,
        CancellationToken cancellationToken)
    {
        var name = LogicalTag.Required(tagName, nameof(tagName));
        var tagValue = new LogicalTagValue(name, value, DateTimeOffset.UtcNow);
        var result = await writer.WriteAsync(tagValue, cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? TagOperationResult<T>.Success(value) : TagOperationResult<T>.Failure(result.Error);
    }

    /// <summary>Tries to cast <paramref name="source"/> to <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="source">The value to cast.</param>
    /// <param name="value">The cast result, or <see langword="default"/> when the cast fails.</param>
    /// <returns><see langword="true"/> if the cast succeeded.</returns>
    private static bool TryGetValue<T>(object? source, out T? value)
    {
        if (source is T typed)
        {
            value = typed;
            return true;
        }

        if (source is null && default(T) is null)
        {
            value = default;
            return true;
        }

        value = default;
        return false;
    }
}
