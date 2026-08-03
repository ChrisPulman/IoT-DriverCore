// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if !NETFRAMEWORK
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
#endif
using System.Collections.Concurrent;
using System.ServiceProcess;
using System.Text;
using IoT.Driver.TwinCATRx.Core;
using TwinCAT.Ads;
using TwinCAT.TypeSystem;
using CoreNotificationContract = IoT.Driver.TwinCATRx.Core.INotification;
using LeanBridge = IoT.Driver.TwinCATRx.ObservableBridgeExtensions;
using RxNotification = IoT.Driver.TwinCATRx.Core.Notification;

namespace IoT.Driver.TwinCATRx.Tests.Rx;

/// <summary>Exercises the production ADS client through deterministic composed dependencies.</summary>
[NotInParallel]
public sealed class RxTcAdsClientCompositionTests
{
    /// <summary>The expected notification handle count.</summary>
    private const int ExpectedNotificationHandleCount = 2;

    /// <summary>A scalar PLC payload.</summary>
    private const int ScalarPayload = 42;

    /// <summary>The simulated stopped-device state.</summary>
    private const int StoppedDeviceState = 7;

    /// <summary>A text and string notification length.</summary>
    private const int TextLength = 5;

    /// <summary>The first TwinCAT 3 runtime port.</summary>
    private const int TwinCat3Port = 851;

    /// <summary>The notification polling rate.</summary>
    private const int UpdateRate = 10;

    /// <summary>A write payload.</summary>
    private const int WritePayload = 73;

    /// <summary>A deterministic array symbol name.</summary>
    private const string ArraySymbolName = "Array";

    /// <summary>A deterministic array variable.</summary>
    private const string ArrayVariable = ".Array";

    /// <summary>A deterministic scalar variable.</summary>
    private const string ScalarVariable = ".Scalar";

    /// <summary>A deterministic text variable.</summary>
    private const string TextVariable = ".Text";

    /// <summary>A deterministic shared symbol name.</summary>
    private const string ValueSymbolName = "Value";

    /// <summary>A deterministic unsupported variable.</summary>
    private const string UnsupportedVariable = ".Unsupported";

    /// <summary>A deterministic shared value variable.</summary>
    private const string ValueVariable = ".Value";

    /// <summary>A deterministic read failure message.</summary>
    private const string ReadFailureMessage = "read failed";

    /// <summary>A deterministic connection failure message.</summary>
    private const string ConnectFailureMessage = "connect failed";

    /// <summary>A deterministic handle failure message.</summary>
    private const string HandleFailureMessage = "handle failed";

    /// <summary>A deterministic write failure message.</summary>
    private const string WriteFailureMessage = "write failed";

    /// <summary>A deterministic write-only variable.</summary>
    private const string WriteOnlyVariable = ".WriteOnly";

    /// <summary>The maximum wait for a queued reactive publication.</summary>
    private static readonly TimeSpan PublicationTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Verifies connection, initialization, notification, read, and write branches.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("Exercises production initialization and dynamic PLC type resolution.")]
    [RequiresUnreferencedCode("Exercises production initialization and dynamic PLC type resolution.")]
