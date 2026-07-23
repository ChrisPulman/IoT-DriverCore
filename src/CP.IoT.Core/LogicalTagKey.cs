// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Identifies a logical tag together with its expected value type.</summary>
/// <typeparam name="T">The expected PLC value type.</typeparam>
public sealed class LogicalTagKey<T>
{
    /// <summary>Initializes a new instance of the <see cref="LogicalTagKey{T}"/> class from a logical tag name.</summary>
    /// <param name="name">The logical tag name.</param>
    public LogicalTagKey(string name) => Name = LogicalTag.Required(name, nameof(name));

    /// <summary>Initializes a new instance of the <see cref="LogicalTagKey{T}"/> class from a logical tag definition.</summary>
    /// <param name="tag">The logical tag definition.</param>
    public LogicalTagKey(LogicalTag tag)
    {
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        Name = tag.Name;
    }

    /// <summary>Gets the CLR type of the expected tag value, derived from the type parameter <typeparamref name="T"/>.</summary>
    public static Type ValueType { get; } = typeof(T);

    /// <summary>Gets the logical tag name.</summary>
    public string Name { get; }
}
