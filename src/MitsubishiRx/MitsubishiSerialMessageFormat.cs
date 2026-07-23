// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Defines the MitsubishiSerialMessageFormat values.</summary>
public enum MitsubishiSerialMessageFormat
{
    /// <summary>Represents the Format1 option.</summary>
    Format1,

    /// <summary>Represents the Format4 option.</summary>
    Format4,

    /// <summary>Represents the Format5 option.</summary>
    Format5,
}
