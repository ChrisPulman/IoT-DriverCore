// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace S7PlcRx.SourceGenerators;

/// <summary>Generates strongly typed PLC property binding hooks from S7 tag attributes.</summary>
public sealed partial class S7TagBindingSourceGenerator
{
    /// <summary>Diagnostic for a non-partial binding class.</summary>
    private static readonly DiagnosticDescriptor BindingClassMustBePartial = new(
        "S7GEN001",
        "S7 binding class must be partial",
        "Class '{0}' is marked with S7PlcBinding and must be declared partial",
        "S7PlcRx.SourceGeneration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>Diagnostic for a non-partial binding property.</summary>
    private static readonly DiagnosticDescriptor BindingPropertyMustBePartial = new(
        "S7GEN002",
        "S7 binding property must be partial",
        "Property '{0}' is marked with S7Tag and must be declared partial",
        "S7PlcRx.SourceGeneration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>Diagnostic for an empty PLC address.</summary>
    private static readonly DiagnosticDescriptor TagAddressMustNotBeEmpty = new(
        "S7GEN003",
        "S7 tag address must not be empty",
        "Property '{0}' has an empty S7 tag address",
        "S7PlcRx.SourceGeneration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>Creates actionable diagnostics for attributed classes and properties.</summary>
    /// <param name="context">The generator syntax context.</param>
    /// <param name="cancellationToken">The cancellation token for the generator operation.</param>
    /// <returns>The diagnostics associated with the syntax node.</returns>
    private static ImmutableArray<Diagnostic> GetDiagnostics(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var classSyntax = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(
                classSyntax,
                cancellationToken) is not INamedTypeSymbol classSymbol ||
            !HasAttribute(classSymbol.GetAttributes(), BindingAttributeName, ReactiveBindingAttributeName))
        {
            return [];
        }

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        AddClassDiagnostic(diagnostics, classSyntax, classSymbol);
        foreach (var propertySyntax in classSyntax.Members.OfType<PropertyDeclarationSyntax>())
        {
            AddPropertyDiagnostics(context, diagnostics, propertySyntax);
        }

        return diagnostics.ToImmutable();
    }

    /// <summary>Adds the partial-class diagnostic when needed.</summary>
    /// <param name="diagnostics">The diagnostic builder.</param>
    /// <param name="classSyntax">The class syntax.</param>
    /// <param name="classSymbol">The class symbol.</param>
    private static void AddClassDiagnostic(
        ImmutableArray<Diagnostic>.Builder diagnostics,
        ClassDeclarationSyntax classSyntax,
        INamedTypeSymbol classSymbol)
    {
        if (classSyntax.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PartialKeyword)))
        {
            return;
        }

        diagnostics.Add(Diagnostic.Create(
            BindingClassMustBePartial,
            classSyntax.Identifier.GetLocation(),
            classSymbol.Name));
    }

    /// <summary>Adds partial-property and address diagnostics when needed.</summary>
    /// <param name="context">The generator syntax context.</param>
    /// <param name="diagnostics">The diagnostic builder.</param>
    /// <param name="propertySyntax">The property syntax.</param>
    private static void AddPropertyDiagnostics(
        GeneratorSyntaxContext context,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        PropertyDeclarationSyntax propertySyntax)
    {
        if (context.SemanticModel.GetDeclaredSymbol(propertySyntax) is not IPropertySymbol propertySymbol)
        {
            return;
        }

        var attribute = FindAttribute(propertySymbol.GetAttributes(), TagAttributeName, ReactiveTagAttributeName);
        if (attribute is null)
        {
            return;
        }

        if (!HasPartialModifier(propertySyntax.Modifiers))
        {
            diagnostics.Add(Diagnostic.Create(
                BindingPropertyMustBePartial,
                propertySyntax.Identifier.GetLocation(),
                propertySymbol.Name));
        }

        AddAddressDiagnostic(diagnostics, propertySyntax, propertySymbol, attribute);
    }

    /// <summary>Adds an empty-address diagnostic when needed.</summary>
    /// <param name="diagnostics">The diagnostic builder.</param>
    /// <param name="propertySyntax">The property syntax.</param>
    /// <param name="propertySymbol">The property symbol.</param>
    /// <param name="attribute">The S7 tag attribute.</param>
    private static void AddAddressDiagnostic(
        ImmutableArray<Diagnostic>.Builder diagnostics,
        PropertyDeclarationSyntax propertySyntax,
        IPropertySymbol propertySymbol,
        AttributeData attribute)
    {
        var address = attribute.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value?.ToString()
            : null;
        if (!string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        diagnostics.Add(Diagnostic.Create(
            TagAddressMustNotBeEmpty,
            propertySyntax.Identifier.GetLocation(),
            propertySymbol.Name));
    }
}
