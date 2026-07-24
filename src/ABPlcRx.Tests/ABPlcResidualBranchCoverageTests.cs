// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using IoT.DriverCore.Core;
using Microsoft.CodeAnalysis;
using TUnit.Assertions;
using TUnit.Core;

namespace IoT.DriverCore.ABPlcRx.Tests;

/// <summary>Exercises deterministic residual branches in the AB simulator and native wrappers.</summary>
public sealed class ABPlcResidualBranchCoverageTests
{
    /// <summary>The logical variable used by operation-metric coverage.</summary>
    private const string MetricVariable = "Metric";

    /// <summary>The physical tag used by operation-metric coverage.</summary>
    private const string MetricTagName = "N7:0";

    /// <summary>The loopback endpoint used by direct native-wrapper tests.</summary>
    private const string LoopbackAddress = "127.0.0.1";

    /// <summary>The value written by operation-metric coverage.</summary>
    private const int MetricValue = 7;

    /// <summary>The Boolean primitive tag name.</summary>
    private const string BooleanTagName = "Boolean";

    /// <summary>The byte primitive tag name.</summary>
    private const string ByteTagName = "Byte";

    /// <summary>The signed-byte primitive tag name.</summary>
    private const string SByteTagName = "SByte";

    /// <summary>The unsigned 16-bit primitive tag name.</summary>
    private const string UInt16TagName = "UInt16";

    /// <summary>The signed 16-bit primitive tag name.</summary>
    private const string Int16TagName = "Int16";

    /// <summary>The unsigned 32-bit primitive tag name.</summary>
    private const string UInt32TagName = "UInt32";

    /// <summary>The signed 32-bit primitive tag name.</summary>
    private const string Int32TagName = "Int32";

    /// <summary>The unsigned 64-bit primitive tag name.</summary>
    private const string UInt64TagName = "UInt64";

    /// <summary>The signed 64-bit primitive tag name.</summary>
    private const string Int64TagName = "Int64";

    /// <summary>The single-precision primitive tag name.</summary>
    private const string SingleTagName = "Single";

    /// <summary>The double-precision primitive tag name.</summary>
    private const string DoubleTagName = "Double";

    /// <summary>The string primitive tag name.</summary>
    private const string StringTagName = "String";

    /// <summary>The edited persistent description.</summary>
    private const string EditedDescription = "edited";

    /// <summary>The missing persistent tag name.</summary>
    private const string MissingPersistentTagName = "MissingPersistentTag";

    /// <summary>Verifies operation metrics include every counted operation and a failure.</summary>
    /// <returns>A task representing the test.</returns>
    [Test]
    internal async Task SimulatorMetricsIncludeCreateDestroyReadWriteAndFailuresAsync()
    {
        using var simulator = new ABPlcSimulator(PlcType.SLC);
        simulator.ScanEnabled = false;
        simulator.AutoWriteValue = false;
        simulator.AddUpdateTagItem<int>(MetricVariable, MetricTagName, default);
        simulator.Value(MetricVariable, MetricValue, 0);

        _ = simulator.Write(MetricVariable);
        _ = simulator.Read(MetricVariable);
        simulator.QueueFault(ABPlcSimulatorOperation.Read, PlcTagStatus.ErrRead, 1, MetricTagName);
        _ = simulator.Read(MetricVariable);
        await Assert.That(simulator.RemoveTagItem(MetricVariable)).IsTrue();

        var metrics = simulator.OperationMetrics;

        await Assert.That(metrics.CreateOperations).IsGreaterThan(0L);
        await Assert.That(metrics.DestroyOperations).IsGreaterThan(0L);
        await Assert.That(metrics.ReadOperations).IsGreaterThan(0L);
        await Assert.That(metrics.WriteOperations).IsGreaterThan(0L);
        await Assert.That(metrics.FailedOperations).IsGreaterThan(0L);
        await Assert.That(metrics.TotalOperations)
            .IsEqualTo(
                metrics.CreateOperations +
                metrics.DestroyOperations +
                metrics.ReadOperations +
                metrics.WriteOperations);
    }

