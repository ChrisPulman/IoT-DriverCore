// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using IoT.DriverCore.S7PlcRx.Mock;
using TUnitAssert = TUnit.Assertions.Assert;

namespace IoT.DriverCore.S7PlcRx.Tests;

/// <summary>Tests deterministic managed S7 memory, lifecycle, framing, and fault behavior.</summary>
[NotInParallel]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class ManagedS7ServerTests
{
    /// <summary>The TPKT header length.</summary>
    private const int TpktHeaderLength = 4;

    /// <summary>The high-byte TPKT length offset.</summary>
    private const int TpktLengthHighOffset = 2;

    /// <summary>The low-byte TPKT length offset.</summary>
    private const int TpktLengthLowOffset = 3;

    /// <summary>The number of bits in one byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>The item return-code offset in a one-item ReadVar response.</summary>
    private const int ReadReturnCodeOffset = 21;

    /// <summary>The item return-code offset in a one-item WriteVar response.</summary>
    private const int WriteReturnCodeOffset = 21;

    /// <summary>The deterministic test DB number.</summary>
    private const ushort TestDbNumber = 7;

    /// <summary>The default DB number used by wire requests.</summary>
    private const ushort DefaultDbNumber = 1;

    /// <summary>The native test DB number.</summary>
    private const int NativeTestDbNumber = 2;

    /// <summary>The deterministic test memory size.</summary>
    private const int TestMemorySize = 8;

    /// <summary>The byte offset used for memory writes.</summary>
    private const int WriteOffset = 2;

    /// <summary>The expected written byte count.</summary>
    private const int WrittenByteCount = 3;

    /// <summary>The test delay in milliseconds.</summary>
    private const int FaultDelayMilliseconds = 25;

    /// <summary>The lower accepted elapsed delay in milliseconds.</summary>
    private const int MinimumObservedDelayMilliseconds = 15;

    /// <summary>The wait timeout in seconds.</summary>
    private const int OperationTimeoutSeconds = 5;

    /// <summary>The scripted S7 error return code.</summary>
    private const byte ScriptedErrorCode = 0x05;

    /// <summary>The deterministic byte written through the protocol.</summary>
    private const byte TestWriteValue = 0x42;

    /// <summary>The successful mock-server result code.</summary>
    private const int SuccessResult = 0;

    /// <summary>The deterministic CPU status.</summary>
    private const int TestCpuStatus = 4;

    /// <summary>The native CPU run status.</summary>
    private const int NativeCpuRunStatus = 8;

    /// <summary>The minimum expected completed read count.</summary>
    private const int MinimumExpectedReadCount = 2;

    /// <summary>The expected number of independently serviced concurrent clients.</summary>
    private const int ExpectedConcurrentClientCount = 2;

    /// <summary>The first unsupported server area code.</summary>
    private const int UnsupportedAreaCode = -1;

    /// <summary>The unsupported S7ANY wire area.</summary>
    private const byte UnsupportedWireArea = 0x99;

    /// <summary>The CPU-information SZL identifier.</summary>
    private const ushort CpuInformationSzl = 0x001c;

    /// <summary>The order-code SZL identifier.</summary>
    private const ushort OrderCodeSzl = 0x0011;

    /// <summary>An unsupported SZL identifier.</summary>
    private const ushort UnsupportedSzl = 0xffff;

    /// <summary>The minimum expected CPU-information response length.</summary>
    private const int MinimumCpuResponseLength = 200;

    /// <summary>The minimum expected order-code response length.</summary>
    private const int MinimumOrderCodeResponseLength = 60;

    /// <summary>The expected two-item ReadVar response length with odd-byte padding.</summary>
    private const int PaddedTwoItemReadResponseLength = 32;

    /// <summary>The second item return-code offset in the padded two-item response.</summary>
    private const int SecondPaddedReadReturnCodeOffset = 27;

    /// <summary>The variable specification offset in a request.</summary>
    private const int VariableSpecificationOffset = 19;

    /// <summary>The S7ANY variable specification length.</summary>
    private const int VariableSpecificationLength = 12;

    /// <summary>The second variable specification offset.</summary>
    private const int SecondVariableSpecificationOffset = 31;

    /// <summary>The item-count offset in a variable request.</summary>
    private const int ItemCountOffset = 18;

    /// <summary>The two-item request count.</summary>
    private const byte TwoItemCount = 2;

    /// <summary>The truncated write length without an item-data header.</summary>
    private const int WriteWithoutDataHeaderLength = 31;

    /// <summary>The truncated write length without the item value.</summary>
    private const int WriteWithoutValueLength = 35;

    /// <summary>Gets a standard COTP connection request.</summary>
    private static readonly byte[] CotpConnectionRequest =
    [
        0x03, 0x00, 0x00, 0x16, 0x11, 0xe0, 0x00, 0x00, 0x00, 0x01, 0x00,
        0xc1, 0x02, 0x01, 0x00, 0xc2, 0x02, 0x01, 0x02, 0xc0, 0x01, 0x09,
    ];

    /// <summary>Gets a standard S7 setup-communication request.</summary>
    private static readonly byte[] SetupCommunicationRequest =
    [
        0x03, 0x00, 0x00, 0x19, 0x02, 0xf0, 0x80,
        0x32, 0x01, 0x00, 0x00, 0x00, 0x04, 0x00, 0x08, 0x00, 0x00,
        0xf0, 0x00, 0x00, 0x01, 0x00, 0x01, 0x03, 0xc0,
    ];

    /// <summary>Gets a one-byte DB1 ReadVar request.</summary>
    private static readonly byte[] ReadDb1ByteRequest =
    [
        0x03, 0x00, 0x00, 0x1f, 0x02, 0xf0, 0x80,
        0x32, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x0e, 0x00, 0x00,
        0x04, 0x01,
        0x12, 0x0a, 0x10, 0x02, 0x00, 0x01, 0x00, 0x01, 0x84, 0x00, 0x00, 0x00,
    ];

    /// <summary>Gets a one-byte DB1 WriteVar request.</summary>
    private static readonly byte[] WriteDb1ByteRequest =
    [
        0x03, 0x00, 0x00, 0x24, 0x02, 0xf0, 0x80,
        0x32, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x0e, 0x00, 0x05,
        0x05, 0x01,
        0x12, 0x0a, 0x10, 0x02, 0x00, 0x01, 0x00, 0x01, 0x84, 0x00, 0x00, 0x00,
        0x00, 0x04, 0x00, 0x08, TestWriteValue,
    ];

    /// <summary>Gets the debugger display text.</summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => GetType().Name;

    /// <summary>Ensures managed memory provides deterministic registration, range checks, and write events.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ManagedMemory_ShouldRegisterReadWriteNotifyAndUnregisterAsync()
    {
        await TUnitAssert.That(DebuggerDisplay).IsEqualTo(nameof(ManagedS7ServerTests));
        var memory = new ManagedS7Memory();
        var backing = memory.Register(S7MemoryArea.DataBlock, TestDbNumber, TestMemorySize);
        S7MemoryChangedEventArgs? changed = null;
        memory.Changed += (_, args) => changed = args;

        byte[] written = [0x10, 0x20, 0x30];
        memory.Write(S7MemoryArea.DataBlock, TestDbNumber, WriteOffset, written);
        written[0] = 0xff;

        await TUnitAssert.That(memory.GetBuffer(S7MemoryArea.DataBlock, TestDbNumber)).IsEqualTo(backing);
        await TUnitAssert.That(memory.Read(
            S7MemoryArea.DataBlock,
            TestDbNumber,
            WriteOffset,
            WrittenByteCount)).IsEquivalentTo((byte[])[0x10, 0x20, 0x30]);
        await TUnitAssert.That(changed).IsNotNull();
        await TUnitAssert.That(changed!.Area).IsEqualTo(S7MemoryArea.DataBlock);
        await TUnitAssert.That(changed.DbNumber).IsEqualTo(TestDbNumber);
        await TUnitAssert.That(changed.Offset).IsEqualTo(WriteOffset);
        await TUnitAssert.That(changed.Data).IsEquivalentTo((byte[])[0x10, 0x20, 0x30]);

        memory.Register(S7MemoryArea.Input, TestDbNumber, [0x42]);
        await TUnitAssert.That(memory.Read(S7MemoryArea.Input, 0, 0, 1)).IsEquivalentTo((byte[])[0x42]);
        await TUnitAssert.That(memory.Unregister(S7MemoryArea.Input, TestDbNumber)).IsTrue();
        await TUnitAssert.That(memory.Unregister(S7MemoryArea.Input, TestDbNumber)).IsFalse();

        await TUnitAssert.That(() => memory.Register(
            S7MemoryArea.DataBlock,
            TestDbNumber,
            (byte[])null!)).Throws<ArgumentNullException>();
        await TUnitAssert.That(() => memory.Register(S7MemoryArea.DataBlock, TestDbNumber, 0))
            .Throws<ArgumentOutOfRangeException>();
        await TUnitAssert.That(() => memory.Read(
            S7MemoryArea.DataBlock,
            TestDbNumber,
            TestMemorySize,
            1)).Throws<ArgumentOutOfRangeException>();
        await TUnitAssert.That(() => memory.GetBuffer(S7MemoryArea.Output, 0)).Throws<KeyNotFoundException>();
    }

    /// <summary>Ensures MockServer retains its managed Snap7-compatible lifecycle and area surface.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MockServer_DefaultBackend_ShouldPreserveManagedStateAcrossRestartAsync()
    {
        using var server = new MockServer
        {
            DefaultDb1Size = TestMemorySize,
            LogMask = 0x1234,
            EventMask = 0x5678,
            CpuStatus = TestCpuStatus,
        };

        await TUnitAssert.That(server.Backend).IsEqualTo(S7ServerBackend.Managed);
        await TUnitAssert.That(server.ManagedServer).IsNotNull();
        await TUnitAssert.That(server.Memory).IsNotNull();
        await TUnitAssert.That(server.LogMask).IsEqualTo(0x1234U);
        await TUnitAssert.That(server.EventMask).IsEqualTo(0x5678U);
        await TUnitAssert.That(server.CpuStatus).IsEqualTo(TestCpuStatus);
        await TUnitAssert.That(server.Start()).IsEqualTo(SuccessResult);
        await TUnitAssert.That(server.Start()).IsEqualTo(SuccessResult);
        await TUnitAssert.That(server.ServerStatus).IsEqualTo(1);
        await TUnitAssert.That(server.LockArea(MockServer.SrvAreaDB, 1)).IsEqualTo(SuccessResult);
        await TUnitAssert.That(server.UnlockArea(MockServer.SrvAreaDB, 1)).IsEqualTo(SuccessResult);
        await TUnitAssert.That(server.ClearEvents()).IsEqualTo(SuccessResult);

        server.DefaultDb1![0] = 0x7a;
        await TUnitAssert.That(server.Stop()).IsEqualTo(SuccessResult);
        await TUnitAssert.That(server.Stop()).IsEqualTo(SuccessResult);
        await TUnitAssert.That(server.ServerStatus).IsEqualTo(0);
        await TUnitAssert.That(server.Start()).IsEqualTo(SuccessResult);
        await TUnitAssert.That(server.DefaultDb1[0]).IsEqualTo((byte)0x7a);
    }

    /// <summary>Ensures lifecycle validation and disposed-object guards are deterministic.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ManagedServer_InvalidLifecycle_ShouldRejectInputsAndDisposedUseAsync()
    {
        var server = new ManagedS7Server();
        server.Stop();

        await TUnitAssert.That(() => server.EnqueueFault(null!)).Throws<ArgumentNullException>();
        await TUnitAssert.That(() => server.Start(string.Empty)).Throws<ArgumentException>();
        await TUnitAssert.That(() => server.Start(MockServer.Localhost, -1))
            .Throws<ArgumentOutOfRangeException>();
        await TUnitAssert.That(() => server.Start(MockServer.Localhost, IPEndPoint.MaxPort + 1))
            .Throws<ArgumentOutOfRangeException>();

        var defaultFault = new S7ServerFault(S7ServerFaultKind.Disconnect);
        await TUnitAssert.That(defaultFault.Operation).IsEqualTo(S7ServerOperation.Any);
        await TUnitAssert.That(defaultFault.Delay).IsEqualTo(TimeSpan.Zero);
        await TUnitAssert.That(defaultFault.ReturnCode).IsEqualTo(ScriptedErrorCode);

        server.Dispose();
        server.Dispose();
        await TUnitAssert.That(() => server.EnqueueFault(defaultFault)).Throws<ObjectDisposedException>();
        await TUnitAssert.That(server.IsRunning).IsFalse();
    }

    /// <summary>Ensures the explicit bundled Snap7 backend remains available without changing the managed default.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MockServer_Snap7Backend_ShouldPreserveNativeCompatibilityAsync()
    {
        if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.Windows))
        {
            return;
        }

        using var server = new MockServer(S7ServerBackend.Snap7);
        byte[] db1 = new byte[TestMemorySize];
        var structuredArea = 0x1234U;
        SrvCallback eventCallback = static (nint _, ref USrvEvent _, int _) => { };
        SrvRwAreaCallback areaCallback =
            static (nint _, int _, int _, ref S7Tag _, ref RwBuffer _) => SuccessResult;

        await TUnitAssert.That(server.Backend).IsEqualTo(S7ServerBackend.Snap7);
        await TUnitAssert.That(server.ManagedServer).IsNull();
        await TUnitAssert.That(server.Memory).IsNull();
        await TUnitAssert.That(server.RegisterArea(
            MockServer.SrvAreaDB,
            1,
            db1,
            db1.Length)).IsEqualTo(SuccessResult);
        await TUnitAssert.That(server.RegisterArea(
            MockServer.SrvAreaDB,
            NativeTestDbNumber,
            ref structuredArea,
            sizeof(uint))).IsEqualTo(SuccessResult);

        server.LogMask = uint.MaxValue;
        server.EventMask = uint.MaxValue;
        await TUnitAssert.That(server.LogMask).IsEqualTo(uint.MaxValue);
        await TUnitAssert.That(server.EventMask).IsEqualTo(uint.MaxValue);
        await TUnitAssert.That(server.SetEventsCallBack(eventCallback, 0)).IsEqualTo(SuccessResult);
        await TUnitAssert.That(server.SetReadEventsCallBack(eventCallback, 0)).IsEqualTo(SuccessResult);
        await TUnitAssert.That(server.SetRwAreaCallBack(areaCallback, 0)).IsEqualTo(SuccessResult);
        await TUnitAssert.That(server.LockArea(MockServer.SrvAreaDB, 1)).IsEqualTo(SuccessResult);
        await TUnitAssert.That(server.UnlockArea(MockServer.SrvAreaDB, 1)).IsEqualTo(SuccessResult);

        await TUnitAssert.That(server.Start()).IsEqualTo(SuccessResult);
        await TUnitAssert.That(server.ServerStatus).IsGreaterThanOrEqualTo(0);
        await TUnitAssert.That(server.ClientsCount).IsGreaterThanOrEqualTo(0);
        server.CpuStatus = NativeCpuRunStatus;
        await TUnitAssert.That(server.CpuStatus).IsGreaterThanOrEqualTo(0);

        var serverEvent = default(USrvEvent);
        _ = server.PickEvent(ref serverEvent);
        await TUnitAssert.That(server.ClearEvents()).IsEqualTo(SuccessResult);
        await TUnitAssert.That(MockServer.ErrorText(SuccessResult)).IsNotNull();
        await TUnitAssert.That(MockServer.EventText(ref serverEvent)).IsNotNull();
        await TUnitAssert.That(MockServer.EvtTimeToDateTime(0)).IsEqualTo(TestTime.UnixEpoch);

        await TUnitAssert.That(server.Stop()).IsEqualTo(SuccessResult);
        await TUnitAssert.That(server.StartTo(MockServer.Localhost)).IsEqualTo(SuccessResult);
        await TUnitAssert.That(server.Stop()).IsEqualTo(SuccessResult);
        await TUnitAssert.That(server.UnregisterArea(
            MockServer.SrvAreaDB,
            NativeTestDbNumber)).IsEqualTo(SuccessResult);
        await TUnitAssert.That(server.UnregisterArea(MockServer.SrvAreaDB, 1)).IsEqualTo(SuccessResult);

        GC.KeepAlive(eventCallback);
        GC.KeepAlive(areaCallback);
    }

    /// <summary>Ensures raw S7 framing and all deterministic single-use fault modes work across reconnects.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ManagedServer_Faults_ShouldDelayReturnErrorMalformAndDisconnectAsync()
    {
        using var server = new MockServer { DefaultDb1Size = TestMemorySize };
        await TUnitAssert.That(server.Start()).IsEqualTo(SuccessResult);
        var managed = server.ManagedServer ?? throw new InvalidOperationException("Managed server is unavailable.");

        using (var client = await ConnectAndHandshakeAsync())
        {
            managed.EnqueueFault(new(
                S7ServerFaultKind.Delay,
                S7ServerOperation.Read,
                TimeSpan.FromMilliseconds(FaultDelayMilliseconds),
                ScriptedErrorCode));
            var stopwatch = Stopwatch.StartNew();
            var delayed = await SendAndReceiveAsync(client.GetStream(), ReadDb1ByteRequest);
            stopwatch.Stop();

            await TUnitAssert.That(delayed[ReadReturnCodeOffset]).IsEqualTo((byte)0xff);
            await TUnitAssert.That(stopwatch.ElapsedMilliseconds)
                .IsGreaterThanOrEqualTo(MinimumObservedDelayMilliseconds);

            managed.EnqueueFault(new(
                S7ServerFaultKind.ReturnCode,
                S7ServerOperation.Write,
                TimeSpan.Zero,
                ScriptedErrorCode));
            var unaffectedRead = await SendAndReceiveAsync(client.GetStream(), ReadDb1ByteRequest);
            await TUnitAssert.That(unaffectedRead[ReadReturnCodeOffset]).IsEqualTo((byte)0xff);

            var failedWrite = await SendAndReceiveAsync(client.GetStream(), WriteDb1ByteRequest);
            await TUnitAssert.That(failedWrite[WriteReturnCodeOffset]).IsEqualTo(ScriptedErrorCode);
            await TUnitAssert.That(server.DefaultDb1![0]).IsEqualTo((byte)0);

            var successfulWrite = await SendAndReceiveAsync(client.GetStream(), WriteDb1ByteRequest);
            await TUnitAssert.That(successfulWrite[WriteReturnCodeOffset]).IsEqualTo((byte)0xff);
            await TUnitAssert.That(server.DefaultDb1[0]).IsEqualTo(TestWriteValue);

            managed.EnqueueFault(new(
                S7ServerFaultKind.ReturnCode,
                S7ServerOperation.Read,
                TimeSpan.Zero,
                ScriptedErrorCode));
            var failed = await SendAndReceiveAsync(client.GetStream(), ReadDb1ByteRequest);
            await TUnitAssert.That(failed[ReadReturnCodeOffset]).IsEqualTo(ScriptedErrorCode);

            managed.EnqueueFault(new(S7ServerFaultKind.MalformedFrame, S7ServerOperation.Read));
            var malformed = await SendAndReceiveAsync(client.GetStream(), ReadDb1ByteRequest);
            await TUnitAssert.That(malformed).IsEquivalentTo((byte[])[0x03, 0x00, 0x00, 0x03]);
        }

        using (var client = await ConnectAndHandshakeAsync())
        {
            managed.EnqueueFault(new(S7ServerFaultKind.Disconnect, S7ServerOperation.Read));
            await NetworkCompatibility.WriteAsync(client.GetStream(), ReadDb1ByteRequest);
            var disconnected = await AsyncCompatibility.WaitAsync(
                NetworkCompatibility.ReadAsync(client.GetStream(), new byte[1]),
                TimeSpan.FromSeconds(OperationTimeoutSeconds));
            await TUnitAssert.That(disconnected).IsEqualTo(0);
        }

        managed.EnqueueFault(new(S7ServerFaultKind.Delay, S7ServerOperation.Write));
        managed.ClearFaults();
        await TUnitAssert.That(managed.ReadCount).IsGreaterThanOrEqualTo(MinimumExpectedReadCount);
        await TUnitAssert.That(managed.WriteCount).IsGreaterThanOrEqualTo(MinimumExpectedReadCount);
        await TUnitAssert.That(managed.ClientsCount).IsEqualTo(0);
    }

    /// <summary>Ensures wrapper area validation rejects unsupported inputs.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MockServer_ManagedWrapper_ShouldValidateAreaInputsAsync()
    {
        using var server = new MockServer();
        byte singleByte = 0;

        await TUnitAssert.That(() => server.RegisterArea(
            MockServer.SrvAreaDB,
            DefaultDbNumber,
            ref singleByte,
            sizeof(byte))).Throws<ArgumentException>();
        await TUnitAssert.That(() => server.RegisterArea(
            MockServer.SrvAreaDB,
            DefaultDbNumber,
            null!,
            0)).Throws<ArgumentNullException>();
        await TUnitAssert.That(() => server.RegisterArea(
            MockServer.SrvAreaDB,
            DefaultDbNumber,
            [1],
            -1)).Throws<ArgumentOutOfRangeException>();
        await TUnitAssert.That(() => server.RegisterArea(
            MockServer.SrvAreaDB,
            DefaultDbNumber,
            [1],
            WrittenByteCount)).Throws<ArgumentOutOfRangeException>();
        await TUnitAssert.That(() => server.RegisterArea(
            UnsupportedAreaCode,
            DefaultDbNumber,
            [1],
            1)).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Ensures managed registrations and callback value records retain their contracts.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MockServer_ManagedWrapper_ShouldRegisterAreasAndValueRecordsAsync()
    {
        using var server = new MockServer();
        var structuredArea = 0x12345678U;

        await TUnitAssert.That(server.RegisterArea(
            MockServer.SrvAreaDB,
            TestDbNumber,
            ref structuredArea,
            sizeof(uint))).IsEqualTo(SuccessResult);
        await TUnitAssert.That(server.RegisterArea(
            MockServer.SrvAreaDB,
            NativeTestDbNumber,
            [0x11, 0x22],
            1)).IsEqualTo(SuccessResult);
        await TUnitAssert.That(server.Memory!.Read(
            S7MemoryArea.DataBlock,
            NativeTestDbNumber,
            0,
            1)).IsEquivalentTo((byte[])[0x11]);
        await TUnitAssert.That(server.UnregisterArea(
            MockServer.SrvAreaDB,
            NativeTestDbNumber)).IsEqualTo(SuccessResult);
        await TUnitAssert.That(server.UnregisterArea(
            MockServer.SrvAreaDB,
            NativeTestDbNumber)).IsNotEqualTo(SuccessResult);

        var serverEvent = default(USrvEvent);
        await TUnitAssert.That(server.PickEvent(ref serverEvent)).IsFalse();
        await TUnitAssert.That(server.ClientsCount).IsEqualTo(0);

        var tag = new S7Tag(
            MockServer.SrvAreaDB,
            TestDbNumber,
            WriteOffset,
            WrittenByteCount,
            BitsPerByte);
        var buffer = new RwBuffer([TestWriteValue]);
        await TUnitAssert.That(tag.Area).IsEqualTo(MockServer.SrvAreaDB);
        await TUnitAssert.That(tag.DBNumber).IsEqualTo((int)TestDbNumber);
        await TUnitAssert.That(tag.Start).IsEqualTo(WriteOffset);
        await TUnitAssert.That(tag.Elements).IsEqualTo(WrittenByteCount);
        await TUnitAssert.That(tag.WordLen).IsEqualTo(BitsPerByte);
        await TUnitAssert.That(buffer.Data).IsEquivalentTo((byte[])[TestWriteValue]);

        var occupiedPort = new TcpListener(IPAddress.Loopback, ManagedS7Server.DefaultPort);
        using var occupiedPortLifetime = NetworkCompatibility.StopOnDispose(occupiedPort);
        occupiedPort.Start();
        await TUnitAssert.That(server.Start()).IsNotEqualTo(SuccessResult);
        occupiedPort.Stop();
    }

    /// <summary>Ensures one idle client cannot prevent another client from completing its S7 handshake.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ManagedServer_ConcurrentClients_ShouldCompleteIndependentHandshakesAsync()
    {
        using var server = new ManagedS7Server();
        _ = server.Memory.Register(S7MemoryArea.DataBlock, DefaultDbNumber, TestMemorySize);
        server.Start();

        using var idleClient = new TcpClient();
        await idleClient.ConnectAsync(MockServer.Localhost, ManagedS7Server.DefaultPort);
        _ = await SendAndReceiveAsync(idleClient.GetStream(), CotpConnectionRequest);

        using var activeClient = await AsyncCompatibility.WaitAsync(
            ConnectAndHandshakeAsync(),
            TimeSpan.FromSeconds(OperationTimeoutSeconds));
        var response = await SendAndReceiveAsync(activeClient.GetStream(), ReadDb1ByteRequest);

        await TUnitAssert.That(response[ReadReturnCodeOffset]).IsEqualTo((byte)0xff);
        await TUnitAssert.That(server.ClientsCount).IsEqualTo(ExpectedConcurrentClientCount);
    }

    /// <summary>Ensures invalid read/write items and odd-byte padding are deterministic.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ManagedServer_InvalidItems_ShouldReturnErrorCodesAndPaddingAsync()
    {
        using var server = new ManagedS7Server();
        _ = server.Memory.Register(S7MemoryArea.DataBlock, DefaultDbNumber, TestMemorySize);
        server.Start();

        using var client = await ConnectAndHandshakeAsync();
        var stream = client.GetStream();
        var padded = await SendAndReceiveAsync(stream, CreateTwoItemReadRequest());
        await TUnitAssert.That(padded.Length).IsEqualTo(PaddedTwoItemReadResponseLength);
        await TUnitAssert.That(padded[ReadReturnCodeOffset]).IsEqualTo((byte)0xff);
        await TUnitAssert.That(padded[SecondPaddedReadReturnCodeOffset]).IsEqualTo((byte)0xff);

        var invalidSpec = (byte[])ReadDb1ByteRequest.Clone();
        invalidSpec[VariableSpecificationOffset] = 0;
        var invalidSpecResponse = await SendAndReceiveAsync(stream, invalidSpec);
        await TUnitAssert.That(invalidSpecResponse[ReadReturnCodeOffset])
            .IsEqualTo(ScriptedErrorCode);

        var invalidArea = (byte[])ReadDb1ByteRequest.Clone();
        invalidArea[27] = UnsupportedWireArea;
        var invalidAreaResponse = await SendAndReceiveAsync(stream, invalidArea);
        await TUnitAssert.That(invalidAreaResponse[ReadReturnCodeOffset])
            .IsEqualTo(ScriptedErrorCode);

        var missingDb = (byte[])ReadDb1ByteRequest.Clone();
        missingDb[25] = byte.MaxValue;
        missingDb[26] = byte.MaxValue;
        var missingDbResponse = await SendAndReceiveAsync(stream, missingDb);
        await TUnitAssert.That(missingDbResponse[ReadReturnCodeOffset])
            .IsEqualTo(ScriptedErrorCode);

        var missingWriteHeader = WriteDb1ByteRequest
            .Take(WriteWithoutDataHeaderLength)
            .ToArray();
        SetTpktLength(missingWriteHeader);
        var missingHeaderResponse = await SendAndReceiveAsync(stream, missingWriteHeader);
        await TUnitAssert.That(missingHeaderResponse[WriteReturnCodeOffset])
            .IsEqualTo(ScriptedErrorCode);

        var missingWriteValue = WriteDb1ByteRequest
            .Take(WriteWithoutValueLength)
            .ToArray();
        SetTpktLength(missingWriteValue);
        var missingValueResponse = await SendAndReceiveAsync(stream, missingWriteValue);
        await TUnitAssert.That(missingValueResponse[WriteReturnCodeOffset])
            .IsEqualTo(ScriptedErrorCode);

        var invalidWriteSpec = (byte[])WriteDb1ByteRequest.Clone();
        invalidWriteSpec[VariableSpecificationOffset] = 0;
        var invalidWriteResponse = await SendAndReceiveAsync(stream, invalidWriteSpec);
        await TUnitAssert.That(invalidWriteResponse[WriteReturnCodeOffset])
            .IsEqualTo(ScriptedErrorCode);
    }

    /// <summary>Ensures SZLs, malformed frames, and stop-with-client behavior are deterministic.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ManagedServer_SzlsAndInvalidFrames_ShouldCloseDeterministicallyAsync()
    {
        using var server = new ManagedS7Server();
        _ = server.Memory.Register(S7MemoryArea.DataBlock, DefaultDbNumber, TestMemorySize);
        server.Start();

        using (var client = await ConnectAndHandshakeAsync())
        {
            var stream = client.GetStream();
            var cpuInformation = await SendAndReceiveAsync(stream, CreateSzlRequest(CpuInformationSzl));
            var orderCode = await SendAndReceiveAsync(stream, CreateSzlRequest(OrderCodeSzl));
            var unsupported = await SendAndReceiveAsync(stream, CreateSzlRequest(UnsupportedSzl));
            await TUnitAssert.That(cpuInformation.Length)
                .IsGreaterThan(MinimumCpuResponseLength);
            await TUnitAssert.That(orderCode.Length)
                .IsGreaterThan(MinimumOrderCodeResponseLength);
            await TUnitAssert.That(unsupported.Length)
                .IsLessThan(MinimumOrderCodeResponseLength);

            byte[] unknownFunction =
            [
                0x03, 0x00, 0x00, 0x12, 0x02, 0xf0, 0x80,
                0x32, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x7f, 0x7f,
            ];
            await NetworkCompatibility.WriteAsync(stream, unknownFunction);
            await TUnitAssert.That(await ReadClosedStreamAsync(stream)).IsEqualTo(0);
        }

        using (var client = await ConnectAndHandshakeAsync())
        {
            await NetworkCompatibility.WriteAsync(
                client.GetStream(),
                [0x02, 0x00, 0x00, 0x04]);
            await TUnitAssert.That(await ReadClosedStreamAsync(client.GetStream())).IsEqualTo(0);
        }

        using (var client = await ConnectAndHandshakeAsync())
        {
            await NetworkCompatibility.WriteAsync(
                client.GetStream(),
                [0x03, 0x00, 0x00, 0x03]);
            await TUnitAssert.That(await ReadClosedStreamAsync(client.GetStream())).IsEqualTo(0);
        }

        using var connectedClient = await ConnectAndHandshakeAsync();
        await TUnitAssert.That(server.ClientsCount).IsEqualTo(1);
        server.Stop();
        await TUnitAssert.That(server.ClientsCount).IsEqualTo(0);
    }

    /// <summary>Connects a raw TCP client and completes COTP and S7 setup.</summary>
    /// <returns>The connected and negotiated client.</returns>
    private static async Task<TcpClient> ConnectAndHandshakeAsync()
    {
        var client = new TcpClient();
        await client.ConnectAsync(MockServer.Localhost, ManagedS7Server.DefaultPort);
        _ = await SendAndReceiveAsync(client.GetStream(), CotpConnectionRequest);
        _ = await SendAndReceiveAsync(client.GetStream(), SetupCommunicationRequest);
        return client;
    }

    /// <summary>Creates a two-item ReadVar request with odd-sized item values.</summary>
    /// <returns>The multi-item request.</returns>
    private static byte[] CreateTwoItemReadRequest()
    {
        var request = new byte[43];
        Buffer.BlockCopy(ReadDb1ByteRequest, 0, request, 0, ReadDb1ByteRequest.Length);
        request[TpktLengthLowOffset] = (byte)request.Length;
        request[14] = 0x1a;
        request[ItemCountOffset] = TwoItemCount;
        Buffer.BlockCopy(
            ReadDb1ByteRequest,
            VariableSpecificationOffset,
            request,
            SecondVariableSpecificationOffset,
            VariableSpecificationLength);
        return request;
    }

    /// <summary>Creates a minimal SZL request.</summary>
    /// <param name="identifier">The SZL identifier.</param>
    /// <returns>The request frame.</returns>
    private static byte[] CreateSzlRequest(ushort identifier)
    {
        byte[] request =
        [
            0x03, 0x00, 0x00, 0x1f, 0x02, 0xf0, 0x80,
            0x32, 0x07, 0x00, 0x00, 0x00, 0x01, 0x00, 0x08, 0x00,
            0x08, 0x00, 0x01, 0x12, 0x04, 0x11, 0x44, 0x01,
            0x00, 0xff, 0x09, 0x00, 0x04, 0x00, 0x00,
        ];
        request[29] = (byte)(identifier >> BitsPerByte);
        request[30] = (byte)identifier;
        return request;
    }

    /// <summary>Updates a request's TPKT length after truncation.</summary>
    /// <param name="request">The request to update.</param>
    private static void SetTpktLength(byte[] request)
    {
        request[TpktLengthHighOffset] = (byte)(request.Length >> BitsPerByte);
        request[TpktLengthLowOffset] = (byte)request.Length;
    }

    /// <summary>Reads the end-of-stream result after a deterministic server disconnect.</summary>
    /// <param name="stream">The client stream.</param>
    /// <returns>The zero-byte read result.</returns>
    private static async Task<int> ReadClosedStreamAsync(NetworkStream stream) =>
        await AsyncCompatibility.WaitAsync(
            NetworkCompatibility.ReadAsync(stream, new byte[1]),
            TimeSpan.FromSeconds(OperationTimeoutSeconds));

    /// <summary>Sends a complete frame and reads one complete TPKT response.</summary>
    /// <param name="stream">The connected network stream.</param>
    /// <param name="request">The request frame.</param>
    /// <returns>The complete response frame.</returns>
    private static async Task<byte[]> SendAndReceiveAsync(NetworkStream stream, byte[] request)
    {
        await NetworkCompatibility.WriteAsync(stream, request);
        var header = new byte[TpktHeaderLength];
        await ReadExactlyAsync(stream, header, 0, header.Length);
        var length = (header[TpktLengthHighOffset] << BitsPerByte) | header[TpktLengthLowOffset];
        if (length <= header.Length)
        {
            return header;
        }

        var response = new byte[length];
        Buffer.BlockCopy(header, 0, response, 0, header.Length);
        await ReadExactlyAsync(stream, response, header.Length, length - header.Length);
        return response;
    }

    /// <summary>Reads exactly the requested number of bytes.</summary>
    /// <param name="stream">The connected network stream.</param>
    /// <param name="buffer">The destination buffer.</param>
    /// <param name="offset">The destination offset.</param>
    /// <param name="count">The number of bytes to read.</param>
    /// <returns>A task representing the asynchronous read.</returns>
    private static async Task ReadExactlyAsync(NetworkStream stream, byte[] buffer, int offset, int count)
    {
        var total = 0;
        while (total < count)
        {
            var read = await AsyncCompatibility.WaitAsync(
                NetworkCompatibility.ReadAsync(stream, buffer, offset + total, count - total),
                TimeSpan.FromSeconds(OperationTimeoutSeconds));
            if (read == 0)
            {
                throw new EndOfStreamException("The simulator closed the connection.");
            }

            total += read;
        }
    }
}
