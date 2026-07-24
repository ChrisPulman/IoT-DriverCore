// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using IoT.DriverCore.ModbusRx.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace IoT.DriverCore.ModbusRx.Generators.Tests;

/// <summary>Tests for the reactive stream source generator.</summary>
public class ModbusReactiveStreamGeneratorTests
{
    /// <summary>Verifies generated properties expose a matching observable and binding method.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task GeneratesPropertyObservableAndBindingAsync()
    {
        const string source = """
using System;
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.ModbusRx.Generators;

[ModbusReactiveDevice(ConnectionMember = "MasterStream")]
public partial class BoilerMap
{
    public IObservable<(bool connected, Exception? error, ModbusIpMaster? master)>
        MasterStream { get; set; } = default!;

    [HoldingRegister(0)]
    public partial ushort? Temperature { get; private set; }
}
""";

        var result = RunGenerator(source);
        var generatedSource = ConcatenateGeneratedSources(result.GeneratedTrees);

        await TUnit.Assertions.Assert.That(generatedSource).Contains("TemperatureObservable");
        await TUnit.Assertions.Assert.That(generatedSource).Contains("BindGeneratedModbusStreams");
        await TUnit.Assertions.Assert.That(generatedSource).Contains(
            "global::IoT.DriverCore.ModbusRx.Create.ReadHoldingRegisters(this.MasterStream, 0, 1, 1000");
    }

