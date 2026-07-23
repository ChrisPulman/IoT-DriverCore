// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace IoT.DriverCore.S7PlcRx.Mock;

/// <summary>Hosts a managed or Snap7-backed mock PLC server for tests and local development.</summary>
public class MockServer : IDisposable
{
    /// <summary>The loopback address used for local server binding.</summary>
    public static readonly string Localhost = "127.0.0.1";

    /// <summary>The Snap7 code for the PE input area.</summary>
    public static readonly int SrvAreaPe;

    /// <summary>The Snap7 code for the PA output area.</summary>
    public static readonly int SrvAreaPa = 1;

    /// <summary>The Snap7 code for the MK memory area.</summary>
    public static readonly int SrvAreaMk = 2;

    /// <summary>The Snap7 code for the CT counter area.</summary>
    public static readonly int SrvAreaCt = 3;

    /// <summary>The Snap7 code for the TM timer area.</summary>
    public static readonly int SrvAreaTm = 4;

    /// <summary>The Snap7 code for the DB area.</summary>
    public static readonly int SrvAreaDB = 5;

    /// <summary>Event mask for incoming PDU notifications.</summary>
    public static readonly uint EvcPdUincoming = 0x00010000;

    /// <summary>Event mask for data read notifications.</summary>
    public static readonly uint EvcDataRead = 0x00020000;

    /// <summary>Event mask for data write notifications.</summary>
    public static readonly uint EvcDataWrite = 0x00040000;

    /// <summary>Event mask for PDU negotiation notifications.</summary>
    public static readonly uint EvcNegotiatePdu = 0x00080000;

    /// <summary>Event mask for SZL read notifications.</summary>
    public static readonly uint EvcReadSzl = 0x00100000;

    /// <summary>Event mask for clock access notifications.</summary>
    public static readonly uint EvcClock = 0x00200000;

    /// <summary>Event mask for upload notifications.</summary>
    public static readonly uint EvcUpload = 0x00400000;

    /// <summary>Event mask for download notifications.</summary>
    public static readonly uint EvcDownload = 0x00800000;

    /// <summary>Event mask for directory access notifications.</summary>
    public static readonly uint EvcDirectory = 0x01000000;

    /// <summary>Event mask for security notifications.</summary>
    public static readonly uint EvcSecurity = 0x02000000;

    /// <summary>Event mask for control notifications.</summary>
    public static readonly uint EvcControl = 0x04000000;

    /// <summary>The Snap7 event-mask selector.</summary>
    private const int EventMaskKind = 0;

    /// <summary>The Snap7 log-mask selector.</summary>
    private const int LogMaskKind = 1;

    /// <summary>The Snap7 PE input area code.</summary>
    private const int PeAreaCode = 0;

    /// <summary>The Snap7 PA output area code.</summary>
    private const int PaAreaCode = 1;

    /// <summary>The Snap7 MK memory area code.</summary>
    private const int MkAreaCode = 2;

    /// <summary>The Snap7 CT counter area code.</summary>
    private const int CtAreaCode = 3;

    /// <summary>The Snap7 TM timer area code.</summary>
    private const int TmAreaCode = 4;

    /// <summary>The Snap7 DB area code.</summary>
    private const int DbAreaCode = 5;

    /// <summary>Holds the pinned handles for registered server areas.</summary>
    private readonly Dictionary<int, GCHandle> _areaHandles;

    /// <summary>The selected implementation.</summary>
    private readonly S7ServerBackend _backend;

    /// <summary>The managed server when that backend is selected.</summary>
    private readonly ManagedS7Server? _managedServer;

    /// <summary>Stores the managed log mask.</summary>
    private uint _managedLogMask;

    /// <summary>Stores the managed event mask.</summary>
    private uint _managedEventMask = uint.MaxValue;

    /// <summary>Stores the default DB1 backing area.</summary>
    private byte[]? _defaultDb1;

    /// <summary>Stores the default PE backing area.</summary>
    private byte[]? _defaultPe;

    /// <summary>Stores the default PA backing area.</summary>
    private byte[]? _defaultPa;

    /// <summary>Stores the default MK backing area.</summary>
    private byte[]? _defaultMk;

    /// <summary>Stores the default CT backing area.</summary>
    private byte[]? _defaultCt;

    /// <summary>Stores the default TM backing area.</summary>
    private byte[]? _defaultTm;

