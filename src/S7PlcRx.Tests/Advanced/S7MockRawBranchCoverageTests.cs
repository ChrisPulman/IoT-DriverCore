// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
using System.Reflection;
using IoT.DriverCore.S7PlcRx.Mock;
using TUnitAssert = TUnit.Assertions.Assert;

namespace IoT.DriverCore.S7PlcRx.Tests.Advanced;

/// <summary>Exercises deterministic raw mock-server protocol and native-wrapper edge cases.</summary>
[NotInParallel]
public sealed class S7MockRawBranchCoverageTests
{
    /// <summary>The expected successful native mock-server result.</summary>
    private const int SuccessResult = 0;

    /// <summary>The deterministic native mock-server area size.</summary>
    private const int NativeAreaSize = 4;

    /// <summary>The managed server classifier method name.</summary>
    private const string ClassifyMethodName = "Classify";

    /// <summary>Verifies raw managed-server response builders cover short-frame and classifier edges.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ManagedServer_RawFrames_ShouldClassifyAndBuildShortRequestsAsync()
    {
        await TUnitAssert.That(() => new ManagedS7Server(null!)).Throws<ArgumentNullException>();

        using var server = new ManagedS7Server();
        await TUnitAssert.That((S7ServerOperation)InvokePrivateStatic(
            typeof(ManagedS7Server),
            ClassifyMethodName,
            [(byte[])[0, 0, 0, 0, 0, 0xe0]])!).IsEqualTo(S7ServerOperation.Connect);
        await TUnitAssert.That((S7ServerOperation)InvokePrivateStatic(
            typeof(ManagedS7Server),
            ClassifyMethodName,
            [new byte[5]])!).IsEqualTo(S7ServerOperation.Any);
        await TUnitAssert.That((S7ServerOperation)InvokePrivateStatic(
            typeof(ManagedS7Server),
            ClassifyMethodName,
            [new byte[18]])!).IsEqualTo(S7ServerOperation.Any);

        var szlRequest = new byte[18];
        szlRequest[7] = 0x32;
        szlRequest[8] = 0x07;
        await TUnitAssert.That((S7ServerOperation)InvokePrivateStatic(
            typeof(ManagedS7Server),
            ClassifyMethodName,
            [szlRequest])!).IsEqualTo(S7ServerOperation.Szl);

        var shortRequest = new byte[18];
        await TUnitAssert.That(((byte[])InvokePrivateInstance(
            server,
            "BuildSetupResponse",
            [shortRequest])!).Length).IsGreaterThan(0);
        await TUnitAssert.That(((byte[])InvokePrivateInstance(
            server,
            "BuildReadResponse",
            [shortRequest, (byte)0xff])!).Length).IsGreaterThan(0);
        await TUnitAssert.That(((byte[])InvokePrivateInstance(
            server,
            "BuildWriteResponse",
            [shortRequest, (byte)0xff])!).Length).IsGreaterThan(0);
        await TUnitAssert.That(((byte[])InvokePrivateInstance(
            server,
            "BuildSzlResponse",
            [shortRequest, (byte)0xff])!).Length).IsGreaterThan(0);

        var copyPduReference = typeof(ManagedS7Server).GetMethod(
            "CopyPduReference",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CopyPduReference was not found.");
        _ = copyPduReference.Invoke(server, [(byte[])[0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], new byte[13]]);
        _ = copyPduReference.Invoke(server, [new byte[13], new byte[12]]);
        _ = copyPduReference.Invoke(server, [new byte[13], new byte[13]]);
    }

    /// <summary>Verifies lifecycle states and accept-loop cancellation remain deterministic without a real PLC.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ManagedServer_RawLifecycleStates_ShouldDrainAndRejectStoppedClientsAsync()
    {
        using var server = new ManagedS7Server();
        SetIsRunning(server, true);
        server.Stop();
        await TUnitAssert.That(server.IsRunning).IsFalse();

        var acceptLoop = typeof(ManagedS7Server).GetMethod(
            "AcceptLoopAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("AcceptLoopAsync was not found.");

        var cancelledListener = new TcpListener(IPAddress.Loopback, 0);
        using (var cancellation = new CancellationTokenSource())
        {
            try
            {
                cancelledListener.Start();
                cancellation.Cancel();
                await (Task)(acceptLoop.Invoke(server, [cancelledListener, cancellation.Token])
                    ?? throw new InvalidOperationException("Cancelled accept loop was not created."));
            }
            finally
            {
                cancelledListener.Stop();
            }
        }

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var rejectedClient = new TcpClient();
        try
        {
            var accepting = (Task)(acceptLoop.Invoke(server, [listener, CancellationToken.None])
                ?? throw new InvalidOperationException("Accept loop was not created."));
            await rejectedClient.ConnectAsync(MockServer.Localhost, endpoint.Port);
            await accepting;
            await TUnitAssert.That(server.ClientsCount).IsEqualTo(0);
        }
        finally
        {
            rejectedClient.Dispose();
            listener.Stop();
        }
    }

    /// <summary>Verifies managed wrapper normalization and callback APIs do not require native hardware.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MockServer_ManagedRawSurface_ShouldNormalizeAreasAndCallbacksAsync()
    {
        var server = new MockServer
        {
            DefaultDb1Size = 0,
            DefaultPeSize = 0,
            DefaultPaSize = 0,
            DefaultMkSize = 0,
            DefaultCtSize = 0,
            DefaultTmSize = 0,
        };
        SrvCallback callback = static (nint _, ref USrvEvent _, int _) => { };
        SrvRwAreaCallback areaCallback = static (nint _, int _, int _, ref S7Tag _, ref RwBuffer _) => SuccessResult;

        try
        {
            await TUnitAssert.That(server.Start()).IsEqualTo(SuccessResult);
            await TUnitAssert.That(server.DefaultDb1Size).IsEqualTo(1);
            await TUnitAssert.That(server.DefaultPeSize).IsEqualTo(1);
            await TUnitAssert.That(server.DefaultPaSize).IsEqualTo(1);
            await TUnitAssert.That(server.DefaultMkSize).IsEqualTo(1);
            await TUnitAssert.That(server.DefaultCtSize).IsEqualTo(1);
            await TUnitAssert.That(server.DefaultTmSize).IsEqualTo(1);
            await TUnitAssert.That(server.SetEventsCallBack(callback, 0)).IsEqualTo(SuccessResult);
            await TUnitAssert.That(server.SetReadEventsCallBack(callback, 0)).IsEqualTo(SuccessResult);
            await TUnitAssert.That(server.SetRwAreaCallBack(areaCallback, 0)).IsEqualTo(SuccessResult);
            await TUnitAssert.That(server.Stop()).IsEqualTo(SuccessResult);
        }
        finally
        {
            server.Dispose();
            server.Dispose();
            GC.KeepAlive(callback);
            GC.KeepAlive(areaCallback);
        }
    }

    /// <summary>Verifies native helper guards and bounded Snap7 wrapper calls use only the bundled mock library.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MockServer_NativeRawSurface_ShouldExerciseBundledInteropEdgesAsync()
    {
        var nativeMethods = typeof(MockServer).Assembly.GetType(
            "IoT.DriverCore.S7PlcRx.Mock.NativeMethods",
            throwOnError: false)
            ?? throw new InvalidOperationException("NativeMethods was not found.");
        await TUnitAssert.That((string)InvokePrivateStatic(
            nativeMethods,
            "GetNullTerminatedString",
            [(byte[])[0x41, 0]])!).IsEqualTo("A");
        await TUnitAssert.That((string)InvokePrivateStatic(
            nativeMethods,
            "GetNullTerminatedString",
            [(byte[])[0x41, 0x42]])!).IsEqualTo("AB");
        await TUnitAssert.That(() => InvokePrivateStatic(
            nativeMethods,
            "Srv_StartTo",
            [(nint)0, null!])).Throws<TargetInvocationException>();

        using var server = new MockServer(S7ServerBackend.Snap7);
        byte[] area = new byte[NativeAreaSize];
        SrvCallback callback = static (nint _, ref USrvEvent _, int _) => { };
        SrvRwAreaCallback areaCallback = static (nint _, int _, int _, ref S7Tag _, ref RwBuffer _) => SuccessResult;
        var serverEvent = default(USrvEvent);

        server.LogMask = uint.MaxValue;
        server.EventMask = uint.MaxValue;
        await TUnitAssert.That(server.LogMask).IsEqualTo(uint.MaxValue);
        await TUnitAssert.That(server.EventMask).IsEqualTo(uint.MaxValue);
        await TUnitAssert.That(server.RegisterArea(MockServer.SrvAreaDB, 1, area, area.Length))
            .IsEqualTo(SuccessResult);
        await TUnitAssert.That(server.SetEventsCallBack(callback, 0)).IsEqualTo(SuccessResult);
        await TUnitAssert.That(server.SetReadEventsCallBack(callback, 0)).IsEqualTo(SuccessResult);
        await TUnitAssert.That(server.SetRwAreaCallBack(areaCallback, 0)).IsEqualTo(SuccessResult);
        await TUnitAssert.That(server.LockArea(MockServer.SrvAreaDB, 1)).IsEqualTo(SuccessResult);
        await TUnitAssert.That(server.UnlockArea(MockServer.SrvAreaDB, 1)).IsEqualTo(SuccessResult);
        _ = server.PickEvent(ref serverEvent);
        await TUnitAssert.That(server.ClearEvents()).IsEqualTo(SuccessResult);
        await TUnitAssert.That(server.UnregisterArea(MockServer.SrvAreaDB, 1)).IsEqualTo(SuccessResult);

        GC.KeepAlive(callback);
        GC.KeepAlive(areaCallback);
    }

    /// <summary>Invokes one private instance method.</summary>
    /// <param name="instance">The owning instance.</param>
    /// <param name="methodName">The private method name.</param>
    /// <param name="arguments">The invocation arguments.</param>
    /// <returns>The method result.</returns>
    private static object? InvokePrivateInstance(object instance, string methodName, object?[] arguments)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{methodName} was not found.");
        return method.Invoke(instance, arguments);
    }

    /// <summary>Invokes one private static method.</summary>
    /// <param name="type">The declaring type.</param>
    /// <param name="methodName">The private method name.</param>
    /// <param name="arguments">The invocation arguments.</param>
    /// <returns>The method result.</returns>
    private static object? InvokePrivateStatic(Type type, string methodName, object?[] arguments)
    {
        var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{methodName} was not found.");
        return method.Invoke(null, arguments);
    }

    /// <summary>Sets the managed server's internal lifecycle state for a shutdown-only edge case.</summary>
    /// <param name="server">The server whose state is set.</param>
    /// <param name="isRunning">The lifecycle state to assign.</param>
    private static void SetIsRunning(ManagedS7Server server, bool isRunning)
    {
        var property = typeof(ManagedS7Server).GetProperty(nameof(ManagedS7Server.IsRunning))
            ?? throw new InvalidOperationException("IsRunning was not found.");
        property.SetValue(server, isRunning);
    }
}
