// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using IoT.Driver.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

#if REACTIVE_SHIM

namespace IoT.Driver.MitsubishiRx.Reactive.Tests;
#else

namespace IoT.Driver.MitsubishiRx.Tests;
#endif

/// <summary>Provides Mitsubishi generated-client test helpers.</summary>
internal sealed partial class MitsubishiGeneratedClientTests
{
    /// <summary>Maximum duration allowed for a nested .NET CLI operation.</summary>
    private static readonly TimeSpan DotNetCommandTimeout = TimeSpan.FromMinutes(10);

    /// <summary>Verifies the generated client and individual tag surface.</summary>
    /// <param name="generated">The generated source.</param>
    /// <returns>A task that completes when the assertions finish.</returns>
    private static async Task AssertGeneratedClientAndTagSurfaceAsync(string generated)
    {
        await Assert.That(
            generated.Contains("public static class GeneratedMitsubishiTagClientExtensions"))
            .IsTrue();
        await Assert.That(
            generated.Contains(
                "public static GeneratedMitsubishiTagClient Generated(this " +
                "global::IoT.Driver.MitsubishiRx.MitsubishiRx owner) => new(owner);"))
            .IsTrue();
        await Assert.That(
            generated.Contains("public sealed partial class GeneratedMitsubishiTagClient"))
            .IsTrue();
        await Assert.That(generated.Contains("public TagsClient Tags { get; }")).IsTrue();
        await Assert.That(generated.Contains("public GroupsClient Groups { get; }")).IsTrue();
        await Assert.That(generated.Contains("public MotorSpeedTag MotorSpeed => new(_owner);")).IsTrue();
        await Assert.That(
            generated.Contains(
                "public Task<Responce<float>> ReadAsync(CancellationToken cancellationToken = default) => " +
                "_owner.ReadFloatByTagAsync(\"MotorSpeed\", cancellationToken);"))
            .IsTrue();
        await Assert.That(
            generated.Contains(
                "public Task<Responce> WriteAsync(float value, CancellationToken cancellationToken = default) => " +
                "_owner.WriteFloatByTagAsync(\"MotorSpeed\", value, cancellationToken);"))
            .IsTrue();
        await Assert.That(
            generated.Contains(
                "public IObservable<MitsubishiReactiveValue<float>> Observe(TimeSpan pollInterval, " +
                "TimeSpan? minimumUpdateSpacing = null) => _owner.ObserveReactiveTag(" +
                "new LogicalTagKey<float>(\"MotorSpeed\"), pollInterval, minimumUpdateSpacing);"))
            .IsTrue();
        await Assert.That(generated.Contains("public ModeTag Mode => new(_owner);")).IsTrue();
        await Assert.That(
            generated.Contains(
                "public Task<Responce<ushort>> ReadAsync(CancellationToken cancellationToken = default) => " +
                "_owner.ReadUInt16ByTagAsync(\"Mode\", cancellationToken);"))
            .IsTrue();
        await Assert.That(
            generated.Contains(
                "public Task<Responce> WriteAsync(ushort value, CancellationToken cancellationToken = default) => " +
                "_owner.WriteUInt16ByTagAsync(\"Mode\", value, cancellationToken);"))
            .IsTrue();
    }

    /// <summary>Verifies the generated group read and write surface.</summary>
    /// <param name="generated">The generated source.</param>
    /// <returns>A task that completes when the assertions finish.</returns>
    private static async Task AssertGeneratedGroupReadWriteSurfaceAsync(string generated)
    {
        await Assert.That(generated.Contains("public Line1Group Line1 => new(_owner);")).IsTrue();
        await Assert.That(
            generated.Contains("public sealed partial record Line1Snapshot(float MotorSpeed, ushort Mode)"))
            .IsTrue();
        await Assert.That(
            generated.Contains(
                "public async Task<Responce<Line1Snapshot>> ReadAsync("))
            .IsTrue();
        await Assert.That(
            generated.Contains("CancellationToken cancellationToken = default)"))
            .IsTrue();
        await Assert.That(
            generated.Contains(
                "return new Responce<Line1Snapshot>(result,"))
            .IsTrue();
        await Assert.That(
            generated.Contains("Line1Snapshot.FromSnapshot(result.Value));"))
            .IsTrue();
        await Assert.That(
            generated.Contains(
                "public Task<Responce> WriteAsync(Line1Snapshot value,"))
            .IsTrue();
        await Assert.That(
            generated.Contains(
                "_owner.WriteTagGroupSnapshotAsync(value.ToSnapshot(), cancellationToken);"))
            .IsTrue();
        await Assert.That(
            generated.Contains(
                "public async Task<Responce<Line1Snapshot?>> ReadOptionalAsync("))
            .IsTrue();
        await Assert.That(
            generated.Contains(
                "return new Responce<Line1Snapshot?>(result,"))
            .IsTrue();
        await Assert.That(
            generated.Contains("Line1Snapshot.TryFromSnapshot(result.Value));"))
            .IsTrue();
    }

