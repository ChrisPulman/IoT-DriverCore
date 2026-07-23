// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using TwinCAT.TypeSystem;

#if REACTIVE_SHIM
namespace IoT.DriverCore.TwinCATRx.Core.Reactive;
#else
namespace IoT.DriverCore.TwinCATRx.Core;
#endif

/// <summary>Provides code-emission helpers for <see cref="CodeGenerator"/>.</summary>
public partial class CodeGenerator
{
    /// <summary>Converts a PLC array type name to a C# array type name.</summary>
    /// <param name="plcType">The PLC type name.</param>
    /// <param name="arrayType">The converted array type.</param>
    /// <returns><c>true</c> when the type was converted.</returns>
    private static bool TryConvertArrayType(string plcType, out string arrayType)
    {
        foreach (var mapping in ArrayTypeMappings)
        {
            if (!plcType.Contains(mapping.PlcType))
            {
                continue;
            }

            var bounds = plcType.Replace(ArrayDeclarationPrefix, string.Empty);
            bounds = bounds.Replace($"] {mapping.PlcType}", string.Empty);
            arrayType = $"{mapping.CSharpType},{bounds}";
            return true;
        }

        arrayType = string.Empty;
        return false;
    }

    /// <summary>Converts a PLC fixed-length string type name to a C# string type name.</summary>
    /// <param name="plcType">The PLC type name.</param>
    /// <param name="stringType">The converted string type.</param>
    /// <returns><c>true</c> when the type was converted.</returns>
    private static bool TryConvertStringType(string plcType, out string stringType)
    {
        if (!plcType.Contains("STRING("))
        {
            stringType = string.Empty;
            return false;
        }

        var size = plcType.Replace("STRING(", string.Empty);
        size = size.Replace(")", string.Empty);
        stringType = $"System.String,{size}";
        return true;
    }

    /// <summary>Writes the C# class members.</summary>
    /// <param name="sb">The sb.</param>
    /// <param name="selectedTN">The selected tn.</param>
    /// <param name="isTwinCat3">if set to <c>true</c> [is twin cat3].</param>
    private static void WriteCSharpClassMembers(ref StringBuilder sb, INodeEmulator selectedTN, bool isTwinCat3)
    {
        foreach (var node in selectedTN.Nodes!)
        {
            if (node.Tag is ISymbol symbol)
            {
                WriteCSharpClassMember(ref sb, symbol, isTwinCat3);
            }
        }
    }

    /// <summary>Writes one C# class member.</summary>
    /// <param name="sb">The string builder.</param>
    /// <param name="symbol">The symbol to write.</param>
    /// <param name="isTwinCat3">Whether TwinCAT 3 packing should be used.</param>
    private static void WriteCSharpClassMember(ref StringBuilder sb, ISymbol symbol, bool isTwinCat3)
    {
        var memberName = symbol.InstanceName;
        if (IsGeneratedStructure(symbol))
        {
            _ = sb.Append(PublicMemberPrefix)
                .Append(symbol.TypeName)
                .Append(' ')
                .Append(memberName)
                .Append(ObjectInitializerFragment)
                .Append(symbol.TypeName)
                .AppendLine("();");
            return;
        }

        var stringArrayWrapper = new StringBuilder();
        var arrayOfStruct = CreateArrayOFStructure(symbol, stringArrayWrapper, isTwinCat3);
        if (!string.IsNullOrWhiteSpace(arrayOfStruct))
        {
            _ = sb.Append(stringArrayWrapper).Append(arrayOfStruct);
            return;
        }

        WritePrimitiveMember(ref sb, PLCToCSharpTypeConverter(symbol.TypeName), memberName);
    }

    /// <summary>Gets whether a symbol should be emitted as a generated structure instance.</summary>
    /// <param name="symbol">The symbol to inspect.</param>
    /// <returns><c>true</c> when the symbol is a generated structure.</returns>
    private static bool IsGeneratedStructure(ISymbol symbol) =>
        symbol.Category != DataTypeCategory.Array
        && symbol.Category != DataTypeCategory.String
        && symbol.Category != DataTypeCategory.Primitive
        && !symbol.TypeName.Contains(ArrayDeclarationPrefix);

