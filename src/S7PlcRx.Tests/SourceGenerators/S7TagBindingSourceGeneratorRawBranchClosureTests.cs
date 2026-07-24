// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using IoT.DriverCore.Core;
using IoT.DriverCore.S7PlcRx.SourceGeneration;
using IoT.DriverCore.S7PlcRx.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ReflectionAssembly = System.Reflection.Assembly;

namespace IoT.DriverCore.S7PlcRx.Tests.SourceGenerators;

/// <summary>Closes raw source-generator decision coverage with diagnostic consumer declarations.</summary>
public sealed class S7TagBindingSourceGeneratorRawBranchClosureTests
{
    /// <summary>Verifies original and reactive attributes select their matching generated runtime roots.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Generate_SelectsOriginalAndReactiveAttributeRootsAsync()
    {
        const string originalSource = """
            using IoT.DriverCore.S7PlcRx.SourceGeneration;

            namespace BranchClosure;

            [S7PlcBinding]
            public partial class OriginalTags
            {
                [S7Tag("DB1.DBW0", Direction = S7TagDirection.ReadOnly)]
                public partial short Value { get; set; }
            }
            """;
        const string reactiveSource = """
            using System;

            namespace IoT.DriverCore.S7PlcRx.Reactive.SourceGeneration
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class S7PlcBindingAttribute : Attribute { }

                [AttributeUsage(AttributeTargets.Property)]
                public sealed class S7TagAttribute : Attribute
                {
                    public S7TagAttribute(string address) { }
                    public int PollIntervalMs { get; set; }
                    public S7TagDirection Direction { get; set; }
                    public int ArrayLength { get; set; }
                }

                public enum S7TagDirection { ReadWrite, ReadOnly, WriteOnly }
            }

            namespace IoT.DriverCore.S7PlcRx.Reactive.BranchClosure
            {
                using IoT.DriverCore.S7PlcRx.Reactive.SourceGeneration;

                [S7PlcBinding]
                public partial class ReactiveTags
                {
                    [S7Tag("DB1.DBW0", Direction = S7TagDirection.WriteOnly)]
                    public partial short Value { get; set; }
                }
            }
            """;

        var originalGenerated = GetGeneratedSource(originalSource);
        var reactiveGenerated = GetGeneratedSource(reactiveSource);

        await TUnit.Assertions.Assert.That(originalGenerated)
            .Contains("global::IoT.DriverCore.S7PlcRx.LogicalTags.S7LogicalTagExtensions");
        await TUnit.Assertions.Assert.That(reactiveGenerated)
            .Contains("global::IoT.DriverCore.S7PlcRx.Reactive.LogicalTags.S7LogicalTagExtensions");
        await TUnit.Assertions.Assert.That(reactiveGenerated).Contains("S7TagDirection.WriteOnly");
    }

    /// <summary>Verifies an internal binding in the global namespace preserves its accessibility.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Generate_SupportsInternalBindingInGlobalNamespaceAsync()
    {
        const string source = """
            using IoT.DriverCore.S7PlcRx.SourceGeneration;

            [S7PlcBinding]
            internal partial class GlobalTags
            {
                [S7Tag("DB1.DBW0")]
                public partial short Value { get; set; }
            }
            """;

        var generated = GetGeneratedSource(source);

        await TUnit.Assertions.Assert.That(generated).Contains("internal partial class GlobalTags");
        await TUnit.Assertions.Assert.That(generated).Contains("global::IoT.DriverCore.S7PlcRx.LogicalTags");
    }

    /// <summary>Verifies malformed and non-binding declarations retain their distinct diagnostic paths.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Generate_DistinguishesUnrelatedUntypedAndMalformedTagDeclarationsAsync()
    {
        const string source = """
            using System;

            namespace IoT.DriverCore.S7PlcRx.SourceGeneration
            {
                [AttributeUsage(AttributeTargets.Property)]
                public sealed class S7TagAttribute : Attribute
                {
                    public S7TagAttribute() { }
                    public S7TagAttribute(string address) { }
                    public int PollIntervalMs { get; set; }
                    public S7TagDirection Direction { get; set; }
                    public int ArrayLength { get; set; }
                }
            }

            namespace BranchClosure;

            using IoT.DriverCore.S7PlcRx.SourceGeneration;

            [Obsolete]
            public class UnrelatedAttribute
            {
            }

            [S7PlcBinding]
            public partial class BranchTags
            {
                public int Untagged { get; set; }

                [S7Tag("DB1.DBW0")]
                public int NonPartial { get; set; }

                [S7Tag("   ")]
                public partial int WhitespaceAddress { get; set; }

                [S7Tag]
                public partial int MissingAddressArgument { get; set; }

                [S7Tag(null)]
                public partial int NullAddress { get; set; }

                [S7Tag("DB1.DBW10", Direction = (S7TagDirection)3)]
                public partial int UnknownDirection { get; set; }
            }
            """;

        var result = RunGeneratorWithoutCompilationValidation(source);
        var generated = string.Join(Environment.NewLine, result.GeneratedTrees.Select(static tree => tree.GetText().ToString()));
        var diagnosticIds = result.Diagnostics.Select(static diagnostic => diagnostic.Id).ToArray();
        await TUnit.Assertions.Assert.That(diagnosticIds).Contains("S7GEN002");
        await TUnit.Assertions.Assert.That(diagnosticIds).Contains("S7GEN003");
        await TUnit.Assertions.Assert.That(generated).Contains("nameof(UnknownDirection)");
        await TUnit.Assertions.Assert.That(generated).Contains("S7TagDirection.ReadWrite");
        await TUnit.Assertions.Assert.That(generated).Contains("nameof(MissingAddressArgument)");
        await TUnit.Assertions.Assert.That(generated).Contains("nameof(NullAddress)");
    }

    /// <summary>Runs the incremental generator while intentionally preserving invalid consumer diagnostics.</summary>
    /// <param name="source">The consumer source to supply to the generator.</param>
    /// <returns>The complete generator execution result.</returns>
    private static GeneratorDriverRunResult RunGeneratorWithoutCompilationValidation(string source)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IDisposable).GetTypeInfo().Assembly.Location),
#if NETFRAMEWORK
            MetadataReference.CreateFromFile(typeof(IAsyncEnumerable<>).GetTypeInfo().Assembly.Location),
#else
            MetadataReference.CreateFromFile(ReflectionAssembly.Load("System.Runtime").Location),
#endif
            MetadataReference.CreateFromFile(ReflectionAssembly.Load("netstandard").Location),
            MetadataReference.CreateFromFile(typeof(LogicalTag).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(S7PlcBindingAttribute).GetTypeInfo().Assembly.Location),
        };
        var compilation = CSharpCompilation.Create(
            "RawGeneratorBranchClosureTests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new S7TagBindingSourceGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        return driver.GetRunResult();
    }

    /// <summary>Runs the generator and concatenates all emitted source files.</summary>
    /// <param name="source">The consumer source to supply to the generator.</param>
    /// <returns>The concatenated generated source.</returns>
    private static string GetGeneratedSource(string source) => string.Join(
        Environment.NewLine,
        RunGeneratorWithoutCompilationValidation(source)
            .GeneratedTrees
            .Select(static tree => tree.GetText().ToString()));
}
