// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Maps a logical tag to its adapter-parsed transport address.</summary>
/// <remarks>Implement this contract in protocol adapters; this core library deliberately owns no protocol grammar.</remarks>
public interface ITagTransportAddressParser
{
    /// <summary>Parses the logical tag into transfer coordinates for the requested access direction.</summary>
    /// <param name="tag">The logical tag to parse.</param>
    /// <param name="access">The direction for which the transfer is being planned.</param>
    /// <returns>The normalized, numeric transport address.</returns>
    TagTransportAddress Parse(LogicalTag tag, TagTransferAccess access);
}
