// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Tags;
#else
namespace IoT.DriverCore.OmronPlcRx.Tags;
#endif

/// <summary>Represents a typed PLC tag binding.</summary>
/// <typeparam name="T">The data type.</typeparam>
/// <param name="tagName">The tag Name.</param>
/// <param name="address">The address.</param>
/// <exception cref="ArgumentNullException">
/// <remarks>
/// Initializes a new instance of the <see cref="PlcTag{T}"/> class.
/// </remarks>
/// name
/// or
/// address.
/// </exception>
public class PlcTag<T>(string tagName, string address) : IPlcTag
{
    /// <summary>Gets the Tag Name.</summary>
    /// <value>
    /// The name.
    /// </value>
    public string TagName { get; } = tagName ?? throw new ArgumentNullException(nameof(tagName));

    /// <summary>Gets the address.</summary>
    /// <value>
    /// The address.
    /// </value>
    public string Address { get; } = address ?? throw new ArgumentNullException(nameof(address));

    /// <summary>Gets the tag value.</summary>
    /// <value>
    /// The value.
    /// </value>
    public T? Value { get; internal set; }

    /// <summary>Gets a value indicating whether this instance is bit address.</summary>
    /// <value>
    ///   <c>true</c> if this instance is bit address; otherwise, <c>false</c>.
    /// </value>
    public Type TagType => typeof(T);

    /// <summary>Gets the value.</summary>
    /// <value>
    /// The value.
    /// </value>
    object? IPlcTag.Value => Value;
}
