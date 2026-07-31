// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

#if REACTIVE_SHIM
namespace IoT.Driver.MitsubishiRx.Reactive;
#else
namespace IoT.Driver.MitsubishiRx;
#endif

/// <summary>Declares an inline Mitsubishi tag schema for source generation.</summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class MitsubishiTagClientSchemaAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="MitsubishiTagClientSchemaAttribute"/> class.</summary>
    /// <param name="schemaJson">The JSON tag schema.</param>
    public MitsubishiTagClientSchemaAttribute(string schemaJson)
        => SchemaJson = schemaJson ?? throw new ArgumentNullException(nameof(schemaJson));

    /// <summary>Gets the JSON tag schema.</summary>
    public string SchemaJson { get; }
}
