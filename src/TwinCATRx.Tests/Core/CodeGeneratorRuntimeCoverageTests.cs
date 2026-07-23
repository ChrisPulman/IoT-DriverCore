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

namespace IoT.DriverCore.TwinCATRx.Tests.Core;

/// <summary>Exercises code generation through the composed native symbol runtime.</summary>
public sealed class CodeGeneratorRuntimeCoverageTests
{
    /// <summary>A representative ADS port.</summary>
    private const int AdsPort = 851;

    /// <summary>The expected load count for each gateway overload family.</summary>
    private const int ExpectedLoadCount = 2;

    /// <summary>The generated assembly file name.</summary>
    private const string GeneratedAssemblyFileName = "Generated.dll";

    /// <summary>A representative native symbol value.</summary>
    private const int NativeValue = 17;

    /// <summary>A representative ADS route.</summary>
    private const string Route = "route";

    /// <summary>Verifies every native gateway overload and symbol-list rebuild branch.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Composed_Symbol_Runtime_Covers_Load_Read_And_DisposeAsync()
    {
        var runtime = new FakeCodeGeneratorRuntime();
        runtime.Symbols.Add(new FakeSymbol("Root", "DINT", DataTypeCategory.Primitive));
        using var generator = new CodeGenerator(null, runtime);

        var addressDefaultCount = generator.LoadSymbols(Route).Count;
        var addressExplicitCount = generator.LoadSymbols(Route, AdsPort).Count;
        var localCount = generator.LoadSymbols(AdsPort).Count;
        var value = generator.ReadSymbol(Route, AdsPort, ".Value", typeof(int));
        runtime.ReturnNullSymbols = true;
        var empty = generator.LoadSymbols(AdsPort);
        generator.Dispose();

        await TUnitAssert.That(addressDefaultCount).IsEqualTo(1);
        await TUnitAssert.That(addressExplicitCount).IsEqualTo(1);
        await TUnitAssert.That(localCount).IsEqualTo(1);
        await TUnitAssert.That(value).IsEqualTo(NativeValue);
        await TUnitAssert.That(empty).IsEmpty();
        await TUnitAssert.That(runtime.RemoteLoadCount).IsEqualTo(ExpectedLoadCount);
        await TUnitAssert.That(runtime.LocalLoadCount).IsEqualTo(ExpectedLoadCount);
        await TUnitAssert.That(runtime.ReadCount).IsEqualTo(1);
        await TUnitAssert.That(runtime.IsDisposed).IsTrue();
    }

    /// <summary>Verifies the null selected-node guard in the class writer.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Null_Selected_Node_Does_Not_Emit_A_ClassAsync()
    {
        using var generator = new CodeGenerator();
        var writer = typeof(CodeGenerator).GetMethod(
            "WriteCSharpClass",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(CodeGenerator).FullName, "WriteCSharpClass");
        object?[] arguments = [new StringBuilder(), null, true];

        _ = writer.Invoke(generator, arguments);

        await TUnitAssert.That(arguments[0]?.ToString()).IsEmpty();
    }

