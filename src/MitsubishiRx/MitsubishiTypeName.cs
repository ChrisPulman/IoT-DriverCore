// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiTypeName record.</summary>
/// <param name="ModelName">The ModelName parameter.</param>
/// <param name="ModelCode">The ModelCode parameter.</param>
public sealed record MitsubishiTypeName(string ModelName, ushort ModelCode);
