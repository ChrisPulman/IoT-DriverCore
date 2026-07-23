// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET9_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Reflection;
using IoT.DriverCore.TwinCATRx.Core;
using TwinCAT.Ads;
using CoreExtensions = IoT.DriverCore.TwinCATRx.Core.TwinCatRxExtensions;
using LeanBridge = IoT.DriverCore.TwinCATRx.ObservableBridgeExtensions;

namespace IoT.DriverCore.TwinCATRx.Tests.Rx;

/// <summary>Exercises deterministic disconnected branches of the native Beckhoff ADS implementation.</summary>
public sealed class NativeAdsInitializationCoverageTests
{
    /// <summary>A representative ADS variable.</summary>
    private const string Variable = ".Machine.Value";

    /// <summary>A representative ADS handle.</summary>
    private const uint Handle = 17;

    /// <summary>The invalid simulated ADS route.</summary>
    private const string InvalidRoute = "invalid.route";

    /// <summary>The first TwinCAT 3 ADS port.</summary>
    private const int TwinCat3Port = 851;

    /// <summary>A representative payload.</summary>
    private const int Payload = 42;

    /// <summary>The private client-state field name.</summary>
    private const string ClientStateField = "_clientState";

    /// <summary>The private native-value publication method name.</summary>
    private const string PublishNativeValueMethod = "PublishNativeValue";

    /// <summary>The private connection-reset method name.</summary>
    private const string ResetConnectionStateMethod = "ResetConnectionState";

    /// <summary>Verifies reset, connection branches, native read guards, and connection error publication.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("Exercises the native ADS initialization implementation through reflection.")]
    [RequiresUnreferencedCode("Exercises the native ADS initialization implementation through reflection.")]
#endif
    public async Task Native_Connection_Guards_And_Error_Channels_Are_DeterministicAsync()
    {
        using var client = new RxTcAdsClient();
        using var ads = new AdsClientRuntime();
        using var generator = new CodeGenerator();
        var errors = new List<Exception>();
        var observer = new RecordingObserver<Unit>();
        using var subscription = LeanBridge.SubscribeTo(client.ErrorReceived, errors.Add);
        var lifetime = GetProperty("ConnectionLifetime");

        await TUnitAssert.That(() => lifetime.GetValue(client)).Throws<TargetInvocationException>();
        _ = Invoke(client, ResetConnectionStateMethod);
        await TUnitAssert.That(lifetime.GetValue(client)).IsNotNull();
        var nullHandle = InvokeStatic("ReadNativeValue", ads, null, typeof(int), -1);
        await TUnitAssert.That(nullHandle).IsNull();
        await TUnitAssert.That(() => InvokeStatic("ReadNativeValue", ads, Handle, typeof(int), -1))
            .Throws<TargetInvocationException>();

        SetSettings(client, new Settings { AdsAddress = string.Empty, Port = TwinCat3Port });
        _ = Invoke(client, "ConnectAdsClient", ads, generator, observer);
        SetSettings(client, new Settings { AdsAddress = InvalidRoute, Port = TwinCat3Port });
        _ = Invoke(client, "ConnectAdsClient", ads, generator, observer);
        _ = Invoke(client, "PublishConnectionError", new IOException("manual"), observer);

        await TUnitAssert.That(client.Connected).IsFalse();
        await TUnitAssert.That(errors).IsNotEmpty();
        await TUnitAssert.That(observer.Errors).IsNotEmpty();
    }

    /// <summary>Verifies disconnected native state polling and queued read/write publication branches.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Native_State_Read_Write_And_Publication_Loops_Are_DeterministicAsync()
    {
        using var client = new RxTcAdsClient();
        using var ads = new AdsClientRuntime();
        var observer = new RecordingObserver<Unit>();
        var states = new List<AdsState>();
        var data = new List<(string Variable, object? Data, string? Id)>();
        _ = Invoke(client, ResetConnectionStateMethod);
        using var stateSubscription = LeanBridge.SubscribeTo(
            GetField<IObservable<AdsState>>(client, ClientStateField),
            states.Add);
        using var dataSubscription = LeanBridge.SubscribeTo(client.DataReceived, data.Add);

        _ = Invoke(client, "ReadAdsState", ads, observer);
        _ = Invoke(client, "MonitorReads", ads);
        _ = Invoke(client, "MonitorWrites", ads);
        ConfigureVariable(client);
        client.Read(Variable, "read");
        client.Write(Variable, Payload, "write");
        _ = Invoke(client, PublishNativeValueMethod, null, Payload, "null-handle");
        _ = Invoke(client, PublishNativeValueMethod, Handle, null, "null-value");
        _ = Invoke(client, PublishNativeValueMethod, Handle, Payload, "unknown");
        GetField<IDictionary<uint, string>>(client, "_readWriteVariablesByHandle")[Handle] = Variable;
        _ = Invoke(client, PublishNativeValueMethod, Handle, Payload, "mapped");

        await TUnitAssert.That(states).Contains(AdsState.Invalid);
        await TUnitAssert.That(data.Single().Variable).IsEqualTo(Variable);
        await TUnitAssert.That(data.Single().Id).IsEqualTo("mapped");
    }