    /// <summary>Holds the native Snap7 server handle.</summary>
    private nint _server;

    /// <summary>Tracks whether disposal has already run.</summary>
    private bool _disposedValue;

    /// <summary>Initializes a new instance of the <see cref="MockServer"/> class.</summary>
    public MockServer()
        : this(S7ServerBackend.Managed)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MockServer"/> class.</summary>
    /// <param name="backend">The server implementation.</param>
    public MockServer(S7ServerBackend backend)
    {
        _backend = backend;
        if (backend == S7ServerBackend.Managed)
        {
            _managedServer = new();
        }
        else
        {
            _server = NativeMethods.Srv_Create();
        }

        _areaHandles = [];
    }

    /// <summary>Finalizes an instance of the <see cref="MockServer"/> class.</summary>
    ~MockServer()
    {
        Dispose(false);
    }

    /// <summary>Gets or sets the log mask.</summary>
    /// <value>
    /// The log mask.
    /// </value>
    public uint LogMask
    {
        get
        {
            if (_managedServer is not null)
            {
                return _managedLogMask;
            }

            var mask = default(uint);
            return NativeMethods.Srv_GetMask(_server, LogMaskKind, ref mask) == 0 ? mask : 0;
        }

        set
        {
            if (_managedServer is not null)
            {
                _managedLogMask = value;
                return;
            }

            _ = NativeMethods.Srv_SetMask(_server, LogMaskKind, value);
        }
    }

    /// <summary>Gets or sets the event mask.</summary>
    /// <value>
    /// The event mask.
    /// </value>
    public uint EventMask
    {
        get
        {
            if (_managedServer is not null)
            {
                return _managedEventMask;
            }

            var mask = default(uint);
            return NativeMethods.Srv_GetMask(_server, EventMaskKind, ref mask) == 0 ? mask : 0;
        }
        set
        {
            if (_managedServer is not null)
            {
                _managedEventMask = value;
                return;
            }

            _ = NativeMethods.Srv_SetMask(_server, EventMaskKind, value);
        }
    }

    /// <summary>Gets the selected server backend.</summary>
    public S7ServerBackend Backend => _backend;

    /// <summary>Gets the managed server, or <see langword="null"/> for the Snap7 backend.</summary>
    public ManagedS7Server? ManagedServer => _managedServer;

    /// <summary>Gets the managed memory, or <see langword="null"/> for the Snap7 backend.</summary>
    public ManagedS7Memory? Memory => _managedServer?.Memory;

    /// <summary>Gets the default Data Block 1 backing store (byte-addressable).</summary>
    public byte[]? DefaultDb1 => _defaultDb1;

    /// <summary>Gets or sets the size (in bytes) of the default DB1 area registered on start.</summary>
    public int DefaultDb1Size { get; set; } = 4096;

    /// <summary>Gets or sets the size (in bytes) of the default PE (Inputs) area registered on start.</summary>
    public int DefaultPeSize { get; set; } = 4096;

    /// <summary>Gets or sets the size (in bytes) of the default PA (Outputs) area registered on start.</summary>
    public int DefaultPaSize { get; set; } = 4096;

    /// <summary>Gets or sets the size (in bytes) of the default MK (Memory) area registered on start.</summary>
    public int DefaultMkSize { get; set; } = 4096;

    /// <summary>Gets or sets the size (in bytes) of the default CT (Counters) area registered on start.</summary>
    public int DefaultCtSize { get; set; } = 512;

    /// <summary>Gets or sets the size (in bytes) of the default TM (Timers) area registered on start.</summary>
    public int DefaultTmSize { get; set; } = 512;

    /// <summary>Gets or sets the virtual CPU status.</summary>
    public int CpuStatus
    {
        get => _managedServer is not null ? _managedServer.CpuStatus : GetNativeCpuStatus();

        set
        {
            if (_managedServer is not null)
            {
                _managedServer.CpuStatus = value;
                return;
            }

            _ = NativeMethods.Srv_SetCpuStatus(_server, value);
        }
    }

    /// <summary>Gets the current server status.</summary>
    public int ServerStatus => _managedServer is not null
        ? Convert.ToInt32(_managedServer.IsRunning)
        : GetNativeServerStatus();

