// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
using TUnit.Assertions;
using TUnit.Core;

namespace IoT.DriverCore.ABPlcRx.Tests;

/// <summary>Exercises the deterministic Allen-Bradley simulator through its public and native surfaces.</summary>
public sealed class ABPlcSimulatorTests
{
    /// <summary>Common logical and raw scalar tag names.</summary>
    private const string BooleanTagName = "Boolean";

    /// <summary>Common byte tag name.</summary>
    private const string ByteTagName = "Byte";

    /// <summary>Common signed-byte tag name.</summary>
    private const string SByteTagName = "SByte";

    /// <summary>Common signed 16-bit tag name.</summary>
    private const string Int16TagName = "Int16";

    /// <summary>Common unsigned 16-bit tag name.</summary>
    private const string UInt16TagName = "UInt16";

    /// <summary>Common signed 32-bit tag name.</summary>
    private const string Int32TagName = "Int32";

    /// <summary>Common unsigned 32-bit tag name.</summary>
    private const string UInt32TagName = "UInt32";

    /// <summary>Common signed 64-bit tag name.</summary>
    private const string Int64TagName = "Int64";

    /// <summary>Common unsigned 64-bit tag name.</summary>
    private const string UInt64TagName = "UInt64";

    /// <summary>Common single-precision tag name.</summary>
    private const string SingleTagName = "Single";

    /// <summary>Common double-precision tag name.</summary>
    private const string DoubleTagName = "Double";

    /// <summary>Common string tag name.</summary>
    private const string StringTagName = "String";

    /// <summary>Bit logical tag name.</summary>
    private const string BitTagName = "Bit";

    /// <summary>Flags logical tag name.</summary>
    private const string FlagsTagName = "Flags";

    /// <summary>Read-only logical tag name.</summary>
    private const string ReadOnlyTagName = "ReadOnly";

    /// <summary>Read-only physical tag name.</summary>
    private const string ReadOnlyPhysicalTagName = "ReadOnlyPhysical";

    /// <summary>Write-only logical tag name.</summary>
    private const string WriteOnlyTagName = "WriteOnly";

    /// <summary>Missing logical tag name.</summary>
    private const string MissingTagName = "Missing";

    /// <summary>Counter logical tag name.</summary>
    private const string CounterTagName = "Counter";

    /// <summary>Counter physical tag name.</summary>
    private const string CounterPhysicalTagName = "CounterPhysical";

    /// <summary>Flags physical tag name.</summary>
    private const string FlagsPhysicalTagName = "FlagsPhysical";

    /// <summary>Group name used by public observation tests.</summary>
    private const string GroupName = "Group";

    /// <summary>Native direct-access tag name.</summary>
    private const string NativeTagName = "Native";

    /// <summary>Sample logical byte value.</summary>
    private const byte SampleByte = 201;

    /// <summary>Sample logical signed-byte value.</summary>
    private const sbyte SampleSByte = -101;

    /// <summary>Sample logical signed 16-bit value.</summary>
    private const short SampleInt16 = -1_201;

    /// <summary>Sample logical unsigned 16-bit value.</summary>
    private const ushort SampleUInt16 = 62_001;

    /// <summary>Sample logical signed 32-bit value.</summary>
    private const int SampleInt32 = -1_234_567;

    /// <summary>Sample logical unsigned 32-bit value.</summary>
    private const uint SampleUInt32 = 3_234_567_890U;

    /// <summary>Sample logical signed 64-bit value.</summary>
    private const long SampleInt64 = -1_234_567_890_123L;

    /// <summary>Sample logical unsigned 64-bit value.</summary>
    private const ulong SampleUInt64 = 12_345_678_901_234UL;

    /// <summary>Sample logical single-precision value.</summary>
    private const float SampleSingle = 12.25F;

    /// <summary>Sample logical double-precision value.</summary>
    private const double SampleDouble = 42.125D;

    /// <summary>Selected bit index.</summary>
    private const int SelectedBit = 2;

    /// <summary>Configured logical bit index.</summary>
    private const int LogicalBit = 3;

    /// <summary>Expected number of repeated results.</summary>
    private const int ExpectedPairCount = 2;

    /// <summary>Expected raw bit-mask value.</summary>
    private const short ExpectedBitMask = 8;

    /// <summary>Initial counter value.</summary>
    private const int InitialCounterValue = 11;

    /// <summary>Seeded read-only value.</summary>
    private const int ReadOnlyValue = 17;

    /// <summary>Denied write sample.</summary>
    private const int DeniedWriteValue = 4;

    /// <summary>Writer counter value.</summary>
    private const int WriterCounterValue = 23;

    /// <summary>Bulk counter value.</summary>
    private const int BulkCounterValue = 31;

    /// <summary>Bulk flags value.</summary>
    private const short BulkFlagsValue = 5;

    /// <summary>Typed counter value.</summary>
    private const int TypedCounterValue = 37;

    /// <summary>Native operation timeout in milliseconds.</summary>
    private const int NativeTimeout = 100;

    /// <summary>Native direct buffer size.</summary>
    private const int NativeBufferSize = 128;

    /// <summary>Native value loaded by a read.</summary>
    private const int NativeReadValue = 901;

    /// <summary>Invalid simulator operation numeric value.</summary>
    private const int InvalidOperationValue = 99;

