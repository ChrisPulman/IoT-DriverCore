// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Provides compile-time command codes for protocol dispatch.</summary>
internal static class MitsubishiCommandCodes
{
    /// <summary>Reads a device.</summary>
    internal const ushort DeviceRead = 0x0401;

    /// <summary>Writes a device.</summary>
    internal const ushort DeviceWrite = 0x1401;

    /// <summary>Reads random devices.</summary>
    internal const ushort RandomRead = 0x0403;

    /// <summary>Writes random devices.</summary>
    internal const ushort RandomWrite = 0x1402;

    /// <summary>Reads device blocks.</summary>
    internal const ushort BlockRead = 0x0406;

    /// <summary>Writes device blocks.</summary>
    internal const ushort BlockWrite = 0x1406;

    /// <summary>Registers monitor devices.</summary>
    internal const ushort EntryMonitorDevice = 0x0801;

    /// <summary>Executes a registered monitor.</summary>
    internal const ushort ExecuteMonitor = 0x0802;

    /// <summary>Reads an extended unit.</summary>
    internal const ushort ExtendUnitRead = 0x0601;

    /// <summary>Writes an extended unit.</summary>
    internal const ushort ExtendUnitWrite = 0x1601;

    /// <summary>Reads PLC memory.</summary>
    internal const ushort MemoryRead = 0x0613;

    /// <summary>Writes PLC memory.</summary>
    internal const ushort MemoryWrite = 0x1613;

    /// <summary>Reads the controller type name.</summary>
    internal const ushort ReadTypeName = 0x0101;

    /// <summary>Runs the controller remotely.</summary>
    internal const ushort RemoteRun = 0x1001;

    /// <summary>Stops the controller remotely.</summary>
    internal const ushort RemoteStop = 0x1002;

    /// <summary>Pauses the controller remotely.</summary>
    internal const ushort RemotePause = 0x1003;

    /// <summary>Clears remote latches.</summary>
    internal const ushort RemoteLatchClear = 0x1005;

    /// <summary>Resets the controller remotely.</summary>
    internal const ushort RemoteReset = 0x1006;

    /// <summary>Unlocks the controller.</summary>
    internal const ushort Unlock = 0x1630;

    /// <summary>Locks the controller.</summary>
    internal const ushort Lock = 0x1631;

    /// <summary>Executes a loopback test.</summary>
    internal const ushort LoopbackTest = 0x0619;

    /// <summary>Clears the controller error.</summary>
    internal const ushort ClearError = 0x1617;
}
