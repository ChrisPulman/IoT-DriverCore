// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiSerialRoute record.</summary>
/// <param name="StationNumber">The StationNumber parameter.</param>
/// <param name="NetworkNumber">The NetworkNumber parameter.</param>
/// <param name="PcNumber">The PcNumber parameter.</param>
/// <param name="RequestDestinationModuleIoNumber">The RequestDestinationModuleIoNumber parameter.</param>
/// <param name="RequestDestinationModuleStationNumber">The RequestDestinationModuleStationNumber parameter.</param>
/// <param name="SelfStationNumber">The SelfStationNumber parameter.</param>
public sealed record MitsubishiSerialRoute(
    byte StationNumber,
    byte NetworkNumber,
    byte PcNumber,
    ushort RequestDestinationModuleIoNumber,
    byte RequestDestinationModuleStationNumber,
    byte SelfStationNumber);