    /// <summary>Writes one primitive C# class member.</summary>
    /// <param name="sb">The string builder.</param>
    /// <param name="csharpType">The C# type.</param>
    /// <param name="memberName">The member name.</param>
    private static void WritePrimitiveMember(ref StringBuilder sb, string csharpType, string memberName)
    {
        if (csharpType == "System.Boolean")
        {
            _ = sb.AppendLine("[MarshalAs(UnmanagedType.I1)]")
                .Append(PublicMemberPrefix).Append(csharpType).Append(' ').Append(memberName).AppendLine(";");
            return;
        }

        if (csharpType == "System.String")
        {
            _ = sb.AppendLine("[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 81)]")
                .Append(PublicMemberPrefix).Append(csharpType).Append(' ').Append(memberName).AppendLine(";");
            return;
        }

        if (csharpType.Contains("System.String[", StringComparison.Ordinal))
        {
            var length = int.Parse(csharpType.Split(',')[1]);
            _ = sb.Append("[MarshalAs(UnmanagedType.ByValTStr, SizeConst = ").Append(length + 1).AppendLine(")] ")
                .Append("public System.String[] ")
                .Append(memberName)
                .Append(" = new ")
                .Append("System.String[")
                .Append(length)
                .AppendLine("];");
            return;
        }

        if (csharpType.Contains("System.String,", StringComparison.Ordinal))
        {
            var length = int.Parse(csharpType.Split(',')[1]);
            _ = sb.Append("[MarshalAs(UnmanagedType.ByValTStr, SizeConst = ").Append(length + 1).AppendLine(")] ")
                .Append("public string ").Append(memberName).AppendLine(";");
            return;
        }

        _ = sb.Append(PublicMemberPrefix).Append(csharpType).Append(' ').Append(memberName).AppendLine(";");
    }

    /// <summary>Creates the new node.</summary>
    /// <param name="symbol">The symbol.</param>
    /// <returns>A Value.</returns>
    private static NodeEmulator CreateNewNode(ISymbol symbol)
    {
        var node = new NodeEmulator
        {
            Text = symbol.InstanceName,
            Tag = symbol,
        };
        foreach (var subSymbol in symbol.SubSymbols)
        {
            node.Nodes?.Add(CreateNewNode(subSymbol));
        }

        return node;
    }

    /// <summary>Creates a generated field for a PLC array type.</summary>
    /// <param name="symbol">The source symbol.</param>
    /// <param name="wrapperBuilder">The wrapper builder for fixed string arrays.</param>
    /// <param name="isTwinCat3">Whether TwinCAT 3 packing should be used.</param>
    /// <returns>The generated field source.</returns>
    private static string CreateArrayOFStructure(ISymbol symbol, StringBuilder wrapperBuilder, bool isTwinCat3)
    {
        if (!TryParseArrayType(symbol.TypeName, out var dimensions, out var elementType)
            || !TryGetArrayLength(dimensions, out var totalLength))
        {
            return string.Empty;
        }

        var instanceName = symbol.InstanceName?.Trim();
        if (string.IsNullOrWhiteSpace(instanceName) || string.IsNullOrWhiteSpace(elementType) || totalLength <= 0)
        {
            return string.Empty;
        }

        var csharpType = elementType;
        var marshalAttribute = $"[MarshalAs(UnmanagedType.ByValArray, SizeConst = {totalLength})]";
        if (TryGetPrimitiveArrayMapping(
            elementType,
            totalLength,
            out var primitiveType,
            out var primitiveMarshalAttribute))
        {
            csharpType = primitiveType;
            marshalAttribute = primitiveMarshalAttribute;
        }
        else if (TryCreateStringArrayWrapper(elementType, wrapperBuilder, isTwinCat3, out var wrapperName))
        {
            csharpType = wrapperName;
        }

        return BuildArrayField(marshalAttribute, csharpType, instanceName!, totalLength);
    }

    /// <summary>Parses a PLC array type into dimensions and element type.</summary>
    /// <param name="typeName">The PLC type name.</param>
    /// <param name="dimensions">The parsed dimensions.</param>
    /// <param name="elementType">The parsed element type.</param>
    /// <returns><c>true</c> when parsing succeeds.</returns>
    private static bool TryParseArrayType(string typeName, out string dimensions, out string elementType)
    {
        var trimmedTypeName = typeName.Trim();
        var arrayIndex = trimmedTypeName.IndexOf(ArrayDeclarationPrefix, StringComparison.OrdinalIgnoreCase);
        var ofIndex = trimmedTypeName.IndexOf("] OF ", StringComparison.OrdinalIgnoreCase);
        if (arrayIndex < 0 || ofIndex < 0)
        {
            dimensions = string.Empty;
            elementType = string.Empty;
            return false;
        }

        var dimensionsStart = arrayIndex + ArrayDeclarationPrefix.Length;
        dimensions = trimmedTypeName.Substring(dimensionsStart, ofIndex - dimensionsStart).Trim();
        elementType = trimmedTypeName.Substring(ofIndex + "] OF ".Length).Trim();
        return true;
    }

    /// <summary>Gets the flattened element count for PLC array dimensions.</summary>
    /// <param name="dimensions">The PLC dimensions.</param>
    /// <param name="totalLength">The flattened element count.</param>
    /// <returns><c>true</c> when the length was calculated.</returns>
    private static bool TryGetArrayLength(string dimensions, out int totalLength)
    {
        totalLength = 1;
        foreach (var dimension in dimensions.Split(','))
        {
            var bounds = dimension.Trim().Split(RangeSeparator, StringSplitOptions.None);
            if (bounds.Length != 2
                || !int.TryParse(bounds[0].Trim(), out var lower)
                || !int.TryParse(bounds[1].Trim(), out var upper)
                || upper < lower)
            {
                totalLength = 0;
                return false;
            }

            totalLength *= upper - lower + 1;
        }

        return true;
    }

