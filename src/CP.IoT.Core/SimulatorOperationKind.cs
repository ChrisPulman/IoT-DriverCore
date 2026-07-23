// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Identifies a logical simulator operation for scripted outcomes.</summary>
public enum SimulatorOperationKind
{
    /// <summary>A read transfer.</summary>
    Read,

    /// <summary>A write transfer.</summary>
    Write,
}
