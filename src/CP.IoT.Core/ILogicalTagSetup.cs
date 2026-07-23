// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Combines logical-tag catalog, definition exchange, tag persistence, and group persistence operations.</summary>
/// <remarks>
/// Protocol-specific connection and device setup deliberately remain on each driver. Once that setup is complete,
/// consumers can use this contract without depending on protocol-specific tag-management APIs.
/// </remarks>
public interface ILogicalTagSetup :
    ILogicalTagRegistry,
    ILogicalTagDefinitionExchange,
    ILogicalTagPersistence,
    ILogicalTagGroupPersistence;
