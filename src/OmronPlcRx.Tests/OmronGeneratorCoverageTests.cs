// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using IoT.DriverCore.OmronPlcRx.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Core;

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>Exercises the Omron incremental source generator through Roslyn's public driver.</summary>
public sealed class OmronGeneratorCoverageTests
{
    /// <summary>Gets the expected diagnostic rule count.</summary>
    private const int DiagnosticRuleCount = 6;

    /// <summary>Gets the expected number of generated core targets.</summary>
    private const int GeneratedCoreTargetCount = 3;

    /// <summary>Gets attribute and protocol stubs used by core generator compilations.</summary>
    private const string CoreStubs = """
        namespace IoT.DriverCore.OmronPlcRx
        {
            [System.AttributeUsage(
                System.AttributeTargets.Field | System.AttributeTargets.Property,
                AllowMultiple = false,
                Inherited = false)]
            public sealed class PlcTagAttribute : System.Attribute
            {
                public PlcTagAttribute() { }
                public PlcTagAttribute(string address) => Address = address;
                public string Address { get; } = "";
                public string? TagName { get; set; }
                public bool Register { get; set; } = true;
                public bool Observe { get; set; } = true;
                public bool Writable { get; set; }
            }

            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class PlcTagBindingAttribute : System.Attribute { }

            public interface IOmronPlcRx { }
        }

        namespace IoT.DriverCore.OmronPlcRx.Core.Types
        {
            public readonly struct Bcd16 { }
            public readonly struct BcdU16 { }
            public readonly struct Bcd32 { }
            public readonly struct BcdU32 { }
        }
        """;

    /// <summary>Gets attribute and protocol stubs used by reactive generator compilations.</summary>
    private const string ReactiveStubs = """
        namespace IoT.DriverCore.OmronPlcRx.Reactive
        {
            [System.AttributeUsage(
                System.AttributeTargets.Field | System.AttributeTargets.Property,
                AllowMultiple = false,
                Inherited = false)]
            public sealed class PlcTagAttribute : System.Attribute
            {
                public PlcTagAttribute(string address) => Address = address;
                public string Address { get; }
                public string? TagName { get; set; }
                public bool Register { get; set; } = true;
                public bool Observe { get; set; } = true;
                public bool Writable { get; set; }
            }

            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class PlcTagBindingAttribute : System.Attribute { }

            public interface IOmronPlcRx { }
        }

        namespace IoT.DriverCore.OmronPlcRx.Reactive.Core.Types
        {
            public readonly struct Bcd16 { }
            public readonly struct BcdU16 { }
            public readonly struct Bcd32 { }
            public readonly struct BcdU32 { }
        }
        """;

    /// <summary>Gets valid core declarations covering every supported data type and emission option.</summary>
    private const string CoreValidSource = """

        namespace Coverage.Core
        {
            using IoT.DriverCore.OmronPlcRx;
            using IoT.DriverCore.OmronPlcRx.Core.Types;

            [PlcTagBinding]
            public partial class Machine
            {
                [PlcTag("D0.0")]
                private bool _enabled;

                [PlcTag("D1")]
                private byte _byteValue;

                [PlcTag("D2")]
                private short _shortValue;

                [PlcTag("D3")]
                private ushort _unsignedShortValue;

                [PlcTag("D4")]
                private int _integerValue;

                [PlcTag("D6")]
                private uint _unsignedIntegerValue;

                [PlcTag("D8")]
                private float _singleValue;

                [PlcTag("D10")]
                private double _doubleValue;

                [PlcTag(
                    "D14[8]",
                    TagName = "Quoted\\\"Tag",
                    Register = false,
                    Observe = false,
                    Writable = true)]
                private string _requiredText = "";

                [PlcTag("D22")]
                private Bcd16 _bcd16;

                [PlcTag("D23")]
                private BcdU16 _bcdU16;

                [PlcTag("D24")]
                private Bcd32 _bcd32;

                [PlcTag("D26")]
                private BcdU32 _bcdU32;

                [PlcTag("D28[8]", Writable = true)]
                public string? OptionalText { get; set; }

                [PlcTag("D36[8]")]
                public string RequiredProperty { get; set; } = "";
            }

            internal partial class InternalMachine
            {
                [PlcTag("D40", Observe = false)]
                private int _value;
            }

            public partial class Outer
            {
                public partial class Nested
                {
                    [PlcTag("D42")]
                    private int _nestedValue;
                }
            }
        }
        """;

    /// <summary>Gets reactive and unrelated attributed declarations.</summary>
    private const string ReactiveValidSource = """

        namespace IoT.DriverCore.OmronPlcRx.Reactive.Subsystem
        {
            using IoT.DriverCore.OmronPlcRx.Reactive;

            public sealed class OtherAttribute : System.Attribute { }

            [PlcTagBinding]
            public partial class ReactiveMachine
            {
                [Other]
                private int _ignored;

                [Other]
                public int IgnoredProperty { get; set; }

                [PlcTag("D0", Writable = true)]
                public int Value { get; set; }
            }
        }
        """;