#endif
    public async Task Composed_Runtime_Initializes_And_Transfers_ValuesAsync()
    {
        var ads = new FakeAdsClient { Port = TwinCat3Port };
        using var platform = new FakePlatform(ads);
        platform.AddSymbol("Scalar", "DINT", DataTypeCategory.Primitive);
        platform.AddSymbol("Text", $"STRING({TextLength})", DataTypeCategory.String);
        platform.AddSymbol("WriteOnly", "DINT", DataTypeCategory.Primitive);
        ads.ValuesByVariable[ScalarVariable] = ScalarPayload;
        ads.ValuesByVariable[TextVariable] = "hello";
        var settings = new Settings { AdsAddress = "1.2.3.4.5.6", Port = TwinCat3Port };
        settings.Notifications.Add(new RxNotification(UpdateRate, ScalarVariable));
        settings.Notifications.Add(new RxNotification(UpdateRate, TextVariable, TextLength));
        settings.WriteVariables.Add(new WriteVariable(WriteOnlyVariable));
        using var client = new RxTcAdsClient(TimeProvider.System, platform);
        var initialized = new List<Unit>();
        var data = new List<(string Variable, object? Data, string? Id)>();
        var writes = new List<string?>();
        using var initializedSubscription = LeanBridge.SubscribeTo(client.InitializeComplete, initialized.Add);
        using var dataSubscription = LeanBridge.SubscribeTo(client.DataReceived, data.Add);
        using var writeSubscription = LeanBridge.SubscribeTo(client.OnWrite, writes.Add);

        client.Connect(settings);
        platform.Ticks.Emit(0);
        client.Read(ScalarVariable, "read");
        client.Read(TextVariable, "text");
        client.Write(WriteOnlyVariable, WritePayload, "write");
        settings.Notifications.Clear();
        client.Read(ScalarVariable, "unconfigured-notification");

        await TUnitAssert.That(ads.RemoteConnectCount).IsEqualTo(1);
        await TUnitAssert.That(platform.LoadSymbolsCount).IsEqualTo(1);
        await TUnitAssert.That(client.Connected).IsTrue();
        await TUnitAssert.That(initialized.Count).IsEqualTo(1);
        await TUnitAssert.That(client.ReadWriteHandleInfo.Count).IsEqualTo(ExpectedNotificationHandleCount);
        await TUnitAssert.That(client.WriteHandleInfo.Count).IsEqualTo(1);
        await TUnitAssert.That(
            data.Exists(static item => item.Variable == ScalarVariable && Equals(item.Data, ScalarPayload))).IsTrue();
        await TUnitAssert.That(data.Exists(static item => item.Id == "text" && Equals(item.Data, "hello"))).IsTrue();
        await TUnitAssert.That(writes).Contains("Success,write");
        await TUnitAssert.That(ads.ValuesByVariable[WriteOnlyVariable]).IsEqualTo(WritePayload);
    }

    /// <summary>Verifies local connection and native state/read/write failure publication.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("Exercises production initialization and dynamic PLC type resolution.")]
    [RequiresUnreferencedCode("Exercises production initialization and dynamic PLC type resolution.")]
#endif
    public async Task Composed_Runtime_Publishes_Native_FailuresAsync()
    {
        var ads = new FakeAdsClient { Port = TwinCat3Port };
        using var platform = new FakePlatform(ads);
        platform.AddSymbol(ValueSymbolName, "DINT", DataTypeCategory.Primitive);
        ads.ValuesByVariable[ValueVariable] = 1;
        var settings = new Settings { AdsAddress = string.Empty, Port = TwinCat3Port };
        settings.Notifications.Add(new RxNotification(UpdateRate, ValueVariable));
        settings.WriteVariables.Add(new WriteVariable(ValueVariable));
        using var client = new RxTcAdsClient(TimeProvider.System, platform);
        var errors = new ConcurrentQueue<Exception>();
        var writes = new ConcurrentQueue<string?>();
        var initializationPublished = CreatePublicationSource();
        var readFailurePublished = CreatePublicationSource();
        var writeFailurePublished = CreatePublicationSource();
        using var initializationSubscription = LeanBridge.SubscribeTo(
            client.InitializeComplete,
            value => CompletePublication(initializationPublished, value));
        using var errorSubscription = LeanBridge.SubscribeTo(
            client.ErrorReceived,
            error => RecordErrorPublication(errors, readFailurePublished, ReadFailureMessage, error));
        using var writeSubscription = LeanBridge.SubscribeTo(
            client.OnWrite,
            write => RecordWritePublication(writes, writeFailurePublished, write));

        client.Connect(settings);
        await TUnitAssert.That(
            await DriveTicksUntilPublicationAsync(initializationPublished.Task, platform.Ticks)).IsTrue();
        await AssertNativeReadFailureAsync(client, ads, readFailurePublished.Task);
        ads.WriteAnyError = new IOException(WriteFailureMessage);
        client.Write(ValueVariable, ExpectedNotificationHandleCount, "write");
        await TUnitAssert.That(await WaitForPublicationAsync(writeFailurePublished.Task)).IsTrue();
        ads.WriteAnyError = null;
        ads.State = new(AdsState.Stop, StoppedDeviceState);
        ads.WriteControlError = new IOException("control failed");
        platform.Ticks.Emit(1);

        var stateAds = new FakeAdsClient
        {
            Port = TwinCat3Port,
            ReadStateError = new IOException("state failed"),
        };
        using var statePlatform = new FakePlatform(stateAds);
        using var stateClient = new RxTcAdsClient(TimeProvider.System, statePlatform);
        using var stateSubscription = LeanBridge.SubscribeTo(stateClient.ErrorReceived, errors.Enqueue);
        stateClient.Connect(new Settings { Port = TwinCat3Port });
        statePlatform.Ticks.Emit(ExpectedNotificationHandleCount);

        await TUnitAssert.That(ads.LocalConnectCount).IsEqualTo(1);
        await TUnitAssert.That(ContainsError(errors, ReadFailureMessage)).IsTrue();
        await TUnitAssert.That(ContainsError(errors, WriteFailureMessage)).IsTrue();
        await TUnitAssert.That(ContainsError(errors, "Ads Fault")).IsTrue();
        await TUnitAssert.That(ContainsError(errors, "control failed")).IsTrue();
        await TUnitAssert.That(ContainsWrite(writes, WriteFailureMessage)).IsTrue();
    }

    /// <summary>Verifies configuration edge cases and notification length errors.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("Exercises production initialization and dynamic PLC type resolution.")]
    [RequiresUnreferencedCode("Exercises production initialization and dynamic PLC type resolution.")]
