// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using IoT.DriverCore.OmronPlcRx.Reactive.Core.Requests;
#else
using IoT.DriverCore.OmronPlcRx.Core.Requests;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Core.Responses;
#else
namespace IoT.DriverCore.OmronPlcRx.Core.Responses;
#endif

/// <summary>Represents the r ea dm em or ya re ab it re sp on se type.</summary>
internal static class ReadMemoryAreaBitResponse
{
    /// <summary>Initializes a new instance of the <see cref="ExtractValues"/> class.</summary>
    /// <param name="request">The r eq ue st value.</param>
    /// <param name="response">The r es po ns e value.</param>
    /// <returns>A value indicating whether the operation succeeded.</returns>
    internal static bool[] ExtractValues(ReadMemoryAreaBitRequest request, FINSResponse response)
    {
        var data = response.Data;
        if (data is null || data.Length < request.Length)
        {
            var actual = data?.Length ?? 0;
            var expected = request.Length;
            throw new FINSException(
                $"The Response Data Length of '{actual}' was too short - Expecting a Length of '{expected}'");
        }

        var result = new bool[request.Length];
        for (var i = 0; i < request.Length; i++)
        {
            result[i] = data[i] != 0;
        }

        return result;
    }
}
