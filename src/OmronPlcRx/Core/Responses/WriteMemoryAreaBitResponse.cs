// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
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

/// <summary>Represents the w ri te me mo ry ar ea bi tr es po ns e type.</summary>
internal static class WriteMemoryAreaBitResponse
{
    /// <summary>Initializes a new instance of the <see cref="Validate"/> class.</summary>
    /// <param name="request">The r eq ue st value.</param>
    /// <param name="response">The r es po ns e value.</param>
    internal static void Validate(WriteMemoryAreaBitRequest request, FINSResponse response)
    {
        _ = request ?? throw new ArgumentNullException(nameof(request));
        _ = response ?? throw new ArgumentNullException(nameof(response));
    }
}