    /// <summary>Gets the number of connected clients.</summary>
    public int ClientsCount
    {
        get
        {
            if (_managedServer is not null)
            {
                return _managedServer.ClientsCount;
            }

            var serverStatus = default(int);
            var cpuStatus = default(int);
            var clientCount = default(int);

            var result = NativeMethods.Srv_GetStatus(
                _server,
                ref serverStatus,
                ref cpuStatus,
                ref clientCount);
            return result == 0 ? clientCount : -1;
        }
    }

    /// <summary>Converts an event to the Snap7 display text.</summary>
    /// <param name="event">The event.</param>
    /// <returns>The formatted event text.</returns>
    public static string EventText(ref USrvEvent @event) => NativeMethods.GetEventText(ref @event);

    /// <summary>Converts a native event timestamp to a <see cref="DateTimeOffset"/>.</summary>
    /// <param name="timeStamp">The native timestamp value.</param>
    /// <returns>The converted <see cref="DateTimeOffset"/>.</returns>
    public static DateTimeOffset EvtTimeToDateTime(nint timeStamp) =>
        DateTimeOffset.FromUnixTimeSeconds((long)timeStamp);

    /// <summary>Converts an error code to the Snap7 display text.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The formatted error text.</returns>
    public static string ErrorText(int error) => NativeMethods.GetErrorText(error);

    /// <summary>Starts the server on the specified address.</summary>
    /// <param name="address">The address.</param>
    /// <returns>The Snap7 result code.</returns>
    public int StartTo(string address)
    {
        EnsureDefaultAreasRegistered();
        if (_managedServer is not null)
        {
            try
            {
                _managedServer.Start(address);
                return 0;
            }
            catch (SocketException ex)
            {
                return ex.ErrorCode == 0 ? 1 : ex.ErrorCode;
            }
        }

        return NativeMethods.Srv_StartTo(_server, address);
    }

    /// <summary>Starts the server using the default address configuration.</summary>
    /// <returns>The Snap7 result code.</returns>
    public int Start()
    {
        EnsureDefaultAreasRegistered();
        return _managedServer is not null ? StartTo(Localhost) : NativeMethods.Srv_Start(_server);
    }

    /// <summary>Stops the server.</summary>
    /// <returns>The Snap7 result code.</returns>
    public int Stop()
    {
        if (_managedServer is not null)
        {
            _managedServer.Stop();
            return 0;
        }

        return NativeMethods.Srv_Stop(_server);
    }

    /// <summary>Registers a structured backing store with the server.</summary>
    /// <typeparam name="T">The backing store type.</typeparam>
    /// <param name="areaCode">The area code.</param>
    /// <param name="index">The index.</param>
    /// <param name="userData">The pinned backing store.</param>
    /// <param name="size">The size.</param>
    /// <returns>The Snap7 result code.</returns>
    public int RegisterArea<T>(int areaCode, int index, ref T userData, int size)
    {
        if (typeof(T) == typeof(byte))
        {
            throw new ArgumentException(
                "Use RegisterArea(int areaCode, int index, byte[] userData, int size) for byte areas.",
                nameof(userData));
        }

        if (_managedServer is not null)
        {
            var buffer = new byte[size];
            var managedHandle = GCHandle.Alloc(userData, GCHandleType.Pinned);
            try
            {
                Marshal.Copy(managedHandle.AddrOfPinnedObject(), buffer, 0, size);
            }
            finally
            {
                managedHandle.Free();
            }

            _managedServer.Memory.Register(MapArea(areaCode), checked((ushort)index), buffer);
            return 0;
        }

        var areaUid = (areaCode << 16) + index;
        var handle = GCHandle.Alloc(userData, GCHandleType.Pinned);
        var result = NativeMethods.Srv_RegisterArea(_server, areaCode, index, handle.AddrOfPinnedObject(), size);
        if (result == 0)
        {
            _areaHandles.Add(areaUid, handle);
        }
        else
        {
            handle.Free();
        }

        return result;
    }

