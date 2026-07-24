// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using IoT.DriverCore.S7PlcRx.Enums;
using IoT.DriverCore.S7PlcRx.Mock;
using TUnitAssert = TUnit.Assertions.Assert;
using TagCollection = global::IoT.DriverCore.S7PlcRx.Tags;

namespace IoT.DriverCore.S7PlcRx.Tests.Core;

/// <summary>Exercises residual tag collection, projection, polling, and watchdog paths against the managed S7 simulator.</summary>
[NotInParallel]
public sealed class S7PollingAndTagResidualCoverageTests
{
    /// <summary>Defines the deterministic local data-block address.</summary>
    private const string ByteAddress = "DB1.DBB0";

    /// <summary>Defines the tag name used for the byte read.</summary>
    private const string ByteTagName = nameof(ByteAddress);

    /// <summary>Defines the scalar array length.</summary>
    private const int ScalarArrayLength = 2;

    /// <summary>Defines the fixed array length.</summary>
    private const int FixedArrayLength = 3;

    /// <summary>Defines the retained tag value.</summary>
    private const int RetainedTagValue = 42;

    /// <summary>Defines the number of non-null root tag values.</summary>
    private const int RootTagCount = 4;

    /// <summary>Defines the long interval that prevents automatic polling during direct invocation.</summary>
    private const int ManualPollingIntervalMilliseconds = 60_000;

    /// <summary>Defines the observed temperature value.</summary>
    private const int TemperatureValue = 21;

    /// <summary>Defines the managed data-block size.</summary>
    private const int DatabaseSize = 32;

    /// <summary>Defines the operation timeout in seconds.</summary>
    private const int OperationTimeoutSeconds = 60;

    /// <summary>Defines the observable state tag name.</summary>
    private const string StateTagName = "State";

    /// <summary>Defines the deterministic watchdog address.</summary>
    private const string WatchdogAddress = "DB1.DBW4";

    /// <summary>Defines the polling interval used by the managed simulator.</summary>
    private const int PollingIntervalMilliseconds = 1;

    /// <summary>Defines the watchdog value used by the managed simulator.</summary>
    private const ushort WatchdogValue = 1234;

    /// <summary>Defines the reflected polling-cycle method name.</summary>
    private const string ProcessTagPollingMethodName = "ProcessTagPollingAsync";

    /// <summary>Defines the reflected pause-notification method name.</summary>
    private const string NotifyPausedMethodName = "NotifyPaused";

    /// <summary>Defines the reflected watchdog observable method name.</summary>
    private const string WatchdogObservableMethodName = "WatchDogObservable";

    /// <summary>Defines the reflected last-connected field name.</summary>
    private const string LastConnectedFieldName = "_lastConnectedAtUtc";

    /// <summary>Defines the reflected PLC-request subject field name.</summary>
    private const string PlcRequestSubjectFieldName = "_plcRequestSubject";

    /// <summary>Defines the reflected pause subject field name.</summary>
    private const string PausedSubjectFieldName = "_paused";

    /// <summary>Exercises all tag constructors and the guarded collection mutation paths.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    [Test]
    public async Task TagsAndTagConstructorsRetainMetadataAndFilterNullValuesAsync()
    {
        var defaultTag = new Tag();
        var scalarTag = new Tag(ByteAddress, typeof(byte));
        var arrayTag = new Tag("Array", typeof(byte[]), ScalarArrayLength);
        var namedTag = new Tag("Named", ByteAddress, typeof(ushort));
        var fixedTag = new Tag("Fixed", ByteAddress, typeof(short[]), FixedArrayLength);
        var valuedTag = new Tag("Valued", ByteAddress, RetainedTagValue, typeof(int));
        var nullValueTag = new Tag("Null", ByteAddress, typeof(byte)) { Value = null };
        var tags = new TagCollection();
        var nested = new TagCollection();

        tags[(object)scalarTag.Name!] = scalarTag;
        tags.Add("array", arrayTag);
        tags.Add(namedTag);
        nested.Add(valuedTag);
        tags.Add("nested", nested);
        tags.AddRange([valuedTag, nullValueTag]);

        await TUnitAssert.That(defaultTag.Type).IsEqualTo(typeof(object));
        await TUnitAssert.That(defaultTag.Name).IsEmpty();
        await TUnitAssert.That(scalarTag.Name).IsEqualTo(ByteAddress);
        await TUnitAssert.That(scalarTag.ArrayLength).IsEqualTo(1);
        await TUnitAssert.That(arrayTag.ArrayLength).IsEqualTo(ScalarArrayLength);
        await TUnitAssert.That(fixedTag.ArrayLength).IsEqualTo(FixedArrayLength);
        await TUnitAssert.That(namedTag.Address).IsEqualTo(ByteAddress);
        await TUnitAssert.That(valuedTag.Value).IsEqualTo(RetainedTagValue);
        await TUnitAssert.That(tags[scalarTag]).IsSameReferenceAs(scalarTag);
        await TUnitAssert.That(tags[(object)scalarTag.Name!]).IsSameReferenceAs(scalarTag);
        await TUnitAssert.That(tags.Get(null)).IsNull();
        await TUnitAssert.That(tags.GetTags().ToList().Count).IsEqualTo(RootTagCount);
        await TUnitAssert.That(tags.ToList().Count).IsEqualTo(RootTagCount);

        await TUnitAssert.That(() => tags.AddRange(null!)).Throws<ArgumentNullException>();
        await TUnitAssert.That(() => tags.Add(new Tag { Name = " " })).Throws<ArgumentException>();
    }

