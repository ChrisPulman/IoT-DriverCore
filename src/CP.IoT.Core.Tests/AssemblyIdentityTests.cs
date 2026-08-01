// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using IoT.Driver.Core;
using TUnit.Assertions;
using TUnit.Core;

namespace IoT.Driver.Core.Tests;

/// <summary>Verifies the assembly identity consumed by downstream driver packages.</summary>
public sealed class AssemblyIdentityTests
{
    /// <summary>Verifies the published core assembly retains its supported assembly version.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CoreAssemblyUsesCompatibleVersionAsync()
    {
        var identity = typeof(LogicalTag).Assembly.GetName();
        Version expectedVersion = new(1, 0, 0, 0);

        await Assert.That(identity.Name).IsEqualTo("CP.IoT.Core");
        await Assert.That(identity.Version).IsEqualTo(expectedVersion);
    }
}
