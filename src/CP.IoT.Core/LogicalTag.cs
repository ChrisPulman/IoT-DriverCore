// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace CP.IoT.Core;

/// <summary>Describes an immutable logical PLC tag.</summary>
public sealed class LogicalTag
{
    /// <summary>Initializes a new instance of the <see cref="LogicalTag"/> class with required fields only.</summary>
    /// <param name="name">The unique tag name; whitespace is trimmed.</param>
    /// <param name="address">The protocol-specific PLC address; whitespace is trimmed.</param>
    /// <param name="dataType">The logical data type; whitespace is trimmed.</param>
    public LogicalTag(string name, string address, string dataType)
        : this(name, address, dataType, new LogicalTagOptions())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LogicalTag"/> class.</summary>
    /// <param name="name">The unique tag name; whitespace is trimmed.</param>
    /// <param name="address">The protocol-specific PLC address; whitespace is trimmed.</param>
    /// <param name="dataType">The logical data type; whitespace is trimmed.</param>
    /// <param name="options">The optional tag settings.</param>
    public LogicalTag(string name, string address, string dataType, LogicalTagOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        Name = Required(name, nameof(name));
        Address = Required(address, nameof(address));
        DataType = Required(dataType, nameof(dataType));
        GroupName = options.GroupName?.Trim() ?? string.Empty;
        Description = options.Description?.Trim() ?? string.Empty;
        Metadata = Copy(options.Metadata);

        if (!Enum.IsDefined(typeof(LogicalTagAccessMode), options.AccessMode))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The access mode is not defined.");
        }

        AccessMode = options.AccessMode;

        if (options.ScanInterval is { } interval && interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The scan interval must be greater than zero.");
        }

        ScanInterval = options.ScanInterval;
    }

    /// <summary>Gets the unique tag name.</summary>
    public string Name { get; }

    /// <summary>Gets the protocol-specific PLC address.</summary>
    public string Address { get; }

    /// <summary>Gets the logical data type.</summary>
    public string DataType { get; }

    /// <summary>Gets the optional group name.</summary>
    public string GroupName { get; }

    /// <summary>Gets the optional human-readable description.</summary>
    public string Description { get; }

    /// <summary>Gets immutable tag metadata.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>Gets the permitted read/write access mode.</summary>
    public LogicalTagAccessMode AccessMode { get; }

    /// <summary>Gets the optional poll interval. A <see langword="null"/> value leaves scheduling to the adapter.</summary>
    public TimeSpan? ScanInterval { get; }

    /// <summary>Returns the current optional settings as a <see cref="LogicalTagOptions"/> instance.</summary>
    /// <returns>A new <see cref="LogicalTagOptions"/> initialised from this tag's properties.</returns>
    public LogicalTagOptions CurrentOptions()
    {
        var groupName = GroupName;
        var description = Description;
        var metadata = Metadata;
        var accessMode = AccessMode;
        var scanInterval = ScanInterval;

        return new()
        {
            GroupName = groupName,
            Description = description,
            Metadata = metadata,
            AccessMode = accessMode,
            ScanInterval = scanInterval,
        };
    }

    /// <summary>Returns a copy of this tag with a different address.</summary>
    /// <param name="address">The new address.</param>
    /// <returns>A new <see cref="LogicalTag"/> with the updated address.</returns>
    public LogicalTag WithAddress(string address) =>
        new(Name, address, DataType, CurrentOptions());

    /// <summary>Returns a copy of this tag with a different data type.</summary>
    /// <param name="dataType">The new data type.</param>
    /// <returns>A new <see cref="LogicalTag"/> with the updated data type.</returns>
    public LogicalTag WithDataType(string dataType) =>
        new(Name, Address, dataType, CurrentOptions());

    /// <summary>Returns a copy of this tag with all optional settings replaced.</summary>
    /// <param name="options">The new options.</param>
    /// <returns>A new <see cref="LogicalTag"/> with the updated options.</returns>
    public LogicalTag WithOptions(LogicalTagOptions options) =>
        new(Name, Address, DataType, options);

    /// <summary>Validates that <paramref name="value"/> is non-empty and returns it trimmed.</summary>
    /// <param name="value">The string to validate.</param>
    /// <param name="parameterName">The name of the source parameter, used in the exception message.</param>
    /// <returns>The trimmed, non-empty string.</returns>
    internal static string Required(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        return value.Trim();
    }

    /// <summary>Creates a defensive read-only copy of <paramref name="metadata"/>.</summary>
    /// <param name="metadata">The source dictionary, or <see langword="null"/> for an empty result.</param>
    /// <returns>An immutable copy of the metadata.</returns>
    internal static IReadOnlyDictionary<string, string> Copy(IReadOnlyDictionary<string, string>? metadata)
    {
        var copy = new Dictionary<string, string>(StringComparer.Ordinal);

        if (metadata is not null)
        {
            foreach (var pair in metadata)
            {
                copy.Add(Required(pair.Key, nameof(metadata)), pair.Value ?? string.Empty);
            }
        }

        return new ReadOnlyDictionary<string, string>(copy);
    }
}
