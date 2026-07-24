// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Reflection;
using System.Xml.Linq;
using IoT.DriverCore.Core;
using IoT.DriverCore.S7PlcRx.Advanced;
using IoT.DriverCore.S7PlcRx.Binding;
using IoT.DriverCore.S7PlcRx.Enterprise;
using IoT.DriverCore.S7PlcRx.Enums;
using IoT.DriverCore.S7PlcRx.LogicalTags;
using PlcClass = IoT.DriverCore.S7PlcRx.PlcTypes.Class;
using PlcString = IoT.DriverCore.S7PlcRx.PlcTypes.String;
using PlcStruct = IoT.DriverCore.S7PlcRx.PlcTypes.Struct;
using S7TypeConverter = IoT.DriverCore.S7PlcRx.Core.TypeConverter;
using TUnitAssert = TUnit.Assertions.Assert;

namespace IoT.DriverCore.S7PlcRx.Tests.Core;

/// <summary>Closes deterministic branches in private S7 runtime helpers.</summary>
public sealed class S7RawBranchClosureTests
{
    /// <summary>Defines a method with one parameter.</summary>
    private const int OneParameter = 1;

    /// <summary>Defines a method with two parameters.</summary>
    private const int TwoParameters = 2;

    /// <summary>Defines a method with three parameters.</summary>
    private const int ThreeParameters = 3;

    /// <summary>Defines a method with four parameters.</summary>
    private const int FourParameters = 4;

    /// <summary>Defines the tag validation helper name.</summary>
    private const string TagValueIsValidMethod = "TagValueIsValid";

    /// <summary>Defines the fixture tag name.</summary>
    private const string TagName = "Value";

    /// <summary>Defines the fixture tag name with alternative casing.</summary>
    private const string LowercaseTagName = "value";

    /// <summary>Defines the fixture tag address.</summary>
    private const string TagAddress = "DB1.DBD0";

    /// <summary>Defines the fixture tag value.</summary>
    private const int TagValue = 42;

    /// <summary>Defines the representative element count.</summary>
    private const int ElementCount = 3;

    /// <summary>Defines the byte length for three word-sized elements.</summary>
    private const int ThreeWordByteLength = 6;

    /// <summary>Defines the byte length for three double-word-sized elements.</summary>
    private const int ThreeDoubleWordByteLength = 12;

    /// <summary>Defines the byte length for three long-real elements.</summary>
    private const int ThreeLongRealByteLength = 24;

    /// <summary>Defines the aligned even offset.</summary>
    private const double EvenOffset = 2D;

    /// <summary>Defines a parsed XML length.</summary>
    private const int XmlLength = 4;

    /// <summary>Defines a parsed CSV length.</summary>
    private const int CsvLength = 2;

    /// <summary>Defines an array length large enough to use the shared byte pool.</summary>
    private const int PooledIntegerCount = 257;

    /// <summary>Defines the helper fixture variable name.</summary>
    private const string HelperVariable = "Helper";

    /// <summary>Defines a unique documentation-only endpoint for cache branch tests.</summary>
    private const string DocumentationEndpoint = "192.0.2.250";

    /// <summary>Defines a second helper tag name.</summary>
    private const string OtherVariable = "Other";

