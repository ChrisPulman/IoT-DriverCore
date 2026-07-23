// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
using TUnit.Assertions;
using TUnit.Core;

namespace IoT.DriverCore.Core.Tests;

/// <summary>Validates the common logical-tag management contract composition.</summary>
public sealed class LogicalTagSetupContractTests
{
    /// <summary>Verifies the aggregate setup interface exposes every focused management contract.</summary>
    /// <returns>A task that represents the asynchronous assertion operation.</returns>
    [Test]
    public async Task SetupContractComposesFocusedManagementContractsAsync()
    {
        var contracts = typeof(ILogicalTagSetup).GetInterfaces();

        await Assert.That(contracts.Contains(typeof(ILogicalTagRegistry))).IsTrue();
        await Assert.That(contracts.Contains(typeof(ILogicalTagDefinitionExchange))).IsTrue();
        await Assert.That(contracts.Contains(typeof(ILogicalTagPersistence))).IsTrue();
        await Assert.That(contracts.Contains(typeof(ILogicalTagGroupPersistence))).IsTrue();
    }

    /// <summary>Verifies managed clients retain the common read, write, and observe surface.</summary>
    /// <returns>A task that represents the asynchronous assertion operation.</returns>
    [Test]
    public async Task ManagedClientComposesOperationsAndSetupAsync()
    {
        var contracts = typeof(IManagedLogicalTagClient).GetInterfaces();

        await Assert.That(contracts.Contains(typeof(ILogicalTagClient))).IsTrue();
        await Assert.That(contracts.Contains(typeof(ILogicalTagSetup))).IsTrue();
    }
}
