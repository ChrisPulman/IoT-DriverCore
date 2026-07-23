// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Represents the outcome of an operation without requiring exceptions for expected failures.</summary>
/// <typeparam name="T">The success payload type.</typeparam>
public sealed class TagOperationResult<T>
{
    /// <summary>Initializes a new instance of the <see cref="TagOperationResult{T}"/> class.</summary>
    /// <param name="succeeded">Whether the operation succeeded.</param>
    /// <param name="value">The success payload, or <see langword="default"/> on failure.</param>
    /// <param name="error">The failure message, or <see langword="null"/> on success.</param>
    private TagOperationResult(bool succeeded, T? value, string? error)
    {
        Succeeded = succeeded;
        Value = value;
        Error = error ?? string.Empty;
    }

    /// <summary>Gets whether the operation succeeded.</summary>
    public bool Succeeded { get; }

    /// <summary>Gets the success payload, or the default value when unsuccessful.</summary>
    public T? Value { get; }

    /// <summary>Gets the failure message, or an empty string when successful.</summary>
    public string Error { get; }

    /// <summary>Creates a successful result.</summary>
    /// <param name="value">The success payload.</param>
    /// <returns>A successful <see cref="TagOperationResult{T}"/>.</returns>
    public static TagOperationResult<T> Success(T value) => new(true, value, null);

    /// <summary>Creates a failed result.</summary>
    /// <param name="error">A non-empty failure message.</param>
    /// <returns>A failed <see cref="TagOperationResult{T}"/>.</returns>
    public static TagOperationResult<T> Failure(string error) =>
        new(false, default, LogicalTag.Required(error, nameof(error)));
}
