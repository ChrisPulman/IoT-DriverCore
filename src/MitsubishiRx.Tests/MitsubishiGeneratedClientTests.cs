// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET10_0_OR_GREATER
using System.IO.Compression;
#endif

#if REACTIVE_SHIM

namespace IoT.Driver.MitsubishiRx.Reactive.Tests;
#else

namespace IoT.Driver.MitsubishiRx.Tests;
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

    /// <summary>Version shared by every package created for the isolated consumer-package test feed.</summary>
    private const string ConsumerPackageVersion = "1.0.0";

    /// <summary>Stores the runtime package identity.</summary>
    private const string RuntimePackageId = "IoT-Driver.MitsubishiRx";

    /// <summary>Stores the standalone generator package identity.</summary>
    private const string GeneratorPackageId = "IoT-Driver.MitsubishiRx.Generators";

    /// <summary>Stores the PackagePackGate field.</summary>
    private static readonly SemaphoreSlim PackagePackGate = new(1, 1);

    /// <summary>Stores the _cachedPackedPackagePath field.</summary>
    private static string? _cachedPackedPackagePath;

    /// <summary>Stores the _cachedPackedGeneratorPackagePath field.</summary>
    private static string? _cachedPackedGeneratorPackagePath;

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

    /// <summary>Verifies runtime packages, rather than the generator, own the marker attribute definitions.</summary>
    /// <returns>A task that completes when the assertions finish.</returns>
    [Test]
    internal async Task IncrementalGeneratorUsesRuntimeMarkerAttributesWithoutEmittingDuplicatesAsync()
    {
        var result = RunGeneratorCompilation(CreateSchemaMarkerSource(GeneratedClientSchema));

        ThrowIfGeneratorErrors(result.Diagnostics);
        await Assert.That(typeof(MitsubishiTagClientSchemaAttribute).Assembly)
            .IsEqualTo(typeof(MitsubishiRx).Assembly);
        await Assert.That(result.Generated.Contains("class MitsubishiTagClientSchemaAttribute", StringComparison.Ordinal))
            .IsFalse();
        await Assert.That(result.Generated.Contains("class MitsubishiTagClientAttribute", StringComparison.Ordinal))
            .IsFalse();
        await Assert.That(result.Generated.Contains("class MitsubishiTagAttribute", StringComparison.Ordinal))
            .IsFalse();
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
                "global::IoT.Driver.MitsubishiRx.MitsubishiRx owner) => new(owner);"))
            .IsTrue();
    }

    /// <summary>Verifies property declarations receive common typed logical-tag helpers.</summary>
    /// <returns>The IncrementalGeneratorEmitsPropertyBindingHelpers operation result.</returns>
    [Test]
    internal async Task IncrementalGeneratorEmitsPropertyBindingHelpersAsync()
    {
        const string source = """
        using IoT.Driver.MitsubishiRx;

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
        var errors = GetErrorDiagnostics(result.Diagnostics);
        if (errors.Length > 0)
        {
            throw new InvalidOperationException(
                FormatDiagnostics(errors));
        }

        await Assert.That(result.Generated.Contains("partial class Dashboard", StringComparison.Ordinal)).IsTrue();
        await Assert.That(
            result.Generated.Contains(
                "MotorSpeedObservable => LogicalTags.Observe(" +
                "new global::IoT.Driver.Core.LogicalTagKey<float>(\"Line1.MotorSpeed\"))",
                StringComparison.Ordinal))
            .IsTrue();
        await Assert.That(
            result.Generated.Contains(
                "MotorSpeedObservableAsync => LogicalTags.ObserveAsync(" +
                "new global::IoT.Driver.Core.LogicalTagKey<float>(\"Line1.MotorSpeed\")",
                StringComparison.Ordinal))
            .IsTrue();
        await Assert.That(result.Generated.Contains("ReadMotorSpeedAsync", StringComparison.Ordinal)).IsTrue();
        await Assert.That(result.Generated.Contains("WriteMotorSpeedAsync", StringComparison.Ordinal)).IsTrue();
        await Assert.That(result.Generated.Contains("TagOperationResult<float>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Verifies consumer project package integration with the standalone generator package.</summary>
    /// <returns>
    /// The
    /// ConsumerProjectReferencingPackedMitsubishiRxAndGeneratorPackagesBuildsGeneratedClientSurface operation result.
    /// </returns>
    [Test]
    internal async Task
        ConsumerProjectReferencingPackedMitsubishiRxAndGeneratorPackagesBuildsGeneratedClientSurfaceAsync()
    {
        string packagePath = await PackMitsubishiRxPackageAsync();
        string version = Path.GetFileNameWithoutExtension(packagePath)[(RuntimePackageId.Length + 1)..];
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
    /// <summary>Verifies the standalone generator package owns the analyzer asset.</summary>
    /// <returns>A task that completes when the package layout has been verified.</returns>
    [Test]
    internal async Task StandaloneMitsubishiRxGeneratorPackageOwnsAnalyzerAssetAsync()
    {
        string runtimePackagePath = await PackMitsubishiRxPackageAsync();
        string generatorPackagePath = _cachedPackedGeneratorPackagePath
            ?? throw new InvalidOperationException("Expected MitsubishiRx generator package was not created.");

        await using var runtimePackage = await ZipFile.OpenReadAsync(runtimePackagePath, CancellationToken.None);
        await using var generatorPackage = await ZipFile.OpenReadAsync(generatorPackagePath, CancellationToken.None);
        const string analyzerPath = "analyzers/dotnet/cs/MitsubishiRx.Generators.dll";
        var runtimeContainsAnalyzer = false;
        foreach (ZipArchiveEntry entry in runtimePackage.Entries)
        {
            if (entry.FullName.EndsWith(analyzerPath, StringComparison.OrdinalIgnoreCase))
            {
                runtimeContainsAnalyzer = true;
                break;
            }
        }

        var generatorContainsAnalyzer = false;
        foreach (ZipArchiveEntry entry in generatorPackage.Entries)
        {
            if (entry.FullName.EndsWith(analyzerPath, StringComparison.OrdinalIgnoreCase))
            {
                generatorContainsAnalyzer = true;
                break;
            }
        }

        await Assert.That(runtimeContainsAnalyzer).IsFalse();
        await Assert.That(generatorContainsAnalyzer).IsTrue();
        var generatorContainsLibraryAsset = false;
        foreach (ZipArchiveEntry entry in generatorPackage.Entries)
        {
            if (entry.FullName.StartsWith("lib/", StringComparison.OrdinalIgnoreCase))
            {
                generatorContainsLibraryAsset = true;
                break;
            }
        }

        await Assert.That(generatorContainsLibraryAsset).IsFalse();
    }
#endif
}
