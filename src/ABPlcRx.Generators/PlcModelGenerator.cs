// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ABPlcRx.SourceGenerators;

/// <summary>Generates reactive PLC stream models from ABPlcRx source generation attributes.</summary>
[Generator]
public sealed partial class PlcModelGenerator : IIncrementalGenerator
{
    /// <summary>The PLC model attribute type name.</summary>
    private const string PlcModelAttributeName = "PlcModelAttribute";

    /// <summary>The PLC tag attribute type name.</summary>
    private const string PlcTagAttributeName = "PlcTagAttribute";

    /// <summary>The source-generation namespace suffix appended to the runtime API namespace.</summary>
    private const string SourceGenerationNamespaceSuffix = ".SourceGeneration";

    /// <summary>The default runtime API namespace.</summary>
    private const string DefaultApiNamespace = "ABPlcRx";

    /// <summary>The System.Reactive-flavoured runtime API namespace.</summary>
    private const string ReactiveApiNamespace = "ABPlcRx.Reactive";

    /// <summary>The number of constructor arguments expected by class-level PLC tag attributes.</summary>
    private const int ClassTagConstructorArgumentCount = 3;

    /// <summary>The value-type argument index in class-level PLC tag attributes.</summary>
    private const int ClassTagValueTypeArgumentIndex = 0;

    /// <summary>The property-name argument index in class-level PLC tag attributes.</summary>
    private const int ClassTagPropertyNameArgumentIndex = 1;

    /// <summary>The tag-name argument index in class-level PLC tag attributes.</summary>
    private const int ClassTagNameArgumentIndex = 2;

    /// <summary>Generated indentation for a class-level opening brace.</summary>
    private const string ClassOpenBrace = "    {";

    /// <summary>Generated indentation for a class-level closing brace.</summary>
    private const string ClassCloseBrace = "    }";

    /// <summary>Generated indentation for a method-level opening brace.</summary>
    private const string MethodOpenBrace = "        {";

    /// <summary>Generated indentation for a method-level closing brace.</summary>
    private const string MethodCloseBrace = "        }";

    /// <summary>The diagnostic descriptor used when a model type is not partial.</summary>
    private static readonly DiagnosticDescriptor PartialRequiredDescriptor = new(
        "ABPLCRXSG001",
        "PLC stream model must be partial",
        "Type '{0}' must be partial to generate ABPlcRx stream members",
        "ABPlcRx.SourceGenerators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var generationResults = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => IsCandidate(node),
                CreateGenerationResult)
            .Where(static result => result is not null)
            .Select(static (result, _) => result.GetValueOrDefault());

