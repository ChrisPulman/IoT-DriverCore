// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET9_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Reflection;
using IoT.DriverCore.Core;
using LeanBridge = IoT.DriverCore.TwinCATRx.ObservableBridgeExtensions;
using LeanCoreExtensions = IoT.DriverCore.TwinCATRx.Core.TwinCatRxExtensions;
using LeanSettings = IoT.DriverCore.TwinCATRx.Core.Settings;
using ReactiveClient = IoT.DriverCore.TwinCATRx.Reactive.InMemoryAdsClient;
using ReactiveCoreExtensions = IoT.DriverCore.TwinCATRx.Core.Reactive.TwinCatRxExtensions;
using ReactiveOperation = IoT.DriverCore.TwinCATRx.Reactive.InMemoryAdsOperation;
using ReactiveSettings = IoT.DriverCore.TwinCATRx.Core.Reactive.Settings;

namespace IoT.DriverCore.TwinCATRx.Tests.Rx;

/// <summary>Exercises the deterministic production ADS simulator and its logical-tag integration.</summary>
public sealed class InMemoryAdsClientTests
{
    /// <summary>The scalar ADS variable.</summary>
    private const string SpeedVariable = ".Machine.Speed";

    /// <summary>A write-only logical tag.</summary>
    private const string WriteOnlyTag = "WriteOnly";

    /// <summary>A read-only logical tag.</summary>
    private const string ReadOnlyTag = "ReadOnly";

    /// <summary>The array ADS variable.</summary>
    private const string SamplesVariable = ".Machine.Samples";

    /// <summary>The structure root ADS variable.</summary>
    private const string StateVariable = ".Machine.State";

    /// <summary>The logical speed tag name.</summary>
    private const string SpeedTag = "Speed";

    /// <summary>The logical count tag name.</summary>
    private const string CountTag = "Count";

    /// <summary>The logical enabled tag name.</summary>
    private const string EnabledTag = "Enabled";

    /// <summary>The enum conversion ADS variable.</summary>
    private const string EnumVariable = ".Enum";

    /// <summary>The generic array ADS variable.</summary>
    private const string GenericArrayVariable = ".Array";

    /// <summary>The generic integer ADS variable.</summary>
    private const string IntegerVariable = ".Int";

    /// <summary>An unknown ADS variable.</summary>
    private const string MissingVariable = ".Missing";

    /// <summary>A symbol added after the simulator connects.</summary>
    private const string LateVariable = ".Late";

    /// <summary>A write-only symbol added after the simulator connects.</summary>
    private const string LateWriteVariable = ".LateWrite";

    /// <summary>The nullable ADS variable.</summary>
    private const string NullableVariable = ".Nullable";

    /// <summary>The deterministic in-memory route.</summary>
    private const string SimulatorAddress = "simulation";

    /// <summary>The first test value.</summary>
    private const int InitialValue = 10;

    /// <summary>The second test value.</summary>
    private const int UpdatedValue = 25;

    /// <summary>The expected configured handle count.</summary>
    private const int ExpectedReadHandleCount = 2;

    /// <summary>The expected bulk operation count.</summary>
    private const int ExpectedBulkCount = 2;

    /// <summary>The full array length.</summary>
    private const int FullArrayLength = 3;

    /// <summary>The conversion test value.</summary>
    private const int ConversionValue = 42;

    /// <summary>The final array value.</summary>
    private const int FinalArrayValue = 50;

    /// <summary>The default notification cycle time.</summary>
    private const int NotificationCycleTime = 100;

    /// <summary>The pause duration in milliseconds.</summary>
    private const int PauseDurationMilliseconds = 10;

    /// <summary>The pause completion wait in milliseconds.</summary>
    private const int PauseWaitMilliseconds = 100;

    /// <summary>The first TwinCAT 3 ADS port.</summary>
    private const int TwinCat3Port = 851;

    /// <summary>Describes the enum conversion test values.</summary>
    private enum TestMode
    {
        /// <summary>The idle state.</summary>
        Idle,

        /// <summary>The running state.</summary>
        Running,
    }

    /// <summary>Verifies setup, handles, code, notifications, scalar/array reads, writes, and direct bulk operations.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("The IRxTcAdsClient contract carries dynamic-code compatibility annotations.")]
    [RequiresUnreferencedCode("The IRxTcAdsClient contract carries reflection compatibility annotations.")]
#endif
    public async Task Setup_Read_Write_Notifications_And_Bulk_Are_DeterministicAsync()
    {
        using var client = new InMemoryAdsClient();
        var settings = CreateSettings();
        var states = new List<InMemoryAdsConnectionState>();
        var initialized = 0;
        var code = new List<string[]>();
        var data = new List<(string Variable, object? Data, string? Id)>();
        var writes = new List<string?>();
        using var stateSubscription = LeanBridge.SubscribeTo(client.ConnectionStates, states.Add);
        using var initSubscription = LeanBridge.SubscribeTo(client.InitializeComplete, _ => initialized++);
        using var codeSubscription = LeanBridge.SubscribeTo(client.Code, code.Add);
        using var dataSubscription = LeanBridge.SubscribeTo(client.DataReceived, data.Add);
        using var writeSubscription = LeanBridge.SubscribeTo(client.OnWrite, writes.Add);

        RegisterStandardSymbols(client);
        client.Connect(settings);

        await TUnitAssert.That(client.Connected).IsTrue();
        await TUnitAssert.That(client.ConnectionState).IsEqualTo(InMemoryAdsConnectionState.Connected);
        await TUnitAssert.That(client.Settings).IsSameReferenceAs(settings);
        await TUnitAssert.That(initialized).IsEqualTo(1);
        await TUnitAssert.That(client.ReadWriteHandleInfo.Count).IsEqualTo(ExpectedReadHandleCount);
        await TUnitAssert.That(client.WriteHandleInfo.Count).IsEqualTo(ExpectedReadHandleCount);
        await TUnitAssert.That(code.Single().Length).IsEqualTo(ExpectedReadHandleCount);
        await TUnitAssert.That(data.Count).IsEqualTo(ExpectedReadHandleCount);

        data.Clear();
        client.Read(SpeedVariable);
        client.Read(SamplesVariable, ExpectedBulkCount, "array");
        client.ReadMany([SpeedVariable, SamplesVariable], "bulk-read");
        client.Write(SpeedVariable, UpdatedValue);
        client.Write(SamplesVariable, new[] { UpdatedValue, ConversionValue, FinalArrayValue }, "array-write");
        client.WriteMany(
            [
                new KeyValuePair<string, object>(SpeedVariable, InitialValue),
                new KeyValuePair<string, object>(
                    SamplesVariable,
                    new[] { ConversionValue, FinalArrayValue, UpdatedValue }),
            ],
            "bulk-write");

        await TUnitAssert.That(data.Count).IsGreaterThanOrEqualTo(ExpectedBulkCount);
        await TUnitAssert.That((object?[])data[1].Data!)
            .IsEquivalentTo([(object?)InitialValue, UpdatedValue]);
        await TUnitAssert.That(data[2].Id).IsEqualTo("bulk-read:0");
        await TUnitAssert.That(data[3].Id).IsEqualTo("bulk-read:1");
        await TUnitAssert.That(writes).Contains("Success");
        await TUnitAssert.That(writes).Contains("Success,array-write");
        await TUnitAssert.That(writes).Contains("Success,bulk-write:1");
        await TUnitAssert.That(client.TryGetValue<int>(SpeedVariable, out var speed)).IsTrue();
        await TUnitAssert.That(speed).IsEqualTo(InitialValue);
        await TUnitAssert.That(client.Symbols.Count).IsEqualTo(ExpectedReadHandleCount);
        await TUnitAssert.That(states).Contains(InMemoryAdsConnectionState.Connecting);
        await TUnitAssert.That(states).Contains(InMemoryAdsConnectionState.Connected);
    }