#endif
    public async Task Composed_Runtime_Covers_Configuration_EdgesAsync()
    {
        var ads = new FakeAdsClient { Port = TwinCat3Port };
        using var platform = new FakePlatform(ads);
        platform.AddSymbol("Text", "STRING(80)", DataTypeCategory.String);
        platform.AddSymbol(ArraySymbolName, "ARRAY [0..2] OF DINT", DataTypeCategory.Array);
        platform.AddSymbol("Unsupported", "POINTER TO DINT", DataTypeCategory.Primitive);
        var settings = new Settings { Port = TwinCat3Port };
        settings.Notifications.Add(new RxNotification(UpdateRate, string.Empty));
        settings.Notifications.Add(new RxNotification(UpdateRate, TextVariable));
        settings.Notifications.Add(new RxNotification(UpdateRate, string.Empty));
        settings.Notifications.Add(new RxNotification(UpdateRate, ArrayVariable));
        settings.Notifications.Add(new RxNotification(UpdateRate, UnsupportedVariable));
        settings.WriteVariables.Add(new WriteVariable(string.Empty));
        settings.WriteVariables.Add(new WriteVariable(".Missing"));
        settings.WriteVariables.Add(new WriteVariable(UnsupportedVariable));
        using var client = new RxTcAdsClient(TimeProvider.System, platform);
        var errors = new List<Exception>();
        using var subscription = LeanBridge.SubscribeTo(client.ErrorReceived, errors.Add);

        client.Connect(settings);
        platform.Ticks.Emit(0);
        platform.Ticks.Emit(1);

        await TUnitAssert.That(client.Connected).IsTrue();
        await TUnitAssert.That(client.ReadWriteHandleInfo.ContainsKey(TextVariable)).IsTrue();
        await TUnitAssert.That(client.ReadWriteHandleInfo.ContainsKey(ArrayVariable)).IsTrue();
        await TUnitAssert.That(client.ReadWriteHandleInfo.ContainsKey(UnsupportedVariable)).IsFalse();
        await TUnitAssert.That(client.WriteHandleInfo.ContainsKey(".Missing")).IsTrue();
        await TUnitAssert.That(ContainsError(errors, "String length")).IsTrue();
        await TUnitAssert.That(ContainsError(errors, "Array length")).IsTrue();
    }

    /// <summary>Verifies initialization errors are reported without external ADS infrastructure.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("Exercises production initialization and dynamic PLC type resolution.")]
    [RequiresUnreferencedCode("Exercises production initialization and dynamic PLC type resolution.")]
#endif
    public async Task Composed_Runtime_Reports_Connect_And_Handle_FailuresAsync()
    {
        var connectionAds = new FakeAdsClient { ConnectError = new IOException(ConnectFailureMessage) };
        using var connectionPlatform = new FakePlatform(connectionAds);
        using var connectionClient = new RxTcAdsClient(TimeProvider.System, connectionPlatform);
        var connectionErrors = new ConcurrentQueue<Exception>();
        var connectionFailurePublished = CreatePublicationSource();
        using var connectionSubscription = LeanBridge.SubscribeTo(
            connectionClient.ErrorReceived,
            error => RecordErrorPublication(
                connectionErrors,
                connectionFailurePublished,
                ConnectFailureMessage,
                error));
        connectionClient.Connect(new Settings());

        var handleAds = new FakeAdsClient
        {
            Port = TwinCat3Port,
            CreateHandleError = new IOException(HandleFailureMessage),
        };
        using var handlePlatform = new FakePlatform(handleAds);
        handlePlatform.AddSymbol(ValueSymbolName, "DINT", DataTypeCategory.Primitive);
        var handleSettings = new Settings { Port = TwinCat3Port };
        handleSettings.Notifications.Add(new RxNotification(UpdateRate, ValueVariable));
        using var handleClient = new RxTcAdsClient(TimeProvider.System, handlePlatform);
        var handleErrors = new ConcurrentQueue<Exception>();
        var handleFailurePublished = CreatePublicationSource();
        using var handleSubscription = LeanBridge.SubscribeTo(
            handleClient.ErrorReceived,
            error => RecordErrorPublication(
                handleErrors,
                handleFailurePublished,
                HandleFailureMessage,
                error));
        handleClient.Connect(handleSettings);
        await TUnitAssert.That(
            await WaitForPublicationAsync(connectionFailurePublished.Task)).IsTrue();
        await TUnitAssert.That(
            await DriveTicksUntilPublicationAsync(handleAds.CreateHandleAttempted.Task, handlePlatform.Ticks)).IsTrue();
        await TUnitAssert.That(await WaitForPublicationAsync(handleFailurePublished.Task)).IsTrue();

        await TUnitAssert.That(ContainsError(connectionErrors, ConnectFailureMessage)).IsTrue();
        await TUnitAssert.That(ContainsError(handleErrors, HandleFailureMessage)).IsTrue();
        await TUnitAssert.That(handleClient.Connected).IsFalse();
    }

    /// <summary>Verifies composed Windows service monitoring and successful PLC startup.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if !NETFRAMEWORK
    [SupportedOSPlatform("windows")]