    /// <summary>Invokes one polling cycle directly so its disconnected pause behavior does not depend on a timer.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    [Test]
    public async Task PollingCyclePausesDeterministicallyWhenDisconnectedAsync()
    {
        using var plc = new RxS7(new(
            new(CpuType.S71500, MockServer.Localhost, 0, 1),
            new(ManualPollingIntervalMilliseconds)));
        var paused = new List<bool>();
        using var subscription = plc.IsPaused.Subscribe(paused.Add);
        await InvokePrivateTaskAsync(plc, ProcessTagPollingMethodName);

        await TUnitAssert.That(paused.Count).IsEqualTo(1);
        await TUnitAssert.That(paused[0]).IsTrue();
    }

    /// <summary>Exercises the non-null observable projections and mutable dictionary snapshot.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    [Test]
    public async Task TagOperationsProjectOnlyNamedNonNullValuesAsync()
    {
        var taggedValues = new List<(string Tag, string Value)>();
        var snapshots = new List<IDictionary<string, object>>();
        var tag = new Tag("Temperature", ByteAddress, TemperatureValue, typeof(int));

        using var taggedSubscription = TagOperations
            .ToTagValue(Observable.Return<string?>("ready"), StateTagName)
            .Subscribe(taggedValues.Add);
        using var emptySubscription = TagOperations
            .ToTagValue(Observable.Return<string?>(null), StateTagName)
            .Subscribe(taggedValues.Add);
        using var snapshotSubscription = TagOperations
            .TagToDictionary(Observable.Return<Tag?>(tag))
            .Subscribe(snapshots.Add);

        await TUnitAssert.That(taggedValues.Count).IsEqualTo(1);
        await TUnitAssert.That(taggedValues[0]).IsEqualTo((StateTagName, "ready"));
        await TUnitAssert.That(snapshots.Count).IsEqualTo(1);
        await TUnitAssert.That(snapshots[0]["Temperature"]).IsEqualTo(TemperatureValue);
    }