    /// <summary>Verifies logical setup/read/write/observe/bulk paths over direct and structure symbols.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("HashTableRx materializes structure payloads for logical structure writes.")]
    [RequiresUnreferencedCode("HashTableRx materializes structure payloads for logical structure writes.")]
#endif
    public async Task Logical_Client_Uses_Simulator_For_Direct_Structure_Observe_And_BulkAsync()
    {
        using var native = new InMemoryAdsClient();
        var settings = new LeanSettings
        {
            AdsAddress = SimulatorAddress,
            Port = TwinCat3Port,
            SettingsId = "logical",
        };
        LeanCoreExtensions.AddNotification(settings, SpeedVariable);
        LeanCoreExtensions.AddNotification(settings, StateVariable);
        LeanCoreExtensions.AddWriteVariable(settings, SpeedVariable);
        LeanCoreExtensions.AddWriteVariable(settings, StateVariable);
        _ = native.RegisterSymbol(SpeedVariable, InitialValue)
            .RegisterStructure(StateVariable, new TestStructure { Count = InitialValue, Enabled = true });
        native.Connect(settings);
        using var client = new TwinCatLogicalTagClient(native);
        client.RegisterTag(new LogicalTag(SpeedTag, SpeedVariable, "DINT"));
        client.RegisterTag(CreateStructureTag(CountTag, CountTag));
        client.RegisterTag(CreateStructureTag(EnabledTag, EnabledTag));

        var direct = await client.ReadAsync(SpeedTag);
        var directWrite = await client.WriteAsync(CreateValue(SpeedTag, UpdatedValue));
        var observed = new List<LogicalTagValue>();
        using var observation = LeanBridge.SubscribeTo(client.Observe(SpeedTag), observed.Add);
        native.SetValue(SpeedVariable, InitialValue);
        var reads = await client.ReadManyAsync([CountTag, EnabledTag]);
        var writes = await client.WriteManyAsync(
            [CreateValue(CountTag, UpdatedValue), CreateValue(EnabledTag, false)]);

        await TUnitAssert.That(direct.Succeeded).IsTrue();
        await TUnitAssert.That(direct.Value!.Value).IsEqualTo(InitialValue);
        await TUnitAssert.That(directWrite.Succeeded).IsTrue();
        await TUnitAssert.That(observed.Last().Value).IsEqualTo(InitialValue);
        await TUnitAssert.That(reads.Count).IsEqualTo(ExpectedBulkCount);
        await TUnitAssert.That(reads[0].Value!.Value).IsEqualTo(InitialValue);
        await TUnitAssert.That(writes.All(static result => result.Succeeded)).IsTrue();
        await TUnitAssert.That(native.TryGetValue<TestStructure>(StateVariable, out var state)).IsTrue();
        await TUnitAssert.That(state!.Count).IsEqualTo(UpdatedValue);
        await TUnitAssert.That(state.Enabled).IsFalse();
    }

    /// <summary>Verifies connection validation, access errors, scripted faults, reconnect, pause, and disposal.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("The IRxTcAdsClient contract carries dynamic-code compatibility annotations.")]
    [RequiresUnreferencedCode("The IRxTcAdsClient contract carries reflection compatibility annotations.")]
#endif
    public async Task State_Faults_Reconnect_Pause_And_Dispose_Are_ObservableAsync()
    {
        var client = new InMemoryAdsClient();
        var settings = CreateSettings();
        var errors = new List<Exception>();
        var data = new List<(string Variable, object? Data, string? Id)>();
        var writes = new List<string?>();
        var states = new List<InMemoryAdsConnectionState>();
        using var errorSubscription = LeanBridge.SubscribeTo(client.ErrorReceived, errors.Add);
        using var dataSubscription = LeanBridge.SubscribeTo(client.DataReceived, data.Add);
        using var writeSubscription = LeanBridge.SubscribeTo(client.OnWrite, writes.Add);
        using var stateSubscription = LeanBridge.SubscribeTo(client.ConnectionStates, states.Add);

        client.Connect(settings);
        await TUnitAssert.That(client.ConnectionState).IsEqualTo(InMemoryAdsConnectionState.Faulted);
        await TUnitAssert.That(errors.Last()).IsTypeOf<InMemoryAdsException>();

        _ = client.RegisterSymbol(
                SpeedVariable,
                InitialValue,
                typeof(int),
                -1,
                isReadable: true,
                isWritable: false)
            .RegisterSymbol(
                SamplesVariable,
                new[] { InitialValue, UpdatedValue },
                typeof(int[]),
                ExpectedBulkCount,
                isReadable: false,
                isWritable: true);
        client.Reconnect();
        await ExerciseFaultsAndPauseAsync(client);

        await TUnitAssert.That(client.Connected).IsTrue();
        await TUnitAssert.That(data.Any(static item => item.Id == "faulted-read" && item.Data is null)).IsTrue();
        await TUnitAssert.That(data.Any(static item => item.Id == "write-only" && item.Data is null)).IsTrue();
        await TUnitAssert.That(writes.Any(static item => item?.EndsWith(",faulted-write", StringComparison.Ordinal) == true))
            .IsTrue();
        await TUnitAssert.That(writes.Any(static item => item?.EndsWith(",read-only", StringComparison.Ordinal) == true))
            .IsTrue();
        await TUnitAssert.That(errors.Count).IsGreaterThanOrEqualTo(ExpectedBulkCount);
        await TUnitAssert.That(client.IsPaused).IsFalse();

        client.Disconnect();
        _ = client.QueueFault(InMemoryAdsOperation.Connect, new IOException("connect fault"));
        client.Reconnect();
        await TUnitAssert.That(client.ConnectionState).IsEqualTo(InMemoryAdsConnectionState.Faulted);
        await TUnitAssert.That(client.RemoveSymbol(SamplesVariable)).IsTrue();
        DisposeTwiceAndExerciseNoOps(client);

        await TUnitAssert.That(client.IsDisposed).IsTrue();
        await TUnitAssert.That(client.ConnectionState).IsEqualTo(InMemoryAdsConnectionState.Disposed);
        await TUnitAssert.That(client.ReadWriteHandleInfo).IsEmpty();
        await TUnitAssert.That(client.WriteHandleInfo).IsEmpty();
        await TUnitAssert.That(states).Contains(InMemoryAdsConnectionState.Disposed);
        await TUnitAssert.That(() => client.Read(SpeedVariable)).Throws<ObjectDisposedException>();
        await TUnitAssert.That(() => client.RegisterSymbol(SpeedVariable, InitialValue))
            .Throws<ObjectDisposedException>();
    }