    /// <summary>Verifies tag validation and byte-length dispatch for every variable family.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AddressingHelpers_ExerciseEveryValidationAndLengthBranchAsync()
    {
        var valueOnly = GetGenericMethod(
            typeof(RxS7),
            TagValueIsValidMethod,
            OneParameter).MakeGenericMethod(typeof(int));
        var valueOnlyObject = GetGenericMethod(
            typeof(RxS7),
            TagValueIsValidMethod,
            OneParameter).MakeGenericMethod(typeof(object));
        var named = GetGenericMethod(
            typeof(RxS7),
            TagValueIsValidMethod,
            TwoParameters).MakeGenericMethod(typeof(int));
        var namedObject = GetGenericMethod(
            typeof(RxS7),
            TagValueIsValidMethod,
            TwoParameters).MakeGenericMethod(typeof(object));
        var valid = new Tag(TagName, TagAddress, TagValue, typeof(int));
        var nullValue = new Tag(TagName, TagAddress, typeof(int)) { Value = null };
        var wrongDeclaredType = new Tag(TagName, TagAddress, TagValue, typeof(uint));
        var wrongRuntimeType = new Tag(TagName, TagAddress, TagValue.ToString(), typeof(int));

        await TUnitAssert.That(Invoke<bool>(valueOnly, (object?)null)).IsFalse();
        await TUnitAssert.That(Invoke<bool>(valueOnlyObject, valid)).IsTrue();
        await TUnitAssert.That(Invoke<bool>(valueOnly, wrongDeclaredType)).IsFalse();
        await TUnitAssert.That(Invoke<bool>(valueOnly, nullValue)).IsFalse();
        await TUnitAssert.That(Invoke<bool>(valueOnly, wrongRuntimeType)).IsFalse();
        await TUnitAssert.That(Invoke<bool>(valueOnly, valid)).IsTrue();

        await TUnitAssert.That(Invoke<bool>(named, null, null)).IsFalse();
        await TUnitAssert.That(Invoke<bool>(named, valid, "Other")).IsFalse();
        await TUnitAssert.That(Invoke<bool>(namedObject, valid, LowercaseTagName)).IsTrue();
        await TUnitAssert.That(Invoke<bool>(named, wrongDeclaredType, TagName)).IsFalse();
        await TUnitAssert.That(Invoke<bool>(named, wrongRuntimeType, TagName)).IsFalse();
        await TUnitAssert.That(Invoke<bool>(named, valid, LowercaseTagName)).IsTrue();

        var byteLength = GetMethod(typeof(RxS7), "VarTypeToByteLength", TwoParameters);
        await TUnitAssert.That(Invoke<int>(byteLength, VarType.Bit, ElementCount)).IsEqualTo(ElementCount);
        await TUnitAssert.That(Invoke<int>(byteLength, VarType.String, ElementCount)).IsEqualTo(ElementCount);
        await TUnitAssert.That(Invoke<int>(byteLength, VarType.Byte, 0)).IsEqualTo(OneParameter);
        await TUnitAssert.That(Invoke<int>(byteLength, VarType.Byte, ElementCount)).IsEqualTo(ElementCount);
        await TUnitAssert.That(Invoke<int>(byteLength, VarType.Word, ElementCount)).IsEqualTo(ThreeWordByteLength);
        await TUnitAssert.That(Invoke<int>(byteLength, VarType.Timer, ElementCount)).IsEqualTo(ThreeWordByteLength);
        await TUnitAssert.That(Invoke<int>(byteLength, VarType.Int, ElementCount)).IsEqualTo(ThreeWordByteLength);
        await TUnitAssert.That(Invoke<int>(byteLength, VarType.Counter, ElementCount)).IsEqualTo(ThreeWordByteLength);
        await TUnitAssert.That(Invoke<int>(byteLength, VarType.DWord, ElementCount))
            .IsEqualTo(ThreeDoubleWordByteLength);
        await TUnitAssert.That(Invoke<int>(byteLength, VarType.DInt, ElementCount))
            .IsEqualTo(ThreeDoubleWordByteLength);
        await TUnitAssert.That(Invoke<int>(byteLength, VarType.Real, ElementCount))
            .IsEqualTo(ThreeDoubleWordByteLength);
        await TUnitAssert.That(Invoke<int>(byteLength, VarType.LReal, ElementCount))
            .IsEqualTo(ThreeLongRealByteLength);
        await TUnitAssert.That(Invoke<int>(byteLength, (VarType)int.MaxValue, ElementCount)).IsEqualTo(0);
    }

