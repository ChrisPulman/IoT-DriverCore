// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET9_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
#if !NETFRAMEWORK
using System.Runtime.Versioning;
#endif
using System.Reflection;
using IoT.DriverCore.TwinCATRx.Core;
using TwinCAT.Ads;
using RxNotification = IoT.DriverCore.TwinCATRx.Core.Notification;

namespace IoT.DriverCore.TwinCATRx.Tests.Rx;

/// <summary>Exercises residual deterministic native-value and variable-registration client branches.</summary>
public sealed class NativeClientVariableResidualCoverageTests
{
    /// <summary>A deterministic array length.</summary>
    private const int ArrayLength = 3;

    /// <summary>The expected scalar read count.</summary>
    private const int ExpectedScalarReadCount = 2;

    /// <summary>The private native-value read method name.</summary>
    private const string ReadNativeValueMethod = "ReadNativeValue";

    /// <summary>The private connection method name.</summary>
    private const string ConnectAdsClientMethod = "ConnectAdsClient";

    /// <summary>The private initialization-completion method name.</summary>
    private const string CompleteInitializationMethod = "CompleteInitialization";

    /// <summary>The private write-monitor method name.</summary>
    private const string MonitorWritesMethod = "MonitorWrites";

    /// <summary>The private notification-read method name.</summary>
    private const string ReadNotificationMethod = "ReadNotification";

    /// <summary>The private connection-reset method name.</summary>
    private const string ResetConnectionStateMethod = "ResetConnectionState";

    /// <summary>The private service-status publication method name.</summary>
    private const string PublishServiceStatusMethod = "PublishServiceStatus";

    /// <summary>The private notification-scheduling method name.</summary>
    private const string ScheduleNotificationsMethod = "ScheduleNotifications";

    /// <summary>The private client type-map field name.</summary>
    private const string TypeInfoField = "_typeInfo";

    /// <summary>A deterministic ADS handle.</summary>
    private const uint Handle = 19;

    /// <summary>A deterministic notification period.</summary>
    private const int UpdateRate = 25;

    /// <summary>A deterministic array variable.</summary>
    private const string ArrayVariable = ".Array";

    /// <summary>A deterministic scalar variable.</summary>
    private const string ScalarVariable = ".Scalar";

    /// <summary>A deterministic string variable.</summary>
    private const string StringVariable = ".String";

    /// <summary>Verifies native scalar, array, string, and null-handle reads choose the correct adapter overload.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Native_Value_Read_Uses_Correct_Runtime_OverloadsAsync()
    {
        var runtime = new RecordingAdsClientRuntime();

        var nullValue = InvokeStatic(ReadNativeValueMethod, runtime, null, typeof(int), -1);
        var scalarValue = InvokeStatic(ReadNativeValueMethod, runtime, Handle, typeof(int), -1);
        var zeroLengthArrayValue = InvokeStatic(ReadNativeValueMethod, runtime, Handle, typeof(int[]), 0);
        var arrayValue = InvokeStatic(ReadNativeValueMethod, runtime, Handle, typeof(int[]), ArrayLength);
        var stringValue = InvokeStatic(ReadNativeValueMethod, runtime, Handle, typeof(string), ArrayLength);

        await TUnitAssert.That(nullValue).IsNull();
        await TUnitAssert.That(scalarValue).IsEqualTo(RecordingAdsClientRuntime.ScalarValue);
        await TUnitAssert.That(zeroLengthArrayValue).IsEqualTo(RecordingAdsClientRuntime.ScalarValue);
        await TUnitAssert.That(arrayValue).IsEqualTo(RecordingAdsClientRuntime.ArrayValue);
        await TUnitAssert.That(stringValue).IsEqualTo(RecordingAdsClientRuntime.ArrayValue);
        await TUnitAssert.That(runtime.ScalarReadCount).IsEqualTo(ExpectedScalarReadCount);
        await TUnitAssert.That(runtime.ArrayReadCount).IsEqualTo(ExpectedScalarReadCount);
    }

