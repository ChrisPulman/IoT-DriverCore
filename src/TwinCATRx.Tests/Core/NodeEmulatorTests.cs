// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using CP.TwinCatRx.Core;

namespace TwinCATRx.Tests.Core;

/// <summary>Tests for NodeEmulator.</summary>
public class NodeEmulatorTests
{
    /// <summary>Dispose clears Nodes and Tag.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Dispose_Clears_StateAsync()
    {
        var type = typeof(Settings).Assembly.GetType("CP.TwinCatRx.Core.NodeEmulator")
            ?? throw new InvalidOperationException("NodeEmulator was not found.");
        var n = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("NodeEmulator could not be created.");
        var nodesProp = type.GetProperty("Nodes")
            ?? throw new InvalidOperationException("Nodes property was not found.");
        _ = nodesProp.GetValue(n) as System.Collections.ICollection;
        var dispose = type.GetMethod("Dispose")
            ?? throw new InvalidOperationException("Dispose method was not found.");
        _ = dispose.Invoke(n, null);
        var nodesAfter = nodesProp.GetValue(n);
        await TUnitAssert.That(nodesAfter).IsNull();
    }
}
