// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
#if REACTIVE_SHIM
using OmronPlcRx.Reactive.Core.Converters;
using OmronPlcRx.Reactive.Core.Requests;
#else
using OmronPlcRx.Core.Converters;
using OmronPlcRx.Core.Requests;
#endif

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive.Core.Responses;
#else
namespace OmronPlcRx.Core.Responses;
#endif

/// <summary>Represents the r ea dc yc le ti me re sp on se type.</summary>
internal static class ReadCycleTimeResponse
{
    /// <summary>Stores the c yc le ti me it em le ng th value.</summary>
    internal const int CycleTimeItemLength = 4;

    /// <summary>Initializes a new instance of the <see cref="ExtractCycleTime"/> class.</summary>
    /// <param name="request">The r eq ue st value.</param>
    /// <param name="response">The r es po ns e value.</param>
    /// <returns>The result produced by the operation.</returns>
    internal static CycleTimeResult ExtractCycleTime(ReadCycleTimeRequest request, FINSResponse response)
    {
        if (response.Data?.Length < CycleTimeItemLength * ProtocolConstants.Three)
        {
            var actual = response.Data.Length;
            const int expected = CycleTimeItemLength * ProtocolConstants.Three;
            throw new FINSException(
                $"The Response Data Length of '{actual}' was too short - Expecting a Length of '{expected}'");
        }

        var data = response.Data;

        return new CycleTimeResult
        {
            AverageCycleTime = GetCycleTime(SubArray(data, 0, CycleTimeItemLength)),
            MaximumCycleTime = GetCycleTime(SubArray(data, CycleTimeItemLength, CycleTimeItemLength)),
            MinimumCycleTime = GetCycleTime(
                SubArray(
                    data,
                    CycleTimeItemLength * ProtocolConstants.Two,
                    CycleTimeItemLength)),
        };
    }

    /// <summary>Initializes a new instance of the <see cref="GetCycleTime"/> class.</summary>
    /// <param name="bytes">The b yt es value.</param>
    /// <returns>The result produced by the operation.</returns>
    private static double GetCycleTime(byte[] bytes)
    {
        if (bytes.Length != 4)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), "The Cycle Time Bytes Array Length must be 4");
        }

        Array.Reverse(bytes);
        var cycleTimeValue = BCDConverter.ToUInt32(bytes);

        return cycleTimeValue > 0 ? cycleTimeValue / ProtocolConstants.TenDouble : 0;
    }

    /// <summary>Initializes a new instance of the <see cref="SubArray"/> class.</summary>
    /// <param name="data">The d at a value.</param>
    /// <param name="index">The i nd ex value.</param>
    /// <param name="length">The l en gt h value.</param>
    /// <returns>The result produced by the operation.</returns>
    private static byte[] SubArray(byte[]? data, int index, int length)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data), "The Data Array cannot be null");
        }

        var result = new byte[length];
        Array.Copy(data, index, result, 0, length);
        return result;
    }

    /// <summary>Represents the c yc le ti me re su lt type.</summary>
    internal readonly record struct CycleTimeResult
    {
        /// <summary>Gets or sets the minimum cycle time value.</summary>
        internal double MinimumCycleTime { get; init; }

        /// <summary>Gets or sets the maximum cycle time value.</summary>
        internal double MaximumCycleTime { get; init; }

        /// <summary>Gets or sets the average cycle time value.</summary>
        internal double AverageCycleTime { get; init; }
    }
}