    /// <summary>Verifies generated code compiles against ModbusRx and ReactiveUI.Primitives.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task GeneratedCodeCompilesAsync()
    {
        const string source = """
using System;
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.ModbusRx.Generators;

namespace Maps;

[ModbusReactiveDevice(ConnectionMember = "MasterStream")]
public partial class BoilerMap
{
    public IObservable<(bool connected, Exception? error, ModbusIpMaster? master)>
        MasterStream { get; set; } = default!;

    [HoldingRegister(0)]
    public partial ushort? Temperature { get; private set; }

    [Coil(3)]
    public partial bool? Enabled { get; private set; }
}
""";

        var result = RunGenerator(source);
        var diagnostics = CollectErrors(result.Compilation.GetDiagnostics());

        await TUnit.Assertions.Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies generated code compiles against the ModbusRx.Reactive shim namespace.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task GeneratedReactiveShimCodeCompilesAsync()
    {
        const string source = """
using System;
using IoT.DriverCore.ModbusRx.Generators;
using IoT.DriverCore.ModbusRx.Reactive.Device;

namespace Maps;

[ModbusReactiveDevice(ConnectionMember = "MasterStream")]
public partial class BoilerMap
{
    public IObservable<(bool connected, Exception? error, ModbusIpMaster? master)>
        MasterStream { get; set; } = default!;

    [HoldingRegister(0)]
    public partial ushort? Temperature { get; private set; }
}
""";

        var result = RunGenerator(source);
        var generatedSource = ConcatenateGeneratedSources(result.GeneratedTrees);
        var diagnostics = CollectErrors(result.Compilation.GetDiagnostics());

        await TUnit.Assertions.Assert.That(generatedSource).Contains(
            "global::IoT.DriverCore.ModbusRx.Reactive.Create.ReadHoldingRegisters(this.MasterStream, 0, 1, 1000");
        await TUnit.Assertions.Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies opt-in logical tag bindings generate the common typed member set.</summary>
    /// <returns>A task representing the test.</returns>
    [TUnit.Core.Test]
    public async Task GeneratesLogicalTagReadWriteAndAsyncObservableMembersAsync()
    {
        const string source = """
using System;
using IoT.DriverCore.Core;
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.ModbusRx.Generators;

[ModbusReactiveDevice(ConnectionMember = "MasterStream", TagClientMember = "TagClient")]
public partial class BoilerMap
{
    public IObservable<(bool connected, Exception? error, ModbusIpMaster? master)>
        MasterStream { get; set; } = default!;

    public ILogicalTagClient TagClient { get; set; } = default!;

    [HoldingRegister(0, TagName = "Boiler.Temperature")]
    public partial ushort? Temperature { get; private set; }
}
""";

        var result = RunGenerator(source);
        var generatedSource = ConcatenateGeneratedSources(result.GeneratedTrees);

        await TUnit.Assertions.Assert.That(generatedSource).Contains("TemperatureObservableAsync");
        await TUnit.Assertions.Assert.That(generatedSource).Contains("ReadTemperatureAsync");
        await TUnit.Assertions.Assert.That(generatedSource).Contains("WriteTemperatureAsync");
        await TUnit.Assertions.Assert.That(generatedSource).Contains("Boiler.Temperature");
    }

    /// <summary>Verifies every supported point kind, scalar type, and serial option emits its intended surface.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task GeneratesAllSupportedSerialPointShapesAsync()
    {
        const string source = """
using System;
using IoT.DriverCore.Core;
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.ModbusRx.Generators;

[ModbusReactiveDevice(
    ConnectionMember = "SerialConnection",
    TagClientMember = "TagClient",
    SlaveAddress = 7,
    DefaultInterval = 12.5,
    MasterKind = ModbusReactiveMasterKind.Serial)]
public partial class CompleteMap
{
    public IObservable<(bool connected, Exception? error, IModbusSerialMaster? master)>
        SerialConnection = default!;

    public ILogicalTagClient TagClient { get; set; } = default!;

    [HoldingRegister(0, Count = 1, SwapWords = false, TagName = " ")]
    public partial ushort Unsigned16 { get; private set; }

    [InputRegister(1, TagName = "Signed16")]
    private partial short? Signed16 { get; set; }

    [HoldingRegister(2, SwapWords = false)]
    protected partial uint Unsigned32 { get; private set; }

    [InputRegister(4)]
    internal partial int? Signed32 { get; private set; }

    [HoldingRegister(6, SwapWords = false)]
    protected internal partial float Float32 { get; private set; }

    [InputRegister(8)]
    private protected partial double? Float64 { get; private set; }

    [Coil(12)]
    public partial bool CoilValue { get; private set; }

    [DiscreteInput(13)]
    public partial bool? DiscreteValue { get; private set; }
}
""";

        var result = RunGenerator(source);
        var generatedSource = ConcatenateGeneratedSources(result.GeneratedTrees);

        await TUnit.Assertions.Assert.That(CollectErrors(result.Compilation.GetDiagnostics())).IsEmpty();
        await AssertSupportedSerialGeneratedSourceAsync(generatedSource);
    }

    /// <summary>Verifies option fallbacks and connection-member shape discovery remain deterministic.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task GeneratesApiRootAndOptionFallbackBranchesAsync()
    {
        const string source = """
using System;
using IoT.DriverCore.ModbusRx.Generators;

namespace Maps;

[ModbusReactiveDevice(
    ConnectionMember = "ReactiveConnection",
    TagClientMember = "",
    MasterKind = ModbusReactiveMasterKind.Ip)]
internal partial class ReactiveMap
{
    internal IObservable<(bool connected, Exception? error, IoT.DriverCore.ModbusRx.Reactive.Device.ModbusIpMaster? master)>
        ReactiveConnection { get; set; } = default!;

    [InputRegister(22, Count = 3, SwapWords = false, TagName = " ")]
    internal partial int Value { get; private set; }
}

[ModbusReactiveDevice(ConnectionMember = "MissingConnection")]
public partial class MissingMemberMap
{
    [DiscreteInput]
    public partial bool Input { get; private set; }
}

[ModbusReactiveDevice(ConnectionMember = "ConnectionEvent")]
public partial class OtherMemberMap
{
    public event Action? ConnectionEvent;

    [Coil(5)]
    public partial bool Output { get; private set; }
}
""";

        var result = RunGenerator(source);
        var generatedSource = ConcatenateGeneratedSources(result.GeneratedTrees);

        await TUnit.Assertions.Assert.That(generatedSource).Contains(
            "global::IoT.DriverCore.ModbusRx.Reactive.Create.ReadInputRegisters(this.ReactiveConnection, 22, 3, 1000");
        await TUnit.Assertions.Assert.That(generatedSource).Contains(
            "global::IoT.DriverCore.ModbusRx.Create.ReadInputs(this.MissingConnection, 0, 1, 1000");
        await TUnit.Assertions.Assert.That(generatedSource).Contains(
            "global::IoT.DriverCore.ModbusRx.Create.ReadCoils(this.ConnectionEvent, 5, 1, 1000");
    }

    /// <summary>Verifies invalid declaration shapes report all generator diagnostics without hiding compiler failures.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task ReportsInvalidClassPropertyAndTypeDiagnosticsAsync()
    {
        const string source = """
using System;
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.ModbusRx.Generators;

[ModbusReactiveDevice]
public class NonPartialMap
{
    public IObservable<(bool connected, Exception? error, ModbusIpMaster? master)>
        MasterStream { get; set; } = default!;

    [HoldingRegister(0)]
    public partial ushort Value { get; private set; }
}

[ModbusReactiveDevice]
public partial class InvalidPointsMap
{
    public IObservable<(bool connected, Exception? error, ModbusIpMaster? master)>
        MasterStream { get; set; } = default!;

    [HoldingRegister(1)]
    public ushort NotPartial { get; private set; }

    [HoldingRegister(2)]
    public partial string UnsupportedRegister { get; private set; }

    [Coil(3)]
    public partial int UnsupportedCoil { get; private set; }
}

public partial class NoDeviceMap
{
    [HoldingRegister(4)]
    public partial ushort Ignored { get; private set; }
}

[ModbusReactiveDevice]
public partial class TaglessMap
{
    public IObservable<(bool connected, Exception? error, ModbusIpMaster? master)>
        MasterStream { get; set; } = default!;

    [Obsolete]
    public partial ushort Ignored { get; private set; }
}
""";

        var result = RunGenerator(source, validateGeneratorDiagnostics: false);
        var diagnosticIdentifiers = new List<string>();
        foreach (var diagnostic in result.Diagnostics)
        {
            diagnosticIdentifiers.Add(diagnostic.Id);
        }

        await TUnit.Assertions.Assert.That(diagnosticIdentifiers).Contains("MBRXGEN001");
        await TUnit.Assertions.Assert.That(diagnosticIdentifiers).Contains("MBRXGEN002");
        await TUnit.Assertions.Assert.That(diagnosticIdentifiers).Contains("MBRXGEN003");
    }

    /// <summary>Verifies the generated serial surface covers every supported read and conversion shape.</summary>
    /// <param name="generatedSource">The generated source to inspect.</param>
    /// <returns>A task representing the asynchronous assertions.</returns>
    private static async Task AssertSupportedSerialGeneratedSourceAsync(string generatedSource)
    {
        await TUnit.Assertions.Assert.That(generatedSource).Contains("ReadHoldingRegisters(this.SerialConnection, (byte)7");
        await TUnit.Assertions.Assert.That(generatedSource).Contains("ReadInputRegisters(this.SerialConnection, (byte)7");
        await TUnit.Assertions.Assert.That(generatedSource).Contains("ReadCoils(this.SerialConnection, (byte)7");
        await TUnit.Assertions.Assert.That(generatedSource).Contains("ReadInputs(this.SerialConnection, (byte)7");
        await TUnit.Assertions.Assert.That(generatedSource).Contains("unchecked((short)data[0])");
        await TUnit.Assertions.Assert.That(generatedSource).Contains("unchecked((int)(((uint)data[1] << 16) | data[0]))");
        await TUnit.Assertions.Assert.That(generatedSource).Contains("ReadSingle(new global::System.ReadOnlySpan<ushort>(data, 0, 2), false)");
        await TUnit.Assertions.Assert.That(generatedSource).Contains("ReadDouble(new global::System.ReadOnlySpan<ushort>(data, 0, 4), true)");
        await TUnit.Assertions.Assert.That(generatedSource).Contains("\"Unsigned16\"");
        await TUnit.Assertions.Assert.That(generatedSource).Contains("\"Signed16\"");
        await TUnit.Assertions.Assert.That(generatedSource).Contains(", 12.5)");
    }

    /// <summary>Runs the reactive stream generator against a source document.</summary>
    /// <param name="source">The source code to compile.</param>
    /// <param name="validateGeneratorDiagnostics">Whether generator errors should fail the harness.</param>
    /// <returns>The generated trees and updated compilation.</returns>
    private static GeneratorRunResult RunGenerator(
        string source,
        bool validateGeneratorDiagnostics = true)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
        var compilation = CSharpCompilation.Create(
            "GeneratorTests",
            [syntaxTree],
            GetReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var generator = new ModbusReactiveStreamGenerator();
        var driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            parseOptions: new CSharpParseOptions(LanguageVersion.Preview));
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);

        var errors = CollectErrors(diagnostics);
        if (validateGeneratorDiagnostics && errors.Count != 0)
        {
            throw new InvalidOperationException(FormatDiagnostics(errors));
        }

        return new GeneratorRunResult(
            driver.GetRunResult().GeneratedTrees,
            outputCompilation,
            diagnostics);
    }

