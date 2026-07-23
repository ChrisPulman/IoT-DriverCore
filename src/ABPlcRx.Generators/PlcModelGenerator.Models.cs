// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace IoT.DriverCore.ABPlcRx.SourceGenerators;

/// <summary>Contains the private source-generation models.</summary>
public sealed partial class PlcModelGenerator
{
    /// <summary>Stores one incremental generator output.</summary>
    private readonly struct GenerationResult
    {
        /// <summary>Initializes a new instance of the <see cref="GenerationResult"/> struct.</summary>
        /// <param name="hintName">The generated hint name.</param>
        /// <param name="source">The generated source.</param>
        /// <param name="diagnostic">The diagnostic to report.</param>
        private GenerationResult(string? hintName, string? source, Diagnostic? diagnostic)
        {
            HintName = hintName;
            Source = source;
            Diagnostic = diagnostic;
        }

        /// <summary>Gets the generated source hint name.</summary>
        public string? HintName { get; }

        /// <summary>Gets the generated source.</summary>
        public string? Source { get; }

        /// <summary>Gets the diagnostic to report.</summary>
        public Diagnostic? Diagnostic { get; }

        /// <summary>Creates a source generation result.</summary>
        /// <param name="hintName">The generated hint name.</param>
        /// <param name="source">The generated source.</param>
        /// <returns>The source result.</returns>
        public static GenerationResult FromSource(string hintName, string source) =>
            new(hintName, source, diagnostic: null);

        /// <summary>Creates a diagnostic generation result.</summary>
        /// <param name="diagnostic">The diagnostic to report.</param>
        /// <returns>The diagnostic result.</returns>
        public static GenerationResult FromDiagnostic(Diagnostic diagnostic) =>
            new(hintName: null, source: null, diagnostic);
    }

    /// <summary>Describes one generated PLC tag stream.</summary>
    private readonly struct TagModel
    {
        /// <summary>Initializes a new instance of the <see cref="TagModel"/> struct.</summary>
        /// <param name="propertyName">The generated property name.</param>
        /// <param name="tagName">The PLC tag name.</param>
        /// <param name="observeType">The observed value type.</param>
        /// <param name="propertyType">The generated property type.</param>
        /// <param name="registerType">The registration type.</param>
        /// <param name="settings">The tag registration settings.</param>
        /// <param name="generateProperty">Whether to generate a property.</param>
        public TagModel(
            string propertyName,
            string tagName,
            string observeType,
            string propertyType,
            string registerType,
            TagSettings settings,
            bool generateProperty)
        {
            PropertyName = SanitizeIdentifier(propertyName);
            Variable = settings.Variable;
            TagName = tagName;
            Group = settings.Group;
            ObserveType = observeType;
            PropertyType = propertyType;
            RegisterType = registerType;
            Bit = settings.Bit;
            RegisterTag = settings.RegisterTag;
            GenerateProperty = generateProperty;
        }

        /// <summary>Gets the generated property name.</summary>
        public string PropertyName { get; }

        /// <summary>Gets the PLC variable key.</summary>
        public string Variable { get; }

        /// <summary>Gets the PLC tag name.</summary>
        public string TagName { get; }

        /// <summary>Gets the PLC tag group.</summary>
        public string Group { get; }

        /// <summary>Gets the observed value type.</summary>
        public string ObserveType { get; }

        /// <summary>Gets the generated property type.</summary>
        public string PropertyType { get; }

        /// <summary>Gets the PLC registration type.</summary>
        public string RegisterType { get; }

        /// <summary>Gets the configured bit index.</summary>
        public int Bit { get; }

        /// <summary>Gets a value indicating whether the tag should be registered.</summary>
        public bool RegisterTag { get; }

        /// <summary>Gets a value indicating whether a backing property should be generated.</summary>
        public bool GenerateProperty { get; }
    }

    /// <summary>Stores optional tag settings read from attribute arguments.</summary>
    private readonly struct TagSettings
    {
        /// <summary>Initializes a new instance of the <see cref="TagSettings"/> struct.</summary>
        /// <param name="variable">The PLC variable key.</param>
        /// <param name="group">The PLC tag group.</param>
        /// <param name="bit">The configured bit index.</param>
        /// <param name="registerTag">Whether the tag should be registered.</param>
        public TagSettings(string variable, string group, int bit, bool registerTag)
        {
            Variable = variable;
            Group = group;
            Bit = bit;
            RegisterTag = registerTag;
        }

        /// <summary>Gets the PLC variable key.</summary>
        public string Variable { get; }

        /// <summary>Gets the PLC tag group.</summary>
        public string Group { get; }

        /// <summary>Gets the configured bit index.</summary>
        public int Bit { get; }

        /// <summary>Gets a value indicating whether the tag should be registered.</summary>
        public bool RegisterTag { get; }
    }
}
