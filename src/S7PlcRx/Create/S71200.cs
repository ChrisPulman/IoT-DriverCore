// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive;
#else
namespace S7PlcRx;
#endif

/// <summary>Creates connections to Siemens S7-1200 PLC devices.</summary>
public static class S71200
{
    /// <summary>The maximum supported rack.</summary>
    private const short MaximumRack = 7;

    /// <summary>Creates an S7-1200 connection with standard settings.</summary>
    /// <param name="ip">The PLC IP address.</param>
    /// <returns>The configured PLC connection.</returns>
    public static IRxS7 Create(string ip) =>
        Create(ip, 0, new S7PollingOptions(), null);

    /// <summary>Creates an S7-1200 connection for a rack with standard settings.</summary>
    /// <param name="ip">The PLC IP address.</param>
    /// <param name="rack">The PLC rack number.</param>
    /// <returns>The configured PLC connection.</returns>
    public static IRxS7 Create(string ip, short rack) =>
        Create(ip, rack, new S7PollingOptions(), null);

    /// <summary>Creates an S7-1200 connection with explicit settings.</summary>
    /// <param name="ip">The PLC IP address.</param>
    /// <param name="rack">The PLC rack number.</param>
    /// <param name="polling">The polling configuration.</param>
    /// <param name="watchdog">The optional watchdog configuration.</param>
    /// <returns>The configured PLC connection.</returns>
    public static IRxS7 Create(
        string ip,
        short rack,
        S7PollingOptions polling,
        S7WatchdogOptions? watchdog)
    {
        ValidateRack(rack);
        return new RxS7(new RxS7Options(
            new S7ConnectionOptions(Enums.CpuType.S71200, ip, rack, 1),
            polling,
            watchdog));
    }

    /// <summary>Validates a rack number.</summary>
    /// <param name="rack">The rack number.</param>
    private static void ValidateRack(short rack)
    {
        _ = rack is < 0 or > MaximumRack
            ? throw new ArgumentOutOfRangeException(nameof(rack), "Rack must be between 0 and 7")
            : rack;
    }
}