    /// <summary>Registers the area using a byte-array backing store.</summary>
    /// <param name="areaCode">The area code.</param>
    /// <param name="index">The index.</param>
    /// <param name="userData">The area backing buffer.</param>
    /// <param name="size">The size.</param>
    /// <returns>The Snap7 result code.</returns>
    public int RegisterArea(int areaCode, int index, byte[] userData, int size)
    {
        if (userData is null)
        {
            throw new ArgumentNullException(nameof(userData));
        }

        if (size < 0 || size > userData.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        if (_managedServer is not null)
        {
            var buffer = userData;
            if (size != userData.Length)
            {
                buffer = new byte[size];
                Buffer.BlockCopy(userData, 0, buffer, 0, size);
            }

            _managedServer.Memory.Register(MapArea(areaCode), checked((ushort)index), buffer);
            return 0;
        }

        var areaUid = (areaCode << 16) + index;
        var handle = GCHandle.Alloc(userData, GCHandleType.Pinned);
        var result = NativeMethods.Srv_RegisterArea(_server, areaCode, index, handle.AddrOfPinnedObject(), size);
        if (result == 0)
        {
            _areaHandles.Add(areaUid, handle);
        }
        else
        {
            handle.Free();
        }

        return result;
    }

    /// <summary>Unregisters an area from the server.</summary>
    /// <param name="areaCode">The area code.</param>
    /// <param name="index">The index.</param>
    /// <returns>The Snap7 result code.</returns>
    public int UnregisterArea(int areaCode, int index)
    {
        if (_managedServer is not null)
        {
            return _managedServer.Memory.Unregister(MapArea(areaCode), checked((ushort)index)) ? 0 : 1;
        }

        var result = NativeMethods.Srv_UnregisterArea(_server, areaCode, index);
        if (result == 0)
        {
            var areaUid = (areaCode << 16) + index;
            if (_areaHandles.TryGetValue(areaUid, out var handle))
            {
                if (handle.IsAllocated)
                {
                    handle.Free();
                }

                _ = _areaHandles.Remove(areaUid);
            }
        }

        return result;
    }

    /// <summary>Locks a registered area.</summary>
    /// <param name="areaCode">The area code.</param>
    /// <param name="index">The index.</param>
    /// <returns>The Snap7 result code.</returns>
    public int LockArea(int areaCode, int index) =>
        _managedServer is not null ? 0 : NativeMethods.Srv_LockArea(_server, areaCode, index);

    /// <summary>Unlocks a registered area.</summary>
    /// <param name="areaCode">The area code.</param>
    /// <param name="index">The index.</param>
    /// <returns>The Snap7 result code.</returns>
    public int UnlockArea(int areaCode, int index) =>
        _managedServer is not null ? 0 : NativeMethods.Srv_UnlockArea(_server, areaCode, index);

    /// <summary>Sets the event callback.</summary>
    /// <param name="callback">The callback.</param>
    /// <param name="usrPtr">The user pointer.</param>
    /// <returns>The Snap7 result code.</returns>
    public int SetEventsCallBack(SrvCallback callback, nint usrPtr) =>
        _managedServer is not null ? 0 : NativeMethods.Srv_SetEventsCallback(_server, callback, usrPtr);

    /// <summary>Sets the read-event callback.</summary>
    /// <param name="callback">The callback.</param>
    /// <param name="usrPtr">The user pointer.</param>
    /// <returns>The Snap7 result code.</returns>
    public int SetReadEventsCallBack(SrvCallback callback, nint usrPtr) =>
        _managedServer is not null ? 0 : NativeMethods.Srv_SetReadEventsCallback(_server, callback, usrPtr);

    /// <summary>Sets the read/write area callback.</summary>
    /// <param name="callback">The callback.</param>
    /// <param name="usrPtr">The user pointer.</param>
    /// <returns>The Snap7 result code.</returns>
    public int SetRwAreaCallBack(SrvRwAreaCallback callback, nint usrPtr) =>
        _managedServer is not null ? 0 : NativeMethods.Srv_SetRWAreaCallback(_server, callback, usrPtr);

    /// <summary>Retrieves the next queued event.</summary>
    /// <param name="event">The event.</param>
    /// <returns><see langword="true"/> when an event was returned.</returns>
    public bool PickEvent(ref USrvEvent @event)
    {
        if (_managedServer is not null)
        {
            return false;
        }

        var evtReady = default(int);
        return NativeMethods.Srv_PickEvent(_server, ref @event, ref evtReady) != 0 ? false : evtReady != 0;
    }

    /// <summary>Clears the pending server events.</summary>
    /// <returns>The Snap7 result code.</returns>
    public int ClearEvents() => _managedServer is not null ? 0 : NativeMethods.Srv_ClearEvents(_server);

    /// <summary>Disposes this server.</summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases unmanaged and - optionally - managed resources.</summary>
    /// <param name="disposing">
    /// <c>true</c> to release both managed and unmanaged resources; otherwise, <c>false</c>.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue)
        {
            return;
        }

        if (disposing)
        {
            _ = ClearEvents();
            _ = Stop();
        }

        foreach (var item in _areaHandles)
        {
            var handle = item.Value;
            if (handle.IsAllocated)
            {
                handle.Free();
            }
        }

        if (_managedServer is not null)
        {
            _managedServer.Dispose();
        }
        else
        {
            _ = NativeMethods.Srv_Destroy(ref _server);
        }

        _disposedValue = true;
    }

