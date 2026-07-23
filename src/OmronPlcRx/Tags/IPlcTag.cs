// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Tags;
#else
namespace IoT.DriverCore.OmronPlcRx.Tags;
#endif

/// <summary>Defines metadata and value access for a PLC tag.</summary>
public interface IPlcTag
{
    /// <summary>Gets the address.</summary>
    /// <value>
    /// The address.
    /// </value>
    string Address { get; }

    /// <summary>Gets a value indicating whether this instance is bit address.</summary>
    /// <value>
    ///   <c>true</c> if this instance is bit address; otherwise, <c>false</c>.
    /// </value>
    Type TagType { get; }

    /// <summary>Gets the name of the tag.</summary>
    /// <value>
    /// The name of the tag.
    /// </value>
    string TagName { get; }

    /// <summary>Gets the value.</summary>
    /// <value>
    /// The value.
    /// </value>
    object? Value { get; }
}