    /// <summary>Verifies bit projection covers every supported primitive tag storage type.</summary>
    /// <returns>A task representing the test.</returns>
    [Test]
    internal async Task WrapperBitProjectionSupportsEveryPrimitiveStorageTypeAsync()
    {
        using var native = new SimulatedPlcTagNative(TimeProvider.System);
        using var plc = new ABPlc(LoopbackAddress, PlcType.SLC, null, native);
        using var tags = plc.CreateTagList("PrimitiveBits", TimeSpan.FromMinutes(1));

        await AssertBitSetAsync(tags.CreateTagType<bool>(BooleanTagName, BooleanTagName), wrapper => wrapper.SetBool(true, 0));
        await AssertBitSetAsync(tags.CreateTagType<byte>(ByteTagName, ByteTagName), wrapper => wrapper.SetUInt8(1, 0));
        await AssertBitSetAsync(tags.CreateTagType<sbyte>(SByteTagName, SByteTagName), wrapper => wrapper.SetInt8(1, 0));
        await AssertBitSetAsync(tags.CreateTagType<ushort>(UInt16TagName, UInt16TagName), wrapper => wrapper.SetUInt16(1, 0));
        await AssertBitSetAsync(tags.CreateTagType<short>(Int16TagName, Int16TagName), wrapper => wrapper.SetInt16(1, 0));
        await AssertBitSetAsync(tags.CreateTagType<uint>(UInt32TagName, UInt32TagName), wrapper => wrapper.SetUInt32(1, 0));
        await AssertBitSetAsync(tags.CreateTagType<int>(Int32TagName, Int32TagName), wrapper => wrapper.SetInt32(1, 0));
        await AssertBitSetAsync(tags.CreateTagType<ulong>(UInt64TagName, UInt64TagName), wrapper => wrapper.SetUInt64(1, 0));
        await AssertBitSetAsync(tags.CreateTagType<long>(Int64TagName, Int64TagName), wrapper => wrapper.SetInt64(1, 0));

        await AssertPrimitiveRoundTripAsync(tags.CreateTagType<bool>(BooleanTagName, BooleanTagName), false);
        await AssertPrimitiveRoundTripAsync(tags.CreateTagType<byte>(ByteTagName, ByteTagName), default(byte));
        await AssertPrimitiveRoundTripAsync(tags.CreateTagType<sbyte>(SByteTagName, SByteTagName), default(sbyte));
        await AssertPrimitiveRoundTripAsync(tags.CreateTagType<ushort>(UInt16TagName, UInt16TagName), default(ushort));
        await AssertPrimitiveRoundTripAsync(tags.CreateTagType<short>(Int16TagName, Int16TagName), default(short));
        await AssertPrimitiveRoundTripAsync(tags.CreateTagType<uint>(UInt32TagName, UInt32TagName), default(uint));
        await AssertPrimitiveRoundTripAsync(tags.CreateTagType<int>(Int32TagName, Int32TagName), default(int));
        await AssertPrimitiveRoundTripAsync(tags.CreateTagType<ulong>(UInt64TagName, UInt64TagName), default(ulong));
        await AssertPrimitiveRoundTripAsync(tags.CreateTagType<long>(Int64TagName, Int64TagName), default(long));
        await AssertPrimitiveRoundTripAsync(tags.CreateTagType<float>(SingleTagName, SingleTagName), default(float));
        await AssertPrimitiveRoundTripAsync(tags.CreateTagType<double>(DoubleTagName, DoubleTagName), default(double));
        await AssertPrimitiveRoundTripAsync(tags.CreateTagType<string>(StringTagName, StringTagName), MetricVariable);
    }

