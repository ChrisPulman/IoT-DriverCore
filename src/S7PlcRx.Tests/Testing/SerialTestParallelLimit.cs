// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using TUnit.Core.Interfaces;

[assembly: ParallelLimiter<SerialTestParallelLimit>]

namespace IoT.DriverCore.S7PlcRx.Tests.Testing;

/// <summary>Limits the test assembly to one concurrently executing test.</summary>
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class SerialTestParallelLimit : IParallelLimit
{
    /// <inheritdoc />
    public int Limit => 1;

    /// <summary>Gets the debugger display text.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => ToString() ?? string.Empty;
}
