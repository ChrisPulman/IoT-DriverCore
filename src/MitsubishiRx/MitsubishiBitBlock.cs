// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;

#else

namespace MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiBitBlock record.</summary>
/// <param name="Address">The Address parameter.</param>
/// <param name="Values">The Values parameter.</param>
public sealed record MitsubishiBitBlock(
    MitsubishiDeviceAddress Address,
    ReadOnlyMemory<bool> Values);
