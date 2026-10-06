// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
namespace IoT.Driver.S7PlcRx.Symbolic;
#endif

/// <summary>Contains a DTL date, nanoseconds within its second, and the PLC structure metadata.</summary>
public sealed class S7SymbolicDtl
{
    /// <summary>The greatest valid fractional second in nanoseconds.</summary>
    private const uint MaximumNanoseconds = 999_999_999;

    /// <summary>Initializes a new instance of the <see cref="S7SymbolicDtl"/> class.</summary>
    /// <param name="dateTime">The calendar time at whole-second precision.</param>
    /// <param name="nanoseconds">The fractional second from zero through 999,999,999 nanoseconds.</param>
    /// <param name="interfaceTimestamp">The PLC interface timestamp.</param>
    public S7SymbolicDtl(DateTimeOffset dateTime, uint nanoseconds, ulong interfaceTimestamp)
        : this(dateTime, nanoseconds, interfaceTimestamp, 0)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="S7SymbolicDtl"/> class.</summary>
    /// <param name="dateTime">The calendar time at whole-second precision.</param>
    /// <param name="nanoseconds">The fractional second from zero through 999,999,999 nanoseconds.</param>
    /// <param name="interfaceTimestamp">The PLC interface timestamp.</param>
    /// <param name="transportFlags">The packed structure transport flags.</param>
    public S7SymbolicDtl(DateTimeOffset dateTime, uint nanoseconds, ulong interfaceTimestamp, uint transportFlags)
    {
        if (nanoseconds > MaximumNanoseconds || dateTime.Ticks % TimeSpan.TicksPerSecond != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nanoseconds));
        }

        DateTime = dateTime;
        Nanoseconds = nanoseconds;
        InterfaceTimestamp = interfaceTimestamp;
        TransportFlags = transportFlags;
    }

    /// <summary>Gets the calendar time to whole-second precision.</summary>
    public DateTimeOffset DateTime { get; }

    /// <summary>Gets the nanoseconds within the second.</summary>
    public uint Nanoseconds { get; }

    /// <summary>Gets the PLC interface timestamp used for writing the packed structure.</summary>
    public ulong InterfaceTimestamp { get; }

    /// <summary>Gets the packed structure transport flags.</summary>
    public uint TransportFlags { get; }
}