    /// <summary>Verifies defensive and alignment branches in structured class and struct conversion.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task StructuredHelpers_ExerciseDefensiveAndAlignmentBranchesAsync()
    {
        await TUnitAssert.That(() => PlcClass.GetClassSize(new NullableValueContainer()))
            .Throws<ArgumentException>();
        await TUnitAssert.That(() => PlcClass.FromBytes(new NullableValueContainer(), [0]))
            .Throws<ArgumentException>();
        await TUnitAssert.That(() => PlcStruct.FromBytes(typeof(int?), []))
            .Throws<ArgumentException>();

        var increasedSize = GetMethod(typeof(PlcClass), "GetIncreasedNumberOfBytes", ThreeParameters);
        await TUnitAssert.That(() => increasedSize.Invoke(null, [0D, typeof(string), null]))
            .Throws<TargetInvocationException>();

        var stringValue = GetMethod(typeof(PlcClass), "GetStringPropertyValue", ThreeParameters);
        object?[] stringValueArguments = [null, Array.Empty<byte>(), 0D];
        await TUnitAssert.That(() => stringValue.Invoke(null, stringValueArguments))
            .Throws<TargetInvocationException>();

        var stringBytes = GetMethod(typeof(PlcClass), "GetStringPropertyBytes", TwoParameters);
        await TUnitAssert.That(() => stringBytes.Invoke(null, [string.Empty, null]))
            .Throws<TargetInvocationException>();

        var primitiveValue = GetMethod(typeof(PlcClass), "GetPrimitivePropertyValue", ThreeParameters);
        object?[] primitiveArguments = [TypeCode.Empty, Array.Empty<byte>(), 0D];
        await TUnitAssert.That(() => primitiveValue.Invoke(null, primitiveArguments))
            .Throws<TargetInvocationException>();

        var classAlignment = GetMethod(typeof(PlcClass), "IncrementToEven", OneParameter);
        object?[] classAlignmentArguments = [1D];
        _ = classAlignment.Invoke(null, classAlignmentArguments);
        await TUnitAssert.That((double)classAlignmentArguments[0]!).IsEqualTo(EvenOffset);

        var structValue = GetGenericMethod(
            typeof(PlcStruct),
            "GetValueOrThrow",
            TwoParameters).MakeGenericMethod(typeof(int));
        var shortContainer = new ValueTuple<short>((short)OneParameter);
        var shortField = typeof(ValueTuple<short>).GetField(nameof(shortContainer.Item1))!;
        await TUnitAssert.That(() => structValue.Invoke(null, [shortField, shortContainer]))
            .Throws<TargetInvocationException>();

        var structAlignment = GetMethod(typeof(PlcStruct), "IncrementToEven", OneParameter);
        object?[] structAlignmentArguments = [1D];
        _ = structAlignment.Invoke(null, structAlignmentArguments);
        await TUnitAssert.That((double)structAlignmentArguments[0]!).IsEqualTo(EvenOffset);
    }

    /// <summary>Verifies whitespace, disposal, response, and empty-request multi-variable branches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MultiVariableHelpers_ExerciseResidualGuardBranchesAsync()
    {
        var hasNoText = GetMethod(typeof(RxS7), "HasNoText", OneParameter);
        await TUnitAssert.That(Invoke<bool>(hasNoText, (object?)null)).IsTrue();
        await TUnitAssert.That(Invoke<bool>(hasNoText, " \t")).IsTrue();
        await TUnitAssert.That(Invoke<bool>(hasNoText, "value")).IsFalse();

        var disposeSemaphore = GetMethod(typeof(RxS7), "DisposeSemaphore", TwoParameters);
        var semaphore = new SemaphoreSlim(OneParameter, OneParameter);
        semaphore.Dispose();
        await TUnitAssert.That(disposeSemaphore.Invoke(null, [semaphore, "test"])).IsNull();

        var resultsSuccessful = GetMethod(typeof(RxS7), "AreMultiVarWriteResultsSuccessful", TwoParameters);
        await TUnitAssert.That(Invoke<bool>(resultsSuccessful, Array.Empty<byte>(), OneParameter)).IsFalse();
        await TUnitAssert.That(Invoke<bool>(resultsSuccessful, Array.Empty<byte>(), 0)).IsTrue();

        var sendRequest = GetMethod(typeof(RxS7), "TrySendMultiVarRequest", FourParameters);
        await TUnitAssert.That(Invoke<bool>(
            sendRequest,
            null,
            new Tag("DB1.DBB0", typeof(byte)),
            Array.Empty<byte>(),
            new byte[OneParameter])).IsFalse();
    }