    /// <summary>Verifies facade typed-value operations cover direct and integral-Boolean paths.</summary>
    /// <returns>A task representing the test.</returns>
    [Test]
    internal async Task FacadeTypedValueOperationsCoverIntegralBooleanFamiliesAsync()
    {
        using var simulator = new ABPlcSimulator(PlcType.SLC);
        simulator.ScanEnabled = false;
        simulator.AutoWriteValue = false;
        simulator.AddUpdateTagItem(BooleanTagName, BooleanTagName, false);
        simulator.AddUpdateTagItem<byte>(ByteTagName, ByteTagName, default);
        simulator.AddUpdateTagItem<sbyte>(SByteTagName, SByteTagName, default);
        simulator.AddUpdateTagItem<ushort>(UInt16TagName, UInt16TagName, default);
        simulator.AddUpdateTagItem<short>(Int16TagName, Int16TagName, default);
        simulator.AddUpdateTagItem<uint>(UInt32TagName, UInt32TagName, default);
        simulator.AddUpdateTagItem<int>(Int32TagName, Int32TagName, default);
        simulator.AddUpdateTagItem<ulong>(UInt64TagName, UInt64TagName, default);
        simulator.AddUpdateTagItem<long>(Int64TagName, Int64TagName, default);

        var missing = await simulator.ReadValueAsync<int>(MetricVariable, default, 0, CancellationToken.None);
        await Assert.That(missing.Succeeded).IsFalse();

        await AssertBooleanBitRoundTripAsync(simulator, ByteTagName);
        await AssertBooleanBitRoundTripAsync(simulator, SByteTagName);
        await AssertBooleanBitRoundTripAsync(simulator, UInt16TagName);
        await AssertBooleanBitRoundTripAsync(simulator, Int16TagName);
        await AssertBooleanBitRoundTripAsync(simulator, UInt32TagName);
        await AssertBooleanBitRoundTripAsync(simulator, Int32TagName);
        await AssertBooleanBitRoundTripAsync(simulator, UInt64TagName);
        await AssertBooleanBitRoundTripAsync(simulator, Int64TagName);

        var directWrite = await simulator.WriteValueAsync(BooleanTagName, true, 0, CancellationToken.None);
        var directRead = await simulator.ReadValueAsync(BooleanTagName, false, 0, CancellationToken.None);
        await Assert.That(directWrite.Succeeded).IsTrue();
        await Assert.That(directRead.Value).IsTrue();

        simulator.QueueFault(ABPlcSimulatorOperation.Read, PlcTagStatus.ErrRead, 1, BooleanTagName);
        var failedRead = await simulator.ReadValueAsync(BooleanTagName, false, 0, CancellationToken.None);
        await Assert.That(failedRead.Succeeded).IsFalse();
    }

    /// <summary>Verifies auxiliary native operations are recorded without affecting the primary metric totals.</summary>
    /// <returns>A task representing the test.</returns>
    [Test]
    internal async Task SimulatorMetricsRetainAuxiliaryNativeOperationsAndFormatMissingTagNamesAsync()
    {
        using var native = new SimulatedPlcTagNative(TimeProvider.System);
        using var plc = new ABPlc(LoopbackAddress, PlcType.SLC, null, native);
        using var tags = plc.CreateTagList("AuxiliaryOperations", TimeSpan.FromMinutes(1));
        using var tag = tags.CreateTagType<short>("Auxiliary", "N7:1");

        await Assert.That(tag.Abort()).IsEqualTo(PlcTagStatus.StatusOK);
        await Assert.That(tag.GetStatus()).IsEqualTo(PlcTagStatus.StatusOK);
        await Assert.That(tag.Lock()).IsEqualTo(PlcTagStatus.StatusOK);
        await Assert.That(tag.Unlock()).IsEqualTo(PlcTagStatus.StatusOK);

        var metrics = native.GetOperationMetrics();
        var entry = new ABPlcSimulatorLogEntry(
            sequence: 1,
            timestamp: DateTimeOffset.FromUnixTimeSeconds(0),
            operation: ABPlcSimulatorOperation.Abort,
            tagName: null,
            handle: 1,
            statusCode: PlcTagStatus.StatusOK);

        await Assert.That(metrics.TotalOperations).IsGreaterThan(metrics.CreateOperations);
        await Assert.That(entry.ToString()).Contains("<none>");
    }

