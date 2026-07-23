// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Serial.SourceGenerators;

/// <summary>Generates reactive serial-port properties and observable streams.</summary>
public sealed partial class SerialPortReactiveStreamGenerator
{
    /// <summary>Appends the storage and property for a generated stream value.</summary>
    /// <param name="builder">The generated source builder.</param>
    /// <param name="propertyName">The generated property name.</param>
    /// <param name="typeName">The fully qualified property type name.</param>
    /// <param name="subjectFieldName">The generated subject field name.</param>
    private static void AppendGeneratedValueProperty(
        StringBuilder builder,
        string propertyName,
        string typeName,
        string subjectFieldName)
    {
        var valueFieldName = $"{subjectFieldName}Value";

        _ = builder.AppendLine();
        _ = builder.Append("    private global::System.Tuple<")
            .Append(typeName)
            .Append(">? ")
            .Append(valueFieldName)
            .AppendLine(";");
        _ = builder.AppendLine();
        _ = builder.Append("    public ")
            .Append(typeName)
            .Append(' ')
            .AppendLine(propertyName)
            .AppendLine("    {")
            .Append("        get => ")
            .Append(valueFieldName)
            .AppendLine(" is { } holder")
            .AppendLine("            ? holder.Item1")
            .Append("            : throw new global::System.InvalidOperationException(")
            .AppendLine("\"The generated serial value has not been observed.\");")
            .Append("        private set => ")
            .Append(valueFieldName)
            .AppendLine(" = global::System.Tuple.Create(value);")
            .AppendLine("    }");
    }

    /// <summary>Identifies the generated member and its target declaration.</summary>
    private readonly struct StreamIdentity
    {
        /// <summary>Initializes a new instance of the <see cref="StreamIdentity"/> struct.</summary>
        /// <param name="targetType">The target class symbol.</param>
        /// <param name="propertyName">The generated property name.</param>
        /// <param name="propertyType">The generated property type.</param>
        /// <param name="location">The attribute location used for diagnostics.</param>
        public StreamIdentity(
            INamedTypeSymbol targetType,
            string propertyName,
            ITypeSymbol? propertyType,
            Location? location)
        {
            TargetType = targetType;
            PropertyName = propertyName;
            PropertyType = propertyType;
            Location = location;
        }

        /// <summary>Gets the target class symbol.</summary>
        public INamedTypeSymbol TargetType { get; }

        /// <summary>Gets the generated property name.</summary>
        public string PropertyName { get; }

        /// <summary>Gets the generated property type.</summary>
        public ITypeSymbol? PropertyType { get; }

        /// <summary>Gets the attribute location used for diagnostics.</summary>
        public Location? Location { get; }
    }

    /// <summary>Describes value matching and the selected observable source.</summary>
    private readonly struct StreamMatchOptions
    {
        /// <summary>Initializes a new instance of the <see cref="StreamMatchOptions"/> struct.</summary>
        /// <param name="pattern">The optional regular expression pattern.</param>
        /// <param name="sourceExpression">The generated observable source expression.</param>
        /// <param name="groupName">The named regular expression group.</param>
        /// <param name="groupNumber">The fallback regular expression group number.</param>
        /// <param name="ignoreCase">Whether matching should ignore case.</param>
        public StreamMatchOptions(
            string? pattern,
            string sourceExpression,
            string? groupName,
            int groupNumber,
            bool ignoreCase)
        {
            Pattern = pattern;
            SourceExpression = sourceExpression;
            GroupName = groupName;
            GroupNumber = groupNumber;
            IgnoreCase = ignoreCase;
        }

        /// <summary>Gets the optional regular expression pattern.</summary>
        public string? Pattern { get; }

        /// <summary>Gets the generated observable source expression.</summary>
        public string SourceExpression { get; }

        /// <summary>Gets the named regular expression group.</summary>
        public string? GroupName { get; }

        /// <summary>Gets the fallback regular expression group number.</summary>
        public int GroupNumber { get; }

        /// <summary>Gets a value indicating whether matching should ignore case.</summary>
        public bool IgnoreCase { get; }
    }

    /// <summary>Describes one generated serial stream.</summary>
    /// <param name="identity">The target member identity.</param>
    /// <param name="matchOptions">The stream matching options.</param>
    private sealed class StreamInfo(StreamIdentity identity, StreamMatchOptions matchOptions)
    {
        /// <summary>Gets the target class symbol.</summary>
        public INamedTypeSymbol TargetType => identity.TargetType;

        /// <summary>Gets the generated property name.</summary>
        public string PropertyName => identity.PropertyName;

        /// <summary>Gets the generated property type.</summary>
        public ITypeSymbol? PropertyType => identity.PropertyType;

        /// <summary>Gets the optional regular expression pattern.</summary>
        public string? Pattern => matchOptions.Pattern;

        /// <summary>Gets the generated observable source expression.</summary>
        public string SourceExpression => matchOptions.SourceExpression;

        /// <summary>Gets the named regular expression group.</summary>
        public string? GroupName => matchOptions.GroupName;

        /// <summary>Gets the fallback regular expression group number.</summary>
        public int GroupNumber => matchOptions.GroupNumber;

        /// <summary>Gets a value indicating whether matching should ignore case.</summary>
        public bool IgnoreCase => matchOptions.IgnoreCase;

        /// <summary>Gets the attribute location used for diagnostics.</summary>
        public Location? Location => identity.Location;
    }
}