#endif
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("Exercises production initialization and dynamic PLC type resolution.")]
    [RequiresUnreferencedCode("Exercises production initialization and dynamic PLC type resolution.")]
#endif
    public async Task Composed_Runtime_Monitors_Service_And_Starts_PlcAsync()
    {
        var ads = new FakeAdsClient
        {
            Port = TwinCat3Port,
            State = new(AdsState.Stop, StoppedDeviceState),
        };
        using var platform = new FakePlatform(ads)
        {
            IsWindowsServiceMonitoringSupported = true,
        };
        using var client = new RxTcAdsClient(TimeProvider.System, platform);
        var errors = new List<Exception>();
        using var subscription = LeanBridge.SubscribeTo(client.ErrorReceived, errors.Add);
        var ignoredService = new FakeObservableServiceController("OtherService", ServiceControllerStatus.Running);
        var twinCatService = new FakeObservableServiceController("TcSysSrv", ServiceControllerStatus.Running);

        client.Connect(new Settings { Port = TwinCat3Port });
        platform.Services.Emit(ignoredService);
        platform.Services.Emit(twinCatService);
        platform.Ticks.Emit(0);
        platform.Ticks.Emit(1);
        var initializedBeforeServiceFault = client.Connected;
        twinCatService.EmitStatus(ServiceControllerStatus.Stopped);

        await TUnitAssert.That(ads.ControlWriteCount).IsEqualTo(1);
        await TUnitAssert.That(ads.State.AdsState).IsEqualTo(AdsState.Run);
        await TUnitAssert.That(initializedBeforeServiceFault).IsTrue();
        await TUnitAssert.That(client.Connected).IsFalse();
        await TUnitAssert.That(ignoredService.StartCount).IsEqualTo(0);
        await TUnitAssert.That(twinCatService.StartCount).IsEqualTo(1);
        await TUnitAssert.That(ContainsError(errors, "Service Fault")).IsTrue();
    }

    /// <summary>Verifies null collections and write-handle failures remain deterministic.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("Exercises production initialization and dynamic PLC type resolution.")]
    [RequiresUnreferencedCode("Exercises production initialization and dynamic PLC type resolution.")]
#endif
    public async Task Composed_Runtime_Covers_Null_Collections_And_Write_Handle_FailureAsync()
    {
        var nullAds = new FakeAdsClient { Port = TwinCat3Port };
        using var nullPlatform = new FakePlatform(nullAds);
        using var nullClient = new RxTcAdsClient(TimeProvider.System, nullPlatform);
        nullClient.Connect(new NullCollectionSettings { Port = TwinCat3Port });
        nullPlatform.Ticks.Emit(0);

        var writeAds = new FakeAdsClient
        {
            Port = TwinCat3Port,
            CreateHandleError = new IOException("write handle failed"),
        };
        using var writePlatform = new FakePlatform(writeAds);
        writePlatform.AddSymbol(ValueSymbolName, "DINT", DataTypeCategory.Primitive);
        var settings = new Settings { Port = TwinCat3Port };
        settings.WriteVariables.Add(new WriteVariable(ValueVariable));
        using var writeClient = new RxTcAdsClient(TimeProvider.System, writePlatform);
        var errors = new List<Exception>();
        using var subscription = LeanBridge.SubscribeTo(writeClient.ErrorReceived, errors.Add);
        writeClient.Connect(settings);
        writePlatform.Ticks.Emit(0);

        await TUnitAssert.That(nullClient.Connected).IsTrue();
        await TUnitAssert.That(ContainsError(errors, "write handle failed")).IsTrue();
        await TUnitAssert.That(writeClient.Connected).IsFalse();
    }

    /// <summary>Verifies stale generated data-type files are removed before variable registration.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("Exercises production initialization and dynamic PLC type resolution.")]
    [RequiresUnreferencedCode("Exercises production initialization and dynamic PLC type resolution.")]