    /// <summary>Verifies enterprise symbol-type and optional field parsing branches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EnterpriseHelpers_ExerciseMappedFallbackAndOptionalFieldBranchesAsync()
    {
        var tagType = GetMethod(typeof(EnterpriseExtensions), "GetTagType", OneParameter);
        await TUnitAssert.That(Invoke<Type>(tagType, "BOOL")).IsEqualTo(typeof(bool));
        await TUnitAssert.That(Invoke<Type>(tagType, "ARRAY[0..2] OF BYTE")).IsEqualTo(typeof(byte[]));
        await TUnitAssert.That(Invoke<Type>(tagType, "UDT")).IsEqualTo(typeof(object));

        var parseJson = GetMethod(typeof(EnterpriseExtensions), "ParseJsonSymbolTable", OneParameter);
        var nullJsonTable = Invoke<SymbolTable>(parseJson, "null");
        await TUnitAssert.That(nullJsonTable.Symbols.Count).IsEqualTo(0);

        var xmlLength = GetMethod(typeof(EnterpriseExtensions), "GetXmlSymbolLength", OneParameter);
        await TUnitAssert.That(Invoke<int>(xmlLength, XElement.Parse("<Symbol><Length>4</Length></Symbol>")))
            .IsEqualTo(XmlLength);
        await TUnitAssert.That(Invoke<int>(xmlLength, XElement.Parse("<Symbol />"))).IsEqualTo(OneParameter);
        await TUnitAssert.That(Invoke<int>(xmlLength, XElement.Parse("<Symbol><Length>x</Length></Symbol>")))
            .IsEqualTo(OneParameter);

        var csvLength = GetMethod(typeof(EnterpriseExtensions), "GetCsvSymbolLength", OneParameter);
        await TUnitAssert.That(Invoke<int>(csvLength, (object)new[] { "A", "B", "BOOL" }))
            .IsEqualTo(OneParameter);
        await TUnitAssert.That(Invoke<int>(
            csvLength,
            (object)new[] { "A", "B", "BOOL", CsvLength.ToString() })).IsEqualTo(CsvLength);
        await TUnitAssert.That(Invoke<int>(csvLength, (object)new[] { "A", "B", "BOOL", "x" }))
            .IsEqualTo(OneParameter);

        var csvDescription = GetMethod(typeof(EnterpriseExtensions), "GetCsvSymbolDescription", OneParameter);
        await TUnitAssert.That(Invoke<string>(csvDescription, (object)new[] { "A", "B", "BOOL" }))
            .IsEqualTo(string.Empty);
        await TUnitAssert.That(Invoke<string>(
            csvDescription,
            (object)new[] { "A", "B", "BOOL", "1", "\"Description\"" })).IsEqualTo("Description");
    }

    /// <summary>Verifies mapped, runtime-resolved, missing, scalar, and array logical type branches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LogicalTypeHelpers_ExerciseMappedRuntimeMissingAndArrayBranchesAsync()
    {
        var resolveScalarType = GetMethod(typeof(S7LogicalTagClient), "ResolveScalarType", TwoParameters);
        await TUnitAssert.That(Invoke<Type>(resolveScalarType, "WORD", "WORD")).IsEqualTo(typeof(ushort));
        await TUnitAssert.That(Invoke<Type>(resolveScalarType, "GUID", "System.Guid")).IsEqualTo(typeof(Guid));
        await TUnitAssert.That(Invoke<Type>(resolveScalarType, "MISSING", "Missing.Type"))
            .IsEqualTo(typeof(object));

        var resolveType = GetMethod(typeof(S7LogicalTagClient), "ResolveType", OneParameter);
        await TUnitAssert.That(Invoke<Type>(
            resolveType,
            new LogicalTag(HelperVariable, TagAddress, "System.Int32[]"))).IsEqualTo(typeof(int[]));
        await TUnitAssert.That(Invoke<Type>(
            resolveType,
            new LogicalTag(HelperVariable, TagAddress, "Missing.Type[]"))).IsEqualTo(typeof(object));
    }

