// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.OmronPlcRx.SourceGenerators;

/// <summary>Contains the source generator's syntax and tag models.</summary>
public sealed partial class PlcTagSourceGenerator
{
    /// <summary>Describes one generated PLC tag.</summary>
    private sealed class TagSpec
    {
        /// <summary>Gets or sets the source member name.</summary>
        public string MemberName { get; set; } = string.Empty;

        /// <summary>Gets or sets the bound property name.</summary>
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>Gets or sets the PLC address.</summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>Gets or sets the logical tag name.</summary>
        public string TagName { get; set; } = string.Empty;

        /// <summary>Gets or sets the declared property type.</summary>
        public string PropertyType { get; set; } = string.Empty;

        /// <summary>Gets or sets the PLC operation type.</summary>
        public string TagType { get; set; } = string.Empty;

        /// <summary>Gets or sets a value indicating whether registration is enabled.</summary>
        public bool Register { get; set; }

        /// <summary>Gets or sets a value indicating whether observation is enabled.</summary>
        public bool Observe { get; set; }

        /// <summary>Gets or sets a value indicating whether writes are enabled.</summary>
        public bool Writable { get; set; }

        /// <summary>Gets or sets a value indicating whether a property is generated.</summary>
        public bool GeneratesProperty { get; set; }

        /// <summary>Gets or sets a value indicating whether null assignment is rejected.</summary>
        public bool NeedsNullGuard { get; set; }
    }
}