    /// <summary>Verifies null values, conversion, enums, slicing validation, and symbol removal behavior.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("The IRxTcAdsClient contract carries dynamic-code compatibility annotations.")]
    [RequiresUnreferencedCode("The IRxTcAdsClient contract carries reflection compatibility annotations.")]
#endif
    public async Task Symbol_Value_Edge_Cases_Are_DeterministicAsync()
    {
        using var client = new InMemoryAdsClient();
        var settings = new LeanSettings { AdsAddress = SimulatorAddress, Port = TwinCat3Port };
        var data = new List<(string Variable, object? Data, string? Id)>();
        var writes = new List<string?>();
        using var dataSubscription = LeanBridge.SubscribeTo(client.DataReceived, data.Add);
        using var writeSubscription = LeanBridge.SubscribeTo(client.OnWrite, writes.Add);
        _ = client.RegisterSymbol(NullableVariable, null, typeof(string))
            .RegisterSymbol(EnumVariable, TestMode.Idle, typeof(TestMode))
            .RegisterSymbol(IntegerVariable, InitialValue, typeof(int))
            .RegisterSymbol(GenericArrayVariable, new[] { InitialValue }, typeof(int[]));
        client.Connect(settings);

        client.SetValue(NullableVariable, null);
        client.Write(EnumVariable, "Running");
        client.Write(IntegerVariable, ConversionValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
        client.Write(IntegerVariable, "not-an-int", "bad-conversion");
        client.Write(GenericArrayVariable, new object(), "bad-array");
        client.Read(GenericArrayVariable, 0, "bad-length");
        client.Read(NullableVariable, "nullable");

        await TUnitAssert.That(client.TryGetValue<TestMode>(EnumVariable, out var mode)).IsTrue();
        await TUnitAssert.That(mode).IsEqualTo(TestMode.Running);
        await TUnitAssert.That(client.TryGetValue<int>(IntegerVariable, out var number)).IsTrue();
        await TUnitAssert.That(number).IsEqualTo(ConversionValue);
        await TUnitAssert.That(client.TryGetValue<string>(MissingVariable, out _)).IsFalse();
        await TUnitAssert.That(writes.Any(static value => value?.EndsWith(",bad-conversion", StringComparison.Ordinal) == true))
            .IsTrue();
        await TUnitAssert.That(data.Any(static value => value.Id == "bad-length" && value.Data is null)).IsTrue();
        await TUnitAssert.That(data.Any(static value => value.Id == "nullable" && value.Data is null)).IsTrue();
        await TUnitAssert.That(client.RemoveSymbol(NullableVariable)).IsTrue();
        await TUnitAssert.That(client.RemoveSymbol(NullableVariable)).IsFalse();
        await TUnitAssert.That(() => client.SetValue(MissingVariable, InitialValue)).Throws<KeyNotFoundException>();
    }

    /// <summary>Verifies guard clauses, disconnected outcomes, dynamic registration, and stream adapters.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("The IRxTcAdsClient contract carries dynamic-code compatibility annotations.")]
    [RequiresUnreferencedCode("The IRxTcAdsClient contract carries reflection compatibility annotations.")]
#endif
    public async Task Guards_Disconnected_Operations_And_Dynamic_Registration_Are_CoveredAsync()
    {
        using var client = new InMemoryAdsClient();
        _ = client.DataReceivedAsync;
        _ = client.ErrorReceivedAsync;
        _ = client.InitializeCompleteAsync;
        _ = client.IsPausedObservable;
        _ = client.IsPausedObservableAsync;
        _ = client.OnWriteAsync;
        client.PublishNotifications();
        client.Read(MissingVariable);
        client.Read(MissingVariable, ExpectedBulkCount);
        client.Write(MissingVariable, InitialValue);
        client.ReadMany([MissingVariable]);
        client.WriteMany([new KeyValuePair<string, object>(MissingVariable, InitialValue)]);
        client.Pause(TimeSpan.Zero);

        await TUnitAssert.That(() => client.Connect(null!)).Throws<ArgumentNullException>();
        await TUnitAssert.That(() => client.QueueFault(InMemoryAdsOperation.Read, null!))
            .Throws<ArgumentNullException>();
        await TUnitAssert.That(() => client.ReadMany(null!)).Throws<ArgumentNullException>();
        await TUnitAssert.That(() => client.WriteMany(null!)).Throws<ArgumentNullException>();
        await TUnitAssert.That(() => client.RegisterSymbol(string.Empty, InitialValue))
            .Throws<ArgumentException>();
        await TUnitAssert.That(() => client.RegisterStructure<TestStructure>(StateVariable, null!))
            .Throws<ArgumentNullException>();

        var settings = CreateSettings();
        RegisterStandardSymbols(client);
        _ = client.RegisterSymbol(NullableVariable, "abcdef", typeof(string));
        client.Connect(settings);
        client.Read(NullableVariable, ExpectedBulkCount);
        await TUnitAssert.That(() => client.SetValue(SpeedVariable, null)).Throws<InvalidCastException>();
        await TUnitAssert.That(client.RemoveSymbol(SpeedVariable)).IsTrue();
        _ = client.RegisterSymbol(SpeedVariable, InitialValue);
        _ = client.RegisterSymbol(IntegerVariable, InitialValue);
        await TUnitAssert.That(client.ReadWriteHandleInfo.ContainsKey(SpeedVariable)).IsTrue();
        await TUnitAssert.That(client.WriteHandleInfo.ContainsKey(SpeedVariable)).IsTrue();
        client.Read(IntegerVariable, ExpectedBulkCount, "scalar-length");
        client.Write(MissingVariable, InitialValue, "missing-write");

        LeanCoreExtensions.AddNotification(settings, string.Empty);
        LeanCoreExtensions.AddNotification(settings, MissingVariable);
        client.PublishNotifications();
        client.Disconnect();
        client.Read(SpeedVariable, "disconnected-read");
        client.Write(SpeedVariable, InitialValue, "disconnected-write");

        using var missingWriteClient = new InMemoryAdsClient();
        var missingWriteSettings = new LeanSettings { AdsAddress = SimulatorAddress, Port = TwinCat3Port };
        LeanCoreExtensions.AddWriteVariable(missingWriteSettings, MissingVariable);
        missingWriteClient.Connect(missingWriteSettings);
        await TUnitAssert.That(missingWriteClient.ConnectionState).IsEqualTo(InMemoryAdsConnectionState.Faulted);
    }

    /// <summary>Verifies logical constructors, catalog guards, access failures, multi-observe, and disposal.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("HashTableRx materializes logical structure payloads.")]
    [RequiresUnreferencedCode("HashTableRx materializes logical structure payloads.")]
#endif
    public async Task Logical_Client_Guards_Constructors_And_Access_Failures_Are_DeterministicAsync()
    {
        using var native = new InMemoryAdsClient();
        var settings = new LeanSettings { AdsAddress = SimulatorAddress, Port = TwinCat3Port };
        LeanCoreExtensions.AddNotification(settings, SpeedVariable);
        LeanCoreExtensions.AddWriteVariable(settings, SpeedVariable);
        _ = native.RegisterSymbol(SpeedVariable, InitialValue);
        native.Connect(settings);
        ExerciseLogicalConstructors(native);
        var client = new TwinCatLogicalTagClient(native);
        _ = client.CreateTag(SpeedTag, SpeedVariable, "DINT");
        _ = client.CreateTag(new LogicalTag(WriteOnlyTag, SpeedVariable, "DINT", new LogicalTagOptions
        {
            AccessMode = LogicalTagAccessMode.Write,
        }));
        client.RegisterTag(new LogicalTag(ReadOnlyTag, SpeedVariable, "DINT", new LogicalTagOptions
        {
            AccessMode = LogicalTagAccessMode.Read,
        }));

        await TUnitAssert.That((await client.ReadAsync(MissingVariable)).Succeeded).IsFalse();
        await TUnitAssert.That((await client.ReadAsync(WriteOnlyTag)).Succeeded).IsFalse();
        await TUnitAssert.That((await client.WriteAsync(CreateValue(ReadOnlyTag, UpdatedValue))).Succeeded).IsFalse();
        var reads = await client.ReadManyAsync([MissingVariable, WriteOnlyTag]);
        var writes = await client.WriteManyAsync(
            [CreateValue(MissingVariable, UpdatedValue), CreateValue(ReadOnlyTag, UpdatedValue)]);
        await TUnitAssert.That(reads.All(static result => !result.Succeeded)).IsTrue();
        await TUnitAssert.That(writes.All(static result => !result.Succeeded)).IsTrue();
        await TUnitAssert.That(async () => await client.ReadManyAsync(null!)).Throws<ArgumentNullException>();
        await TUnitAssert.That(async () => await client.WriteAsync(null!)).Throws<ArgumentNullException>();
        await TUnitAssert.That(async () => await client.WriteManyAsync(null!)).Throws<ArgumentNullException>();
        await TUnitAssert.That(() => client.Observe(MissingVariable)).Throws<KeyNotFoundException>();
        await TUnitAssert.That(() => client.Observe(WriteOnlyTag)).Throws<InvalidOperationException>();
        await TUnitAssert.That(() => client.ObserveMany(null!)).Throws<ArgumentNullException>();
        using var observation = LeanBridge.SubscribeTo(client.ObserveMany([SpeedTag, ReadOnlyTag]), _ => { });
        await TUnitAssert.That(client.RemoveTag(SpeedTag)).IsTrue();
        await TUnitAssert.That(client.RemoveTag(SpeedTag)).IsFalse();
        client.Dispose();
        await TUnitAssert.That(() => client.CreateTag(SpeedTag, SpeedVariable, "DINT"))
            .Throws<ObjectDisposedException>();
        await TUnitAssert.That(async () => await client.InitializeStoreAsync()).Throws<ObjectDisposedException>();
    }

    /// <summary>Verifies that a member write reports a materialization failure when its deterministic root is null.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("HashTableRx materializes logical structure payloads.")]
    [RequiresUnreferencedCode("HashTableRx materializes logical structure payloads.")]
#endif
    public async Task Logical_Structure_Write_With_Null_Root_Returns_A_Deterministic_FailureAsync()
    {
        using var native = new InMemoryAdsClient();
        _ = native.RegisterSymbol(StateVariable, null, typeof(TestStructure));
        native.Connect(new LeanSettings { AdsAddress = SimulatorAddress, Port = TwinCat3Port });
        using var client = new TwinCatLogicalTagClient(native);
        client.RegisterTag(CreateStructureTag(CountTag, CountTag));

        var result = await client.WriteAsync(CreateValue(CountTag, UpdatedValue));

        await TUnitAssert.That(result.Succeeded).IsFalse();
        await TUnitAssert.That(result.Error).Contains("could not be materialized");
    }

    /// <summary>Verifies logical helper normalization, metadata routing, cancellation, and table ownership without an ADS route.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Logical_Helpers_And_Structure_Table_Exercise_All_Deterministic_BranchesAsync()
    {
        const string RequiredValue = "value";

        await TUnitAssert.That(TwinCatLogicalTagHelpers.CanRead(LogicalTagAccessMode.Read)).IsTrue();
        await TUnitAssert.That(TwinCatLogicalTagHelpers.CanRead(LogicalTagAccessMode.Write)).IsFalse();
        await TUnitAssert.That(TwinCatLogicalTagHelpers.CanWrite(LogicalTagAccessMode.Write)).IsTrue();
        await TUnitAssert.That(TwinCatLogicalTagHelpers.CanWrite(LogicalTagAccessMode.Read)).IsFalse();
        await TUnitAssert.That(TwinCatLogicalTagHelpers.GetMemberAddress(".State", ".State.Count"))
            .IsEqualTo(CountTag);
        await TUnitAssert.That(TwinCatLogicalTagHelpers.GetMemberAddress(".State", ".Status.Count")).IsNull();
        await TUnitAssert.That(TwinCatLogicalTagHelpers.Required($" {RequiredValue} ", RequiredValue)).IsEqualTo(RequiredValue);
        await TUnitAssert.That(() => TwinCatLogicalTagHelpers.Required(" ", RequiredValue)).Throws<ArgumentException>();

        var tagged = new LogicalTag(
            CountTag,
            StateVariable,
            "DINT",
            new LogicalTagOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["TwinCAT.MemberAddress"] = $" {CountTag} ",
                    ["Empty"] = " ",
                },
            });
        await TUnitAssert.That(TwinCatLogicalTagHelpers.TryMetadata(tagged, "MemberAddress", out var member)).IsTrue();
        await TUnitAssert.That(member).IsEqualTo(CountTag);
        await TUnitAssert.That(TwinCatLogicalTagHelpers.TryMetadata(tagged, "Empty", out _)).IsFalse();
        await TUnitAssert.That(TwinCatLogicalTagHelpers.TryMetadata(tagged, "Missing", out _)).IsFalse();

