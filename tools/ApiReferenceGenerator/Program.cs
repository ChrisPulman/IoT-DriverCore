using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

return await ApiReferenceProgram.RunAsync(args);

internal static class ApiReferenceProgram
{
    internal const BindingFlags DeclaredPublic = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public;

    public static Task<int> RunAsync(string[] args)
    {
        try
        {
            var options = Options.Parse(args);
            Generate(options);
            return Task.FromResult(0);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine(Options.Usage);
            return Task.FromResult(2);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"API reference generation failed: {exception}");
            return Task.FromResult(1);
        }
    }

    private static void Generate(Options options)
    {
        var fullAssemblyPaths = options.Assemblies
            .Select(Path.GetFullPath)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var xmlPaths = fullAssemblyPaths.ToDictionary(
            static assemblyPath => assemblyPath,
            assemblyPath => options.XmlPaths.TryGetValue(assemblyPath, out var xmlPath)
                ? Path.GetFullPath(xmlPath)
                : Path.ChangeExtension(assemblyPath, ".xml"),
            StringComparer.OrdinalIgnoreCase);

        foreach (var assemblyPath in fullAssemblyPaths)
        {
            if (!File.Exists(assemblyPath))
            {
                throw new ArgumentException($"Assembly does not exist: {assemblyPath}");
            }

            if (!File.Exists(xmlPaths[assemblyPath]))
            {
                throw new ArgumentException($"XML documentation does not exist for '{assemblyPath}': {xmlPaths[assemblyPath]}");
            }
        }

        var loadContext = new DocumentationLoadContext(fullAssemblyPaths);
        var reports = fullAssemblyPaths
            .Select(assemblyPath => new AssemblyReport(
                loadContext.LoadFromAssemblyPath(assemblyPath),
                XmlDocumentation.Load(xmlPaths[assemblyPath])))
            .OrderBy(static report => report.Name, StringComparer.Ordinal)
            .ToArray();

        var markdown = MarkdownRenderer.Render(reports, options.ReadmePath is null ? RenderMode.Standalone : RenderMode.ReadmeFragment);
        if (options.OutputPath is not null)
        {
            var outputPath = Path.GetFullPath(options.OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory());
            File.WriteAllText(outputPath, markdown, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Console.WriteLine($"Generated {outputPath}");
        }
        else
        {
            ReadmeFragment.Replace(Path.GetFullPath(options.ReadmePath!), markdown);
            Console.WriteLine($"Updated generated API fragment in {Path.GetFullPath(options.ReadmePath!)}");
        }

        var typeCount = reports.Sum(static report => report.Types.Count);
        var memberCount = reports.Sum(static report => report.Types.Sum(static type => type.Members.Count));
        Console.WriteLine($"Assemblies: {reports.Length}; exported public types: {typeCount}; declared public members: {memberCount}.");
        loadContext.Unload();
    }

    private sealed class DocumentationLoadContext : AssemblyLoadContext
    {
        private readonly Dictionary<string, string> _assemblies;
        private readonly AssemblyDependencyResolver[] _dependencyResolvers;

        public DocumentationLoadContext(IEnumerable<string> rootAssemblies)
            : base("IoTDriverCoreApiReference", isCollectible: true)
        {
            var roots = rootAssemblies.ToArray();
            _assemblies = roots
                .SelectMany(path => Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.dll"))
                .Concat(roots)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .GroupBy(static path => Path.GetFileNameWithoutExtension(path) ?? path, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);
            AddNuGetRuntimeDependencies(roots, _assemblies);
            _dependencyResolvers = roots.Select(static path => new AssemblyDependencyResolver(path)).ToArray();
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name is not null && _assemblies.TryGetValue(assemblyName.Name, out var localPath))
            {
                return LoadFromAssemblyPath(localPath);
            }

            foreach (var resolver in _dependencyResolvers)
            {
                var dependencyPath = resolver.ResolveAssemblyToPath(assemblyName);
                if (dependencyPath is not null)
                {
                    return LoadFromAssemblyPath(dependencyPath);
                }
            }

            return null;
        }

        private static void AddNuGetRuntimeDependencies(
            IEnumerable<string> roots,
            IDictionary<string, string> assemblies)
        {
            var packageRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
            if (string.IsNullOrWhiteSpace(packageRoot))
            {
                packageRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
            }

            foreach (var root in roots)
            {
                var depsPath = Path.ChangeExtension(root, ".deps.json");
                if (!File.Exists(depsPath))
                {
                    continue;
                }

                using var document = JsonDocument.Parse(File.ReadAllText(depsPath));
                var documentRoot = document.RootElement;
                if (!documentRoot.TryGetProperty("targets", out var targets)
                    || !documentRoot.TryGetProperty("libraries", out var libraries))
                {
                    continue;
                }

                foreach (var target in targets.EnumerateObject())
                {
                    foreach (var library in target.Value.EnumerateObject())
                    {
                        if (!libraries.TryGetProperty(library.Name, out var libraryMetadata)
                            || !libraryMetadata.TryGetProperty("path", out var libraryPath)
                            || !library.Value.TryGetProperty("runtime", out var runtime))
                        {
                            continue;
                        }

                        foreach (var asset in runtime.EnumerateObject())
                        {
                            if (!asset.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            var dependencyPath = Path.Combine(packageRoot, libraryPath.GetString()!, asset.Name.Replace('/', Path.DirectorySeparatorChar));
                            if (File.Exists(dependencyPath))
                            {
                                _ = assemblies.TryAdd(Path.GetFileNameWithoutExtension(dependencyPath), dependencyPath);
                            }
                        }
                    }
                }
            }
        }
    }

    private sealed class Options
    {
        public const string Usage = "Usage: dotnet run --project tools/ApiReferenceGenerator -- --assembly <built.dll> [--assembly <built.dll> ...] [--xml <xml-for-preceding-assembly>] (--output <reference.md> | --readme <package-readme.md>)";

        public List<string> Assemblies { get; } = [];

        public Dictionary<string, string> XmlPaths { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string? OutputPath { get; private set; }

        public string? ReadmePath { get; private set; }

        public static Options Parse(string[] args)
        {
            var options = new Options();
            string? currentAssembly = null;
            for (var index = 0; index < args.Length; index++)
            {
                var argument = args[index];
                if (argument is "--help" or "-h")
                {
                    throw new ArgumentException(Usage);
                }

                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException($"Missing value for {argument}.");
                }

                var value = args[++index];
                switch (argument)
                {
                    case "--assembly":
                        currentAssembly = Path.GetFullPath(value);
                        options.Assemblies.Add(currentAssembly);
                        break;
                    case "--xml" when currentAssembly is not null:
                        options.XmlPaths[currentAssembly] = Path.GetFullPath(value);
                        break;
                    case "--xml":
                        throw new ArgumentException("--xml must follow its --assembly.");
                    case "--output":
                        options.OutputPath = value;
                        break;
                    case "--readme":
                        options.ReadmePath = value;
                        break;
                    default:
                        throw new ArgumentException($"Unknown option: {argument}");
                }
            }

            if (options.Assemblies.Count == 0)
            {
                throw new ArgumentException("At least one --assembly is required.");
            }

            if (string.IsNullOrWhiteSpace(options.OutputPath) == string.IsNullOrWhiteSpace(options.ReadmePath))
            {
                throw new ArgumentException("Specify exactly one of --output or --readme.");
            }

            return options;
        }
    }
}

internal static class ReadmeFragment
{
    public const string BeginMarker = "<!-- BEGIN GENERATED PUBLIC API -->";
    public const string EndMarker = "<!-- END GENERATED PUBLIC API -->";

    public static void Replace(string readmePath, string fragment)
    {
        if (!File.Exists(readmePath))
        {
            throw new ArgumentException($"README does not exist: {readmePath}");
        }

        var original = File.ReadAllText(readmePath);
        var begin = original.IndexOf(BeginMarker, StringComparison.Ordinal);
        var end = original.IndexOf(EndMarker, StringComparison.Ordinal);
        if ((begin < 0) != (end < 0))
        {
            throw new ArgumentException($"README markers are incomplete in {readmePath}.");
        }

        if (begin >= 0 && (end < begin || original.IndexOf(BeginMarker, begin + BeginMarker.Length, StringComparison.Ordinal) >= 0 || original.IndexOf(EndMarker, end + EndMarker.Length, StringComparison.Ordinal) >= 0))
        {
            throw new ArgumentException($"README must contain exactly one ordered generated API marker pair: {readmePath}.");
        }

        var newline = original.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var rendered = fragment
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\n", newline, StringComparison.Ordinal);
        var trimmedFragment = rendered.TrimEnd('\r', '\n');
        var replacement = begin < 0
            ? original + (original.EndsWith("\n", StringComparison.Ordinal) ? newline : newline + newline) + trimmedFragment + newline
            : original[..begin] + trimmedFragment + original[(end + EndMarker.Length)..];
        File.WriteAllText(readmePath, replacement, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}

internal sealed class AssemblyReport
{
    public AssemblyReport(Assembly assembly, XmlDocumentation documentation)
    {
        Name = assembly.GetName().Name ?? assembly.FullName ?? "Unknown assembly";
        Path = assembly.Location;
        Types = assembly.GetExportedTypes()
            .OrderBy(TypeFormatting.XmlTypeName, StringComparer.Ordinal)
            .Select(type => new TypeReport(type, documentation))
            .ToArray();
    }

    public string Name { get; }

    public string Path { get; }

    public IReadOnlyList<TypeReport> Types { get; }
}

internal sealed class TypeReport
{
    public TypeReport(Type type, XmlDocumentation documentation)
    {
        Type = type;
        DocumentationId = "T:" + TypeFormatting.XmlTypeName(type);
        Documentation = documentation.Lookup(DocumentationId);
        Members = GetMembers(type)
            .OrderBy(static member => member.DocumentationId, StringComparer.Ordinal)
            .ToArray();

        IEnumerable<MemberReport> GetMembers(Type declaredType)
        {
            foreach (var constructor in declaredType.GetConstructors(ApiReferenceProgram.DeclaredPublic))
            {
                yield return new MemberReport(constructor, documentation);
            }

            foreach (var field in declaredType.GetFields(ApiReferenceProgram.DeclaredPublic)
                .Where(static field => !(field.IsSpecialName && field.Name == "value__")))
            {
                yield return new MemberReport(field, documentation);
            }

            foreach (var property in declaredType.GetProperties(ApiReferenceProgram.DeclaredPublic)
                .Where(static property => property.GetMethod?.IsPublic == true || property.SetMethod?.IsPublic == true))
            {
                yield return new MemberReport(property, documentation);
            }

            foreach (var eventInfo in declaredType.GetEvents(ApiReferenceProgram.DeclaredPublic)
                .Where(static eventInfo => eventInfo.AddMethod?.IsPublic == true || eventInfo.RemoveMethod?.IsPublic == true))
            {
                yield return new MemberReport(eventInfo, documentation);
            }

            foreach (var method in declaredType.GetMethods(ApiReferenceProgram.DeclaredPublic)
                .Where(static method =>
                    (!method.IsSpecialName || method.Name.StartsWith("op_", StringComparison.Ordinal))
                    && !method.Name.StartsWith("<", StringComparison.Ordinal)))
            {
                yield return new MemberReport(method, documentation);
            }
        }
    }

    public Type Type { get; }

    public string DocumentationId { get; }

    public XmlMemberDocumentation Documentation { get; }

    public IReadOnlyList<MemberReport> Members { get; }
}

internal sealed class MemberReport
{
    public MemberReport(MemberInfo member, XmlDocumentation documentation)
    {
        Member = member;
        DocumentationId = TypeFormatting.XmlMemberName(member);
        Signature = TypeFormatting.Signature(member);
        Documentation = documentation.Lookup(DocumentationId);
    }

    public MemberInfo Member { get; }

    public string DocumentationId { get; }

    public string Signature { get; }

    public XmlMemberDocumentation Documentation { get; }
}

internal sealed class XmlDocumentation
{
    private readonly IReadOnlyDictionary<string, XmlMemberDocumentation> _members;

    private XmlDocumentation(IReadOnlyDictionary<string, XmlMemberDocumentation> members) => _members = members;

    public static XmlDocumentation Load(string xmlPath)
    {
        var members = XDocument.Load(xmlPath)
            .Root?
            .Element("members")?
            .Elements("member")
            .Where(static element => element.Attribute("name")?.Value is not null)
            .ToDictionary(
                static element => element.Attribute("name")!.Value,
                static element => XmlMemberDocumentation.FromElement(element),
                StringComparer.Ordinal)
            ?? new Dictionary<string, XmlMemberDocumentation>(StringComparer.Ordinal);
        return new XmlDocumentation(members);
    }

    public XmlMemberDocumentation Lookup(string documentationId) =>
        _members.TryGetValue(documentationId, out var documentation)
            ? documentation
            : XmlMemberDocumentation.Empty;
}

internal sealed class XmlMemberDocumentation
{
    public static readonly XmlMemberDocumentation Empty = new(
        null,
        new Dictionary<string, string>(StringComparer.Ordinal),
        null,
        null,
        isInheritDoc: false);

    private XmlMemberDocumentation(
        string? summary,
        IReadOnlyDictionary<string, string> parameters,
        string? returns,
        string? value,
        bool isInheritDoc)
    {
        Summary = summary;
        Parameters = parameters;
        Returns = returns;
        Value = value;
        IsInheritDoc = isInheritDoc;
    }

    public string? Summary { get; }

    public IReadOnlyDictionary<string, string> Parameters { get; }

    public string? Returns { get; }

    public string? Value { get; }

    public bool IsInheritDoc { get; }

    public static XmlMemberDocumentation FromElement(XElement element)
    {
        var parameters = element.Elements("param")
            .Where(static parameter => parameter.Attribute("name")?.Value is not null)
            .Select(static parameter => new KeyValuePair<string, string>(
                parameter.Attribute("name")!.Value,
                Normalize(parameter.Nodes()) ?? string.Empty))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        return new XmlMemberDocumentation(
            NormalizeElement(element, "summary"),
            parameters,
            NormalizeElement(element, "returns"),
            NormalizeElement(element, "value"),
            element.Element("inheritdoc") is not null);
    }

    private static string? NormalizeElement(XElement element, string name)
    {
        var child = element.Element(name);
        return child is null ? null : Normalize(child.Nodes());
    }

    private static string? Normalize(IEnumerable<XNode> nodes)
    {
        var value = string.Join(" ", nodes
            .Select(RenderNode)
            .SelectMany(static text => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string RenderNode(XNode node) => node switch
    {
        XCData cdata => cdata.Value,
        XText text => text.Value,
        XElement { Name.LocalName: "see" } element => RenderReference(element, "cref"),
        XElement { Name.LocalName: "paramref" } element => RenderReference(element, "name"),
        XElement element => string.Concat(element.Nodes().Select(RenderNode)),
        _ => string.Empty,
    };

    private static string RenderReference(XElement element, string attributeName)
    {
        var value = element.Attribute(attributeName)?.Value;
        return string.IsNullOrWhiteSpace(value) ? string.Concat(element.Nodes().Select(RenderNode)) : $"`{value}`";
    }
}

internal static class TypeFormatting
{
    public static string XmlMemberName(MemberInfo member) => member switch
    {
        Type type => "T:" + XmlTypeName(type),
        ConstructorInfo constructor => "M:" + XmlTypeName(constructor.DeclaringType!) + ".#ctor" + XmlParameters(constructor.GetParameters()),
        MethodInfo method => "M:" + XmlTypeName(method.DeclaringType!) + "." + XmlMethodName(method) + XmlParameters(method.GetParameters()) + XmlConversionReturn(method),
        PropertyInfo property => "P:" + XmlTypeName(property.DeclaringType!) + "." + property.Name + XmlParameters(property.GetIndexParameters()),
        FieldInfo field => "F:" + XmlTypeName(field.DeclaringType!) + "." + field.Name,
        EventInfo eventInfo => "E:" + XmlTypeName(eventInfo.DeclaringType!) + "." + eventInfo.Name,
        _ => throw new ArgumentOutOfRangeException(nameof(member), member, "Unsupported public member kind."),
    };

    public static string XmlTypeName(Type type)
    {
        var fullName = type.FullName ?? type.Name;
        return fullName.Replace('+', '.');
    }

    public static string Signature(MemberInfo member) => member switch
    {
        ConstructorInfo constructor => $"public {TypeDisplayName(constructor.DeclaringType!)}({Parameters(constructor.GetParameters())})",
        MethodInfo method => $"public {(method.IsStatic ? "static " : string.Empty)}{TypeDisplayName(method.ReturnType)} {method.Name}{GenericParameters(method)}({Parameters(method.GetParameters())})",
        PropertyInfo property => $"public {TypeDisplayName(property.PropertyType)} {property.Name}{IndexParameters(property)} {{ {Accessors(property)} }}",
        FieldInfo field => $"public {(field.IsStatic ? "static " : string.Empty)}{(field.IsLiteral ? "const " : string.Empty)}{TypeDisplayName(field.FieldType)} {field.Name}",
        EventInfo eventInfo => $"public event {TypeDisplayName(eventInfo.EventHandlerType!)} {eventInfo.Name}",
        _ => throw new ArgumentOutOfRangeException(nameof(member), member, "Unsupported public member kind."),
    };

    private static string XmlMethodName(MethodInfo method)
    {
        var name = method.Name;
        if (method.IsGenericMethodDefinition)
        {
            name += "``" + method.GetGenericArguments().Length;
        }

        return name;
    }

    private static string XmlConversionReturn(MethodInfo method) =>
        method.Name is "op_Implicit" or "op_Explicit" ? "~" + XmlTypeReference(method.ReturnType) : string.Empty;

    private static string XmlParameters(ParameterInfo[] parameters) =>
        parameters.Length == 0 ? string.Empty : "(" + string.Join(",", parameters.Select(static parameter => XmlTypeReference(parameter.ParameterType))) + ")";

    private static string XmlTypeReference(Type type)
    {
        if (type.IsByRef)
        {
            return XmlTypeReference(type.GetElementType()!) + "@";
        }

        if (type.IsPointer)
        {
            return XmlTypeReference(type.GetElementType()!) + "*";
        }

        if (type.IsArray)
        {
            var rank = type.GetArrayRank();
            return XmlTypeReference(type.GetElementType()!) + (rank == 1 ? "[]" : "[" + string.Join(",", Enumerable.Repeat("0:", rank)) + "]");
        }

        if (type.IsGenericParameter)
        {
            return (type.DeclaringMethod is null ? "`" : "``") + type.GenericParameterPosition;
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            return XmlTypeName(definition) + "{" + string.Join(",", type.GetGenericArguments().Select(XmlTypeReference)) + "}";
        }

        return XmlTypeName(type);
    }

    private static string GenericParameters(MethodInfo method) =>
        method.IsGenericMethodDefinition ? "<" + string.Join(", ", method.GetGenericArguments().Select(static argument => argument.Name)) + ">" : string.Empty;

    private static string IndexParameters(PropertyInfo property)
    {
        var parameters = property.GetIndexParameters();
        return parameters.Length == 0 ? string.Empty : "[" + Parameters(parameters) + "]";
    }

    private static string Accessors(PropertyInfo property)
    {
        var accessors = new List<string>();
        if (property.GetMethod?.IsPublic == true) accessors.Add("get;");
        if (property.SetMethod?.IsPublic == true) accessors.Add("set;");
        return string.Join(" ", accessors);
    }

    private static string Parameters(ParameterInfo[] parameters) =>
        string.Join(", ", parameters.Select(static parameter =>
        {
            var modifier = parameter.IsOut ? "out " : parameter.ParameterType.IsByRef ? "ref " : string.Empty;
            return modifier + TypeDisplayName(parameter.ParameterType) + " " + parameter.Name;
        }));

    public static string TypeDisplayName(Type type)
    {
        if (type.IsByRef) return TypeDisplayName(type.GetElementType()!);
        if (type.IsArray) return TypeDisplayName(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        if (type.IsPointer) return TypeDisplayName(type.GetElementType()!) + "*";
        if (type.IsGenericParameter) return type.Name;

        var aliases = new Dictionary<Type, string>
        {
            [typeof(void)] = "void", [typeof(bool)] = "bool", [typeof(byte)] = "byte", [typeof(sbyte)] = "sbyte",
            [typeof(short)] = "short", [typeof(ushort)] = "ushort", [typeof(int)] = "int", [typeof(uint)] = "uint",
            [typeof(long)] = "long", [typeof(ulong)] = "ulong", [typeof(float)] = "float", [typeof(double)] = "double",
            [typeof(decimal)] = "decimal", [typeof(char)] = "char", [typeof(string)] = "string", [typeof(object)] = "object",
        };
        if (aliases.TryGetValue(type, out var alias)) return alias;
        if (!type.IsGenericType) return (type.FullName ?? type.Name).Replace('+', '.');

        var genericName = (type.GetGenericTypeDefinition().FullName ?? type.Name).Replace('+', '.');
        genericName = genericName[..genericName.IndexOf('`')];
        return genericName + "<" + string.Join(", ", type.GetGenericArguments().Select(TypeDisplayName)) + ">";
    }
}

internal static class MarkdownRenderer
{
    public static string Render(IReadOnlyList<AssemblyReport> reports, RenderMode mode)
    {
        var builder = new StringBuilder();
        if (mode == RenderMode.Standalone)
        {
            builder.AppendLine("# Public API Reference");
            builder.AppendLine();
            builder.AppendLine("Generated from the supplied built assemblies and their XML documentation. Only exported public types and public members declared directly on those types are included; inherited members and non-public implementation details are intentionally omitted.");
        }
        else
        {
            builder.AppendLine(ReadmeFragment.BeginMarker);
            builder.AppendLine();
            builder.AppendLine("## Exhaustive public API reference");
            builder.AppendLine();
            builder.AppendLine("This catalogue is generated from the packaged runtime assemblies and their XML documentation. It includes exported public types and their declared public members; inherited members and non-public implementation details are intentionally omitted.");
        }

        builder.AppendLine();

        foreach (var report in reports)
        {
            builder.AppendLine($"{Heading(mode, standalone: "##", readme: "###")} `{report.Name}`");
            builder.AppendLine();
            if (mode == RenderMode.Standalone)
            {
                builder.AppendLine($"Assembly: `{report.Path}`  ");
            }

            builder.AppendLine($"Exported public types: {report.Types.Count}; declared public members: {report.Types.Sum(static type => type.Members.Count)}.");
            builder.AppendLine();

            foreach (var type in report.Types)
            {
                builder.AppendLine($"{Heading(mode, standalone: "###", readme: "####")} `{type.DocumentationId}`");
                builder.AppendLine();
                builder.AppendLine("```csharp");
                builder.AppendLine($"public {TypeKeyword(type.Type)} {TypeFormatting.XmlTypeName(type.Type)}");
                builder.AppendLine("```");
                AppendDocumentation(builder, type.Documentation, type.Type);

                if (type.Members.Count == 0)
                {
                    continue;
                }

                builder.AppendLine($"{Heading(mode, standalone: "####", readme: "#####")} Declared public members");
                builder.AppendLine();
                foreach (var member in type.Members)
                {
                    builder.AppendLine($"{Heading(mode, standalone: "#####", readme: "######")} `{member.DocumentationId}`");
                    builder.AppendLine();
                    builder.AppendLine("```csharp");
                    builder.AppendLine(member.Signature);
                    builder.AppendLine("```");
                    AppendDocumentation(builder, member.Documentation, member.Member);
                }
            }
        }

        if (mode == RenderMode.ReadmeFragment)
        {
            builder.AppendLine(ReadmeFragment.EndMarker);
        }

        return builder.ToString();
    }

    private static string Heading(RenderMode mode, string standalone, string readme) =>
        mode == RenderMode.Standalone ? standalone : readme;

    private static string TypeKeyword(Type type) =>
        type.IsInterface ? "interface" : type.IsEnum ? "enum" : typeof(MulticastDelegate).IsAssignableFrom(type.BaseType) ? "delegate" : type.IsValueType ? "struct" : "class";

    private static void AppendDocumentation(
        StringBuilder builder,
        XmlMemberDocumentation documentation,
        MemberInfo declaration)
    {
        if (documentation.Summary is not null)
        {
            builder.AppendLine(documentation.Summary);
            builder.AppendLine();
        }
        else if (documentation.IsInheritDoc)
        {
            builder.AppendLine("Inherits XML documentation from its implemented or overridden member.");
            builder.AppendLine();
        }
        else
        {
            builder.AppendLine(FallbackSummary(declaration));
            builder.AppendLine();
        }

        var parameters = declaration switch
        {
            MethodBase method => method.GetParameters(),
            PropertyInfo property => property.GetIndexParameters(),
            _ => [],
        };
        var wroteDetails = false;
        foreach (var parameter in parameters)
        {
            var parameterName = parameter.Name ?? $"arg{parameter.Position}";
            var description = documentation.Parameters.TryGetValue(parameterName, out var documented)
                ? documented
                : $"The `{parameterName}` value.";
            builder.AppendLine($"- Parameter `{parameterName}`: {description}");
            wroteDetails = true;
        }

        if (documentation.Returns is not null)
        {
            builder.AppendLine($"- Returns: {documentation.Returns}");
            wroteDetails = true;
        }
        else if (declaration is MethodInfo { ReturnType: var returnType } && returnType != typeof(void))
        {
            builder.AppendLine($"- Returns: A `{TypeFormatting.TypeDisplayName(returnType)}` result.");
            wroteDetails = true;
        }

        if (documentation.Value is not null)
        {
            builder.AppendLine($"- Value: {documentation.Value}");
            wroteDetails = true;
        }
        else if (declaration is PropertyInfo property)
        {
            builder.AppendLine($"- Value: The `{property.Name}` value.");
            wroteDetails = true;
        }

        if (wroteDetails)
        {
            builder.AppendLine();
        }
    }

    private static string FallbackSummary(MemberInfo declaration) => declaration switch
    {
        Type type => $"Public {TypeKeyword(type)} `{TypeFormatting.XmlTypeName(type)}`.",
        ConstructorInfo constructor => $"Initializes a new instance of `{TypeFormatting.XmlTypeName(constructor.DeclaringType!)}`.",
        PropertyInfo property when property.GetMethod?.IsPublic == true && property.SetMethod?.IsPublic == true =>
            $"Gets or sets `{property.Name}`.",
        PropertyInfo property when property.GetMethod?.IsPublic == true => $"Gets `{property.Name}`.",
        PropertyInfo property => $"Sets `{property.Name}`.",
        FieldInfo field when field.DeclaringType?.IsEnum == true => $"Represents the `{field.Name}` enum value.",
        FieldInfo field => $"Exposes the public `{field.Name}` field.",
        EventInfo eventInfo => $"Occurs when `{eventInfo.Name}` is raised.",
        MethodInfo { Name: "Deconstruct" } => "Deconstructs the value into its component values.",
        MethodInfo { Name: "Equals" } => "Determines whether the supplied value is equal to the current value.",
        MethodInfo { Name: "GetHashCode" } => "Returns the hash code for the current value.",
        MethodInfo { Name: "ToString" } => "Returns a string representation of the current value.",
        MethodInfo { Name: "op_Equality" } => "Determines whether the two supplied values are equal.",
        MethodInfo { Name: "op_Inequality" } => "Determines whether the two supplied values are not equal.",
        MethodInfo { Name: "op_Implicit" } => "Converts the supplied value using the implicit conversion operator.",
        MethodInfo { Name: "op_Explicit" } => "Converts the supplied value using the explicit conversion operator.",
        MethodInfo method => $"Executes the `{method.Name}` operation.",
        _ => "Public API declaration.",
    };
}

internal enum RenderMode
{
    Standalone,
    ReadmeFragment,
}
