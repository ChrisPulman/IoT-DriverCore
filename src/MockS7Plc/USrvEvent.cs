// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace IoT.DriverCore.S7PlcRx.Mock;

/// <summary>Represents a Snap7 server event payload.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct USrvEvent
{
    /// <summary>Gets the native event timestamp value.</summary>
    public nint EvtTime { get; }

    /// <summary>Gets the event sender identifier.</summary>
    public int EvtSender { get; }

    /// <summary>Gets the event code.</summary>
    public uint EvtCode { get; }

    /// <summary>Gets the event return code.</summary>
    public ushort EvtRetCode { get; }

    /// <summary>Gets the first event parameter.</summary>
    public ushort EvtParam1 { get; }

    /// <summary>Gets the second event parameter.</summary>
    public ushort EvtParam2 { get; }

    /// <summary>Gets the third event parameter.</summary>
    public ushort EvtParam3 { get; }

    /// <summary>Gets the fourth event parameter.</summary>
    public ushort EvtParam4 { get; }
}
