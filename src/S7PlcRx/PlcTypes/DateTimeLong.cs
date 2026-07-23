// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using SystemDateTime = System.DateTime;
using SystemDateTimeOffset = System.DateTimeOffset;
using SystemTimeSpan = System.TimeSpan;

#if REACTIVE_SHIM
namespace IoT.DriverCore.S7PlcRx.Reactive.PlcTypes;
#else
namespace IoT.DriverCore.S7PlcRx.PlcTypes;
#endif

/// <summary>Converts offset-aware values to and from the S7 DateTimeLong representation.</summary>
public static class DateTimeLong
{
    /// <summary>The type length in bytes.</summary>
    public static readonly int TypeLengthInBytes = 12;

    /// <summary>The minimum value supported by the specification.</summary>
    public static readonly SystemDateTimeOffset SpecMinimumDateTime =
        new(1970, 1, 1, 0, 0, 0, SystemTimeSpan.Zero);

    /// <summary>The maximum value supported by the specification.</summary>
    public static readonly SystemDateTimeOffset SpecMaximumDateTime =
        new(2262, 4, 11, 23, 47, 16, 854, SystemTimeSpan.Zero);

    /// <summary>The allocation size above which array pooling is used.</summary>
    private const int ArrayPoolThresholdInBytes = 1024;

    /// <summary>The encoded year width in bytes.</summary>
    private const int YearLengthInBytes = sizeof(ushort);

    /// <summary>The byte offset containing the month.</summary>
    private const int MonthOffset = 2;

    /// <summary>The byte offset containing the day.</summary>
    private const int DayOffset = 3;

    /// <summary>The byte offset containing the weekday.</summary>
    private const int WeekdayOffset = 4;

    /// <summary>The byte offset containing the hour.</summary>
    private const int HourOffset = 5;

    /// <summary>The byte offset containing the minute.</summary>
    private const int MinuteOffset = 6;

    /// <summary>The byte offset containing the second.</summary>
    private const int SecondOffset = 7;

    /// <summary>The byte offset at which the nanosecond field begins.</summary>
    private const int NanosecondsOffset = 8;

    /// <summary>The encoded nanosecond field width in bytes.</summary>
    private const int NanosecondsLengthInBytes = sizeof(uint);

    /// <summary>The number of nanoseconds represented by one .NET tick.</summary>
    private const int NanosecondsPerTick = 100;

    /// <summary>The largest nanosecond value within one second.</summary>
    private const uint MaximumNanoseconds = 999_999_999U;

    /// <summary>The largest valid month value.</summary>
    private const byte MaximumMonth = 12;

    /// <summary>The largest valid day value in the wire field.</summary>
    private const byte MaximumDay = 31;

    /// <summary>The largest valid hour value.</summary>
    private const byte MaximumHour = 23;

    /// <summary>The largest valid minute or second value.</summary>
    private const byte MaximumMinuteOrSecond = 59;

    /// <summary>Parses a <see cref="System.DateTimeOffset" /> value from bytes.</summary>
    /// <param name="bytes">Input bytes read from PLC.</param>
    /// <returns>A value representing the date and time read from the PLC.</returns>
    public static SystemDateTimeOffset FromByteArray(byte[] bytes) => FromSpan(bytes.AsSpan());

