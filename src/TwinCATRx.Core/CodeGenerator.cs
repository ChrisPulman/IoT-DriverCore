// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Text;
using Microsoft.CSharp;
#if REACTIVE_SHIM
namespace IoT.DriverCore.TwinCATRx.Core.Reactive;
#else
namespace IoT.DriverCore.TwinCATRx.Core;
#endif
/// <summary>Code Generator.</summary>
/// <seealso cref="ICodeGenerator"/>
public partial class CodeGenerator : ICodeGenerator
{
    /// <summary>Stores the PLC array declaration prefix.</summary>
    private const string ArrayDeclarationPrefix = "ARRAY [";

    /// <summary>Stores the generated public-member prefix.</summary>
    private const string PublicMemberPrefix = "public ";

    /// <summary>Stores the generated object-initializer fragment.</summary>
    private const string ObjectInitializerFragment = " = new ";

    /// <summary>Separates PLC array lower and upper bounds.</summary>
    private static readonly string[] RangeSeparator = [".."];

    /// <summary>Maps PLC array element markers to C# array type names.</summary>
    private static readonly (string PlcType, string CSharpType)[] ArrayTypeMappings =
    [
        ("OF STRING", typeof(string[]) + string.Empty),
        ("OF BOOL", typeof(bool[]) + string.Empty),
        ("OF BIT", typeof(bool[]) + string.Empty),
        ("OF BIT8", typeof(bool[]) + string.Empty),
        ("OF BYTE", typeof(byte[]) + string.Empty),
        ("OF REAL", "System.Single[]"),
        ("OF LREAL", "System.Double[]"),
        ("OF FLOAT", "System.Single[]"),
        ("OF INT", "System.Int16[]"),
        ("OF INT16", "System.Int16[]"),
        ("OF DINT", "System.Int32[]"),
        ("OF INT32", "System.Int32[]")
    ];

    /// <summary>Maps primitive PLC type names to C# type names.</summary>
    private static readonly Dictionary<string, string> PrimitiveTypeMappings = new(StringComparer.Ordinal)
    {
        ["STRING(80)"] = typeof(string).ToString(),
        ["BIT"] = typeof(bool).ToString(),
        ["BIT8"] = typeof(bool).ToString(),
        ["BOOL"] = typeof(bool).ToString(),
        ["WORD"] = typeof(ushort).ToString(),
        ["BITARR16"] = typeof(ushort).ToString(),
        ["UINT16"] = typeof(ushort).ToString(),
        ["UINT"] = typeof(ushort).ToString(),
        ["INT8"] = "sbyte",
        ["INT16"] = typeof(short).ToString(),
        ["INT"] = typeof(short).ToString(),
        ["INT32"] = typeof(int).ToString(),
        ["DINT"] = typeof(int).ToString(),
        ["BITARR32"] = typeof(uint).ToString(),
        ["DWORD"] = typeof(uint).ToString(),
        ["UINT32"] = typeof(uint).ToString(),
        ["UDINT"] = typeof(uint).ToString(),
        ["UINT64"] = "ulong",
        ["ULINT"] = "ulong",
        ["INT64"] = "long",
        ["LINT"] = "long",
        ["FLOAT"] = typeof(float).ToString(),
        ["REAL"] = typeof(float).ToString(),
        ["DOUBLE"] = typeof(double).ToString(),
        ["LREAL"] = typeof(double).ToString(),
        ["BITARR8"] = typeof(byte).ToString(),
        ["USINT"] = typeof(byte).ToString(),
        ["UINT8"] = typeof(byte).ToString(),
        ["BYTE"] = typeof(byte).ToString(),
    };

