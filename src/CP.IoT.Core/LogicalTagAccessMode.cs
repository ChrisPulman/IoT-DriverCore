// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.IoT.Core;

/// <summary>Declares which operations callers may perform on a logical tag.</summary>
public enum LogicalTagAccessMode
{
    /// <summary>The tag may only be read.</summary>
    Read,

    /// <summary>The tag may only be written.</summary>
    Write,

    /// <summary>The tag may be both read and written.</summary>
    ReadWrite,
}
