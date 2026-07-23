// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.TwinCATRx.Reactive;
#else
namespace IoT.DriverCore.TwinCATRx;
#endif

/// <summary>Identifies an operation that can receive a deterministic simulator fault.</summary>
public enum InMemoryAdsOperation
{
    /// <summary>A connection or reconnection operation.</summary>
    Connect,

    /// <summary>A symbol read operation.</summary>
    Read,

    /// <summary>A symbol write operation.</summary>
    Write,

    /// <summary>A configured notification publication.</summary>
    Notification,
}