    /// <summary>Maps primitive PLC array element names to C# type and marshal subtype names.</summary>
    private static readonly Dictionary<string, (string CSharpType, string MarshalSubType)> PrimitiveArrayMappings =
        new(StringComparer.Ordinal)
        {
            ["BIT"] = ("bool", "U1"),
            ["BIT8"] = ("bool", "U1"),
            ["BOOL"] = ("bool", "U1"),
            ["BITARR8"] = ("byte", "U1"),
            ["USINT"] = ("byte", "U1"),
            ["UINT8"] = ("byte", "U1"),
            ["BYTE"] = ("byte", "U1"),
            ["WORD"] = ("ushort", "I2"),
            ["BITARR16"] = ("ushort", "I2"),
            ["UINT16"] = ("ushort", "I2"),
            ["UINT"] = ("ushort", "I2"),
            ["INT16"] = ("short", "I2"),
            ["INT"] = ("short", "I2"),
            ["BITARR32"] = ("uint", "I4"),
            ["DWORD"] = ("uint", "I4"),
            ["UINT32"] = ("uint", "I4"),
            ["UDINT"] = ("uint", "I4"),
            ["INT32"] = ("int", "I4"),
            ["DINT"] = ("int", "I4"),
            ["FLOAT"] = ("float", "R4"),
            ["REAL"] = ("float", "R4"),
            ["DOUBLE"] = ("double", "R8"),
            ["LREAL"] = ("double", "R8"),
        };

    /// <summary>Stores generated type names while code is emitted.</summary>
    private readonly Hashtable _typeList = [];

    /// <summary>Receives failures caught by compatibility methods.</summary>
    private readonly Action<Exception>? _errorHandler;

    /// <summary>Stores replaceable native symbol operations.</summary>
    private readonly ICodeGeneratorRuntime _runtime;

    /// <summary>Tracks whether this instance has been disposed.</summary>
    private bool _disposedValue;

    /// <summary>Initializes a new instance of the <see cref="CodeGenerator"/> class.</summary>
    public CodeGenerator()
        : this(null, new CodeGeneratorRuntime())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CodeGenerator"/> class.</summary>
    /// <param name="errorHandler">An optional composable error sink for caught generation failures.</param>
    public CodeGenerator(Action<Exception>? errorHandler)
        : this(errorHandler, new CodeGeneratorRuntime())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CodeGenerator"/> class.</summary>
    /// <param name="errorHandler">An optional composable error sink for caught generation failures.</param>
    /// <param name="runtime">The replaceable native symbol operations.</param>
    internal CodeGenerator(Action<Exception>? errorHandler, ICodeGeneratorRuntime runtime)
    {
        _errorHandler = errorHandler;
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _disposedValue = false;
    }

    /// <summary>Gets the symbol list.</summary>
    /// <value>The symbol list.</value>
    public HashSet<INodeEmulator> SymbolList { get; } = [];

    /// <summary>Converts a supported PLC scalar, string, or array type name to its CLR representation.</summary>
    /// <param name="plcType">Type of the PLC.</param>
    /// <returns>A Value.</returns>
    /// <exception cref="Exception">
    /// This Type (" + PLCType + ")is not supported in this version, Please contact us for details of next version.
    /// </exception>
    public static string PLCToCSharpTypeConverter(string? plcType)
    {
        if (plcType is null)
        {
            return "NULL";
        }

        if (PrimitiveTypeMappings.TryGetValue(plcType, out var primitiveType))
        {
            return primitiveType;
        }

        if (TryConvertArrayType(plcType, out var arrayType))
        {
            return arrayType;
        }

        if (TryConvertStringType(plcType, out var stringType))
        {
            return stringType;
        }

        throw new UnsuportedTypeException(
            $"This Type ({plcType})is not supported in this version, Please contact us for details of next version");
    }

    /// <summary>Creates a C# code file using the default TwinCAT version.</summary>
    /// <param name="selectedTN">The selected node.</param>
    /// <returns><c>true</c> when code was created.</returns>
    public bool CreateCSharpCode(INodeEmulator selectedTN) => CreateCSharpCode(selectedTN, false);

