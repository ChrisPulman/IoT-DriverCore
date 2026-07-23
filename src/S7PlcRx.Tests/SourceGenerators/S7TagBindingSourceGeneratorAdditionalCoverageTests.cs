// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.S7PlcRx.Tests.SourceGenerators;

/// <summary>Exercises generator metadata normalisation and deterministic source emission.</summary>
public sealed class S7TagBindingSourceGeneratorAdditionalCoverageTests
{
    /// <summary>Verifies named attribute arguments are normalised and emitted as stable binding definitions.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Generate_NormalisesNamedMetadataAndEscapesAddressesAsync()
    {
        const string source = """
            using IoT.DriverCore.S7PlcRx.SourceGeneration;

            namespace GeneratorCoverage;

            [S7PlcBinding]
            public partial class NormalisedTags
            {
                [S7Tag("DB1.\\\"Quoted\\\"", PollIntervalMs = -5, ArrayLength = 0, Direction = S7TagDirection.WriteOnly)]
                public partial int Value { get; set; }
            }
            """;

        var generated = GetGeneratedSource(source);

        await TUnit.Assertions.Assert.That(generated).Contains("DB1.\\\\");
        await TUnit.Assertions.Assert.That(generated).Contains("Quoted");
        await TUnit.Assertions.Assert.That(generated).Contains("0, global::IoT.DriverCore.S7PlcRx.Binding.S7TagDirection.WriteOnly, 1)");
        await TUnit.Assertions.Assert.That(generated).Contains("TagOperationResult<int>");
    }

    /// <summary>Verifies nullable and array types retain their fully-qualified declarations in generated members.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Generate_PreservesNullableAndArrayPropertyTypesAsync()
    {
        const string source = """
            using IoT.DriverCore.S7PlcRx.SourceGeneration;

            namespace GeneratorCoverage;

            [S7PlcBinding]
            public partial class TypedTags
            {
                [S7Tag("DB1.DBD0")]
                public partial int? OptionalCount { get; set; }

                [S7Tag("DB1.DBB4", ArrayLength = 4)]
                public partial byte[] Payload { get; set; }
            }
            """;

        var generated = GetGeneratedSource(source);

        await TUnit.Assertions.Assert.That(generated).Contains("global::System.Nullable<int> __s7OptionalCount");
        await TUnit.Assertions.Assert.That(generated).Contains("byte[] __s7Payload");
        await TUnit.Assertions.Assert.That(generated).Contains("S7TagValueObservable<byte[]>");
        await TUnit.Assertions.Assert.That(generated).Contains("\"DB1.DBB4\", typeof(byte[]), 100");
    }

    /// <summary>Verifies generated source is reproducible and keeps the source member ordering.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Generate_IsDeterministicAndKeepsPropertyOrderAsync()
    {
        const string source = """
            using IoT.DriverCore.S7PlcRx.SourceGeneration;

            namespace GeneratorCoverage;

            [S7PlcBinding]
            public partial class OrderedTags
            {
                [S7Tag("DB1.DBW0")]
                public partial short First { get; set; }

                [S7Tag("DB1.DBW2")]
                public partial short Second { get; set; }
            }
            """;

        var first = GetGeneratedSource(source);
        var second = GetGeneratedSource(source);

        await TUnit.Assertions.Assert.That(first).IsEqualTo(second);
        await TUnit.Assertions.Assert.That(first.IndexOf("nameof(First)", StringComparison.Ordinal)).IsLessThan(
            first.IndexOf("nameof(Second)", StringComparison.Ordinal));
    }

    /// <summary>Verifies non-binding declarations leave the compilation untouched.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Generate_IgnoresUnattributedAndTaglessDeclarationsAsync()
    {
        const string source = """
            using IoT.DriverCore.S7PlcRx.SourceGeneration;

            namespace GeneratorCoverage;

            public class PlainTags
            {
                public int Value { get; set; }
            }

            [S7PlcBinding]
            public partial class EmptyBinding
            {
                public int Value { get; set; }
            }
            """;

        var result = S7TagBindingSourceGeneratorTests.RunGenerator(source);

        await TUnit.Assertions.Assert.That(result.GeneratedTrees).IsEmpty();
        await TUnit.Assertions.Assert.That(result.Diagnostics).IsEmpty();
    }

    /// <summary>Runs the existing generator harness and concatenates its generated source.</summary>
    /// <param name="source">The source supplied to the generator.</param>
    /// <returns>The generated source text.</returns>
    private static string GetGeneratedSource(string source) => string.Join(
        Environment.NewLine,
        S7TagBindingSourceGeneratorTests.RunGenerator(source)
            .GeneratedTrees
            .Select(static tree => tree.GetText().ToString()));
}