    /// <summary>Verifies wrapper and helper paths handle composite values and deterministic cached observables.</summary>
    /// <returns>A task representing the test.</returns>
    [Test]
    internal async Task WrapperAndHelpersCoverCompositeValuesCachedResultsAndNullScaleValuesAsync()
    {
        using var native = new SimulatedPlcTagNative(TimeProvider.System);
        using var plc = new ABPlc(LoopbackAddress, PlcType.SLC, null, native)
        {
            AutoWriteValue = false,
        };
        using var tags = plc.CreateTagList("CompositeCoverage", TimeSpan.FromMinutes(1));
        using var tag = tags.CreateTagType<int>("Composite", "N7:2");

        tag.ValueManager.Set(new IntComposite { Value = MetricValue });
        var readComposite = (IntComposite)tag.ValueManager.Get(new IntComposite())!;
        var firstResults = tags.ReadResults;
        var secondResults = tags.ReadResults;
        var expectedText = new TextComposite().Text;
        var createdText = TagHelper.CreateObject<TextComposite>(null, 1);

        await Assert.That(readComposite.Value).IsEqualTo(MetricValue);
        await Assert.That(secondResults).IsEqualTo(firstResults);
        await Assert.That(createdText.Text).IsEqualTo(expectedText);
        _ = Assert.Throws<NullReferenceException>(() => ((IPlcTag)tag).Value = null);
        _ = Assert.Throws<InvalidCastException>(() => TagHelper.ScaleLinear(tag, 0D, 1D, 0D, 1D));
        _ = Assert.Throws<InvalidCastException>(() => TagHelper.ScaleSquareRoot(tag, 0D, 1D, 0D, 1D));
    }

    /// <summary>Verifies generator paths for global namespaces, non-nullable property tags, and unresolved attributes.</summary>
    /// <returns>A task representing the test.</returns>
    [Test]
    internal async Task GeneratorHandlesGlobalModelsAndUnresolvedAttributesAsync()
    {
        const string globalSource = """
            using IoT.DriverCore.ABPlcRx.SourceGeneration;

            [PlcModel]
            public partial class GlobalModel
            {
                [PlcTag("Counter")]
                public int Counter { get; private set; }
            }
            """;
        const string unresolvedAttributeSource = """
            [MissingGeneratorAttribute]
            public partial class MissingAttributeModel
            {
            }
            """;

        var globalDriver = RunGeneratorThroughExistingTestHarness(globalSource);
        var unresolvedDriver = RunGeneratorThroughExistingTestHarness(unresolvedAttributeSource);

        await Assert.That(globalDriver.GetRunResult().GeneratedTrees.Length).IsEqualTo(1);
        await Assert.That(unresolvedDriver.GetRunResult().GeneratedTrees.Length).IsEqualTo(0);
    }

