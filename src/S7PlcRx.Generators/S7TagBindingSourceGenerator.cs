// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace S7PlcRx.SourceGenerators;

/// <summary>Generates strongly typed PLC property binding hooks from S7 tag attributes.</summary>
[Generator(LanguageNames.CSharp)]
public sealed partial class S7TagBindingSourceGenerator : IIncrementalGenerator
{
    /// <summary>Original binding attribute metadata name.</summary>
    private const string BindingAttributeName = "S7PlcRx.SourceGeneration.S7PlcBindingAttribute";

    /// <summary>Reactive binding attribute metadata name.</summary>
    private const string ReactiveBindingAttributeName = "S7PlcRx.Reactive.SourceGeneration.S7PlcBindingAttribute";

    /// <summary>Original tag attribute metadata name.</summary>
    private const string TagAttributeName = "S7PlcRx.SourceGeneration.S7TagAttribute";

    /// <summary>Reactive tag attribute metadata name.</summary>
    private const string ReactiveTagAttributeName = "S7PlcRx.Reactive.SourceGeneration.S7TagAttribute";

    /// <summary>Generated member-level opening brace.</summary>
    private const string MemberBlockOpen = "    {";

    /// <summary>Generated nested opening brace.</summary>
    private const string NestedBlockOpen = "        {";

    /// <summary>Generated nested closing brace.</summary>
    private const string NestedBlockClose = "        }";