    /// <summary>Verifies the generated group observation and conversion surface.</summary>
    /// <param name="generated">The generated source.</param>
    /// <returns>A task that completes when the assertions finish.</returns>
    private static async Task AssertGeneratedGroupObservationSurfaceAsync(string generated)
    {
        await Assert.That(
            generated.Contains(
                "public IObservable<MitsubishiReactiveValue<Line1Snapshot>> Observe("))
            .IsTrue();
        await Assert.That(
            generated.Contains("TimeSpan pollInterval, TimeSpan? minimumUpdateSpacing = null)"))
            .IsTrue();
        await Assert.That(
            generated.Contains(
                "public IObservable<MitsubishiReactiveValue<Line1Snapshot?>> ObserveOptional("))
            .IsTrue();
        await Assert.That(generated.Contains("public MitsubishiTagGroupSnapshot ToSnapshot()")).IsTrue();
        await Assert.That(
            generated.Contains(
                "public static Line1Snapshot? TryFromSnapshot("))
            .IsTrue();
        await Assert.That(generated.Contains("MitsubishiTagGroupSnapshot? snapshot)"))
            .IsTrue();
        await Assert.That(generated.Contains("catch (KeyNotFoundException)")).IsTrue();
        await Assert.That(generated.Contains("catch (InvalidCastException)")).IsTrue();
        await Assert.That(generated.Contains("new LogicalTagKey<float>(\"MotorSpeed\")")).IsTrue();
        await Assert.That(generated.Contains("new LogicalTagKey<ushort>(\"Mode\")")).IsTrue();
    }