    /// <summary>Maximum supported simulator string length plus one.</summary>
    private const int ExcessiveStringLength = 85;

    /// <summary>Raw single-precision sample.</summary>
    private const float RawSingle = 1.5F;

    /// <summary>Raw double-precision sample.</summary>
    private const double RawDouble = 2.5D;

    /// <summary>Defensive-copy replacement byte.</summary>
    private const byte ReplacementByte = 9;

    /// <summary>Native floating-point sample value.</summary>
    private const float NativeFloat32 = 1.25F;

    /// <summary>Native double-precision sample value.</summary>
    private const double NativeFloat64 = 2.25D;

    /// <summary>Native signed 16-bit sample value.</summary>
    private const short NativeInt16 = -16;

    /// <summary>Native signed 32-bit sample value.</summary>
    private const int NativeInt32 = -32;

    /// <summary>Native signed 64-bit sample value.</summary>
    private const long NativeInt64 = -64L;

    /// <summary>Native signed 8-bit sample value.</summary>
    private const sbyte NativeInt8 = -8;

    /// <summary>Native unsigned 16-bit sample value.</summary>
    private const ushort NativeUInt16 = 16;

    /// <summary>Native unsigned 32-bit sample value.</summary>
    private const uint NativeUInt32 = 32U;

    /// <summary>Native unsigned 64-bit sample value.</summary>
    private const ulong NativeUInt64 = 64UL;

    /// <summary>Native unsigned 8-bit sample value.</summary>
    private const byte NativeUInt8 = 8;

    /// <summary>Offset of the native double value.</summary>
    private const int Float64Offset = 8;

    /// <summary>Offset of the native signed 16-bit value.</summary>
    private const int Int16Offset = 16;

    /// <summary>Offset of the native signed 32-bit value.</summary>
    private const int Int32Offset = 20;

    /// <summary>Offset of the native signed 64-bit value.</summary>
    private const int Int64Offset = 24;

    /// <summary>Offset of the native signed 8-bit value.</summary>
    private const int Int8Offset = 32;

    /// <summary>Offset of the native unsigned 16-bit value.</summary>
    private const int UInt16Offset = 34;

    /// <summary>Offset of the native unsigned 32-bit value.</summary>
    private const int UInt32Offset = 36;

    /// <summary>Offset of the native unsigned 64-bit value.</summary>
    private const int UInt64Offset = 40;

    /// <summary>Offset of the native unsigned 8-bit value.</summary>
    private const int UInt8Offset = 48;

    /// <summary>Short polling interval used by observable tests.</summary>
    private static readonly TimeSpan ShortInterval = TimeSpan.FromMilliseconds(5);

    /// <summary>Operation timeout used by simulator tests.</summary>
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromMilliseconds(250);

    /// <summary>Maximum wait for an observable assertion.</summary>
    private static readonly TimeSpan ObservationTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Exercises all supported logical scalar registrations and bulk IO.</summary>
    /// <returns><see cref="Task"/> representing the test.</returns>
    [Test]
    internal async Task LogicalTagsRoundTripEverySupportedScalarTypeAsync()
    {
        using var simulator = CreateSimulator();
        using var client = simulator.CreateLogicalTagClient();
        var definitions = new[]
        {
            client.CreateTag(BooleanTagName, "BoolTag", "boolean"),
            client.CreateTag(ByteTagName, "ByteTag", "uint8"),
            client.CreateTag(SByteTagName, "SByteTag", "int8"),
            client.CreateTag(Int16TagName, "Int16Tag", "system.int16"),
            client.CreateTag(UInt16TagName, "UInt16Tag", "system.uint16"),
            client.CreateTag(Int32TagName, "Int32Tag", "dint"),
            client.CreateTag(UInt32TagName, "UInt32Tag", "udint"),
            client.CreateTag(Int64TagName, "Int64Tag", "lint"),
            client.CreateTag(UInt64TagName, "UInt64Tag", "ulint"),
            client.CreateTag(SingleTagName, "SingleTag", "real"),
            client.CreateTag(DoubleTagName, "DoubleTag", "lreal"),
            client.CreateTag(StringTagName, "StringTag", "system.string"),
        };
        var now = TimeProvider.System.GetUtcNow();
        var values = new LogicalTagValue[]
        {
            new(BooleanTagName, true, now),
            new(ByteTagName, SampleByte, now),
            new(SByteTagName, SampleSByte, now),
            new(Int16TagName, SampleInt16, now),
            new(UInt16TagName, SampleUInt16, now),
            new(Int32TagName, SampleInt32, now),
            new(UInt32TagName, SampleUInt32, now),
            new(Int64TagName, SampleInt64, now),
            new(UInt64TagName, SampleUInt64, now),
            new(SingleTagName, SampleSingle, now),
            new(DoubleTagName, SampleDouble, now),
            new(StringTagName, "simulated", now),
        };

        var writes = await client.WriteManyAsync(values);
        var reads = await client.ReadManyAsync(definitions.Select(static tag => tag.Name).ToArray());

        await Assert.That(writes.All(static result => result.Succeeded)).IsTrue();
        await Assert.That(reads.All(static result => result.Succeeded)).IsTrue();
        await Assert.That(simulator.GetTagValue<bool>("BoolTag", default)).IsTrue();
        await Assert.That(simulator.GetTagValue<byte>("ByteTag", default)).IsEqualTo(SampleByte);
        await Assert.That(simulator.GetTagValue<sbyte>("SByteTag", default)).IsEqualTo(SampleSByte);
        await Assert.That(simulator.GetTagValue<short>("Int16Tag", default)).IsEqualTo(SampleInt16);
        await Assert.That(simulator.GetTagValue<ushort>("UInt16Tag", default)).IsEqualTo(SampleUInt16);
        await Assert.That(simulator.GetTagValue<int>("Int32Tag", default)).IsEqualTo(SampleInt32);
        await Assert.That(simulator.GetTagValue<uint>("UInt32Tag", default)).IsEqualTo(SampleUInt32);
        await Assert.That(simulator.GetTagValue<long>("Int64Tag", default)).IsEqualTo(SampleInt64);
        await Assert.That(simulator.GetTagValue<ulong>("UInt64Tag", default)).IsEqualTo(SampleUInt64);
        await Assert.That(simulator.GetTagValue<float>("SingleTag", default)).IsEqualTo(SampleSingle);
        await Assert.That(simulator.GetTagValue<double>("DoubleTag", default)).IsEqualTo(SampleDouble);
        await Assert.That(simulator.GetTagValue<string>("StringTag", default)).IsEqualTo("simulated");
        await Assert.That(simulator.ActiveHandleCount).IsEqualTo(definitions.Length);
        await Assert.That(simulator.OperationLog.Count).IsGreaterThan(definitions.Length);
        await Assert.That(simulator.TagStatuses.Values.All(static status => status == PlcTagStatus.StatusOK)).IsTrue();
    }