        var cancelled = false;
        using var source = new CancellationTokenSource();
        using var registration = TwinCatLogicalTagHelpers.RegisterCancellation(() => cancelled = true, source.Token);
        source.Cancel();
        await TUnitAssert.That(cancelled).IsTrue();

        var first = new TrackingDisposable();
        var second = new TrackingDisposable();
        using (var table = new TwinCatStructureTable(useUpperCase: false))
        {
            table.SetSourceSubscription(first);
            table.SetSourceSubscription(second);
            await TUnitAssert.That(second.IsDisposed).IsTrue();
        }

        await TUnitAssert.That(first.IsDisposed).IsTrue();
    }

    /// <summary>Verifies simulator lifecycle, configured-handle, conversion, and result-format branches without time-based assertions.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("The IRxTcAdsClient contract carries dynamic-code compatibility annotations.")]
    [RequiresUnreferencedCode("The IRxTcAdsClient contract carries reflection compatibility annotations.")]
#endif
    public async Task Simulator_Lifecycle_And_Configured_Handle_Branches_Are_DeterministicAsync()
    {
        using var client = new InMemoryAdsClient();
        await TUnitAssert.That(() => client.Reconnect()).Throws<InvalidOperationException>();
        _ = client.RegisterSymbol(".Null", null)
            .RegisterSymbol(SpeedVariable, InitialValue)
            .RegisterSymbol(".Other", InitialValue)
            .RegisterSymbol(EnumVariable, TestMode.Idle, typeof(TestMode));
        var settings = new LeanSettings { AdsAddress = SimulatorAddress, Port = TwinCat3Port };
        LeanCoreExtensions.AddNotification(settings, SpeedVariable);
        LeanCoreExtensions.AddWriteVariable(settings, SpeedVariable);
        LeanCoreExtensions.AddWriteVariable(settings, ".Other");
        client.Connect(settings);

        client.Write(SpeedVariable, UpdatedValue);
        _ = client.QueueFault(InMemoryAdsOperation.Write, new IOException("expected"));
        client.Write(SpeedVariable, InitialValue);
        client.Write(EnumVariable, (int)TestMode.Running);
        client.Pause(TimeSpan.FromMinutes(1));
        client.Pause(TimeSpan.FromMinutes(1));
        client.Disconnect();

        LeanCoreExtensions.AddNotification(settings, LateVariable);
        LeanCoreExtensions.AddWriteVariable(settings, LateVariable);
        LeanCoreExtensions.AddWriteVariable(settings, LateWriteVariable);
        _ = client.RegisterSymbol(LateVariable, InitialValue)
            .RegisterSymbol(LateWriteVariable, InitialValue);
        client.Reconnect();

        await TUnitAssert.That(client.ReadWriteHandleInfo.ContainsKey(LateVariable)).IsTrue();
        await TUnitAssert.That(client.WriteHandleInfo.ContainsKey(LateVariable)).IsTrue();
        await TUnitAssert.That(client.WriteHandleInfo.ContainsKey(LateWriteVariable)).IsTrue();
        await TUnitAssert.That(client.TryGetValue<TestMode>(EnumVariable, out var mode)).IsTrue();
        await TUnitAssert.That(mode).IsEqualTo(TestMode.Running);
        await TUnitAssert.That(client.IsPaused).IsFalse();
    }

    /// <summary>Verifies invalid configured symbols, notification traversal, live handle synchronization, and timer disposal.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("The IRxTcAdsClient contract carries dynamic-code compatibility annotations.")]
    [RequiresUnreferencedCode("The IRxTcAdsClient contract carries reflection compatibility annotations.")]