    /// <summary>Creates a C# code file based on the selected node structure.</summary>
    /// <param name="selectedTN">The selected tn.</param>
    /// <param name="isTwinCat3">Whether TwinCAT 3 packing should be used.</param>
    /// <returns>
    /// Result as a Boolean.
    /// </returns>
    public bool CreateCSharpCode(INodeEmulator selectedTN, bool isTwinCat3) =>
        CreateCSharpCode(selectedTN, string.Empty, isTwinCat3);

    /// <summary>Creates a C# code file using default generation settings.</summary>
    /// <inheritdoc cref="CreateCSharpCode(INodeEmulator, string, bool, string)"/>
    public bool CreateCSharpCode(INodeEmulator selectedTN, string fileName) =>
        CreateCSharpCode(selectedTN, fileName, false, CodeGeneratorDefaults.Namespace);

    /// <summary>Creates a C# code file using the default namespace.</summary>
    /// <inheritdoc cref="CreateCSharpCode(INodeEmulator, string, bool, string)"/>
    public bool CreateCSharpCode(INodeEmulator selectedTN, string fileName, bool isTwinCat3) =>
        CreateCSharpCode(selectedTN, fileName, isTwinCat3, CodeGeneratorDefaults.Namespace);

    /// <summary>Creates a C# code file based on the selected node structure.</summary>
    /// <param name="selectedTN">The selected tn.</param>
    /// <param name="fileName">Name of the file.</param>
    /// <param name="isTwinCat3">if set to <c>true</c> [is twin cat3].</param>
    /// <param name="classNamespace">The class namespace.</param>
    /// <returns>
    /// Result as a Boolean.
    /// </returns>
    public bool CreateCSharpCode(
        INodeEmulator selectedTN,
        string fileName,
        bool isTwinCat3,
        string classNamespace)
    {
        if (selectedTN?.Nodes?.Count <= 0)
        {
            return false;
        }

        _typeList.Clear();
        var sb = new StringBuilder();
        CreateCsharpCodeFile(ref sb, selectedTN, classNamespace, isTwinCat3);
        var sourceCode = sb.ToString();
        if (sourceCode.Length <= 1)
        {
            return false;
        }

        try
        {
            using Stream stream = File.Open(fileName, FileMode.Create);
            using var writer = new StreamWriter(stream);
            using var codeProvider = new CSharpCodeProvider();
            var compileUnit = new CodeSnippetCompileUnit(sourceCode);
            var options = new CodeGeneratorOptions
            {
                BracingStyle = "C",
                IndentString = "   ",
            };
            codeProvider.CreateGenerator(writer).GenerateCodeFromCompileUnit(compileUnit, writer, options);
            return true;
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
            return false;
        }
    }

    /// <summary>Creates a C# code string using default generation settings.</summary>
    /// <param name="selectedTN">The selected node.</param>
    /// <returns>The generated code.</returns>
    public string CreateCSharpCodeString(INodeEmulator? selectedTN) =>
        CreateCSharpCodeString(selectedTN, false, CodeGeneratorDefaults.Namespace);

    /// <summary>Creates a C# code string using the default namespace.</summary>
    /// <inheritdoc cref="CreateCSharpCodeString(INodeEmulator, bool, string)"/>
    public string CreateCSharpCodeString(INodeEmulator? selectedTN, bool isTwinCat3) =>
        CreateCSharpCodeString(selectedTN, isTwinCat3, CodeGeneratorDefaults.Namespace);

    /// <summary>Creates the C# code string.</summary>
    /// <param name="selectedTN">The selected tn.</param>
    /// <param name="isTwinCat3">if set to <c>true</c> [is twin cat3].</param>
    /// <param name="classNamespace">The class namespace.</param>
    /// <returns>A Value.</returns>
    public string CreateCSharpCodeString(
        INodeEmulator? selectedTN,
        bool isTwinCat3,
        string classNamespace)
    {
        if (selectedTN?.Nodes?.Count != 0)
        {
            _typeList.Clear();
            var sb = new StringBuilder();
            CreateCsharpCodeFile(ref sb, selectedTN, classNamespace, isTwinCat3);
            return sb.ToString().Length <= 1 ? string.Empty : sb.ToString();
        }

        return string.Empty;
    }

