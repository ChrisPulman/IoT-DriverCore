// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.Driver.Serial.SourceGenerators;

/// <summary>Generates reactive serial-port properties and observable streams.</summary>
[Generator(LanguageNames.CSharp)]
[System.Diagnostics.DebuggerDisplay("SerialPortReactiveStreamGenerator")]
public sealed partial class SerialPortReactiveStreamGenerator : IIncrementalGenerator
{
    /// <summary>The generated enum value for the raw byte stream.</summary>
    private const int DataReceivedBytesSource = 2;

    /// <summary>The generated enum value for the received byte-count stream.</summary>
    private const int BytesReceivedSource = 3;

    /// <summary>The generated enum value for the open-state stream.</summary>
    private const int IsOpenObservableSource = 4;

    /// <summary>Metadata name for the marker attribute owned by the standard runtime package.</summary>
    private const string StandardAttributeMetadataName =
        "IoT.Driver.Serial.SourceGeneration.SerialPortReactiveStreamAttribute";

    /// <summary>Metadata name for the marker attribute owned by the reactive runtime package.</summary>
    private const string ReactiveAttributeMetadataName =
        "IoT.Driver.Serial.Reactive.SourceGeneration.SerialPortReactiveStreamAttribute";

    /// <summary>Diagnostic reported when a target class is not partial.</summary>
    private static readonly DiagnosticDescriptor ClassMustBePartial = new(
        "SPRX001",
        "Reactive serial stream target must be partial",
        "Type '{0}' must be partial to receive generated serial stream members",
        "SerialPortRx.SourceGeneration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>Diagnostic reported when a generated property name is invalid.</summary>
    private static readonly DiagnosticDescriptor PropertyNameMustBeIdentifier = new(
        "SPRX002",
        "Reactive serial stream property name must be a valid identifier",
        "Property name '{0}' must be a valid C# identifier",
        "SerialPortRx.SourceGeneration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var standardStreamDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                StandardAttributeMetadataName,
                static (node, _) => node is ClassDeclarationSyntax,
                static (syntaxContext, cancellationToken) => GetStreamInfos(in syntaxContext, cancellationToken));
        var reactiveStreamDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ReactiveAttributeMetadataName,
                static (node, _) => node is ClassDeclarationSyntax,
                static (syntaxContext, cancellationToken) => GetStreamInfos(in syntaxContext, cancellationToken));
        var standardCompilationAndStreams = context.CompilationProvider.Combine(standardStreamDeclarations.Collect());
        var compilationAndStreams = standardCompilationAndStreams.Combine(reactiveStreamDeclarations.Collect());