        context.RegisterSourceOutput(generationResults, EmitGenerationResult);
    }

    /// <summary>Checks whether a syntax node can contain PLC source-generation attributes.</summary>
    /// <param name="node">The syntax node.</param>
    /// <returns>True when the node should receive semantic inspection.</returns>
    private static bool IsCandidate(SyntaxNode node) =>
        node is TypeDeclarationSyntax { AttributeLists.Count: > 0 };

    /// <summary>Creates a generated source result or diagnostic from one candidate declaration.</summary>
    /// <param name="context">The generator syntax context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The generation result, or null when the declaration is not an ABPlcRx model.</returns>
    private static GenerationResult? CreateGenerationResult(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var declaration = (TypeDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(declaration, cancellationToken) is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        var apiNamespace = GetApiNamespace(typeSymbol.GetAttributes(), PlcModelAttributeName);
        var tags = CollectTags(typeSymbol, ref apiNamespace);
        if (tags.Length == 0 && apiNamespace is null)
        {
            return null;
        }

        if (!IsPartial(declaration))
        {
            return GenerationResult.FromDiagnostic(CreatePartialRequiredDiagnostic(declaration, typeSymbol));
        }

        if (tags.Length == 0)
        {
            return null;
        }

        var hintName = $"{GetSafeHintName(typeSymbol)}.ABPlcRx.g.cs";
        return GenerationResult.FromSource(
            hintName,
            GenerateModel(typeSymbol, tags, apiNamespace ?? DefaultApiNamespace));
    }

    /// <summary>Emits generated source or reports a candidate diagnostic.</summary>
    /// <param name="context">The source production context.</param>
    /// <param name="result">The generation result.</param>
    private static void EmitGenerationResult(SourceProductionContext context, GenerationResult result)
    {
        if (result.Diagnostic is { } diagnostic)
        {
            context.ReportDiagnostic(diagnostic);
            return;
        }

        context.AddSource(result.HintName!, SourceText.From(result.Source!, Encoding.UTF8));
    }

    /// <summary>Collects PLC tag metadata from class and property attributes.</summary>
    /// <param name="typeSymbol">The candidate type symbol.</param>
    /// <param name="apiNamespace">The runtime API namespace resolved from source-generation attributes.</param>
    /// <returns>The collected tag models.</returns>
    private static ImmutableArray<TagModel> CollectTags(INamedTypeSymbol typeSymbol, ref string? apiNamespace)
    {
        var builder = ImmutableArray.CreateBuilder<TagModel>();

        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (!TryGetApiNamespace(attribute, PlcTagAttributeName, out var tagApiNamespace))
            {
                continue;
            }

            apiNamespace ??= tagApiNamespace;
            if (TryCreateClassTag(attribute, out var tag))
            {
                builder.Add(tag);
            }
        }

        foreach (var property in typeSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            foreach (var attribute in property.GetAttributes())
            {
                if (!TryGetApiNamespace(attribute, PlcTagAttributeName, out var tagApiNamespace))
                {
                    continue;
                }

                apiNamespace ??= tagApiNamespace;
                if (TryCreatePropertyTag(property, attribute, out var tag))
                {
                    builder.Add(tag);
                }
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>Creates a tag model from a class-level tag attribute.</summary>
    /// <param name="attribute">The attribute data.</param>
    /// <param name="tag">The created tag model.</param>
    /// <returns>True when the attribute contains a valid tag model.</returns>
    private static bool TryCreateClassTag(AttributeData attribute, out TagModel tag)
    {
        tag = default;
        if (attribute.ConstructorArguments.Length != ClassTagConstructorArgumentCount ||
            attribute.ConstructorArguments[ClassTagValueTypeArgumentIndex].Value is not ITypeSymbol valueType ||
            attribute.ConstructorArguments[ClassTagPropertyNameArgumentIndex].Value is not string propertyName ||
            attribute.ConstructorArguments[ClassTagNameArgumentIndex].Value is not string tagName ||
            string.IsNullOrWhiteSpace(propertyName) ||
            string.IsNullOrWhiteSpace(tagName))
        {
            return false;
        }

        var settings = ReadSettings(attribute, propertyName);
        tag = new(
            propertyName,
            tagName,
            GetObserveType(valueType),
            GetObserveType(valueType),
            GetRegisterType(valueType, settings.Bit),
            settings,
            generateProperty: true);
        return true;
    }

    /// <summary>Creates a tag model from a property-level tag attribute.</summary>
    /// <param name="property">The attributed property.</param>
    /// <param name="attribute">The attribute data.</param>
    /// <param name="tag">The created tag model.</param>
    /// <returns>True when the attribute contains a valid tag model.</returns>
    private static bool TryCreatePropertyTag(IPropertySymbol property, AttributeData attribute, out TagModel tag)
    {
        tag = default;
        if (attribute.ConstructorArguments.Length == 0 ||
            attribute.ConstructorArguments[0].Value is not string tagName ||
            string.IsNullOrWhiteSpace(tagName))
        {
            return false;
        }

        var settings = ReadSettings(attribute, property.Name);
        var valueType = GetNullableUnderlyingType(property.Type) ?? property.Type;
        tag = new(
            property.Name,
            tagName,
            GetObserveType(valueType),
            property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            GetRegisterType(valueType, settings.Bit),
            settings,
            generateProperty: false);
        return true;
    }

    /// <summary>Reads optional tag settings from named attribute arguments.</summary>
    /// <param name="attribute">The attribute data.</param>
    /// <param name="propertyName">The fallback property name.</param>
    /// <returns>The resolved tag settings.</returns>
    private static TagSettings ReadSettings(AttributeData attribute, string propertyName)
    {
        var variable = propertyName;
        var group = "Default";
        var bit = -1;
        var registerTag = true;

        foreach (var namedArgument in attribute.NamedArguments)
        {
            switch (namedArgument.Key)
            {
                case "Variable" when namedArgument.Value.Value is string value && !string.IsNullOrWhiteSpace(value):
                {
                    variable = value;
                    break;
                }

                case "Group" when namedArgument.Value.Value is string value && !string.IsNullOrWhiteSpace(value):
                {
                    group = value;
                    break;
                }

                case "Bit" when namedArgument.Value.Value is int value:
                {
                    bit = value;
                    break;
                }

                case "RegisterTag" when namedArgument.Value.Value is bool value:
                {
                    registerTag = value;
                    break;
                }
            }
        }

        return new(variable, group, bit, registerTag);
    }

    /// <summary>Generates the partial model source for collected tags.</summary>
    /// <param name="typeSymbol">The target type symbol.</param>
    /// <param name="tags">The tags to generate.</param>
    /// <param name="apiNamespace">The runtime API namespace used by generated controller references.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateModel(INamedTypeSymbol typeSymbol, ImmutableArray<TagModel> tags, string apiNamespace)
    {
        var builder = new StringBuilder();
        var namespaceName = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : typeSymbol.ContainingNamespace.ToDisplayString();

        _ = builder.AppendLine("// <auto-generated />");
        _ = builder.AppendLine("#nullable enable");
        _ = builder.AppendLine("using System;");
        _ = apiNamespace == ReactiveApiNamespace
            ? builder.AppendLine("using ReactiveUI.Primitives.Reactive;")
            : builder.AppendLine("using ReactiveUI.Primitives;");
        _ = builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(namespaceName))
        {
            _ = builder.Append("namespace ").Append(namespaceName).AppendLine(";");
            _ = builder.AppendLine();
        }

        _ = builder.Append("partial class ").AppendLine(typeSymbol.Name);
        _ = builder.AppendLine("{");
        _ = builder.AppendLine(
            "    private readonly global::ReactiveUI.Primitives.Disposables.MultipleDisposable")
            .AppendLine("        _abPlcRxSubscriptions = new();");
        _ = builder.Append("    private global::").Append(apiNamespace).AppendLine(".IABPlcRx? _abPlcRxController;");
        _ = builder.Append("    private global::")
            .Append(apiNamespace)
            .AppendLine(".ABLogicalTagClient? _abPlcRxTagClient;");
        _ = builder.AppendLine();
        _ = builder.AppendLine(
            "    /// <summary>Gets the shared logical-tag client after streams are attached.</summary>");
        _ = builder.AppendLine("    public global::CP.IoT.Core.ILogicalTagClient TagClient =>");
        _ = builder.AppendLine("        _abPlcRxTagClient ?? throw new global::System.InvalidOperationException(");
        _ = builder.AppendLine("            \"Call AttachPlcStreams before using generated PLC helpers.\");");
        _ = builder.AppendLine();

        foreach (var tag in tags)
        {
            AppendTagMembers(builder, tag, apiNamespace);
        }

        AppendAttachMethod(builder, tags, apiNamespace);
        AppendDetachMethod(builder, tags);
        AppendObserverType(builder);

        _ = builder.AppendLine("}");
        return builder.ToString();
    }

    /// <summary>Appends properties and observable accessors for one tag.</summary>
    /// <param name="builder">The target source builder.</param>
    /// <param name="tag">The tag model.</param>
    /// <param name="apiNamespace">The runtime API namespace used by generated controller references.</param>
    private static void AppendTagMembers(StringBuilder builder, TagModel tag, string apiNamespace)
    {
        var fieldName = $"_{ToCamelCase(SanitizeIdentifier(tag.PropertyName))}";
        var observableFieldName = $"{fieldName}Observable";

        _ = builder.Append("    private global::System.IObservable<")
            .Append(tag.ObserveType)
            .Append(">? ")
            .Append(observableFieldName)
            .AppendLine(";");

        if (tag.GenerateProperty)
        {
            AppendGeneratedProperty(builder, tag, fieldName);
        }

        _ = builder.AppendLine();
        _ = builder.Append("    public global::System.IObservable<")
            .Append(tag.ObserveType)
            .Append("> ")
            .Append(tag.PropertyName)
            .AppendLine("Observable =>");
        _ = builder.Append("        ")
            .Append(observableFieldName)
            .Append(" ?? throw new global::System.InvalidOperationException(\"Call ")
            .Append("AttachPlcStreams").AppendLine(" before reading generated PLC observables.\");");
        _ = builder.AppendLine();
        _ = builder.AppendLine("#if NET8_0_OR_GREATER");
        _ = builder.Append("    public global::ReactiveUI.Primitives.Async.IObservableAsync<")
            .Append(tag.ObserveType)
            .Append("> ")
            .Append(tag.PropertyName).AppendLine("ObservableAsync =>");
        _ = builder.Append("        global::")
            .Append(apiNamespace)
            .Append(".ObservableAsyncBridgeExtensions.ToAsyncObservable(")
            .Append(tag.PropertyName).AppendLine("Observable);");
        _ = builder.AppendLine("#endif");
        _ = builder.AppendLine();
        _ = builder.Append("    /// <summary>Reads ")
            .Append(tag.PropertyName)
            .AppendLine(" by logical tag name.</summary>");
        _ = builder.Append("    public global::System.Threading.Tasks.Task<global::CP.IoT.Core.TagOperationResult<")
            .Append(tag.ObserveType).Append(">> Read").Append(tag.PropertyName)
            .AppendLine("Async(global::System.Threading.CancellationToken cancellationToken = default) =>");
        _ = builder.Append("        global::CP.IoT.Core.LogicalTagContractHelpers.ReadAsync<")
            .Append(tag.ObserveType).Append(">(TagClient, ").Append(ToLiteral(tag.Variable))
            .AppendLine(", cancellationToken);");
        _ = builder.AppendLine();
        _ = builder.Append("    /// <summary>Writes ")
            .Append(tag.PropertyName)
            .AppendLine(" by logical tag name.</summary>");
        _ = builder.Append("    public global::System.Threading.Tasks.Task<global::CP.IoT.Core.TagOperationResult<")
            .Append(tag.ObserveType).Append(">> Write").Append(tag.PropertyName).Append("Async(")
            .Append(tag.ObserveType)
            .AppendLine(" value, global::System.Threading.CancellationToken cancellationToken = default) =>");
        _ = builder.Append("        global::CP.IoT.Core.LogicalTagContractHelpers.WriteAsync<")
            .Append(tag.ObserveType).Append(">(TagClient, ").Append(ToLiteral(tag.Variable))
            .AppendLine(", value, cancellationToken);");
        _ = builder.AppendLine();
    }

    /// <summary>Appends one generated value property.</summary>
    /// <param name="builder">The target source builder.</param>
    /// <param name="tag">The tag model.</param>
    /// <param name="fieldName">The generated backing field name.</param>
    private static void AppendGeneratedProperty(StringBuilder builder, TagModel tag, string fieldName)
    {
        _ = builder.Append("    private ").Append(tag.PropertyType).Append(' ').Append(fieldName).AppendLine(";");
        _ = builder.AppendLine();
        _ = builder.Append("    public ").Append(tag.PropertyType).Append(' ').AppendLine(tag.PropertyName);
        _ = builder.AppendLine(ClassOpenBrace);
        _ = builder.Append("        get => ").Append(fieldName).AppendLine(";");
        _ = builder.Append("        private set => ").Append(fieldName).AppendLine(" = value;");
        _ = builder.AppendLine(ClassCloseBrace);
    }

    /// <summary>Appends the AttachPlcStreams method.</summary>
    /// <param name="builder">The target source builder.</param>
    /// <param name="tags">The generated tags.</param>
    /// <param name="apiNamespace">The runtime API namespace used by generated controller references.</param>
    private static void AppendAttachMethod(StringBuilder builder, ImmutableArray<TagModel> tags, string apiNamespace)
    {
        _ = builder.Append("    public global::System.IDisposable AttachPlcStreams(global::")
            .Append(apiNamespace)
            .AppendLine(".IABPlcRx controller)");
        _ = builder.AppendLine(ClassOpenBrace);
        _ = builder.AppendLine("        if (controller is null)");
        _ = builder.AppendLine(MethodOpenBrace);
        _ = builder.AppendLine("            throw new global::System.ArgumentNullException(nameof(controller));");
        _ = builder.AppendLine(MethodCloseBrace);
        _ = builder.AppendLine();
        _ = builder.AppendLine("        DetachPlcStreams();");
        _ = builder.AppendLine("        _abPlcRxController = controller;");
        _ = builder.Append("        _abPlcRxTagClient = new global::").Append(apiNamespace)
            .AppendLine(".ABLogicalTagClient(controller);");
        _ = builder.AppendLine();

        foreach (var tag in tags)
        {
            AppendAttachTag(builder, tag);
        }

        _ = builder.AppendLine();
        _ = builder.AppendLine(
            "        return new global::ReactiveUI.Primitives.Disposables.ActionDisposable(DetachPlcStreams);");
        _ = builder.AppendLine(ClassCloseBrace);
        _ = builder.AppendLine();
    }

    /// <summary>Appends registration and subscription statements for one tag.</summary>
    /// <param name="builder">The target source builder.</param>
    /// <param name="tag">The generated tag.</param>
    private static void AppendAttachTag(StringBuilder builder, TagModel tag)
    {
        var observableFieldName = $"_{ToCamelCase(SanitizeIdentifier(tag.PropertyName))}Observable";
        if (tag.RegisterTag)
        {
            _ = builder.Append("        controller.AddUpdateTagItem<").Append(tag.RegisterType).Append(">(")
                .Append(ToLiteral(tag.Variable)).Append(", ")
                .Append(ToLiteral(tag.TagName)).Append(", ")
                .Append(ToLiteral(tag.Group)).Append(", default(")
                .Append(tag.RegisterType).AppendLine("));");
        }

        _ = builder.Append("        _abPlcRxTagClient.Catalog.Upsert(new global::CP.IoT.Core.LogicalTag(")
            .Append(ToLiteral(tag.Variable)).Append(", ")
            .Append(ToLiteral(tag.TagName)).Append(", ")
            .Append(ToLiteral(tag.ObserveType)).Append(", ")
            .Append(ToLiteral(tag.Group));
        if (tag.Bit >= 0)
        {
            _ = builder.Append(
                    ", metadata: new global::System.Collections.Generic.Dictionary<string, string> { [\"Bit\"] = ")
                .Append(ToLiteral(tag.Bit.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                .Append(" }");
        }

        _ = builder.AppendLine("));");
        _ = builder.Append("        ").Append(observableFieldName).Append(" = controller.Observe<")
            .Append(tag.ObserveType).Append(">(").Append(ToLiteral(tag.Variable)).Append(", default(")
            .Append(tag.ObserveType).Append("), ")
            .Append(tag.Bit.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .AppendLine(").Publish().RefCount();");
        _ = builder.Append("        _abPlcRxSubscriptions.Add(").Append(observableFieldName)
            .Append(".Subscribe(new AbPlcRxObserver<").Append(tag.ObserveType).Append(">(value => ")
            .Append(tag.PropertyName).AppendLine(" = value)));");
    }

    /// <summary>Appends the DetachPlcStreams method.</summary>
    /// <param name="builder">The target source builder.</param>
    /// <param name="tags">The generated tags.</param>
    private static void AppendDetachMethod(StringBuilder builder, ImmutableArray<TagModel> tags)
    {
        _ = builder.AppendLine("    public void DetachPlcStreams()");
        _ = builder.AppendLine(ClassOpenBrace);
        _ = builder.AppendLine("        _abPlcRxSubscriptions.Clear();");
        _ = builder.AppendLine("        _abPlcRxTagClient?.Dispose();");
        _ = builder.AppendLine("        _abPlcRxTagClient = null;");
        _ = builder.AppendLine("        _abPlcRxController = null;");

        foreach (var tag in tags)
        {
            _ = builder.Append("        _")
                .Append(ToCamelCase(SanitizeIdentifier(tag.PropertyName)))
                .AppendLine("Observable = null;");
        }

        _ = builder.AppendLine(ClassCloseBrace);
    }

    /// <summary>Appends the generated observer wrapper type.</summary>
    /// <param name="builder">The target source builder.</param>
    private static void AppendObserverType(StringBuilder builder)
    {
        _ = builder.AppendLine();
        _ = builder.AppendLine("    private sealed class AbPlcRxObserver<T>(global::System.Action<T> onNext)")
            .AppendLine("        : global::System.IObserver<T>");
        _ = builder.AppendLine(ClassOpenBrace);
        _ = builder.AppendLine("        public void OnCompleted()");
        _ = builder.AppendLine(MethodOpenBrace);
        _ = builder.AppendLine(MethodCloseBrace);
        _ = builder.AppendLine();
        _ = builder.AppendLine("        public void OnError(global::System.Exception error)");
        _ = builder.AppendLine(MethodOpenBrace);
        _ = builder.AppendLine(MethodCloseBrace);
        _ = builder.AppendLine();
        _ = builder.AppendLine("        public void OnNext(T value) => onNext(value);");
        _ = builder.AppendLine(ClassCloseBrace);
    }

    /// <summary>Gets the runtime API namespace from an attribute collection.</summary>
    /// <param name="attributes">The attributes to inspect.</param>
    /// <param name="attributeName">The attribute type name.</param>
    /// <returns>The runtime API namespace, or null.</returns>
    private static string? GetApiNamespace(ImmutableArray<AttributeData> attributes, string attributeName)
    {
        foreach (var attribute in attributes)
        {
            if (TryGetApiNamespace(attribute, attributeName, out var apiNamespace))
            {
                return apiNamespace;
            }
        }

        return null;
    }

    /// <summary>Gets the runtime API namespace from one source-generation attribute.</summary>
    /// <param name="attribute">The attribute data.</param>
    /// <param name="attributeName">The attribute type name.</param>
    /// <param name="apiNamespace">The resolved runtime API namespace.</param>
    /// <returns>True when the attribute belongs to an ABPlcRx API namespace.</returns>
    private static bool TryGetApiNamespace(AttributeData attribute, string attributeName, out string apiNamespace)
    {
        apiNamespace = string.Empty;
        var attributeClass = attribute.AttributeClass;
        if (attributeClass?.Name != attributeName)
        {
            return false;
        }

        var sourceGenerationNamespace = attributeClass.ContainingNamespace.ToDisplayString();
        if (!sourceGenerationNamespace.EndsWith(SourceGenerationNamespaceSuffix, System.StringComparison.Ordinal))
        {
            return false;
        }

        var candidateApiNamespace = sourceGenerationNamespace.Substring(
            0,
            sourceGenerationNamespace.Length - SourceGenerationNamespaceSuffix.Length);
        if (candidateApiNamespace is not (DefaultApiNamespace or ReactiveApiNamespace))
        {
            return false;
        }

        apiNamespace = candidateApiNamespace;
        return true;
    }

    /// <summary>Checks whether a type declaration is partial.</summary>
    /// <param name="declaration">The declaration.</param>
    /// <returns>True when the declaration is partial.</returns>
    private static bool IsPartial(TypeDeclarationSyntax declaration) =>
        declaration.Modifiers.Any(SyntaxKind.PartialKeyword);

    /// <summary>Creates the diagnostic for non-partial PLC models.</summary>
    /// <param name="declaration">The declaration.</param>
    /// <param name="typeSymbol">The type symbol.</param>
    /// <returns>The diagnostic instance.</returns>
    private static Diagnostic CreatePartialRequiredDiagnostic(
        TypeDeclarationSyntax declaration,
        INamedTypeSymbol typeSymbol) =>
        Diagnostic.Create(PartialRequiredDescriptor, declaration.Identifier.GetLocation(), typeSymbol.Name);

    /// <summary>Gets the underlying type for nullable value types.</summary>
    /// <param name="type">The type symbol.</param>
    /// <returns>The nullable underlying type, or null.</returns>
    private static ITypeSymbol? GetNullableUnderlyingType(ITypeSymbol type) =>
        type is INamedTypeSymbol namedType &&
        namedType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T
            ? namedType.TypeArguments[0]
            : null;

    /// <summary>Checks whether a type represents a Boolean value.</summary>
    /// <param name="type">The type symbol.</param>
    /// <returns>True when the type is Boolean.</returns>
    private static bool IsBoolean(ITypeSymbol type) =>
        (GetNullableUnderlyingType(type) ?? type).SpecialType == SpecialType.System_Boolean;

    /// <summary>Gets the PLC registration type for a tag.</summary>
    /// <param name="type">The observed type.</param>
    /// <param name="bit">The configured bit index.</param>
    /// <returns>The registration type name.</returns>
    private static string GetRegisterType(ITypeSymbol type, int bit) =>
        IsBoolean(type) && bit >= 0 ? "short" : GetObserveType(type);

    /// <summary>Gets the generated observable value type.</summary>
    /// <param name="type">The source type.</param>
    /// <returns>The generated type name.</returns>
    private static string GetObserveType(ITypeSymbol type) =>
        (GetNullableUnderlyingType(type) ?? type).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    /// <summary>Gets a safe generated hint name for the target type.</summary>
    /// <param name="typeSymbol">The target type symbol.</param>
    /// <returns>The generated hint name.</returns>
    private static string GetSafeHintName(INamedTypeSymbol typeSymbol)
    {
        var name = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var builder = new StringBuilder(name.Length);
        foreach (var character in name)
        {
            _ = builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return builder.ToString();
    }

    /// <summary>Sanitizes an attribute value into a C# identifier.</summary>
    /// <param name="value">The original value.</param>
    /// <returns>The sanitized identifier.</returns>
    private static string SanitizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Value";
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            _ = builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        }

        if (builder.Length == 0 || !IsIdentifierStart(builder[0]))
        {
            _ = builder.Insert(0, '_');
        }

        return builder.ToString();
    }

    /// <summary>Checks whether a character can start a generated identifier.</summary>
    /// <param name="value">The character to check.</param>
    /// <returns>True when the character can start an identifier.</returns>
    private static bool IsIdentifierStart(char value) =>
        char.IsLetter(value) || value == '_';

    /// <summary>Converts an identifier to camel case.</summary>
    /// <param name="value">The source identifier.</param>
    /// <returns>The camel-cased identifier.</returns>
    private static string ToCamelCase(string value) =>
        string.IsNullOrWhiteSpace(value) ? "value" : char.ToLowerInvariant(value[0]) + value.Remove(0, 1);

    /// <summary>Converts a string to a generated verbatim string literal.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The generated literal.</returns>
    private static string ToLiteral(string value) =>
        $"@\"{value.Replace("\"", "\"\"")}\"";
}
