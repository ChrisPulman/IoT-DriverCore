// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.IO.Ports.Tests;

/// <summary>Tests for the serial reactive stream source generator.</summary>
public sealed class SerialPortReactiveStreamGeneratorTests
{
    /// <summary>Verifies generated serial stream properties and observables compile.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Generator_CreatesPropertyAndObservableStreamsAsync()
    {
        const string source = """
using CP.IO.Ports.SourceGeneration;

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
using CP.IO.Ports.SourceGeneration;

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
            .Contains("ConnectReactiveSerialPort(global::CP.IO.Ports.Reactive.ISerialPortRx serialPort)");
        await Assert
            .That(generatedSource)
            .Contains("global::CP.IO.Ports.Reactive.ObservableAsyncBridgeExtensions.ToAsyncObservable");
        await Assert
            .That(generatedSource)
            .Contains("global::CP.IO.Ports.Reactive.SourceGeneration.SerialPortReactiveValueConverter");
    }

    /// <summary>
    /// Verifies generated marker types stay compilation-local when an annotated library and
    /// its consumer both run the generator.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Generator_EmitsInternalMarkersForLibraryAndConsumerCompilationsAsync()
    {
        const string librarySource = """
using CP.IO.Ports.SourceGeneration;

namespace GeneratedLibrary;

[SerialPortReactiveStream("Temperature", typeof(double), @"^TEMP:(?<value>-?\d+(\.\d+)?)$")]
public partial class DeviceState
{
}
""";
        const string consumerSource = """
using CP.IO.Ports.SourceGeneration;

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

        await using var libraryImage = new MemoryStream();
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

        var markerSource = generatedConsumerCompilation.SyntaxTrees
            .Single(static tree => tree.FilePath.EndsWith(
                "SerialPortReactiveStreamAttribute.g.cs",
                StringComparison.Ordinal))
            .ToString();
        var consumerErrors = GetErrors(generatedConsumerCompilation, consumerGeneratorDiagnostics);
        var markerConflicts = generatedConsumerCompilation.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Id == "CS0436")
            .ToList();

        await Assert.That(markerSource).Contains("internal enum SerialPortReactiveSource");
        await Assert.That(markerSource).Contains("internal sealed class SerialPortReactiveStreamAttribute");
        await Assert.That(markerConflicts).IsEmpty();
        await Assert.That(consumerErrors).IsEmpty();
    }

    /// <summary>Returns all error diagnostics from a generated compilation.</summary>
    /// <param name="compilation">The generated compilation to inspect.</param>
    /// <param name="generatorDiagnostics">The diagnostics reported by the generator driver.</param>
    /// <returns>The error diagnostics reported by compilation or generator.</returns>
    private static List<Diagnostic> GetErrors(
        Compilation compilation,
        System.Collections.Immutable.ImmutableArray<Diagnostic> generatorDiagnostics) =>
        compilation.GetDiagnostics()
            .Concat(generatorDiagnostics)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToList();

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
            if (string.Equals(fileName, "SerialPortRx.dll", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "SerialPortRx.Reactive.dll", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AddReferenceIfMissing(references, path);
        }

        AddReferenceIfMissing(references, serialPortAssemblyPath);
        AddReferenceIfMissing(references, typeof(Signal).Assembly.Location);
        AddReferenceIfMissing(references, typeof(IObservableAsync<>).Assembly.Location);

        return CSharpCompilation.Create(
            "SerialPortRx.GeneratedTests",
            [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references,
            new CSharpCompilationOptions(
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
        var testProjectDirectory = binDirectory.Parent ?? throw new DirectoryNotFoundException(binDirectory.FullName);
        var srcDirectory = testProjectDirectory.Parent ??
            throw new DirectoryNotFoundException(testProjectDirectory.FullName);

        return Path.Combine(
            srcDirectory.FullName,
            "SerialPortRx.Reactive",
            "bin",
            configurationDirectory.Name,
            targetFrameworkDirectory.Name,
            "SerialPortRx.Reactive.dll");
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
}
