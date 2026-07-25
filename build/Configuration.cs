// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using Nuke.Common.Tooling;

namespace IoT.DriverCore.Building;

/// <summary>Identifies a supported build configuration.</summary>
[TypeConverter(typeof(TypeConverter<Configuration>))]
public sealed class Configuration : Enumeration
{
    /// <summary>Gets the debug configuration.</summary>
    public static readonly Configuration Debug = new() { Value = nameof(Debug) };

    /// <summary>Gets the release configuration.</summary>
    public static readonly Configuration Release = new() { Value = nameof(Release) };

    /// <summary>Converts a configuration to its command-line value.</summary>
    /// <param name="configuration">The build configuration.</param>
    public static implicit operator string(Configuration configuration) => configuration.Value;

    /// <inheritdoc />
    public override string ToString() => Value;
}