    /// <summary>Exercises bit projection, access validation, duplicates, missing tags, and lifecycle changes.</summary>
    /// <returns><see cref="Task"/> representing the test.</returns>
    [Test]
    internal async Task LogicalTagsHandleBitsValidationFaultsAndLifecycleAsync()
    {
        using var simulator = CreateSimulator();
        using var client = simulator.CreateLogicalTagClient();
        RegisterValidationTags(simulator, client);
        await AssertLogicalValidationAsync(simulator, client);
        await AssertFaultAndConnectionLifecycleAsync(simulator, client);
        simulator.ClearOperationLog();
        await Assert.That(simulator.OperationLog).IsEmpty();
        _ = client.RemoveTag(BitTagName);
        await Assert.That(client.RemoveTag(BitTagName)).IsFalse();
    }

    /// <summary>Exercises public observation, writer, bulk, scan, ping, and disposal forwarding.</summary>
    /// <returns><see cref="Task"/> representing the test.</returns>
    [Test]
    internal async Task PublicFacadeSupportsObservationBulkIoAndDisposalAsync()
    {
        var simulator = CreateSimulator();
        simulator.AddUpdateTagItem<int>(CounterTagName, CounterPhysicalTagName, GroupName, default);
        simulator.AddUpdateTagItem<short>(FlagsTagName, FlagsPhysicalTagName, GroupName, default);
        simulator.ScanEnabled = false;
        simulator.SetTagValue(CounterPhysicalTagName, InitialCounterValue);
        simulator.SetTagValue(FlagsPhysicalTagName, (short)0);
        await AssertFacadeValuesAndBulkAsync(simulator);
        await AssertFacadeObservationAndDisposalAsync(simulator);
    }

