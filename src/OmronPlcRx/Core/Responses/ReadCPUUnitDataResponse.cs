// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Core.Responses;
#else
namespace IoT.DriverCore.OmronPlcRx.Core.Responses;
#endif

/// <summary>Represents the r ea dc pu un it da ta re sp on se type.</summary>
internal static class ReadCPUUnitDataResponse
{
    /// <summary>Stores the c on tr ol le rm od el le ng th value.</summary>
    internal const int ControllerModelLength = 20;

    /// <summary>Stores the c on tr ol le rv er si on le ng th value.</summary>
    internal const int ControllerVersionLength = 20;

    /// <summary>Stores the s ys te mr es er ve dl en gt h value.</summary>
    internal const int SystemReservedLength = 40;

    /// <summary>Stores the a re ad at al en gt h value.</summary>
    internal const int AreaDataLength = 12;

    /// <summary>Stores the t ot al re sp on se le ng th value.</summary>
    internal const int TotalResponseLength =
        ControllerModelLength + ControllerVersionLength + SystemReservedLength + AreaDataLength;

    /// <summary>Initializes a new instance of the <see cref="ExtractData"/> class.</summary>
    /// <param name="response">The r es po ns e value.</param>
    /// <returns>The result produced by the operation.</returns>
    internal static CPUUnitDataResult ExtractData(FINSResponse response)
    {
        const int expected = TotalResponseLength;
        var data = response.Data;
        if (data is null || data.Length < expected)
        {
            var actual = data?.Length ?? 0;
            throw new FINSException(
                $"The Response Data Length of '{actual}' was too short - Expecting a Length of '{expected}'");
        }

        return new CPUUnitDataResult
        {
            ControllerModel = ExtractStringValue(SubArray(data, 0, ControllerModelLength)),
            ControllerVersion = ExtractStringValue(SubArray(data, ControllerModelLength, ControllerVersionLength)),
        };
    }

    /// <summary>Initializes a new instance of the <see cref="ExtractStringValue"/> class.</summary>
    /// <param name="bytes">The b yt es value.</param>
    /// <returns>The result produced by the operation.</returns>
    private static string ExtractStringValue(byte[] bytes)
    {
        var stringBytes = new List<byte>(bytes.Length);

        foreach (var byteValue in bytes)
        {
            if (byteValue > 0)
            {
                stringBytes.Add(byteValue);
            }
            else
            {
                break;
            }
        }

        return stringBytes.Count == 0 ? string.Empty : Encoding.ASCII.GetString([.. stringBytes]).Trim();
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

    /// <summary>Represents the c pu un it da ta re su lt type.</summary>
    internal readonly record struct CPUUnitDataResult
    {
        /// <summary>Gets or sets the controller model value.</summary>
        internal string ControllerModel { get; init; }

        /// <summary>Gets or sets the controller version value.</summary>
        internal string ControllerVersion { get; init; }
    }
}