    /// <summary>Verifies private PLC variable resolution branches through deterministic settings and handle maps.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("Exercises private PLC variable resolution branches through reflection.")]
    [RequiresUnreferencedCode("Exercises private PLC variable resolution branches through reflection.")]
#endif
    public async Task Variable_Resolution_Covers_Prefixes_Notifications_And_Read_TargetsAsync()
    {
        using var client = new RxTcAdsClient();
        var settings = new Settings();
        settings.Notifications.Add(new RxNotification(UpdateRate, ArrayVariable, ArrayLength));
        SetSettings(client, settings);
        var types = GetField<IDictionary<string, Type>>(client, TypeInfoField);
        types[ScalarVariable] = typeof(int);
        types[ArrayVariable] = typeof(int[]);
        types[StringVariable] = typeof(string);
        client.ReadWriteHandleInfo[ScalarVariable] = Handle;
        client.ReadWriteHandleInfo[ArrayVariable] = Handle;
        client.ReadWriteHandleInfo[StringVariable] = Handle;

        var dottedPrefix = InvokeStatic("BuildDataTypesFileName", ScalarVariable);
        var plainPrefix = InvokeStatic("BuildDataTypesFileName", "Scalar");
        var matchingLength = InvokeInstance(client, "FindNotificationArrayLength", ArrayVariable);
        var missingLength = InvokeInstance(client, "FindNotificationArrayLength", ".Missing");
        var scalar = InvokeReadTarget(client, ScalarVariable, null);
        var registeredArray = InvokeReadTarget(client, ArrayVariable, null);
        var suppliedStringLength = InvokeReadTarget(client, StringVariable, ArrayLength);
        var blank = InvokeReadTarget(client, string.Empty, null);
        _ = client.ReadWriteHandleInfo.Remove(StringVariable);
        var missingHandle = InvokeReadTarget(client, StringVariable, ArrayLength);
        client.ReadWriteHandleInfo[StringVariable] = Handle;
        settings.Notifications.Clear();
        await TUnitAssert.That(() => InvokeReadTarget(client, StringVariable, null))
            .Throws<TargetInvocationException>();

        await TUnitAssert.That(dottedPrefix).IsEqualTo("PLC_Scalar");
        await TUnitAssert.That(plainPrefix).IsEqualTo("PLC_Scalar");
        await TUnitAssert.That(matchingLength).IsEqualTo(ArrayLength);
        await TUnitAssert.That(missingLength).IsEqualTo(-1);
        await TUnitAssert.That(scalar.Resolved).IsTrue();
        await TUnitAssert.That(scalar.Length).IsEqualTo(-1);
        await TUnitAssert.That(registeredArray.Resolved).IsTrue();
        await TUnitAssert.That(registeredArray.Length).IsEqualTo(ArrayLength);
        await TUnitAssert.That(suppliedStringLength.Resolved).IsTrue();
        await TUnitAssert.That(suppliedStringLength.Length).IsEqualTo(ArrayLength);
        await TUnitAssert.That(blank.Resolved).IsFalse();
        await TUnitAssert.That(missingHandle.Resolved).IsFalse();
    }

    /// <summary>Verifies empty registration, primitive conversion, and read-notification guard branches without ADS hardware.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("Exercises private PLC variable registration branches through reflection.")]
    [RequiresUnreferencedCode("Exercises private PLC variable registration branches through reflection.")]
#endif
    public async Task Variable_Registration_And_Notification_Guards_Are_DeterministicAsync()
    {
        using var client = new RxTcAdsClient();
        var runtime = new RecordingAdsClientRuntime();
        var settings = new Settings();
        SetSettings(client, settings);
        var errors = new List<Exception>();
        using var errorSubscription = ObservableBridgeExtensions.SubscribeTo(client.ErrorReceived, errors.Add);
        _ = InvokeInstance(client, ResetConnectionStateMethod);
        _ = InvokeInstance(client, "CreateNotificationVariables", null, runtime);
        _ = InvokeInstance(client, "CreateWriteVariables", null, runtime);
        _ = InvokeInstance(client, "CreateNotificationVariable", new RxNotification(UpdateRate, string.Empty), runtime, false);
        _ = InvokeInstance(client, "CreateNotificationVariable", new RxNotification(UpdateRate, null), runtime, false);
        _ = InvokeInstance(client, "CreateWriteVariable", new WriteVariable(string.Empty), runtime, false);
        _ = InvokeInstance(client, "CreateWriteVariable", new WriteVariable(null), runtime, false);
        var knownType = InvokeStatic("TryResolvePlcType", "DINT", null);
        var unsupportedType = InvokeStatic("TryResolvePlcType", "POINTER TO DINT", null);
        var unresolvedGeneratedType = InvokeInstance(
            client,
            "ResolveNotificationType",
            ".Unknown",
            "missing.dll",
            "residual",
            false);

        _ = InvokeInstance(client, ReadNotificationMethod, runtime, new RxNotification(UpdateRate, null));
        _ = InvokeInstance(client, ReadNotificationMethod, runtime, new RxNotification(UpdateRate, ScalarVariable));
        runtime.IsConnectedValue = true;
        GetField<IDictionary<string, Type>>(client, TypeInfoField)[StringVariable] = typeof(string);
        client.ReadWriteHandleInfo[StringVariable] = Handle;
        _ = InvokeInstance(client, ReadNotificationMethod, runtime, new RxNotification(UpdateRate, StringVariable));
        _ = InvokeInstance(client, ReadNotificationMethod, runtime, new RxNotification(UpdateRate, StringVariable, ArrayLength));

        await TUnitAssert.That((bool)knownType!).IsTrue();
        await TUnitAssert.That((bool)unsupportedType!).IsFalse();
        await TUnitAssert.That(unresolvedGeneratedType).IsNull();
        await TUnitAssert.That(client.ReadWriteHandleInfo).Count().IsEqualTo(1);
        await TUnitAssert.That(client.WriteHandleInfo).IsEmpty();
        await TUnitAssert.That(errors).Count().IsEqualTo(1);
        await TUnitAssert.That(errors.Single().Message.Contains("String length")).IsTrue();
    }

