// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using CP.IoT.Core;

#if REACTIVE_SHIM
namespace CP.TwinCatRx.Reactive;
#else
namespace CP.TwinCatRx;
#endif

/// <summary>Provides stateless logical-tag routing helpers.</summary>
internal static class TwinCatLogicalTagHelpers
{
    /// <summary>Gets whether an access mode permits reads.</summary>
    /// <param name="mode">The access mode.</param>
    /// <returns>Whether reads are permitted.</returns>
    internal static bool CanRead(LogicalTagAccessMode mode) => mode != LogicalTagAccessMode.Write;

    /// <summary>Gets whether an access mode permits writes.</summary>
    /// <param name="mode">The access mode.</param>
    /// <returns>Whether writes are permitted.</returns>
    internal static bool CanWrite(LogicalTagAccessMode mode) => mode != LogicalTagAccessMode.Read;

    /// <summary>Gets an address relative to its structure root.</summary>
    /// <param name="root">The root address.</param>
    /// <param name="address">The full address.</param>
    /// <returns>The relative member, or null.</returns>
    internal static string? GetMemberAddress(string root, string address) =>
        address.Length > root.Length && address[root.Length] == '.'
            ? address.Substring(root.Length + 1)
            : null;

    /// <summary>Registers cancellation behind the synchronous disposable abstraction.</summary>
    /// <param name="cancel">The cancellation callback.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cancellation registration.</returns>
    internal static IDisposable RegisterCancellation(Action cancel, CancellationToken cancellationToken) =>
        cancellationToken.Register(cancel);

    /// <summary>Checks and normalizes required text.</summary>
    /// <param name="value">The text.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <returns>The normalized text.</returns>
    internal static string Required(string value, string parameterName)
    {
#if NET8_0_OR_GREATER
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
#else
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }
#endif

        return value.Trim();
    }

    /// <summary>Tries to read a protocol metadata value.</summary>
    /// <param name="tag">The logical tag.</param>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>Whether a non-empty value was found.</returns>
    internal static bool TryMetadata(
        LogicalTag tag,
        string key,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)
    {
        foreach (var item in tag.Metadata)
        {
            if (!string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(item.Key, $"TwinCAT.{key}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value = string.IsNullOrWhiteSpace(item.Value) ? null : item.Value.Trim();
            return value is not null;
        }

        value = null;
        return false;
    }
}
