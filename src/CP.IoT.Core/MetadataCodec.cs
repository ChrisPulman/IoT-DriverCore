// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.IoT.Core;

/// <summary>Encodes and decodes tag metadata as a URL-encoded query string.</summary>
internal static class MetadataCodec
{
    /// <summary>Encodes <paramref name="metadata"/> as a URL-encoded ampersand-separated key=value string.</summary>
    /// <param name="metadata">The metadata dictionary to encode.</param>
    /// <returns>The encoded string, or an empty string when the dictionary is empty.</returns>
    internal static string Encode(IReadOnlyDictionary<string, string> metadata) =>
        string.Join(
            "&",
            metadata
                .OrderBy(static item => item.Key, StringComparer.Ordinal)
                .Select(static item => $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}"));

    /// <summary>Decodes a URL-encoded key=value pair string into a metadata dictionary.</summary>
    /// <param name="value">The encoded string to decode.</param>
    /// <returns>A read-only dictionary containing the decoded metadata entries.</returns>
    internal static IReadOnlyDictionary<string, string> Decode(string value)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);

        if (string.IsNullOrEmpty(value))
        {
            return metadata;
        }

        foreach (var pair in value.Split('&'))
        {
            var separator = pair.IndexOf('=');

            if (separator < 1)
            {
                throw new FormatException("Metadata must contain URL-escaped key=value pairs.");
            }

            var key = Uri.UnescapeDataString(pair[..separator]);
            var itemValue = Uri.UnescapeDataString(pair[(separator + 1)..]);
            metadata.Add(LogicalTag.Required(key, nameof(value)), itemValue);
        }

        return metadata;
    }
}