        context.RegisterSourceOutput(
            compilationAndStreams,
            static (sourceProductionContext, source) => Execute(
                sourceProductionContext,
                source.Left.Left,
                source.Left.Right,
                source.Right));
    }

    /// <summary>Gets all stream declarations from matching attributes on a target type.</summary>
    /// <param name="context">The attribute syntax context.</param>
    /// <param name="cancellationToken">The cancellation token supplied by Roslyn.</param>
    /// <returns>The stream declarations found on the target type.</returns>
    private static ImmutableArray<StreamInfo> GetStreamInfos(
        in GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.TargetSymbol is not INamedTypeSymbol targetType)
        {
            return ImmutableArray<StreamInfo>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<StreamInfo>();
        foreach (var attribute in context.Attributes)
        {
            if (IsMarkerAttribute(attribute.AttributeClass) &&
                TryCreateStreamInfo(targetType, attribute, cancellationToken, out var streamInfo) &&
                streamInfo is not null)
            {
                builder.Add(streamInfo);
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>Determines whether an attribute is a marker owned by either SerialPortRx runtime package.</summary>
    /// <param name="attributeType">The attribute type to inspect.</param>
    /// <returns><see langword="true"/> when the attribute is a supported marker type.</returns>
    private static bool IsMarkerAttribute(INamedTypeSymbol? attributeType)
    {
        var metadataName = attributeType?.ToDisplayString();
        return string.Equals(metadataName, StandardAttributeMetadataName, StringComparison.Ordinal) ||
            string.Equals(metadataName, ReactiveAttributeMetadataName, StringComparison.Ordinal);
    }

    /// <summary>Creates a stream declaration from a single attribute instance.</summary>
    /// <param name="targetType">The target class symbol.</param>
    /// <param name="attribute">The attribute data.</param>
    /// <param name="cancellationToken">The cancellation token supplied by Roslyn.</param>
    /// <param name="streamInfo">The created stream declaration.</param>
    /// <returns><see langword="true"/> when the attribute contains the required constructor data.</returns>
    private static bool TryCreateStreamInfo(
        INamedTypeSymbol targetType,
        AttributeData attribute,
        CancellationToken cancellationToken,
        out StreamInfo? streamInfo)
    {
        streamInfo = null;
        if (attribute.ConstructorArguments.Length < 2)
        {
            return false;
        }

        var propertyName = (attribute.ConstructorArguments[0].Value as string) ?? string.Empty;
        var propertyType = attribute.ConstructorArguments[1].Value as ITypeSymbol;
        var pattern = attribute.ConstructorArguments.Length > 2
            ? attribute.ConstructorArguments[2].Value as string
            : null;

        var sourceExpression = GetSourceExpression(GetNamedInt(attribute, "Source", 0));
        var groupName = GetNamedString(attribute, "GroupName", "value");
        var groupNumber = GetNamedInt(attribute, "GroupNumber", 1);
        var ignoreCase = GetNamedBool(attribute, "IgnoreCase", false);

        streamInfo = new(
            new StreamIdentity(
                targetType,
                propertyName,
                propertyType,
                attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation()
                    ?? GetFirstLocation(targetType)),
            new StreamMatchOptions(pattern, sourceExpression, groupName, groupNumber, ignoreCase));

        return true;
    }

    /// <summary>Gets a named integer argument value.</summary>
    /// <param name="attribute">The attribute data.</param>
    /// <param name="name">The named argument name.</param>
    /// <param name="defaultValue">The value used when the argument is absent.</param>
    /// <returns>The integer value, or the default value.</returns>
    private static int GetNamedInt(AttributeData attribute, string name, int defaultValue)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == name)
            {
                return (argument.Value.Value as int?) ?? defaultValue;
            }
        }

        return defaultValue;
    }

    /// <summary>Gets a named string argument value.</summary>
    /// <param name="attribute">The attribute data.</param>
    /// <param name="name">The named argument name.</param>
    /// <param name="defaultValue">The value used when the argument is absent.</param>
    /// <returns>The string value, or the default value.</returns>
    private static string? GetNamedString(AttributeData attribute, string name, string? defaultValue)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == name)
            {
                return argument.Value.Value as string;
            }
        }

        return defaultValue;
    }

    /// <summary>Gets a named boolean argument value.</summary>
    /// <param name="attribute">The attribute data.</param>
    /// <param name="name">The named argument name.</param>
    /// <param name="defaultValue">The value used when the argument is absent.</param>
    /// <returns>The boolean value, or the default value.</returns>
    private static bool GetNamedBool(AttributeData attribute, string name, bool defaultValue)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == name)
            {
                return (argument.Value.Value as bool?) ?? defaultValue;
            }
        }

        return defaultValue;
    }

    /// <summary>Gets the generated observable expression for a generated attribute source value.</summary>
    /// <param name="source">The numeric value of the generated source enum.</param>
    /// <returns>The generated observable expression.</returns>
    private static string GetSourceExpression(int source) =>
        source switch
        {
            1 => "serialPort.DataReceived",
            DataReceivedBytesSource => "serialPort.DataReceivedBytes",
            BytesReceivedSource => "serialPort.BytesReceived",
            IsOpenObservableSource => "serialPort.IsOpenObservable",
            _ => "serialPort.Lines",
        };

    /// <summary>Executes source generation for the collected stream declarations.</summary>
    /// <param name="context">The source production context.</param>
    /// <param name="compilation">The consumer compilation.</param>
    /// <param name="standardStreamInfoGroups">The standard-runtime declarations from syntax discovery.</param>
    /// <param name="reactiveStreamInfoGroups">The reactive-runtime declarations from syntax discovery.</param>
    private static void Execute(
        in SourceProductionContext context,
        Compilation compilation,
        in ImmutableArray<ImmutableArray<StreamInfo>> standardStreamInfoGroups,
        in ImmutableArray<ImmutableArray<StreamInfo>> reactiveStreamInfoGroups)
    {
        var serialPortNamespace = ResolveSerialPortNamespace(compilation);
        var streamInfoGroups = standardStreamInfoGroups.AddRange(reactiveStreamInfoGroups);
        foreach (var typeGroup in GroupStreamInfosByType(streamInfoGroups))
        {
            GenerateForType(in context, (INamedTypeSymbol)typeGroup.Key, typeGroup.Value, serialPortNamespace);
        }
    }

    /// <summary>Resolves the namespace root exposed by the referenced SerialPortRx package.</summary>
    /// <param name="compilation">The consumer compilation.</param>
    /// <returns>The namespace root to use in generated references.</returns>
    private static string ResolveSerialPortNamespace(Compilation compilation)
    {
        var reactiveType = compilation.GetTypeByMetadataName("IoT.Driver.Serial.Reactive.ISerialPortRx");
        var standardType = compilation.GetTypeByMetadataName("IoT.Driver.Serial.ISerialPortRx");

        return reactiveType is not null && standardType is null
            ? "IoT.Driver.Serial.Reactive"
            : "IoT.Driver.Serial";
    }

    /// <summary>Groups stream declarations by target type.</summary>
    /// <param name="streamInfoGroups">The stream declaration groups to combine.</param>
    /// <returns>The stream declarations keyed by target type.</returns>
    private static Dictionary<ISymbol, List<StreamInfo>> GroupStreamInfosByType(
        in ImmutableArray<ImmutableArray<StreamInfo>> streamInfoGroups)
    {
        var streamInfosByType = new Dictionary<ISymbol, List<StreamInfo>>(SymbolEqualityComparer.Default);
        foreach (var group in streamInfoGroups)
        {
            foreach (var streamInfo in group)
            {
                AddStreamInfo(streamInfosByType, streamInfo);
            }
        }

        return streamInfosByType;
    }

    /// <summary>Adds one stream declaration to the grouping dictionary.</summary>
    /// <param name="streamInfosByType">The stream grouping dictionary.</param>
    /// <param name="streamInfo">The stream declaration to add.</param>
    private static void AddStreamInfo(Dictionary<ISymbol, List<StreamInfo>> streamInfosByType, StreamInfo streamInfo)
    {
        if (!streamInfosByType.TryGetValue(streamInfo.TargetType, out var streamInfos))
        {
            streamInfos = [];
            streamInfosByType.Add(streamInfo.TargetType, streamInfos);
        }

        streamInfos.Add(streamInfo);
    }

    /// <summary>Generates source for a single target type.</summary>
    /// <param name="context">The source production context.</param>
    /// <param name="targetType">The target class symbol.</param>
    /// <param name="typeStreams">The stream declarations for the target type.</param>
    /// <param name="serialPortNamespace">The namespace root to use for SerialPortRx references.</param>
    private static void GenerateForType(
        in SourceProductionContext context,
        INamedTypeSymbol targetType,
        IReadOnlyList<StreamInfo> typeStreams,
        string serialPortNamespace)
    {
        if (!IsPartial(targetType))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(ClassMustBePartial, GetFirstLocation(targetType), targetType.Name));
            return;
        }

        var streams = GetValidStreams(in context, typeStreams);
        if (streams.Count == 0)
        {
            return;
        }

        var source = GenerateType(targetType, streams, serialPortNamespace);
        context.AddSource($"{SanitizeHintName(targetType.ToDisplayString())}.SerialPortReactiveStreams.g.cs", source);
    }

    /// <summary>Gets valid stream declarations and reports declaration diagnostics.</summary>
    /// <param name="context">The source production context.</param>
    /// <param name="typeStreams">The stream declarations for the target type.</param>
    /// <returns>The stream declarations that can be generated.</returns>
    private static List<StreamInfo> GetValidStreams(
        in SourceProductionContext context,
        IReadOnlyList<StreamInfo> typeStreams)
    {
        var streams = new List<StreamInfo>();
        foreach (var stream in typeStreams)
        {
            if (stream.PropertyType is null)
            {
                continue;
            }

            if (!SyntaxFacts.IsValidIdentifier(stream.PropertyName))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(PropertyNameMustBeIdentifier, stream.Location, stream.PropertyName));
                continue;
            }

            streams.Add(stream);
        }

        return streams;
    }

    /// <summary>Determines whether a target class is declared partial.</summary>
    /// <param name="typeSymbol">The target type symbol.</param>
    /// <returns><see langword="true"/> when any declaring syntax reference is partial.</returns>
    private static bool IsPartial(INamedTypeSymbol typeSymbol)
    {
        foreach (var syntaxReference in typeSymbol.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not ClassDeclarationSyntax classDeclaration)
            {
                continue;
            }

            foreach (var modifier in classDeclaration.Modifiers)
            {
                if (modifier.IsKind(SyntaxKind.PartialKeyword))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Generates the partial class containing reactive stream members.</summary>
    /// <param name="targetType">The target class symbol.</param>
    /// <param name="streams">The stream declarations for the target class.</param>
    /// <param name="serialPortNamespace">The namespace root to use for SerialPortRx references.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateType(
        INamedTypeSymbol targetType,
        IReadOnlyList<StreamInfo> streams,
        string serialPortNamespace)
    {
        var builder = new StringBuilder();
        _ = builder.AppendLine("// <auto-generated />");
        _ = builder.AppendLine("#nullable enable");
        _ = builder.AppendLine();

        var namespaceName = targetType.ContainingNamespace.IsGlobalNamespace
            ? null
            : targetType.ContainingNamespace.ToDisplayString();
        if (namespaceName is not null)
        {
            _ = builder.Append("namespace ").Append(namespaceName).AppendLine(";");
            _ = builder.AppendLine();
        }

        _ = builder.Append("partial class ").AppendLine(targetType.Name);
        _ = builder.AppendLine("{");

        foreach (var stream in streams)
        {
            AppendStreamMembers(builder, stream, serialPortNamespace);
        }

        _ = builder.Append("    public global::System.IDisposable ConnectReactiveSerialPort(global::")
            .Append(serialPortNamespace)
            .AppendLine(".ISerialPortRx serialPort)");
        _ = builder.AppendLine("    {");
        _ = builder.AppendLine("        if (serialPort is null)");
        _ = builder.AppendLine("        {");
        _ = builder.AppendLine("            throw new global::System.ArgumentNullException(nameof(serialPort));");
        _ = builder.AppendLine("        }");
        _ = builder.AppendLine();
        _ = builder.AppendLine(
            "        var disposables = new global::ReactiveUI.Primitives.Disposables.MultipleDisposable();");

        foreach (var stream in streams)
        {
            AppendSubscription(builder, stream, serialPortNamespace);
        }

        _ = builder.AppendLine("        return disposables;");
        _ = builder.AppendLine("    }");
        _ = builder.AppendLine("}");

        return builder.ToString();
    }

    /// <summary>Appends generated fields, properties, and observables for one stream.</summary>
    /// <param name="builder">The source builder.</param>
    /// <param name="stream">The stream declaration.</param>
    /// <param name="serialPortNamespace">The namespace root to use for SerialPortRx references.</param>
    private static void AppendStreamMembers(StringBuilder builder, StreamInfo stream, string serialPortNamespace)
    {
        var propertyType = stream.PropertyType ??
            throw new InvalidOperationException("A validated stream must have a property type.");
        var typeName = propertyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var fieldName = GetSubjectFieldName(stream.PropertyName);

        _ = builder.AppendLine("#if REACTIVE_SHIM");
        _ = builder.Append("    private readonly global::ReactiveUI.Primitives.Reactive.Signals.ReplaySignal<")
            .Append(typeName)
            .Append("> ")
            .Append(fieldName)
            .AppendLine(" = new(0);");
        _ = builder.AppendLine("#else");
        _ = builder.Append("    private readonly global::ReactiveUI.Primitives.Signals.ReplaySignal<")
            .Append(typeName)
            .Append("> ")
            .Append(fieldName)
            .AppendLine(" = new(0);");
        _ = builder.AppendLine("#endif");
        AppendGeneratedValueProperty(builder, stream.PropertyName, typeName, fieldName);
        _ = builder.AppendLine();
        _ = builder.Append("    public global::System.IObservable<")
            .Append(typeName)
            .Append("> ")
            .Append(stream.PropertyName)
            .Append("Observable => ")
            .Append(fieldName)
            .AppendLine(";");
        _ = builder.AppendLine();
        _ = builder.Append("    public global::ReactiveUI.Primitives.Async.IObservableAsync<")
            .Append(typeName)
            .Append("> ")
            .Append(stream.PropertyName)
            .Append("ObservableAsync => global::")
            .Append(serialPortNamespace)
            .Append(".ObservableAsyncBridgeExtensions.ToAsyncObservable(")
            .Append(stream.PropertyName)
            .AppendLine("Observable);");
        _ = builder.AppendLine();
    }

    /// <summary>Appends a generated subscription for one stream.</summary>
    /// <param name="builder">The source builder.</param>
    /// <param name="stream">The stream declaration.</param>
    /// <param name="serialPortNamespace">The namespace root to use for SerialPortRx references.</param>
    private static void AppendSubscription(StringBuilder builder, StreamInfo stream, string serialPortNamespace)
    {
        var propertyType = stream.PropertyType ??
            throw new InvalidOperationException("A validated stream must have a property type.");
        var typeName = propertyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var fieldName = GetSubjectFieldName(stream.PropertyName);

        _ = builder.AppendLine("#if REACTIVE_SHIM");
        AppendSubscriptionCore(
            builder,
            stream,
            typeName,
            fieldName,
            "global::ReactiveUI.Primitives.SubscribeExtensions",
            serialPortNamespace);
        _ = builder.AppendLine("#else");
        AppendSubscriptionCore(
            builder,
            stream,
            typeName,
            fieldName,
            "global::ReactiveUI.Primitives.SubscribeExtensions",
            serialPortNamespace);
        _ = builder.AppendLine("#endif");
    }

    /// <summary>Appends the subscription body shared by lean and reactive builds.</summary>
    /// <param name="builder">The source builder.</param>
    /// <param name="stream">The stream declaration.</param>
    /// <param name="typeName">The generated value type name.</param>
    /// <param name="fieldName">The generated backing signal field name.</param>
    /// <param name="extensionsType">The extension type that owns the generated subscribe call.</param>
    /// <param name="serialPortNamespace">The namespace root to use for SerialPortRx references.</param>
    private static void AppendSubscriptionCore(
        StringBuilder builder,
        StreamInfo stream,
        string typeName,
        string fieldName,
        string extensionsType,
        string serialPortNamespace)
    {
        _ = builder.Append("        disposables.Add(")
            .Append(extensionsType)
            .Append(".Subscribe(")
            .Append(stream.SourceExpression)
            .AppendLine(", __serialPortRxValue =>");
        _ = builder.AppendLine("        {");
        _ = builder.Append("            if (global::")
            .Append(serialPortNamespace)
            .Append(".SourceGeneration.SerialPortReactiveValueConverter.TryConvertMatch<")
            .Append(typeName)
            .Append(">(__serialPortRxValue, ")
            .Append(ToLiteral(stream.Pattern))
            .Append(", ")
            .Append(ToLiteral(stream.GroupName))
            .Append(", ")
            .Append(stream.GroupNumber.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Append(", ")
            .Append(stream.IgnoreCase ? "true" : "false")
            .AppendLine(", out var __serialPortRxConverted))");
        _ = builder.AppendLine("            {");
        _ = builder.Append("                ").Append(stream.PropertyName).AppendLine(" = __serialPortRxConverted;");
        _ = builder.Append("                ").Append(fieldName).AppendLine(".OnNext(__serialPortRxConverted);");
        _ = builder.AppendLine("            }");
        _ = builder.AppendLine("        }));");
    }

    /// <summary>Gets the generated backing signal field name.</summary>
    /// <param name="propertyName">The generated property name.</param>
    /// <returns>The generated backing signal field name.</returns>
    private static string GetSubjectFieldName(string propertyName) =>
        $"__serialPortRx{propertyName}Subject";

    /// <summary>Formats a nullable string as C# source.</summary>
    /// <param name="value">The nullable value to format.</param>
    /// <returns>The generated literal.</returns>
    private static string ToLiteral(string? value) =>
        value is null ? "null" : SymbolDisplay.FormatLiteral(value, quote: true);

    /// <summary>Sanitizes a symbol display string for use as a generated hint name.</summary>
    /// <param name="value">The symbol display string.</param>
    /// <returns>The sanitized hint name.</returns>
    private static string SanitizeHintName(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            _ = builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return builder.ToString();
    }

    /// <summary>Gets the first known source location for a symbol.</summary>
    /// <param name="symbol">The symbol to inspect.</param>
    /// <returns>The first source location when available; otherwise, <see langword="null"/>.</returns>
    private static Location? GetFirstLocation(ISymbol symbol)
    {
        var locations = symbol.Locations;
        return !locations.IsEmpty ? locations[0] : null;
    }
}