    /// <summary>Verifies binding guards, buffer pooling, and string conversion branches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BindingAndConversionHelpers_ExerciseResidualGuardAndPoolingBranchesAsync()
    {
        var invalidName = () => new S7TagDefinition(
            string.Empty,
            TagAddress,
            typeof(int),
            OneParameter,
            S7TagDirection.ReadWrite,
            OneParameter);
        var invalidAddress = () => new S7TagDefinition(
            HelperVariable,
            string.Empty,
            typeof(int),
            OneParameter,
            S7TagDirection.ReadWrite,
            OneParameter);
        var invalidType = () => new S7TagDefinition(
            HelperVariable,
            TagAddress,
            null!,
            OneParameter,
            S7TagDirection.ReadWrite,
            OneParameter);
        await TUnitAssert.That(invalidName).Throws<ArgumentNullException>();
        await TUnitAssert.That(invalidAddress).Throws<ArgumentNullException>();
        await TUnitAssert.That(invalidType).Throws<ArgumentNullException>();

        var writeOnly = new S7TagDefinition(
            HelperVariable,
            TagAddress,
            typeof(int),
            OneParameter,
            S7TagDirection.WriteOnly,
            OneParameter);
        var zeroInterval = new S7TagDefinition(
            HelperVariable,
            TagAddress,
            typeof(int),
            0,
            S7TagDirection.ReadWrite,
            OneParameter);
        var readOnly = new S7TagDefinition(
            HelperVariable,
            TagAddress,
            typeof(int),
            OneParameter,
            S7TagDirection.ReadOnly,
            OneParameter);
        await TUnitAssert.That(writeOnly.CanRead).IsFalse();
        await TUnitAssert.That(zeroInterval.CanRead).IsFalse();
        await TUnitAssert.That(readOnly.CanWrite).IsFalse();
    }

    /// <summary>Verifies pooled value conversion and empty string conversion branches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ConversionHelpers_ExercisePooledAndEmptyBranchesAsync()
    {
        var pooledValues = Enumerable.Repeat(TagValue, PooledIntegerCount).ToArray();
        var pooledBytes = S7TypeConverter.ToByteArray(pooledValues, BitConverter.GetBytes);
        await TUnitAssert.That(pooledBytes.Length).IsEqualTo(PooledIntegerCount * sizeof(int));
        await TUnitAssert.That(S7TypeConverter.ToByteArray<int>([], BitConverter.GetBytes).Length)
            .IsEqualTo(0);

        await TUnitAssert.That(PlcString.FromByteArray(null!, 0, 0)).IsEqualTo(string.Empty);
        await TUnitAssert.That(PlcString.FromSpan(ReadOnlySpan<byte>.Empty)).IsEqualTo(string.Empty);
        await TUnitAssert.That(PlcString.ToByteArray(null).Length).IsEqualTo(0);
        await TUnitAssert.That(PlcString.TryToSpan(null, Span<byte>.Empty, out var bytesWritten)).IsTrue();
        await TUnitAssert.That(bytesWritten).IsEqualTo(0);
    }

    /// <summary>Verifies advanced tag validation, database grouping, and recommendation branches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AdvancedHelpers_ExerciseValidationGroupingAndRecommendationBranchesAsync()
    {
        var advancedTagValidation = GetGenericMethod(
            typeof(AdvancedExtensions),
            TagValueIsValidMethod,
            OneParameter).MakeGenericMethod(typeof(int));
        var advancedObjectValidation = GetGenericMethod(
            typeof(AdvancedExtensions),
            TagValueIsValidMethod,
            OneParameter).MakeGenericMethod(typeof(object));
        var valid = new Tag(TagName, TagAddress, TagValue, typeof(int));
        await TUnitAssert.That(Invoke<bool>(advancedTagValidation, (object?)null)).IsFalse();
        await TUnitAssert.That(Invoke<bool>(advancedObjectValidation, valid)).IsTrue();
        await TUnitAssert.That(Invoke<bool>(
            advancedTagValidation,
            new Tag(TagName, TagAddress, TagValue, typeof(uint)))).IsFalse();
        await TUnitAssert.That(Invoke<bool>(
            advancedTagValidation,
            new Tag(TagName, TagAddress, typeof(int)) { Value = null })).IsFalse();

        var extractDataBlock = GetMethod(typeof(AdvancedExtensions), "ExtractDataBlockId", OneParameter);
        await TUnitAssert.That(Invoke<string>(extractDataBlock, "DB1")).IsEqualTo("SYSTEM");
        await TUnitAssert.That(Invoke<string>(extractDataBlock, TagAddress)).IsEqualTo("DB1");

        var analyzeDistribution = GetMethod(
            typeof(AdvancedExtensions),
            "AnalyzeDataBlockDistribution",
            OneParameter);
        var distribution = Invoke<Dictionary<string, int>>(
            analyzeDistribution,
            (object)(Tag[])
            [
                new Tag(TagAddress, typeof(int)),
                new Tag("DB1.DBD4", typeof(int)),
            ]);
        await TUnitAssert.That(distribution["DB1"]).IsEqualTo(TwoParameters);

        var performanceRecommendations = GetMethod(
            typeof(AdvancedExtensions),
            "GeneratePerformanceRecommendations",
            TwoParameters);
        ConcurrentDictionary<string, int> middleRate = [];
        middleRate[HelperVariable] = ElementCount;
        _ = Invoke<List<string>>(performanceRecommendations, middleRate, TimeSpan.FromMinutes(OneParameter));
        var emptyRecommendations = Invoke<List<string>>(
            performanceRecommendations,
            (ConcurrentDictionary<string, int>)[],
            TimeSpan.FromMinutes(OneParameter));
        await TUnitAssert.That(emptyRecommendations.Count).IsEqualTo(0);
    }

