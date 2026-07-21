// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace S7PlcRx.SourceGenerators;

/// <summary>Provides attribute-discovery helpers for the S7 binding source generator.</summary>
public sealed partial class S7TagBindingSourceGenerator
{
    /// <summary>Checks whether the current class declaration directly carries a binding attribute.</summary>
    /// <param name="classSyntax">The class declaration to inspect.</param>
    /// <param name="semanticModel">The semantic model for the current compilation.</param>
    /// <returns>True when the declaration is the source of an accepted binding attribute.</returns>
    private static bool HasBindingAttribute(ClassDeclarationSyntax classSyntax, SemanticModel semanticModel)
    {
        foreach (var attributeSyntax in classSyntax.AttributeLists.SelectMany(static list => list.Attributes))
        {
            if (semanticModel.GetSymbolInfo(attributeSyntax).Symbol is not IMethodSymbol constructor)
            {
                continue;
            }

            var attributeName = constructor.ContainingType.ToDisplayString();
            if (string.Equals(attributeName, BindingAttributeName, StringComparison.Ordinal) ||
                string.Equals(attributeName, ReactiveBindingAttributeName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