    /// <summary>Gets metadata references used by in-memory generator test compilations.</summary>
    /// <returns>The metadata references needed to compile the generated source.</returns>
    private static IEnumerable<MetadataReference> GetReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        if (!string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
        {
            foreach (var path in trustedPlatformAssemblies.Split(Path.PathSeparator))
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    yield return MetadataReference.CreateFromFile(path);
                }
            }
        }

        yield return MetadataReference.CreateFromFile(typeof(ModbusRx.Device.ModbusIpMaster).Assembly.Location);
        yield return MetadataReference.CreateFromFile(
            typeof(ModbusRx.Reactive.Device.ModbusIpMaster).Assembly.Location);
        yield return MetadataReference.CreateFromFile(typeof(ReactiveUI.Primitives.Signals.Signal).Assembly.Location);
        yield return MetadataReference.CreateFromFile(typeof(IoT.DriverCore.Core.ILogicalTagClient).Assembly.Location);
    }

    /// <summary>Collects error diagnostics from a diagnostic sequence.</summary>
    /// <param name="diagnostics">The diagnostics to inspect.</param>
    /// <returns>The diagnostics with error severity.</returns>
    private static List<Diagnostic> CollectErrors(IEnumerable<Diagnostic> diagnostics)
    {
        var errors = new List<Diagnostic>();
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                errors.Add(diagnostic);
            }
        }

        return errors;
    }

    /// <summary>Formats diagnostics as a multi-line assertion message.</summary>
    /// <param name="diagnostics">The diagnostics to format.</param>
    /// <returns>The formatted diagnostic message.</returns>
    private static string FormatDiagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        var builder = new StringBuilder();
        foreach (var diagnostic in diagnostics)
        {
            if (builder.Length > 0)
            {
                _ = builder.AppendLine();
            }

            _ = builder.Append(diagnostic.ToString());
        }

        return builder.ToString();
    }

    /// <summary>Concatenates generated syntax tree source text.</summary>
    /// <param name="generatedTrees">The generated trees to concatenate.</param>
    /// <returns>The generated source text.</returns>
    private static string ConcatenateGeneratedSources(IReadOnlyList<SyntaxTree> generatedTrees)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < generatedTrees.Count; i++)
        {
            if (i > 0)
            {
                _ = builder.AppendLine();
            }

            _ = builder.Append(generatedTrees[i].GetText().ToString());
        }

        return builder.ToString();
    }

    /// <summary>Stores generator run output for assertions.</summary>
    /// <param name="generatedTrees">The generated syntax trees.</param>
    /// <param name="compilation">The updated compilation.</param>
    /// <param name="diagnostics">The generator diagnostics.</param>
    private sealed class GeneratorRunResult(
        IReadOnlyList<SyntaxTree> generatedTrees,
        Compilation compilation,
        ImmutableArray<Diagnostic> diagnostics)
    {
        /// <summary>Gets the generated syntax trees.</summary>
        public IReadOnlyList<SyntaxTree> GeneratedTrees { get; } = generatedTrees;

        /// <summary>Gets the updated compilation.</summary>
        public Compilation Compilation { get; } = compilation;

        /// <summary>Gets the generator diagnostics.</summary>
        public ImmutableArray<Diagnostic> Diagnostics { get; } = diagnostics;
    }
}