    /// <summary>Gets the C# type and marshal attribute for primitive PLC array elements.</summary>
    /// <param name="elementType">The PLC element type.</param>
    /// <param name="totalLength">The flattened element count.</param>
    /// <param name="csharpType">The C# type.</param>
    /// <param name="marshalAttribute">The marshal attribute source.</param>
    /// <returns><c>true</c> when the element type is primitive.</returns>
    private static bool TryGetPrimitiveArrayMapping(
        string elementType,
        int totalLength,
        out string csharpType,
        out string marshalAttribute)
    {
        if (!PrimitiveArrayMappings.TryGetValue(elementType.ToUpperInvariant(), out var mapping))
        {
            csharpType = string.Empty;
            marshalAttribute = string.Empty;
            return false;
        }

        csharpType = mapping.CSharpType;
        marshalAttribute =
            $"[MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.{mapping.MarshalSubType}, "
            + $"SizeConst = {totalLength})]";
        return true;
    }

    /// <summary>Creates a wrapper struct for fixed-length PLC string arrays.</summary>
    /// <param name="elementType">The PLC element type.</param>
    /// <param name="wrapperBuilder">The wrapper builder.</param>
    /// <param name="isTwinCat3">Whether TwinCAT 3 packing should be used.</param>
    /// <param name="wrapperName">The wrapper type name.</param>
    /// <returns><c>true</c> when a string wrapper was created or found.</returns>
    private static bool TryCreateStringArrayWrapper(
        string elementType,
        StringBuilder wrapperBuilder,
        bool isTwinCat3,
        out string wrapperName)
    {
        if (!TryGetFixedStringLength(elementType, out var stringLength))
        {
            wrapperName = string.Empty;
            return false;
        }

        wrapperName = $"STRING_{stringLength}_WRAPPER";
        if (wrapperBuilder.ToString().Contains($"struct {wrapperName}"))
        {
            return true;
        }

        _ = wrapperBuilder
            .AppendLine($"[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = {(isTwinCat3 ? 0 : 1)})]")
            .AppendLine($"public struct {wrapperName}")
            .AppendLine("{")
            .AppendLine($"    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = {stringLength + 1})]")
            .AppendLine("    public string Value;")
            .AppendLine("}")
            .AppendLine()
            .AppendLine($"public static string[] ToStringArray({wrapperName}[] wrappers)")
            .AppendLine("{")
            .AppendLine("    if (wrappers == null) return Array.Empty<string>();")
            .AppendLine("    var result = new string[wrappers.Length];")
            .AppendLine("    for (int i = 0; i < wrappers.Length; i++)")
            .AppendLine("        result[i] = wrappers[i].Value;")
            .AppendLine("    return result;")
            .AppendLine("}")
            .AppendLine();
        return true;
    }

    /// <summary>Tries to read the declared length from a fixed-length PLC string type.</summary>
    /// <param name="elementType">The PLC element type.</param>
    /// <param name="stringLength">The parsed string length.</param>
    /// <returns><c>true</c> when the type is a fixed-length string.</returns>
    private static bool TryGetFixedStringLength(string elementType, out int stringLength)
    {
        stringLength = 0;
        var openParenIndex = elementType.IndexOf('(');
        if (openParenIndex < 0)
        {
            return false;
        }

        var closeParenIndex = elementType.IndexOf(')', openParenIndex + 1);
        if (closeParenIndex <= openParenIndex)
        {
            return false;
        }

        var typeName = elementType[..openParenIndex].Trim();
        if (!string.Equals(typeName, "STRING", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var lengthText = elementType[(openParenIndex + 1)..closeParenIndex].Trim();
        return int.TryParse(lengthText, out stringLength);
    }

    /// <summary>Builds the generated array field source.</summary>
    /// <param name="marshalAttribute">The marshal attribute source.</param>
    /// <param name="csharpType">The C# type.</param>
    /// <param name="instanceName">The PLC instance name.</param>
    /// <param name="totalLength">The flattened element count.</param>
    /// <returns>The generated array field source.</returns>
    private static string BuildArrayField(
        string marshalAttribute,
        string csharpType,
        string instanceName,
        int totalLength)
    {
        var sb = new StringBuilder();
        _ = sb.AppendLine(marshalAttribute)
            .Append(PublicMemberPrefix)
            .Append(csharpType)
            .Append("[] ")
            .Append(instanceName)
            .Append(ObjectInitializerFragment)
            .Append(csharpType)
            .Append('[')
            .Append(totalLength)
            .AppendLine("];");
        return sb.ToString();
    }
}