#endif
    public async Task Simulator_Configuration_Validation_Live_Handle_Synchronization_And_Timer_Disposal_Are_DeterministicAsync()
    {
        const string missingNotification = ".Missing.Notification";
        const string missingWrite = ".Missing.Write";

        using (var invalidNotificationClient = new InMemoryAdsClient())
        {
            var errors = new List<Exception>();
            using var errorsSubscription = LeanBridge.SubscribeTo(invalidNotificationClient.ErrorReceived, errors.Add);
            var settings = new LeanSettings { AdsAddress = SimulatorAddress, Port = TwinCat3Port };
            LeanCoreExtensions.AddNotification(settings, missingNotification);
            invalidNotificationClient.Connect(settings);

            await TUnitAssert.That(invalidNotificationClient.ConnectionState).IsEqualTo(InMemoryAdsConnectionState.Faulted);
            await TUnitAssert.That(errors.Single().Message).Contains(missingNotification);
        }

        using (var invalidWriteClient = new InMemoryAdsClient())
        {
            var settings = new LeanSettings { AdsAddress = SimulatorAddress, Port = TwinCat3Port };
            LeanCoreExtensions.AddWriteVariable(settings, missingWrite);
            invalidWriteClient.Connect(settings);

            await TUnitAssert.That(invalidWriteClient.ConnectionState).IsEqualTo(InMemoryAdsConnectionState.Faulted);
        }

        var client = new InMemoryAdsClient();
        var liveSettings = new LeanSettings { AdsAddress = SimulatorAddress, Port = TwinCat3Port };
        LeanCoreExtensions.AddNotification(liveSettings, SpeedVariable);
        _ = client.RegisterSymbol(SpeedVariable, InitialValue);
        client.Connect(liveSettings);
        LeanCoreExtensions.AddNotification(liveSettings, LateVariable);
        LeanCoreExtensions.AddWriteVariable(liveSettings, LateVariable);
        _ = client.RegisterSymbol(LateVariable, UpdatedValue);
        client.PublishNotifications();
        client.Pause(TimeSpan.FromMinutes(1));
        client.Dispose();

        await TUnitAssert.That(client.ReadWriteHandleInfo).IsEmpty();
        await TUnitAssert.That(client.WriteHandleInfo).IsEmpty();
        await TUnitAssert.That(client.IsPaused).IsFalse();
        await TUnitAssert.That(client.ConnectionState).IsEqualTo(InMemoryAdsConnectionState.Disposed);

        using var descriptionClient = new InMemoryAdsClient();
        var descriptions = new List<string[]>();
        using var descriptionsSubscription = LeanBridge.SubscribeTo(descriptionClient.Code, descriptions.Add);
        _ = descriptionClient.RegisterSymbol(
            ".TypeParameter",
            null,
            typeof(List<>).GetGenericArguments()[0]);
        descriptionClient.Connect(new LeanSettings { AdsAddress = SimulatorAddress, Port = TwinCat3Port });

        await TUnitAssert.That(descriptions.Single()).Contains(".TypeParameter:T:-1");
    }

    /// <summary>Verifies logical client guards and mixed direct/structured bulk writes through the deterministic simulator.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("HashTableRx materializes logical structure payloads.")]
    [RequiresUnreferencedCode("HashTableRx materializes logical structure payloads.")]
