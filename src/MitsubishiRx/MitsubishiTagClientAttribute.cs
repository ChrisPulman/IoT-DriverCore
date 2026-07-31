// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

#if REACTIVE_SHIM
namespace IoT.Driver.MitsubishiRx.Reactive;
#else
namespace IoT.Driver.MitsubishiRx;
#endif

/// <summary>Binds generated tag members to a Mitsubishi logical-tag client member.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MitsubishiTagClientAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="MitsubishiTagClientAttribute"/> class.</summary>
    public MitsubishiTagClientAttribute()
        : this("LogicalTags")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MitsubishiTagClientAttribute"/> class.</summary>
    /// <param name="clientMemberName">The client field or property name.</param>
    public MitsubishiTagClientAttribute(string clientMemberName)
        => ClientMemberName = clientMemberName ?? throw new ArgumentNullException(nameof(clientMemberName));

    /// <summary>Gets the client field or property name.</summary>
    public string ClientMemberName { get; }
}