    /// <summary>Verifies asynchronous cache, multi-variable, and symbol-table helper branches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [NotInParallel]
    public async Task AsyncAndEnterpriseHelpers_ExerciseCacheMultiVarAndSymbolBranchesAsync()
    {
        var valid = new Tag(TagName, TagAddress, TagValue, typeof(int));
        using var testPlc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        testPlc.TagList.Add(valid);
        var tryGetCurrentValue = GetGenericMethod(
            typeof(AsyncExtensions),
            "TryGetCurrentValue",
            ThreeParameters).MakeGenericMethod(typeof(object));
        object?[] currentValueArguments = [testPlc, TagName, null];
        await TUnitAssert.That(Invoke<bool>(tryGetCurrentValue, currentValueArguments)).IsTrue();
        await TUnitAssert.That(currentValueArguments[TwoParameters]).IsEqualTo(TagValue);

        var asyncHasNoText = GetMethod(typeof(AsyncExtensions), "HasNoText", OneParameter);
        await TUnitAssert.That(Invoke<bool>(asyncHasNoText, " \t")).IsTrue();
        await TUnitAssert.That(Invoke<bool>(asyncHasNoText, HelperVariable)).IsFalse();

        var tryWriteMultiVar = GetGenericMethod(
            typeof(AsyncExtensions),
            "TryWriteMultiVar",
            TwoParameters).MakeGenericMethod(typeof(int));
        using var disconnected = new RxS7(
            new(new(CpuType.S71500, DocumentationEndpoint, 0, OneParameter), new(OneParameter)));
        await TUnitAssert.That(Invoke<bool>(
            tryWriteMultiVar,
            disconnected,
            new Dictionary<string, int> { [HelperVariable] = TagValue })).IsFalse();

        var tryReadMultiVar = GetGenericMethod(
            typeof(AsyncExtensions),
            "TryReadMultiVar",
            ThreeParameters).MakeGenericMethod(typeof(int));
        object?[] missingReadArguments = [disconnected, new[] { HelperVariable }, null];
        await TUnitAssert.That(Invoke<bool>(tryReadMultiVar, missingReadArguments)).IsFalse();

        var getSymbolTable = GetMethod(typeof(EnterpriseExtensions), "GetSymbolTable", OneParameter);
        await TUnitAssert.That(getSymbolTable.Invoke(null, [disconnected])).IsNull();
        _ = await EnterpriseExtensions.LoadSymbolTableAsync(
            disconnected,
            "\"Known\",\"DB1.DBD0\",\"DINT\"");
        await TUnitAssert.That(getSymbolTable.Invoke(null, [disconnected])).IsNotNull();
        var requiredSymbol = GetMethod(typeof(EnterpriseExtensions), "GetRequiredSymbol", TwoParameters);
        await TUnitAssert.That(() => requiredSymbol.Invoke(null, [disconnected, "Unknown"]))
            .Throws<TargetInvocationException>();
    }