    /// <summary>Verifies constructor, disposal, connection, and disconnect guards without starting an ADS operation.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("Exercises the client connection guard through its public entry point.")]
    [RequiresUnreferencedCode("Exercises the client connection guard through its public entry point.")]
#endif
    public async Task Client_Lifecycle_Guards_Are_DeterministicAsync()
    {
        using var platform = new MinimalPlatform(new RecordingAdsClientRuntime());
        await TUnitAssert.That(() => _ = new RxTcAdsClient(null!, platform)).Throws<ArgumentNullException>();
        await TUnitAssert.That(() => _ = new RxTcAdsClient(TimeProvider.System, null!)).Throws<ArgumentNullException>();

        using var guardedClient = new RxTcAdsClient();
        var errors = new List<Exception>();
        using var errorSubscription = ObservableBridgeExtensions.SubscribeTo(guardedClient.ErrorReceived, errors.Add);
        _ = InvokeInstance(guardedClient, ResetConnectionStateMethod);
        var cleanup = GetField<IDisposable>(guardedClient, "_cleanup");
        cleanup.Dispose();
        guardedClient.Connect(new Settings());

        using var disconnectClient = new RxTcAdsClient();
        var pauseStates = new List<bool>();
        using var pauseSubscription = ObservableBridgeExtensions.SubscribeTo(
            disconnectClient.IsPausedObservable,
            pauseStates.Add);
        using var plcCleanup = new CancellationTokenSource();
        SetField(disconnectClient, "_plcCleanup", plcCleanup);
        disconnectClient.Disconnect();
        SetProperty(disconnectClient, nameof(RxTcAdsClient.IsPaused), true);
        disconnectClient.Disconnect();

        using var nullCleanupClient = new RxTcAdsClient();
        nullCleanupClient.Dispose();
        using var disposableClient = new RxTcAdsClient();
        _ = InvokeInstance(disposableClient, ResetConnectionStateMethod);
        disposableClient.ReadWriteHandleInfo[ScalarVariable] = Handle;
        GetField<IDictionary<string, Type>>(disposableClient, TypeInfoField)[ScalarVariable] = typeof(int);
        disposableClient.Dispose();

        await TUnitAssert.That(errors.Single()).IsTypeOf<ObjectDisposedException>();
        await TUnitAssert.That(disconnectClient.IsPaused).IsFalse();
        await TUnitAssert.That(pauseStates).Contains(false);
        await TUnitAssert.That(disposableClient.ReadWriteHandleInfo).IsEmpty();
        await TUnitAssert.That(disposableClient.IsDisposed).IsTrue();
    }

    /// <summary>Verifies local and remote connection, initialization error, service state, write, and scheduling branches.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if !NETFRAMEWORK
    [SupportedOSPlatform("windows")]
#endif
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("Exercises private initialization and connection branches through reflection.")]
    [RequiresUnreferencedCode("Exercises private initialization and connection branches through reflection.")]