#endif
    public async Task Composed_Runtime_Removes_Stale_Generated_Data_Type_FileAsync()
    {
        var variableName = $".Value_Stale_{Guid.NewGuid():N}";
        var symbolName = variableName[1..];
        var staleFile = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            $"PLC_{symbolName}{Guid.NewGuid():N}.dll");
#if NETFRAMEWORK
        using (var writer = File.CreateText(staleFile))
        {
            await writer.WriteAsync("stale");
        }
#else
        await using (var writer = File.CreateText(staleFile))
        {
            await writer.WriteAsync("stale");
        }
#endif

        try
        {
            var ads = new FakeAdsClient { Port = TwinCat3Port };
            using var platform = new FakePlatform(ads);
            platform.AddSymbol(symbolName, "DINT", DataTypeCategory.Primitive);
            var settings = new Settings { Port = TwinCat3Port };
            settings.Notifications.Add(new RxNotification(UpdateRate, variableName));
            using var client = new RxTcAdsClient(TimeProvider.System, platform);

            client.Connect(settings);
            platform.Ticks.Emit(0);

            await TUnitAssert.That(File.Exists(staleFile)).IsFalse();
        }
        finally
        {
            File.Delete(staleFile);
        }
    }

    /// <summary>Creates an asynchronously continued reactive-publication completion source.</summary>
    /// <returns>The new completion source.</returns>
    private static TaskCompletionSource<bool> CreatePublicationSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Verifies a queued read reaches the native runtime and publishes its configured failure.</summary>
    /// <param name="client">The composed client.</param>
    /// <param name="ads">The deterministic native runtime.</param>
    /// <param name="failurePublication">The expected error publication.</param>
    /// <returns>A task that completes after the native attempt and error publication.</returns>
    private static async Task AssertNativeReadFailureAsync(
        RxTcAdsClient client,
        FakeAdsClient ads,
        Task<bool> failurePublication)
    {
        await TUnitAssert.That(client.Connected).IsTrue();
        await TUnitAssert.That(client.ReadWriteHandleInfo.ContainsKey(ValueVariable)).IsTrue();
        ads.ReadAnyError = new IOException(ReadFailureMessage);
        client.Read(ValueVariable, "read");
        await TUnitAssert.That(await WaitForPublicationAsync(ads.ReadAttempted.Task)).IsTrue();
        await TUnitAssert.That(await WaitForPublicationAsync(failurePublication)).IsTrue();
        ads.ReadAnyError = null;
    }

    /// <summary>Completes a publication barrier after accepting its observed value.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="publication">The publication barrier.</param>
    /// <param name="value">The observed value.</param>
    private static void CompletePublication<T>(TaskCompletionSource<bool> publication, T value)
    {
        _ = value;
        _ = publication.TrySetResult(true);
    }

    /// <summary>Determines whether an observed error contains the supplied message fragment.</summary>
    /// <param name="errors">The observed errors.</param>
    /// <param name="messageFragment">The message fragment to locate.</param>
    /// <returns><see langword="true"/> when a matching error was observed.</returns>
    private static bool ContainsError(IEnumerable<Exception> errors, string messageFragment)
    {
        foreach (var error in errors)
        {
            if (error.Message.Contains(messageFragment))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether an observed write contains the supplied message fragment.</summary>
    /// <param name="writes">The observed writes.</param>
    /// <param name="messageFragment">The message fragment to locate.</param>
    /// <returns><see langword="true"/> when a matching write was observed.</returns>
    private static bool ContainsWrite(IEnumerable<string?> writes, string messageFragment)
    {
        foreach (var write in writes)
        {
            if (write?.Contains(messageFragment) == true)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Drives the deterministic interval until the expected reactive publication arrives.</summary>
    /// <param name="publication">The expected publication task.</param>
    /// <param name="ticks">The deterministic interval source.</param>
    /// <returns>Whether the publication completed before the timeout.</returns>
    private static async Task<bool> DriveTicksUntilPublicationAsync(
        Task<bool> publication,
        ManualObservable<long> ticks)
    {
        var timeout = Task.Delay(PublicationTimeout);
        var tick = 0L;
        while (!publication.IsCompleted && !timeout.IsCompleted)
        {
            ticks.Emit(tick);
            tick++;
            _ = await Task.WhenAny(Task.Delay(TimeSpan.FromMilliseconds(1)), timeout);
        }

        return publication.Status == TaskStatus.RanToCompletion
            && await publication.ConfigureAwait(false);
    }

    /// <summary>Records an error and completes a matching error publication.</summary>
    /// <param name="errors">The observed errors.</param>
    /// <param name="publication">The expected publication.</param>
    /// <param name="expectedMessage">The expected error message fragment.</param>
    /// <param name="error">The published error.</param>
    private static void RecordErrorPublication(
        ConcurrentQueue<Exception> errors,
        TaskCompletionSource<bool> publication,
        string expectedMessage,
        Exception error)
    {
        errors.Enqueue(error);
        if (!error.Message.Contains(expectedMessage))
        {
            return;
        }

        _ = publication.TrySetResult(true);
    }

    /// <summary>Records a write result and completes a matching write-failure publication.</summary>
    /// <param name="writes">The observed write results.</param>
    /// <param name="publication">The expected publication.</param>
    /// <param name="write">The published write result.</param>
    private static void RecordWritePublication(
        ConcurrentQueue<string?> writes,
        TaskCompletionSource<bool> publication,
        string? write)
    {
        writes.Enqueue(write);
        if (write?.Contains(WriteFailureMessage) != true)
        {
            return;
        }

        _ = publication.TrySetResult(true);
    }

    /// <summary>Waits for a queued reactive publication without polling shared state.</summary>
    /// <param name="publication">The publication task.</param>
    /// <returns>Whether the publication completed before the timeout.</returns>
    private static async Task<bool> WaitForPublicationAsync(Task<bool> publication)
    {
        Task completed = await Task.WhenAny(publication, Task.Delay(PublicationTimeout));
        return ReferenceEquals(completed, publication) && await publication;
    }

    /// <summary>Deterministic ADS runtime.</summary>
    private sealed class FakeAdsClient : IAdsClientRuntime
    {
        /// <summary>Maps variable names to deterministic handles.</summary>
        private readonly Dictionary<string, uint> _handles = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Maps handles back to variable names.</summary>
        private readonly Dictionary<uint, string> _variables = [];

        /// <summary>Stores the next handle.</summary>
        private uint _nextHandle = 1;

        /// <inheritdoc/>
        public bool IsConnected { get; private set; }

        /// <inheritdoc/>
        public int? Port { get; set; }

        /// <summary>Gets or sets the current state.</summary>
        public StateInfo State { get; set; } = new(AdsState.Run, 0);

        /// <summary>Gets configured values by variable.</summary>
        public Dictionary<string, object> ValuesByVariable { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Gets the local connection count.</summary>
        public int LocalConnectCount { get; private set; }

        /// <summary>Gets the remote connection count.</summary>
        public int RemoteConnectCount { get; private set; }

        /// <summary>Gets the control-write count.</summary>
        public int ControlWriteCount { get; private set; }

        /// <summary>Gets the signal for the first attempted native handle creation.</summary>
        public TaskCompletionSource<bool> CreateHandleAttempted { get; } = CreatePublicationSource();

        /// <summary>Gets the signal for the first attempted native read.</summary>
        public TaskCompletionSource<bool> ReadAttempted { get; } = CreatePublicationSource();

        /// <summary>Gets or sets a connection error.</summary>
        public Exception? ConnectError
        {
            get => Volatile.Read(ref field);
            set => Volatile.Write(ref field, value);
        }

        /// <summary>Gets or sets a handle creation error.</summary>
        public Exception? CreateHandleError
        {
            get => Volatile.Read(ref field);
            set => Volatile.Write(ref field, value);
        }

        /// <summary>Gets or sets a read error.</summary>
        public Exception? ReadAnyError
        {
            get => Volatile.Read(ref field);
            set => Volatile.Write(ref field, value);
        }

        /// <summary>Gets or sets a state-read error.</summary>
        public Exception? ReadStateError
        {
            get => Volatile.Read(ref field);
            set => Volatile.Write(ref field, value);
        }

        /// <summary>Gets or sets a write error.</summary>
        public Exception? WriteAnyError
        {
            get => Volatile.Read(ref field);
            set => Volatile.Write(ref field, value);
        }

        /// <summary>Gets or sets a control-write error.</summary>
        public Exception? WriteControlError
        {
            get => Volatile.Read(ref field);
            set => Volatile.Write(ref field, value);
        }

        /// <inheritdoc/>
        public void Connect(int port)
        {
            ThrowIfConfigured(ConnectError);
            LocalConnectCount++;
            Port = port;
            IsConnected = true;
        }

        /// <inheritdoc/>
        public void Connect(string adsAddress, int port)
        {
            _ = adsAddress;
            ThrowIfConfigured(ConnectError);
            RemoteConnectCount++;
            Port = port;
            IsConnected = true;
        }

        /// <inheritdoc/>
        public uint CreateVariableHandle(string variable)
        {
            _ = CreateHandleAttempted.TrySetResult(true);
            ThrowIfConfigured(CreateHandleError);
            if (_handles.TryGetValue(variable, out var existing))
            {
                return existing;
            }

            var handle = _nextHandle++;
            _handles.Add(variable, handle);
            _variables.Add(handle, variable);
            return handle;
        }

        /// <inheritdoc/>
        public void Dispose() => IsConnected = false;

        /// <inheritdoc/>
        public object ReadAny(uint handle, Type type)
        {
            _ = type;
            _ = ReadAttempted.TrySetResult(true);
            ThrowIfConfigured(ReadAnyError);
            return ValuesByVariable[_variables[handle]];
        }

        /// <inheritdoc/>
        public object ReadAny(uint handle, Type type, int[] lengths)
        {
            _ = lengths;
            return ReadAny(handle, type);
        }

        /// <inheritdoc/>
        public StateInfo ReadState()
        {
            ThrowIfConfigured(ReadStateError);
            return State;
        }

        /// <inheritdoc/>
        public void WriteAny(uint handle, object value)
        {
            ThrowIfConfigured(WriteAnyError);
            ValuesByVariable[_variables[handle]] = value;
        }

        /// <inheritdoc/>
        public void WriteControl(StateInfo state)
        {
            ThrowIfConfigured(WriteControlError);
            ControlWriteCount++;
            State = state;
        }

        /// <summary>Throws a configured exception.</summary>
        /// <param name="error">The configured exception.</param>
        private static void ThrowIfConfigured(Exception? error)
        {
            if (error is null)
            {
                return;
            }

            throw error;
        }
    }

    /// <summary>Deterministic platform dependencies.</summary>
    /// <remarks>Initializes a new instance of the <see cref="FakePlatform"/> class.</remarks>
    /// <param name="adsClient">The deterministic ADS runtime.</param>
    private sealed class FakePlatform(FakeAdsClient adsClient) : IRxTcAdsPlatform, IDisposable
    {
        /// <summary>Stores the deterministic generator.</summary>
        private readonly CodeGenerator _generator = new();

        /// <summary>Gets the shared manual tick sequence.</summary>
        public ManualObservable<long> Ticks { get; } = new();

        /// <summary>Gets the shared manual service sequence.</summary>
        public ManualObservable<IObservableServiceController> Services { get; } = new();

        /// <summary>Gets the number of symbol-load requests.</summary>
        public int LoadSymbolsCount { get; private set; }

        /// <inheritdoc/>
        public bool IsWindowsServiceMonitoringSupported { get; set; }

        /// <summary>Adds one deterministic symbol to the generator.</summary>
        /// <param name="name">The symbol name without a leading dot.</param>
        /// <param name="typeName">The PLC type name.</param>
        /// <param name="category">The symbol category.</param>
        public void AddSymbol(string name, string typeName, DataTypeCategory category)
        {
            var node = new NodeEmulator
            {
                Text = name,
                Tag = new FakeSymbol(name, typeName, category),
            };
            _ = _generator.SymbolList.Add(node);
        }

        /// <inheritdoc/>
        public IAdsClientRuntime CreateAdsClient() => adsClient;

        /// <inheritdoc/>
        public ICodeGenerator CreateCodeGenerator() => _generator;

        /// <inheritdoc/>
        public void Dispose() => _generator.Dispose();

        /// <inheritdoc/>
        public IObservable<long> Interval(TimeSpan period)
        {
            _ = period;
            return Ticks;
        }

        /// <inheritdoc/>
        public void LoadSymbols(ICodeGenerator codeGenerator, string adsAddress, int port)
        {
            _ = codeGenerator;
            _ = adsAddress;
            _ = port;
            LoadSymbolsCount++;
        }

        IObservable<IObservableServiceController> IRxTcAdsPlatform.GetServices() => Services;
    }

    /// <summary>Settings that deliberately expose null optional collections.</summary>
    private sealed class NullCollectionSettings : ISettings
    {
        /// <inheritdoc/>
        public string AdsAddress { get; set; } = string.Empty;

        /// <inheritdoc/>
        public int Port { get; set; }

        /// <inheritdoc/>
        public IList<CoreNotificationContract> Notifications => null!;

        /// <inheritdoc/>
        public string? SettingsId { get; set; }

        /// <inheritdoc/>
        public IList<IWriteVariable> WriteVariables => null!;

        /// <inheritdoc/>
        public T Defaults<T>(T defaultSettings)
            where T : ISettings, new() =>
            defaultSettings;
    }

    /// <summary>Deterministic observable service used by connection monitoring.</summary>
    /// <remarks>Initializes a new instance of the <see cref="FakeObservableServiceController"/> class.</remarks>
    /// <param name="serviceName">The service name.</param>
    /// <param name="status">The initial status.</param>
#if !NETFRAMEWORK
    [SupportedOSPlatform("windows")]
#endif
    private sealed class FakeObservableServiceController(
        string serviceName,
        ServiceControllerStatus status) : IObservableServiceController
    {
        /// <summary>Stores status notifications.</summary>
        private readonly ManualObservable<ServiceControllerStatus> _statuses = new();

        /// <inheritdoc/>
        public bool CanStop => true;

        /// <inheritdoc/>
        public string DisplayName => serviceName;

        /// <inheritdoc/>
        public string ServiceName => serviceName;

        /// <inheritdoc/>
        public ServiceControllerStatus Status { get; private set; } = status;

        /// <inheritdoc/>
        public IObservable<ServiceControllerStatus> StatusObserver => _statuses;

        /// <summary>Gets the start count.</summary>
        public int StartCount { get; private set; }

        /// <inheritdoc/>
        public void Dispose()
        {
        }

        /// <summary>Emits one service status.</summary>
        /// <param name="value">The status.</param>
        public void EmitStatus(ServiceControllerStatus value)
        {
            Status = value;
            _statuses.Emit(value);
        }

        /// <inheritdoc/>
        public void Restart() => Start();

        /// <inheritdoc/>
        public void Start()
        {
            StartCount++;
            Status = ServiceControllerStatus.Running;
        }

        /// <inheritdoc/>
        public void Stop() => Status = ServiceControllerStatus.Stopped;
    }

    /// <summary>Manually triggered observable sequence.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    private sealed class ManualObservable<T> : IObservable<T>
    {
        /// <summary>Stores current observers.</summary>
        private readonly List<IObserver<T>> _observers = [];

        /// <summary>Emits one value.</summary>
        /// <param name="value">The value.</param>
        public void Emit(T value)
        {
            foreach (var observer in _observers.ToArray())
            {
                observer.OnNext(value);
            }
        }

        /// <inheritdoc/>
        public IDisposable Subscribe(IObserver<T> observer)
        {
            _observers.Add(observer);
            return new Subscription(_observers, observer);
        }

        /// <summary>Removes one observer.</summary>
        /// <remarks>Initializes a new instance of the <see cref="Subscription"/> class.</remarks>
        /// <param name="observers">The observer collection.</param>
        /// <param name="observer">The observer to remove.</param>
        private sealed class Subscription(List<IObserver<T>> observers, IObserver<T> observer) : IDisposable
        {
            /// <inheritdoc/>
            public void Dispose() => _ = observers.Remove(observer);
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
        /// <param name="category">The category.</param>
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
        public bool IsContainerType => false;

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
        public bool Contains(string instancePath)
        {
            foreach (var symbol in this)
            {
                if (symbol.InstancePath == instancePath)
                {
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc/>
        public bool ContainsName(string instanceName)
        {
            foreach (var symbol in this)
            {
                if (symbol.InstanceName == instanceName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc/>
        public ISymbol GetInstance(string instancePath)
        {
            foreach (var symbol in this)
            {
                if (symbol.InstancePath == instancePath)
                {
                    return symbol;
                }
            }

            throw new KeyNotFoundException($"No symbol has instance path '{instancePath}'.");
        }

        /// <inheritdoc/>
        public IList<ISymbol> GetInstanceByName(string instanceName)
        {
            var symbols = new List<ISymbol>();
            foreach (var symbol in this)
            {
                if (symbol.InstanceName == instanceName)
                {
                    symbols.Add(symbol);
                }
            }

            return symbols;
        }

        /// <inheritdoc/>
#if NETFRAMEWORK
        public bool TryGetInstance(string instancePath, out ISymbol symbol)
#else
        public bool TryGetInstance(string instancePath, [NotNullWhen(true)] out ISymbol? symbol)
#endif
        {
            foreach (var candidate in this)
            {
                if (candidate.InstancePath == instancePath)
                {
                    symbol = candidate;
                    return true;
                }
            }

#if NETFRAMEWORK
            symbol = null!;
#else
            symbol = null;
#endif
            return false;
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
