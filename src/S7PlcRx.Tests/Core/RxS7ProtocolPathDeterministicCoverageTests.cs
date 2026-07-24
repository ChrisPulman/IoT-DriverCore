// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.S7PlcRx.Enums;
using IoT.DriverCore.S7PlcRx.Mock;
using S7DInt = IoT.DriverCore.S7PlcRx.PlcTypes.DInt;
using TUnitAssert = TUnit.Assertions.Assert;

namespace IoT.DriverCore.S7PlcRx.Tests.Core;

/// <summary>Provides deterministic coverage for RxS7 read, write, and multi-variable protocol paths.</summary>
[NotInParallel]
public sealed class RxS7ProtocolPathDeterministicCoverageTests
{
    /// <summary>Defines the simulator database size.</summary>
    private const int DatabaseSize = 512;

    /// <summary>Defines the polling interval in milliseconds.</summary>
    private const int PollingIntervalMilliseconds = 1;

    /// <summary>Defines the rack number.</summary>
    private const short RackNumber = 0;

    /// <summary>Defines the slot number.</summary>
    private const short SlotNumber = 1;

    /// <summary>Defines the fixed array length used by the protocol tests.</summary>
    private const int ArrayLength = 2;

    /// <summary>Defines the byte width of a counter value.</summary>
    private const int CounterByteLength = 2;

    /// <summary>Defines an invalid S7 item count.</summary>
    private const int OversizedItemCount = byte.MaxValue + 1;

    /// <summary>Defines the input byte value.</summary>
    private const byte InputByteValue = 0x11;

    /// <summary>Defines the output byte value.</summary>
    private const byte OutputByteValue = 0x22;

    /// <summary>Defines the marker byte value.</summary>
    private const byte MarkerByteValue = 0x33;

    /// <summary>Defines the counter value.</summary>
    private const ushort CounterValue = 0x1234;

    /// <summary>Defines the timer value.</summary>
    private const double TimerValue = 42D;

    /// <summary>Defines the S7 error code returned by scripted faults.</summary>
    private const byte ScriptedErrorCode = 0x05;

    /// <summary>Defines the input bit mask.</summary>
    private const byte InputBitMask = 0x02;

    /// <summary>Defines the output bit mask.</summary>
    private const byte OutputBitMask = 0x04;

    /// <summary>Defines the marker bit mask.</summary>
    private const byte MarkerBitMask = 0x08;

    /// <summary>Defines the database bit mask.</summary>
    private const byte DatabaseBitMask = 0x01;

    /// <summary>Defines the signed input scalar used to verify DInt decoding.</summary>
    private const int SignedInputScalarValue = -123_456;

    /// <summary>Defines the first signed input array value used to verify DInt decoding.</summary>
    private const int SignedInputArrayFirstValue = int.MinValue;

    /// <summary>Defines the second signed input array value used to verify DInt decoding.</summary>
    private const int SignedInputArraySecondValue = -654_321;

    /// <summary>Defines the input offset for the signed scalar.</summary>
    private const int SignedInputScalarOffset = 8;

    /// <summary>Defines the input offset for the signed array.</summary>
    private const int SignedInputArrayOffset = 20;

    /// <summary>Defines the first byte address in data block 1.</summary>
    private const string FirstDataBlockByteAddress = "DB1.DBB0";

    /// <summary>Defines the tag used to exercise a failed single-variable write.</summary>
    private const string FaultedSingleWriteTagName = "FaultedSingleWrite";

    /// <summary>Defines the tag used to exercise a missing array-length read.</summary>
    private const string MissingArrayLengthTagName = "MissingArrayLength";

    /// <summary>Defines the registered signed counter-array tag name.</summary>
    private const string SignedCounterArrayTagName = "CounterInts";

    /// <summary>Defines the minimum number of fields returned by CPU information projection.</summary>
    private const int MinimumCpuInformationFieldCount = 9;

    /// <summary>Defines the timeout used for deterministic operations.</summary>
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(60);

