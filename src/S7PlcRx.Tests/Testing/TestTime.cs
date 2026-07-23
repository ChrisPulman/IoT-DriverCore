// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.S7PlcRx.Tests.Testing;

/// <summary>Provides deterministic values that are unavailable on older target frameworks.</summary>
internal static class TestTime
{
    /// <summary>Gets the Unix epoch.</summary>
    internal static DateTimeOffset UnixEpoch { get; } =
        new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
}
