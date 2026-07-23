// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace IoT.DriverCore.OmronPlcRx.SourceGenerators;

/// <summary>Generates PLC tag binding members for attributed fields and properties.</summary>
[Generator]
public sealed partial class PlcTagSourceGenerator : IIncrementalGenerator
{
    /// <summary>Metadata names for supported PLC tag attributes.</summary>
    private static readonly string[] TagAttributeNames =
    [
        "IoT.DriverCore.OmronPlcRx.PlcTagAttribute",
        "IoT.DriverCore.OmronPlcRx.Reactive.PlcTagAttribute",
    ];

    /// <summary>Metadata names for supported binding-container attributes.</summary>
    private static readonly string[] BindingAttributeNames =
    [
        "IoT.DriverCore.OmronPlcRx.PlcTagBindingAttribute",
        "IoT.DriverCore.OmronPlcRx.Reactive.PlcTagBindingAttribute",
    ];

    /// <summary>Special types supported by the logical Omron adapter.</summary>
    private static readonly SpecialType[] SupportedSpecialTypes =
    [
        SpecialType.System_Boolean,
        SpecialType.System_Byte,
        SpecialType.System_Int16,
        SpecialType.System_UInt16,
        SpecialType.System_Int32,
        SpecialType.System_UInt32,
        SpecialType.System_Single,
        SpecialType.System_Double,
        SpecialType.System_String,
    ];

    /// <summary>Format used for non-nullable generated type names.</summary>
    private static readonly SymbolDisplayFormat TypeFormat =
        SymbolDisplayFormat.FullyQualifiedFormat;

