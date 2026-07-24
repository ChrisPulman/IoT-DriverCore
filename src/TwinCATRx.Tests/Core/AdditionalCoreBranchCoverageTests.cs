// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if !NETFRAMEWORK
using System.Diagnostics.CodeAnalysis;
#endif
using System.Reflection;
using System.Text;
using IoT.DriverCore.TwinCATRx.Core;
using TwinCAT.TypeSystem;
using CoreTwinCatRxExtensions = IoT.DriverCore.TwinCATRx.Core.TwinCatRxExtensions;

namespace IoT.DriverCore.TwinCATRx.Tests.Core;

/// <summary>Exercises deterministic remaining core guard branches without an ADS connection.</summary>
public sealed class AdditionalCoreBranchCoverageTests
{
    /// <summary>The namespace passed to generated code methods.</summary>
    private const string GeneratedNamespace = "Generated";

    /// <summary>The representative primitive member name.</summary>
    private const string ValueName = "Value";

    /// <summary>The representative empty node text.</summary>
    private const string EmptyNodeName = "Empty";

    /// <summary>The representative generated root type name.</summary>
    private const string RootTypeName = "RootType";

    /// <summary>The private array-emitter method name.</summary>
    private const string CreateArrayOfStructureMethodName = "CreateArrayOFStructure";

    /// <summary>Verifies settings defaults reject a missing prototype and initialise every default collection.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Settings_Defaults_Covers_Guard_And_Default_CollectionsAsync()
    {
        var settings = new Settings();

        await TUnitAssert.That(() => settings.Defaults<Settings>(null!)).Throws<ArgumentNullException>();

        var defaults = settings.Defaults(settings);
        await TUnitAssert.That(defaults.SettingsId).IsEqualTo("Defaults");
        await TUnitAssert.That(defaults.Notifications).Count().IsEqualTo(1);
        await TUnitAssert.That(defaults.WriteVariables).Count().IsEqualTo(1);
    }

    /// <summary>Verifies missing assembly paths return no loaded assembly or type.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Assembly_Helpers_Return_Null_For_A_Missing_FileAsync()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"TwinCATRx_Missing_{Guid.NewGuid():N}.dll");

