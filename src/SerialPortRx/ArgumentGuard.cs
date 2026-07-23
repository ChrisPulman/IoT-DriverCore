// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Serial;

/// <summary>Provides argument validation that is available on every supported target framework.</summary>
internal static class ArgumentGuard
{
    /// <summary>Throws when an argument is null.</summary>
    /// <param name="value">The argument value.</param>
    /// <param name="parameterName">The argument name.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    internal static void ThrowIfNull(
        [System.Diagnostics.CodeAnalysis.NotNull] object? value,
        string parameterName)
    {
        _ = value ?? throw new ArgumentNullException(parameterName);
    }
}
