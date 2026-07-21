// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
#if REACTIVE_SHIM
using OmronPlcRx.Reactive.Core.Converters;
using OmronPlcRx.Reactive.Core.Enums;
#else
using OmronPlcRx.Core.Converters;
using OmronPlcRx.Core.Enums;
#endif

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive.Core.Requests;
#else
namespace OmronPlcRx.Core.Requests;
#endif

/// <summary>Represents the w ri te cl oc kr eq ue st type.</summary>
internal sealed class WriteClockRequest : FINSRequest
{
    /// <summary>Initializes a new instance of the <see cref="WriteClockRequest"/> class.</summary>
    /// <param name="plc">The p lc value.</param>
    private WriteClockRequest(OmronPLCConnection plc)
        : base(plc)
    {
    }

    /// <summary>Gets or sets the date time value.</summary>
    internal DateTime DateTime { get; set; }

    /// <summary>Gets or sets the day of week value.</summary>
    internal byte DayOfWeek { get; set; }

    /// <summary>Initializes a new instance of the <see cref="CreateNew"/> class.</summary>
    /// <param name="plc">The p lc value.</param>
    /// <param name="dateTime">The d at et im e value.</param>
    /// <param name="dayOfWeek">The d ay of we ek value.</param>
    /// <returns>The result produced by the operation.</returns>
    internal static WriteClockRequest CreateNew(OmronPLCConnection plc, DateTime dateTime, byte dayOfWeek) => new(plc)
    {
        FunctionCode = (byte)Enums.FunctionCode.TimeData,
        SubFunctionCode = (byte)TimeDataFunctionCode.WriteClock,
        DateTime = dateTime,
        DayOfWeek = dayOfWeek,
    };

    protected override List<byte> BuildRequestData() =>
    [

        // Year (Last 2 Digits)
        BCDConverter.GetBCDByte((byte)(DateTime.Year % ProtocolConstants.OneHundred)),

        // Month
        BCDConverter.GetBCDByte((byte)DateTime.Month),

        // Day
        BCDConverter.GetBCDByte((byte)DateTime.Day),

        // Hour
        BCDConverter.GetBCDByte((byte)DateTime.Hour),

        // Minute
        BCDConverter.GetBCDByte((byte)DateTime.Minute),

        // Second
        BCDConverter.GetBCDByte((byte)DateTime.Second),

        // Day of Week
        BCDConverter.GetBCDByte(DayOfWeek),
    ];
}
