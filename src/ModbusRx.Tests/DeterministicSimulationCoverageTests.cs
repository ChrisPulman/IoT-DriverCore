// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Numerics;
using IoT.DriverCore.Core;
using IoT.DriverCore.ModbusRx.Data;
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.ModbusRx.IO;
using IoT.DriverCore.ModbusRx.LogicalTags;
using IoT.DriverCore.ModbusRx.Message;
using IoT.DriverCore.ModbusRx.Unme.Common;
using IoT.DriverCore.ModbusRx.Utility;
using NativeAssert = TUnit.Assertions.Assert;

namespace IoT.DriverCore.ModbusRx.UnitTests;

/// <summary>Deterministic coverage for the in-memory data, framing, pooling, and reactive server seams.</summary>
public sealed class DeterministicSimulationCoverageTests
{
    /// <summary>The size of the compact in-memory data stores.</summary>
    private const ushort StoreSize = 16;

    /// <summary>The first address used by optimized operations.</summary>
    private const ushort FirstAddress = 1;

    /// <summary>The slave address used by optimized frames.</summary>
    private const byte SlaveAddress = 1;

    /// <summary>The second address used by optimized operations.</summary>
    private const int SecondAddress = 2;

    /// <summary>The number of values in a three-value operation.</summary>
    private const ushort ThreeValues = 3;

    /// <summary>The number of points occupied by a 32-bit value.</summary>
    private const ushort TwoPoints = 2;

    /// <summary>The number of points occupied by a 64-bit value.</summary>
    private const ushort FourPoints = 4;

    /// <summary>The first holding-register value.</summary>
    private const ushort FirstRegisterValue = 10;

    /// <summary>The second holding-register value.</summary>
    private const ushort SecondRegisterValue = 20;

    /// <summary>The third holding-register value.</summary>
    private const ushort ThirdRegisterValue = 30;

    /// <summary>The first input-register value.</summary>
    private const ushort FirstInputValue = 40;

    /// <summary>The second input-register value.</summary>
    private const ushort SecondInputValue = 50;

    /// <summary>A deliberately different register value.</summary>
    private const ushort DifferentRegisterValue = 99;

    /// <summary>The number of booleans used to exercise vectorized paths.</summary>
    private const int BooleanSampleCount = 35;

    /// <summary>The repetition period used by the boolean test pattern.</summary>
    private const int BooleanPatternPeriod = 3;

    /// <summary>The tail appended after a hardware vector.</summary>
    private const int VectorTailLength = 3;

    /// <summary>The minimum byte-buffer length.</summary>
    private const int ByteBufferLength = 8;

    /// <summary>The minimum word and bit buffer length.</summary>
    private const int SmallBufferLength = 4;

    /// <summary>The number of coils encoded across two bytes.</summary>
    private const int NineCoils = 9;

    /// <summary>A floating-point value used in round trips.</summary>
    private const float FloatValue = 123.5F;

    /// <summary>The expected unsigned 32-bit value.</summary>
    private const uint ExpectedUInt32 = 0x12345678U;

    /// <summary>The signed 32-bit round-trip value.</summary>
    private const int IntValue = unchecked((int)0x89ABCDEF);

    /// <summary>The unsigned 32-bit round-trip value.</summary>
    private const uint UIntValue = 0xFEDCBA98;

    /// <summary>The signed 64-bit round-trip value.</summary>
    private const long LongValue = -8_526_495_043_095_935_641L;

    /// <summary>The explicit error message used by exception constructor coverage.</summary>
    private const string FailureMessage = "message";

    /// <summary>The first framed register value.</summary>
    private const ushort FramedRegisterOne = 0x1234;

    /// <summary>The second framed register value.</summary>
    private const ushort FramedRegisterTwo = 0x5678;

    /// <summary>The byte count of a two-register response.</summary>
    private const byte RegisterResponseByteCount = 4;

    /// <summary>The byte count of a nine-coil response.</summary>
    private const byte CoilResponseByteCount = 2;

    /// <summary>The number of supported slave requests exercised by the dispatch test.</summary>
    private const int TenRequests = 10;

    /// <summary>The number of requests that complete a write.</summary>
    private const int FiveWrites = 5;

    /// <summary>The sixth zero-based index used by constructor coverage.</summary>
    private const int SixValues = 6;

    /// <summary>The number of public invalid-request constructors.</summary>
    private const int SevenValues = 7;

    /// <summary>The zero register value used by clearing tests.</summary>
    private const ushort ZeroRegister = 0;

    /// <summary>A maximum register value used by constant patterns.</summary>
    private const ushort MaximumRegister = ushort.MaxValue;

