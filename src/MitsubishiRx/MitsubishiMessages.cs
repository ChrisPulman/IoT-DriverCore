// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Provides shared Mitsubishi driver messages and tokens.</summary>
internal static class MitsubishiMessages
{
    /// <summary>Identifies big-endian tag encoding.</summary>
    internal const string BigEndian = "BigEndian";

    /// <summary>Explains that a tag database is required for grouped writes.</summary>
    internal const string TagDatabaseRequiredForGroupedWrites =
        "TagDatabase must be assigned before grouped writes can be validated.";

    /// <summary>Explains that a tag database is required for tag APIs.</summary>
    internal const string TagDatabaseRequiredForTagApis =
        "TagDatabase must be assigned before tag-based APIs can be used.";

    /// <summary>Explains that a tag database is required for validation.</summary>
    internal const string TagDatabaseRequiredForValidation =
        "TagDatabase must be assigned before validation can run.";

    /// <summary>Explains that the transport has not been configured.</summary>
    internal const string TransportNotConfigured = "Transport is not configured.";
}
