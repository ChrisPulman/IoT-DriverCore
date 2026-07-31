// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.Driver.Serial.Tests;

/// <summary>Tests for the serial reactive stream source generator.</summary>
public sealed class SerialPortReactiveStreamGeneratorTests
{
    /// <summary>The reactive SerialPortRx assembly file name.</summary>
    private const string ReactiveSerialPortAssemblyFileName = "SerialPortRx.Reactive.dll";

    /// <summary>The standard SerialPortRx assembly file name.</summary>
    private const string SerialPortAssemblyFileName = "SerialPortRx.dll";

    /// <summary>An unsupported source enum value used to exercise the generator fallback.</summary>
    private const int UnsupportedSourceValue = 99;

    /// <summary>Verifies generated serial stream properties and observables compile.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Generator_CreatesPropertyAndObservableStreamsAsync()
    {
        const string source = """
using IoT.Driver.Serial.SourceGeneration;

namespace GeneratedTests;

[SerialPortReactiveStream("Temperature", typeof(double), @"^TEMP:(?<value>-?\d+(\.\d+)?)$")]
[SerialPortReactiveStream("IsConnected", typeof(bool), Source = SerialPortReactiveSource.IsOpen)]
public partial class DeviceState
{
}
""";

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var compilation = CreateCompilation(source, parseOptions);
        var generator = new SerialPortReactiveStreamGenerator();
        var driver = CSharpGeneratorDriver.Create([generator.AsSourceGenerator()], parseOptions: parseOptions);

        _ = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var errors = new List<Diagnostic>();
        foreach (var diagnostic in outputCompilation.GetDiagnostics())
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                errors.Add(diagnostic);
            }
        }

        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                errors.Add(diagnostic);
            }
        }

        SyntaxTree? generatedTree = null;
        foreach (var tree in outputCompilation.SyntaxTrees)
        {
            if (tree.FilePath.EndsWith(
                    "GeneratedTests_DeviceState.SerialPortReactiveStreams.g.cs",
                    StringComparison.Ordinal))
            {
                generatedTree = tree;
                break;
            }
        }

        if (generatedTree is null)
        {
            throw new InvalidOperationException("The expected generated syntax tree was not produced.");
        }

        var generatedSource = generatedTree.ToString();

        await Assert.That(errors).IsEmpty();
        await Assert.That(generatedSource).Contains("public double Temperature");
        await Assert.That(generatedSource).Contains("TemperatureObservable");
        await Assert.That(generatedSource).Contains("TemperatureObservableAsync");
        await Assert.That(generatedSource).Contains("public bool IsConnected");
    }

    /// <summary>Verifies generated streams bind when only the reactive package is referenced.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Generator_UsesReactiveSerialPortNamespaceForReactiveOnlyConsumersAsync()
    {
        const string source = """
using IoT.Driver.Serial.Reactive.SourceGeneration;

namespace GeneratedTests;

[SerialPortReactiveStream("Temperature", typeof(double), @"^TEMP:(?<value>-?\d+(\.\d+)?)$")]
public partial class DeviceState
{
}
""";

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var compilation = CreateReactiveCompilation(source, parseOptions);
        var generator = new SerialPortReactiveStreamGenerator();
        var driver = CSharpGeneratorDriver.Create([generator.AsSourceGenerator()], parseOptions: parseOptions);

        _ = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var errors = new List<Diagnostic>();
        foreach (var diagnostic in outputCompilation.GetDiagnostics())
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                errors.Add(diagnostic);
            }
        }

        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                errors.Add(diagnostic);
            }
        }

        SyntaxTree? generatedTree = null;
        foreach (var tree in outputCompilation.SyntaxTrees)
        {
            if (tree.FilePath.EndsWith(
                    "GeneratedTests_DeviceState.SerialPortReactiveStreams.g.cs",
                    StringComparison.Ordinal))
            {
                generatedTree = tree;
                break;
            }
        }

        await Assert.That(errors).IsEmpty();
        await Assert.That(generatedTree).IsNotNull();

        var generatedSource = generatedTree?.ToString() ??
            throw new InvalidOperationException("The expected generated source was not produced.");

        await Assert
            .That(generatedSource)
            .Contains("ConnectReactiveSerialPort(global::IoT.Driver.Serial.Reactive.ISerialPortRx serialPort)");
        await Assert
            .That(generatedSource)
            .Contains("global::IoT.Driver.Serial.Reactive.ObservableAsyncBridgeExtensions.ToAsyncObservable");
        await Assert
            .That(generatedSource)
            .Contains("global::IoT.Driver.Serial.Reactive.SourceGeneration.SerialPortReactiveValueConverter");
    }

    /// <summary>
    /// Verifies runtime-owned marker types can be used by an annotated library and consumer
    /// without the generator emitting duplicate marker declarations.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Generator_UsesRuntimeOwnedMarkersForLibraryAndConsumerCompilationsAsync()
    {
        const string librarySource = """
using IoT.Driver.Serial.SourceGeneration;

namespace GeneratedLibrary;

[SerialPortReactiveStream("Temperature", typeof(double), @"^TEMP:(?<value>-?\d+(\.\d+)?)$")]
public partial class DeviceState
{
}
""";
        const string consumerSource = """
using IoT.Driver.Serial.SourceGeneration;

namespace GeneratedConsumer;

[SerialPortReactiveStream("IsConnected", typeof(bool), Source = SerialPortReactiveSource.IsOpen)]
public sealed partial class Consumer
{
}
""";

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var generator = new SerialPortReactiveStreamGenerator();
        var libraryDriver = CSharpGeneratorDriver.Create([generator.AsSourceGenerator()], parseOptions: parseOptions);
        var libraryCompilation = CreateCompilation(librarySource, parseOptions);

        _ = libraryDriver.RunGeneratorsAndUpdateCompilation(
            libraryCompilation,
            out var generatedLibraryCompilation,
            out var libraryGeneratorDiagnostics);

        var libraryErrors = GetErrors(generatedLibraryCompilation, libraryGeneratorDiagnostics);
        await Assert.That(libraryErrors).IsEmpty();

#if NET8_0_OR_GREATER
        await using var libraryImage = new MemoryStream();
#else
        using var libraryImage = new MemoryStream();
#endif
        var libraryEmit = generatedLibraryCompilation.Emit(libraryImage);
        await Assert.That(libraryEmit.Success).IsTrue();

        var libraryReference = MetadataReference.CreateFromImage(
            System.Collections.Immutable.ImmutableArray.CreateRange(libraryImage.ToArray()));
        var consumerCompilation = CreateCompilation(consumerSource, parseOptions).AddReferences(libraryReference);
        var consumerDriver = CSharpGeneratorDriver.Create([generator.AsSourceGenerator()], parseOptions: parseOptions);

        _ = consumerDriver.RunGeneratorsAndUpdateCompilation(
            consumerCompilation,
            out var generatedConsumerCompilation,
            out var consumerGeneratorDiagnostics);

        var markerAudit = GetMarkerOwnershipAudit(generatedConsumerCompilation, consumerGeneratorDiagnostics);

        await Assert.That(markerAudit.EmittedMarkerSource).IsFalse();
        await Assert.That(markerAudit.Conflicts).IsEmpty();
        await Assert.That(markerAudit.Errors).IsEmpty();
    }

    /// <summary>Verifies invalid targets report both public generator diagnostics.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task Generator_InvalidTargets_ReportDiagnosticsAsync()
    {
        const string source = """
using IoT.Driver.Serial.SourceGeneration;

[SerialPortReactiveStream("Value", typeof(int))]
public class NonPartialDevice
{
}

[SerialPortReactiveStream("", typeof(int))]
public partial class InvalidPropertyDevice
{
}
""";
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var compilation = CreateCompilation(source, parseOptions);
        var driver = CSharpGeneratorDriver.Create(
            [new SerialPortReactiveStreamGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);

        var result = driver.RunGenerators(compilation).GetRunResult();
        var diagnosticIds = new List<string>();
        foreach (var diagnostic in result.Diagnostics)
        {
            diagnosticIds.Add(diagnostic.Id);
        }

        await Assert.That(diagnosticIds).Contains("SPRX001");
        await Assert.That(diagnosticIds).Contains("SPRX002");
        var generatedTreeCount = 0;
        foreach (var generatedTree in result.GeneratedTrees)
        {
            if (generatedTree.FilePath.EndsWith("SerialPortReactiveStreams.g.cs", StringComparison.Ordinal))
            {
                generatedTreeCount++;
            }
        }

        await Assert.That(generatedTreeCount).IsEqualTo(0);
    }

    /// <summary>Verifies every source and matching option is emitted for a global-namespace target.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task Generator_AllSourcesAndMatchOptions_AreEmittedAsync()
    {
        const string source = """
using IoT.Driver.Serial.SourceGeneration;

[SerialPortReactiveStream("Character", typeof(char), Source = SerialPortReactiveSource.DataReceived)]
[SerialPortReactiveStream("RawByte", typeof(byte), Source = SerialPortReactiveSource.DataReceivedBytes)]
[SerialPortReactiveStream("ReadByte", typeof(int), Source = SerialPortReactiveSource.BytesReceived)]
[SerialPortReactiveStream("OpenState", typeof(bool), Source = SerialPortReactiveSource.IsOpen)]
[SerialPortReactiveStream("Line", typeof(string), "^ok:(.*)$", GroupName = null, GroupNumber = 2, IgnoreCase = true)]
public partial class RootDevice
{
}
""";
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var compilation = CreateCompilation(source, parseOptions);
        var driver = CSharpGeneratorDriver.Create(
            [new SerialPortReactiveStreamGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);

        _ = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);
        var errors = GetErrors(outputCompilation, diagnostics);
        string? generatedSource = null;
        foreach (var syntaxTree in outputCompilation.SyntaxTrees)
        {
            if (syntaxTree.FilePath.EndsWith("RootDevice.SerialPortReactiveStreams.g.cs", StringComparison.Ordinal))
            {
                generatedSource = syntaxTree.ToString();
                break;
            }
        }

        if (generatedSource is null)
        {
            throw new InvalidOperationException("The generated root-device source was not found.");
        }

        await Assert.That(errors).IsEmpty();
        await Assert.That(generatedSource).Contains("serialPort.DataReceived");
        await Assert.That(generatedSource).Contains("serialPort.DataReceivedBytes");
        await Assert.That(generatedSource).Contains("serialPort.BytesReceived");
        await Assert.That(generatedSource).Contains("serialPort.IsOpenObservable");
        await Assert.That(generatedSource).Contains("\"^ok:(.*)$\", null, 2, true");
        await Assert.That(generatedSource).DoesNotContain("namespace ;");
    }

    /// <summary>Verifies fallback stream sources and defensive malformed-attribute handling remain deterministic.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task Generator_FallbackAndDefensiveAttributePaths_AreHandledAsync()
    {
        const string fallbackSource = """
using IoT.Driver.Serial.SourceGeneration;

[SerialPortReactiveStream(null, typeof(int), Source = (SerialPortReactiveSource)99)]
public partial class FallbackDevice
{
}
""";
        const string malformedSource = """
using System;

[Obsolete("legacy")]
public class MalformedDevice
{
}
""";
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var fallbackCompilation = CreateCompilation(fallbackSource, parseOptions);
        var driver = CSharpGeneratorDriver.Create(
            [new SerialPortReactiveStreamGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);

        var runResult = driver.RunGenerators(fallbackCompilation).GetRunResult();
        var malformedCompilation = CreateCompilation(malformedSource, parseOptions);
        var malformedType = GetRequiredType(malformedCompilation, "MalformedDevice");
        var malformedAttribute = GetSingleAttribute(malformedType);
        var tryCreate = GetPrivateGeneratorMethod("TryCreateStreamInfo");
        var sourceExpression = GetPrivateGeneratorMethod("GetSourceExpression");
        object?[] arguments = [malformedType, malformedAttribute, CancellationToken.None, null];
        var malformedResult = (bool)(tryCreate.Invoke(null, arguments) ?? false);
        var fallbackExpression = (string)(sourceExpression.Invoke(null, [UnsupportedSourceValue]) ?? string.Empty);

        await Assert.That(ContainsDiagnostic(runResult.Diagnostics, "SPRX002")).IsTrue();
        await Assert.That(malformedResult).IsFalse();
        await Assert.That(fallbackExpression).IsEqualTo("serialPort.Lines");
    }

    /// <summary>Verifies private generation guards reject invalid internal stream state without emitting bad code.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task Generator_InternalNullStreamGuards_ThrowDeterministicallyAsync()
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var compilation = CreateCompilation("public partial class GeneratedTarget { }", parseOptions);
        var target = compilation.GetTypeByMetadataName("GeneratedTarget") ??
            throw new InvalidOperationException("Generated target was not created.");
        var invalidStream = CreateInvalidStream(target);
        var appendMembers = GetPrivateGeneratorMethod("AppendStreamMembers");
        var appendSubscription = GetPrivateGeneratorMethod("AppendSubscription");
        var getFirstLocation = GetPrivateGeneratorMethod("GetFirstLocation");
        var builder = new StringBuilder();
        var locationlessSymbol = compilation.CreateErrorTypeSymbol(compilation.GlobalNamespace, nameof(CreateInvalidStream), 0);

        await Assert.That(() => appendMembers.Invoke(null, [builder, invalidStream, "IoT.Driver.Serial"]))
            .Throws<TargetInvocationException>();
        await Assert.That(() => appendSubscription.Invoke(null, [builder, invalidStream, "IoT.Driver.Serial"]))
            .Throws<TargetInvocationException>();
        await Assert.That(getFirstLocation.Invoke(null, [locationlessSymbol])).IsNull();
    }

    /// <summary>Returns all error diagnostics from a generated compilation.</summary>
    /// <param name="compilation">The generated compilation to inspect.</param>
    /// <param name="generatorDiagnostics">The diagnostics reported by the generator driver.</param>
    /// <returns>The error diagnostics reported by compilation or generator.</returns>
    private static List<Diagnostic> GetErrors(
        Compilation compilation,
        System.Collections.Immutable.ImmutableArray<Diagnostic> generatorDiagnostics)
    {
        var errors = new List<Diagnostic>();
        foreach (var diagnostic in compilation.GetDiagnostics())
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                errors.Add(diagnostic);
            }
        }

        foreach (var diagnostic in generatorDiagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                errors.Add(diagnostic);
            }
        }

        return errors;
    }

    /// <summary>Audits generated consumer output for incorrectly emitted marker types and conflicts.</summary>
    /// <param name="compilation">The generated consumer compilation.</param>
    /// <param name="generatorDiagnostics">The diagnostics reported by the generator driver.</param>
    /// <returns>The marker-ownership audit result.</returns>
    private static (bool EmittedMarkerSource, List<Diagnostic> Conflicts, List<Diagnostic> Errors) GetMarkerOwnershipAudit(
        Compilation compilation,
        System.Collections.Immutable.ImmutableArray<Diagnostic> generatorDiagnostics)
    {
        var emittedMarkerSource = false;
        var conflicts = new List<Diagnostic>();
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            emittedMarkerSource |= syntaxTree.FilePath.EndsWith(
                "SerialPortReactiveStreamAttribute.g.cs",
                StringComparison.Ordinal);
        }

        foreach (var diagnostic in compilation.GetDiagnostics())
        {
            if (diagnostic.Id == "CS0436")
            {
                conflicts.Add(diagnostic);
            }
        }

        return (emittedMarkerSource, conflicts, GetErrors(compilation, generatorDiagnostics));
    }

    /// <summary>Gets a type by its metadata name or throws when the test setup is invalid.</summary>
    /// <param name="compilation">The compilation containing the type.</param>
    /// <param name="metadataName">The type metadata name.</param>
    /// <returns>The requested type.</returns>
    private static INamedTypeSymbol GetRequiredType(Compilation compilation, string metadataName)
    {
        return compilation.GetTypeByMetadataName(metadataName) ??
            throw new InvalidOperationException($"Test type '{metadataName}' was not created.");
    }

    /// <summary>Gets the single attribute attached to a test symbol.</summary>
    /// <param name="symbol">The symbol whose attribute is required.</param>
    /// <returns>The symbol's single attribute.</returns>
    private static AttributeData GetSingleAttribute(INamedTypeSymbol symbol)
    {
        var attributes = symbol.GetAttributes();
        if (attributes.Length != 1)
        {
            throw new InvalidOperationException("The malformed test type must have exactly one attribute.");
        }

        return attributes[0];
    }

    /// <summary>Determines whether the supplied diagnostics contain an identifier.</summary>
    /// <param name="diagnostics">The diagnostics to inspect.</param>
    /// <param name="id">The expected diagnostic identifier.</param>
    /// <returns><see langword="true"/> when the diagnostic was reported.</returns>
    private static bool ContainsDiagnostic(System.Collections.Immutable.ImmutableArray<Diagnostic> diagnostics, string id)
    {
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Id == id)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Creates an internal stream declaration with no property type for defensive guard coverage.</summary>
    /// <param name="target">The target type symbol.</param>
    /// <returns>The generator's private stream-info instance.</returns>
    private static object CreateInvalidStream(INamedTypeSymbol target)
    {
        var generatorType = typeof(SerialPortReactiveStreamGenerator);
        var identityType = generatorType.GetNestedType("StreamIdentity", BindingFlags.NonPublic) ??
            throw new MissingMemberException(generatorType.FullName, "StreamIdentity");
        var optionsType = generatorType.GetNestedType("StreamMatchOptions", BindingFlags.NonPublic) ??
            throw new MissingMemberException(generatorType.FullName, "StreamMatchOptions");
        var streamType = generatorType.GetNestedType("StreamInfo", BindingFlags.NonPublic) ??
            throw new MissingMemberException(generatorType.FullName, "StreamInfo");
        var identity = Activator.CreateInstance(identityType, target, "Invalid", null, null) ??
            throw new InvalidOperationException("Stream identity was not created.");
        var options = Activator.CreateInstance(optionsType, null, "serialPort.Lines", "value", 1, false) ??
            throw new InvalidOperationException("Stream match options were not created.");

        return Activator.CreateInstance(streamType, identity, options) ??
            throw new InvalidOperationException("Invalid stream was not created.");
    }

    /// <summary>Gets a named private static generator method.</summary>
    /// <param name="methodName">The method name.</param>
    /// <returns>The located method.</returns>
    private static MethodInfo GetPrivateGeneratorMethod(string methodName) =>
        typeof(SerialPortReactiveStreamGenerator).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static) ??
        throw new MissingMethodException(nameof(SerialPortReactiveStreamGenerator), methodName);

    /// <summary>Creates a C# compilation for source generator tests.</summary>
    /// <param name="source">The source text to compile.</param>
    /// <param name="parseOptions">The parse options to use.</param>
    /// <returns>The created compilation.</returns>
    private static CSharpCompilation CreateCompilation(string source, CSharpParseOptions parseOptions) =>
        CreateCompilation(source, parseOptions, typeof(SerialPortRx).Assembly.Location);

    /// <summary>Creates a C# compilation for source generator tests.</summary>
    /// <param name="source">The source text to compile.</param>
    /// <param name="parseOptions">The parse options to use.</param>
    /// <param name="serialPortAssemblyPath">The SerialPortRx assembly path to reference.</param>
    /// <returns>The created compilation.</returns>
    private static CSharpCompilation CreateCompilation(
        string source,
        CSharpParseOptions parseOptions,
        string serialPortAssemblyPath)
    {
        var references = new List<MetadataReference>();
        var trustedPlatformAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?
            .Split(Path.PathSeparator) ?? [];

        foreach (var path in trustedPlatformAssemblies)
        {
            var fileName = Path.GetFileName(path);
            if (string.Equals(fileName, SerialPortAssemblyFileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, ReactiveSerialPortAssemblyFileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AddReferenceIfMissing(references, path);
        }

        if (references.Count == 0)
        {
            AddLoadedRuntimeReferences(references);
        }

        AddReferenceIfMissing(references, serialPortAssemblyPath);
        AddReferenceIfMissing(references, typeof(Signal).Assembly.Location);
        AddReferenceIfMissing(references, typeof(ISignal<>).Assembly.Location);
        AddReferenceIfMissing(references, typeof(MultipleDisposable).Assembly.Location);
        AddReferenceIfMissing(references, typeof(IObservableAsync<>).Assembly.Location);

        return CSharpCompilation.Create(
            "SerialPortRx.GeneratedTests",
            [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references,
            new(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    /// <summary>Creates a C# compilation that references only the reactive SerialPortRx package output.</summary>
    /// <param name="source">The source text to compile.</param>
    /// <param name="parseOptions">The parse options to use.</param>
    /// <returns>The created compilation.</returns>
    private static CSharpCompilation CreateReactiveCompilation(string source, CSharpParseOptions parseOptions) =>
        CreateCompilation(source, parseOptions, GetReactiveSerialPortAssemblyPath());

    /// <summary>Gets the built reactive SerialPortRx assembly for the current test target framework.</summary>
    /// <returns>The reactive SerialPortRx assembly path.</returns>
    private static string GetReactiveSerialPortAssemblyPath()
    {
        var targetFrameworkDirectory = new DirectoryInfo(
            AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var configurationDirectory = targetFrameworkDirectory.Parent ??
            throw new DirectoryNotFoundException(AppContext.BaseDirectory);
        var binDirectory = configurationDirectory.Parent ??
            throw new DirectoryNotFoundException(configurationDirectory.FullName);
        var isolatedArtifactsPath = Path.Combine(
            binDirectory.FullName,
            "SerialPortRx.Reactive",
            targetFrameworkDirectory.Name,
            ReactiveSerialPortAssemblyFileName);
        if (File.Exists(isolatedArtifactsPath))
        {
            return isolatedArtifactsPath;
        }

        var testProjectDirectory = binDirectory.Parent ?? throw new DirectoryNotFoundException(binDirectory.FullName);
        var srcDirectory = testProjectDirectory.Parent ??
            throw new DirectoryNotFoundException(testProjectDirectory.FullName);

        return Path.Combine(
            srcDirectory.FullName,
            "SerialPortRx.Reactive",
            "bin",
            configurationDirectory.Name,
            targetFrameworkDirectory.Name,
            ReactiveSerialPortAssemblyFileName);
    }

    /// <summary>Adds a metadata reference if it has not already been added.</summary>
    /// <param name="references">The reference collection.</param>
    /// <param name="path">The assembly path.</param>
    private static void AddReferenceIfMissing(List<MetadataReference> references, string path)
    {
        foreach (var reference in references)
        {
            if (string.Equals(reference.Display, path, StringComparison.Ordinal))
            {
                return;
            }
        }

        references.Add(MetadataReference.CreateFromFile(path));
    }

    /// <summary>Adds file-backed runtime assemblies when the target framework does not provide trusted-platform paths.</summary>
    /// <param name="references">The reference collection to populate.</param>
    private static void AddLoadedRuntimeReferences(List<MetadataReference> references)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic ||
                string.IsNullOrWhiteSpace(assembly.Location) ||
                IsSerialPortAssembly(Path.GetFileName(assembly.Location)))
            {
                continue;
            }

            AddReferenceIfMissing(references, assembly.Location);
        }
    }

    /// <summary>Determines whether a runtime file is either SerialPortRx package identity.</summary>
    /// <param name="fileName">The assembly file name.</param>
    /// <returns><see langword="true"/> when the file is a SerialPortRx package assembly.</returns>
    private static bool IsSerialPortAssembly(string fileName) =>
        string.Equals(fileName, SerialPortAssemblyFileName, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(fileName, ReactiveSerialPortAssemblyFileName, StringComparison.OrdinalIgnoreCase);
}