    /// <summary>Verifies every supported scalar and array value shape is serialized by multi-variable writes.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task MultiVarSerializesEverySupportedValueShapeAsync()
    {
        using var server = new MockServer { DefaultDb1Size = DatabaseSize };
        await TUnitAssert.That(server.Start()).IsEqualTo(0);
        using var plc = CreatePlc();
        await WaitUntilConnectedAsync(plc);
        Tag[] scalars =
        [
            CreateWriteTag("ScalarBoolean", FirstDataBlockByteAddress, true),
            CreateWriteTag("ScalarByte", "DB1.DBB1", byte.MaxValue),
            CreateWriteTag("ScalarInt16", "DB1.DBW2", short.MinValue),
            CreateWriteTag("ScalarUInt16", "DB1.DBW4", ushort.MaxValue),
            CreateWriteTag("ScalarInt32", "DB1.DBD6", int.MinValue),
            CreateWriteTag("ScalarUInt32", "DB1.DBD10", uint.MaxValue),
            CreateWriteTag("ScalarSingle", "DB1.DBD14", float.MaxValue),
            CreateWriteTag("ScalarDouble", "DB1.DBD18", double.MaxValue),
        ];
        var arrays = CreateArrayWriteTags();

        await TUnitAssert.That(plc.WriteMultiVar(scalars)).IsTrue();
        await TUnitAssert.That(plc.WriteMultiVar(arrays)).IsTrue();
        var scalarRead = plc.ReadMultiVar(scalars);
        var arrayRead = plc.ReadMultiVar(arrays);

        await TUnitAssert.That(scalarRead).IsNotNull();
        await TUnitAssert.That(scalarRead!.Count).IsEqualTo(scalars.Length);
        await TUnitAssert.That(arrayRead).IsNotNull();
        await TUnitAssert.That(arrayRead!.Count).IsEqualTo(arrays.Length);
    }

    /// <summary>Verifies invalid multi-variable inputs and protocol return codes fail without throwing.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task MultiVarRejectsInvalidInputsAndProtocolReturnCodesAsync()
    {
        using var server = new MockServer { DefaultDb1Size = DatabaseSize };
        await TUnitAssert.That(server.Start()).IsEqualTo(0);
        using var plc = CreatePlc();
        await WaitUntilConnectedAsync(plc);
        var blankName = CreateWriteTag("Blank", FirstDataBlockByteAddress, byte.MaxValue);
        blankName.Name = " ";
        var nullValue = new Tag("Null", FirstDataBlockByteAddress, typeof(byte));
        var badAddress = CreateWriteTag("BadAddress", "MB0", byte.MaxValue);
        var unsupported = CreateWriteTag("Unsupported", FirstDataBlockByteAddress, decimal.MaxValue);
        var oversized = Enumerable.Range(0, OversizedItemCount)
            .Select(index => CreateWriteTag($"Oversized{index}", FirstDataBlockByteAddress, byte.MaxValue))
            .ToArray();

        await TUnitAssert.That(plc.ReadMultiVar(null!)).IsNull();
        await TUnitAssert.That(plc.ReadMultiVar([])).IsNull();
        await TUnitAssert.That(plc.ReadMultiVar([blankName])).IsNull();
        await TUnitAssert.That(plc.ReadMultiVar([badAddress])).IsNull();
        await TUnitAssert.That(plc.ReadMultiVar(oversized)).IsNull();
        await TUnitAssert.That(plc.WriteMultiVar(null!)).IsFalse();
        await TUnitAssert.That(plc.WriteMultiVar([])).IsFalse();
        await TUnitAssert.That(plc.WriteMultiVar([blankName])).IsFalse();
        await TUnitAssert.That(plc.WriteMultiVar([nullValue])).IsFalse();
        await TUnitAssert.That(plc.WriteMultiVar([badAddress])).IsFalse();
        await TUnitAssert.That(plc.WriteMultiVar([unsupported])).IsFalse();
        await TUnitAssert.That(plc.WriteMultiVar(oversized)).IsFalse();
        await VerifyMultiVarFaultsAsync(server, plc);
    }

