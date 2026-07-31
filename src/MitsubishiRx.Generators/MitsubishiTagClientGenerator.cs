// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace IoT.Driver.MitsubishiRx;

/// <summary>Generates strongly typed Mitsubishi tag clients from embedded tag schema JSON.</summary>
[Generator]
public sealed partial class MitsubishiTagClientGenerator : IIncrementalGenerator
{
    /// <summary>Namespace of the standard Mitsubishi runtime marker attributes.</summary>
    private const string RuntimeNamespace = "IoT.Driver.MitsubishiRx";

    /// <summary>Namespace of the Reactive Mitsubishi runtime marker attributes.</summary>
    private const string ReactiveRuntimeNamespace = "IoT.Driver.MitsubishiRx.Reactive";

    /// <summary>Diagnostic reported when generation fails unexpectedly.</summary>
    private static readonly DiagnosticDescriptor GenerationFailureDiagnostic = new(
        id: "MRTXGEN001",
        title: "Failed to generate Mitsubishi tag client",
        messageFormat: "{0}",
        category: "MitsubishiRx.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>Diagnostic reported when tag names are duplicated.</summary>
    private static readonly DiagnosticDescriptor DuplicateTagDiagnostic = new(
        id: "MRTXGEN002",
        title: "Duplicate generated tag name",
        messageFormat: "Schema contains duplicate tag name '{0}'",
        category: "MitsubishiRx.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>Diagnostic reported when a group references a missing tag.</summary>
    private static readonly DiagnosticDescriptor UnknownGroupTagDiagnostic = new(
        id: "MRTXGEN003",
        title: "Unknown generated group tag reference",
        messageFormat: "Group '{0}' references unknown tag '{1}'",
        category: "MitsubishiRx.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>Diagnostic reported when a tag has an unsupported data type.</summary>
    private static readonly DiagnosticDescriptor UnsupportedDataTypeDiagnostic = new(
        id: "MRTXGEN004",
        title: "Unsupported generated tag data type",
        messageFormat: "Tag '{0}' uses unsupported data type '{1}'",
        category: "MitsubishiRx.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>Diagnostic reported when different schema names sanitize to the same identifier.</summary>
    private static readonly DiagnosticDescriptor SanitizedIdentifierCollisionDiagnostic = new(
        id: "MRTXGEN005",
        title: "Generated identifier collision",
        messageFormat: "Generated identifier '{0}' is produced by multiple {1}: {2}",
        category: "MitsubishiRx.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>Diagnostic reported when a tag has no name.</summary>
    private static readonly DiagnosticDescriptor EmptyTagNameDiagnostic = new(
        id: "MRTXGEN006",
        title: "Empty generated tag name",
        messageFormat: "Tag name must not be empty",
        category: "MitsubishiRx.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>Diagnostic reported when a group has no name.</summary>
    private static readonly DiagnosticDescriptor EmptyGroupNameDiagnostic = new(
        id: "MRTXGEN007",
        title: "Empty generated group name",
        messageFormat: "Group name must not be empty",
        category: "MitsubishiRx.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>Diagnostic reported when a group has no tag members.</summary>
    private static readonly DiagnosticDescriptor EmptyGroupMembershipDiagnostic = new(
        id: "MRTXGEN008",
        title: "Empty generated group membership",
        messageFormat: "Group '{0}' must reference at least one tag",
        category: "MitsubishiRx.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>Diagnostic reported when group names are duplicated.</summary>
    private static readonly DiagnosticDescriptor DuplicateGroupDiagnostic = new(
        id: "MRTXGEN009",
        title: "Duplicate generated group name",
        messageFormat: "Schema contains duplicate group name '{0}'",
        category: "MitsubishiRx.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>Diagnostic reported when a group contains an empty tag reference.</summary>
    private static readonly DiagnosticDescriptor EmptyGroupTagReferenceDiagnostic = new(
        id: "MRTXGEN010",
        title: "Empty generated group tag reference",
        messageFormat: "Group '{0}' contains an empty tag reference",
        category: "MitsubishiRx.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>Diagnostic reported when a group references the same tag more than once.</summary>
    private static readonly DiagnosticDescriptor DuplicateGroupTagReferenceDiagnostic = new(
        id: "MRTXGEN011",
        title: "Duplicate generated group tag reference",
        messageFormat: "Group '{0}' references tag '{1}' more than once",
        category: "MitsubishiRx.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>Supported schema data types.</summary>
    private static readonly HashSet<string> SupportedDataTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Bit",
        "Word",
        "DWord",
        "Float",
        "String",
        "Int16",
        "UInt16",
        "Int32",
        "UInt32",
    };

    /// <inheritdoc/>
    public void Initialize(
        IncrementalGeneratorInitializationContext context)
    {
        RegisterSchemaGeneration(in context);
        RegisterPropertyBindingGeneration(in context);
    }

    /// <summary>Registers the schema-to-client incremental generation pipeline.</summary>
    /// <param name="context">Generator initialization context.</param>
    private static void RegisterSchemaGeneration(in IncrementalGeneratorInitializationContext context)
    {
        var schemaValues = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is AttributeSyntax attribute && MightBeSchemaAttribute(attribute),
                transform: static (syntaxContext, _) => ExtractSchemaLiteral(in syntaxContext))
            .Where(static schema => schema is not null)
            .Collect();
        context.RegisterSourceOutput(schemaValues, static (productionContext, schemas) =>
        {
            if (schemas.IsDefaultOrEmpty)
            {
                return;
            }

            try
            {
                var schema = schemas[0]!;
                var model = SchemaModel.Parse(schema.Json, schema.NamespaceName);
                if (!ValidateModel(model, in productionContext))
                {
                    return;
                }

                var source = MitsubishiTagClientEmitter.Emit(model);
                productionContext.AddSource("MitsubishiTagClient.g.cs", SourceText.From(source, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                productionContext.ReportDiagnostic(Diagnostic.Create(
                    GenerationFailureDiagnostic,
                Location.None,
                ex.Message));
            }
        });
    }

    /// <summary>Registers the property-binding incremental generation pipeline.</summary>
    /// <param name="context">Generator initialization context.</param>
    private static void RegisterPropertyBindingGeneration(in IncrementalGeneratorInitializationContext context)
    {
        var standardPropertyBindings = context.SyntaxProvider.ForAttributeWithMetadataName(
                $"{RuntimeNamespace}.MitsubishiTagAttribute",
                predicate: static (node, _) => node is PropertyDeclarationSyntax,
                transform: static (syntaxContext, _) => ExtractPropertyBinding(in syntaxContext))
            .Collect();
        var reactivePropertyBindings = context.SyntaxProvider.ForAttributeWithMetadataName(
                $"{ReactiveRuntimeNamespace}.MitsubishiTagAttribute",
                predicate: static (node, _) => node is PropertyDeclarationSyntax,
                transform: static (syntaxContext, _) => ExtractPropertyBinding(in syntaxContext))
            .Collect();

        context.RegisterSourceOutput(
            standardPropertyBindings.Combine(reactivePropertyBindings),
            static (productionContext, bindings) =>
        {
            if (bindings.Left.IsDefaultOrEmpty && bindings.Right.IsDefaultOrEmpty)
            {
                return;
            }

            var nonNullBindings = new List<PropertyBindingModel>(bindings.Left.Length + bindings.Right.Length);
            AddPropertyBindings(bindings.Left, nonNullBindings);
            AddPropertyBindings(bindings.Right, nonNullBindings);
            if (nonNullBindings.Count == 0)
            {
                return;
            }

            var source = MitsubishiTagClientEmitter.EmitPropertyBindings(nonNullBindings);
            productionContext.AddSource("MitsubishiTagBindings.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    /// <summary>Adds non-null property bindings from an incremental collection.</summary>
    /// <param name="bindings">Property bindings produced by the incremental pipeline.</param>
    /// <param name="destination">Destination collection for non-null bindings.</param>
    private static void AddPropertyBindings(
        ImmutableArray<PropertyBindingModel?> bindings,
        List<PropertyBindingModel> destination)
    {
        foreach (var binding in bindings)
        {
            if (binding is not null)
            {
                destination.Add(binding);
            }
        }
    }

    /// <summary>Extracts one property-level logical-tag binding.</summary>
    /// <param name="context">The generator attribute context.</param>
    /// <returns>The binding model.</returns>
    private static PropertyBindingModel? ExtractPropertyBinding(in GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not IPropertySymbol property || context.Attributes.IsEmpty)
        {
            return null;
        }

        var attribute = context.Attributes[0];
        var tagName = !attribute.ConstructorArguments.IsEmpty
            ? attribute.ConstructorArguments[0].Value as string
            : null;
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return null;
        }

        var clientMemberName = GetClientMemberName(property.ContainingType);

        return new(
            property.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : property.ContainingNamespace.ToDisplayString(),
            property.ContainingType.Name,
            property.Name,
            property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            tagName!,
            clientMemberName);
    }

    /// <summary>Gets the configured generated client member name.</summary>
    /// <param name="containingType">The property containing type.</param>
    /// <returns>The configured member name, or the default logical-tag member name.</returns>
    private static string GetClientMemberName(INamedTypeSymbol containingType)
    {
        foreach (var candidate in containingType.GetAttributes())
        {
            if (!IsTagClientAttribute(candidate.AttributeClass) ||
                candidate.ConstructorArguments.IsEmpty ||
                candidate.ConstructorArguments[0].Value is not string configuredMemberName ||
                string.IsNullOrWhiteSpace(configuredMemberName))
            {
                continue;
            }

            return configuredMemberName;
        }

        return "LogicalTags";
    }

    /// <summary>Validates a parsed schema model.</summary>
    /// <param name="model">Parsed schema model.</param>
    /// <param name="context">Source production context.</param>
    /// <returns><c>true</c> when the model is valid.</returns>
    private static bool ValidateModel(SchemaModel model, in SourceProductionContext context)
    {
        var isValid = true;
        var sanitizedTagNames = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var sanitizedGroupNames = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        isValid &= ValidateTags(model.Tags, in context, sanitizedTagNames);

        var knownTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in model.Tags)
        {
            if (!string.IsNullOrWhiteSpace(tag.Name))
            {
                _ = knownTags.Add(tag.Name);
            }
        }

        isValid &= ValidateGroups(model.Groups, knownTags, in context, sanitizedGroupNames);

        ReportSanitizedCollisions(in context, sanitizedTagNames, "tag names", ref isValid);
        ReportSanitizedCollisions(in context, sanitizedGroupNames, "group names", ref isValid);

        return isValid;
    }

    /// <summary>Validates tag entries.</summary>
    /// <param name="tags">Tag entries.</param>
    /// <param name="context">Source production context.</param>
    /// <param name="sanitizedTagNames">Sanitized tag name index.</param>
    /// <returns><c>true</c> when all tags are valid.</returns>
    private static bool ValidateTags(
        IReadOnlyList<TagModel> tags,
        in SourceProductionContext context,
        Dictionary<string, List<string>> sanitizedTagNames)
    {
        var isValid = true;
        var seenTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in tags)
        {
            isValid &= ValidateTag(tag, in context, seenTags, sanitizedTagNames);
        }

        return isValid;
    }

    /// <summary>Validates one tag entry.</summary>
    /// <param name="tag">Tag entry.</param>
    /// <param name="context">Source production context.</param>
    /// <param name="seenTags">Observed tag names.</param>
    /// <param name="sanitizedTagNames">Sanitized tag name index.</param>
    /// <returns><c>true</c> when the tag is valid.</returns>
    private static bool ValidateTag(
        TagModel tag,
        in SourceProductionContext context,
        HashSet<string> seenTags,
        Dictionary<string, List<string>> sanitizedTagNames)
    {
        if (string.IsNullOrWhiteSpace(tag.Name))
        {
            context.ReportDiagnostic(Diagnostic.Create(EmptyTagNameDiagnostic, Location.None));
            return false;
        }

        var isValid = true;
        if (!seenTags.Add(tag.Name))
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateTagDiagnostic, Location.None, tag.Name));
            isValid = false;
        }

        if (!string.IsNullOrWhiteSpace(tag.DataType) && !SupportedDataTypes.Contains(tag.DataType!))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(UnsupportedDataTypeDiagnostic, Location.None, tag.Name, tag.DataType));
            isValid = false;
        }

        AddSanitizedName(sanitizedTagNames, MitsubishiTagClientEmitter.SanitizeIdentifier(tag.Name), tag.Name);
        return isValid;
    }

    /// <summary>Validates group entries.</summary>
    /// <param name="groups">Group entries.</param>
    /// <param name="knownTags">Known tag names.</param>
    /// <param name="context">Source production context.</param>
    /// <param name="sanitizedGroupNames">Sanitized group name index.</param>
    /// <returns><c>true</c> when all groups are valid.</returns>
    private static bool ValidateGroups(
        IReadOnlyList<GroupModel> groups,
        HashSet<string> knownTags,
        in SourceProductionContext context,
        Dictionary<string, List<string>> sanitizedGroupNames)
    {
        var isValid = true;
        var seenGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            isValid &= ValidateGroup(group, knownTags, in context, seenGroups, sanitizedGroupNames);
        }

        return isValid;
    }

    /// <summary>Validates one group entry.</summary>
    /// <param name="group">Group entry.</param>
    /// <param name="knownTags">Known tag names.</param>
    /// <param name="context">Source production context.</param>
    /// <param name="seenGroups">Observed group names.</param>
    /// <param name="sanitizedGroupNames">Sanitized group name index.</param>
    /// <returns><c>true</c> when the group is valid.</returns>
    private static bool ValidateGroup(
        GroupModel group,
        HashSet<string> knownTags,
        in SourceProductionContext context,
        HashSet<string> seenGroups,
        Dictionary<string, List<string>> sanitizedGroupNames)
    {
        var isValid = ValidateGroupName(group, in context, seenGroups, sanitizedGroupNames);

        if (group.TagNames.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(EmptyGroupMembershipDiagnostic, Location.None, group.Name));
            isValid = false;
        }

        isValid &= ValidateGroupTagReferences(group, knownTags, in context);
        return isValid;
    }

    /// <summary>Validates one group name.</summary>
    /// <param name="group">Group entry.</param>
    /// <param name="context">Source production context.</param>
    /// <param name="seenGroups">Observed group names.</param>
    /// <param name="sanitizedGroupNames">Sanitized group name index.</param>
    /// <returns><c>true</c> when the group name is valid.</returns>
    private static bool ValidateGroupName(
        GroupModel group,
        in SourceProductionContext context,
        HashSet<string> seenGroups,
        Dictionary<string, List<string>> sanitizedGroupNames)
    {
        if (string.IsNullOrWhiteSpace(group.Name))
        {
            context.ReportDiagnostic(Diagnostic.Create(EmptyGroupNameDiagnostic, Location.None));
            return false;
        }

        var isValid = true;
        if (!seenGroups.Add(group.Name))
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateGroupDiagnostic, Location.None, group.Name));
            isValid = false;
        }

        AddSanitizedName(sanitizedGroupNames, MitsubishiTagClientEmitter.SanitizeIdentifier(group.Name), group.Name);
        return isValid;
    }

    /// <summary>Validates all tag references for one group.</summary>
    /// <param name="group">Group entry.</param>
    /// <param name="knownTags">Known tag names.</param>
    /// <param name="context">Source production context.</param>
    /// <returns><c>true</c> when all references are valid.</returns>
    private static bool ValidateGroupTagReferences(
        GroupModel group,
        HashSet<string> knownTags,
        in SourceProductionContext context)
    {
        var isValid = true;
        var seenGroupTagNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tagName in group.TagNames)
        {
            isValid &= ValidateGroupTagReference(group.Name, tagName, knownTags, seenGroupTagNames, in context);
        }

        return isValid;
    }