#endif
    public async Task Logical_Client_Guards_And_Mixed_Bulk_Writes_Are_DeterministicAsync()
    {
        const string directVariable = ".Direct";
        const string directTag = "Direct";

        await TUnitAssert.That(() => new TwinCatLogicalTagClient(null!)).Throws<ArgumentNullException>();
        using var native = new InMemoryAdsClient();
        using var catalog = new LogicalTagCatalog();
        await TUnitAssert.That(() => new TwinCatLogicalTagClient(native, (ILogicalTagCatalog)null!))
            .Throws<ArgumentNullException>();

        var settings = new LeanSettings { AdsAddress = SimulatorAddress, Port = TwinCat3Port };
        LeanCoreExtensions.AddNotification(settings, StateVariable);
        LeanCoreExtensions.AddWriteVariable(settings, StateVariable);
        LeanCoreExtensions.AddWriteVariable(settings, directVariable);
        _ = native.RegisterStructure(StateVariable, new TestStructure { Count = InitialValue, Enabled = true })
            .RegisterSymbol(directVariable, InitialValue);
        native.Connect(settings);
        using var client = new TwinCatLogicalTagClient(native, catalog);
        await TUnitAssert.That(() => client.RegisterTag(null!)).Throws<ArgumentNullException>();
        client.RegisterTag(new LogicalTag(directTag, directVariable, "DINT"));
        client.RegisterTag(CreateStructureTag(CountTag, CountTag));
        client.RegisterTag(CreateStructureTag(EnabledTag, EnabledTag));

        await TUnitAssert.That(async () => await client.WriteManyAsync(
            [CreateValue(directTag, UpdatedValue), null!]))
            .Throws<ArgumentException>();
        var writes = await client.WriteManyAsync(
            [CreateValue(directTag, UpdatedValue), CreateValue(CountTag, ConversionValue), CreateValue(EnabledTag, false)]);

        await TUnitAssert.That(writes.All(static result => result.Succeeded)).IsTrue();
        await TUnitAssert.That(native.TryGetValue<int>(directVariable, out var direct)).IsTrue();
        await TUnitAssert.That(direct).IsEqualTo(UpdatedValue);
        await TUnitAssert.That(native.TryGetValue<TestStructure>(StateVariable, out var state)).IsTrue();
        await TUnitAssert.That(state!.Count).IsEqualTo(ConversionValue);
        await TUnitAssert.That(state.Enabled).IsFalse();
    }

    /// <summary>Verifies grouped logical writes return one deterministic failure for every member when the root cannot materialize.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("HashTableRx materializes logical structure payloads.")]
    [RequiresUnreferencedCode("HashTableRx materializes logical structure payloads.")]
#endif
    public async Task Logical_Grouped_Write_With_Null_Root_Fails_Every_Member_DeterministicallyAsync()
    {
        using var native = new InMemoryAdsClient();
        _ = native.RegisterSymbol(StateVariable, null, typeof(TestStructure));
        native.Connect(new LeanSettings { AdsAddress = SimulatorAddress, Port = TwinCat3Port });
        using var client = new TwinCatLogicalTagClient(native);
        client.RegisterTag(CreateStructureTag(CountTag, CountTag));
        client.RegisterTag(CreateStructureTag(EnabledTag, EnabledTag));

        var results = await client.WriteManyAsync(
            [CreateValue(CountTag, UpdatedValue), CreateValue(EnabledTag, false)]);

        await TUnitAssert.That(results).Count().IsEqualTo(ExpectedBulkCount);
        await TUnitAssert.That(results.All(static result => !result.Succeeded)).IsTrue();
        await TUnitAssert.That(results.All(static result => result.Error.Contains("could not be materialized"))).IsTrue();
    }

    /// <summary>Verifies notification-root discovery and grouped member writes without explicit root metadata.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("HashTableRx materializes logical structure payloads.")]
    [RequiresUnreferencedCode("HashTableRx materializes logical structure payloads.")]