    /// <summary>Parses a <see cref="System.DateTimeOffset" /> value from a span.</summary>
    /// <param name="bytes">Input bytes span read from PLC.</param>
    /// <returns>A value representing the date and time read from the PLC.</returns>
    public static SystemDateTimeOffset FromSpan(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < TypeLengthInBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bytes),
                bytes.Length,
                string.Concat(
                    "Parsing a DateTimeLong requires exactly 12 bytes of input data, input data is ",
                    $"{bytes.Length} bytes long."));
        }

        return new SystemDateTimeOffset(FromSpanImpl(bytes), SystemTimeSpan.Zero);
    }

    /// <summary>Parses an array of <see cref="T:System.DateTime" /> values from bytes.</summary>
    /// <param name="bytes">Input bytes read from PLC.</param>
    /// <returns>An array of <see cref="T:System.DateTime" /> objects representing the values read from PLC.</returns>
    public static SystemDateTimeOffset[] ToArray(byte[] bytes) => ToArray(bytes.AsSpan());

    /// <summary>Parses an array of <see cref="T:System.DateTime" /> values from a span.</summary>
    /// <param name="bytes">Input bytes span read from PLC.</param>
    /// <returns>An array of <see cref="T:System.DateTime" /> objects representing the values read from PLC.</returns>
    public static SystemDateTimeOffset[] ToArray(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length % TypeLengthInBytes != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bytes),
                bytes.Length,
                string.Concat(
                    "Parsing an array of DateTimeLong requires a multiple of 12 bytes of input data, input data is ",
                    $"'{bytes.Length}' long."));
        }

        var cnt = bytes.Length / TypeLengthInBytes;
        var result = new SystemDateTimeOffset[cnt];

        for (var i = 0; i < cnt; i++)
        {
            result[i] = new(
                FromSpanImpl(bytes.Slice(i * TypeLengthInBytes, TypeLengthInBytes)),
                SystemTimeSpan.Zero);
        }

        return result;
    }

    /// <summary>Converts a <see cref="T:System.DateTime" /> value to a byte array.</summary>
    /// <param name="dateTime">The DateTime value to convert.</param>
    /// <returns>A byte array containing the S7 DateTimeLong representation of <paramref name="dateTime" />.</returns>
    public static byte[] ToByteArray(SystemDateTimeOffset dateTime)
    {
        Span<byte> bytes = stackalloc byte[TypeLengthInBytes];
        ToSpan(dateTime, bytes);
        return bytes.ToArray();
    }

    /// <summary>Converts an array of <see cref="T:System.DateTime" /> values to a byte array.</summary>
    /// <param name="dateTimes">The DateTime values to convert.</param>
    /// <returns>A byte array containing the S7 DateTimeLong representations of <paramref name="dateTimes" />.</returns>
    public static byte[] ToByteArray(SystemDateTimeOffset[] dateTimes)
    {
        if (dateTimes is null)
        {
            throw new ArgumentNullException(nameof(dateTimes));
        }

        // Use ArrayPool for large allocations
        var totalBytes = dateTimes.Length * TypeLengthInBytes;
        byte[]? pooledArray = null;
        var buffer = totalBytes > ArrayPoolThresholdInBytes
            ? pooledArray = ArrayPool<byte>.Shared.Rent(totalBytes)
            : new byte[totalBytes];

        try
        {
            var span = buffer.AsSpan(0, totalBytes);
            for (var i = 0; i < dateTimes.Length; i++)
            {
                ToSpan(dateTimes[i], span.Slice(i * TypeLengthInBytes, TypeLengthInBytes));
            }

            return span.ToArray();
        }
        finally
        {
            if (pooledArray is not null)
            {
                ArrayPool<byte>.Shared.Return(pooledArray);
            }
        }
    }

    /// <summary>Converts a <see cref="T:System.DateTime" /> value to a span.</summary>
    /// <param name="dateTime">The DateTime value to convert.</param>
    /// <param name="destination">The destination span.</param>
    public static void ToSpan(SystemDateTimeOffset dateTime, Span<byte> destination)
    {
        if (destination.Length < TypeLengthInBytes)
        {
            throw new ArgumentException("Destination span must be at least 12 bytes", nameof(destination));
        }

        if (dateTime < SpecMinimumDateTime)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dateTime),
                dateTime,
                string.Concat(
                    $"Date time '{dateTime}' is before the minimum '{SpecMinimumDateTime}' supported ",
                    "in S7 DateTimeLong representation."));
        }

        if (dateTime > SpecMaximumDateTime)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dateTime),
                dateTime,
                string.Concat(
                    $"Date time '{dateTime}' is after the maximum '{SpecMaximumDateTime}' supported ",
                    "in S7 DateTimeLong representation."));
        }

        var plcDateTime = dateTime.DateTime;

        // Convert Year
        Word.ToSpan((ushort)plcDateTime.Year, destination.Slice(0, YearLengthInBytes));

        // Convert Month
        destination[MonthOffset] = (byte)plcDateTime.Month;

        // Convert Day
        destination[DayOffset] = (byte)plcDateTime.Day;

        // Convert WeekDay. NET DateTime starts with Sunday = 0, while S7DT has Sunday = 1.
        destination[WeekdayOffset] = (byte)(plcDateTime.DayOfWeek + 1);

        // Convert Hour
        destination[HourOffset] = (byte)plcDateTime.Hour;

        // Convert Minutes
        destination[MinuteOffset] = (byte)plcDateTime.Minute;

        // Convert Seconds
        destination[SecondOffset] = (byte)plcDateTime.Second;

        // Convert Nanoseconds. Net DateTime has a representation of 1 Tick = 100ns.
        // Thus First take the ticks Mod 1 Second (1s = 10'000'000 ticks), and then Convert to nanoseconds.
        var nanoseconds = (uint)(plcDateTime.Ticks % SystemTimeSpan.TicksPerSecond * NanosecondsPerTick);
        DWord.ToSpan(nanoseconds, destination.Slice(NanosecondsOffset, NanosecondsLengthInBytes));
    }

    /// <summary>Converts multiple DateTime values to the specified span.</summary>
    /// <param name="dateTimes">The DateTime values.</param>
    /// <param name="destination">The destination span.</param>
    public static void ToSpan(ReadOnlySpan<SystemDateTimeOffset> dateTimes, Span<byte> destination)
    {
        if (destination.Length < dateTimes.Length * TypeLengthInBytes)
        {
            throw new ArgumentException("Destination span is too small", nameof(destination));
        }

        for (var i = 0; i < dateTimes.Length; i++)
        {
            ToSpan(dateTimes[i], destination.Slice(i * TypeLengthInBytes, TypeLengthInBytes));
        }
    }

    /// <summary>Stores the f ro ms pa ni m p l value.</summary>
    /// <param name="bytes">The b yt e s value.</param>
    /// <returns>The resulting value.</returns>
    private static SystemDateTime FromSpanImpl(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < TypeLengthInBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bytes),
                bytes.Length,
                string.Concat(
                    "Parsing a DateTimeLong requires exactly 12 bytes of input data, input data is ",
                    $"{bytes.Length} bytes long."));
        }

        var year = AssertRangeInclusive(
            Word.FromSpan(bytes.Slice(0, YearLengthInBytes)),
            (ushort)SpecMinimumDateTime.Year,
            (ushort)SpecMaximumDateTime.Year,
            "year");
        var month = AssertRangeInclusive(bytes[MonthOffset], (byte)1, MaximumMonth, "month");
        var day = AssertRangeInclusive(bytes[DayOffset], (byte)1, MaximumDay, "day of month");
        var hour = AssertRangeInclusive(bytes[HourOffset], (byte)0, MaximumHour, "hour");
        var minute = AssertRangeInclusive(bytes[MinuteOffset], (byte)0, MaximumMinuteOrSecond, "minute");
        var second = AssertRangeInclusive(bytes[SecondOffset], (byte)0, MaximumMinuteOrSecond, "second");

        var nanoseconds = AssertRangeInclusive(
            DWord.FromSpan(bytes.Slice(NanosecondsOffset, NanosecondsLengthInBytes)),
            0U,
            MaximumNanoseconds,
            "nanoseconds");

        var time = new SystemDateTime(
            year,
            month,
            day,
            hour,
            minute,
            second,
            DateTimeKind.Unspecified);
        return time.AddTicks(nanoseconds / NanosecondsPerTick);
    }

    /// <summary>Stores the a ss er tr an ge in cl us i v e value.</summary>
    /// <typeparam name="T">The t value type.</typeparam>
    /// <param name="input">The i np u t value.</param>
    /// <param name="min">The m i n value.</param>
    /// <param name="max">The m a x value.</param>
    /// <param name="field">The f ie l d value.</param>
    /// <returns>The resulting value.</returns>
    private static T AssertRangeInclusive<T>(T input, T min, T max, string field)
        where T : IComparable<T>
    {
        if (input.CompareTo(min) < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input),
                input,
                $"Value '{input}' is lower than the minimum '{min}' allowed for {field}.");
        }

        if (input.CompareTo(max) > 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input),
                input,
                $"Value '{input}' is higher than the maximum '{max}' allowed for {field}.");
        }

        return input;
    }
}
