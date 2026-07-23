// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Defines the MitsubishiTagDatabaseSchemaFormat values.</summary>
internal enum MitsubishiTagDatabaseSchemaFormat
{
    /// <summary>Represents the Csv option.</summary>
    Csv,

    /// <summary>Represents the Json option.</summary>
    Json,

    /// <summary>Represents the Yaml option.</summary>
    Yaml,
}
