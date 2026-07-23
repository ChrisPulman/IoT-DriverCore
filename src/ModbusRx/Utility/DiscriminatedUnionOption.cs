// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.Utility;
#else
namespace IoT.DriverCore.ModbusRx.Utility;
#endif

/// <summary>Possible options for <see cref="DiscriminatedUnion{TA, TB}"/>.</summary>
public enum DiscriminatedUnionOption
{
    /// <summary>Option A.</summary>
    A,

    /// <summary>Option B.</summary>
    B,
}
