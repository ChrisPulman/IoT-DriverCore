// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if !NETFRAMEWORK
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
#endif
using System.ServiceProcess;
using System.Text;
using IoT.DriverCore.TwinCATRx.Core;
using TwinCAT.Ads;
using TwinCAT.TypeSystem;
using CoreNotificationContract = IoT.DriverCore.TwinCATRx.Core.INotification;
using LeanBridge = IoT.DriverCore.TwinCATRx.ObservableBridgeExtensions;
using PublicationTimeoutException = System.TimeoutException;
using RxNotification = IoT.DriverCore.TwinCATRx.Core.Notification;

namespace IoT.DriverCore.TwinCATRx.Tests.Rx;

/// <summary>Exercises the production ADS client through deterministic composed dependencies.</summary>
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

    /// <summary>The maximum number of queued-publication polls.</summary>
    private const int PublicationAttemptCount = 100;

    /// <summary>The delay between queued-publication polls.</summary>
    private const int PublicationPollDelayMilliseconds = 10;

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

    /// <summary>A deterministic write failure message.</summary>
    private const string WriteFailureMessage = "write failed";

    /// <summary>A deterministic write-only variable.</summary>
    private const string WriteOnlyVariable = ".WriteOnly";

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
        await TUnitAssert.That(initialized).Count().IsEqualTo(1);
        await TUnitAssert.That(client.ReadWriteHandleInfo).Count().IsEqualTo(ExpectedNotificationHandleCount);
        await TUnitAssert.That(client.WriteHandleInfo).Count().IsEqualTo(1);
        await TUnitAssert.That(
            data.Any(item => item.Variable == ScalarVariable && Equals(item.Data, ScalarPayload))).IsTrue();
        await TUnitAssert.That(data.Any(item => item.Id == "text" && Equals(item.Data, "hello"))).IsTrue();
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
        var errors = new List<Exception>();
        var writes = new List<string?>();
        using var errorSubscription = LeanBridge.SubscribeTo(client.ErrorReceived, errors.Add);
        using var writeSubscription = LeanBridge.SubscribeTo(client.OnWrite, writes.Add);

        client.Connect(settings);
        platform.Ticks.Emit(0);
        ads.ReadAnyError = new IOException(ReadFailureMessage);
        client.Read(ValueVariable, "read");
        await WaitUntilAsync(() => errors.Any(error => error.Message.Contains(ReadFailureMessage)));
        ads.ReadAnyError = null;
        ads.WriteAnyError = new IOException(WriteFailureMessage);
        client.Write(ValueVariable, ExpectedNotificationHandleCount, "write");
        await WaitUntilAsync(() => writes.Any(write => write?.Contains(WriteFailureMessage) == true));
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
        using var stateSubscription = LeanBridge.SubscribeTo(stateClient.ErrorReceived, errors.Add);
        stateClient.Connect(new Settings { Port = TwinCat3Port });
        statePlatform.Ticks.Emit(ExpectedNotificationHandleCount);

        await TUnitAssert.That(ads.LocalConnectCount).IsEqualTo(1);
        await TUnitAssert.That(errors.Any(error => error.Message.Contains(ReadFailureMessage))).IsTrue();
        await TUnitAssert.That(errors.Any(error => error.Message.Contains(WriteFailureMessage))).IsTrue();
        await TUnitAssert.That(errors.Any(error => error.Message.Contains("Ads Fault"))).IsTrue();
        await TUnitAssert.That(errors.Any(error => error.Message.Contains("control failed"))).IsTrue();
        await TUnitAssert.That(writes.Any(write => write?.Contains(WriteFailureMessage) == true)).IsTrue();
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
        await TUnitAssert.That(errors.Any(error => error.Message.Contains("String length"))).IsTrue();
        await TUnitAssert.That(errors.Any(error => error.Message.Contains("Array length"))).IsTrue();
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
        var connectionAds = new FakeAdsClient { ConnectError = new IOException("connect failed") };
        using var connectionPlatform = new FakePlatform(connectionAds);
        using var connectionClient = new RxTcAdsClient(TimeProvider.System, connectionPlatform);
        var connectionErrors = new List<Exception>();
        using var connectionSubscription = LeanBridge.SubscribeTo(
            connectionClient.ErrorReceived,
            connectionErrors.Add);
        connectionClient.Connect(new Settings());

        var handleAds = new FakeAdsClient
        {
            Port = TwinCat3Port,
            CreateHandleError = new IOException("handle failed"),
        };
        using var handlePlatform = new FakePlatform(handleAds);
        handlePlatform.AddSymbol(ValueSymbolName, "DINT", DataTypeCategory.Primitive);
        var handleSettings = new Settings { Port = TwinCat3Port };
        handleSettings.Notifications.Add(new RxNotification(UpdateRate, ValueVariable));
        using var handleClient = new RxTcAdsClient(TimeProvider.System, handlePlatform);
        var handleErrors = new List<Exception>();
        using var handleSubscription = LeanBridge.SubscribeTo(handleClient.ErrorReceived, handleErrors.Add);
        handleClient.Connect(handleSettings);
        handlePlatform.Ticks.Emit(0);

        await TUnitAssert.That(connectionErrors.Any(error => error.Message.Contains("connect failed"))).IsTrue();
        await TUnitAssert.That(handleErrors.Any(error => error.Message.Contains("handle failed"))).IsTrue();
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
        await TUnitAssert.That(errors.Any(error => error.Message.Contains("Service Fault"))).IsTrue();
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
        await TUnitAssert.That(errors.Any(error => error.Message.Contains("write handle failed"))).IsTrue();
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
        var staleFile = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            $"PLC_Value_Coverage_{Guid.NewGuid():N}.tmp");
        File.WriteAllText(staleFile, "stale");
        try
        {
            var ads = new FakeAdsClient { Port = TwinCat3Port };
            using var platform = new FakePlatform(ads);
            platform.AddSymbol(ValueSymbolName, "DINT", DataTypeCategory.Primitive);
            var settings = new Settings { Port = TwinCat3Port };
            settings.Notifications.Add(new RxNotification(UpdateRate, ValueVariable));
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

    /// <summary>Waits for a queued reactive action to publish its deterministic result.</summary>
    /// <param name="condition">The completion condition.</param>
    /// <returns>The wait task.</returns>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < PublicationAttemptCount; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(PublicationPollDelayMilliseconds);
        }

        throw new PublicationTimeoutException("The expected reactive publication was not observed.");
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

        /// <summary>Gets or sets a connection error.</summary>
        public Exception? ConnectError { get; set; }

        /// <summary>Gets or sets a handle creation error.</summary>
        public Exception? CreateHandleError { get; set; }

        /// <summary>Gets or sets a read error.</summary>
        public Exception? ReadAnyError { get; set; }

        /// <summary>Gets or sets a state-read error.</summary>
        public Exception? ReadStateError { get; set; }

        /// <summary>Gets or sets a write error.</summary>
        public Exception? WriteAnyError { get; set; }

        /// <summary>Gets or sets a control-write error.</summary>
        public Exception? WriteControlError { get; set; }

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