#endif
    public async Task Client_Initialization_Service_Write_And_Scheduling_Branches_Are_DeterministicAsync()
    {
        var runtime = new RecordingAdsClientRuntime { IsConnectedValue = true };
        using var platform = new MinimalPlatform(runtime);
        using var client = new RxTcAdsClient(TimeProvider.System, platform);
        using var generator = new CodeGenerator();
        var initializationObserver = new RecordingObserver<Unit>();
        SetSettings(client, new Settings { AdsAddress = string.Empty, Port = UpdateRate });
        _ = InvokeInstance(client, ConnectAdsClientMethod, runtime, generator, initializationObserver);
        SetSettings(client, new Settings { AdsAddress = "1.2.3.4.5.6", Port = UpdateRate });
        _ = InvokeInstance(client, ConnectAdsClientMethod, runtime, generator, initializationObserver);
        SetSettings(client, null!);
        _ = InvokeInstance(client, CompleteInitializationMethod, runtime, initializationObserver);

        _ = InvokeInstance(client, ResetConnectionStateMethod);
        var serviceStatuses = new List<ServiceStatus>();
        using var serviceSubscription = ObservableBridgeExtensions.SubscribeTo(
            GetField<IObservable<ServiceStatus>>(client, "_serviceStatus"),
            serviceStatuses.Add);
        _ = InvokeInstance(client, PublishServiceStatusMethod, new Dictionary<string, System.ServiceProcess.ServiceControllerStatus>());
        _ = InvokeInstance(
            client,
            PublishServiceStatusMethod,
            new Dictionary<string, System.ServiceProcess.ServiceControllerStatus>
            {
                ["TcSysSrv"] = System.ServiceProcess.ServiceControllerStatus.Stopped,
            });

        SetField(client, "_initialized", true);
        _ = InvokeInstance(client, MonitorWritesMethod, runtime);
        client.ReadWriteHandleInfo[ScalarVariable] = Handle;
        var writes = new List<string?>();
        var errors = new List<Exception>();
        using var writeSubscription = ObservableBridgeExtensions.SubscribeTo(client.OnWrite, writes.Add);
        using var errorSubscription = ObservableBridgeExtensions.SubscribeTo(client.ErrorReceived, errors.Add);
        client.Write(ScalarVariable, RecordingAdsClientRuntime.ScalarValue);
        client.Write(ScalarVariable, RecordingAdsClientRuntime.ScalarValue, "correlated");
        runtime.WriteError = new IOException("write failure");
        client.Write(ScalarVariable, RecordingAdsClientRuntime.ScalarValue, "failure");

        SetSettings(client, null!);
        _ = InvokeInstance(client, ScheduleNotificationsMethod, runtime);
        var schedulingSettings = new Settings();
        schedulingSettings.Notifications.Add(new RxNotification(UpdateRate, ScalarVariable));
        SetSettings(client, schedulingSettings);
        _ = InvokeInstance(client, ScheduleNotificationsMethod, runtime);

        await TUnitAssert.That(runtime.LocalConnectCount).IsEqualTo(1);
        await TUnitAssert.That(runtime.RemoteConnectCount).IsEqualTo(1);
        await TUnitAssert.That(platform.LoadSymbolsCount).IsEqualTo(ExpectedScalarReadCount);
        await TUnitAssert.That(initializationObserver.Errors).Count().IsEqualTo(1);
        await TUnitAssert.That(serviceStatuses).Contains(ServiceStatus.Running);
        await TUnitAssert.That(serviceStatuses).Contains(ServiceStatus.Faulted);
        await TUnitAssert.That(writes).Contains("Success");
        await TUnitAssert.That(writes).Contains("Success,correlated");
        await TUnitAssert.That(errors.Single().Message).IsEqualTo("write failure");
        await TUnitAssert.That(platform.IntervalSubscriptionCount).IsEqualTo(1);
    }

    /// <summary>Verifies the native service runtime rejects a missing wrapped service deterministically.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if !NETFRAMEWORK
    [SupportedOSPlatform("windows")]