    /// <summary>Generated member-level closing brace.</summary>
    private const string MemberBlockClose = "    }";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) =>
                    node is ClassDeclarationSyntax classDeclaration && classDeclaration.AttributeLists.Count > 0,
                static (ctx, _) => GetBindingClass(ctx))
            .Where(static item => item is not null);

        var diagnostics = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) =>
                node is ClassDeclarationSyntax classDeclaration && classDeclaration.AttributeLists.Count > 0,
            GetDiagnostics);

        context.RegisterSourceOutput(candidates.Collect(), Execute);
        context.RegisterSourceOutput(diagnostics, static (ctx, items) =>
        {
            foreach (var diagnostic in items)
            {
                ctx.ReportDiagnostic(diagnostic);
            }
        });
    }

    /// <summary>Gets binding metadata for an attributed class.</summary>
    /// <param name="context">The generator syntax context.</param>
    /// <returns>The binding metadata, or null when the class is not a binding target.</returns>
    private static BindingClass? GetBindingClass(GeneratorSyntaxContext context)
    {
        var classSyntax = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(classSyntax) is not INamedTypeSymbol classSymbol)
        {
            return null;
        }

        if (!HasBindingAttribute(classSyntax, context.SemanticModel))
        {
            return null;
        }

        var properties = new List<BindingProperty>();
        foreach (var propertySymbol in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            var propertySyntax = propertySymbol.DeclaringSyntaxReferences
                .Select(static reference => reference.GetSyntax())
                .OfType<PropertyDeclarationSyntax>()
                .FirstOrDefault();
            if (propertySyntax is null)
            {
                continue;
            }

            var attribute = FindAttribute(propertySymbol.GetAttributes(), TagAttributeName, ReactiveTagAttributeName);
            if (attribute is null)
            {
                continue;
            }

            if (!HasPartialModifier(propertySyntax.Modifiers))
            {
                continue;
            }

            properties.Add(CreateProperty(propertySymbol, attribute));
        }

        if (properties.Count == 0)
        {
            return null;
        }

        var namespaceName = classSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : classSymbol.ContainingNamespace.ToDisplayString();
        return new BindingClass(namespaceName, classSymbol.Name, classSymbol.DeclaredAccessibility, properties);
    }

    /// <summary>Creates generated property metadata from an attributed property.</summary>
    /// <param name="property">The property symbol.</param>
    /// <param name="attribute">The tag attribute data.</param>
    /// <returns>The generated binding property metadata.</returns>
    private static BindingProperty CreateProperty(IPropertySymbol property, AttributeData attribute)
    {
        var address = attribute.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value?.ToString() ?? string.Empty
            : string.Empty;
        var pollIntervalMs = 100;
        var direction = "ReadWrite";
        var arrayLength = 1;

        foreach (var argument in attribute.NamedArguments)
        {
            switch (argument.Key)
            {
                case "PollIntervalMs":
                {
                    pollIntervalMs = Convert.ToInt32(argument.Value.Value, CultureInfo.InvariantCulture);
                    break;
                }

                case "Direction":
                {
                    direction = argument.Value.Value?.ToString() switch
                    {
                        "1" => "ReadOnly",
                        "2" => "WriteOnly",
                        _ => "ReadWrite",
                    };
                    break;
                }

                case "ArrayLength":
                {
                    arrayLength = Convert.ToInt32(argument.Value.Value, CultureInfo.InvariantCulture);
                    break;
                }
            }
        }

        return new BindingProperty(
            property.Name,
            property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            address,
            Math.Max(0, pollIntervalMs),
            direction,
            Math.Max(1, arrayLength));
    }

    /// <summary>Emits generated files for all binding classes.</summary>
    /// <param name="context">The source production context.</param>
    /// <param name="items">The binding class metadata items.</param>
    private static void Execute(SourceProductionContext context, ImmutableArray<BindingClass?> items)
    {
        var generatedTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (item is null)
            {
                continue;
            }

            var typeName = $"{item.NamespaceName}.{item.ClassName}";
            if (!generatedTypes.Add(typeName))
            {
                continue;
            }

            context.AddSource($"{item.ClassName}.S7Binding.g.cs", SourceText.From(Generate(item), Encoding.UTF8));
        }
    }

    /// <summary>Generates binding source for one class.</summary>
    /// <param name="bindingClass">The binding class metadata.</param>
    /// <returns>The generated source text.</returns>
    private static string Generate(BindingClass bindingClass)
    {
        var libraryRoot = GetLibraryRoot(bindingClass);
        var builder = new StringBuilder();
        AppendLine(builder, "// <auto-generated />");
        AppendLine(builder, "#nullable enable");
        AppendLine(builder, "using System;");
        AppendLine(builder, "using System.Collections.Generic;");
        AppendLine(builder);

        if (!string.IsNullOrWhiteSpace(bindingClass.NamespaceName))
        {
            AppendLine(builder, $"namespace {bindingClass.NamespaceName};");
            AppendLine(builder);
        }

        AppendLine(builder, $"{GetAccessibility(bindingClass.Accessibility)} partial class {bindingClass.ClassName}");
        AppendLine(builder, "{");
        AppendLine(builder, $"    private global::{libraryRoot}.Binding.S7TagRuntimeBinding? __s7Binding;");
        AppendLine(builder, $"    private global::{libraryRoot}.LogicalTags.S7LogicalTagClient? __s7LogicalClient;");
        AppendLine(builder, $"    private global::{libraryRoot}.Binding.S7TagBindingSession? __s7BindingSession;");
        AppendLine(builder, "    private bool __s7SuppressWrites;");
        AppendLine(builder);

        foreach (var property in bindingClass.Properties)
        {
            AppendLine(builder, $"    private {property.FullyQualifiedTypeName} {GetBackingFieldName(property.Name)};");
            var observableType =
                $"global::{libraryRoot}.Binding.S7TagValueObservable<{property.FullyQualifiedTypeName}>";
            AppendLine(
                builder,
                $"    private readonly {observableType} {GetObservableFieldName(property.Name)} = " +
                $"new {observableType}();");
        }

        AppendLine(builder);
        GenerateDefinitions(builder, libraryRoot, bindingClass.Properties);
        foreach (var property in bindingClass.Properties)
        {
            GenerateProperty(builder, libraryRoot, property);
        }

        GenerateBindMethod(builder, libraryRoot);
        GenerateApplyRead(builder, bindingClass.Properties);

        AppendLine(builder, "}");
        return builder.ToString();
    }

    /// <summary>Generates one partial property implementation.</summary>
    /// <param name="builder">The generated source builder.</param>
    /// <param name="libraryRoot">The root namespace for generated library references.</param>
    /// <param name="property">The binding property metadata.</param>
    private static void GenerateProperty(StringBuilder builder, string libraryRoot, BindingProperty property)
    {
        var backingField = GetBackingFieldName(property.Name);
        AppendLine(builder, $"    public partial {property.FullyQualifiedTypeName} {property.Name}");
        AppendLine(builder, MemberBlockOpen);
        AppendLine(builder, $"        get => {backingField};");
        AppendLine(builder, "        set");
        AppendLine(builder, NestedBlockOpen);
        AppendLine(
            builder,
            $"            if (global::System.Collections.Generic.EqualityComparer<{property.FullyQualifiedTypeName}>" +
            $".Default.Equals({backingField}, value))");
        AppendLine(builder, "            {");
        AppendLine(builder, "                return;");
        AppendLine(builder, "            }");
        AppendLine(builder);
        AppendLine(builder, $"            {backingField} = value;");
        AppendLine(builder, $"            {GetObservableFieldName(property.Name)}.Publish(value);");
        AppendLine(builder, $"            __s7Write(nameof({property.Name}), value);");
        AppendLine(builder, NestedBlockClose);
        AppendLine(builder, MemberBlockClose);
        AppendLine(builder);
        GeneratePropertyOperations(builder, libraryRoot, property);
    }

    /// <summary>Generates observable and explicit read/write members for one property.</summary>
    /// <param name="builder">The generated source builder.</param>
    /// <param name="libraryRoot">The root namespace for generated library references.</param>
    /// <param name="property">The binding property metadata.</param>
    private static void GeneratePropertyOperations(
        StringBuilder builder,
        string libraryRoot,
        BindingProperty property)
    {
        AppendLine(builder, $"    /// <summary>Gets an observable for {property.Name} values.</summary>");
        AppendLine(
            builder,
            $"    public global::System.IObservable<{property.FullyQualifiedTypeName}> {property.Name}Observable => " +
            $"{GetObservableFieldName(property.Name)};");
        AppendLine(builder);
        AppendLine(builder, $"    /// <summary>Gets an async sequence for {property.Name} values.</summary>");
        AppendLine(
            builder,
            $"    public global::System.Collections.Generic.IAsyncEnumerable<{property.FullyQualifiedTypeName}> " +
            $"{property.Name}ObservableAsync => global::{libraryRoot}.Binding.S7TagObservableAdapter" +
            $".ToAsyncEnumerable({GetObservableFieldName(property.Name)});");
        AppendLine(builder);
        var resultType = $"global::CP.IoT.Core.TagOperationResult<{property.FullyQualifiedTypeName}>";
        GenerateReadOperation(builder, libraryRoot, property, resultType);
        GenerateWriteOperation(builder, libraryRoot, property, resultType);
    }

    /// <summary>Generates the explicit read member for one property.</summary>
    /// <param name="builder">The generated source builder.</param>
    /// <param name="libraryRoot">The root namespace for generated library references.</param>
    /// <param name="property">The binding property metadata.</param>
    /// <param name="resultType">The generated result type.</param>
    private static void GenerateReadOperation(
        StringBuilder builder,
        string libraryRoot,
        BindingProperty property,
        string resultType)
    {
        AppendLine(builder, $"    /// <summary>Reads {property.Name} through the common logical client.</summary>");
        AppendLine(builder, "    /// <param name=\"cancellationToken\">The cancellation token.</param>");
        AppendLine(builder, "    /// <returns>The typed operation result.</returns>");
        AppendLine(
            builder,
            string.Concat(
                $"    public global::System.Threading.Tasks.Task<{resultType}> ",
                $"Read{property.Name}Async(",
                "global::System.Threading.CancellationToken cancellationToken = default)"));
        AppendLine(builder, MemberBlockOpen);
        AppendLine(
            builder,
            "        var client = __s7LogicalClient ?? throw new global::System.InvalidOperationException(" +
            "\"Call Bind before reading generated S7 tags.\");");
        AppendLine(
            builder,
            $"        return global::{libraryRoot}.LogicalTags.S7LogicalTagExtensions" +
            $".ReadAsync<{property.FullyQualifiedTypeName}>(client, nameof({property.Name}), " +
            $"default({property.FullyQualifiedTypeName}), cancellationToken);");
        AppendLine(builder, MemberBlockClose);
        AppendLine(builder);
    }

    /// <summary>Generates the explicit write member for one property.</summary>
    /// <param name="builder">The generated source builder.</param>
    /// <param name="libraryRoot">The root namespace for generated library references.</param>
    /// <param name="property">The binding property metadata.</param>
    /// <param name="resultType">The generated result type.</param>
    private static void GenerateWriteOperation(
        StringBuilder builder,
        string libraryRoot,
        BindingProperty property,
        string resultType)
    {
        AppendLine(builder, $"    /// <summary>Writes {property.Name} through the common logical client.</summary>");
        AppendLine(builder, "    /// <param name=\"value\">The value to write.</param>");
        AppendLine(builder, "    /// <param name=\"cancellationToken\">The cancellation token.</param>");
        AppendLine(builder, "    /// <returns>The typed operation result.</returns>");
        AppendLine(
            builder,
            string.Concat(
                $"    public global::System.Threading.Tasks.Task<{resultType}> ",
                $"Write{property.Name}Async(",
                $"{property.FullyQualifiedTypeName} value, ",
                "global::System.Threading.CancellationToken cancellationToken = default)"));
        AppendLine(builder, MemberBlockOpen);
        AppendLine(
            builder,
            "        var client = __s7LogicalClient ?? throw new global::System.InvalidOperationException(" +
            "\"Call Bind before writing generated S7 tags.\");");
        AppendLine(
            builder,
            $"        return global::{libraryRoot}.LogicalTags.S7LogicalTagExtensions" +
            $".WriteAsync(client, nameof({property.Name}), value, cancellationToken);");
        AppendLine(builder, MemberBlockClose);
        AppendLine(builder);
    }

    /// <summary>Generates the reusable public tag-definition catalog.</summary>
    /// <param name="builder">The generated source builder.</param>
    /// <param name="libraryRoot">The root namespace for generated library references.</param>
    /// <param name="properties">The generated binding properties.</param>
    private static void GenerateDefinitions(
        StringBuilder builder,
        string libraryRoot,
        IReadOnlyList<BindingProperty> properties)
    {
        var definitionType = $"global::{libraryRoot}.Binding.S7TagDefinition";
        AppendLine(builder, "    /// <summary>Gets the generated S7 tag definitions.</summary>");
        AppendLine(
            builder,
            $"    public static global::System.Collections.Generic.IReadOnlyList<{definitionType}> " +
            $"S7TagDefinitions {{ get; }} = new {definitionType}[]");
        AppendLine(builder, MemberBlockOpen);
        foreach (var property in properties)
        {
            AppendLine(
                builder,
                $"        new {definitionType}(nameof({property.Name}), {ToLiteral(property.Address)}, " +
                $"typeof({property.FullyQualifiedTypeName}), " +
                $"{property.PollIntervalMs.ToString(CultureInfo.InvariantCulture)}, " +
                $"global::{libraryRoot}.Binding.S7TagDirection.{property.Direction}, " +
                $"{property.ArrayLength.ToString(CultureInfo.InvariantCulture)}),");
        }

        AppendLine(builder, $"{MemberBlockClose};");
        AppendLine(builder);
        AppendLine(builder, "    /// <summary>Creates the common logical-tag catalog.</summary>");
        AppendLine(builder, "    /// <returns>The generated logical-tag catalog.</returns>");
        AppendLine(builder, "    public static global::CP.IoT.Core.LogicalTagCatalog CreateLogicalTagCatalog() =>");
        AppendLine(
            builder,
            string.Concat(
                $"        global::{libraryRoot}.LogicalTags.S7LogicalTagExtensions",
                ".CreateLogicalTagCatalog(S7TagDefinitions);"));
        AppendLine(builder);
    }

    /// <summary>Generates the Bind method and write callback.</summary>
    /// <param name="builder">The generated source builder.</param>
    /// <param name="libraryRoot">The root namespace for generated library references.</param>
    private static void GenerateBindMethod(StringBuilder builder, string libraryRoot)
    {
        AppendLine(builder, "    /// <summary>Binds the generated members to an S7 connection.</summary>");
        AppendLine(builder, "    /// <param name=\"plc\">The S7 connection.</param>");
        AppendLine(builder, "    /// <returns>The binding session.</returns>");
        AppendLine(builder, $"    public global::System.IDisposable Bind(global::{libraryRoot}.IRxS7 plc)");
        AppendLine(builder, MemberBlockOpen);
        AppendLine(builder, "        if (plc is null)");
        AppendLine(builder, NestedBlockOpen);
        AppendLine(builder, "            throw new global::System.ArgumentNullException(nameof(plc));");
        AppendLine(builder, NestedBlockClose);
        AppendLine(builder);
        AppendLine(builder, "        __s7BindingSession?.Dispose();");
        AppendLine(
            builder,
            string.Concat(
                $"        __s7Binding = global::{libraryRoot}.Binding.S7TagRuntimeBinding",
                ".Bind(plc, S7TagDefinitions, __s7ApplyRead);"));
        AppendLine(
            builder,
                string.Concat(
                    $"        __s7LogicalClient = new global::{libraryRoot}.LogicalTags.S7LogicalTagClient(",
                    "plc, CreateLogicalTagCatalog(), null);"));
        AppendLine(
            builder,
            string.Concat(
                $"        __s7BindingSession = new global::{libraryRoot}.Binding.S7TagBindingSession(",
                "__s7Binding, __s7LogicalClient);"));
        AppendLine(builder, "        return __s7BindingSession;");
        AppendLine(builder, MemberBlockClose);
        AppendLine(builder);
        AppendLine(builder, "    private void __s7Write(string name, object? value)");
        AppendLine(builder, MemberBlockOpen);
        AppendLine(builder, "        if (!__s7SuppressWrites)");
        AppendLine(builder, NestedBlockOpen);
        AppendLine(builder, "            __s7Binding?.Write(name, value);");
        AppendLine(builder, NestedBlockClose);
        AppendLine(builder, MemberBlockClose);
        AppendLine(builder);
    }

    /// <summary>Generates the read callback switch.</summary>
    /// <param name="builder">The generated source builder.</param>
    /// <param name="properties">The generated binding properties.</param>
    private static void GenerateApplyRead(StringBuilder builder, IReadOnlyList<BindingProperty> properties)
    {
        AppendLine(builder, "    private void __s7ApplyRead(string name, object? value)");
        AppendLine(builder, MemberBlockOpen);
        AppendLine(builder, "        __s7SuppressWrites = true;");
        AppendLine(builder, "        try");
        AppendLine(builder, NestedBlockOpen);
        AppendLine(builder, "            switch (name)");
        AppendLine(builder, "            {");
        foreach (var property in properties)
        {
            AppendLine(builder, $"                case nameof({property.Name}):");
            AppendLine(builder, "                {");
            AppendLine(
                builder,
                $"                    {GetBackingFieldName(property.Name)} = " +
                $"value is {property.FullyQualifiedTypeName} typedValue ? typedValue : default!;");
            AppendLine(
                builder,
                $"                    {GetObservableFieldName(property.Name)}" +
                $".Publish({GetBackingFieldName(property.Name)});");
            AppendLine(builder, "                    break;");
            AppendLine(builder, "                }");
        }

        AppendLine(builder, "            }");
        AppendLine(builder, NestedBlockClose);
        AppendLine(builder, "        finally");
        AppendLine(builder, NestedBlockOpen);
        AppendLine(builder, "            __s7SuppressWrites = false;");
        AppendLine(builder, NestedBlockClose);
        AppendLine(builder, MemberBlockClose);
    }

    /// <summary>Checks whether an attribute collection contains either metadata name.</summary>
    /// <param name="attributes">The attributes to inspect.</param>
    /// <param name="firstMetadataName">The first accepted metadata name.</param>
    /// <param name="secondMetadataName">The second accepted metadata name.</param>
    /// <returns>True when an accepted attribute is present; otherwise false.</returns>
    private static bool HasAttribute(
        ImmutableArray<AttributeData> attributes,
        string firstMetadataName,
        string secondMetadataName)
    {
        foreach (var attribute in attributes)
        {
            if (IsAttribute(attribute, firstMetadataName, secondMetadataName))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Finds an accepted attribute in a collection.</summary>
    /// <param name="attributes">The attributes to inspect.</param>
    /// <param name="firstMetadataName">The first accepted metadata name.</param>
    /// <param name="secondMetadataName">The second accepted metadata name.</param>
    /// <returns>The matching attribute, or null when no accepted attribute exists.</returns>
    private static AttributeData? FindAttribute(
        ImmutableArray<AttributeData> attributes,
        string firstMetadataName,
        string secondMetadataName)
    {
        foreach (var attribute in attributes)
        {
            if (IsAttribute(attribute, firstMetadataName, secondMetadataName))
            {
                return attribute;
            }
        }

        return null;
    }

    /// <summary>Checks whether an attribute matches either accepted metadata name.</summary>
    /// <param name="attribute">The attribute to inspect.</param>
    /// <param name="firstMetadataName">The first accepted metadata name.</param>
    /// <param name="secondMetadataName">The second accepted metadata name.</param>
    /// <returns>True when the attribute matches; otherwise false.</returns>
    private static bool IsAttribute(AttributeData attribute, string firstMetadataName, string secondMetadataName)
    {
        var attributeName = attribute.AttributeClass?.ToDisplayString();
        return string.Equals(attributeName, firstMetadataName, StringComparison.Ordinal) ||
            string.Equals(attributeName, secondMetadataName, StringComparison.Ordinal);
    }

    /// <summary>Checks whether a property declaration is partial.</summary>
    /// <param name="modifiers">The property modifiers.</param>
    /// <returns>True when the property has a partial modifier; otherwise false.</returns>
    private static bool HasPartialModifier(SyntaxTokenList modifiers)
    {
        foreach (var modifier in modifiers)
        {
            if (modifier.IsKind(SyntaxKind.PartialKeyword))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets the library root namespace for generated references.</summary>
    /// <param name="bindingClass">The binding class metadata.</param>
    /// <returns>The root namespace to use in generated code.</returns>
    private static string GetLibraryRoot(BindingClass bindingClass) =>
        bindingClass.NamespaceName?.StartsWith("S7PlcRx.Reactive", StringComparison.Ordinal) == true
            ? "S7PlcRx.Reactive"
            : "S7PlcRx";

    /// <summary>Gets the generated backing field name.</summary>
    /// <param name="propertyName">The source property name.</param>
    /// <returns>The generated backing field name.</returns>
    private static string GetBackingFieldName(string propertyName) =>
        $"__s7{char.ToLowerInvariant(propertyName[0])}{propertyName.Remove(0, 1)}";

    /// <summary>Gets the generated observable field name.</summary>
    /// <param name="propertyName">The source property name.</param>
    /// <returns>The generated observable field name.</returns>
    private static string GetObservableFieldName(string propertyName) =>
        $"{GetBackingFieldName(propertyName)}Observable";

    /// <summary>Gets the generated C# accessibility keyword.</summary>
    /// <param name="accessibility">The symbol accessibility.</param>
    /// <returns>The generated accessibility keyword.</returns>
    private static string GetAccessibility(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => "public",
        Accessibility.Internal => "internal",
        _ => "public",
    };

    /// <summary>Escapes a string as a generated C# literal.</summary>
    /// <param name="value">The value to escape.</param>
    /// <returns>The generated string literal.</returns>
    private static string ToLiteral(string value) => $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

    /// <summary>Appends one line to generated source.</summary>
    /// <param name="builder">The generated source builder.</param>
    /// <param name="value">The line value.</param>
    private static void AppendLine(StringBuilder builder, string value = "")
    {
        _ = builder.AppendLine(value);
    }

    /// <summary>Describes a class that should receive generated bindings.</summary>
    private sealed class BindingClass
    {
        /// <summary>Initializes a new instance of the <see cref="BindingClass"/> class.</summary>
        /// <param name="namespaceName">The generated class namespace.</param>
        /// <param name="className">The generated class name.</param>
        /// <param name="accessibility">The generated class accessibility.</param>
        /// <param name="properties">The generated binding properties.</param>
        public BindingClass(
            string? namespaceName,
            string className,
            Accessibility accessibility,
            IReadOnlyList<BindingProperty> properties)
        {
            NamespaceName = namespaceName;
            ClassName = className;
            Accessibility = accessibility;
            Properties = properties;
        }

        /// <summary>Gets the generated class namespace.</summary>
        public string? NamespaceName { get; }

        /// <summary>Gets the generated class name.</summary>
        public string ClassName { get; }

        /// <summary>Gets the generated class accessibility.</summary>
        public Accessibility Accessibility { get; }

        /// <summary>Gets the generated binding properties.</summary>
        public IReadOnlyList<BindingProperty> Properties { get; }
    }

    /// <summary>Describes a property that should receive generated binding code.</summary>
    private sealed class BindingProperty
    {
        /// <summary>Initializes a new instance of the <see cref="BindingProperty"/> class.</summary>
        /// <param name="name">The source property name.</param>
        /// <param name="fullyQualifiedTypeName">The fully qualified property type name.</param>
        /// <param name="address">The PLC tag address.</param>
        /// <param name="pollIntervalMs">The polling interval in milliseconds.</param>
        /// <param name="direction">The binding direction.</param>
        /// <param name="arrayLength">The PLC array length.</param>
        public BindingProperty(
            string name,
            string fullyQualifiedTypeName,
            string address,
            int pollIntervalMs,
            string direction,
            int arrayLength)
        {
            Name = name;
            FullyQualifiedTypeName = fullyQualifiedTypeName;
            Address = address;
            PollIntervalMs = pollIntervalMs;
            Direction = direction;
            ArrayLength = arrayLength;
        }

        /// <summary>Gets the source property name.</summary>
        public string Name { get; }

        /// <summary>Gets the fully qualified property type name.</summary>
        public string FullyQualifiedTypeName { get; }

        /// <summary>Gets the PLC tag address.</summary>
        public string Address { get; }

        /// <summary>Gets the polling interval in milliseconds.</summary>
        public int PollIntervalMs { get; }

        /// <summary>Gets the binding direction.</summary>
        public string Direction { get; }

        /// <summary>Gets the PLC array length.</summary>
        public int ArrayLength { get; }
    }
}