    /// <summary>Gets invalid declarations covering every generator diagnostic.</summary>
    private static readonly string[] InvalidSources =
    [
        """
        namespace Cases
        {
            using IoT.DriverCore.OmronPlcRx;
            public class NotPartial
            {
                [PlcTag("D0")]
                private int _value;
            }
        }
        """,
        """
        namespace Cases
        {
            using IoT.DriverCore.OmronPlcRx;
            public partial class EmptyAddress
            {
                [PlcTag]
                private int _value;
            }
        }
        """,
        """
        namespace Cases
        {
            using IoT.DriverCore.OmronPlcRx;
            public partial class Unsupported
            {
                [PlcTag("D0")]
                private decimal _value;
            }
        }
        """,
        """
        namespace Cases
        {
            using IoT.DriverCore.OmronPlcRx;
            public partial class Collision
            {
                [PlcTag("D0")]
                private int _value;
                public int Value { get; set; }
            }
        }
        """,
        """
        namespace Cases
        {
            using IoT.DriverCore.OmronPlcRx;
            public partial class MissingBinding
            {
                [PlcTag("D0")]
                public int Value { get; set; }
            }
        }
        """,
        """
        namespace Cases
        {
            using IoT.DriverCore.OmronPlcRx;
            [PlcTagBinding]
            public partial class InvalidProperties
            {
                [PlcTag("")]
                public int Empty { get; set; }

                [PlcTag("D0")]
                public decimal Unsupported { get; set; }

                [PlcTag("D1")]
                public int InitOnly { get; init; }

                [PlcTag("D2")]
                public int GetterOnly { get; }
            }
        }
        """,
    ];

    /// <summary>Verifies valid fields and properties emit the full core binding surface.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Generator_EmitsCoreBindingsForEverySupportedTypeAsync()
    {
        var result = RunGenerator(string.Concat(CoreStubs, CoreValidSource));

        var generated = string.Join(
            Environment.NewLine,
            result.GeneratedSources.Select(static source => source.SourceText.ToString()));

        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(result.GeneratedSources.Length).IsEqualTo(GeneratedCoreTargetCount);
        await Assert.That(generated).Contains("public partial class Machine");
        await Assert.That(generated).Contains("internal partial class InternalMachine");
        await Assert.That(generated).Contains("public partial class Nested");
        await Assert.That(generated).Contains("ReadOptionalTextAsync");
        await Assert.That(generated).Contains("WriteRequiredTextAsync");
        await Assert.That(generated).Contains("Catalog.Upsert");
        await Assert.That(generated).Contains("returned null");
        await Assert.That(generated).Contains("Quoted\\\\\\\"Tag");
    }

    /// <summary>Verifies all public diagnostics are reported for invalid declarations.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Generator_ReportsEveryValidationDiagnosticAsync()
    {
        var diagnosticIds = new List<string>();
        foreach (var source in InvalidSources)
        {
            var result = RunGenerator(string.Concat(CoreStubs, source));
            diagnosticIds.AddRange(result.Diagnostics.Select(static diagnostic => diagnostic.Id));
        }

        await Assert.That(diagnosticIds.Distinct().Count()).IsEqualTo(DiagnosticRuleCount);
        await Assert.That(diagnosticIds).Contains("OPRX001");
        await Assert.That(diagnosticIds).Contains("OPRX002");
        await Assert.That(diagnosticIds).Contains("OPRX003");
        await Assert.That(diagnosticIds).Contains("OPRX004");
        await Assert.That(diagnosticIds).Contains("OPRX005");
        await Assert.That(diagnosticIds).Contains("OPRX006");
    }

    /// <summary>Verifies reactive targets and unrelated attributed syntax follow their dedicated paths.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Generator_HandlesReactiveAndUnrelatedCandidatesAsync()
    {
        var result = RunGenerator(string.Concat(ReactiveStubs, ReactiveValidSource));

        var generated = result.GeneratedSources.Single().SourceText.ToString();
        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(generated).Contains("Reactive.IOmronPlcRx");
        await Assert.That(generated).Contains("Reactive.OmronLogicalTagClient");
        await Assert.That(generated).Contains("Serial.Reactive.ObservableAsyncBridgeExtensions");
        await Assert.That(generated).DoesNotContain("IgnoredPropertyObservable");
    }

    /// <summary>Verifies a compilation without either supported attribute exits without output.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Generator_NoSupportedAttributesProducesNoOutputAsync()
    {
        var result = RunGenerator(
            """
            namespace Coverage;

            public partial class Empty
            {
                private int _value;
            }
            """);

        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(result.GeneratedSources).IsEmpty();
    }

    /// <summary>Runs the generator against one deterministic in-memory compilation.</summary>
    /// <param name="source">Compilation source.</param>
    /// <returns>The generator result.</returns>
    private static GeneratorRunResult RunGenerator(string source)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var references = GetReferences();
        var compilation = CSharpCompilation.Create(
            "OmronGeneratorCoverage",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new PlcTagSourceGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        return driver.GetRunResult().Results.Single();
    }

    /// <summary>Gets portable metadata references for the in-memory compilation.</summary>
    /// <returns>The compilation references.</returns>
    private static ImmutableArray<MetadataReference> GetReferences()
    {
        var locations = new[]
        {
            typeof(object).Assembly.Location,
            typeof(Enumerable).Assembly.Location,
            typeof(Attribute).Assembly.Location,
        };
        return locations
            .Where(static location => !string.IsNullOrWhiteSpace(location))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static location => (MetadataReference)MetadataReference.CreateFromFile(location))
            .ToImmutableArray();
    }
}
