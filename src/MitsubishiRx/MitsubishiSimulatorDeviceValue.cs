// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Represents one populated value in a Mitsubishi simulator memory snapshot.</summary>
/// <param name="Symbol">The device symbol.</param>
/// <param name="Number">The numeric device address.</param>
/// <param name="Kind">The device value kind.</param>
/// <param name="Value">The stored raw value.</param>
public sealed record MitsubishiSimulatorDeviceValue(
    string Symbol,
    int Number,
    DeviceValueKind Kind,
    ushort Value);