    /// <summary>Verifies single writes route to all standard memory areas and bit-address paths.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task SingleWritesRouteToNonDbAndBitMemoryAreasAsync()
    {
        using var server = new MockServer { DefaultDb1Size = DatabaseSize };
        await TUnitAssert.That(server.Start()).IsEqualTo(0);
        using var plc = CreatePlc();
        RegisterWriteRoutingTags(plc);
        await WaitUntilConnectedAsync(plc);

        plc.Value("InputByte", InputByteValue);
        plc.Value("OutputByte", OutputByteValue);
        plc.Value("MarkerByte", MarkerByteValue);
        plc.Value("Counter", CounterValue);
        plc.Value("TimerWrite", TimerValue);
        plc.Value("InputBit", true);
        plc.Value("OutputBit", true);
        plc.Value("MarkerBit", true);
        plc.Value("DatabaseBit", true);

        await WaitUntilMemoryWritesCompleteAsync(server);
        await TUnitAssert.That(server.Memory!.Read(S7MemoryArea.Input, 0, 0, 1)[0]).IsEqualTo(InputByteValue);
        await TUnitAssert.That(server.Memory.Read(S7MemoryArea.Output, 0, 0, 1)[0]).IsEqualTo(OutputByteValue);
        await TUnitAssert.That(server.Memory.Read(S7MemoryArea.Memory, 0, 0, 1)[0]).IsEqualTo(MarkerByteValue);
        await TUnitAssert.That(
            server.Memory.Read(S7MemoryArea.Counter, 0, 0, CounterByteLength)[0]).IsEqualTo((byte)0x12);
        await TUnitAssert.That(server.Memory.Read(S7MemoryArea.Timer, 0, 0, 1)[0]).IsNotEqualTo((byte)0);
    }

    /// <summary>Verifies the unvisited typed DB and non-DB read branches return strongly typed values.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task TypedReadsCoverScalarArrayAndAreaBranchesAsync()
    {
        using var server = new MockServer { DefaultDb1Size = DatabaseSize };
        await TUnitAssert.That(server.Start()).IsEqualTo(0);
        InitializeTypedReadMemory(server);
        using var plc = CreatePlc();
        RegisterTypedReadTags(plc);
        await WaitUntilConnectedAsync(plc);
        using var cancellation = new CancellationTokenSource(OperationTimeout);

        await VerifyDbTypedReadsAsync(plc, cancellation.Token);
        await VerifyAreaTypedReadsAsync(plc, cancellation.Token);
        await VerifySpecialTypedReadsAsync(plc, cancellation.Token);
    }

    /// <summary>Verifies signed counter arrays retain their declared signed element type.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task SignedCounterArrayReadPreservesDeclaredTypeAsync()
    {
        using var server = new MockServer { DefaultDb1Size = DatabaseSize };
        await TUnitAssert.That(server.Start()).IsEqualTo(0);
        using var plc = CreatePlc();
        RegisterTag<short[]>(plc, SignedCounterArrayTagName, "CT8", ArrayLength);
        await WaitUntilConnectedAsync(plc);
        using var cancellation = new CancellationTokenSource(OperationTimeout);

        await VerifyReadAsync<short[]>(plc, SignedCounterArrayTagName, cancellation.Token);
    }

    /// <summary>Verifies lifecycle guards and valid multi-variable requests fail cleanly while disconnected.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    [Test]
    public async Task MultiVarDisconnectedAndNullTagLifecyclePathsFailCleanlyAsync()
    {
        using var plc = CreatePlc();
        var tag = CreateWriteTag("Disconnected", FirstDataBlockByteAddress, byte.MaxValue);

        await TUnitAssert.That(() => plc.RemoveTagItemInternal(null!)).Throws<ArgumentNullException>();
        await TUnitAssert.That(plc.ReadMultiVar([tag])).IsNull();
        await TUnitAssert.That(plc.WriteMultiVar([tag])).IsFalse();
    }

    /// <summary>Verifies unsupported data-block address kinds report a write-format error.</summary>
    /// <returns>A task that represents the asynchronous assertion.</returns>
    [Test]
    public async Task SingleWriteRejectsUnsupportedDataBlockAddressKindAsync()
    {
        using var plc = CreatePlc();
        RegisterTag<int>(plc, "UnsupportedDbKind", "DB1.DBT0");
        var errorTask = plc.LastErrorCode
            .Where(error => error == ErrorCode.WrongVarFormat)
            .Take(1)
            .Timeout(OperationTimeout)
            .FirstAsync();

        plc.Value("UnsupportedDbKind", 1);

        await TUnitAssert.That(await errorTask).IsEqualTo(ErrorCode.WrongVarFormat);
    }