    /// <summary>Verifies monitor composition, empty configuration initialization, and notification guards.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresDynamicCode("Exercises the native ADS initialization implementation through reflection.")]
    [RequiresUnreferencedCode("Exercises the native ADS initialization implementation through reflection.")]
#endif
    public async Task Native_Monitor_Composition_And_Empty_Initialization_Are_DeterministicAsync()
    {
        using var client = new RxTcAdsClient();
        using var ads = new AdsClientRuntime();
        var observer = new RecordingObserver<Unit>();
        var settings = new Settings { AdsAddress = InvalidRoute, Port = TwinCat3Port };
        CoreExtensions.AddNotification(settings, Variable);
        SetSettings(client, settings);
        _ = Invoke(client, ResetConnectionStateMethod);

        _ = Invoke(client, "MonitorAdsState", ads, observer);
        _ = Invoke(client, "MonitorInitialization", ads, observer);
        _ = Invoke(client, "ScheduleNotifications", ads);
        _ = Invoke(client, "ReadNotification", ads, settings.Notifications.Single());
        _ = Invoke(client, "TryStartPlcProgram", ads, observer);
        GetField<IObserver<ServiceStatus>>(client, "_serviceStatus").OnNext(ServiceStatus.Running);
        GetField<IObserver<AdsState>>(client, ClientStateField).OnNext(AdsState.Stop);
        settings.Notifications.Clear();
        GetField<IObserver<AdsState>>(client, ClientStateField).OnNext(AdsState.Run);

        await TUnitAssert.That(observer.Errors).IsNotEmpty();
        await TUnitAssert.That(client.Connected).IsTrue();
    }

    /// <summary>Configures one nullable native handle for disconnected loop coverage.</summary>
    /// <param name="client">The client.</param>
    private static void ConfigureVariable(RxTcAdsClient client)
    {
        client.ReadWriteHandleInfo[Variable] = null;
        GetField<IDictionary<string, Type>>(client, "_typeInfo")[Variable] = typeof(int);
    }

    /// <summary>Gets a private field as the requested contract.</summary>
    /// <typeparam name="T">The field contract.</typeparam>
    /// <param name="instance">The containing instance.</param>
    /// <param name="name">The field name.</param>
    /// <returns>The field value.</returns>
    private static T GetField<T>(object instance, string name) =>
        (T)(instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance)
            ?? throw new MissingFieldException(instance.GetType().FullName, name));

    /// <summary>Gets a private instance property.</summary>
    /// <param name="name">The property name.</param>
    /// <returns>The property.</returns>
    private static PropertyInfo GetProperty(string name) =>
        typeof(RxTcAdsClient).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMemberException(typeof(RxTcAdsClient).FullName, name);

    /// <summary>Invokes a private instance method.</summary>
    /// <param name="instance">The containing instance.</param>
    /// <param name="name">The method name.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The method result.</returns>
    private static object? Invoke(object instance, string name, params object?[] arguments)
    {
        var method = instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        return method is null
            ? MissingMethodResult(instance.GetType(), name)
            : method.Invoke(instance, arguments);
    }

    /// <summary>Invokes a private static method.</summary>
    /// <param name="name">The method name.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The method result.</returns>
    private static object? InvokeStatic(string name, params object?[] arguments) =>
        typeof(RxTcAdsClient).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, arguments);

    /// <summary>Throws a descriptive missing-method exception for expression-bodied reflection helpers.</summary>
    /// <param name="type">The containing type.</param>
    /// <param name="name">The method name.</param>
    /// <returns>This method never returns.</returns>
    private static object MissingMethodResult(Type type, string name) =>
        throw new MissingMethodException(type.FullName, name);

    /// <summary>Sets the current private settings property.</summary>
    /// <param name="client">The client.</param>
    /// <param name="settings">The settings.</param>
    private static void SetSettings(RxTcAdsClient client, Settings settings)
    {
        var property = typeof(RxTcAdsClient).GetProperty(
            nameof(RxTcAdsClient.Settings),
            BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMemberException(typeof(RxTcAdsClient).FullName, nameof(RxTcAdsClient.Settings));
        property.SetValue(client, settings);
    }

    /// <summary>Records observer errors without terminating deterministic test setup.</summary>
    /// <typeparam name="T">The observed type.</typeparam>
    private sealed class RecordingObserver<T> : IObserver<T>
    {
        /// <summary>Gets observed errors.</summary>
        public List<Exception> Errors { get; } = [];

        /// <inheritdoc/>
        public void OnCompleted()
        {
        }

        /// <inheritdoc/>
        public void OnError(Exception error) => Errors.Add(error);

        /// <inheritdoc/>
        public void OnNext(T value)
        {
        }
    }
}
