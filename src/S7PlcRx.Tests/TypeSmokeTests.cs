// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.Driver.S7PlcRx.Tests;

/// <summary>Smoke tests to ensure TUnit discovery is working for newly added test files.</summary>
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public class TypeSmokeTests
{
    /// <summary>Gets a debugger-friendly test description.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => ToString() ?? string.Empty;

    /// <summary>Ensures a newly-added test file is discoverable and executable.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Smoke_ShouldRun()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        await Assert.That(true, Is.True);
    }
}