#endif
    public async Task Logical_Route_Discovery_Uses_The_Longest_Notification_Root_And_Grouped_WritesAsync()
    {
        const string discoveredRoot = ".Machine.Detail";
        const string discoveredCount = ".Machine.Detail.Count";
        const string discoveredEnabled = ".Machine.Detail.Enabled";
        const string discoveredCountTag = "DiscoveredCount";
        const string discoveredEnabledTag = "DiscoveredEnabled";

        using var native = new InMemoryAdsClient();
        var settings = new LeanSettings { AdsAddress = SimulatorAddress, Port = TwinCat3Port };
        LeanCoreExtensions.AddNotification(settings, ".Machine");
        LeanCoreExtensions.AddNotification(settings, discoveredRoot);
        LeanCoreExtensions.AddWriteVariable(settings, discoveredRoot);
        _ = native.RegisterSymbol(".Machine", new object())
            .RegisterStructure(discoveredRoot, new TestStructure { Count = InitialValue, Enabled = true });
        native.Connect(settings);
        using var client = new TwinCatLogicalTagClient(native);
        client.RegisterTag(new LogicalTag(discoveredCountTag, discoveredCount, "DINT"));
        client.RegisterTag(new LogicalTag(discoveredEnabledTag, discoveredEnabled, "BOOL"));

        var reads = await client.ReadManyAsync([discoveredCountTag, discoveredEnabledTag]);

        await TUnitAssert.That(reads.All(static result => result.Succeeded)).IsTrue();
        await TUnitAssert.That(reads[0].Value!.Value).IsEqualTo(InitialValue);
        await TUnitAssert.That((bool)reads[1].Value!.Value!).IsTrue();
        await TUnitAssert.That(native.TryGetValue<TestStructure>(discoveredRoot, out _)).IsTrue();

        var writes = await client.WriteManyAsync(
            [CreateValue(discoveredCountTag, UpdatedValue), CreateValue(discoveredEnabledTag, false)]);

        await TUnitAssert.That(writes[0].Error).IsEmpty();
        await TUnitAssert.That(writes[1].Error).IsEmpty();
        await TUnitAssert.That(writes.All(static result => result.Succeeded)).IsTrue();
        await TUnitAssert.That(native.TryGetValue<TestStructure>(discoveredRoot, out var state)).IsTrue();
        await TUnitAssert.That(state!.Count).IsEqualTo(UpdatedValue);
        await TUnitAssert.That(state.Enabled).IsFalse();
    }

    /// <summary>Verifies the same simulator source behaves correctly in the System.Reactive package.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("The IRxTcAdsClient contract carries dynamic-code compatibility annotations.")]
    [RequiresUnreferencedCode("The IRxTcAdsClient contract carries reflection compatibility annotations.")]
#endif
    public async Task Reactive_Package_Provides_Equivalent_Simulator_BehaviorAsync()
    {
        using var client = new ReactiveClient();
        var settings = new ReactiveSettings
        {
            AdsAddress = SimulatorAddress,
            Port = TwinCat3Port,
            SettingsId = "reactive",
        };
        ReactiveCoreExtensions.AddNotification(settings, SpeedVariable);
        ReactiveCoreExtensions.AddWriteVariable(settings, SpeedVariable);
        var data = new List<(string Variable, object? Data, string? Id)>();
        var errors = new List<Exception>();
        var writes = new List<string?>();
        using var dataSubscription = client.DataReceived.Subscribe(new RecordingObserver<
            (string Variable, object? Data, string? Id)>(data));
        using var errorSubscription = client.ErrorReceived.Subscribe(new RecordingObserver<Exception>(errors));
        using var writeSubscription = client.OnWrite.Subscribe(new RecordingObserver<string?>(writes));
        _ = client.RegisterSymbol(SpeedVariable, InitialValue);
        client.Connect(settings);
        client.Read(SpeedVariable, "reactive-read");
        client.Write(SpeedVariable, UpdatedValue, "reactive-write");
        _ = client.QueueFault(ReactiveOperation.Read, new IOException("reactive fault"));
        client.Read(SpeedVariable, "reactive-fault");

        await TUnitAssert.That(client.Connected).IsTrue();
        await TUnitAssert.That(data.Any(static value => value.Id == "reactive-read" && (int)value.Data! == InitialValue))
            .IsTrue();
        await TUnitAssert.That(data.Any(static value => value.Id == "reactive-fault" && value.Data is null)).IsTrue();
        await TUnitAssert.That(writes).Contains("Success,reactive-write");
        await TUnitAssert.That(errors.Single().Message).IsEqualTo("reactive fault");
        await TUnitAssert.That(client.TryGetValue<int>(SpeedVariable, out var value)).IsTrue();
        await TUnitAssert.That(value).IsEqualTo(UpdatedValue);
    }

    /// <summary>Verifies the simulator exception and symbol metadata public surface.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Metadata_And_Exception_Surface_Is_CompleteAsync()
    {
        var defaultError = new InMemoryAdsException();
        var messageError = new InMemoryAdsException("message");
        var inner = new IOException("inner");
        var wrapped = new InMemoryAdsException("wrapped", inner);
        var operationError = new InMemoryAdsException(InMemoryAdsOperation.Read, "read");
        var variableError = new InMemoryAdsException(InMemoryAdsOperation.Write, "write", SpeedVariable);
        var symbol = new InMemoryAdsSymbol(
            SpeedVariable,
            InitialValue,
            typeof(int),
            -1,
            isReadable: true,
            isWritable: false);

        await TUnitAssert.That(defaultError.Operation).IsEqualTo(InMemoryAdsOperation.Connect);
        await TUnitAssert.That(messageError.Message).IsEqualTo("message");
        await TUnitAssert.That(wrapped.InnerException).IsSameReferenceAs(inner);
        await TUnitAssert.That(operationError.Operation).IsEqualTo(InMemoryAdsOperation.Read);
        await TUnitAssert.That(variableError.Variable).IsEqualTo(SpeedVariable);
        await TUnitAssert.That(symbol.Name).IsEqualTo(SpeedVariable);
        await TUnitAssert.That(symbol.Value).IsEqualTo(InitialValue);
        await TUnitAssert.That(symbol.DataType).IsEqualTo(typeof(int));
        await TUnitAssert.That(symbol.ArrayLength).IsEqualTo(-1);
        await TUnitAssert.That(symbol.IsReadable).IsTrue();
        await TUnitAssert.That(symbol.IsWritable).IsFalse();
    }

    /// <summary>Creates standard simulator settings.</summary>
    /// <returns>The settings.</returns>
    private static LeanSettings CreateSettings()
    {
        var settings = new LeanSettings
        {
            AdsAddress = SimulatorAddress,
            Port = TwinCat3Port,
            SettingsId = "test",
        };
        LeanCoreExtensions.AddNotification(settings, SpeedVariable);
        LeanCoreExtensions.AddNotification(
            settings,
            SamplesVariable,
            NotificationCycleTime,
            FullArrayLength);
        LeanCoreExtensions.AddWriteVariable(settings, SpeedVariable);
        LeanCoreExtensions.AddWriteVariable(settings, SamplesVariable, FullArrayLength);
        return settings;
    }

    /// <summary>Exercises all public logical-client constructor compositions.</summary>
    /// <param name="native">The composed native simulator.</param>