    /// <summary>Exercises the raw scalar codec, defensive copies, and validation errors.</summary>
    /// <returns><see cref="Task"/> representing the test.</returns>
    [Test]
    internal async Task RawMemoryCodecSupportsScalarsAndRejectsInvalidValuesAsync()
    {
        using var simulator = new ABPlcSimulator();
        ExerciseFacadeOverloads(simulator);
        simulator.SetTagValue(BooleanTagName, true);
        simulator.SetTagValue(ByteTagName, byte.MaxValue);
        simulator.SetTagValue(SByteTagName, sbyte.MinValue);
        simulator.SetTagValue(Int16TagName, short.MinValue);
        simulator.SetTagValue(UInt16TagName, ushort.MaxValue);
        simulator.SetTagValue(Int32TagName, int.MinValue);
        simulator.SetTagValue(UInt32TagName, uint.MaxValue);
        simulator.SetTagValue(Int64TagName, long.MinValue);
        simulator.SetTagValue(UInt64TagName, ulong.MaxValue);
        simulator.SetTagValue(SingleTagName, RawSingle);
        simulator.SetTagValue(DoubleTagName, RawDouble);
        simulator.SetTagValue(StringTagName, "£ simulated");

        await Assert.That(simulator.GetTagValue<bool>(BooleanTagName, default)).IsTrue();
        await Assert.That(simulator.GetTagValue<byte>(ByteTagName, default)).IsEqualTo(byte.MaxValue);
        await Assert.That(simulator.GetTagValue<sbyte>(SByteTagName, default)).IsEqualTo(sbyte.MinValue);
        await Assert.That(simulator.GetTagValue<short>(Int16TagName, default)).IsEqualTo(short.MinValue);
        await Assert.That(simulator.GetTagValue<ushort>(UInt16TagName, default)).IsEqualTo(ushort.MaxValue);
        await Assert.That(simulator.GetTagValue<int>(Int32TagName, default)).IsEqualTo(int.MinValue);
        await Assert.That(simulator.GetTagValue<uint>(UInt32TagName, default)).IsEqualTo(uint.MaxValue);
        await Assert.That(simulator.GetTagValue<long>(Int64TagName, default)).IsEqualTo(long.MinValue);
        await Assert.That(simulator.GetTagValue<ulong>(UInt64TagName, default)).IsEqualTo(ulong.MaxValue);
        await Assert.That(simulator.GetTagValue<float>(SingleTagName, default)).IsEqualTo(RawSingle);
        await Assert.That(simulator.GetTagValue<double>(DoubleTagName, default)).IsEqualTo(RawDouble);
        await Assert.That(simulator.GetTagValue<string>(StringTagName, default)).IsEqualTo("£ simulated");

        var bytes = simulator.GetTagBytes(Int32TagName);
        bytes[0] = 0;
        await Assert.That(simulator.GetTagValue<int>(Int32TagName, default)).IsEqualTo(int.MinValue);
        simulator.SetTagBytes("Copy", [1, ExpectedPairCount, LogicalBit]);
        var copy = simulator.GetTagBytes("Copy");
        copy[0] = ReplacementByte;
        await Assert.That(simulator.GetTagBytes("Copy")[0]).IsEqualTo((byte)1);

        _ = Assert.Throws<ArgumentException>(() => simulator.SetTagBytes(" ", [1]));
        _ = Assert.Throws<ArgumentNullException>(() => simulator.SetTagBytes("Null", null!));
        _ = Assert.Throws<KeyNotFoundException>(() => simulator.GetTagBytes(MissingTagName));
        _ = Assert.Throws<ArgumentNullException>(() => simulator.SetTagValue<string>("Null", null!));
        _ = Assert.Throws<NotSupportedException>(
            () => simulator.SetTagValue("Unsupported", TimeProvider.System.GetUtcNow().UtcDateTime));
        simulator.SetTagBytes("Short", [1]);
        _ = Assert.Throws<InvalidOperationException>(() => simulator.GetTagValue<int>("Short", default));
        simulator.SetTagBytes("BadStringLength", [byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue]);
        _ = Assert.Throws<InvalidOperationException>(
            () => simulator.GetTagValue<string>("BadStringLength", default));
        simulator.SetTagBytes("TruncatedString", [ExpectedPairCount, 0, 0, 0, (byte)'A']);
        _ = Assert.Throws<InvalidOperationException>(
            () => simulator.GetTagValue<string>("TruncatedString", default));
        _ = Assert.Throws<NotSupportedException>(
            () => simulator.GetTagValue<DateTime>(BooleanTagName, default));
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => simulator.SetTagValue("LongString", new string('x', ExcessiveStringLength)));
    }

    /// <summary>Exercises every native operation, scalar accessor, scripted result, and validation path.</summary>
    /// <returns><see cref="Task"/> representing the test.</returns>
    [Test]
    internal async Task NativeSimulatorCoversLifecycleScalarAndValidationPathsAsync()
    {
        var native = new SimulatedPlcTagNative(TimeProvider.System);
        var api = (IPlcTagNative)native;
        native.SetTagBytes(NativeTagName, [1]);
        var handle = api.Create(
            "protocol=ab_eip&gateway=127.0.0.1&plc=controllogix&name=Native&elem_size=16&elem_count=8",
            NativeTimeout);

        await Assert.That(handle).IsGreaterThan(0);
        await Assert.That(native.ActiveHandleCount).IsEqualTo(1);
        await Assert.That(api.GetSize(handle)).IsEqualTo(NativeBufferSize);
        await Assert.That(api.GetStatus(handle)).IsEqualTo(PlcTagStatus.StatusOK);
        await Assert.That(api.Lock(handle)).IsEqualTo(PlcTagStatus.StatusOK);
        await Assert.That(api.Unlock(handle)).IsEqualTo(PlcTagStatus.StatusOK);
        await Assert.That(api.Abort(handle)).IsEqualTo(PlcTagStatus.StatusOK);
        await AssertNativeScalarsAsync(native, api, handle);
        await AssertNativeFaultsAsync(native, api, handle);
        await AssertNativeValidationAndDisposalAsync(native, api);
    }

    /// <summary>Registers tags used by logical validation tests.</summary>
    /// <param name="simulator">The simulator.</param>
    /// <param name="client">The logical client.</param>
    private static void RegisterValidationTags(ABPlcSimulator simulator, ABLogicalTagClient client)
    {
        client.RegisterTag(
            new LogicalTag(
                BitTagName,
                FlagsTagName,
                "bool",
                new LogicalTagOptions
                {
                    GroupName = "Fast",
                    Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [BitTagName] = LogicalBit.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    },
                }));
        client.RegisterTag(
            new LogicalTag(
                ReadOnlyTagName,
                ReadOnlyPhysicalTagName,
                "int",
                new LogicalTagOptions { AccessMode = LogicalTagAccessMode.Read }));
        client.RegisterTag(
            new LogicalTag(
                WriteOnlyTagName,
                "WriteOnlyPhysical",
                "int",
                new LogicalTagOptions { AccessMode = LogicalTagAccessMode.Write }));
        simulator.SetTagValue(ReadOnlyPhysicalTagName, ReadOnlyValue);
        simulator.ScanEnabled = false;
    }

    /// <summary>Exercises concise facade and fault overloads.</summary>
    /// <param name="simulator">The simulator.</param>
    private static void ExerciseFacadeOverloads(ABPlcSimulator simulator)
    {
        simulator.AddUpdateTagItem<int>("Direct", default);
        simulator.AddUpdateTagItem<int>("Alias", "AliasPhysical", default);
        _ = simulator.Write(MissingTagName);
        simulator.QueueFault(ABPlcSimulatorOperation.Create, PlcTagStatus.ErrCreate);
        simulator.ClearFaults();
        simulator.QueueFault(ABPlcSimulatorOperation.Create, PlcTagStatus.ErrCreate, ExpectedPairCount);
        simulator.ClearFaults();
    }

    /// <summary>Asserts logical bit, access, missing, duplicate, and mixed bulk outcomes.</summary>
    /// <param name="simulator">The simulator.</param>
    /// <param name="client">The logical client.</param>
    /// <returns><see cref="Task"/> representing the assertion.</returns>
    private static async Task AssertLogicalValidationAsync(
        ABPlcSimulator simulator,
        ABLogicalTagClient client)
    {
        var now = TimeProvider.System.GetUtcNow();
        var bitWrite = await client.WriteAsync(new LogicalTagValue(BitTagName, true, now));
        var bitRead = await client.ReadAsync(BitTagName);
        var invalidBitWrite = await client.WriteAsync(new LogicalTagValue(BitTagName, 1, now));
        var deniedWrite = await client.WriteAsync(new LogicalTagValue(ReadOnlyTagName, DeniedWriteValue, now));
        var deniedRead = await client.ReadAsync(WriteOnlyTagName);
        var missingRead = await client.ReadAsync(MissingTagName);
        var duplicates = await client.WriteManyAsync(
        [
            new LogicalTagValue(BitTagName, true, now),
            new LogicalTagValue(BitTagName, false, now),
            new LogicalTagValue(MissingTagName, 1, now),
        ]);
        var mixedReads = await client.ReadManyAsync(
            [ReadOnlyTagName, WriteOnlyTagName, MissingTagName, ReadOnlyTagName]);

        await Assert.That(bitWrite.Succeeded).IsTrue();
        await Assert.That(bitRead.Succeeded).IsTrue();
        await Assert.That((bool)bitRead.Value!.Value!).IsTrue();
        await Assert.That(simulator.GetTagValue<short>(FlagsTagName, default)).IsEqualTo(ExpectedBitMask);
        await Assert.That(invalidBitWrite.Succeeded).IsFalse();
        await Assert.That(deniedWrite.Succeeded).IsFalse();
        await Assert.That(deniedRead.Succeeded).IsFalse();
        await Assert.That(missingRead.Succeeded).IsFalse();
        await Assert.That(duplicates.All(static result => !result.Succeeded)).IsTrue();
        await Assert.That(mixedReads.Count).IsEqualTo(DeniedWriteValue);
        await Assert.That(mixedReads.Count(static result => result.Succeeded)).IsEqualTo(ExpectedPairCount);
    }

    /// <summary>Asserts scripted faults, disconnects, reconnects, and ping behavior.</summary>
    /// <param name="simulator">The simulator.</param>
    /// <param name="client">The logical client.</param>
    /// <returns><see cref="Task"/> representing the assertion.</returns>
    private static async Task AssertFaultAndConnectionLifecycleAsync(
        ABPlcSimulator simulator,
        ABLogicalTagClient client)
    {
        simulator.QueueFault(
            ABPlcSimulatorOperation.Read,
            PlcTagStatus.ErrRead,
            ExpectedPairCount,
            ReadOnlyPhysicalTagName);
        var firstFault = await client.ReadAsync(ReadOnlyTagName);
        var secondFault = await client.ReadAsync(ReadOnlyTagName);
        simulator.ClearFaults();
        var recovered = await client.ReadAsync(ReadOnlyTagName);
        await Assert.That(firstFault.Succeeded).IsFalse();
        await Assert.That(secondFault.Succeeded).IsFalse();
        await Assert.That(recovered.Succeeded).IsTrue();

        var states = new List<bool>();
        using var subscription = simulator.ConnectionChanged.Subscribe(new CaptureObserver<bool>(states.Add));
        simulator.Disconnect();
        simulator.Disconnect();
        var disconnectedRead = await client.ReadAsync(ReadOnlyTagName);
        simulator.Reconnect();
        simulator.Reconnect();
        var reconnectedRead = await client.ReadAsync(ReadOnlyTagName);

        await Assert.That(disconnectedRead.Succeeded).IsFalse();
        await Assert.That(reconnectedRead.Succeeded).IsTrue();
        await Assert.That(states).IsEquivalentTo([true, false, true]);
        await Assert.That(simulator.Ping(echo: false)).IsTrue();
        await Assert.That(simulator.Ping(echo: true)).IsTrue();
        await Assert.That(await simulator.PingAsync(echo: false, CancellationToken.None)).IsTrue();
        await Assert.That(() => simulator.Disconnect(PlcTagStatus.StatusOK))
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Asserts facade value, writer, typed, bulk, and scan behavior.</summary>
    /// <param name="simulator">The simulator.</param>
    /// <returns><see cref="Task"/> representing the assertion.</returns>
    private static async Task AssertFacadeValuesAndBulkAsync(ABPlcSimulator simulator)
    {
        var readResult = simulator.Read(CounterTagName);
        await Assert.That(readResult).IsNotNull();
        simulator.Value(FlagsTagName, true, SelectedBit);
        await Assert.That(simulator.GetValue<bool>(FlagsTagName, default, SelectedBit)).IsTrue();
        var writer = simulator.CreateWriter<int>(CounterTagName, default, -1);
        writer.OnNext(WriterCounterValue);
        await Assert.That(simulator.GetTagValue<int>(CounterPhysicalTagName, default))
            .IsEqualTo(WriterCounterValue);

        var writes = await simulator.WriteManyAsync(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [CounterTagName] = BulkCounterValue,
                [FlagsTagName] = BulkFlagsValue,
                [MissingTagName] = 1,
            },
            CancellationToken.None);
        var reads = await simulator.ReadManyAsync(
            [FlagsTagName, MissingTagName, CounterTagName],
            CancellationToken.None);
        var typedRead = await simulator.ReadValueAsync<int>(
            CounterTagName,
            default,
            -1,
            CancellationToken.None);
        var typedWrite = await simulator.WriteValueAsync(
            CounterTagName,
            TypedCounterValue,
            -1,
            CancellationToken.None);
        var missingRead = await simulator.ReadValueAsync<int>(MissingTagName, default, -1, CancellationToken.None);
        var missingWrite = await simulator.WriteValueAsync(MissingTagName, 1, -1, CancellationToken.None);

        await Assert.That(writes.Count).IsEqualTo(ExpectedPairCount);
        await Assert.That(reads.Count).IsEqualTo(ExpectedPairCount);
        await Assert.That(typedRead.Succeeded).IsTrue();
        await Assert.That(typedWrite.Succeeded).IsTrue();
        await Assert.That(missingRead.Succeeded).IsFalse();
        await Assert.That(missingWrite.Succeeded).IsFalse();
        simulator.ScanEnabled = true;
        await Assert.That(simulator.ScanEnabled).IsTrue();
        simulator.ScanEnabled = false;
        simulator.AutoWriteValue = false;
        await Assert.That(simulator.AutoWriteValue).IsFalse();
        simulator.AutoWriteValue = true;
    }

    /// <summary>Asserts observable bridges, error streams, ping cancellation, removal, and disposal.</summary>
    /// <param name="simulator">The simulator.</param>
    /// <returns><see cref="Task"/> representing the assertion.</returns>
    private static async Task AssertFacadeObservationAndDisposalAsync(ABPlcSimulator simulator)
    {
        var observedValue = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var observedMany = new TaskCompletionSource<IReadOnlyDictionary<string, object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var observedAll = new TaskCompletionSource<IPlcTag?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var observedGroup = new TaskCompletionSource<IPlcTag>(TaskCreationOptions.RunContinuationsAsynchronously);
        var observedError = new TaskCompletionSource<PlcTagResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var valueSubscription = simulator.Observe<int>(CounterTagName, default, -1).Subscribe(
            new CaptureObserver<int>(value => observedValue.TrySetResult(value)));
        using var manySubscription = simulator.ObserveMany(CounterTagName, FlagsTagName).Subscribe(
            new CaptureObserver<IReadOnlyDictionary<string, object?>>(value => observedMany.TrySetResult(value)));
        using var allSubscription = simulator.ObserveAll.Subscribe(
            new CaptureObserver<IPlcTag?>(value => observedAll.TrySetResult(value)));
        using var groupSubscription = simulator.ObserveGroup(GroupName).Subscribe(
            new CaptureObserver<IPlcTag>(value => observedGroup.TrySetResult(value)));
        using var errorSubscription = simulator.ObserveErrors().Subscribe(
            new CaptureObserver<PlcTagResult>(value => observedError.TrySetResult(value)));

        _ = simulator.Read().ToArray();
        _ = simulator.Write().ToArray();
        simulator.QueueFault(ABPlcSimulatorOperation.Read, PlcTagStatus.ErrRead, 1, CounterPhysicalTagName);
        _ = simulator.Read(CounterTagName);
        await Assert.That(await AwaitObservationAsync(observedValue.Task)).IsEqualTo(TypedCounterValue);
        await Assert.That((await AwaitObservationAsync(observedError.Task)).StatusCode)
            .IsEqualTo(PlcTagStatus.ErrRead);
        _ = await AwaitObservationAsync(observedMany.Task);
        _ = await AwaitObservationAsync(observedAll.Task);
        _ = await AwaitObservationAsync(observedGroup.Task);

        var ping = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var pingSubscription = simulator.ObservePing(ShortInterval, echo: false, scheduler: null).Subscribe(
            new CaptureObserver<bool>(value => ping.TrySetResult(value)));
        await Assert.That(await AwaitObservationAsync(ping.Task)).IsTrue();
        CreateAsyncObservableSurfaces(simulator);
        using var cancellation = new CancellationTokenSource();
        await TestCompatibility.CancelAsync(cancellation);
        await Assert.That(async () => await simulator.PingAsync(false, cancellation.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(simulator.RemoveTagItem(CounterTagName)).IsTrue();
        await Assert.That(simulator.RemoveTagItem(CounterTagName)).IsFalse();
        simulator.Dispose();
        simulator.Dispose();
        await Assert.That(simulator.IsDisposed).IsTrue();
        await Assert.That(simulator.IsConnected).IsFalse();
        await Assert.That(() => simulator.GetTagBytes(FlagsPhysicalTagName)).Throws<ObjectDisposedException>();
        await Assert.That(() => simulator.Reconnect()).Throws<ObjectDisposedException>();
        await Assert.That(() => simulator.CreateLogicalTagClient()).Throws<ObjectDisposedException>();
    }

    /// <summary>Creates all asynchronous observable forwarding surfaces.</summary>
    /// <param name="simulator">The simulator.</param>
    private static void CreateAsyncObservableSurfaces(ABPlcSimulator simulator)
    {
        _ = simulator.ObserveAllAsyncObservable;
        _ = simulator.ObserveAsyncObservable<int>(CounterTagName, default, -1);
        _ = simulator.ObserveManyAsyncObservable(CounterTagName);
        _ = simulator.ObserveGroupAsyncObservable(GroupName);
        _ = simulator.ObserveSampled<int>(CounterTagName, ShortInterval, default, -1, null);
        _ = simulator.ObserveSampledAsyncObservable<int>(CounterTagName, ShortInterval, default, -1, null);
        _ = simulator.ObserveErrorsAsyncObservable();
        _ = simulator.ObservePingAsyncObservable(ShortInterval, echo: false, scheduler: null);
    }

    /// <summary>Asserts all native scalar accessors and device-to-handle staging.</summary>
    /// <param name="native">The simulator-native adapter.</param>
    /// <param name="api">The native interface.</param>
    /// <param name="handle">The native handle.</param>
    /// <returns><see cref="Task"/> representing the assertion.</returns>
    private static async Task AssertNativeScalarsAsync(
        SimulatedPlcTagNative native,
        IPlcTagNative api,
        int handle)
    {
        api.SetFloat32(handle, 0, NativeFloat32);
        api.SetFloat64(handle, Float64Offset, NativeFloat64);
        api.SetInt16(handle, Int16Offset, NativeInt16);
        api.SetInt32(handle, Int32Offset, NativeInt32);
        api.SetInt64(handle, Int64Offset, NativeInt64);
        api.SetInt8(handle, Int8Offset, NativeInt8);
        api.SetUInt16(handle, UInt16Offset, NativeUInt16);
        api.SetUInt32(handle, UInt32Offset, NativeUInt32);
        api.SetUInt64(handle, UInt64Offset, NativeUInt64);
        api.SetUInt8(handle, UInt8Offset, NativeUInt8);
        await Assert.That(api.GetFloat32(handle, 0)).IsEqualTo(NativeFloat32);
        await Assert.That(api.GetFloat64(handle, Float64Offset)).IsEqualTo(NativeFloat64);
        await Assert.That(api.GetInt16(handle, Int16Offset)).IsEqualTo(NativeInt16);
        await Assert.That(api.GetInt32(handle, Int32Offset)).IsEqualTo(NativeInt32);
        await Assert.That(api.GetInt64(handle, Int64Offset)).IsEqualTo(NativeInt64);
        await Assert.That(api.GetInt8(handle, Int8Offset)).IsEqualTo(NativeInt8);
        await Assert.That(api.GetUInt16(handle, UInt16Offset)).IsEqualTo(NativeUInt16);
        await Assert.That(api.GetUInt32(handle, UInt32Offset)).IsEqualTo(NativeUInt32);
        await Assert.That(api.GetUInt64(handle, UInt64Offset)).IsEqualTo(NativeUInt64);
        await Assert.That(api.GetUInt8(handle, UInt8Offset)).IsEqualTo(NativeUInt8);
        await Assert.That(api.Write(handle, NativeTimeout)).IsEqualTo(PlcTagStatus.StatusOK);
        native.SetTagBytes(NativeTagName, ABPlcSimulatorValueCodec.Encode(NativeReadValue));
        await Assert.That(api.Read(handle, NativeTimeout)).IsEqualTo(PlcTagStatus.StatusOK);
        await Assert.That(api.GetInt32(handle, 0)).IsEqualTo(NativeReadValue);
    }

    /// <summary>Asserts native scripted faults, connection changes, invalid handles, and create faults.</summary>
    /// <param name="native">The simulator-native adapter.</param>
    /// <param name="api">The native interface.</param>
    /// <param name="handle">The native handle.</param>
    /// <returns><see cref="Task"/> representing the assertion.</returns>
    private static async Task AssertNativeFaultsAsync(
        SimulatedPlcTagNative native,
        IPlcTagNative api,
        int handle)
    {
        native.QueueFault(ABPlcSimulatorOperation.Lock, PlcTagStatus.ErrMutexLock, ExpectedPairCount, "Other");
        await Assert.That(api.Lock(handle)).IsEqualTo(PlcTagStatus.StatusOK);
        native.QueueFault(
            ABPlcSimulatorOperation.Lock,
            PlcTagStatus.ErrMutexLock,
            ExpectedPairCount,
            NativeTagName);
        await Assert.That(api.Lock(handle)).IsEqualTo(PlcTagStatus.ErrMutexLock);
        await Assert.That(api.Lock(handle)).IsEqualTo(PlcTagStatus.ErrMutexLock);
        native.ClearFaults();
        await Assert.That(native.Disconnect(PlcTagStatus.ErrBadConnection)).IsTrue();
        await Assert.That(native.Disconnect(PlcTagStatus.ErrBadConnection)).IsFalse();
        await Assert.That(api.Read(handle, NativeTimeout)).IsEqualTo(PlcTagStatus.ErrBadConnection);
        await Assert.That(api.Destroy(handle)).IsEqualTo(PlcTagStatus.StatusOK);
        await Assert.That(native.Reconnect()).IsTrue();
        await Assert.That(native.Reconnect()).IsFalse();
        await Assert.That(api.Destroy(handle)).IsEqualTo(PlcTagStatus.ErrNotFound);
        await Assert.That(api.Read(-1, NativeTimeout)).IsEqualTo(PlcTagStatus.ErrNotFound);
        await Assert.That(api.Write(-1, NativeTimeout)).IsEqualTo(PlcTagStatus.ErrNotFound);
        await Assert.That(api.Abort(-1)).IsEqualTo(PlcTagStatus.ErrNotFound);

        var createFaultName = "CreateFault";
        native.QueueFault(ABPlcSimulatorOperation.Create, PlcTagStatus.ErrCreate, 1, createFaultName);
        var createFault = api.Create(
            $"protocol=ab_eip&name={createFaultName}&elem_size=4&elem_count=1",
            NativeTimeout);
        await Assert.That(createFault).IsEqualTo(PlcTagStatus.ErrCreate);
        await Assert.That(native.TagStatuses[createFaultName]).IsEqualTo(PlcTagStatus.ErrCreate);
        await Assert.That(native.OperationLog[0].ToString()).Contains("Create");
        native.ClearOperationLog();
        await Assert.That(native.OperationLog).IsEmpty();
    }

    /// <summary>Asserts native validation paths and idempotent disposal.</summary>
    /// <param name="native">The simulator-native adapter.</param>
    /// <param name="api">The native interface.</param>
    /// <returns><see cref="Task"/> representing the assertion.</returns>
    private static async Task AssertNativeValidationAndDisposalAsync(
        SimulatedPlcTagNative native,
        IPlcTagNative api)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => native.Disconnect(PlcTagStatus.StatusOK));
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => native.QueueFault(
                (ABPlcSimulatorOperation)InvalidOperationValue,
                PlcTagStatus.ErrRead,
                1,
                null));
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => native.QueueFault(ABPlcSimulatorOperation.Read, PlcTagStatus.ErrRead, 0, null));
        _ = Assert.Throws<ArgumentException>(
            () => native.QueueFault(ABPlcSimulatorOperation.Read, PlcTagStatus.ErrRead, 1, " "));
        _ = Assert.Throws<ArgumentNullException>(() => api.Create(null!, 0));
        _ = Assert.Throws<ArgumentException>(() => api.Create("elem_size=4&elem_count=1", 0));
        _ = Assert.Throws<ArgumentException>(() => api.Create("name=X&elem_size=0&elem_count=1", 0));
        _ = Assert.Throws<ArgumentException>(() => api.Create("name=X&elem_size=4&elem_count=X", 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => api.GetSize(-1));

        var smallHandle = api.Create("name=Small&elem_size=1&elem_count=1", 0);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => api.GetInt32(smallHandle, 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => api.GetUInt8(smallHandle, -1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => api.SetUInt16(smallHandle, 0, 1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => api.SetUInt8(-1, 0, 1));
        ((IDisposable)native).Dispose();
        ((IDisposable)native).Dispose();
        await Assert.That(native.IsConnected).IsFalse();
        _ = Assert.Throws<ObjectDisposedException>(() => native.ClearFaults());
        _ = Assert.Throws<ObjectDisposedException>(() => api.Create("name=X&elem_size=1&elem_count=1", 0));
    }

    /// <summary>Creates a fast ControlLogix simulator.</summary>
    /// <returns>The simulator.</returns>
    private static ABPlcSimulator CreateSimulator() =>
        new(PlcType.LGX, ShortInterval, OperationTimeout, "1,0", TimeProvider.System);

    /// <summary>Awaits an observable value with a deterministic timeout.</summary>
    /// <typeparam name="T">The observed type.</typeparam>
    /// <param name="task">The observation task.</param>
    /// <returns>The observed value.</returns>
    private static async Task<T> AwaitObservationAsync<T>(Task<T> task)
    {
        var completed = await Task.WhenAny(task, Task.Delay(ObservationTimeout));
        return completed == task
            ? await task
            : throw new TimeoutException("The simulator did not emit the expected value.");
    }

    /// <summary>Observer that forwards values to an action.</summary>
    /// <typeparam name="T">The observed type.</typeparam>
    /// <param name="onNext">The callback.</param>
    private sealed class CaptureObserver<T>(Action<T> onNext) : IObserver<T>
    {
        /// <inheritdoc/>
        public void OnCompleted()
        {
        }

        /// <inheritdoc/>
        public void OnError(Exception error)
        {
        }

        /// <inheritdoc/>
        public void OnNext(T value) => onNext(value);
    }
}
