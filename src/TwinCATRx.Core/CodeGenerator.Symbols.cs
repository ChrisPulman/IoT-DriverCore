// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using TwinCAT.TypeSystem;

#if REACTIVE_SHIM
namespace CP.TwinCatRx.Core.Reactive;
#else
namespace CP.TwinCatRx.Core;
#endif

/// <summary>Provides symbol traversal and source assembly helpers for <see cref="CodeGenerator"/>.</summary>
public partial class CodeGenerator
{
    /// <summary>Reports a caught compatibility failure to the optional composed error sink.</summary>
    /// <param name="exception">The caught exception.</param>
    private void ReportFailure(Exception exception) => _errorHandler?.Invoke(exception);

    /// <summary>Builds the symbol list.</summary>
    private void BuildSymbolList()
    {
        SymbolList.Clear();
        if (_symbolLoader is null)
        {
            return;
        }

        foreach (var symbol in _symbolLoader.Symbols)
        {
            _ = SymbolList.Add(CreateNewNode(symbol));
        }
    }

    /// <summary>Creates the C# code file.</summary>
    /// <param name="sb">The string builder.</param>
    /// <param name="selectedTN">The selected tn.</param>
    /// <param name="classNamespace">The class namespace.</param>
    /// <param name="isTwinCat3">if set to <c>true</c> [is twin cat3].</param>
    /// <exception cref="Exception">
    /// You cannot create a structure from simple types. Please add as a single tag in your program.
    /// </exception>
    private void CreateCsharpCodeFile(
        ref StringBuilder sb,
        INodeEmulator? selectedTN,
        string classNamespace,
        bool isTwinCat3)
    {
        var selectedNodes = selectedTN?.Nodes;
        if (selectedNodes is null || selectedNodes.Count == 0)
        {
            throw new SimpleTypeException(
                "You cannot create a structure from simple types. Please add as a single tag in your program");
        }

        _ = sb.AppendLine("using System;")
            .AppendLine("using System.Runtime.InteropServices;")
            .AppendLine(string.Empty)
            .Append("namespace ").AppendLine(classNamespace)
            .AppendLine("{");
        WriteCSharpClass(ref sb, selectedTN!, isTwinCat3);

        foreach (var node in selectedNodes)
        {
            WriteCSharpClasses(ref sb, node, isTwinCat3);
            var symbol = (ISymbol?)node.Tag;
            if (node.Nodes?.Count <= 0 || symbol?.TypeName.Contains(ArrayDeclarationPrefix) == true)
            {
                continue;
            }

            WriteCSharpClass(ref sb, node, isTwinCat3);
        }

        _ = sb.AppendLine("}");
    }

    /// <summary>Finds the next node.</summary>
    /// <param name="selectedTN">The selected tn.</param>
    /// <returns>A Value.</returns>
    private INodeEmulator? FindNextNode(INodeEmulator selectedTN)
    {
        if (selectedTN.Nodes is null)
        {
            return null;
        }

        foreach (var node in selectedTN.Nodes)
        {
            if (node.Nodes?.Count <= 0 || node.Tag is not ISymbol symbol)
            {
                continue;
            }

            var typeName = symbol.TypeName;
            if (typeName is not null
                && !_typeList.ContainsKey(typeName)
                && !typeName.Contains(ArrayDeclarationPrefix))
            {
                return node;
            }
        }

        return null;
    }

    /// <summary>Writes the C# class.</summary>
    /// <param name="sb">The string builder.</param>
    /// <param name="selectedTN">The selected tn.</param>
    /// <param name="isTwinCat3">if set to <c>true</c> [is twin cat3].</param>
    private void WriteCSharpClass(ref StringBuilder sb, INodeEmulator? selectedTN, bool isTwinCat3)
    {
        if (selectedTN is null)
        {
            return;
        }

        var symbol = (ISymbol?)selectedTN.Tag;
        if (_typeList.ContainsKey(symbol!.TypeName))
        {
            return;
        }

        _ = sb.AppendLine("[Serializable]")
            .Append("[StructLayout(LayoutKind.Sequential, Pack = ").Append(isTwinCat3 ? "0" : "1").AppendLine(")]")
            .Append("public class ").AppendLine(symbol?.TypeName)
            .AppendLine("{");
        _typeList.Add(symbol!.TypeName, symbol!.InstanceName);
        _ = sb.Append(PublicMemberPrefix).Append(symbol?.TypeName).AppendLine("()")
            .AppendLine("{")
            .AppendLine("}");
        WriteCSharpClassMembers(ref sb, selectedTN, isTwinCat3);
        _ = sb.AppendLine("}")
            .AppendLine(string.Empty);
    }

    /// <summary>Writes the C# classes.</summary>
    /// <param name="sb">The string builder.</param>
    /// <param name="selectedTN">The selected tn.</param>
    /// <param name="isTwinCat3">if set to <c>true</c> [is twin cat3].</param>
    private void WriteCSharpClasses(ref StringBuilder sb, INodeEmulator selectedTN, bool isTwinCat3)
    {
        while (true)
        {
            var nextNode = FindNextNode(selectedTN);
            if (nextNode is null)
            {
                return;
            }

            WriteCSharpClass(ref sb, nextNode, isTwinCat3);
            WriteCSharpClasses(ref sb, nextNode, isTwinCat3);
        }
    }
}
