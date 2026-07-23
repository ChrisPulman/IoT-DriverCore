// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
        using var plc = new ABPlc("127.0.0.1", PlcType.SLC, null, native);
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
}
