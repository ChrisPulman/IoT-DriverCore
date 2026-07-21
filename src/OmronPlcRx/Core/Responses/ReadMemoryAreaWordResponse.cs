// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using OmronPlcRx.Reactive.Core.Requests;
#else
using OmronPlcRx.Core.Requests;
#endif

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive.Core.Responses;
#else
namespace OmronPlcRx.Core.Responses;
#endif

/// <summary>Represents the r ea dm em or ya re aw or dr es po ns e type.</summary>
internal static class ReadMemoryAreaWordResponse
{
    /// <summary>Initializes a new instance of the <see cref="ExtractValues"/> class.</summary>
    /// <param name="request">The r eq ue st value.</param>
    /// <param name="response">The r es po ns e value.</param>
    /// <returns>The result produced by the operation.</returns>
    internal static short[] ExtractValues(ReadMemoryAreaWordRequest request, FINSResponse response)
    {
        if (response.Data?.Length < request.Length * ProtocolConstants.Two)
        {
            var actual = response.Data.Length;
            var expected = request.Length * ProtocolConstants.Two;
            throw new FINSException(
                $"The Response Data Length of '{actual}' was too short - Expecting a Length of '{expected}'");
        }

        var values = new short[request.Length];
        var data = response.Data;

        for (int i = 0, w = 0; i < request.Length * ProtocolConstants.Two; i += ProtocolConstants.Two, w++)
        {
            // Data is big-endian per protocol, convert to host order (little-endian)
            values[w] = (short)((data![i] << 8) | data[i + 1]);
        }

        return values;
    }
}