    /// <summary>Format used for generated type names with nullable annotations.</summary>
    private static readonly SymbolDisplayFormat NullableTypeFormat =
        TypeFormat.WithMiscellaneousOptions(
            TypeFormat.MiscellaneousOptions
                | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>Diagnostic for a non-partial target type.</summary>
    private static readonly DiagnosticDescriptor PartialTypeRule = CreateRule(
        "OPRX001",
        "PLC tag containing type must be partial",
        "Type '{0}' must be partial to receive generated PLC reactive stream members");

    /// <summary>Diagnostic for an unsupported PLC tag type.</summary>
    private static readonly DiagnosticDescriptor UnsupportedTypeRule = CreateRule(
        "OPRX002",
        "PLC tag type is not supported",
        "Member '{0}' uses type '{1}', which is not supported by OmronPlcRx tag conversion");

    /// <summary>Diagnostic for an empty PLC address.</summary>
    private static readonly DiagnosticDescriptor EmptyAddressRule = CreateRule(
        "OPRX003",
        "PLC tag address is empty",
        "Member '{0}' must specify a non-empty PLC address");

    /// <summary>Diagnostic for a generated-member collision.</summary>
    private static readonly DiagnosticDescriptor CollisionRule = CreateRule(
        "OPRX004",
        "Generated PLC property collides with an existing member",
        "Member '{0}' would generate '{1}', but that name already exists on '{2}'");

    /// <summary>Diagnostic for a property without an assignable setter.</summary>
    private static readonly DiagnosticDescriptor SetterRule = CreateRule(
        "OPRX005",
        "PLC tag property must have a setter",
        "Property '{0}' must have a non-init setter so generated PLC bindings can update it");

    /// <summary>Diagnostic for a property outside a binding container.</summary>
    private static readonly DiagnosticDescriptor BindingRule = CreateRule(
        "OPRX006",
        "PLC tag property requires a binding container",
        "Type '{0}' must use PlcTagBindingAttribute to bind attributed properties");

    /// <inheritdoc />
    /// <param name="context">Generator initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context
            .SyntaxProvider.CreateSyntaxProvider(
                static (node, _) =>
                    node
                        is FieldDeclarationSyntax { AttributeLists.Count: > 0 }
                            or PropertyDeclarationSyntax { AttributeLists.Count: > 0 },
                static (syntaxContext, _) => syntaxContext.Node)
            .Collect();

        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(candidates),
            static (productionContext, input) => Execute(productionContext, input.Left, input.Right));
    }

    /// <summary>Produces generated tag-binding sources for one compilation.</summary>
    /// <param name="context">Generator production context.</param>
    /// <param name="compilation">Current compilation.</param>
    /// <param name="candidates">Attributed syntax candidates.</param>
    private static void Execute(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<SyntaxNode> candidates)
    {
        var tagAttributes = ResolveAttributes(compilation, TagAttributeNames);
        if (tagAttributes.Count == 0)
        {
            return;
        }

        var bindingAttributes = ResolveAttributes(compilation, BindingAttributeNames);
        var targets = new Dictionary<INamedTypeSymbol, List<TagSpec>>(
            SymbolEqualityComparer.Default);
        foreach (var declaration in candidates)
        {
            if (declaration is FieldDeclarationSyntax field)
            {
                CollectFields(context, compilation, field, tagAttributes, targets);
            }
            else if (declaration is PropertyDeclarationSyntax property)
            {
                CollectProperty(
                    context,
                    compilation,
                    property,
                    tagAttributes,
                    bindingAttributes,
                    targets);
            }
        }

        foreach (var target in targets)
        {
            context.AddSource(GetHintName(target.Key), GenerateSource(target.Key, target.Value));
        }
    }

    /// <summary>Creates one source-generation diagnostic rule.</summary>
    /// <param name="id">Diagnostic identifier.</param>
    /// <param name="title">Diagnostic title.</param>
    /// <param name="message">Diagnostic message format.</param>
    /// <returns>The configured diagnostic descriptor.</returns>
    private static DiagnosticDescriptor CreateRule(string id, string title, string message) =>
        new(id, title, message, "IoT.DriverCore.OmronPlcRx.SourceGeneration", DiagnosticSeverity.Error, true);

    /// <summary>Resolves available attributes by metadata name.</summary>
    /// <param name="compilation">Current compilation.</param>
    /// <param name="metadataNames">Attribute metadata names.</param>
    /// <returns>The resolved attribute symbols.</returns>
    private static List<INamedTypeSymbol> ResolveAttributes(
        Compilation compilation,
        string[] metadataNames)
    {
        var result = new List<INamedTypeSymbol>(metadataNames.Length);
        foreach (var name in metadataNames)
        {
            if (compilation.GetTypeByMetadataName(name) is { } symbol)
            {
                result.Add(symbol);
            }
        }

        return result;
    }

    /// <summary>Collects attributed fields from one declaration.</summary>
    /// <param name="context">Generator production context.</param>
    /// <param name="compilation">Current compilation.</param>
    /// <param name="declaration">Candidate field declaration.</param>
    /// <param name="tagAttributes">Resolved tag attributes.</param>
    /// <param name="targets">Target map to populate.</param>
    private static void CollectFields(
        SourceProductionContext context,
        Compilation compilation,
        FieldDeclarationSyntax declaration,
        IReadOnlyCollection<INamedTypeSymbol> tagAttributes,
        Dictionary<INamedTypeSymbol, List<TagSpec>> targets)
    {
        var model = compilation.GetSemanticModel(declaration.SyntaxTree);
        foreach (var variable in declaration.Declaration.Variables)
        {
            if (
                model.GetDeclaredSymbol(variable) is not IFieldSymbol field
                || GetAttribute(field, tagAttributes) is not { } attribute
            )
            {
                continue;
            }

            var propertyName = ToPropertyName(field.Name);
            var tagType = GetTagType(field.Type);
            var diagnostic = ValidateField(
                field,
                propertyName,
                tagType,
                attribute,
                variable.GetLocation());
            if (diagnostic is not null)
            {
                context.ReportDiagnostic(diagnostic);
                continue;
            }

            AddTarget(
                targets,
                field.ContainingType,
                CreateSpec(field.Name, propertyName, field.Type, tagType, attribute, true));
        }
    }

    /// <summary>Collects an attributed property declaration.</summary>
    /// <param name="context">Generator production context.</param>
    /// <param name="compilation">Current compilation.</param>
    /// <param name="declaration">Candidate property declaration.</param>
    /// <param name="tagAttributes">Resolved tag attributes.</param>
    /// <param name="bindingAttributes">Resolved binding attributes.</param>
    /// <param name="targets">Target map to populate.</param>
    private static void CollectProperty(
        SourceProductionContext context,
        Compilation compilation,
        PropertyDeclarationSyntax declaration,
        IReadOnlyCollection<INamedTypeSymbol> tagAttributes,
        IReadOnlyCollection<INamedTypeSymbol> bindingAttributes,
        Dictionary<INamedTypeSymbol, List<TagSpec>> targets)
    {
        var model = compilation.GetSemanticModel(declaration.SyntaxTree);
        if (
            model.GetDeclaredSymbol(declaration) is not IPropertySymbol property
            || GetAttribute(property, tagAttributes) is not { } attribute
        )
        {
            return;
        }

        var tagType = GetTagType(property.Type);
        var diagnostic = ValidateProperty(
            property,
            tagType,
            attribute,
            bindingAttributes,
            declaration.GetLocation());
        if (diagnostic is not null)
        {
            context.ReportDiagnostic(diagnostic);
            return;
        }

        AddTarget(
            targets,
            property.ContainingType,
            CreateSpec(property.Name, property.Name, property.Type, tagType, attribute, false));
    }

    /// <summary>Validates an attributed field.</summary>
    /// <param name="field">Attributed field.</param>
    /// <param name="propertyName">Generated property name.</param>
    /// <param name="tagType">Resolved PLC tag type.</param>
    /// <param name="attribute">PLC tag attribute.</param>
    /// <param name="location">Diagnostic source location.</param>
    /// <returns>A diagnostic when validation fails; otherwise null.</returns>
    private static Diagnostic? ValidateField(
        IFieldSymbol field,
        string propertyName,
        ITypeSymbol tagType,
        AttributeData attribute,
        Location location)
    {
        if (!IsPartial(field.ContainingType))
        {
            return Diagnostic.Create(PartialTypeRule, location, field.ContainingType.Name);
        }

        if (string.IsNullOrWhiteSpace(GetAddress(attribute)))
        {
            return Diagnostic.Create(EmptyAddressRule, location, field.Name);
        }

        if (!IsSupported(tagType))
        {
            return Diagnostic.Create(UnsupportedTypeRule, location, field.Name, tagType.Name);
        }

        return
            SyntaxFacts.IsValidIdentifier(propertyName)
            && field.ContainingType.GetMembers(propertyName).Length == 0
                ? null
                : Diagnostic.Create(
                CollisionRule,
                location,
                field.Name,
                propertyName,
                field.ContainingType.Name);
    }

    /// <summary>Validates an attributed property.</summary>
    /// <param name="property">Attributed property.</param>
    /// <param name="tagType">Resolved PLC tag type.</param>
    /// <param name="attribute">PLC tag attribute.</param>
    /// <param name="bindingAttributes">Resolved binding attributes.</param>
    /// <param name="location">Diagnostic source location.</param>
    /// <returns>A diagnostic when validation fails; otherwise null.</returns>
    private static Diagnostic? ValidateProperty(
        IPropertySymbol property,
        ITypeSymbol tagType,
        AttributeData attribute,
        IReadOnlyCollection<INamedTypeSymbol> bindingAttributes,
        Location location)
    {
        if (!IsPartial(property.ContainingType))
        {
            return Diagnostic.Create(PartialTypeRule, location, property.ContainingType.Name);
        }

        if (!HasAttribute(property.ContainingType, bindingAttributes))
        {
            return Diagnostic.Create(BindingRule, location, property.ContainingType.Name);
        }

        if (string.IsNullOrWhiteSpace(GetAddress(attribute)))
        {
            return Diagnostic.Create(EmptyAddressRule, location, property.Name);
        }

        if (!IsSupported(tagType))
        {
            return Diagnostic.Create(UnsupportedTypeRule, location, property.Name, tagType.Name);
        }

        return property.SetMethod?.IsInitOnly != false
            ? Diagnostic.Create(SetterRule, location, property.Name)
            : null;
    }

    /// <summary>Gets a matching attribute from a symbol.</summary>
    /// <param name="symbol">Symbol to inspect.</param>
    /// <param name="attributes">Resolved attribute symbols.</param>
    /// <returns>The matching attribute when present; otherwise null.</returns>
    private static AttributeData? GetAttribute(
        ISymbol symbol,
        IReadOnlyCollection<INamedTypeSymbol> attributes)
    {
        foreach (var candidate in symbol.GetAttributes())
        {
            foreach (var attribute in attributes)
            {
                if (SymbolEqualityComparer.Default.Equals(candidate.AttributeClass, attribute))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    /// <summary>Determines whether a symbol has a matching attribute.</summary>
    /// <param name="symbol">Symbol to inspect.</param>
    /// <param name="attributes">Resolved attribute symbols.</param>
    /// <returns>True when a matching attribute exists; otherwise false.</returns>
    private static bool HasAttribute(
        ISymbol symbol,
        IReadOnlyCollection<INamedTypeSymbol> attributes) => GetAttribute(symbol, attributes) is not null;

    /// <summary>Determines whether a type is declared partial.</summary>
    /// <param name="type">Type to inspect.</param>
    /// <returns>True when the type is partial; otherwise false.</returns>
    private static bool IsPartial(INamedTypeSymbol type)
    {
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            if (
                reference.GetSyntax() is TypeDeclarationSyntax declaration
                && declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
            )
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets the non-nullable PLC tag type.</summary>
    /// <param name="type">Declared field or property type.</param>
    /// <returns>The type used for PLC operations.</returns>
    private static ITypeSymbol GetTagType(ITypeSymbol type) =>
        type is INamedTypeSymbol named
        && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            ? named.TypeArguments[0]
            : type;

    /// <summary>Determines whether the PLC adapter supports a type.</summary>
    /// <param name="type">Type to inspect.</param>
    /// <returns>True when the type is supported; otherwise false.</returns>
    private static bool IsSupported(ITypeSymbol type)
    {
        return Array.IndexOf(SupportedSpecialTypes, type.SpecialType) >= 0
            || type.Name is "Bcd16" or "BcdU16" or "Bcd32" or "BcdU32";
    }

    /// <summary>Creates an immutable generation model.</summary>
    /// <param name="memberName">Source member name.</param>
    /// <param name="propertyName">Generated or existing property name.</param>
    /// <param name="propertyType">Declared property type.</param>
    /// <param name="tagType">PLC operation type.</param>
    /// <param name="attribute">PLC tag attribute.</param>
    /// <param name="generatesProperty">Whether a value property must be generated.</param>
    /// <returns>The generation model.</returns>
    private static TagSpec CreateSpec(
        string memberName,
        string propertyName,
        ITypeSymbol propertyType,
        ITypeSymbol tagType,
        AttributeData attribute,
        bool generatesProperty) =>
        new()
        {
            MemberName = memberName,
            PropertyName = propertyName,
            Address = GetAddress(attribute),
            TagName = GetNamedString(attribute, "TagName") ?? propertyName,
            PropertyType = propertyType.ToDisplayString(NullableTypeFormat),
            TagType = tagType.ToDisplayString(TypeFormat),
            Register = GetNamedBoolean(attribute, "Register", true),
            Observe = GetNamedBoolean(attribute, "Observe", true),
            Writable = GetNamedBoolean(attribute, "Writable", false),
            GeneratesProperty = generatesProperty,
            NeedsNullGuard =
                propertyType.IsReferenceType
                && propertyType.NullableAnnotation != NullableAnnotation.Annotated,
        };

    /// <summary>Adds one generation model to its containing type.</summary>
    /// <param name="targets">Target map.</param>
    /// <param name="type">Containing type.</param>
    /// <param name="spec">Tag generation model.</param>
    private static void AddTarget(
        Dictionary<INamedTypeSymbol, List<TagSpec>> targets,
        INamedTypeSymbol type,
        TagSpec spec)
    {
        if (!targets.TryGetValue(type, out var specs))
        {
            specs = [];
            targets.Add(type, specs);
        }

        specs.Add(spec);
    }

    /// <summary>Gets the PLC address from an attribute.</summary>
    /// <param name="attribute">PLC tag attribute.</param>
    /// <returns>The configured address.</returns>
    private static string GetAddress(AttributeData attribute) =>
        attribute.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value as string ?? string.Empty
            : string.Empty;

    /// <summary>Gets a named string argument.</summary>
    /// <param name="attribute">PLC tag attribute.</param>
    /// <param name="name">Argument name.</param>
    /// <returns>The configured value when present; otherwise null.</returns>
    private static string? GetNamedString(AttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == name)
            {
                return argument.Value.Value as string;
            }
        }

        return null;
    }

    /// <summary>Gets a named Boolean argument.</summary>
    /// <param name="attribute">PLC tag attribute.</param>
    /// <param name="name">Argument name.</param>
    /// <param name="defaultValue">Value used when the argument is absent.</param>
    /// <returns>The configured or default value.</returns>
    private static bool GetNamedBoolean(AttributeData attribute, string name, bool defaultValue)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == name && argument.Value.Value is bool value)
            {
                return value;
            }
        }

        return defaultValue;
    }

    /// <summary>Converts a backing-field name to a property name.</summary>
    /// <param name="fieldName">Backing-field name.</param>
    /// <returns>The generated property name.</returns>
    private static string ToPropertyName(string fieldName)
    {
        var trimmed = fieldName.TrimStart('_');
        return trimmed.Length == 0
            ? fieldName
            : $"{char.ToUpperInvariant(trimmed[0])}{trimmed.Remove(0, 1)}";
    }

    /// <summary>Gets a stable generated-source hint name.</summary>
    /// <param name="type">Generated target type.</param>
    /// <returns>The source hint name.</returns>
    private static string GetHintName(INamedTypeSymbol type) =>
        $"{type.ToDisplayString().Replace('.', '_').Replace('+', '_')}.PlcTags.g.cs";
}
