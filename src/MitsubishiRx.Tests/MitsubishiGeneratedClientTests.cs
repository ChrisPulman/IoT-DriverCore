// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET10_0_OR_GREATER
using System.IO.Compression;
#endif
using Microsoft.CodeAnalysis;

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive.Tests;
#else

namespace IoT.DriverCore.MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiGeneratedClientTests type.</summary>
internal sealed partial class MitsubishiGeneratedClientTests
{
    /// <summary>Stores the common generated-client schema.</summary>
    private const string GeneratedClientSchema = """
    {
      "tags": [
        {
          "name": "MotorSpeed",
          "address": "D100",
          "dataType": "Float",
          "description": "Main spindle speed"
        },
        {
          "name": "Mode",
          "address": "D101",
          "dataType": "UInt16"
        }
      ],
      "groups": [
        {
          "name": "Line1",
          "tagNames": ["MotorSpeed", "Mode"]
        }
      ]
    }
    """;

    /// <summary>Stores the PackagePackGate field.</summary>
    private static readonly SemaphoreSlim PackagePackGate = new(1, 1);

    /// <summary>Stores the _cachedPackedPackagePath field.</summary>
    private static string? _cachedPackedPackagePath;

    /// <summary>Executes the IncrementalGeneratorEmitsTypedTagAndGroupClientSurface operation.</summary>
    /// <returns>The IncrementalGeneratorEmitsTypedTagAndGroupClientSurface operation result.</returns>
    [Test]
    internal async Task IncrementalGeneratorEmitsTypedTagAndGroupClientSurfaceAsync()
    {
        var generated = RunGenerator(CreateSchemaMarkerSource(GeneratedClientSchema));

        await AssertGeneratedClientAndTagSurfaceAsync(generated);
        await AssertGeneratedGroupReadWriteSurfaceAsync(generated);
        await AssertGeneratedGroupObservationSurfaceAsync(generated);
    }

    /// <summary>Executes the IncrementalGeneratorOutputCompilesAndSupportsGeneratedExtensionUsage operation.</summary>
    /// <returns>The IncrementalGeneratorOutputCompilesAndSupportsGeneratedExtensionUsage operation result.</returns>
    [Test]
    internal async Task IncrementalGeneratorOutputCompilesAndSupportsGeneratedExtensionUsageAsync()
    {
        var result = RunGeneratorCompilation(CreateGeneratedExtensionUsageSource(GeneratedClientSchema));
        ThrowIfGeneratorErrors(result.Diagnostics);

        await Assert.That(
            result.Generated.Contains(
                "public static GeneratedMitsubishiTagClient Generated(this " +
                "global::IoT.DriverCore.MitsubishiRx.MitsubishiRx owner) => new(owner);"))
            .IsTrue();
    }

    /// <summary>Verifies property declarations receive common typed logical-tag helpers.</summary>
    /// <returns>The IncrementalGeneratorEmitsPropertyBindingHelpers operation result.</returns>
    [Test]
    internal async Task IncrementalGeneratorEmitsPropertyBindingHelpersAsync()
    {
        const string source = """
        using IoT.DriverCore.MitsubishiRx;

        namespace Consumer;

        [MitsubishiTagClient(nameof(LogicalTags))]
        internal sealed partial class Dashboard
        {
            public MitsubishiLogicalTagClient LogicalTags { get; init; } = null!;

            [MitsubishiTag("Line1.MotorSpeed")]
            public float MotorSpeed { get; set; }
        }
        """;

        var result = RunGeneratorCompilation(source);
        var errors = result.Diagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        if (errors.Length > 0)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, errors.Select(static diagnostic => diagnostic.ToString())));
        }

        await Assert.That(result.Generated.Contains("partial class Dashboard", StringComparison.Ordinal)).IsTrue();
        await Assert.That(
            result.Generated.Contains(
                "MotorSpeedObservable => LogicalTags.Observe(" +
                "new global::IoT.DriverCore.Core.LogicalTagKey<float>(\"Line1.MotorSpeed\"))",
                StringComparison.Ordinal))
            .IsTrue();
        await Assert.That(
            result.Generated.Contains(
                "MotorSpeedObservableAsync => LogicalTags.ObserveAsync(" +
                "new global::IoT.DriverCore.Core.LogicalTagKey<float>(\"Line1.MotorSpeed\")",
                StringComparison.Ordinal))
            .IsTrue();
        await Assert.That(result.Generated.Contains("ReadMotorSpeedAsync", StringComparison.Ordinal)).IsTrue();
        await Assert.That(result.Generated.Contains("WriteMotorSpeedAsync", StringComparison.Ordinal)).IsTrue();
        await Assert.That(result.Generated.Contains("TagOperationResult<float>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Verifies consumer project package integration.</summary>
    /// <returns>
    /// The
    /// ConsumerProjectReferencingPackedMitsubishiRxPackageBuildsGeneratedClientSurfaceAutomatically operation result.
    /// </returns>
    [Test]
    internal async Task
        ConsumerProjectReferencingPackedMitsubishiRxPackageBuildsGeneratedClientSurfaceAutomaticallyAsync()
    {
        string packagePath = await PackMitsubishiRxPackageAsync();
        string version = Path.GetFileNameWithoutExtension(packagePath)["MitsubishiRx.".Length..];
        string tempDirectory = CreateTemporaryDirectory();

        try
        {
            string consumerDirectory = Path.Combine(tempDirectory, "consumer");
            string packageCacheDirectory = Path.Combine(tempDirectory, "packages");
            _ = Directory.CreateDirectory(consumerDirectory);
            _ = Directory.CreateDirectory(packageCacheDirectory);
            string consumerProjectPath = Path.Combine(consumerDirectory, "Consumer.csproj");
            string programPath = Path.Combine(consumerDirectory, "Program.cs");

            await WriteConsumerProjectFilesAsync(consumerProjectPath, programPath, version);

            var restore = await RunDotNetAsync(
                "restore",
                consumerProjectPath,
                consumerDirectory,
                $"/p:RestorePackagesPath={packageCacheDirectory}",
                $"/p:RestoreAdditionalProjectSources={Path.GetDirectoryName(packagePath)}");
            if (restore.ExitCode != 0)
            {
                throw new InvalidOperationException(restore.Output);
            }

            var build = await RunDotNetAsync(
                "build",
                consumerProjectPath,
                consumerDirectory,
                "--no-restore",
                $"/p:RestorePackagesPath={packageCacheDirectory}");
            if (build.ExitCode != 0)
            {
                throw new InvalidOperationException(build.Output);
            }

            await Assert.That(build.Output.Contains("Build succeeded.")).IsTrue();
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

#if NET10_0_OR_GREATER
    /// <summary>Executes the MitsubishiRxPackageShouldContainGeneratorAnalyzerAsset operation.</summary>
    /// <returns>The MitsubishiRxPackageShouldContainGeneratorAnalyzerAsset operation result.</returns>
    [Test]
    internal async Task MitsubishiRxPackageShouldContainGeneratorAnalyzerAssetAsync()
    {
        string packagePath = await PackMitsubishiRxPackageAsync();

        await using var package = await ZipFile.OpenReadAsync(packagePath, CancellationToken.None);
        bool hasAnalyzer = package.Entries.Any(
            static entry => entry.FullName.EndsWith(
                "analyzers/dotnet/cs/MitsubishiRx.Generators.dll",
                StringComparison.OrdinalIgnoreCase));
        await Assert.That(hasAnalyzer).IsTrue();
    }
#endif
}
