// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.S7PlcRx.Tests.SourceGenerators;

/// <summary>Exercises generated members in a real consumer compilation.</summary>
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class GeneratedBindingIntegrationTests
{
    /// <summary>Gets the debugger display text.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay
    {
        get => ToString() ?? string.Empty;
    }

    /// <summary>Verifies a decorated consumer compiles with the generated observable and catalog surface.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GeneratedBindingCompilesAndPublishesValuesAsync()
    {
        System.Diagnostics.Debug.WriteLine(DebuggerDisplay);
        const string source = """
            using IoT.DriverCore.S7PlcRx.SourceGeneration;

            namespace Consumer;

            [S7PlcBinding]
            public partial class GeneratedMachineTags
            {
                [S7Tag("DB1.DBD0", PollIntervalMs = 100)]
                public partial float Temperature { get; set; }
            }
            """;

        var result = S7TagBindingSourceGeneratorTests.RunGenerator(source);
        var generated = string.Join(
            Environment.NewLine,
            result.GeneratedTrees.Select(static tree => tree.GetText().ToString()));

        await TUnit.Assertions.Assert.That(generated).Contains("TemperatureObservable");
        await TUnit.Assertions.Assert.That(generated).Contains("TemperatureObservableAsync");
        await TUnit.Assertions.Assert.That(generated).Contains("CreateLogicalTagCatalog");
    }

    /// <summary>Verifies the common catalog does not replace the optimized runtime polling registration.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BindRetainsOptimizedRangePollingAsync()
    {
        System.Diagnostics.Debug.WriteLine(DebuggerDisplay);
        const string source = """
            using IoT.DriverCore.S7PlcRx.SourceGeneration;

            namespace Consumer;

            [S7PlcBinding]
            public partial class GeneratedMachineTags
            {
                [S7Tag("DB1.DBD0", PollIntervalMs = 100)]
                public partial float Temperature { get; set; }

                [S7Tag("DB1.DBX4.0", PollIntervalMs = 100)]
                public partial bool Running { get; set; }
            }
            """;

        var result = S7TagBindingSourceGeneratorTests.RunGenerator(source);
        var generated = string.Join(
            Environment.NewLine,
            result.GeneratedTrees.Select(static tree => tree.GetText().ToString()));

        await TUnit.Assertions.Assert.That(generated).Contains("S7TagRuntimeBinding.Bind");
        await TUnit.Assertions.Assert.That(generated).Contains("S7TagBindingSession");
        await TUnit.Assertions.Assert.That(generated).Contains("ReadTemperatureAsync");
        await TUnit.Assertions.Assert.That(generated).Contains("WriteRunningAsync");
    }
}