    /// <summary>Creates a DLL based on the selected node structure.</summary>
    /// <param name="selectedTN">The selected tn.</param>
    /// <returns>
    /// Result as a Boolean.
    /// </returns>
#if NET8_0_OR_GREATER
    [RequiresDynamicCode("Emits and loads assemblies dynamically via Roslyn/Mono.Cecil.")]
    [RequiresUnreferencedCode("Dynamic compilation may access trimmed members.")]
#endif
    public bool CreateDll(INodeEmulator selectedTN) => CreateDll(selectedTN, false);

    /// <summary>Creates a DLL based on the selected node structure.</summary>
    /// <param name="selectedTN">The selected node.</param>
    /// <param name="isTwinCat3">Whether TwinCAT 3 packing should be used.</param>
    /// <returns><c>true</c> when the DLL was created.</returns>
#if NET8_0_OR_GREATER
    [RequiresDynamicCode("Emits and loads assemblies dynamically via Roslyn/Mono.Cecil.")]
    [RequiresUnreferencedCode("Dynamic compilation may access trimmed members.")]
#endif
    public bool CreateDll(INodeEmulator selectedTN, bool isTwinCat3) =>
        CreateDll(selectedTN, string.Empty, isTwinCat3);

    /// <summary>Creates a DLL using default generation settings.</summary>
    /// <inheritdoc cref="CreateDll(INodeEmulator, string, bool, string)"/>
#if NET8_0_OR_GREATER
    [RequiresDynamicCode("Emits and loads assemblies dynamically via Roslyn/Mono.Cecil.")]
    [RequiresUnreferencedCode("Dynamic compilation may access trimmed members.")]
#endif
    public bool CreateDll(INodeEmulator? selectedTN, string fileName) =>
        CreateDll(selectedTN, fileName, false, CodeGeneratorDefaults.Namespace);

    /// <summary>Creates a DLL using the default namespace.</summary>
    /// <inheritdoc cref="CreateDll(INodeEmulator, string, bool, string)"/>
#if NET8_0_OR_GREATER
    [RequiresDynamicCode("Emits and loads assemblies dynamically via Roslyn/Mono.Cecil.")]
    [RequiresUnreferencedCode("Dynamic compilation may access trimmed members.")]
#endif
    public bool CreateDll(INodeEmulator? selectedTN, string fileName, bool isTwinCat3) =>
        CreateDll(selectedTN, fileName, isTwinCat3, CodeGeneratorDefaults.Namespace);

    /// <summary>Creates a DLL based on the selected node structure.</summary>
    /// <param name="selectedTN">The selected tn.</param>
    /// <param name="fileName">Name of the file.</param>
    /// <param name="isTwinCat3">if set to <c>true</c> [is twincat3].</param>
    /// <param name="classNamespace">The class namespace.</param>
    /// <returns>
    /// Result as a Boolean.
    /// </returns>
#if NET8_0_OR_GREATER
    [RequiresDynamicCode("Emits and loads assemblies dynamically via Roslyn/Mono.Cecil.")]
    [RequiresUnreferencedCode("Dynamic compilation may access trimmed members.")]
#endif
    public bool CreateDll(
        INodeEmulator? selectedTN,
        string fileName,
        bool isTwinCat3,
        string classNamespace)
    {
        if (string.IsNullOrWhiteSpace(fileName) || selectedTN?.Nodes?.Count <= 0)
        {
            return false;
        }

        File.Delete(fileName);
        var sb = new StringBuilder();
        _typeList.Clear();
        CreateCsharpCodeFile(ref sb, selectedTN, classNamespace, isTwinCat3);
        var sourceCode = sb.ToString();
        if (sourceCode.Length <= 1)
        {
            return false;
        }

        try
        {
            return CSharpLanguage.CreateAssembly(sourceCode, fileName);
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
            return false;
        }
    }