    /// <summary>Exercises managed polling, a normal read, and the configured watchdog registration without hardware.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    [Test]
    public async Task ManagedPollingRegistersWatchdogAndReadsConfiguredTagAsync()
    {
        using var server = new MockServer { DefaultDb1Size = DatabaseSize };
        await TUnitAssert.That(server.Start()).IsEqualTo(0);
        using var plc = new RxS7(new(
            new(CpuType.S71500, MockServer.Localhost, 0, 1),
            new(PollingIntervalMilliseconds),
            new(WatchdogAddress, WatchdogValue, 1)));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));

        _ = await plc.IsConnected.Where(connected => connected).Timeout(TimeSpan.FromSeconds(OperationTimeoutSeconds)).FirstAsync();
        _ = TagOperations.AddUpdateTagItem(plc, typeof(byte), ByteTagName, ByteAddress).SetPolling(false);
        var value = await plc.ReadAsync(new LogicalTagKey<byte>(ByteTagName), cancellation.Token);

        await TUnitAssert.That(plc.TagList["WatchDog"]).IsNotNull();
        await TUnitAssert.That(plc.TagList["WatchDog"]!.DoNotPoll).IsTrue();
        await TUnitAssert.That(value).IsEqualTo(byte.MinValue);
    }

    /// <summary>Exercises a connected polling cycle and its disposed-observer race handling.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    [Test]
    public async Task ConnectedPollingPublishesReadsAndToleratesDisposedSubjectsAsync()
    {
        using var server = new MockServer { DefaultDb1Size = DatabaseSize };
        await TUnitAssert.That(server.Start()).IsEqualTo(0);
        using var plc = new RxS7(new(
            new(CpuType.S71500, MockServer.Localhost, 0, 1),
            new(ManualPollingIntervalMilliseconds)));
        var paused = new List<bool>();
        var readTimes = new List<long>();
        var errors = new List<string>();
        using var pausedSubscription = GetPrivateField<IObservable<bool>>(
            plc,
            PausedSubjectFieldName).Subscribe(paused.Add);
        using var readTimeSubscription = plc.ReadTime.Subscribe(readTimes.Add);
        using var errorSubscription = plc.LastError.Subscribe(errors.Add);

        _ = await plc.IsConnected
            .Where(static connected => connected)
            .Timeout(TimeSpan.FromSeconds(OperationTimeoutSeconds))
            .FirstAsync();
        _ = TagOperations.AddUpdateTagItem(plc, typeof(byte), ByteTagName, ByteAddress)
            .SetPolling();
        SetPrivateField(plc, LastConnectedFieldName, DateTime.MinValue);
        SetPrivateProperty(plc, nameof(RxS7.IsConnectedValue), true);

        await InvokePrivateTaskAsync(plc, ProcessTagPollingMethodName);

        await TUnitAssert.That(paused).Contains(false);
        await TUnitAssert.That(readTimes.Count).IsGreaterThan(0);

        GetPrivateField<IDisposable>(plc, PlcRequestSubjectFieldName).Dispose();
        await InvokePrivateTaskAsync(plc, ProcessTagPollingMethodName);
        await TUnitAssert.That(errors.Count).IsGreaterThan(0);

        GetPrivateField<IDisposable>(plc, PausedSubjectFieldName).Dispose();
        _ = GetPrivateMethod(NotifyPausedMethodName).Invoke(plc, [true]);
    }

    /// <summary>Exercises disabled and status-reporting watchdog observables deterministically.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    [Test]
    public async Task WatchdogObservableCompletesWhenDisabledAndReportsConfiguredWritesAsync()
    {
        {
            using var disabled = new RxS7(new(
                new(CpuType.S71500, MockServer.Localhost, 0, 1),
                new(ManualPollingIntervalMilliseconds)));
            var completed = false;
            using var disabledSubscription = InvokeWatchdogObservable(disabled).Subscribe(
                _ => { },
                () => completed = true);
            await TUnitAssert.That(completed).IsTrue();
        }

        using var server = new MockServer { DefaultDb1Size = DatabaseSize };
        await TUnitAssert.That(server.Start()).IsEqualTo(0);
        using var plc = new RxS7(new(
            new(CpuType.S71500, MockServer.Localhost, 0, 1),
            new(ManualPollingIntervalMilliseconds),
            new(WatchdogAddress, WatchdogValue, 1)));
        var status = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var statusSubscription = plc.Status
            .Where(message => message.Contains("WatchDog writing", StringComparison.Ordinal))
            .Subscribe(message => _ = status.TrySetResult(message));

        _ = await plc.IsConnected
            .Where(static connected => connected)
            .Timeout(TimeSpan.FromSeconds(OperationTimeoutSeconds))
            .FirstAsync();
        plc.ShowWatchDogWriting = true;
        using var watchdogSubscription = InvokeWatchdogObservable(plc).Subscribe();

        var message = await AsyncCompatibility.WaitAsync(
            status.Task,
            TimeSpan.FromSeconds(OperationTimeoutSeconds));
        await TUnitAssert.That(message).Contains(WatchdogAddress);
    }

    /// <summary>Gets a private RxS7 method.</summary>
    /// <param name="methodName">The method name.</param>
    /// <returns>The reflected method.</returns>
    private static MethodInfo GetPrivateMethod(string methodName) =>
        typeof(RxS7).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic) ??
        throw new InvalidOperationException($"{methodName} was not found.");

    /// <summary>Gets a private RxS7 field value.</summary>
    /// <typeparam name="TValue">The field value type.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="fieldName">The field name.</param>
    /// <returns>The field value.</returns>
    private static TValue GetPrivateField<TValue>(RxS7 plc, string fieldName)
    {
        var field = typeof(RxS7).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException($"{fieldName} was not found.");
        return (TValue)field.GetValue(plc)!;
    }

    /// <summary>Sets a private RxS7 field value.</summary>
    /// <typeparam name="TValue">The field value type.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="fieldName">The field name.</param>
    /// <param name="value">The field value.</param>
    private static void SetPrivateField<TValue>(RxS7 plc, string fieldName, TValue value)
    {
        var field = typeof(RxS7).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException($"{fieldName} was not found.");
        field.SetValue(plc, value);
    }

    /// <summary>Sets a private-set RxS7 property value.</summary>
    /// <typeparam name="TValue">The property value type.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The property value.</param>
    private static void SetPrivateProperty<TValue>(
        RxS7 plc,
        string propertyName,
        TValue value)
    {
        var property = typeof(RxS7).GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public) ??
            throw new InvalidOperationException($"{propertyName} was not found.");
        property.SetValue(plc, value);
    }

    /// <summary>Invokes a private asynchronous RxS7 method.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="methodName">The method name.</param>
    /// <returns>A task representing the invocation.</returns>
    private static async Task InvokePrivateTaskAsync(RxS7 plc, string methodName)
    {
        var task = (Task?)GetPrivateMethod(methodName).Invoke(plc, null) ??
            throw new InvalidOperationException($"{methodName} did not return a task.");
        await task.ConfigureAwait(false);
    }

    /// <summary>Invokes the private watchdog-observable factory.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <returns>The watchdog observable.</returns>
    private static IObservable<RxVoid> InvokeWatchdogObservable(RxS7 plc) =>
        (IObservable<RxVoid>?)GetPrivateMethod(WatchdogObservableMethodName).Invoke(plc, null) ??
        throw new InvalidOperationException("The watchdog observable was not returned.");
}