        await TUnitAssert.That(CoreTwinCatRxExtensions.AssemblyLoad(missingPath)).IsNull();
        await TUnitAssert.That(CoreTwinCatRxExtensions.GetType(missingPath, "Missing.Type")).IsNull();
    }

    /// <summary>Verifies private symbol traversal guards tolerate missing nodes and non-symbol tags.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Symbol_Traversal_Private_Guards_Are_DeterministicAsync()
    {
        using var generator = new CodeGenerator();
        var findNextNode = GetPrivateInstanceMethod("FindNextNode");
        var writeClasses = GetPrivateInstanceMethod("WriteCSharpClasses");
        var buildSymbolList = GetPrivateInstanceMethod("BuildSymbolList");
        var empty = new TestNode(EmptyNodeName, null);
        var nonSymbol = new TestNode("NonSymbol", new object());
        var root = new TestNode("Root", null, empty, nonSymbol);
        var builder = new StringBuilder();

        var noChildren = findNextNode.Invoke(generator, [empty]);
        var noEligibleChildren = findNextNode.Invoke(generator, [root]);
        _ = writeClasses.Invoke(generator, [builder, root, false]);
        _ = buildSymbolList.Invoke(generator, [null]);

        await TUnitAssert.That(noChildren).IsNull();
        await TUnitAssert.That(noEligibleChildren).IsNull();
        await TUnitAssert.That(builder.ToString()).IsEmpty();
        await TUnitAssert.That(generator.SymbolList).IsEmpty();
    }

    /// <summary>Verifies private source emission rejects an empty root before file creation.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Source_Emission_Rejects_Empty_Selected_NodeAsync()
    {
        using var generator = new CodeGenerator();
        var createFile = GetPrivateInstanceMethod("CreateCsharpCodeFile");
        var arguments = new object?[] { new StringBuilder(), new TestNode(EmptyNodeName, null), GeneratedNamespace, false };

        var invocation = () => createFile.Invoke(generator, arguments);

        await TUnitAssert.That(invocation).Throws<TargetInvocationException>();
        await TUnitAssert.That(GetRequired<StringBuilder>(arguments[0]).ToString()).IsEmpty();
    }

    /// <summary>Verifies nested symbols produce deterministic classes and de-duplicate generated types.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Nested_Symbols_Emit_Classes_And_Deduplicate_TypesAsync()
    {
        using var generator = new CodeGenerator();
        var value = new TestNode(ValueName, CreateSymbol(ValueName, "DINT"));
        var child = new TestNode("Child", CreateSymbol("Child", "ChildType", DataTypeCategory.Struct), value);
        var root = new TestNode("Root", CreateSymbol("Root", RootTypeName, DataTypeCategory.Struct), child);

        var source = generator.CreateCSharpCodeString(root, isTwinCat3: true, GeneratedNamespace);

        await TUnitAssert.That(source).Contains($"namespace {GeneratedNamespace}");
        await TUnitAssert.That(source).Contains($"public class {RootTypeName}");
        await TUnitAssert.That(source).Contains("public class ChildType");
        await TUnitAssert.That(source).Contains("Pack = 0");
    }

    /// <summary>Verifies private emission paths for duplicate classes, arrays, file errors, and null searches.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Private_Emitter_Guards_And_Array_Wrappers_Are_DeterministicAsync()
    {
        using var generator = new CodeGenerator();
        var root = new TestNode(
            "Root",
            CreateSymbol("Root", RootTypeName, DataTypeCategory.Struct),
            new TestNode(ValueName, CreateSymbol(ValueName, "DINT")));
        var writer = GetPrivateInstanceMethod("WriteCSharpClass");
        var findNode = typeof(CodeGenerator).GetMethod("FindNode", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(CodeGenerator).FullName, "FindNode");
        var createArray = typeof(CodeGenerator).GetMethod(
            CreateArrayOfStructureMethodName,
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(CodeGenerator).FullName, CreateArrayOfStructureMethodName);
        var builder = new StringBuilder();
        var writerArguments = new object?[] { builder, root, false };
        var wrapperBuilder = new StringBuilder();
        var arraySymbol = CreateSymbol("Labels", "ARRAY [0..1] OF STRING(4)");

        _ = writer.Invoke(generator, writerArguments);
        _ = writer.Invoke(generator, writerArguments);
        var firstArray = GetRequired<string>(createArray.Invoke(null, [arraySymbol, wrapperBuilder, false]));
        var duplicateArray = GetRequired<string>(createArray.Invoke(null, [arraySymbol, wrapperBuilder, true]));
        var found = findNode.Invoke(null, [null, "Missing"]);

        var failures = new List<Exception>();
        using var errorGenerator = new CodeGenerator(failures.Add);
        var result = errorGenerator.CreateCSharpCode(root, Path.GetTempPath(), false, GeneratedNamespace);

        await TUnitAssert.That(builder.ToString()).Contains($"public class {RootTypeName}");
        await TUnitAssert.That(firstArray).Contains("STRING_4_WRAPPER[] Labels");
        await TUnitAssert.That(duplicateArray).Contains("STRING_4_WRAPPER[] Labels");
        await TUnitAssert.That(wrapperBuilder.ToString()).Contains("ToStringArray");
        await TUnitAssert.That(found).IsNull();
        await TUnitAssert.That(result).IsFalse();
        await TUnitAssert.That(failures).Count().IsEqualTo(1);
    }

    /// <summary>Verifies nullable node collections and the no-error-sink file failure path remain deterministic.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Nullable_Nodes_And_Unhandled_File_Failure_Cover_Generator_GuardsAsync()
    {
        using var generator = new CodeGenerator();
        var nullNodes = new NullNodesTestNode { Text = "Null" };
        var root = new TestNode(
            "Root",
            CreateSymbol("Root", RootTypeName, DataTypeCategory.Struct),
            new TestNode(ValueName, CreateSymbol(ValueName, "DINT")));

        await TUnitAssert.That(() => generator.CreateCSharpCodeString(nullNodes))
            .Throws<SimpleTypeException>();
        await TUnitAssert.That(() => generator.CreateCSharpCode(nullNodes, "Unused.cs", false))
            .Throws<SimpleTypeException>();

        var failedWrite = generator.CreateCSharpCode(root, Path.GetTempPath(), false, GeneratedNamespace);

        await TUnitAssert.That(failedWrite).IsFalse();
    }

    /// <summary>Verifies array emission rejects a missing instance name without creating a wrapper.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Array_Emission_Rejects_A_Missing_Instance_NameAsync()
    {
        var createArray = typeof(CodeGenerator).GetMethod(
            CreateArrayOfStructureMethodName,
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(CodeGenerator).FullName, CreateArrayOfStructureMethodName);
        var wrapperBuilder = new StringBuilder();
        var symbol = CreateSymbol("Ignored", "ARRAY [0..1] OF STRING(4)");
        ((TestSymbol)symbol).InstanceName = null!;

        var emitted = GetRequired<string>(createArray.Invoke(null, [symbol, wrapperBuilder, false]));

        await TUnitAssert.That(emitted).IsEmpty();
        await TUnitAssert.That(wrapperBuilder.ToString()).IsEmpty();
    }

    /// <summary>Verifies each public generator guard family distinguishes empty and nullable node collections.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Public_Generator_Guard_Combinations_Are_StableAsync()
    {
        using var generator = new CodeGenerator();
        var empty = new TestNode(EmptyNodeName, null);
        var nullNodes = new NullNodesTestNode { Text = "Null" };
        var outputPath = Path.Combine(Path.GetTempPath(), $"TwinCATRx_Guard_{Guid.NewGuid():N}.dll");

        var code = generator.CreateCSharpCodeString(empty);
        var rejectedCodeFile = generator.CreateCSharpCode(empty, "Unused.cs", false);
        var rejectedDll = generator.CreateDll(empty, outputPath, false, GeneratedNamespace);
        await TUnitAssert.That(() => generator.CreateDll(nullNodes, outputPath, false, GeneratedNamespace))
            .Throws<SimpleTypeException>();

        await TUnitAssert.That(code).IsEqualTo(string.Empty);
        await TUnitAssert.That(rejectedCodeFile).IsFalse();
        await TUnitAssert.That(rejectedDll).IsFalse();
        await TUnitAssert.That(File.Exists(outputPath)).IsFalse();
    }

    /// <summary>Gets a private instance method from the code generator.</summary>
    /// <param name="name">The method name.</param>
    /// <returns>The method.</returns>
    private static MethodInfo GetPrivateInstanceMethod(string name) =>
        typeof(CodeGenerator).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(typeof(CodeGenerator).FullName, name);

    /// <summary>Returns a required value.</summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The typed value.</returns>
    private static T GetRequired<T>(object? value)
        where T : class => value as T ?? throw new InvalidOperationException("Expected value was missing.");

    /// <summary>Creates a minimal TwinCAT symbol used exclusively by the source emitter.</summary>
    /// <param name="instanceName">The symbol instance name.</param>
    /// <param name="typeName">The PLC type name.</param>
    /// <param name="category">The TwinCAT type category.</param>
    /// <returns>The configured symbol.</returns>
    private static TestSymbol CreateSymbol(
        string instanceName,
        string typeName,
        DataTypeCategory category = DataTypeCategory.Primitive)
    {
        return new TestSymbol(instanceName, typeName, category);
    }

    /// <summary>Supplies the TwinCAT symbol contract consumed by the deterministic code emitter.</summary>
    private sealed class TestSymbol : ISymbol
    {
        /// <summary>The number of bits in a byte.</summary>
        private const int ByteBitSize = 8;

        /// <summary>Initializes a new instance of the <see cref="TestSymbol"/> class.</summary>
        /// <param name="instanceName">The instance name.</param>
        /// <param name="typeName">The PLC type name.</param>
        /// <param name="category">The data type category.</param>
        public TestSymbol(string instanceName, string typeName, DataTypeCategory category)
        {
            InstanceName = instanceName;
            InstancePath = instanceName;
            TypeName = typeName;
            Category = category;
        }

        /// <inheritdoc/>
        public DataTypeCategory Category { get; }

        /// <inheritdoc/>
        public ISymbol? Parent => null;

        /// <inheritdoc/>
        public ISymbolCollection<ISymbol> SubSymbols { get; } = new TestSymbolCollection([]);

        /// <inheritdoc/>
        public bool IsContainerType => Category is DataTypeCategory.Struct or DataTypeCategory.Array;

        /// <inheritdoc/>
        public bool IsPrimitiveType => Category == DataTypeCategory.Primitive;

        /// <inheritdoc/>
        public bool IsPersistent => false;

        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <inheritdoc/>
        public bool IsRecursive => false;

        /// <inheritdoc/>
        public IDataType DataType => throw new NotSupportedException();

        /// <inheritdoc/>
        public string TypeName { get; }

        /// <inheritdoc/>
        public string InstanceName { get; set; }

        /// <inheritdoc/>
        public string InstancePath { get; }

        /// <inheritdoc/>
        public bool IsStatic => false;

        /// <inheritdoc/>
        public bool IsReference => false;

        /// <inheritdoc/>
        public bool IsPointer => false;

        /// <inheritdoc/>
        public string Comment => string.Empty;

        /// <inheritdoc/>
        public bool IsProperty => false;

        /// <inheritdoc/>
        public ITypeAttributeCollection Attributes => throw new NotSupportedException();

        /// <inheritdoc/>
        public Encoding ValueEncoding => Encoding.UTF8;

        /// <inheritdoc/>
        public int Size => ByteSize;

        /// <inheritdoc/>
        public bool IsBitType => false;

        /// <inheritdoc/>
        public int BitSize => ByteBitSize;

        /// <inheritdoc/>
        public int ByteSize => 1;

        /// <inheritdoc/>
        public bool IsByteAligned => true;
    }

    /// <summary>List-backed TwinCAT symbol collection used by <see cref="TestSymbol"/>.</summary>
    private sealed class TestSymbolCollection : List<ISymbol>, ISymbolCollection<ISymbol>
    {
        /// <summary>Initializes a new instance of the <see cref="TestSymbolCollection"/> class.</summary>
        /// <param name="symbols">The symbols to copy.</param>
        public TestSymbolCollection(IEnumerable<ISymbol> symbols)
            : base(symbols)
        {
        }

        /// <inheritdoc/>
        public InstanceCollectionMode Mode => InstanceCollectionMode.Names;

        /// <inheritdoc/>
        public ISymbol this[string instancePath] => GetInstance(instancePath);

        /// <inheritdoc/>
        public bool Contains(string instancePath) => this.Any(symbol => symbol.InstancePath == instancePath);

        /// <inheritdoc/>
        public bool ContainsName(string instanceName) => this.Any(symbol => symbol.InstanceName == instanceName);

        /// <inheritdoc/>
        public ISymbol GetInstance(string instancePath) => this.First(symbol => symbol.InstancePath == instancePath);

        /// <inheritdoc/>
        public IList<ISymbol> GetInstanceByName(string instanceName) =>
            this.Where(symbol => symbol.InstanceName == instanceName).ToList();

        /// <inheritdoc/>
#if NETFRAMEWORK
        public bool TryGetInstance(string instancePath, out ISymbol symbol)
#else
        public bool TryGetInstance(string instancePath, [NotNullWhen(true)] out ISymbol? symbol)
#endif
        {
            symbol = this.FirstOrDefault(candidate => candidate.InstancePath == instancePath);
            return symbol is not null;
        }

        /// <inheritdoc/>
#if NETFRAMEWORK
        public bool TryGetInstanceByName(string instanceName, out IList<ISymbol> symbols)
#else
        public bool TryGetInstanceByName(string instanceName, [NotNullWhen(true)] out IList<ISymbol>? symbols)
#endif
        {
            symbols = GetInstanceByName(instanceName);
            return symbols.Count > 0;
        }
    }

    /// <summary>Minimal deterministic tree node for generator guard coverage.</summary>
    private sealed class TestNode : INodeEmulator
    {
        /// <summary>Initializes a new instance of the <see cref="TestNode"/> class.</summary>
        /// <param name="text">The node text.</param>
        /// <param name="tag">The node tag.</param>
        /// <param name="children">The child nodes.</param>
        public TestNode(string text, object? tag, params TestNode[] children)
        {
            Text = text;
            Tag = tag;
            Nodes = children.Cast<INodeEmulator>().ToHashSet();
        }

        /// <inheritdoc/>
        public HashSet<INodeEmulator>? Nodes { get; }

        /// <inheritdoc/>
        public object? Tag { get; set; }

        /// <inheritdoc/>
        public string Text { get; set; } = string.Empty;

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }

    /// <summary>Represents a deliberately incomplete external node for nullable guard coverage.</summary>
    private sealed class NullNodesTestNode : INodeEmulator
    {
        /// <inheritdoc/>
        public HashSet<INodeEmulator>? Nodes => null;

        /// <inheritdoc/>
        public object? Tag { get; set; }

        /// <inheritdoc/>
        public string Text { get; set; } = string.Empty;

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }
}