    /// <summary>Verifies logical-client null guards, write fallback, and observation filtering.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [NotInParallel]
    public async Task LogicalClient_ExercisesNullWriteAndObservationBranchesAsync()
    {
        using var testPlc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        using var catalog = new LogicalTagCatalog();
        await TUnitAssert.That(() => new S7LogicalTagClient(null!, catalog)).Throws<ArgumentNullException>();
        await TUnitAssert.That(() => new S7LogicalTagClient(testPlc, null!)).Throws<ArgumentNullException>();

        var logicalTag = new LogicalTag(HelperVariable, TagAddress, "DINT");
        catalog.Upsert(logicalTag);
        using var client = new S7LogicalTagClient(testPlc, catalog);
        await TUnitAssert.That(() => client.RegisterTag(null!)).Throws<ArgumentNullException>();
        await TUnitAssert.That(async () => await client.InitializeStoreAsync(null!))
            .Throws<ArgumentNullException>();
        await TUnitAssert.That(async () => await client.WriteAsync(null!)).Throws<ArgumentNullException>();

        var observedCount = 0;
        using var subscription = client.ObserveMany([HelperVariable]).Subscribe(_ => observedCount++);
        testPlc.ObserveAllSubject.OnNext(null);
        testPlc.ObserveAllSubject.OnNext(new Tag(TagAddress, typeof(int)) { Name = null });
        testPlc.ObserveAllSubject.OnNext(new Tag(OtherVariable, TagAddress, TagValue, typeof(int)));
        testPlc.ObserveAllSubject.OnNext(new Tag(HelperVariable, TagAddress, TagValue, typeof(int)));
        await TUnitAssert.That(observedCount).IsEqualTo(OneParameter);

        using var disconnected = new RxS7(
            new(new(CpuType.S71500, DocumentationEndpoint, 0, OneParameter), new(OneParameter)));
        using var disconnectedCatalog = new LogicalTagCatalog();
        disconnectedCatalog.Upsert(logicalTag);
        using var disconnectedClient = new S7LogicalTagClient(disconnected, disconnectedCatalog);
        var writeResult = await disconnectedClient.WriteAsync(
            new LogicalTagValue(HelperVariable, TagValue, TimeProvider.System.GetUtcNow()));
        await TUnitAssert.That(writeResult.Succeeded).IsFalse();
    }

    /// <summary>Verifies residual RxS7 value, tag sequence, async cache, and multi-read branches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [NotInParallel]
    public async Task ValueSequenceAndAsyncHelpers_ExerciseResidualCompatibilityBranchesAsync()
    {
        using var disconnected = new RxS7(
            new(new(CpuType.S71500, DocumentationEndpoint, 0, OneParameter), new(OneParameter)));
        disconnected.Value(null, TagValue);
        disconnected.Value(HelperVariable, TagValue);
        var registered = (Tag)TagOperations
            .AddUpdateTagItem(disconnected, typeof(int), HelperVariable, TagAddress).Tag!;
        disconnected.Value<object>(HelperVariable, null);
        registered.Type = typeof(uint);
        disconnected.Value(HelperVariable, TagValue);
        disconnected.Value<object>(HelperVariable, TagValue);

        var tagSequence = GetMethod(typeof(TagOperations), "CreateTagValueSequence", OneParameter);
        await TUnitAssert.That(tagSequence.Invoke(null, [(object?)null])).IsNotNull();
        await TUnitAssert.That(tagSequence.Invoke(
            null,
            [new Tag(TagAddress, typeof(int)) { Name = null, Value = TagValue }])).IsNotNull();
        await TUnitAssert.That(tagSequence.Invoke(
            null,
            [new Tag(TagName, TagAddress, typeof(int)) { Value = null }])).IsNotNull();
        await TUnitAssert.That(tagSequence.Invoke(
            null,
            [new Tag(TagName, TagAddress, TagValue, typeof(int))])).IsNotNull();

        using var testPlc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        var currentValue = GetGenericMethod(
            typeof(AsyncExtensions),
            "TryGetCurrentValue",
            ThreeParameters).MakeGenericMethod(typeof(int));
        await TUnitAssert.That(InvokeCurrentValue(currentValue, testPlc, HelperVariable)).IsFalse();
        testPlc.TagList.Add(new Tag(HelperVariable, TagAddress, typeof(int)) { Value = null });
        await TUnitAssert.That(InvokeCurrentValue(currentValue, testPlc, HelperVariable)).IsFalse();
        testPlc.TagList[HelperVariable]!.Value = TagValue.ToString();
        await TUnitAssert.That(InvokeCurrentValue(currentValue, testPlc, HelperVariable)).IsFalse();
        testPlc.TagList[HelperVariable]!.Value = TagValue;
        await TUnitAssert.That(InvokeCurrentValue(currentValue, testPlc, HelperVariable)).IsTrue();

        using var cancellation = new CancellationTokenSource();
        var deferred = await AsyncExtensions.ReadValueAsync<int>(
            testPlc,
            default,
            OtherVariable,
            cancellation.Token);
        await TUnitAssert.That(deferred).IsEqualTo(default(int));

        registered.Type = typeof(int);
        var multiRead = GetGenericMethod(
            typeof(AsyncExtensions),
            "TryReadMultiVar",
            ThreeParameters).MakeGenericMethod(typeof(int));
        object?[] multiReadArguments = [disconnected, new[] { HelperVariable }, null];
        await TUnitAssert.That(Invoke<bool>(multiRead, multiReadArguments)).IsFalse();
    }

