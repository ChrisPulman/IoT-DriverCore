// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive;
#else
namespace S7PlcRx;
#endif

/// <summary>Creates connections to Siemens S7-1500 PLC devices.</summary>
public static class S71500
{
    /// <summary>The maximum supported rack.</summary>
    private const short MaximumRack = 7;

    /// <summary>The maximum supported CPU slot.</summary>
    private const short MaximumSlot = 31;

    /// <summary>Creates an S7-1500 connection with standard settings.</summary>
    /// <param name="ip">The PLC IP address.</param>
    /// <returns>The configured PLC connection.</returns>
    public static IRxS7 Create(string ip) =>
        Create(ip, 0, 1, new S7PollingOptions(), null);

    /// <summary>Creates an S7-1500 connection with an explicit polling interval.</summary>
    /// <param name="ip">The PLC IP address.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>The configured PLC connection.</returns>
    public static IRxS7 Create(string ip, double interval) =>
        Create(ip, 0, 1, new S7PollingOptions(interval), null);

    /// <summary>Creates an S7-1500 connection at a rack and slot with standard polling.</summary>
    /// <param name="ip">The PLC IP address.</param>
    /// <param name="rack">The PLC rack number.</param>
    /// <param name="slot">The PLC CPU slot.</param>
    /// <returns>The configured PLC connection.</returns>
    public static IRxS7 Create(string ip, short rack, short slot) =>
        Create(ip, rack, slot, new S7PollingOptions(), null);

    /// <summary>Creates an S7-1500 connection with legacy scalar settings.</summary>
    /// <param name="ip">The PLC IP address.</param>
    /// <param name="rack">The PLC rack number.</param>
    /// <param name="slot">The PLC CPU slot.</param>
    /// <param name="watchDogAddress">The optional watchdog address.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>The configured PLC connection.</returns>
    public static IRxS7 Create(
        string ip,
        short rack,
        short slot,
        string? watchDogAddress,
        double interval) =>
        Create(
            ip,
            rack,
            slot,
            new S7PollingOptions(interval),
            watchDogAddress is null ? null : new S7WatchdogOptions(watchDogAddress));

    /// <summary>Creates an S7-1500 connection with explicit settings.</summary>
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
        S7WatchdogOptions? watchdog)
    {
        ValidateLocation(rack, slot);
        return new RxS7(new RxS7Options(
            new S7ConnectionOptions(Enums.CpuType.S71500, ip, rack, slot),
            polling,
            watchdog));
    }

    /// <summary>Validates a rack and CPU slot.</summary>
    /// <param name="rack">The rack number.</param>
    /// <param name="slot">The CPU slot.</param>
    private static void ValidateLocation(short rack, short slot)
    {
        _ = rack is < 0 or > MaximumRack
            ? throw new ArgumentOutOfRangeException(nameof(rack), "Rack must be between 0 and 7")
            : rack;
        _ = slot is < 1 or > MaximumSlot
            ? throw new ArgumentOutOfRangeException(nameof(slot), "Slot must be between 1 and 31")
            : slot;
    }
}