    /// <summary>Exercises optimized bulk reads, writes, clears, copies, comparisons, and guards.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task DataStoreOptimizedOperations_RoundTripAndValidateAsync()
    {
        using var source = DataStoreFactory.CreateDefaultDataStore(StoreSize, StoreSize, StoreSize, StoreSize);
        using var destination = DataStoreFactory.CreateDefaultDataStore(StoreSize, StoreSize, StoreSize, StoreSize);

        DataStoreExtensions.WriteHoldingRegistersOptimized(
            source,
            FirstAddress,
            [FirstRegisterValue, SecondRegisterValue, ThirdRegisterValue]);
        DataStoreExtensions.WriteCoilsOptimized(source, FirstAddress, [true, false, true]);
        source.InputRegisters[FirstAddress] = FirstInputValue;
        source.InputRegisters[SecondAddress] = SecondInputValue;
        source.InputDiscretes[FirstAddress] = true;
        source.InputDiscretes[SecondAddress] = false;

        await NativeAssert.That(
                DataStoreExtensions.ReadHoldingRegistersOptimized(source, FirstAddress, ThreeValues))
            .IsEquivalentTo([FirstRegisterValue, SecondRegisterValue, ThirdRegisterValue]);
        await NativeAssert.That(DataStoreExtensions.ReadCoilsOptimized(source, FirstAddress, ThreeValues))
            .IsEquivalentTo([true, false, true]);
        await NativeAssert.That(
                DataStoreExtensions.ReadInputRegistersOptimized(source, FirstAddress, SecondAddress))
            .IsEquivalentTo([FirstInputValue, SecondInputValue]);
        await NativeAssert.That(DataStoreExtensions.ReadInputsOptimized(source, FirstAddress, SecondAddress))
            .IsEquivalentTo([true, false]);

        DataStoreExtensions.BulkCopyHoldingRegisters(source, destination, FirstAddress, ThreeValues);
        DataStoreExtensions.BulkCopyCoils(source, destination, FirstAddress, ThreeValues);
        await NativeAssert.That(
                DataStoreExtensions.CompareHoldingRegisters(source, destination, FirstAddress, ThreeValues))
            .IsTrue();

        destination.HoldingRegisters[SecondAddress] = DifferentRegisterValue;
        await NativeAssert.That(
                DataStoreExtensions.CompareHoldingRegisters(source, destination, FirstAddress, ThreeValues))
            .IsFalse();

        DataStoreExtensions.ClearHoldingRegisters(source, FirstAddress, ThreeValues);
        DataStoreExtensions.ClearCoils(source, FirstAddress, ThreeValues);
        await NativeAssert.That(
                DataStoreExtensions.ReadHoldingRegistersOptimized(source, FirstAddress, ThreeValues))
            .IsEquivalentTo([ZeroRegister, ZeroRegister, ZeroRegister]);
        await NativeAssert.That(DataStoreExtensions.ReadCoilsOptimized(source, FirstAddress, ThreeValues))
            .IsEquivalentTo([false, false, false]);

        await NativeAssert.That(
                () => DataStoreExtensions.ReadHoldingRegistersOptimized(null!, 0, 1))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                () => DataStoreExtensions.BulkCopyHoldingRegisters(source, null!, 0, 1))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                () => DataStoreExtensions.BulkCopyCoils(null!, destination, 0, 1))
            .Throws<ArgumentNullException>();
    }

    /// <summary>Exercises scalar and SIMD-sized data conversion paths with both word orders.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusDataExtensions_RoundTripScalarAndVectorDataAsync()
    {
        var booleans = Enumerable.Range(
                0,
                Math.Max(Vector<byte>.Count + VectorTailLength, BooleanSampleCount))
            .Select(static index => index % BooleanPatternPeriod == 0)
            .ToArray();
        var packed = ModbusDataExtensions.PackBooleans(booleans);
        var unpacked = ModbusDataExtensions.UnpackBooleans(packed, booleans.Length);

        await NativeAssert.That(unpacked).IsEquivalentTo(booleans);
        await NativeAssert.That(ModbusDataExtensions.PackBooleans(null!)).IsEmpty();
        await NativeAssert.That(ModbusDataExtensions.UnpackBooleans(null!, 1)).IsEmpty();
        await NativeAssert.That(ModbusDataExtensions.UnpackBooleans([1], 0)).IsEmpty();
        await NativeAssert.That(ModbusDataExtensions.FastEquals(packed, (byte[])packed.Clone())).IsTrue();
        await NativeAssert.That(ModbusDataExtensions.FastEquals(packed, [1])).IsFalse();
        await NativeAssert.That(ModbusDataExtensions.FastEquals(null!, null!)).IsTrue();

        var scalarBooleans = new[] { true };
        await NativeAssert.That(ModbusDataExtensions.UnpackBooleans(
                ModbusDataExtensions.PackBooleans(scalarBooleans),
                scalarBooleans.Length))
            .IsEquivalentTo(scalarBooleans);

        var vectorBytes = Enumerable.Repeat((byte)FirstAddress, Vector<byte>.Count + VectorTailLength).ToArray();
        var vectorDifference = (byte[])vectorBytes.Clone();
        vectorDifference[FirstAddress] = (byte)SecondAddress;
        var tailDifference = (byte[])vectorBytes.Clone();
        tailDifference[^1] = (byte)SecondAddress;
        await NativeAssert.That(ModbusDataExtensions.FastEquals(vectorBytes, (byte[])vectorBytes.Clone())).IsTrue();
        await NativeAssert.That(ModbusDataExtensions.FastEquals(vectorBytes, vectorDifference)).IsFalse();
        await NativeAssert.That(ModbusDataExtensions.FastEquals(vectorBytes, tailDifference)).IsFalse();

        foreach (var swapWords in new[] { false, true })
        {
            var intRegisters = ModbusDataExtensions.ToRegisters(IntValue, swapWords);
            var uintRegisters = ModbusDataExtensions.ToRegisters(UIntValue, swapWords);
            var longRegisters = ModbusDataExtensions.ToRegisters(LongValue, swapWords);

            await NativeAssert.That(ModbusDataExtensions.ToInt32(intRegisters, 0, swapWords))
                .IsEqualTo(IntValue);
            await NativeAssert.That(ModbusDataExtensions.ToUInt32(uintRegisters, 0, swapWords))
                .IsEqualTo(UIntValue);
            await NativeAssert.That(ModbusDataExtensions.ToInt64(longRegisters, 0, swapWords))
                .IsEqualTo(LongValue);
        }

        await NativeAssert.That(() => ModbusDataExtensions.ToInt32([], 0, false))
            .Throws<ArgumentException>();
        await NativeAssert.That(() => ModbusDataExtensions.ToUInt32(null!, 0, false))
            .Throws<ArgumentException>();
        await NativeAssert.That(
                () => ModbusDataExtensions.ToInt64(
                    [FirstAddress, SecondAddress, ThreeValues],
                    0,
                    false))
            .Throws<ArgumentException>();
    }

    /// <summary>Exercises every deterministic and random simulation pattern against a compact data store.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task SimulationDataProvider_LoadsEveryPatternAndBooleanModeAsync()
    {
        using var provider = new SimulationDataProvider(new FixedTimeProvider());
        using var dataStore = DataStoreFactory.CreateDefaultDataStore(StoreSize, StoreSize, StoreSize, StoreSize);

        foreach (var pattern in TestFrameworkCompatibilityExtensions.GetEnumValues<TestPattern>())
        {
            provider.LoadTestPattern(dataStore, pattern);
        }

        await NativeAssert.That(dataStore.HoldingRegisters[FirstAddress]).IsEqualTo(MaximumRegister);
        await NativeAssert.That(dataStore.InputRegisters[FirstAddress]).IsEqualTo(MaximumRegister);
        await NativeAssert.That(dataStore.CoilDiscretes[FirstAddress]).IsTrue();
        await NativeAssert.That(dataStore.InputDiscretes[FirstAddress]).IsFalse();

        await NativeAssert.That(
                provider.GenerateBooleanPattern(StoreSize, BooleanPattern.AllTrue))
            .IsEquivalentTo(Enumerable.Repeat(true, StoreSize));
        await NativeAssert.That(
                provider.GenerateBooleanPattern(StoreSize, BooleanPattern.AllFalse))
            .IsEquivalentTo(Enumerable.Repeat(false, StoreSize));
        var alternating = provider.GenerateBooleanPattern(StoreSize, BooleanPattern.Alternating);
        await NativeAssert.That(alternating[0]).IsTrue();
        await NativeAssert.That(alternating[FirstAddress]).IsFalse();
        await NativeAssert.That(
                provider.GenerateBooleanPattern(StoreSize, BooleanPattern.Random).Length)
            .IsEqualTo(StoreSize);

        await NativeAssert.That(() => provider.LoadTestPattern(null!, TestPattern.AllZeros))
            .Throws<ArgumentNullException>();
    }

    /// <summary>Exercises each live update mode, idempotent start, idempotent stop, and input guards.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task SimulationDataProvider_StartsEveryUpdateModeDeterministicallyAsync()
    {
        using var provider = new SimulationDataProvider(new FixedTimeProvider());
        using var dataStore = DataStoreFactory.CreateDefaultDataStore(StoreSize, StoreSize, StoreSize, StoreSize);
        var dormantInterval = TimeSpan.FromDays(FirstAddress);

        foreach (var simulationType in TestFrameworkCompatibilityExtensions.GetEnumValues<SimulationType>())
        {
            provider.Start(dataStore, dormantInterval, simulationType);
            provider.Start(dataStore, dormantInterval, simulationType);
            await NativeAssert.That(await provider.IsRunning.FirstAsync()).IsTrue();
            provider.Stop();
            provider.Stop();
            await NativeAssert.That(await provider.IsRunning.FirstAsync()).IsFalse();
        }

        await NativeAssert.That(
                () => provider.Start(null!, dormantInterval, SimulationType.CountingUp))
            .Throws<ArgumentNullException>();
    }

    /// <summary>Round-trips every supported scalar, array, bit, and register value through each byte order.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task TagCodec_RoundTripsSupportedTypesAndByteOrdersAsync()
    {
        foreach (var byteOrder in TestFrameworkCompatibilityExtensions.GetEnumValues<ModbusByteOrder>())
        {
            foreach (var sample in CreateRegisterCodecSamples())
            {
                var tag = CreateCodecTag(sample.Count, sample.Value.GetType(), byteOrder);
                var encoded = ModbusTagCodec.Encode(tag, sample.Value);
                var decoded = ModbusTagCodec.Decode(tag, encoded, 0);
                if (sample.Value is float expectedFloat)
                {
                    await NativeAssert.That((float)decoded)
                        .IsEqualTo(expectedFloat)
                        .Because($"{sample.Value.GetType()} must round-trip with {byteOrder}.");
                }
                else
                {
                    await NativeAssert.That(ValuesEqual(sample.Value, decoded))
                        .IsTrue()
                        .Because($"{sample.Value.GetType()} must round-trip with {byteOrder}.");
                }
            }
        }

        var boolTag = CreateCodecTag(FirstAddress, typeof(bool), ModbusByteOrder.BigEndian, ModbusDataArea.Coil);
        var boolArrayTag = CreateCodecTag(
            ThreeValues,
            typeof(bool[]),
            ModbusByteOrder.BigEndian,
            ModbusDataArea.DiscreteInput);
        await NativeAssert.That((bool)ModbusTagCodec.Decode(boolTag, ModbusTagCodec.Encode(boolTag, true), 0))
            .IsTrue();
        await NativeAssert.That(
                ValuesEqual(
                    new[] { true, false, true },
                    ModbusTagCodec.Decode(
                        boolArrayTag,
                        ModbusTagCodec.Encode(boolArrayTag, new[] { true, false, true }),
                        0)))
            .IsTrue();
    }

    /// <summary>Exercises codec validation for unsupported types, invalid counts, and invalid values.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task TagCodec_RejectsInvalidTypeCountAndValueAsync()
    {
        await NativeAssert.That(
                () => ModbusTagCodec.ValidateType(ModbusDataArea.Coil, FirstAddress, typeof(int)))
            .Throws<NotSupportedException>();
        await NativeAssert.That(
                () => ModbusTagCodec.ValidateType(
                    ModbusDataArea.HoldingRegister,
                    FirstAddress,
                    typeof(decimal)))
            .Throws<NotSupportedException>();
        await NativeAssert.That(
                () => ModbusTagCodec.ValidateType(
                    ModbusDataArea.HoldingRegister,
                    FirstAddress,
                    typeof(int)))
            .Throws<ArgumentException>();

        var scalarTag = CreateCodecTag(FirstAddress, typeof(ushort), ModbusByteOrder.BigEndian);
        var arrayTag = CreateCodecTag(TwoPoints, typeof(ushort[]), ModbusByteOrder.BigEndian);
        await NativeAssert.That(() => ModbusTagCodec.Encode(scalarTag, null)).Throws<ArgumentException>();
        await NativeAssert.That(() => ModbusTagCodec.Encode(scalarTag, "wrong")).Throws<ArgumentException>();
        await NativeAssert.That(() => ModbusTagCodec.Encode(arrayTag, new ushort[] { FirstAddress }))
            .Throws<ArgumentException>();
    }

    /// <summary>Dispatches every supported slave function and converts handler failures to protocol exceptions.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusSlave_DispatchesAllFunctionsAndRaisesEventsAsync()
    {
        using var listener = new TestTcpListener(IPAddress.Loopback, 0);
        using var slave = ModbusTcpSlave.CreateTcp(SlaveAddress, listener);
        slave.DataStore = DataStoreFactory.CreateDefaultDataStore(StoreSize, StoreSize, StoreSize, StoreSize);
        var requestCount = 0;
        var writeCount = 0;
        slave.ModbusSlaveRequestReceived += (_, _) => requestCount++;
        slave.WriteComplete += (_, _) => writeCount++;

        var responses = CreateSlaveDispatchRequests().Select(slave.ApplyRequest).ToArray();

        await NativeAssert.That(responses.Length).IsEqualTo(TenRequests);
        await NativeAssert.That(responses[0].GetType()).IsEqualTo(typeof(ReadCoilsInputsResponse));
        await NativeAssert.That(responses[SecondAddress].GetType())
            .IsEqualTo(typeof(ReadHoldingInputRegistersResponse));
        await NativeAssert.That(responses[FourPoints].GetType()).IsEqualTo(typeof(DiagnosticsRequestResponse));
        await NativeAssert.That(writeCount).IsEqualTo(FiveWrites);
        await NativeAssert.That(requestCount).IsEqualTo(TenRequests);
        await NativeAssert.That(slave.DataStore.CoilDiscretes[FirstAddress]).IsTrue();
        await NativeAssert.That(slave.DataStore.HoldingRegisters[FirstAddress])
            .IsEqualTo(ThirdRegisterValue);

        var unsupported = slave.ApplyRequest(
            new ReadCoilsInputsRequest(byte.MaxValue, SlaveAddress, 0, FirstAddress));
        await NativeAssert.That(unsupported.GetType()).IsEqualTo(typeof(SlaveExceptionResponse));
        await NativeAssert.That(((SlaveExceptionResponse)unsupported).SlaveExceptionCode)
            .IsEqualTo(Modbus.IllegalFunction);

        EventHandler<ModbusSlaveRequestEventArgs> reject =
            (_, _) => throw new InvalidModbusRequestException(Modbus.IllegalDataAddress);
        slave.ModbusSlaveRequestReceived += reject;
        var rejected = (SlaveExceptionResponse)slave.ApplyRequest(
            new ReadCoilsInputsRequest(Modbus.ReadCoils, SlaveAddress, 0, FirstAddress));
        slave.ModbusSlaveRequestReceived -= reject;
        await NativeAssert.That(rejected.SlaveExceptionCode).IsEqualTo(Modbus.IllegalDataAddress);
    }

    /// <summary>Exercises optimized store locking, event delivery, materialization, and range guards.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task DataStore_OptimizedMethodsReadWriteAndValidateAsync()
    {
        using var dataStore = DataStoreFactory.CreateDefaultDataStore(StoreSize, StoreSize, StoreSize, StoreSize);
        var readEvents = 0;
        var writeEvents = 0;
        dataStore.DataStoreReadFrom += (_, _) => readEvents++;
        dataStore.DataStoreWrittenTo += (_, _) => writeEvents++;
        IEnumerable<ushort> generated = Enumerable.Range(FirstAddress, ThreeValues)
            .Select(static value => (ushort)value);

        dataStore.WriteDataOptimized(generated, dataStore.HoldingRegisters, 0);
        var values = dataStore.ReadDataOptimized<RegisterCollection, ushort>(
            dataStore.HoldingRegisters,
            static () => [],
            0,
            ThreeValues);

        await NativeAssert.That(values).IsEquivalentTo([FirstAddress, TwoPoints, ThreeValues]);
        await NativeAssert.That(readEvents).IsEqualTo(FirstAddress);
        await NativeAssert.That(writeEvents).IsEqualTo(FirstAddress);
        await NativeAssert.That(
                () => dataStore.ReadDataOptimized<RegisterCollection, ushort>(
                    null!,
                    static () => [],
                    0,
                    FirstAddress))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                () => dataStore.ReadDataOptimized<RegisterCollection, ushort>(
                    dataStore.HoldingRegisters,
                    null!,
                    0,
                    FirstAddress))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                () => dataStore.ReadDataOptimized<RegisterCollection, ushort>(
                    dataStore.HoldingRegisters,
                    static () => [],
                    StoreSize,
                    FirstAddress))
            .Throws<InvalidModbusRequestException>();
        await NativeAssert.That(
                () => dataStore.WriteDataOptimized([FirstRegisterValue], null!, 0))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                () => dataStore.WriteDataOptimized(
                    [FirstRegisterValue, SecondRegisterValue],
                    dataStore.HoldingRegisters,
                    StoreSize))
            .Throws<InvalidModbusRequestException>();
    }

    /// <summary>Exercises extension growth behavior and every public null guard.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task DataStoreExtensions_GrowCollectionsAndRejectNullStoresAsync()
    {
        using var dataStore = DataStoreFactory.CreateDefaultDataStore(StoreSize, StoreSize, StoreSize, StoreSize);
        dataStore.HoldingRegisters.Clear();
        dataStore.CoilDiscretes.Clear();
        DataStoreExtensions.WriteHoldingRegistersOptimized(
            dataStore,
            ThreeValues,
            [FirstRegisterValue]);
        DataStoreExtensions.WriteCoilsOptimized(dataStore, ThreeValues, [true]);
        DataStoreExtensions.WriteHoldingRegistersOptimized(dataStore, 0, null!);
        DataStoreExtensions.WriteCoilsOptimized(dataStore, 0, null!);

        await NativeAssert.That(dataStore.HoldingRegisters[ThreeValues]).IsEqualTo(FirstRegisterValue);
        await NativeAssert.That(dataStore.CoilDiscretes[ThreeValues]).IsTrue();
        await NativeAssert.That(
                () => DataStoreExtensions.ReadInputRegistersOptimized(null!, 0, FirstAddress))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(() => DataStoreExtensions.ReadCoilsOptimized(null!, 0, FirstAddress))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(() => DataStoreExtensions.ReadInputsOptimized(null!, 0, FirstAddress))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                () => DataStoreExtensions.WriteHoldingRegistersOptimized(null!, 0, []))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(() => DataStoreExtensions.WriteCoilsOptimized(null!, 0, []))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                () => DataStoreExtensions.BulkCopyHoldingRegisters(null!, dataStore, 0, FirstAddress))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                () => DataStoreExtensions.BulkCopyHoldingRegisters(dataStore, null!, 0, FirstAddress))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                () => DataStoreExtensions.BulkCopyCoils(null!, dataStore, 0, FirstAddress))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                () => DataStoreExtensions.BulkCopyCoils(dataStore, null!, 0, FirstAddress))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(() => DataStoreExtensions.ClearHoldingRegisters(null!, 0, FirstAddress))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(() => DataStoreExtensions.ClearCoils(null!, 0, FirstAddress))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                () => DataStoreExtensions.CompareHoldingRegisters(null!, dataStore, 0, FirstAddress))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                () => DataStoreExtensions.CompareHoldingRegisters(dataStore, null!, 0, FirstAddress))
            .Throws<ArgumentNullException>();
    }

    /// <summary>Exercises bounded sequence slices, array comparison, and exception constructor state.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task UtilityTypes_CoverEqualitySlicingAndExceptionStateAsync()
    {
        int[] first = [FirstAddress, SecondAddress, ThreeValues];
        int[] equal = [FirstAddress, SecondAddress, ThreeValues];
        var comparer = new ArrayEqualityComparer<int>();

        await NativeAssert.That(comparer.GetHashCode(first))
            .IsEqualTo(comparer.GetHashCode(equal));
        await NativeAssert.That(comparer.Equals(first, first)).IsTrue();
        await NativeAssert.That(comparer.Equals(first, null)).IsFalse();
        await NativeAssert.That(comparer.Equals(null, equal)).IsFalse();
        await NativeAssert.That(comparer.Equals(first, equal)).IsTrue();
        await NativeAssert.That(comparer.Equals(first, [FirstAddress])).IsFalse();
        await NativeAssert.That(comparer.Equals(first, [FirstAddress, SecondAddress, FourPoints])).IsFalse();
        await NativeAssert.That(comparer.GetHashCode(null!)).IsEqualTo(0);

        int[] sliceSource = [FirstAddress, SecondAddress, ThreeValues];
        await NativeAssert.That(SequenceExtensions.Slice(sliceSource, FirstAddress, TwoPoints))
            .IsEquivalentTo([SecondAddress, ThreeValues]);
        await NativeAssert.That(SequenceExtensions.Slice(sliceSource.Where(static _ => true), 0, FirstAddress))
            .IsEquivalentTo([(int)FirstAddress]);
        await NativeAssert.That(() => SequenceExtensions.Slice<int>(null!, 0, 0))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(() => SequenceExtensions.Slice(sliceSource, -1, FirstAddress))
            .Throws<ArgumentOutOfRangeException>();
        await NativeAssert.That(() => SequenceExtensions.Slice(sliceSource, StoreSize, 0))
            .Throws<ArgumentOutOfRangeException>();
        await NativeAssert.That(() => SequenceExtensions.Slice(sliceSource, 0, -1))
            .Throws<ArgumentOutOfRangeException>();
        await NativeAssert.That(() => SequenceExtensions.Slice(sliceSource, SecondAddress, TwoPoints))
            .Throws<ArgumentOutOfRangeException>();

        await AssertInvalidRequestConstructorsAsync();
    }

    /// <summary>Exercises pooled buffer lifecycle, bounded copies, comparisons, and disposal.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task BufferManager_RentCopyReturnAndDisposeAsync()
    {
        using var manager = new ModbusBufferManager();
        var bytes = manager.RentByteBuffer(ByteBufferLength);
        var words = manager.RentUshortBuffer(SmallBufferLength);
        var bits = manager.RentBoolBuffer(SmallBufferLength);

        bytes[0] = NineCoils;
        words[0] = FirstRegisterValue;
        bits[0] = true;
        manager.ReturnByteBuffer(bytes, true);
        manager.ReturnUshortBuffer(words, true);
        manager.ReturnBoolBuffer(bits, true);

        var destination = new int[ThreeValues];
        await NativeAssert.That(
                ModbusBufferManager.CopyData(
                    [FirstAddress, SecondAddress, ThreeValues, SmallBufferLength],
                    FirstAddress,
                    destination,
                    0,
                    StoreSize))
            .IsEqualTo(ThreeValues);
        await NativeAssert.That(destination)
            .IsEquivalentTo([SecondAddress, ThreeValues, SmallBufferLength]);
        await NativeAssert.That(
                ModbusBufferManager.CopyData((int[]?)null!, 0, destination, 0, FirstAddress))
            .IsEqualTo(0);
        await NativeAssert.That(
                ModbusBufferManager.CopyData([FirstAddress], SecondAddress, destination, 0, FirstAddress))
            .IsEqualTo(0);
        await NativeAssert.That(
                ModbusBufferManager.CompareArrays((int[]?)null!, (int[]?)null!))
            .IsTrue();
        await NativeAssert.That(
                ModbusBufferManager.CompareArrays(
                    [FirstAddress, SecondAddress],
                    [FirstAddress, SecondAddress]))
            .IsTrue();
        await NativeAssert.That(
                ModbusBufferManager.CompareArrays(
                    [FirstAddress, SecondAddress],
                    [FirstAddress, ThreeValues]))
            .IsFalse();
        await NativeAssert.That(
                ModbusBufferManager.CompareArrays([FirstAddress], [FirstAddress, SecondAddress]))
            .IsFalse();

        ModbusBufferManager.ClearArray(destination);
        ModbusBufferManager.ClearArray((int[]?)null!);
        await NativeAssert.That(destination).IsEquivalentTo(new int[ThreeValues]);

        manager.Dispose();
        manager.ReturnByteBuffer([], true);
        await NativeAssert.That(() => manager.RentByteBuffer(1)).Throws<ObjectDisposedException>();
        await NativeAssert.That(() => manager.RentUshortBuffer(1)).Throws<ObjectDisposedException>();
        await NativeAssert.That(() => manager.RentBoolBuffer(1)).Throws<ObjectDisposedException>();
    }

    /// <summary>Exercises deterministic RTU frame construction, parsing, validation, and failures.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task OptimizedMessageFactory_CreatesAndParsesValidFramesAsync()
    {
        var frames = CreateValidOptimizedFrames();

        foreach (var frame in frames)
        {
            await NativeAssert.That(OptimizedModbusMessageFactory.ValidateMessageCrc(frame)).IsTrue();
        }

        byte[] registerData =
        [
            SlaveAddress,
            Modbus.ReadHoldingRegisters,
            RegisterResponseByteCount,
            0x12,
            0x34,
            0x56,
            0x78,
        ];
        var registerCrc = ModbusUtility.CalculateCrc(registerData);
        var registerResponse = registerData.Concat(registerCrc).ToArray();
        await NativeAssert.That(
                OptimizedModbusMessageFactory.ParseReadHoldingRegistersResponse(registerResponse))
            .IsEquivalentTo([FramedRegisterOne, FramedRegisterTwo]);

        byte[] coilData =
        [
            SlaveAddress,
            Modbus.ReadCoils,
            CoilResponseByteCount,
            0b01010101,
            0b00000001,
        ];
        var coilResponse = coilData.Concat(ModbusUtility.CalculateCrc(coilData)).ToArray();
        await NativeAssert.That(OptimizedModbusMessageFactory.ParseReadCoilsResponse(coilResponse, NineCoils))
            .IsEquivalentTo([true, false, true, false, true, false, true, false, true]);

        var corrupted = (byte[])frames[0].Clone();
        corrupted[SecondAddress] ^= SlaveAddress;
        await NativeAssert.That(OptimizedModbusMessageFactory.ValidateMessageCrc(corrupted)).IsFalse();
        await NativeAssert.That(OptimizedModbusMessageFactory.ValidateMessageCrc(null!)).IsFalse();
        await NativeAssert.That(
                OptimizedModbusMessageFactory.ValidateMessageCrc(
                    [SlaveAddress, CoilResponseByteCount, RegisterResponseByteCount]))
            .IsFalse();
    }

    /// <summary>Exercises optimized frame input validation failures.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task OptimizedMessageFactory_RejectsInvalidFramesAsync()
    {
        await NativeAssert.That(
                () => OptimizedModbusMessageFactory.CreateWriteMultipleRegistersRequest(
                    SlaveAddress,
                    0,
                    null!))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                () => OptimizedModbusMessageFactory.CreateWriteMultipleCoilsRequest(SlaveAddress, 0, null!))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                () => OptimizedModbusMessageFactory.ParseReadHoldingRegistersResponse(
                    [SlaveAddress, Modbus.ReadHoldingRegisters, CoilResponseByteCount, 0]))
            .Throws<ArgumentException>();
        await NativeAssert.That(
                () => OptimizedModbusMessageFactory.ParseReadCoilsResponse(
                    [SlaveAddress, Modbus.ReadCoils, CoilResponseByteCount, SlaveAddress],
                    SlaveAddress))
            .Throws<ArgumentException>();
    }

    /// <summary>Exercises endian conversion, ASCII conversion, CRC/LRC, and span boundary guards.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusUtility_RoundTripsNumericAndTextRepresentationsAsync()
    {
        await NativeAssert.That(ModbusUtility.GetUInt32(0x1234, 0x5678)).IsEqualTo(ExpectedUInt32);
        await NativeAssert.That(ModbusUtility.GetAsciiBytes((byte)0xAB, (byte)0x01))
            .IsEquivalentTo([(byte)'A', (byte)'B', (byte)'0', (byte)'1']);
        await NativeAssert.That(ModbusUtility.GetAsciiBytes((ushort)0xABCD))
            .IsEquivalentTo([(byte)'A', (byte)'B', (byte)'C', (byte)'D']);
        await NativeAssert.That(ModbusUtility.HexToBytes("00A1ff"))
            .IsEquivalentTo([(byte)0x00, (byte)0xA1, (byte)0xFF]);

        var decoded = new byte[ThreeValues];
        await NativeAssert.That(ModbusUtility.HexToBytes("00A1ff".AsSpan(), decoded))
            .IsEqualTo(ThreeValues);
        await NativeAssert.That(decoded)
            .IsEquivalentTo([(byte)0x00, (byte)0xA1, (byte)0xFF]);

        var words = new ushort[SecondAddress];
        await NativeAssert.That(ModbusUtility.NetworkBytesToHostUInt16([0x12, 0x34, 0x56, 0x78], words))
            .IsEqualTo(SecondAddress);
        await NativeAssert.That(words)
            .IsEquivalentTo([FramedRegisterOne, FramedRegisterTwo]);

        byte[] crcInput = [SlaveAddress, Modbus.ReadHoldingRegisters, 0, 0, 0, SlaveAddress];
        var crc = new byte[SecondAddress];
        await NativeAssert.That(ModbusUtility.CalculateCrc(crcInput, crc)).IsEqualTo(SecondAddress);
        await NativeAssert.That(crc.ToArray()).IsEquivalentTo(ModbusUtility.CalculateCrc(crcInput));
        await NativeAssert.That(ModbusUtility.CalculateLrc(crcInput.AsSpan()))
            .IsEqualTo(ModbusUtility.CalculateLrc(crcInput));

        var doubleRegisters = new ushort[SmallBufferLength];
        var floatRegisters = new ushort[SecondAddress];
        foreach (var swapWords in new[] { false, true })
        {
            ModbusUtility.WriteDouble(Math.PI, doubleRegisters, swapWords);
            ModbusUtility.WriteSingle(FloatValue, floatRegisters, swapWords);

            await NativeAssert.That(ModbusUtility.ReadDouble(doubleRegisters, swapWords)).IsEqualTo(Math.PI);
            await NativeAssert.That(ModbusUtility.ReadSingle(floatRegisters, swapWords)).IsEqualTo(FloatValue);
        }

        await NativeAssert.That(() => ModbusUtility.HexToBytes("0")).Throws<FormatException>();
        await NativeAssert.That(() => ModbusUtility.HexToBytes("GG")).Throws<FormatException>();
        await NativeAssert.That(() => ModbusUtility.NetworkBytesToHostUInt16([1]))
            .Throws<FormatException>();
        await NativeAssert.That(() => ModbusUtility.WriteDouble(1, new ushort[3], false))
            .Throws<ArgumentException>();
        await NativeAssert.That(() => ModbusUtility.ReadSingle(new ushort[1], false))
            .Throws<ArgumentException>();
    }

    /// <summary>Verifies immutable server snapshot behavior and value semantics.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task ServerSnapshot_ClonesAndComparesDataAsync()
    {
        var source = new ushort[] { FirstAddress, SecondAddress };
        var timestamp = TestFrameworkCompatibilityExtensions.UnixEpoch;
        var left = new ModbusServerDataSnapshot(source, [ThreeValues], [true], [false], timestamp);
        var right = new ModbusServerDataSnapshot(
            [FirstAddress, SecondAddress],
            [ThreeValues],
            [true],
            [false],
            timestamp.AddDays(FirstAddress));
        var different = new ModbusServerDataSnapshot(
            [FirstAddress, NineCoils],
            [ThreeValues],
            [true],
            [false],
            timestamp);
        source[0] = DifferentRegisterValue;

        await NativeAssert.That(left.HoldingRegisters[0]).IsEqualTo((ushort)1);
        await NativeAssert.That(left.IsEmpty).IsFalse();
        await NativeAssert.That(new ModbusServerDataSnapshot().IsEmpty).IsTrue();
        await NativeAssert.That(left.Equals(left)).IsTrue();
        await NativeAssert.That(left.Equals(right)).IsTrue();
        await NativeAssert.That(left == right).IsTrue();
        await NativeAssert.That(left != different).IsTrue();
        await NativeAssert.That(left.Equals("snapshot")).IsFalse();
        await NativeAssert.That(left.GetHashCode()).IsEqualTo(right.GetHashCode());
    }

    /// <summary>Creates representative optimized RTU request frames.</summary>
    /// <returns>The created request frames.</returns>
    private static byte[][] CreateValidOptimizedFrames() =>
    [
        OptimizedModbusMessageFactory.CreateReadHoldingRegistersRequest(
            SlaveAddress,
            SecondAddress,
            ThreeValues),
        OptimizedModbusMessageFactory.CreateReadCoilsRequest(SlaveAddress, SecondAddress, NineCoils),
        OptimizedModbusMessageFactory.CreateWriteSingleRegisterRequest(
            SlaveAddress,
            SecondAddress,
            FramedRegisterOne),
        OptimizedModbusMessageFactory.CreateWriteMultipleRegistersRequest(
            SlaveAddress,
            SecondAddress,
            [FramedRegisterOne, FramedRegisterTwo]),
        OptimizedModbusMessageFactory.CreateWriteSingleCoilRequest(
            SlaveAddress,
            SecondAddress,
            true),
        OptimizedModbusMessageFactory.CreateWriteSingleCoilRequest(
            SlaveAddress,
            SecondAddress,
            false),
        OptimizedModbusMessageFactory.CreateWriteMultipleCoilsRequest(
            SlaveAddress,
            SecondAddress,
            [true, false, true, false, true, false, true, false, true]),
    ];

    /// <summary>Creates one request for every supported slave dispatch branch.</summary>
    /// <returns>The protocol requests.</returns>
    private static IModbusMessage[] CreateSlaveDispatchRequests() =>
    [
        new ReadCoilsInputsRequest(Modbus.ReadCoils, SlaveAddress, 0, TwoPoints),
        new ReadCoilsInputsRequest(Modbus.ReadInputs, SlaveAddress, 0, TwoPoints),
        new ReadHoldingInputRegistersRequest(Modbus.ReadHoldingRegisters, SlaveAddress, 0, TwoPoints),
        new ReadHoldingInputRegistersRequest(Modbus.ReadInputRegisters, SlaveAddress, 0, TwoPoints),
        new DiagnosticsRequestResponse(
            Modbus.DiagnosticsReturnQueryData,
            SlaveAddress,
            new RegisterCollection(FirstRegisterValue)),
        new WriteSingleCoilRequestResponse(SlaveAddress, 0, true),
        new WriteSingleRegisterRequestResponse(SlaveAddress, 0, FirstRegisterValue),
        new WriteMultipleCoilsRequest(SlaveAddress, 0, new DiscreteCollection(true, false)),
        new WriteMultipleRegistersRequest(
            SlaveAddress,
            0,
            new RegisterCollection(SecondRegisterValue, FirstRegisterValue)),
        new ReadWriteMultipleRegistersRequest(
            SlaveAddress,
            0,
            TwoPoints,
            0,
            new RegisterCollection(ThirdRegisterValue)),
    ];

    /// <summary>Exercises every invalid-request exception constructor.</summary>
    /// <returns>A task representing the asynchronous assertion.</returns>
    private static async Task AssertInvalidRequestConstructorsAsync()
    {
        var inner = new InvalidOperationException("inner");
        var exceptions = new[]
        {
            new InvalidModbusRequestException(),
            new InvalidModbusRequestException(FailureMessage),
            new InvalidModbusRequestException(FailureMessage, inner),
            new InvalidModbusRequestException(Modbus.IllegalDataAddress),
            new InvalidModbusRequestException(FailureMessage, Modbus.IllegalDataAddress),
            new InvalidModbusRequestException(Modbus.IllegalDataAddress, inner),
            new InvalidModbusRequestException(FailureMessage, Modbus.IllegalDataAddress, inner),
        };

        await NativeAssert.That(exceptions.Length).IsEqualTo(SevenValues);
        await NativeAssert.That(exceptions[ThreeValues].ExceptionCode)
            .IsEqualTo(Modbus.IllegalDataAddress);
        await NativeAssert.That(exceptions[FiveWrites].InnerException).IsSameReferenceAs(inner);
        await NativeAssert.That(exceptions[SixValues].InnerException).IsSameReferenceAs(inner);
    }

    /// <summary>Creates representative supported register values and their point counts.</summary>
    /// <returns>The codec samples.</returns>
    private static (object Value, ushort Count)[] CreateRegisterCodecSamples() =>
    [
        (FramedRegisterOne, FirstAddress),
        ((short)-FirstRegisterValue, FirstAddress),
        (ExpectedUInt32, TwoPoints),
        (IntValue, TwoPoints),
        (FloatValue, TwoPoints),
        (Math.PI, FourPoints),
        (new ushort[] { FirstRegisterValue, SecondRegisterValue }, TwoPoints),
        (new short[] { -FirstRegisterValue, (short)SecondRegisterValue }, TwoPoints),
        (new uint[] { ExpectedUInt32, UIntValue }, FourPoints),
        (new int[] { IntValue, FirstRegisterValue }, FourPoints),
        (new float[] { FloatValue, -FloatValue }, FourPoints),
        (new double[] { Math.PI, -Math.PI }, StoreSize / TwoPoints),
    ];

    /// <summary>Creates a logical tag for direct codec coverage.</summary>
    /// <param name="count">The point count.</param>
    /// <param name="type">The exposed CLR type.</param>
    /// <param name="byteOrder">The configured byte order.</param>
    /// <param name="area">The Modbus data area.</param>
    /// <returns>The validated logical tag.</returns>
    private static ModbusLogicalTag CreateCodecTag(
        ushort count,
        Type type,
        ModbusByteOrder byteOrder,
        ModbusDataArea area = ModbusDataArea.HoldingRegister) =>
        new(new ModbusTagConfiguration("Codec", SlaveAddress, area, 0, count, type)
        {
            ByteOrder = byteOrder,
            AccessMode = area is ModbusDataArea.DiscreteInput or ModbusDataArea.InputRegister
                ? LogicalTagAccessMode.Read
                : LogicalTagAccessMode.ReadWrite,
        });

    /// <summary>Compares scalar or array values produced by codec round trips.</summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    /// <returns><c>true</c> when both values are equal.</returns>
    private static bool ValuesEqual(object expected, object actual) =>
        expected is Array expectedArray && actual is Array actualArray
            ? expectedArray.Cast<object>().SequenceEqual(actualArray.Cast<object>())
            : expected.Equals(actual);

    /// <summary>Provides a stable local time to simulation modes.</summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        /// <inheritdoc />
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => TestFrameworkCompatibilityExtensions.UnixEpoch;
    }
}
