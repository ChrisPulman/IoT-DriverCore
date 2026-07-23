// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.TwinCATRx.Reactive;
#else
namespace IoT.DriverCore.TwinCATRx;
#endif

/// <summary>Describes the lifecycle state of an <see cref="InMemoryAdsClient"/>.</summary>
public enum InMemoryAdsConnectionState
{
    /// <summary>The simulator is disconnected and can be connected.</summary>
    Disconnected,

    /// <summary>The simulator is validating settings and creating handles.</summary>
    Connecting,

    /// <summary>The simulator is ready to service reads, writes, and notifications.</summary>
    Connected,

    /// <summary>The latest connection attempt or simulated operation failed.</summary>
    Faulted,

    /// <summary>The simulator and its observable streams have been disposed.</summary>
    Disposed,
}