    /// <summary>Verifies null-value scaling, empty reduction, and an explicit Logix route.</summary>
    /// <returns>A task representing the test.</returns>
    [Test]
    internal async Task CoreResidualBranchesUseDeterministicValuesAsync()
    {
        using var native = new SimulatedPlcTagNative(TimeProvider.System);
        using var plc = new ABPlc(LoopbackAddress, PlcType.SLC, null, native)
        {
            AutoWriteValue = false,
        };
        using var tags = plc.CreateTagList("NullValues", TimeSpan.FromMinutes(1));
        using var nullTag = tags.CreateTagType<string>("NullValue", "ST9:0");
        nullTag.Value = null;

        _ = Assert.Throws<InvalidOperationException>(
            () => TagMixins.ScaleLinear(nullTag, 0D, 1D, 0D, 1D));
        _ = Assert.Throws<InvalidOperationException>(
            () => TagMixins.ScaleSquareRoot(nullTag, 0D, 1D, 0D, 1D));
        _ = Assert.Throws<InvalidOperationException>(
            () => TagHelper.CreateObject<int?>(null, 1));
        var fixStringMethod = typeof(TagHelper).GetMethod(
            "FixStringNullToEmpty",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(TagHelper).FullName, "FixStringNullToEmpty");
        _ = fixStringMethod.Invoke(null, [null]);

        var expectedTimestamp = new DateTimeOffset(2026, 7, 24, 8, 0, 0, TimeSpan.Zero);
        var reduced = PlcTagResult.Reduce([], new FixedTimeProvider(expectedTimestamp));

        using var routedSimulator = new ABPlcSimulator(
            PlcType.LGX,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(1),
            "1,0",
            TimeProvider.System);
        using var defaultRoutedSimulator = new ABPlcSimulator(
            PlcType.LGX,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(1),
            path: null,
            TimeProvider.System);

        await Assert.That(reduced.Timestamp).IsEqualTo(expectedTimestamp);
        await Assert.That(reduced.ExecutionTime).IsEqualTo(0L);
        await Assert.That(reduced.StatusCode).IsEqualTo(PlcTagStatus.StatusOK);
        await Assert.That(routedSimulator.IsDisposed).IsFalse();
        await Assert.That(defaultRoutedSimulator.IsDisposed).IsFalse();
    }

    /// <summary>Verifies persisted edits synchronize only when the stored definition exists.</summary>
    /// <returns>A task representing the test.</returns>
    [Test]
    internal async Task PersistentLogicalTagEditsCoverExistingAndMissingDefinitionsAsync()
    {
        using var simulator = new ABPlcSimulator(PlcType.SLC);
        using var client = simulator.CreateLogicalTagClient();
        var databasePath = Path.Combine(Path.GetTempPath(), $"abplcrx-edit-{Guid.NewGuid():N}.db");

        try
        {
            var store = new LogicalTagSqliteStore($"Data Source={databasePath};Pooling=False");
            _ = Assert.Throws<ArgumentNullException>(
                () => client.InitializeStoreAsync(null!, CancellationToken.None).GetAwaiter().GetResult());
            _ = Assert.Throws<InvalidOperationException>(
                () => client.ListTagsAsync(CancellationToken.None).GetAwaiter().GetResult());
            _ = Assert.Throws<InvalidOperationException>(() => client.Observe(MissingPersistentTagName));
            await client.InitializeStoreAsync(store, CancellationToken.None);
            var original = client.CreateTag("Editable", "N7:10", "int");
            await client.UpsertTagAsync(original, CancellationToken.None);

            var options = original.CurrentOptions();
            options.Description = EditedDescription;
            var edited = await client.EditTagAsync(original.WithOptions(options), CancellationToken.None);
            var missing = await client.EditTagAsync(
                new LogicalTag(MissingPersistentTagName, "N7:99", "int"),
                CancellationToken.None);
            var stored = await client.GetTagAsync(original.Name, CancellationToken.None);
            var catalogContainsEdited = client.Catalog.TryGet(original.Name, out var live);

            await Assert.That(edited).IsTrue();
            await Assert.That(missing).IsFalse();
            await Assert.That(stored).IsNotNull();
            await Assert.That(stored!.Description).IsEqualTo(EditedDescription);
            await Assert.That(catalogContainsEdited).IsTrue();
            await Assert.That(live!.Description).IsEqualTo(EditedDescription);
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    /// <summary>Verifies bulk writes route unique bit projections through the typed logical path.</summary>
    /// <returns>A task representing the test.</returns>
    [Test]
    internal async Task LogicalBulkWriteRoutesUniqueBitProjectionAsync()
    {
        using var simulator = new ABPlcSimulator(PlcType.SLC);
        using var client = simulator.CreateLogicalTagClient();
        client.RegisterTag(
            new LogicalTag(
                "Flag",
                "N7:20",
                "bool",
                new LogicalTagOptions
                {
                    Metadata = new Dictionary<string, string>(StringComparer.Ordinal) { ["Bit"] = "0" },
                }));

        var result = await client.WriteManyAsync(
        [
            new LogicalTagValue("Flag", true, TimeProvider.System.GetUtcNow()),
        ]);
        var read = await client.ReadAsync("Flag");

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].Succeeded).IsTrue();
        await Assert.That(read.Succeeded).IsTrue();
        await Assert.That((bool)read.Value!.Value!).IsTrue();
    }

