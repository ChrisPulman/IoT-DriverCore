// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using IoT.DriverCore.TwinCATRx.Core;

namespace IoT.DriverCore.TwinCATRx.Tests.Core;

/// <summary>Tests for NodeEmulator.</summary>
public class NodeEmulatorTests
{
    /// <summary>Dispose clears Nodes and Tag.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Dispose_Clears_StateAsync()
    {
        var type = typeof(Settings).Assembly.GetType("IoT.DriverCore.TwinCATRx.Core.NodeEmulator")
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
