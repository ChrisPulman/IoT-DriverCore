// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

#if REACTIVE_SHIM
namespace IoT.Driver.MitsubishiRx.Reactive;
#else
namespace IoT.Driver.MitsubishiRx;
#endif

/// <summary>Binds a generated property to a logical Mitsubishi tag name.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class MitsubishiTagAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="MitsubishiTagAttribute"/> class.</summary>
    /// <param name="tagName">The logical tag name.</param>
    public MitsubishiTagAttribute(string tagName)
        => TagName = tagName ?? throw new ArgumentNullException(nameof(tagName));

    /// <summary>Gets the logical tag name.</summary>
    public string TagName { get; }
}