    /// <summary>Writes one primitive value and verifies its low bit through the public wrapper surface.</summary>
    /// <param name="tag">The PLC tag whose wrapper is exercised.</param>
    /// <param name="setValue">The primitive write operation.</param>
    /// <returns>A task representing the assertion.</returns>
    private static async Task AssertBitSetAsync(IPlcTag tag, Action<PlcTagWrapper> setValue)
    {
        var wrapper = tag.ValueManager;
        setValue(wrapper);
        await Assert.That(wrapper.GetBit(0)).IsTrue();
    }

    /// <summary>Writes through the generic wrapper surface and reads the same primitive value back.</summary>
    /// <typeparam name="T">The primitive type under test.</typeparam>
    /// <param name="tag">The PLC tag whose wrapper is exercised.</param>
    /// <param name="value">The value to round-trip.</param>
    /// <returns>A task representing the assertion.</returns>
    private static async Task AssertPrimitiveRoundTripAsync<T>(IPlcTag tag, T value)
    {
        var wrapper = tag.ValueManager;
        wrapper.Set(value);
        await Assert.That(wrapper.Get(value)).IsEqualTo(value);
    }

    /// <summary>Writes and reads both states of one Boolean bit backed by an integral PLC tag.</summary>
    /// <param name="simulator">The simulator facade.</param>
    /// <param name="variable">The logical variable to exercise.</param>
    /// <returns>A task representing the assertions.</returns>
    private static async Task AssertBooleanBitRoundTripAsync(ABPlcSimulator simulator, string variable)
    {
        var set = await simulator.WriteValueAsync(variable, true, 0, CancellationToken.None);
        var setValue = await simulator.ReadValueAsync(variable, false, 0, CancellationToken.None);
        var clear = await simulator.WriteValueAsync(variable, false, 0, CancellationToken.None);
        var clearValue = await simulator.ReadValueAsync(variable, false, 0, CancellationToken.None);

        await Assert.That(set.Succeeded).IsTrue();
        await Assert.That(setValue.Value).IsTrue();
        await Assert.That(clear.Succeeded).IsTrue();
        await Assert.That(clearValue.Value).IsFalse();
    }

    /// <summary>Runs the existing generator harness without duplicating its compilation setup.</summary>
    /// <param name="source">The source snippet supplied to the generator.</param>
    /// <returns>The completed generator driver.</returns>
    private static GeneratorDriver RunGeneratorThroughExistingTestHarness(string source)
    {
        var method = typeof(SourceGeneratorTests).GetMethod(
            "RunGenerator",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(SourceGeneratorTests).FullName, "RunGenerator");
        var run = method.Invoke(null, [source, false])
            ?? throw new InvalidOperationException("The source generator test harness did not return a result.");
        var driver = run.GetType().GetProperty("Driver")?.GetValue(run);
        return driver as GeneratorDriver
            ?? throw new InvalidOperationException("The source generator test harness returned an invalid driver.");
    }

    /// <summary>Small composite value used to exercise the wrapper class-value path.</summary>
    private sealed class IntComposite
    {
        /// <summary>Gets or sets the primitive value.</summary>
        public int Value { get; set; }
    }

    /// <summary>Composite value with a preinitialized string property.</summary>
    private sealed class TextComposite
    {
        /// <summary>The expected initialized text value.</summary>
        private const string InitialText = "already-set";

        /// <summary>Gets or sets the initialized string property.</summary>
        public string Text { get; set; } = InitialText;
    }

    /// <summary>Provides a fixed timestamp for deterministic reduction coverage.</summary>
    /// <param name="utcNow">The UTC timestamp returned by the provider.</param>
    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