    /// <summary>Verifies generated source file success and caught file-system failure branches.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("Compiles a deterministic generated PLC structure.")]
    [RequiresUnreferencedCode("Compiles a deterministic generated PLC structure.")]
#endif
    public async Task Structured_Node_Covers_Source_And_Assembly_File_PathsAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"TwinCATRx_Runtime_{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        var failures = new List<Exception>();
        using var runtime = new FakeCodeGeneratorRuntime();
        using var generator = new CodeGenerator(failures.Add, runtime);
        using var defaultRuntimeGenerator = new CodeGenerator(failures.Add);
        using var node = CreateStructuredNode();
        try
        {
            var sourcePath = Path.Combine(directory, "Generated.cs");
            var assemblyPath = Path.Combine(directory, GeneratedAssemblyFileName);
            var invalidRawAssemblyPath = Path.Combine(directory, "Invalid.dll");
            var createdSource = generator.CreateCSharpCode(node, sourcePath, true);
            var rejectedSource = generator.CreateCSharpCode(node, "\0", false);
            var createdAssembly = generator.CreateDll(node, assemblyPath, true);
            var rejectedRawAssembly = generator.CreateDll(
                "invalid C# source",
                invalidRawAssemblyPath);

            await TUnitAssert.That(createdSource).IsTrue();
            await TUnitAssert.That(File.Exists(sourcePath)).IsTrue();
            await TUnitAssert.That(rejectedSource).IsFalse();
            await TUnitAssert.That(failures).IsNotEmpty();
            await TUnitAssert.That(createdAssembly).IsTrue();
            await TUnitAssert.That(File.Exists(assemblyPath)).IsTrue();
            await TUnitAssert.That(rejectedRawAssembly).IsFalse();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Creates one structure containing a primitive member.</summary>
    /// <returns>The node.</returns>
    private static NodeEmulator CreateStructuredNode()
    {
        var root = new NodeEmulator
        {
            Text = "Root",
            Tag = new FakeSymbol("Root", "RootType", DataTypeCategory.Struct),
        };
        var child = new NodeEmulator
        {
            Text = "Value",
            Tag = new FakeSymbol("Value", "DINT", DataTypeCategory.Primitive),
        };
        _ = root.Nodes!.Add(child);
        return root;
    }

    /// <summary>Deterministic native symbol gateway.</summary>
    private sealed class FakeCodeGeneratorRuntime : ICodeGeneratorRuntime
    {
        /// <summary>Gets deterministic symbols.</summary>
        public List<ISymbol> Symbols { get; } = [];

        /// <summary>Gets or sets a value indicating whether null symbols are returned.</summary>
        public bool ReturnNullSymbols { get; set; }

        /// <summary>Gets the remote-load count.</summary>
        public int RemoteLoadCount { get; private set; }

        /// <summary>Gets the local-load count.</summary>
        public int LocalLoadCount { get; private set; }

        /// <summary>Gets the read count.</summary>
        public int ReadCount { get; private set; }

        /// <summary>Gets a value indicating whether the gateway is disposed.</summary>
        public bool IsDisposed { get; private set; }

        /// <inheritdoc/>
        public void Dispose() => IsDisposed = true;

        /// <inheritdoc/>
        public void LoadSymbols(string adsAddress, int port, Action<IEnumerable<ISymbol>> consumeSymbols)
        {
            _ = adsAddress;
            _ = port;
            RemoteLoadCount++;
            consumeSymbols(ReturnNullSymbols ? null! : Symbols);
        }

        /// <inheritdoc/>
        public void LoadSymbols(int port, Action<IEnumerable<ISymbol>> consumeSymbols)
        {
            _ = port;
            LocalLoadCount++;
            consumeSymbols(ReturnNullSymbols ? null! : Symbols);
        }

        /// <inheritdoc/>
        public object ReadSymbol(string adsAddress, int port, string variable, Type variableType)
        {
            _ = adsAddress;
            _ = port;
            _ = variable;
            _ = variableType;
            ReadCount++;
            return NativeValue;
        }
    }

    /// <summary>Minimal deterministic TwinCAT symbol.</summary>
    private sealed class FakeSymbol : ISymbol
    {
        /// <summary>The bit count in one byte.</summary>
        private const int ByteBitSize = 8;

        /// <summary>Initializes a new instance of the <see cref="FakeSymbol"/> class.</summary>
        /// <param name="instanceName">The instance name.</param>
        /// <param name="typeName">The PLC type name.</param>
        /// <param name="category">The type category.</param>
        public FakeSymbol(string instanceName, string typeName, DataTypeCategory category)
        {
            InstanceName = instanceName;
            InstancePath = instanceName;
            TypeName = typeName;
            Category = category;
            SubSymbols = new FakeSymbolCollection();
        }

        /// <inheritdoc/>
        public DataTypeCategory Category { get; }

        /// <inheritdoc/>
        public ISymbol? Parent => null;

        /// <inheritdoc/>
        public ISymbolCollection<ISymbol> SubSymbols { get; }

        /// <inheritdoc/>
        public bool IsContainerType => Category == DataTypeCategory.Struct;

        /// <inheritdoc/>
        public bool IsPrimitiveType => Category == DataTypeCategory.Primitive;

        /// <inheritdoc/>
        public bool IsPersistent => false;

        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <inheritdoc/>
        public bool IsRecursive => false;

        /// <inheritdoc/>
        public IDataType DataType => null!;

        /// <inheritdoc/>
        public string TypeName { get; }

        /// <inheritdoc/>
        public string InstanceName { get; }

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
        public ITypeAttributeCollection Attributes => null!;

        /// <inheritdoc/>
        public Encoding ValueEncoding => Encoding.UTF8;

        /// <inheritdoc/>
        public int Size => 1;

        /// <inheritdoc/>
        public bool IsBitType => false;

        /// <inheritdoc/>
        public int BitSize => ByteBitSize;

        /// <inheritdoc/>
        public int ByteSize => 1;

        /// <inheritdoc/>
        public bool IsByteAligned => true;
    }

    /// <summary>List-backed symbol collection.</summary>
    private sealed class FakeSymbolCollection : List<ISymbol>, ISymbolCollection<ISymbol>
    {
        /// <inheritdoc/>
        public InstanceCollectionMode Mode => InstanceCollectionMode.Names;

        /// <inheritdoc/>
        public ISymbol this[string instancePath] => GetInstance(instancePath);

        /// <inheritdoc/>
        public bool Contains(string instancePath) => this.Any(symbol => symbol.InstancePath == instancePath);

        /// <inheritdoc/>
        public bool ContainsName(string instanceName) => this.Any(symbol => symbol.InstanceName == instanceName);

        /// <inheritdoc/>
        public ISymbol GetInstance(string instancePath) =>
            this.First(symbol => symbol.InstancePath == instancePath);

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
        public bool TryGetInstanceByName(
            string instanceName,
            [NotNullWhen(true)] out IList<ISymbol>? symbols)
#endif
        {
            symbols = GetInstanceByName(instanceName);
            return symbols.Count > 0;
        }
    }
}
