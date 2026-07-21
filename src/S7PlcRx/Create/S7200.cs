// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive;
#else
namespace S7PlcRx;
#endif

/// <summary>Creates connections to Siemens S7-200 PLC devices.</summary>
public static class S7200
{
    /// <summary>Creates an S7-200 connection with standard polling.</summary>
    /// <param name="ip">The PLC IP address.</param>
    /// <param name="rack">The PLC rack number.</param>
    /// <param name="slot">The PLC CPU slot.</param>
    /// <returns>The configured PLC connection.</returns>
    public static IRxS7 Create(string ip, short rack, short slot) =>
        Create(ip, rack, slot, new S7PollingOptions(), null);

    /// <summary>Creates an S7-200 connection with explicit settings.</summary>
    /// <param name="ip">The PLC IP address.</param>
    /// <param name="rack">The PLC rack number.</param>
    /// <param name="slot">The PLC CPU slot.</param>
    /// <param name="polling">The polling configuration.</param>
    /// <param name="watchdog">The optional watchdog configuration.</param>
    /// <returns>The configured PLC connection.</returns>
    public static IRxS7 Create(
        string ip,
        short rack,
        short slot,
        S7PollingOptions polling,
        S7WatchdogOptions? watchdog) =>
        new RxS7(new RxS7Options(
            new S7ConnectionOptions(Enums.CpuType.S7200, ip, rack, slot),
            polling,
            watchdog));
}