#endif
    public async Task Service_Runtime_Rejects_Null_Wrapped_ServiceAsync()
    {
        await TUnitAssert.That(() => _ = new ServiceControllerRuntime(null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>Invokes the private read-target resolver and captures its out values.</summary>
    /// <param name="client">The client under test.</param>
    /// <param name="variable">The requested PLC variable.</param>
    /// <param name="arrayLength">The optional supplied array length.</param>
    /// <returns>The resolved flag and selected length.</returns>
    private static (bool Resolved, int Length) InvokeReadTarget(
        RxTcAdsClient client,
        string variable,
        int? arrayLength)
    {
        object?[] arguments = [variable, arrayLength, null, null, -1];
        var result = InvokeInstance(client, "TryGetReadTarget", arguments);
        return ((bool)result!, (int)arguments[4]!);
    }

    /// <summary>Gets a private client field as the requested contract.</summary>
    /// <typeparam name="T">The field contract.</typeparam>
    /// <param name="instance">The containing instance.</param>
    /// <param name="name">The field name.</param>
    /// <returns>The field value.</returns>
    private static T GetField<T>(object instance, string name) =>
        (T)(instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance)
            ?? throw new MissingFieldException(instance.GetType().FullName, name));

    /// <summary>Sets a private client field to a deterministic test value.</summary>
    /// <param name="instance">The containing instance.</param>
    /// <param name="name">The field name.</param>
    /// <param name="value">The replacement value.</param>
    private static void SetField(object instance, string name, object value)
    {
        var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(instance.GetType().FullName, name);
        field.SetValue(instance, value);
    }

    /// <summary>Invokes a private instance method.</summary>
    /// <param name="instance">The containing instance.</param>
    /// <param name="name">The method name.</param>
    /// <param name="arguments">The method arguments.</param>
    /// <returns>The invocation result.</returns>
    private static object? InvokeInstance(object instance, string name, params object?[] arguments) =>
        GetMethod(instance.GetType(), name, BindingFlags.Instance).Invoke(instance, arguments);

    /// <summary>Invokes a private static client method.</summary>
    /// <param name="name">The method name.</param>
    /// <param name="arguments">The method arguments.</param>
    /// <returns>The invocation result.</returns>
    private static object? InvokeStatic(string name, params object?[] arguments) =>
        GetMethod(typeof(RxTcAdsClient), name, BindingFlags.Static).Invoke(null, arguments);

    /// <summary>Gets one private method with a descriptive failure when its implementation changes.</summary>
    /// <param name="type">The declaring type.</param>
    /// <param name="name">The private method name.</param>
    /// <param name="scope">The requested binding scope.</param>
    /// <returns>The method metadata.</returns>
    private static MethodInfo GetMethod(Type type, string name, BindingFlags scope) =>
        type.GetMethod(name, scope | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(type.FullName, name);

    /// <summary>Sets the public settings property whose setter is private to production callers.</summary>
    /// <param name="client">The client under test.</param>
    /// <param name="settings">The deterministic settings.</param>
    private static void SetSettings(RxTcAdsClient client, Settings settings)
    {
        SetProperty(client, nameof(RxTcAdsClient.Settings), settings);
    }

    /// <summary>Sets a public property whose setter is private to production callers.</summary>
    /// <param name="instance">The containing instance.</param>
    /// <param name="name">The property name.</param>
    /// <param name="value">The replacement value.</param>
    private static void SetProperty(object instance, string name, object? value)
    {
        var property = instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMemberException(instance.GetType().FullName, name);
        property.SetValue(instance, value);
    }

    /// <summary>Records native ADS calls while providing deterministic scalar and array responses.</summary>
    private sealed class RecordingAdsClientRuntime : IAdsClientRuntime
    {
        /// <summary>The deterministic scalar response.</summary>
        public const int ScalarValue = 42;

        /// <summary>The deterministic array response.</summary>
        public static readonly int[] ArrayValue = [1, 2, 3];

        /// <summary>Gets the scalar read count.</summary>
        public int ScalarReadCount { get; private set; }

        /// <summary>Gets the array read count.</summary>
        public int ArrayReadCount { get; private set; }

        /// <summary>Gets the local connection count.</summary>
        public int LocalConnectCount { get; private set; }

        /// <summary>Gets the remote connection count.</summary>
        public int RemoteConnectCount { get; private set; }

        /// <summary>Gets or sets the deterministic write failure.</summary>
        public Exception? WriteError { get; set; }

        /// <summary>Gets or sets the deterministic connection state.</summary>
        public bool IsConnectedValue { get; set; }

        /// <inheritdoc/>
        public bool IsConnected => IsConnectedValue;

        /// <inheritdoc/>
        public int? Port { get; private set; }

        /// <inheritdoc/>
        public void Connect(int port)
        {
            Port = port;
            IsConnectedValue = true;
            LocalConnectCount++;
        }

        /// <inheritdoc/>
        public void Connect(string adsAddress, int port)
        {
            _ = adsAddress;
            Port = port;
            IsConnectedValue = true;
            RemoteConnectCount++;
        }

        /// <inheritdoc/>
        public uint CreateVariableHandle(string variable)
        {
            _ = variable;
            return Handle;
        }

        /// <inheritdoc/>
        public void Dispose() => IsConnectedValue = false;

        /// <inheritdoc/>
        public object ReadAny(uint handle, Type type)
        {
            _ = handle;
            _ = type;
            ScalarReadCount++;
            return ScalarValue;
        }

        /// <inheritdoc/>
        public object ReadAny(uint handle, Type type, int[] lengths)
        {
            _ = handle;
            _ = type;
            _ = lengths;
            ArrayReadCount++;
            return ArrayValue;
        }

        /// <inheritdoc/>
        public StateInfo ReadState() => new(AdsState.Run, 0);

        /// <inheritdoc/>
        public void WriteAny(uint handle, object value)
        {
            _ = handle;
            _ = value;
            ThrowIfConfigured(WriteError);
        }

        /// <inheritdoc/>
        public void WriteControl(StateInfo state) => _ = state;

        /// <summary>Throws the configured deterministic error when one is present.</summary>
        /// <param name="error">The optional configured error.</param>
        private static void ThrowIfConfigured(Exception? error)
        {
            if (error is null)
            {
                return;
            }

            throw error;
        }
    }

    /// <summary>Provides deterministic client construction dependencies without native ADS or service access.</summary>
    private sealed class MinimalPlatform : IRxTcAdsPlatform, IDisposable
    {
        /// <summary>Stores the reusable no-value interval sequence.</summary>
        private static readonly EmptyObservable<long> EmptyIntervals = new();

        /// <summary>Stores the reusable no-service sequence.</summary>
        private static readonly EmptyObservable<IObservableServiceController> EmptyServices = new();

        /// <summary>Stores the deterministic code generator.</summary>
        private readonly CodeGenerator _generator = new();

        /// <summary>Stores the deterministic ADS runtime.</summary>
        private readonly RecordingAdsClientRuntime _runtime;

        /// <summary>Initializes a new instance of the <see cref="MinimalPlatform"/> class.</summary>
        /// <param name="runtime">The deterministic ADS runtime.</param>
        public MinimalPlatform(RecordingAdsClientRuntime runtime) =>
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

        /// <summary>Gets the number of interval subscriptions requested by production code.</summary>
        public int IntervalSubscriptionCount { get; private set; }

        /// <summary>Gets the number of symbol-load requests.</summary>
        public int LoadSymbolsCount { get; private set; }

        /// <inheritdoc/>
        public bool IsWindowsServiceMonitoringSupported => false;

        /// <inheritdoc/>
        public IAdsClientRuntime CreateAdsClient() => _runtime;

        /// <inheritdoc/>
        public ICodeGenerator CreateCodeGenerator() => _generator;

        /// <inheritdoc/>
        public void Dispose() => _generator.Dispose();

        /// <inheritdoc/>
        public IObservable<long> Interval(TimeSpan period)
        {
            _ = period;
            IntervalSubscriptionCount++;
            return EmptyIntervals;
        }

        /// <inheritdoc/>
        public void LoadSymbols(ICodeGenerator codeGenerator, string adsAddress, int port)
        {
            _ = codeGenerator;
            _ = adsAddress;
            _ = port;
            LoadSymbolsCount++;
        }

        /// <inheritdoc/>
#if !NETFRAMEWORK
        [SupportedOSPlatform("windows")]
#endif
        public IObservable<IObservableServiceController> GetServices() => EmptyServices;
    }

    /// <summary>Records errors emitted by a deterministic production observer.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    private sealed class RecordingObserver<T> : IObserver<T>
    {
        /// <summary>Gets the recorded errors.</summary>
        public List<Exception> Errors { get; } = [];

        /// <inheritdoc/>
        public void OnCompleted()
        {
        }

        /// <inheritdoc/>
        public void OnError(Exception error) => Errors.Add(error);

        /// <inheritdoc/>
        public void OnNext(T value) => _ = value;
    }

    /// <summary>Provides a deterministic observable that completes without producing values.</summary>
    /// <typeparam name="T">The observable value type.</typeparam>
    private sealed class EmptyObservable<T> : IObservable<T>
    {
        /// <summary>Stores the shared empty subscription.</summary>
        private static readonly EmptySubscription Subscription = new();

        /// <inheritdoc/>
        public IDisposable Subscribe(IObserver<T> observer)
        {
            observer.OnCompleted();
            return Subscription;
        }

        /// <summary>Provides one no-op subscription.</summary>
        private sealed class EmptySubscription : IDisposable
        {
            /// <inheritdoc/>
            public void Dispose()
            {
            }
        }
    }
}