    /// <summary>Maps a Snap7 server area code to its S7ANY wire-area value.</summary>
    /// <param name="areaCode">The Snap7 server area code.</param>
    /// <returns>The corresponding S7ANY wire-area value.</returns>
    private static S7MemoryArea MapArea(int areaCode) => areaCode switch
    {
        PeAreaCode => S7MemoryArea.Input,
        PaAreaCode => S7MemoryArea.Output,
        MkAreaCode => S7MemoryArea.Memory,
        CtAreaCode => S7MemoryArea.Counter,
        TmAreaCode => S7MemoryArea.Timer,
        DbAreaCode => S7MemoryArea.DataBlock,
        _ => throw new ArgumentOutOfRangeException(nameof(areaCode)),
    };

    /// <summary>Gets the virtual CPU status from the native Snap7 server.</summary>
    /// <returns>The CPU status, or <c>-1</c> when Snap7 reports an error.</returns>
    private int GetNativeCpuStatus()
    {
        var cpuStatus = default(int);
        var serverStatus = default(int);
        var clientCount = default(int);

        var result = NativeMethods.Srv_GetStatus(
            _server,
            ref serverStatus,
            ref cpuStatus,
            ref clientCount);
        return result == 0 ? cpuStatus : -1;
    }

    /// <summary>Gets the running status from the native Snap7 server.</summary>
    /// <returns>The server status, or <c>-1</c> when Snap7 reports an error.</returns>
    private int GetNativeServerStatus()
    {
        var cpuStatus = default(int);
        var serverStatus = default(int);
        var clientCount = default(int);

        var result = NativeMethods.Srv_GetStatus(
            _server,
            ref serverStatus,
            ref cpuStatus,
            ref clientCount);
        return result == 0 ? serverStatus : -1;
    }

    /// <summary>Creates the default areas required for standard PLC address access.</summary>
    private void EnsureDefaultAreasRegistered()
    {
        if (_defaultDb1 is not null)
        {
            return;
        }

        static int NormalizeSize(int size) => size < 1 ? 1 : size;

        DefaultDb1Size = NormalizeSize(DefaultDb1Size);
        DefaultPeSize = NormalizeSize(DefaultPeSize);
        DefaultPaSize = NormalizeSize(DefaultPaSize);
        DefaultMkSize = NormalizeSize(DefaultMkSize);
        DefaultCtSize = NormalizeSize(DefaultCtSize);
        DefaultTmSize = NormalizeSize(DefaultTmSize);

        _defaultDb1 = new byte[DefaultDb1Size];
        _defaultPe = new byte[DefaultPeSize];
        _defaultPa = new byte[DefaultPaSize];
        _defaultMk = new byte[DefaultMkSize];
        _defaultCt = new byte[DefaultCtSize];
        _defaultTm = new byte[DefaultTmSize];

        // Register DB1 so Snap7 can service ReadVar/WriteVar (including multi-item)
        // against a real backing store.
        _ = RegisterArea(SrvAreaDB, 1, _defaultDb1, _defaultDb1.Length);

        // Register the standard non-DB areas so IB/QB/MB and bit addressing can be used in tests.
        _ = RegisterArea(SrvAreaPe, 0, _defaultPe, _defaultPe.Length);
        _ = RegisterArea(SrvAreaPa, 0, _defaultPa, _defaultPa.Length);
        _ = RegisterArea(SrvAreaMk, 0, _defaultMk, _defaultMk.Length);
        _ = RegisterArea(SrvAreaCt, 0, _defaultCt, _defaultCt.Length);
        _ = RegisterArea(SrvAreaTm, 0, _defaultTm, _defaultTm.Length);
    }
}