    /// <summary>Verifies CPU information retries both SZL records after transient protocol errors.</summary>
    /// <returns>A task that represents the asynchronous assertion.</returns>
    [Test]
    public async Task CpuInformationRetriesTransientSzlFailuresAsync()
    {
        using var server = new MockServer { DefaultDb1Size = DatabaseSize };
        await TUnitAssert.That(server.Start()).IsEqualTo(0);
        using var plc = CreatePlc();
        await WaitUntilConnectedAsync(plc);
        EnqueueTransientSzlFailures(server);

        var information = await plc.GetCpuInfo().Timeout(OperationTimeout).FirstAsync();

        await TUnitAssert.That(information.Length).IsGreaterThanOrEqualTo(MinimumCpuInformationFieldCount);
    }

    /// <summary>Verifies a failed single write publishes its protocol error details.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    [Test]
    public async Task SingleWriteProtocolFailurePublishesErrorDetailsAsync()
    {
        using var server = new MockServer { DefaultDb1Size = DatabaseSize };
        await TUnitAssert.That(server.Start()).IsEqualTo(0);
        using var plc = CreatePlc();
        RegisterTag<byte>(plc, FaultedSingleWriteTagName, FirstDataBlockByteAddress);
        await WaitUntilConnectedAsync(plc);
        var codeTask = plc.LastErrorCode
            .Where(error => error == ErrorCode.WriteData)
            .Take(1)
            .Timeout(OperationTimeout)
            .FirstAsync();
        var textTask = plc.LastError
            .Where(text => text.Contains(FaultedSingleWriteTagName, StringComparison.Ordinal))
            .Take(1)
            .Timeout(OperationTimeout)
            .FirstAsync();
        server.ManagedServer!.EnqueueFault(new(
            S7ServerFaultKind.ReturnCode,
            S7ServerOperation.Write,
            TimeSpan.Zero,
            ScriptedErrorCode));

        plc.Value(FaultedSingleWriteTagName, byte.MaxValue);

        await TUnitAssert.That(await codeTask).IsEqualTo(ErrorCode.WriteData);
        await TUnitAssert.That((await textTask).Contains(FaultedSingleWriteTagName, StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Verifies typed reads publish conversion errors when required array metadata is absent.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    [Test]
    public async Task TypedReadWithMissingArrayLengthPublishesErrorAsync()
    {
        using var server = new MockServer { DefaultDb1Size = DatabaseSize };
        await TUnitAssert.That(server.Start()).IsEqualTo(0);
        using var plc = CreatePlc();
        var registration = TagOperations.AddUpdateTagItem(
            plc,
            typeof(byte),
            MissingArrayLengthTagName,
            FirstDataBlockByteAddress).SetPolling(false);
        ((Tag)registration.Tag!).ArrayLength = null;
        await WaitUntilConnectedAsync(plc);
        var errorTask = plc.LastError
            .Where(text => !string.IsNullOrEmpty(text))
            .Take(1)
            .Timeout(OperationTimeout)
            .FirstAsync();
        using var cancellation = new CancellationTokenSource(OperationTimeout);

        var value = await plc.ReadAsync(
            new LogicalTagKey<byte>(MissingArrayLengthTagName),
            cancellation.Token);

        await TUnitAssert.That(value).IsEqualTo(byte.MinValue);
        await TUnitAssert.That(await errorTask).IsNotEmpty();
    }

    /// <summary>Creates array-valued tags for every supported multi-variable encoding.</summary>
    /// <returns>The array-valued write tags.</returns>
    private static Tag[] CreateArrayWriteTags() =>
    [
        CreateWriteTag<byte[]>("Bytes", "DB1.DBB32", [byte.MinValue, byte.MaxValue], ArrayLength),
        CreateWriteTag<short[]>("Int16s", "DB1.DBW34", [short.MinValue, short.MaxValue], ArrayLength),
        CreateWriteTag<ushort[]>("UInt16s", "DB1.DBW38", [ushort.MinValue, ushort.MaxValue], ArrayLength),
        CreateWriteTag<int[]>("Int32s", "DB1.DBD42", [int.MinValue, int.MaxValue], ArrayLength),
        CreateWriteTag<uint[]>("UInt32s", "DB1.DBD50", [uint.MinValue, uint.MaxValue], ArrayLength),
        CreateWriteTag<float[]>("Singles", "DB1.DBD58", [float.MinValue, float.MaxValue], ArrayLength),
        CreateWriteTag<double[]>("Doubles", "DB1.DBD66", [double.MinValue, double.MaxValue], ArrayLength),
        CreateWriteTag("StringValue", "DB1.DBB82", "S7", ArrayLength),
    ];

    /// <summary>Creates a write tag with a strongly typed value.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="name">The tag name.</param>
    /// <param name="address">The S7 address.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="arrayLength">The encoded item count.</param>
    /// <returns>The configured write tag.</returns>
    private static Tag CreateWriteTag<T>(string name, string address, T value, int arrayLength = 1) =>
        new(name, address, typeof(T), arrayLength) { NewValue = value };

    /// <summary>Creates a connected test PLC configuration.</summary>
    /// <returns>The PLC instance.</returns>
    private static RxS7 CreatePlc() =>
        new(new(
            new(CpuType.S71500, MockServer.Localhost, RackNumber, SlotNumber),
            new(PollingIntervalMilliseconds)));

    /// <summary>Waits until the PLC reports a completed connection.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <returns>A task that represents the wait.</returns>
    private static async Task WaitUntilConnectedAsync(RxS7 plc) =>
        _ = await plc.IsConnected.Where(connected => connected).Timeout(OperationTimeout).FirstAsync();

    /// <summary>Queues alternating failed and successful SZL operations for both CPU records.</summary>
    /// <param name="server">The managed simulator.</param>
    private static void EnqueueTransientSzlFailures(MockServer server)
    {
        server.ManagedServer!.EnqueueFault(new(
            S7ServerFaultKind.ReturnCode,
            S7ServerOperation.Szl,
            TimeSpan.Zero,
            ScriptedErrorCode));
        server.ManagedServer.EnqueueFault(new(
            S7ServerFaultKind.Delay,
            S7ServerOperation.Szl,
            TimeSpan.Zero,
            ScriptedErrorCode));
        server.ManagedServer.EnqueueFault(new(
            S7ServerFaultKind.ReturnCode,
            S7ServerOperation.Szl,
            TimeSpan.Zero,
            ScriptedErrorCode));
        server.ManagedServer.EnqueueFault(new(
            S7ServerFaultKind.Delay,
            S7ServerOperation.Szl,
            TimeSpan.Zero,
            ScriptedErrorCode));
    }

    /// <summary>Verifies scripted read and write item return codes propagate as failed multi-variable operations.</summary>
    /// <param name="server">The managed simulator.</param>
    /// <param name="plc">The connected PLC.</param>
    /// <returns>A task that represents the assertions.</returns>
    private static async Task VerifyMultiVarFaultsAsync(MockServer server, RxS7 plc)
    {
        var tag = CreateWriteTag("Faulted", "DB1.DBB100", byte.MaxValue);
        server.ManagedServer!.EnqueueFault(new(
            S7ServerFaultKind.ReturnCode,
            S7ServerOperation.Write,
            TimeSpan.Zero,
            ScriptedErrorCode));
        await TUnitAssert.That(plc.WriteMultiVar([tag])).IsFalse();

        server.ManagedServer.EnqueueFault(new(
            S7ServerFaultKind.ReturnCode,
            S7ServerOperation.Read,
            TimeSpan.Zero,
            ScriptedErrorCode));
        var result = plc.ReadMultiVar([tag]);
        await TUnitAssert.That(result).IsNotNull();
        await TUnitAssert.That(result!["Faulted"]).IsNull();
    }

    /// <summary>Registers tags used to verify the single-write routing table.</summary>
    /// <param name="plc">The PLC instance.</param>
    private static void RegisterWriteRoutingTags(RxS7 plc)
    {
        RegisterTag<byte>(plc, "InputByte", "EB0");
        RegisterTag<byte>(plc, "OutputByte", "AB0");
        RegisterTag<byte>(plc, "MarkerByte", "MB0");
        RegisterTag<ushort>(plc, "Counter", "C0");
        RegisterTag<double>(plc, "TimerWrite", "T0");
        RegisterTag<bool>(plc, "InputBit", "E1.1");
        RegisterTag<bool>(plc, "OutputBit", "A1.2");
        RegisterTag<bool>(plc, "MarkerBit", "M1.3");
        RegisterTag<bool>(plc, "DatabaseBit", "DB1.DBX0.0");
    }

    /// <summary>Waits until every queued memory-area write has reached the simulator.</summary>
    /// <param name="server">The managed simulator.</param>
    /// <returns>A task that represents the wait.</returns>
    private static async Task WaitUntilMemoryWritesCompleteAsync(MockServer server)
    {
        using var cancellation = new CancellationTokenSource(OperationTimeout);
        while (!cancellation.IsCancellationRequested)
        {
            if (AllMemoryWritesCompleted(server))
            {
                return;
            }

            await Task.Delay(PollingIntervalMilliseconds, cancellation.Token);
        }
    }

    /// <summary>Checks whether every queued memory-area write reached the simulator.</summary>
    /// <param name="server">The managed simulator.</param>
    /// <returns>true when every expected value is present.</returns>
    private static bool AllMemoryWritesCompleted(MockServer server)
    {
        var memory = server.Memory!;
        return memory.Read(S7MemoryArea.Input, 0, 0, 1)[0] == InputByteValue &&
            memory.Read(S7MemoryArea.Output, 0, 0, 1)[0] == OutputByteValue &&
            memory.Read(S7MemoryArea.Memory, 0, 0, 1)[0] == MarkerByteValue &&
            (memory.Read(S7MemoryArea.Input, 0, 1, 1)[0] & InputBitMask) != 0 &&
            (memory.Read(S7MemoryArea.Output, 0, 1, 1)[0] & OutputBitMask) != 0 &&
            (memory.Read(S7MemoryArea.Memory, 0, 1, 1)[0] & MarkerBitMask) != 0 &&
            (memory.Read(S7MemoryArea.DataBlock, 1, 0, 1)[0] & DatabaseBitMask) != 0;
    }

    /// <summary>Registers tags covering every typed read branch.</summary>
    /// <param name="plc">The PLC instance.</param>
    private static void RegisterTypedReadTags(RxS7 plc)
    {
        RegisterDbTypedReadTags(plc);
        RegisterAreaTypedReadTags(plc);
        RegisterTag<double>(plc, "MemoryDouble", "MD0");
        RegisterTag<double[]>(plc, "MemoryDoubles", "MD8", ArrayLength);
        RegisterTag<double>(plc, "TimerScalar", "T0");
        RegisterTag<double[]>(plc, "TimerArray", "TM2", ArrayLength);
        RegisterTag<ushort>(plc, "CounterUInt", "C0");
        RegisterTag<short>(plc, "CounterInt", "C2");
        RegisterTag<ushort[]>(plc, "CounterUInts", "CT4", ArrayLength);
        RegisterTag<short[]>(plc, SignedCounterArrayTagName, "CT8", ArrayLength);
    }

    /// <summary>Registers data-block typed read tags.</summary>
    /// <param name="plc">The PLC instance.</param>
    private static void RegisterDbTypedReadTags(RxS7 plc)
    {
        RegisterTag<byte>(plc, "DbByte", "DB1.DBB100");
        RegisterTag<byte[]>(plc, "DbBytes", "DB1.DBB102", ArrayLength);
        RegisterTag<bool>(plc, "DbBit", "DB1.DBX104.1");
        RegisterTag<short>(plc, "DbInt", "DB1.DBW0");
        RegisterTag<short[]>(plc, "DbInts", "DB1.DBW2", ArrayLength);
        RegisterTag<ushort[]>(plc, "DbWords", "DB1.DBW6", ArrayLength);
        RegisterTag<double>(plc, "DbDouble", "DB1.DBD10");
        RegisterTag<double[]>(plc, "DbDoubles", "DB1.DBD18", ArrayLength);
        RegisterTag<float>(plc, "DbReal", "DB1.DBD34");
        RegisterTag<float[]>(plc, "DbReals", "DB1.DBD38", ArrayLength);
        RegisterTag<int>(plc, "DbDInt", "DB1.DBD46");
        RegisterTag<int[]>(plc, "DbDInts", "DB1.DBD50", ArrayLength);
        RegisterTag<uint>(plc, "DbDWord", "DB1.DBD58");
        RegisterTag<uint[]>(plc, "DbDWords", "DB1.DBD62", ArrayLength);
    }

    /// <summary>Registers non-data-block typed read tags.</summary>
    /// <param name="plc">The PLC instance.</param>
    private static void RegisterAreaTypedReadTags(RxS7 plc)
    {
        RegisterTag<byte>(plc, "InputByteRead", "EB0");
        RegisterTag<byte[]>(plc, "InputBytes", "EB32", ArrayLength);
        RegisterTag<bool>(plc, "InputBitRead", "E100.1");
        RegisterTag<ushort>(plc, "InputWordRead", "EW2");
        RegisterTag<uint>(plc, "InputDWord", "ED4");
        RegisterTag<int>(plc, "InputDInt", "ED8");
        RegisterTag<uint[]>(plc, "InputDWords", "ED12", ArrayLength);
        RegisterTag<int[]>(plc, "InputDInts", "ED20", ArrayLength);
        RegisterTag<byte>(plc, "OutputByteRead", "AB0");
        RegisterTag<byte[]>(plc, "OutputBytes", "AB32", ArrayLength);
        RegisterTag<bool>(plc, "OutputBitRead", "A100.2");
        RegisterTag<ushort>(plc, "OutputWordRead", "AW2");
        RegisterTag<uint>(plc, "OutputDWord", "AD4");
        RegisterTag<byte>(plc, "MemoryByteRead", "MB32");
        RegisterTag<byte[]>(plc, "MemoryBytes", "MB34", ArrayLength);
        RegisterTag<bool>(plc, "MemoryBitRead", "M100.3");
    }

    /// <summary>Registers one non-polling tag.</summary>
    /// <typeparam name="T">The tag type.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="name">The tag name.</param>
    /// <param name="address">The S7 address.</param>
    /// <param name="arrayLength">The fixed array length.</param>
    private static void RegisterTag<T>(RxS7 plc, string name, string address, int arrayLength = 1) =>
        _ = TagOperations.AddUpdateTagItem(plc, typeof(T), name, address, arrayLength).SetPolling(false);

    /// <summary>Verifies strongly typed data-block reads.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the assertions.</returns>
    private static async Task VerifyDbTypedReadsAsync(RxS7 plc, CancellationToken cancellationToken)
    {
        await VerifyReadAsync<byte>(plc, "DbByte", cancellationToken);
        await VerifyReadAsync<byte[]>(plc, "DbBytes", cancellationToken);
        await VerifyReadAsync<bool>(plc, "DbBit", cancellationToken);
        await VerifyReadAsync<short>(plc, "DbInt", cancellationToken);
        await VerifyReadAsync<short[]>(plc, "DbInts", cancellationToken);
        await VerifyReadAsync<ushort[]>(plc, "DbWords", cancellationToken);
        await VerifyReadAsync<double>(plc, "DbDouble", cancellationToken);
        await VerifyReadAsync<double[]>(plc, "DbDoubles", cancellationToken);
        await VerifyReadAsync<float>(plc, "DbReal", cancellationToken);
        await VerifyReadAsync<float[]>(plc, "DbReals", cancellationToken);
        await VerifyReadAsync<int>(plc, "DbDInt", cancellationToken);
        await VerifyReadAsync<int[]>(plc, "DbDInts", cancellationToken);
        await VerifyReadAsync<uint>(plc, "DbDWord", cancellationToken);
        await VerifyReadAsync<uint[]>(plc, "DbDWords", cancellationToken);
    }

    /// <summary>Verifies strongly typed non-data-block reads.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the assertions.</returns>
    private static async Task VerifyAreaTypedReadsAsync(RxS7 plc, CancellationToken cancellationToken)
    {
        await VerifyReadAsync<byte>(plc, "InputByteRead", cancellationToken);
        await VerifyReadAsync<byte[]>(plc, "InputBytes", cancellationToken);
        await VerifyReadAsync<bool>(plc, "InputBitRead", cancellationToken);
        await VerifyReadAsync<ushort>(plc, "InputWordRead", cancellationToken);
        await VerifyReadAsync<uint>(plc, "InputDWord", cancellationToken);
        await VerifyReadValueAsync(plc, "InputDInt", SignedInputScalarValue, cancellationToken);
        await VerifyReadAsync<uint[]>(plc, "InputDWords", cancellationToken);
        await VerifyReadValueAsync(
            plc,
            "InputDInts",
            (int[])[SignedInputArrayFirstValue, SignedInputArraySecondValue],
            cancellationToken);
        await VerifyReadAsync<byte>(plc, "OutputByteRead", cancellationToken);
        await VerifyReadAsync<byte[]>(plc, "OutputBytes", cancellationToken);
        await VerifyReadAsync<bool>(plc, "OutputBitRead", cancellationToken);
        await VerifyReadAsync<ushort>(plc, "OutputWordRead", cancellationToken);
        await VerifyReadAsync<uint>(plc, "OutputDWord", cancellationToken);
        await VerifyReadAsync<byte>(plc, "MemoryByteRead", cancellationToken);
        await VerifyReadAsync<byte[]>(plc, "MemoryBytes", cancellationToken);
        await VerifyReadAsync<bool>(plc, "MemoryBitRead", cancellationToken);
        await VerifyReadAsync<double>(plc, "MemoryDouble", cancellationToken);
        await VerifyReadAsync<double[]>(plc, "MemoryDoubles", cancellationToken);
    }

    /// <summary>Verifies timer and counter typed reads.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the assertions.</returns>
    private static async Task VerifySpecialTypedReadsAsync(RxS7 plc, CancellationToken cancellationToken)
    {
        await VerifyReadAsync<double>(plc, "TimerScalar", cancellationToken);
        await VerifyReadAsync<double[]>(plc, "TimerArray", cancellationToken);
        await VerifyReadAsync<ushort>(plc, "CounterUInt", cancellationToken);
        await VerifyReadAsync<short>(plc, "CounterInt", cancellationToken);
        await VerifyReadAsync<ushort[]>(plc, "CounterUInts", cancellationToken);
    }

    /// <summary>Initializes signed non-data-block values that distinguish DInt decoding from DWord decoding.</summary>
    /// <param name="server">The managed simulator.</param>
    private static void InitializeTypedReadMemory(MockServer server)
    {
        server.Memory!.Write(
            S7MemoryArea.Input,
            0,
            SignedInputScalarOffset,
            S7DInt.ToByteArray(SignedInputScalarValue));
        server.Memory.Write(
            S7MemoryArea.Input,
            0,
            SignedInputArrayOffset,
            S7DInt.ToByteArray([SignedInputArrayFirstValue, SignedInputArraySecondValue]));
    }

    /// <summary>Reads and verifies one registered tag has a value of its declared type.</summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="name">The registered tag name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the assertion.</returns>
    private static async Task VerifyReadAsync<T>(RxS7 plc, string name, CancellationToken cancellationToken)
    {
        var value = await plc.ReadAsync(new LogicalTagKey<T>(name), cancellationToken);
        await TUnitAssert.That(value is not null).IsTrue();
    }

    /// <summary>Reads and verifies one registered tag equals an expected value.</summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="name">The registered tag name.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the assertion.</returns>
    private static async Task VerifyReadValueAsync<T>(
        RxS7 plc,
        string name,
        T expected,
        CancellationToken cancellationToken)
    {
        var value = await plc.ReadAsync(new LogicalTagKey<T>(name), cancellationToken);
        await TUnitAssert.That(value).IsEquivalentTo(expected);
    }
}