    /// <summary>Creates source containing only a schema marker.</summary>
    /// <param name="schema">The serialized tag schema.</param>
    /// <returns>The source text.</returns>
    private static string CreateSchemaMarkerSource(string schema)
        => $$"""
        using IoT.Driver.MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

    /// <summary>Creates source that exercises the generated extension surface.</summary>
    /// <param name="schema">The serialized tag schema.</param>
    /// <returns>The source text.</returns>
    private static string CreateGeneratedExtensionUsageSource(string schema)
        => $$"""
        using System;
        using System.Threading.Tasks;
        using IoT.Driver.MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }

        /// <summary>Provides the Usage type.</summary>
        internal static class Usage
        {
            /// <summary>Executes the ExecuteAsync operation.</summary>
            /// <param name="client">The client parameter.</param>
            /// <returns>The ExecuteAsync operation result.</returns>
            public static async Task ExecuteAsync(global::IoT.Driver.MitsubishiRx.MitsubishiRx client)
            {
                _ = client.Generated().Tags.MotorSpeed;
                _ = client.Generated().Groups.Line1;
                _ = client.Generated().Tags.MotorSpeed.Observe(TimeSpan.FromMilliseconds(250));
                _ = client.Generated().Groups.Line1.Observe(TimeSpan.FromSeconds(1));
                _ = client.Generated().Groups.Line1.ObserveOptional(TimeSpan.FromSeconds(1));
                var line1 = await client.Generated().Groups.Line1.ReadAsync();
                _ = line1.Value!.Mode;
                var optional = await client.Generated().Groups.Line1.ReadOptionalAsync();
                _ = optional.Value?.Mode;
                if (line1.Value is not null)
                {
                    await client.Generated().Groups.Line1.WriteAsync(line1.Value);
                    _ = line1.Value.ToSnapshot();
                }
                await client.Generated().Tags.Mode.WriteAsync(2);
            }
        }
        """;

    /// <summary>Throws when generator compilation produced errors.</summary>
    /// <param name="diagnostics">The generator diagnostics.</param>
    private static void ThrowIfGeneratorErrors(IReadOnlyList<Diagnostic> diagnostics)
    {
        var errors = GetErrorDiagnostics(diagnostics);
        if (errors.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            FormatDiagnostics(errors));
    }

    /// <summary>Writes the generated-client consumer project files.</summary>
    /// <param name="consumerProjectPath">The consumer project path.</param>
    /// <param name="programPath">The consumer source path.</param>
    /// <param name="version">The package version shared by the runtime and generator packages.</param>
    /// <returns>A task that completes after both files are written.</returns>
    private static async Task WriteConsumerProjectFilesAsync(
        string consumerProjectPath,
        string programPath,
        string version)
    {
        await WriteConsumerProjectFileAsync(consumerProjectPath, version);
        await WriteConsumerProgramFileAsync(programPath);
    }

    /// <summary>Writes the generated-client consumer project definition.</summary>
    /// <param name="consumerProjectPath">The consumer project path.</param>
    /// <param name="version">The package version shared by the runtime and generator packages.</param>
    /// <returns>A task that completes after the file is written.</returns>
    private static Task WriteConsumerProjectFileAsync(string consumerProjectPath, string version)
    {
        string targetFramework = GetCurrentTargetFramework();
        return File.WriteAllTextAsync(
            consumerProjectPath,
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{{targetFramework}}</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="{{RuntimePackageId}}" Version="{{version}}" />
                <PackageReference Include="{{GeneratorPackageId}}" Version="{{version}}" PrivateAssets="all" />
              </ItemGroup>
            </Project>
            """,
            CancellationToken.None);
    }

    /// <summary>Writes the generated-client consumer source file.</summary>
    /// <param name="programPath">The consumer source path.</param>
    /// <returns>A task that completes after the file is written.</returns>
    private static Task WriteConsumerProgramFileAsync(string programPath)
        => File.WriteAllTextAsync(
            programPath,
            """
            using System.Threading.Tasks;
            using IoT.Driver.MitsubishiRx;

            /// <summary>Provides the SchemaMarker type.</summary>
            [MitsubishiTagClientSchema(
                @"{""tags"":[{""name"":""MotorSpeed"",""address"":""D100"",""dataType"":""Float""}]," +
                "\"groups\":[{\"name\":\"Line1\",\"tagNames\":[\"MotorSpeed\"]}]}" )]
            internal sealed class SchemaMarker { }

            /// <summary>Provides the Usage type.</summary>
            internal static class Usage
            {
                /// <summary>Executes the ExecuteAsync operation.</summary>
                /// <param name="client">The client parameter.</param>
                /// <returns>The ExecuteAsync operation result.</returns>
                public static async Task ExecuteAsync(global::IoT.Driver.MitsubishiRx.MitsubishiRx client)
                {
                    _ = client.Generated().Tags.MotorSpeed;
                    _ = await client.Generated().Tags.MotorSpeed.ReadAsync();
                    var line1 = await client.Generated().Groups.Line1.ReadAsync();
                    _ = line1.Value?.MotorSpeed;
                    var optional = await client.Generated().Groups.Line1.ReadOptionalAsync();
                    _ = optional.Value?.MotorSpeed;
                }
            }
            """,
            CancellationToken.None);

    /// <summary>Executes the RunGenerator operation.</summary>
    /// <param name="source">The source parameter.</param>
    /// <returns>The RunGenerator operation result.</returns>
    private static string RunGenerator(string source)
    {
        var result = RunGeneratorCompilation(source);
        ThrowIfGeneratorErrors(result.Diagnostics);

        if (string.IsNullOrWhiteSpace(result.Generated))
        {
            throw new InvalidOperationException("Generator produced no sources.");
        }

        return result.Generated;
    }

    /// <summary>Executes the RunGeneratorCompilation operation.</summary>
    /// <param name="source">The source parameter.</param>
    /// <returns>The RunGeneratorCompilation operation result.</returns>
    private static (string Generated, IReadOnlyList<Diagnostic> Diagnostics) RunGeneratorCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var generatorAssembly = typeof(MitsubishiTagClientGenerator).Assembly;
        var references = new List<MetadataReference>();
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            {
                references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
        }

        AddReference(references, generatorAssembly.Location);
        AddReference(references, typeof(MitsubishiRx).Assembly.Location);
        AddReference(references, typeof(LogicalTag).Assembly.Location);
        AddReference(references, typeof(Expression).Assembly.Location);
        AddReference(references, typeof(LinqExtensions).Assembly.Location);

        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorTests",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new(OutputKind.DynamicallyLinkedLibrary));

        IIncrementalGenerator generator = new MitsubishiTagClientGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics);
        var runResult = driver.GetRunResult();

        var generatedSources = new List<string>();
        foreach (GeneratorRunResult generatorResult in runResult.Results)
        {
            foreach (GeneratedSourceResult generatedSource in generatorResult.GeneratedSources)
            {
                generatedSources.Add(generatedSource.SourceText.ToString());
            }
        }

        var generated = string.Join(
            $"{Environment.NewLine}// ----{Environment.NewLine}",
            generatedSources);

        var diagnostics = new List<Diagnostic>(outputCompilation.GetDiagnostics());
        diagnostics.AddRange(generatorDiagnostics);
        diagnostics.AddRange(runResult.Diagnostics);

        return (generated, diagnostics);
    }

    /// <summary>Executes the AddReference operation.</summary>
    /// <param name="references">The references parameter.</param>
    /// <param name="assemblyLocation">The assemblyLocation parameter.</param>
    private static void AddReference(List<MetadataReference> references, string assemblyLocation)
    {
        foreach (MetadataReference metadataReference in references)
        {
            if (metadataReference is PortableExecutableReference reference
                && string.Equals(reference.FilePath, assemblyLocation, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        references.Add(MetadataReference.CreateFromFile(assemblyLocation));
    }

    /// <summary>Executes the ToLiteral operation.</summary>
    /// <param name="value">The value parameter.</param>
    /// <returns>The ToLiteral operation result.</returns>
    private static string ToLiteral(string value)
        => SymbolDisplay.FormatLiteral(value, quote: true);

    /// <summary>Executes the CreateTemporaryDirectory operation.</summary>
    /// <returns>The CreateTemporaryDirectory operation result.</returns>
    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"mitsubishirx-generated-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>Executes the GetRepositoryRoot operation.</summary>
    /// <returns>The GetRepositoryRoot operation result.</returns>
    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "README.md"))
                && File.Exists(Path.Combine(current.FullName, "src", "MitsubishiRx", "MitsubishiRx.csproj")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    /// <summary>Executes the PackMitsubishiRxPackageAsync operation.</summary>
    /// <returns>The PackMitsubishiRxPackageAsync operation result.</returns>
    private static async Task<string> PackMitsubishiRxPackageAsync()
    {
        if (HasValidPackageCache())
        {
            return _cachedPackedPackagePath!;
        }

        await PackagePackGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (HasValidPackageCache())
            {
                return _cachedPackedPackagePath!;
            }

            string repoRoot = GetRepositoryRoot();
            string outputDirectory = CreateTemporaryDirectory();
            string targetFramework = GetCurrentTargetFramework();
            await PackMitsubishiPackagesAsync(repoRoot, outputDirectory, targetFramework).ConfigureAwait(false);
            _cachedPackedPackagePath = FindRuntimePackage(outputDirectory);
            _cachedPackedGeneratorPackagePath = FindPackage(outputDirectory, GeneratorPackageId);

            return _cachedPackedPackagePath
                ?? throw new InvalidOperationException("Expected MitsubishiRx runtime package was not created.");
        }
        finally
        {
            _ = PackagePackGate.Release();
        }
    }

    /// <summary>Checks whether both cached package paths still identify existing files.</summary>
    /// <returns><see langword="true"/> when the runtime and generator packages are cached.</returns>
    private static bool HasValidPackageCache() =>
        !string.IsNullOrWhiteSpace(_cachedPackedPackagePath)
        && File.Exists(_cachedPackedPackagePath)
        && !string.IsNullOrWhiteSpace(_cachedPackedGeneratorPackagePath)
        && File.Exists(_cachedPackedGeneratorPackagePath);

    /// <summary>Packs the Mitsubishi runtime and each local dependency used by the consumer test.</summary>
    /// <param name="repoRoot">The repository root.</param>
    /// <param name="outputDirectory">The local package source.</param>
    /// <param name="targetFramework">The active test target framework.</param>
    /// <returns>A task that completes after every package has been created.</returns>
    private static async Task PackMitsubishiPackagesAsync(
        string repoRoot,
        string outputDirectory,
        string targetFramework)
    {
        string sourceRoot = Path.Combine(repoRoot, "src");
        await PackProjectAsync(
            Path.Combine(sourceRoot, "CP.IoT.Core", "CP.IoT.Core.csproj"),
            outputDirectory,
            repoRoot,
            "netstandard2.0").ConfigureAwait(false);
        await PackProjectAsync(
            Path.Combine(sourceRoot, "SerialPortRx", "SerialPortRx.csproj"),
            outputDirectory,
            repoRoot,
            targetFramework).ConfigureAwait(false);
        await PackProjectAsync(
            Path.Combine(sourceRoot, "MitsubishiRx.Generators", "MitsubishiRx.Generators.csproj"),
            outputDirectory,
            repoRoot,
            "netstandard2.0").ConfigureAwait(false);
        await PackProjectAsync(
            Path.Combine(sourceRoot, "MitsubishiRx", "MitsubishiRx.csproj"),
            outputDirectory,
            repoRoot,
            targetFramework).ConfigureAwait(false);
    }

    /// <summary>Finds the generated runtime package while excluding its similarly prefixed generator package.</summary>
    /// <param name="outputDirectory">The package output directory.</param>
    /// <returns>The runtime package path, or <see langword="null"/> when absent.</returns>
    private static string? FindRuntimePackage(string outputDirectory)
    {
        foreach (string packagePath in Directory.GetFiles(
            outputDirectory,
            $"{RuntimePackageId}.*.nupkg",
            SearchOption.TopDirectoryOnly))
        {
            if (!packagePath.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase)
                && !Path.GetFileName(packagePath).StartsWith(
                    $"{GeneratorPackageId}.",
                    StringComparison.OrdinalIgnoreCase))
            {
                return packagePath;
            }
        }

        return null;
    }

    /// <summary>Finds a non-symbol package with the requested identifier.</summary>
    /// <param name="outputDirectory">The package output directory.</param>
    /// <param name="packageId">The package identifier.</param>
    /// <returns>The package path, or <see langword="null"/> when absent.</returns>
    private static string? FindPackage(string outputDirectory, string packageId)
    {
        foreach (string packagePath in Directory.GetFiles(
            outputDirectory,
            $"{packageId}.*.nupkg",
            SearchOption.TopDirectoryOnly))
        {
            if (!packagePath.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase))
            {
                return packagePath;
            }
        }

        return null;
    }

    /// <summary>Packs one project target into the package-consumer test source.</summary>
    /// <param name="projectPath">The project path.</param>
    /// <param name="outputDirectory">The local package source.</param>
    /// <param name="workingDirectory">The pack working directory.</param>
    /// <param name="targetFramework">The target framework to include.</param>
    /// <returns>A task that completes when the dependency package has been created.</returns>
    private static async Task PackProjectAsync(
        string projectPath,
        string outputDirectory,
        string workingDirectory,
        string targetFramework)
    {
        string projectName = Path.GetFileNameWithoutExtension(projectPath);
        string intermediateDirectory = Path.Combine(
            outputDirectory,
            "obj",
            $"{projectName}-{targetFramework}");
        _ = Directory.CreateDirectory(intermediateDirectory);
        string projectExtensionsPath = intermediateDirectory + Path.DirectorySeparatorChar;
        string[] targetProperties =
        [
            $"/p:TargetFramework={targetFramework}",
            $"/p:TargetFrameworks={targetFramework}",
            $"/p:LibraryTargetFrameworks={targetFramework}",
            $"/p:MSBuildProjectExtensionsPath={projectExtensionsPath}",
            $"/p:MinVerVersionOverride={ConsumerPackageVersion}",
        ];
        var restore = await RunDotNetAsync(
            "restore",
            Path.GetFullPath(projectPath),
            workingDirectory,
            ["--no-dependencies", .. targetProperties, "/p:UseSharedCompilation=false"])
            .ConfigureAwait(false);
        if (restore.ExitCode != 0)
        {
            throw new InvalidOperationException(restore.Output);
        }

        string[] packArguments =
        [
            "-c",
            "Release",
            "--no-build",
            "--no-restore",
            "-o",
            outputDirectory,
            .. targetProperties,
            $"/p:OutputPath={AppContext.BaseDirectory}",
            "/p:BuildProjectReferences=false",
            "/p:GeneratePackageOnBuild=false",
            "/p:UseSharedCompilation=false",
        ];
        var pack = await RunDotNetAsync(
            "pack",
            Path.GetFullPath(projectPath),
            workingDirectory,
            packArguments).ConfigureAwait(false);
        if (pack.ExitCode == 0)
        {
            return;
        }

        throw new InvalidOperationException(pack.Output);
    }

    /// <summary>Gets the target framework that produced the active test assembly.</summary>
    /// <returns>The SDK target framework moniker used by the active test assembly.</returns>
    private static string GetCurrentTargetFramework()
    {
        const string FrameworkNamePrefix = ".NETCoreApp,Version=v";
        string frameworkName = AppContext.TargetFrameworkName
            ?? throw new InvalidOperationException("The active test target framework is unavailable.");

        if (!frameworkName.StartsWith(FrameworkNamePrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported test target framework '{frameworkName}'.");
        }

        return $"net{frameworkName.Substring(FrameworkNamePrefix.Length)}";
    }

    /// <summary>Executes the RunDotNetAsync operation.</summary>
    /// <param name="command">The command parameter.</param>
    /// <param name="projectPath">The projectPath parameter.</param>
    /// <param name="workingDirectory">The workingDirectory parameter.</param>
    /// <param name="extraArguments">The extraArguments parameter.</param>
    /// <returns>The RunDotNetAsync operation result.</returns>
    private static async Task<(int ExitCode, string Output)> RunDotNetAsync(
        string command,
        string projectPath,
        string workingDirectory,
        params string[] extraArguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = GetCurrentDotNetHostPath(),
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add(command);
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--disable-build-servers");
        foreach (string argument in extraArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start dotnet process.");

        Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(DotNetCommandTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (timeout.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().ConfigureAwait(false);
            }

            string timedOutStandardOutput = await standardOutputTask.ConfigureAwait(false);
            string timedOutStandardError = await standardErrorTask.ConfigureAwait(false);
            throw new TimeoutException(
                $"dotnet {command} exceeded {DotNetCommandTimeout}.{Environment.NewLine}" +
                timedOutStandardOutput +
                Environment.NewLine +
                timedOutStandardError,
                ex);
        }

        string standardOutput = await standardOutputTask.ConfigureAwait(false);
        string standardError = await standardErrorTask.ConfigureAwait(false);
        return (process.ExitCode, standardOutput + Environment.NewLine + standardError);
    }

    /// <summary>Gets the .NET host that loaded the current test process.</summary>
    /// <returns>The absolute path to the current .NET host.</returns>
    private static string GetCurrentDotNetHostPath()
    {
        string? hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(hostPath) && File.Exists(hostPath))
        {
            return hostPath;
        }

        string? processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath)
            && string.Equals(
                Path.GetFileNameWithoutExtension(processPath),
                "dotnet",
                StringComparison.OrdinalIgnoreCase))
        {
            return processPath;
        }

        var runtimeDirectory = new DirectoryInfo(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory());
        string? runtimeHostPath = runtimeDirectory.Parent?.Parent?.Parent is DirectoryInfo dotnetRoot
            ? Path.Combine(dotnetRoot.FullName, "dotnet.exe")
            : null;
        if (!string.IsNullOrWhiteSpace(runtimeHostPath) && File.Exists(runtimeHostPath))
        {
            return runtimeHostPath;
        }

        throw new FileNotFoundException("Could not locate the .NET host for the active test runtime.");
    }

    /// <summary>Executes the TryDeleteDirectory operation.</summary>
    /// <param name="path">The path parameter.</param>
    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Unable to delete temporary directory '{path}'.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException($"Unable to delete temporary directory '{path}'.", ex);
        }
    }
}
