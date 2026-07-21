// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using CP.IoT.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using S7PlcRx.SourceGeneration;
using S7PlcRx.SourceGenerators;
using ReflectionAssembly = System.Reflection.Assembly;

namespace S7PlcRx.Tests.SourceGenerators;

/// <summary>Tests for S7 tag binding source generation.</summary>
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class S7TagBindingSourceGeneratorTests
{
    /// <summary>Gets the debugger display text.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay
    {
        get => ToString() ?? string.Empty;
    }

    /// <summary>Ensures attributed properties generate binding hooks and grouped byte-array metadata.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Generate_WithDbTags_ShouldEmitBindingHooksAndGroupedByteArrayMetadataAsync()
    {
        System.Diagnostics.Debug.WriteLine(DebuggerDisplay);
        const string source = """
            using S7PlcRx.SourceGeneration;

            namespace Demo;

            [S7PlcBinding]
            public partial class MachineTags
            {
                [S7Tag("DB1.DBD0", PollIntervalMs = 100)]
                public partial float Temperature { get; set; }

                [S7Tag("DB1.DBX4.0", PollIntervalMs = 100)]
                public partial bool Running { get; set; }

                [S7Tag("DB2.DBW0", PollIntervalMs = 250, Direction = S7TagDirection.ReadOnly)]
                public partial short Counter { get; set; }
            }
            """;

        var result = RunGenerator(source);
        var generated = string.Join("\n---\n", result.GeneratedTrees.Select(static tree => tree.GetText().ToString()));

        await TUnit.Assertions.Assert.That(generated)
            .Contains("global::S7PlcRx.Binding.S7TagRuntimeBinding.Bind");
        await TUnit.Assertions.Assert.That(generated)
            .Contains("new global::S7PlcRx.Binding.S7TagDefinition");
        await TUnit.Assertions.Assert.That(generated).Contains("nameof(Temperature)");
        await TUnit.Assertions.Assert.That(generated).Contains("\"DB1.DBD0\"");
        await TUnit.Assertions.Assert.That(generated).Contains("S7TagDirection.ReadOnly");
        await TUnit.Assertions.Assert.That(generated).Contains("__s7SuppressWrites");
        await TUnit.Assertions.Assert.That(
            result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            .IsEmpty();
    }

    /// <summary>Ensures every tag receives common observable and typed operation-result helpers.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenerateWithTagEmitsCommonObservableAndReadWriteSurfaceAsync()
    {
        System.Diagnostics.Debug.WriteLine(DebuggerDisplay);
        const string source = """
            using S7PlcRx.SourceGeneration;

            namespace Demo;

            [S7PlcBinding]
            public partial class MachineTags
            {
                [S7Tag("DB1.DBD0")]
                public partial float Temperature { get; set; }
            }
            """;

        var result = RunGenerator(source);
        var generated = string.Join("\n---\n", result.GeneratedTrees.Select(static tree => tree.GetText().ToString()));

        await TUnit.Assertions.Assert.That(generated).Contains("TemperatureObservable");
        await TUnit.Assertions.Assert.That(generated).Contains("TemperatureObservableAsync");
        await TUnit.Assertions.Assert.That(generated).Contains("ReadTemperatureAsync");
        await TUnit.Assertions.Assert.That(generated).Contains("WriteTemperatureAsync");
        await TUnit.Assertions.Assert.That(generated).Contains("TagOperationResult<float>");
        await TUnit.Assertions.Assert.That(generated).Contains("CreateLogicalTagCatalog");
    }

    /// <summary>Ensures invalid attributed declarations produce stable sequential diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenerateWithInvalidDeclarationsReportsStableDiagnosticsAsync()
    {
        System.Diagnostics.Debug.WriteLine(DebuggerDisplay);
        const string source = """
            using S7PlcRx.SourceGeneration;

            [S7PlcBinding]
            public class MachineTags
            {
                [S7Tag("")]
                public float Temperature { get; set; }
            }
            """;

        var result = RunGenerator(source, validateOutputCompilation: false);
        var identifiers = result.Diagnostics.Select(static diagnostic => diagnostic.Id).ToArray();

        await TUnit.Assertions.Assert.That(identifiers).Contains("S7GEN001");
        await TUnit.Assertions.Assert.That(identifiers).Contains("S7GEN002");
        await TUnit.Assertions.Assert.That(identifiers).Contains("S7GEN003");
    }

    /// <summary>Runs the S7 binding generator against the supplied source.</summary>
    /// <param name="source">The compilation source to process.</param>
    /// <returns>The generator driver results.</returns>
    internal static GeneratorDriverRunResult RunGenerator(string source)
    {
        return RunGenerator(source, validateOutputCompilation: true);
    }

    /// <summary>Runs the S7 binding generator against the supplied source.</summary>
    /// <param name="source">The compilation source to process.</param>
    /// <param name="validateOutputCompilation">Whether generated compilation errors should fail the test.</param>
    /// <returns>The generator driver results.</returns>
    private static GeneratorDriverRunResult RunGenerator(string source, bool validateOutputCompilation)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IDisposable).GetTypeInfo().Assembly.Location),
#if !NETFRAMEWORK
            MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("System.Runtime").Location),
#endif
            MetadataReference.CreateFromFile(ReflectionAssembly.Load("netstandard").Location),
            MetadataReference.CreateFromFile(typeof(LogicalTag).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(S7PlcBindingAttribute).GetTypeInfo().Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            "GeneratorTests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new S7TagBindingSourceGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics);

        var errors = generatorDiagnostics
            .Concat(outputCompilation.GetDiagnostics())
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .ToArray();
        if (validateOutputCompilation && errors.Length > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        return driver.GetRunResult();
    }
}