    /// <summary>Creates the DLL from raw source.</summary>
    /// <param name="sourceCode">The C# source code.</param>
    /// <param name="fileName">Name of the file.</param>
    /// <returns>A Value.</returns>
#if NET8_0_OR_GREATER
    [RequiresDynamicCode("Emits and loads assemblies dynamically via Roslyn/Mono.Cecil.")]
    [RequiresUnreferencedCode("Dynamic compilation may access trimmed members.")]
#endif
    public bool CreateDll(string sourceCode, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || sourceCode is null || sourceCode.Length <= 1)
        {
            return false;
        }

        File.Delete(fileName);
        try
        {
            return CSharpLanguage.CreateAssembly(sourceCode, fileName);
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
            return false;
        }
    }

    /// <summary>Performs application-defined tasks associated with freeing resources.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Loads symbols from the specified PLC ADS address.</summary>
    /// <param name="adsAddress">The ADS address.</param>
    /// <returns>
    /// HashSet(Of NodeEmulator).
    /// </returns>
    public HashSet<INodeEmulator> LoadSymbols(string adsAddress) =>
        LoadSymbols(adsAddress, CodeGeneratorDefaults.AdsPort);

    /// <summary>Loads symbols from the specified PLC ADS address and port.</summary>
    /// <param name="adsAddress">The ADS address.</param>
    /// <param name="port">The port.</param>
    /// <returns>
    /// HashSet(Of NodeEmulator).
    /// </returns>
    public HashSet<INodeEmulator> LoadSymbols(string adsAddress, int port)
    {
        _runtime.LoadSymbols(adsAddress, port, BuildSymbolList);
        return SymbolList;
    }

    /// <summary>Loads symbols from the specified PLC ADS port.</summary>
    /// <param name="port">The port.</param>
    /// <returns>A Value.</returns>
    public HashSet<INodeEmulator> LoadSymbols(int port)
    {
        _runtime.LoadSymbols(port, BuildSymbolList);
        return SymbolList;
    }

    /// <summary>Reads the symbol.</summary>
    /// <param name="adsAddress">The ADS address.</param>
    /// <param name="port">The port.</param>
    /// <param name="variable">The variable.</param>
    /// <param name="variableType">Type of the variable.</param>
    /// <returns>A Value.</returns>
    public object ReadSymbol(string adsAddress, int port, string variable, Type variableType) =>
        _runtime.ReadSymbol(adsAddress, port, variable, variableType);

    /// <summary>Searches for the nearest matching symbol list element.</summary>
    /// <param name="symbolName">Name of the symbol.</param>
    /// <returns>
    /// NodeEmulator.
    /// </returns>
    public INodeEmulator SearchSymbols(string? symbolName)
    {
        if (string.IsNullOrWhiteSpace(symbolName))
        {
            return new NodeEmulator();
        }

        var normalizedSymbolName = symbolName!;
        if (normalizedSymbolName.StartsWith(".", StringComparison.Ordinal))
        {
            normalizedSymbolName = normalizedSymbolName.Remove(0, 1);
        }

        var symbols = normalizedSymbolName.Split('.');
        var ret = FindNode(SymbolList, symbols[0]);
        for (var i = 1; i < symbols.Length && ret is not null; i++)
        {
            ret = FindNode(ret.Nodes, symbols[i]);
        }

        return ret ?? new NodeEmulator();
    }

    /// <summary>Releases unmanaged and optionally managed resources.</summary>
    /// <param name="disposing">
    /// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only
    /// unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue && disposing)
        {
            _runtime.Dispose();
            SymbolList?.Clear();
        }

        _disposedValue = true;
    }

    /// <summary>Finds a child node by text.</summary>
    /// <param name="nodes">The nodes to search.</param>
    /// <param name="text">The text to match.</param>
    /// <returns>The matching node.</returns>
    private static INodeEmulator? FindNode(IEnumerable<INodeEmulator>? nodes, string text)
    {
        if (nodes is null)
        {
            return null;
        }

        foreach (var node in nodes)
        {
            if (string.Equals(node.Text, text, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }
        }

        return null;
    }
}