#if NET9_0_OR_GREATER
    [RequiresUnreferencedCode("Logical TwinCAT clients support HashTableRx structure materialization.")]
#endif
    private static void ExerciseLogicalConstructors(InMemoryAdsClient native)
    {
        using var catalog = new LogicalTagCatalog();
        var store = new LogicalTagSqliteStore("Data Source=:memory:");
        using var defaultClient = new TwinCatLogicalTagClient(native);
        using var timedClient = new TwinCatLogicalTagClient(native, TimeProvider.System);
        using var catalogClient = new TwinCatLogicalTagClient(native, catalog);
        using var timedCatalogClient = new TwinCatLogicalTagClient(native, catalog, TimeProvider.System);
        using var storeClient = new TwinCatLogicalTagClient(native, store);
        using var timedStoreClient = new TwinCatLogicalTagClient(native, store, TimeProvider.System);
        using var combinedClient = new TwinCatLogicalTagClient(native, catalog, store);
        using var timedCombinedClient = new TwinCatLogicalTagClient(
            native,
            catalog,
            store,
            TimeProvider.System);
    }

    /// <summary>Exercises idempotent disposal and no-op behavior after disposal.</summary>
    /// <param name="client">The simulator.</param>
    private static void DisposeTwiceAndExerciseNoOps(InMemoryAdsClient client)
    {
        client.Dispose();
        client.Dispose();
        client.Disconnect();
        _ = typeof(InMemoryAdsClient)
            .GetMethod("ReportError", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(client, [new IOException("ignored after disposal")]);
    }

    /// <summary>Registers the standard scalar and array symbols.</summary>
    /// <param name="client">The simulator.</param>
    private static void RegisterStandardSymbols(InMemoryAdsClient client) =>
        _ = client
            .RegisterSymbol(SpeedVariable, InitialValue)
            .RegisterSymbol(
                SamplesVariable,
                new[] { InitialValue, UpdatedValue, ConversionValue },
                typeof(int[]),
                FullArrayLength,
                isReadable: true,
                isWritable: true);

    /// <summary>Exercises scripted operation failures and timed pause behavior.</summary>
    /// <param name="client">The connected simulator.</param>
    /// <returns>The exercise task.</returns>
    private static async Task ExerciseFaultsAndPauseAsync(InMemoryAdsClient client)
    {
        _ = client.QueueFault(InMemoryAdsOperation.Read, new IOException("read fault"));
        client.Read(SpeedVariable, "faulted-read");
        _ = client.QueueFault(InMemoryAdsOperation.Write, new IOException("write fault"));
        client.Write(SamplesVariable, new[] { ConversionValue, FinalArrayValue }, "faulted-write");
        _ = client.QueueFault(InMemoryAdsOperation.Notification, new IOException("notification fault"));
        client.PublishNotifications();
        client.Write(SpeedVariable, UpdatedValue, "read-only");
        client.Read(SamplesVariable, "write-only");
        client.Read(MissingVariable, "missing");
        client.Pause(TimeSpan.FromMilliseconds(PauseDurationMilliseconds));
        await Task.Delay(TimeSpan.FromMilliseconds(PauseWaitMilliseconds));
    }

    /// <summary>Creates a structure-backed logical tag.</summary>
    /// <param name="name">The logical name.</param>
    /// <param name="member">The member name.</param>
    /// <returns>The logical tag.</returns>
    private static LogicalTag CreateStructureTag(string name, string member) =>
        new(
            name,
            $"{StateVariable}.{member}",
            member == CountTag ? "DINT" : "BOOL",
            new LogicalTagOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["TwinCAT.StructureRoot"] = StateVariable,
                    ["TwinCAT.MemberAddress"] = member,
                },
            });

    /// <summary>Creates a logical value.</summary>
    /// <param name="name">The logical tag name.</param>
    /// <param name="value">The payload.</param>
    /// <returns>The logical value.</returns>
    private static LogicalTagValue CreateValue(string name, object value) =>
        new(name, value, TimeProvider.System.GetUtcNow(), "Good");

    /// <summary>Provides a structure payload for grouped logical operations.</summary>
    private sealed class TestStructure
    {
        /// <summary>Gets or sets the count.</summary>
        public int Count { get; set; }

        /// <summary>Gets or sets whether the state is enabled.</summary>
        public bool Enabled { get; set; }
    }

    /// <summary>Records deterministic disposal ownership for structure table tests.</summary>
    private sealed class TrackingDisposable : IDisposable
    {
        /// <summary>Gets whether disposal was requested.</summary>
        public bool IsDisposed { get; private set; }

        /// <inheritdoc/>
        public void Dispose() => IsDisposed = true;
    }

    /// <summary>Records observable values without extension-method ambiguity.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="values">The destination values.</param>
    private sealed class RecordingObserver<T>(ICollection<T> values) : IObserver<T>
    {
        /// <inheritdoc/>
        public void OnCompleted()
        {
        }

        /// <inheritdoc/>
        public void OnError(Exception error) => throw error;

        /// <inheritdoc/>
        public void OnNext(T value) => values.Add(value);
    }
}
