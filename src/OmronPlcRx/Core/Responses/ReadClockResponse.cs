// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
#if REACTIVE_SHIM
using IoT.DriverCore.OmronPlcRx.Reactive.Core.Converters;
using IoT.DriverCore.OmronPlcRx.Reactive.Core.Requests;
#else
using IoT.DriverCore.OmronPlcRx.Core.Converters;
using IoT.DriverCore.OmronPlcRx.Core.Requests;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Core.Responses;
#else
namespace IoT.DriverCore.OmronPlcRx.Core.Responses;
#endif

/// <summary>Represents the r ea dc lo ck re sp on se type.</summary>
internal static class ReadClockResponse
{
    /// <summary>Stores the d at el en gt h value.</summary>
    internal const int DateLength = 6;

    /// <summary>Stores the d ay of we ek le ng th value.</summary>
    internal const int DayOfWeekLength = 1;

    /// <summary>Initializes a new instance of the <see cref="ExtractClock"/> class.</summary>
    /// <param name="request">The r eq ue st value.</param>
    /// <param name="response">The r es po ns e value.</param>
    /// <returns>The result produced by the operation.</returns>
    internal static ClockResult ExtractClock(ReadClockRequest request, FINSResponse response)
    {
        const int expected = DateLength + DayOfWeekLength;
        var data = response.Data;
        if (data is null || data.Length < expected)
        {
            var actual = data?.Length ?? 0;
            throw new FINSException(
                $"The Response Data Length of '{actual}' was too short - Expecting a Length of '{expected}'");
        }

        return new ClockResult
        {
            ClockDateTime = GetClockDateTime(SubArray(data, 0, DateLength)),
            DayOfWeek = BCDConverter.ToByte(data![DateLength]),
        };
    }

    /// <summary>Initializes a new instance of the <see cref="GetClockDateTime"/> class.</summary>
    /// <param name="bytes">The b yt es value.</param>
    /// <returns>The result produced by the operation.</returns>
    private static DateTime GetClockDateTime(byte[] bytes)
    {
        var year = BCDConverter.ToByte(bytes[0]);
        var month = BCDConverter.ToByte(bytes[1]);
        var day = BCDConverter.ToByte(bytes[2]);
        var hour = BCDConverter.ToByte(bytes[3]);
        var minute = BCDConverter.ToByte(bytes[4]);
        var second = BCDConverter.ToByte(bytes[5]);

        if (year < ProtocolConstants.Seventy)
        {
            return new DateTime(
                ProtocolConstants.TwoThousand + year,
                month,
                day,
                hour,
                minute,
                second,
                DateTimeKind.Utc);
        }

        if (year < ProtocolConstants.OneHundred)
        {
            return new DateTime(
                ProtocolConstants.OneThousandNineHundred + year,
                month,
                day,
                hour,
                minute,
                second,
                DateTimeKind.Utc);
        }

        throw new FINSException("Invalid DateTime Values received from the PLC Clock");
    }

    /// <summary>Initializes a new instance of the <see cref="SubArray"/> class.</summary>
    /// <param name="data">The d at a value.</param>
    /// <param name="index">The i nd ex value.</param>
    /// <param name="length">The l en gt h value.</param>
    /// <returns>The result produced by the operation.</returns>
    private static byte[] SubArray(byte[] data, int index, int length)
    {
        var result = new byte[length];
        Array.Copy(data, index, result, 0, length);
        return result;
    }

    /// <summary>Represents the c lo ck re su lt type.</summary>
    internal struct ClockResult
    {
        /// <summary>Gets or sets the clock date time value.</summary>
        internal DateTime ClockDateTime { get; set; }

        /// <summary>Gets or sets the day of week value.</summary>
        internal byte DayOfWeek { get; set; }
    }
}
