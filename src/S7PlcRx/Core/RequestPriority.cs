// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.S7PlcRx.Reactive.Core;
#else
namespace IoT.DriverCore.S7PlcRx.Core;
#endif

/// <summary>Request priority levels for batch processing.</summary>
public enum RequestPriority
{
    /// <summary>Low priority request.</summary>
    Low = 0,

    /// <summary>Normal priority request.</summary>
    Normal = 1,

    /// <summary>High priority request.</summary>
    High = 2,

    /// <summary>Critical priority request.</summary>
    Critical = 3,
}
