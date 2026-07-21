// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive;
#else
namespace S7PlcRx;
#endif

/// <summary>Provides target-framework-independent argument validation.</summary>
internal static class Guard
{
    /// <summary>Requires a non-null reference.</summary>
    /// <typeparam name="T">The reference type.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="parameterName">The parameter name.</param>
    internal static void NotNull<T>(T? value, string parameterName)
        where T : class
        => _ = value ?? throw new ArgumentNullException(parameterName);

    /// <summary>Requires text containing at least one non-whitespace character.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="parameterName">The parameter name.</param>
    internal static void NotNullOrWhiteSpace(string? value, string parameterName) =>
        _ = value is { } text && text.Trim().Length > 0
            ? true
            : throw new ArgumentException("A non-empty value is required.", parameterName);
}
