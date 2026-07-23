// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

#if REACTIVE_SHIM
namespace IoT.DriverCore.TwinCATRx.Core.Reactive;
#else
namespace IoT.DriverCore.TwinCATRx.Core;
#endif

/// <summary>I Language Service.</summary>
public interface ILanguageService
{
    /// <summary>Parses the text.</summary>
    /// <param name="code">The code.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>A SyntaxTree.</returns>
    SyntaxTree ParseText(string code, SourceCodeKind kind);

    /// <summary>Creates the library compilation.</summary>
    /// <param name="assemblyName">Name of the assembly.</param>
    /// <param name="enableOptimisations">if set to <c>true</c> [enable optimisations].</param>
    /// <returns>A Compilation.</returns>
    Compilation CreateLibraryCompilation(string assemblyName, bool enableOptimisations);
}
