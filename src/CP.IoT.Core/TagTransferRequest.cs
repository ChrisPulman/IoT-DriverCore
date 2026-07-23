// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Associates a logical tag name with an adapter-parsed transport address.</summary>
public sealed class TagTransferRequest
{
    /// <summary>Initializes a new instance of the <see cref="TagTransferRequest"/> class.</summary>
    /// <param name="tagName">The logical tag name.</param>
    /// <param name="address">The parser-provided transport address.</param>
    public TagTransferRequest(string tagName, TagTransportAddress address)
    {
        TagName = LogicalTag.Required(tagName, nameof(tagName));
        Address = address ?? throw new ArgumentNullException(nameof(address));
    }

    /// <summary>Gets the logical tag name.</summary>
    public string TagName { get; }

    /// <summary>Gets the parser-provided transport address.</summary>
    public TagTransportAddress Address { get; }
}
