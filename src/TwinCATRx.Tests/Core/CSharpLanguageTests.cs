// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace IoT.DriverCore.TwinCATRx.Tests.Core;

/// <summary>Tests for the internal C# language service.</summary>
public class CSharpLanguageTests
{
    /// <summary>Verifies parsing source through the language service.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ParseText_Returns_Syntax_TreeAsync()
    {
        var service = CreateLanguageService();
        var parseMethod = service.GetType().GetMethod("ParseText")
            ?? throw new MissingMethodException("ParseText was not found.");

        var tree = Required<SyntaxTree>(parseMethod.Invoke(service, ["class Sample { }", SourceCodeKind.Regular]));

        var text = await tree.GetTextAsync();
        await TUnitAssert.That(text.ToString()).Contains("Sample");
    }

    /// <summary>Verifies compilation creation through the language service.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task CreateLibraryCompilation_Returns_Debug_And_Release_CompilationsAsync()
    {
        var service = CreateLanguageService();
        var compilationMethod = service.GetType().GetMethod("CreateLibraryCompilation")
            ?? throw new MissingMethodException("CreateLibraryCompilation was not found.");

        var debugCompilation = Required<Compilation>(compilationMethod.Invoke(service, ["DebugAssembly", false]));
        var releaseCompilation = Required<Compilation>(compilationMethod.Invoke(service, ["ReleaseAssembly", true]));

        await TUnitAssert.That(debugCompilation.AssemblyName).IsEqualTo("DebugAssembly");
        await TUnitAssert.That(releaseCompilation.AssemblyName).IsEqualTo("ReleaseAssembly");
    }

    /// <summary>Verifies assembly creation fails for invalid source.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task CreateAssembly_Returns_False_For_Invalid_SourceAsync()
    {
        var assemblyPath = Path.Combine(Path.GetTempPath(), $"TwinCATRx_{Guid.NewGuid()}.dll");
        try
        {
            var result = InvokeCreateAssembly("public sealed class Broken {", assemblyPath);

            await TUnitAssert.That(result).IsFalse();
        }
        finally
        {
            if (File.Exists(assemblyPath))
            {
                File.Delete(assemblyPath);
            }
        }
    }

    /// <summary>Verifies assembly creation fails for blank output path.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task CreateAssembly_Returns_False_For_Blank_PathAsync()
    {
        var result = InvokeCreateAssembly("public sealed class ValidType { }", string.Empty);

        await TUnitAssert.That(result).IsFalse();
    }

    /// <summary>Creates the internal language service through reflection.</summary>
    /// <returns>The language service.</returns>
    private static object CreateLanguageService()
    {
        var type = GetLanguageServiceType();
        return Required<object>(Activator.CreateInstance(type, nonPublic: true));
    }

    /// <summary>Invokes the internal assembly creation helper.</summary>
    /// <param name="code">The source code.</param>
    /// <param name="assemblyPath">The assembly path.</param>
    /// <returns>The create result.</returns>
    private static bool InvokeCreateAssembly(string code, string assemblyPath)
    {
        var method = GetLanguageServiceType().GetMethod("CreateAssembly", BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException("CreateAssembly was not found.");
        return Required<bool>(method.Invoke(null, [code, assemblyPath]));
    }

    /// <summary>Gets the internal language service type.</summary>
    /// <returns>The language service type.</returns>
    private static Type GetLanguageServiceType() =>
        typeof(IoT.DriverCore.TwinCATRx.Core.Settings).Assembly.GetType("IoT.DriverCore.TwinCATRx.Core.CSharpLanguage")
        ?? throw new TypeLoadException("CSharpLanguage was not found.");

    /// <summary>Returns a reflected value when it has the requested type.</summary>
    /// <typeparam name="T">The requested value type.</typeparam>
    /// <param name="value">The reflected value.</param>
    /// <returns>The typed reflected value.</returns>
    private static T Required<T>(object? value) => value is T typed
        ? typed
        : throw new InvalidOperationException("The reflected value was null or had an unexpected type.");
}
