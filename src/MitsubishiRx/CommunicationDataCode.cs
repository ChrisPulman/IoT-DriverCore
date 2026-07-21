// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;

#else

namespace MitsubishiRx;

#endif

/// <summary>Defines the CommunicationDataCode values.</summary>
public enum CommunicationDataCode
{
    /// <summary>Represents the Binary option.</summary>
    Binary,

    /// <summary>Represents the Ascii option.</summary>
    Ascii,
}