    /// <summary>Validates one group tag reference.</summary>
    /// <param name="groupName">Group name.</param>
    /// <param name="tagName">Referenced tag name.</param>
    /// <param name="knownTags">Known tag names.</param>
    /// <param name="seenGroupTagNames">Observed tag references for this group.</param>
    /// <param name="context">Source production context.</param>
    /// <returns><c>true</c> when the reference is valid.</returns>
    private static bool ValidateGroupTagReference(
        string groupName,
        string tagName,
        HashSet<string> knownTags,
        HashSet<string> seenGroupTagNames,
        in SourceProductionContext context)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            context.ReportDiagnostic(Diagnostic.Create(EmptyGroupTagReferenceDiagnostic, Location.None, groupName));
            return false;
        }

        if (!seenGroupTagNames.Add(tagName))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(DuplicateGroupTagReferenceDiagnostic, Location.None, groupName, tagName));
            return false;
        }

        if (knownTags.Contains(tagName))
        {
            return true;
        }

        context.ReportDiagnostic(Diagnostic.Create(UnknownGroupTagDiagnostic, Location.None, groupName, tagName));
        return false;
    }

    /// <summary>Adds an original name to the sanitized identifier index.</summary>
    /// <param name="index">Sanitized identifier index.</param>
    /// <param name="sanitizedName">Sanitized identifier.</param>
    /// <param name="originalName">Original schema name.</param>
    private static void AddSanitizedName(
        Dictionary<string, List<string>> index,
        string sanitizedName,
        string originalName)
    {
        if (!index.TryGetValue(sanitizedName, out var originals))
        {
            originals = [];
            index[sanitizedName] = originals;
        }

        originals.Add(originalName);
    }

    /// <summary>Reports generated identifier collisions.</summary>
    /// <param name="context">Source production context.</param>
    /// <param name="index">Sanitized identifier index.</param>
    /// <param name="entityKind">Entity kind for diagnostics.</param>
    /// <param name="isValid">Current validation result.</param>
    private static void ReportSanitizedCollisions(
        in SourceProductionContext context,
        Dictionary<string, List<string>> index,
        string entityKind,
        ref bool isValid)
    {
        foreach (var pair in index)
        {
            var distinctOriginals = new List<string>(pair.Value.Count);
            var seenOriginals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var original in pair.Value)
            {
                if (seenOriginals.Add(original))
                {
                    distinctOriginals.Add(original);
                }
            }

            if (distinctOriginals.Count <= 1)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                SanitizedIdentifierCollisionDiagnostic,
                Location.None,
                pair.Key,
                entityKind,
                string.Join(", ", distinctOriginals)));
            isValid = false;
        }
    }

    /// <summary>Returns whether the attribute syntax may be the generator schema attribute.</summary>
    /// <param name="attribute">Attribute syntax.</param>
    /// <returns><c>true</c> when the attribute may be the schema attribute.</returns>
    private static bool MightBeSchemaAttribute(AttributeSyntax attribute)
    {
        var name = attribute.Name.ToString();
        return name.Contains("MitsubishiTagClientSchema", StringComparison.Ordinal);
    }

    /// <summary>Extracts the schema JSON literal from a schema attribute.</summary>
    /// <param name="syntaxContext">Generator syntax context.</param>
    /// <returns>Schema JSON when one can be read.</returns>
    private static SchemaInput? ExtractSchemaLiteral(in GeneratorSyntaxContext syntaxContext)
    {
        if (syntaxContext.Node is not AttributeSyntax attribute ||
            attribute.ArgumentList is null ||
            attribute.ArgumentList.Arguments.Count == 0 ||
            !TryGetSchemaNamespace(attribute, syntaxContext.SemanticModel, out var namespaceName))
        {
            return null;
        }

        var expression = attribute.ArgumentList.Arguments[0].Expression;
        var constant = syntaxContext.SemanticModel.GetConstantValue(expression);
        if (constant.HasValue && constant.Value is string text)
        {
            return new(text, namespaceName);
        }

        return expression is LiteralExpressionSyntax literal
            ? new(literal.Token.ValueText, namespaceName)
            : null;
    }

    /// <summary>Returns whether an attribute is a supported Mitsubishi client marker.</summary>
    /// <param name="attributeClass">Attribute type to inspect.</param>
    /// <returns><c>true</c> when the attribute is a supported client marker.</returns>
    private static bool IsTagClientAttribute(INamedTypeSymbol? attributeClass)
        => IsAttribute(attributeClass, "MitsubishiTagClientAttribute");

    /// <summary>Gets the owning runtime namespace from a schema attribute.</summary>
    /// <param name="attribute">Schema attribute syntax.</param>
    /// <param name="semanticModel">Semantic model for the source file.</param>
    /// <param name="namespaceName">The owning runtime namespace when successful.</param>
    /// <returns><c>true</c> when the attribute is a supported schema marker.</returns>
    private static bool TryGetSchemaNamespace(
        AttributeSyntax attribute,
        SemanticModel semanticModel,
        out string namespaceName)
    {
        var symbol = semanticModel.GetSymbolInfo(attribute).Symbol as IMethodSymbol;
        var attributeClass = symbol?.ContainingType;
        if (IsAttribute(attributeClass, "MitsubishiTagClientSchemaAttribute"))
        {
            namespaceName = attributeClass!.ContainingNamespace.ToDisplayString();
            return true;
        }

        namespaceName = string.Empty;
        return false;
    }

    /// <summary>Returns whether an attribute type belongs to either Mitsubishi runtime surface.</summary>
    /// <param name="attributeClass">Attribute type to inspect.</param>
    /// <param name="attributeName">Unqualified marker attribute name.</param>
    /// <returns><c>true</c> when the attribute belongs to a supported runtime surface.</returns>
    private static bool IsAttribute(INamedTypeSymbol? attributeClass, string attributeName)
    {
        var name = attributeClass?.ToDisplayString();
        return string.Equals(name, $"{RuntimeNamespace}.{attributeName}", StringComparison.Ordinal) ||
            string.Equals(name, $"{ReactiveRuntimeNamespace}.{attributeName}", StringComparison.Ordinal);
    }

    /// <summary>Represents a schema and the runtime namespace that owns its marker attribute.</summary>
    private sealed class SchemaInput
    {
        /// <summary>Initializes a new instance of the <see cref="SchemaInput"/> class.</summary>
        /// <param name="json">Schema JSON.</param>
        /// <param name="namespaceName">Runtime namespace that owns the marker attribute.</param>
        internal SchemaInput(string json, string namespaceName) => (Json, NamespaceName) = (json, namespaceName);

        /// <summary>Gets the schema JSON.</summary>
        internal string Json { get; }

        /// <summary>Gets the runtime namespace that owns the marker attribute.</summary>
        internal string NamespaceName { get; }
    }
}
