// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiCommands type.</summary>
public static class MitsubishiCommands
{
    /// <summary>Stores the DeviceRead field.</summary>
    public static readonly ushort DeviceRead = MitsubishiCommandCodes.DeviceRead;

    /// <summary>Stores the DeviceWrite field.</summary>
    public static readonly ushort DeviceWrite = MitsubishiCommandCodes.DeviceWrite;

    /// <summary>Stores the RandomRead field.</summary>
    public static readonly ushort RandomRead = MitsubishiCommandCodes.RandomRead;

    /// <summary>Stores the RandomWrite field.</summary>
    public static readonly ushort RandomWrite = MitsubishiCommandCodes.RandomWrite;

    /// <summary>Stores the BlockRead field.</summary>
    public static readonly ushort BlockRead = MitsubishiCommandCodes.BlockRead;

    /// <summary>Stores the BlockWrite field.</summary>
    public static readonly ushort BlockWrite = MitsubishiCommandCodes.BlockWrite;

    /// <summary>Stores the EntryMonitorDevice field.</summary>
    public static readonly ushort EntryMonitorDevice = MitsubishiCommandCodes.EntryMonitorDevice;

    /// <summary>Stores the ExecuteMonitor field.</summary>
    public static readonly ushort ExecuteMonitor = MitsubishiCommandCodes.ExecuteMonitor;

    /// <summary>Stores the ExtendUnitRead field.</summary>
    public static readonly ushort ExtendUnitRead = MitsubishiCommandCodes.ExtendUnitRead;

    /// <summary>Stores the ExtendUnitWrite field.</summary>
    public static readonly ushort ExtendUnitWrite = MitsubishiCommandCodes.ExtendUnitWrite;

    /// <summary>Stores the MemoryRead field.</summary>
    public static readonly ushort MemoryRead = MitsubishiCommandCodes.MemoryRead;

    /// <summary>Stores the MemoryWrite field.</summary>
    public static readonly ushort MemoryWrite = MitsubishiCommandCodes.MemoryWrite;

    /// <summary>Stores the ReadTypeName field.</summary>
    public static readonly ushort ReadTypeName = MitsubishiCommandCodes.ReadTypeName;

    /// <summary>Stores the RemoteRun field.</summary>
    public static readonly ushort RemoteRun = MitsubishiCommandCodes.RemoteRun;

    /// <summary>Stores the RemoteStop field.</summary>
    public static readonly ushort RemoteStop = MitsubishiCommandCodes.RemoteStop;

    /// <summary>Stores the RemotePause field.</summary>
    public static readonly ushort RemotePause = MitsubishiCommandCodes.RemotePause;

    /// <summary>Stores the RemoteLatchClear field.</summary>
    public static readonly ushort RemoteLatchClear = MitsubishiCommandCodes.RemoteLatchClear;

    /// <summary>Stores the RemoteReset field.</summary>
    public static readonly ushort RemoteReset = MitsubishiCommandCodes.RemoteReset;

    /// <summary>Stores the Unlock field.</summary>
    public static readonly ushort Unlock = MitsubishiCommandCodes.Unlock;

    /// <summary>Stores the Lock field.</summary>
    public static readonly ushort Lock = MitsubishiCommandCodes.Lock;

    /// <summary>Stores the LoopbackTest field.</summary>
    public static readonly ushort LoopbackTest = MitsubishiCommandCodes.LoopbackTest;

    /// <summary>Stores the ClearError field.</summary>
    public static readonly ushort ClearError = MitsubishiCommandCodes.ClearError;
}