    /// <summary>Verifies the slow-changing-tag performance recommendation branch.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AdvancedRecommendations_ExerciseSlowChangingTagBranchAsync()
    {
        var recommendations = GetMethod(
            typeof(AdvancedExtensions),
            "GeneratePerformanceRecommendations",
            TwoParameters);
        ConcurrentDictionary<string, int> slowChanges = [];
        slowChanges[HelperVariable] = 0;
        await TUnitAssert.That(Invoke<List<string>>(
            recommendations,
            slowChanges,
            TimeSpan.FromMinutes(OneParameter)).Count).IsEqualTo(OneParameter);
    }

    /// <summary>Invokes the asynchronous current-value helper with an out-value slot.</summary>
    /// <param name="method">The closed generic current-value method.</param>
    /// <param name="plc">The test PLC.</param>
    /// <param name="name">The tag name.</param>
    /// <returns>The helper result.</returns>
    private static bool InvokeCurrentValue(
        MethodInfo method,
        S7PlcRxAsyncExtensionsTests.TestPlc plc,
        string name)
    {
        object?[] arguments = [plc, name, null];
        return Invoke<bool>(method, arguments);
    }

    /// <summary>Gets a private static method with an exact parameter count.</summary>
    /// <param name="type">The declaring type.</param>
    /// <param name="name">The method name.</param>
    /// <param name="parameterCount">The parameter count.</param>
    /// <returns>The matching method.</returns>
    private static MethodInfo GetMethod(Type type, string name, int parameterCount) =>
        type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(method =>
                method.Name == name &&
                !method.IsGenericMethodDefinition &&
                method.GetParameters().Length == parameterCount);

    /// <summary>Gets a private generic static method with an exact parameter count.</summary>
    /// <param name="type">The declaring type.</param>
    /// <param name="name">The method name.</param>
    /// <param name="parameterCount">The parameter count.</param>
    /// <returns>The matching generic method definition.</returns>
    private static MethodInfo GetGenericMethod(Type type, string name, int parameterCount) =>
        type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(method =>
                method.Name == name &&
                method.IsGenericMethodDefinition &&
                method.GetParameters().Length == parameterCount);

    /// <summary>Invokes a static method and casts its result.</summary>
    /// <typeparam name="T">The expected result type.</typeparam>
    /// <param name="method">The method to invoke.</param>
    /// <param name="arguments">The method arguments.</param>
    /// <returns>The typed invocation result.</returns>
    private static T Invoke<T>(MethodInfo method, params object?[] arguments) =>
        (T)method.Invoke(null, arguments)!;

    /// <summary>Provides a nullable nested property that cannot be activated as an object.</summary>
    private sealed class NullableValueContainer
    {
        /// <summary>Gets or sets the nullable value.</summary>
        public int? Value { get; set; }
    }
}
