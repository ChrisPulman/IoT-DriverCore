// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Represents an immutable value read from or written to a logical tag.</summary>
public sealed class LogicalTagValue
{
    /// <summary>Initializes a new instance of the <see cref="LogicalTagValue"/> class without a quality code.</summary>
    /// <param name="tagName">The tag name; whitespace is trimmed.</param>
    /// <param name="value">The value payload, which may be <see langword="null"/>.</param>
    /// <param name="timestampUtc">The UTC timestamp of the value.</param>
    public LogicalTagValue(string tagName, object? value, DateTimeOffset timestampUtc)
        : this(tagName, value, timestampUtc, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LogicalTagValue"/> class.</summary>
    /// <param name="tagName">The tag name; whitespace is trimmed.</param>
    /// <param name="value">The value payload, which may be <see langword="null"/>.</param>
    /// <param name="timestampUtc">The UTC timestamp of the value.</param>
    /// <param name="quality">The optional quality code; whitespace is trimmed.</param>
    public LogicalTagValue(string tagName, object? value, DateTimeOffset timestampUtc, string? quality)
    {
        TagName = LogicalTag.Required(tagName, nameof(tagName));
        Value = value;
        TimestampUtc = timestampUtc.ToUniversalTime();
        Quality = quality?.Trim() ?? string.Empty;
    }

    /// <summary>Gets the tag name.</summary>
    public string TagName { get; }

    /// <summary>Gets the value payload.</summary>
    public object? Value { get; }

    /// <summary>Gets the UTC timestamp.</summary>
    public DateTimeOffset TimestampUtc { get; }

    /// <summary>Gets the optional quality code.</summary>
    public string Quality { get; }
}
