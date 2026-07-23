// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace IoT.DriverCore.ABPlcRx.Reactive;
#else
namespace IoT.DriverCore.ABPlcRx;
#endif

/// <summary>Cross-target argument guard helpers.</summary>
internal static class ArgumentExceptionHelper
{
    /// <summary>Throws when an argument is null.</summary>
    /// <typeparam name="T">The argument type.</typeparam>
    /// <param name="argument">The argument value.</param>
    /// <param name="paramName">The parameter name.</param>
    internal static void ThrowIfNull<T>(T? argument, string paramName)
        where T : class
    {
        if (argument is not null)
        {
            return;
        }

        throw new ArgumentNullException(paramName);
    }

    /// <summary>Throws when a string argument is null, empty, or whitespace.</summary>
    /// <param name="argument">The argument value.</param>
    /// <param name="paramName">The parameter name.</param>
    internal static void ThrowIfNullOrWhiteSpace(string? argument, string paramName)
    {
        if (argument?.Trim().Length > 0)
        {
            return;
        }

        throw new ArgumentException("Value cannot be null, empty, or whitespace.", paramName);
    }

    /// <summary>Throws when an integer argument is negative.</summary>
    /// <param name="argument">The argument value.</param>
    /// <param name="paramName">The parameter name.</param>
    internal static void ThrowIfNegative(int argument, string paramName)
    {
        if (argument >= 0)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(paramName);
    }

    /// <summary>Throws when an integer argument is zero or negative.</summary>
    /// <param name="argument">The argument value.</param>
    /// <param name="paramName">The parameter name.</param>
    internal static void ThrowIfNegativeOrZero(int argument, string paramName)
    {
        if (argument > 0)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(paramName);
    }

    /// <summary>Throws when an integer argument is greater than a limit.</summary>
    /// <param name="argument">The argument value.</param>
    /// <param name="limit">The inclusive upper limit.</param>
    /// <param name="paramName">The parameter name.</param>
    internal static void ThrowIfGreaterThan(int argument, int limit, string paramName)
    {
        if (argument <= limit)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(paramName);
    }

    /// <summary>Throws when an integer argument is greater than or equal to a limit.</summary>
    /// <param name="argument">The argument value.</param>
    /// <param name="limit">The exclusive upper limit.</param>
    /// <param name="paramName">The parameter name.</param>
    internal static void ThrowIfGreaterThanOrEqual(int argument, int limit, string paramName)
    {
        if (argument < limit)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(paramName);
    }
}
