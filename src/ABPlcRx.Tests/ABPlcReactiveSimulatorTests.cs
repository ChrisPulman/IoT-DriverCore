// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
using TUnit.Assertions;
using TUnit.Core;
using ReactiveOperation = IoT.DriverCore.ABPlcRx.Reactive.ABPlcSimulatorOperation;
using ReactivePlcType = IoT.DriverCore.ABPlcRx.Reactive.PlcType;
using ReactiveSimulator = IoT.DriverCore.ABPlcRx.Reactive.ABPlcSimulator;
using ReactiveStatus = IoT.DriverCore.ABPlcRx.Reactive.PlcTagStatus;

namespace IoT.DriverCore.ABPlcRx.Tests;

/// <summary>Verifies the simulator is production-ready in the Reactive compatibility assembly.</summary>
public sealed class ABPlcReactiveSimulatorTests
{
    /// <summary>Logical tag name.</summary>
    private const string TagName = "ReactiveValue";

    /// <summary>Physical tag name.</summary>
    private const string PhysicalTagName = "ReactivePhysical";

    /// <summary>Initial value.</summary>
    private const int InitialValue = 31;

    /// <summary>Updated value.</summary>
    private const int UpdatedValue = 47;

    /// <summary>Scan interval.</summary>
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMilliseconds(10);

    /// <summary>Operation timeout.</summary>
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromMilliseconds(250);

    /// <summary>Exercises Reactive logical registration, IO, scripted faults, and reconnection.</summary>
    /// <returns><see cref="Task"/> representing the test.</returns>
    [Test]
    internal async Task ReactiveSimulatorSupportsLogicalIoFaultsAndReconnectAsync()
    {
        using var simulator = new ReactiveSimulator(
            ReactivePlcType.LGX,
            ScanInterval,
            OperationTimeout,
            "1,0",
            TimeProvider.System);
        using var client = simulator.CreateLogicalTagClient();
        _ = client.CreateTag(TagName, PhysicalTagName, "int");
        simulator.ScanEnabled = false;
        simulator.SetTagValue(PhysicalTagName, InitialValue);

        var initial = await client.ReadAsync(TagName);
        var write = await client.WriteAsync(
            new LogicalTagValue(TagName, UpdatedValue, TimeProvider.System.GetUtcNow()));
        simulator.QueueFault(ReactiveOperation.Read, ReactiveStatus.ErrRead, 1, PhysicalTagName);
        var fault = await client.ReadAsync(TagName);
        simulator.Disconnect();
        var disconnected = await client.ReadAsync(TagName);
        simulator.Reconnect();
        var recovered = await client.ReadAsync(TagName);

        await Assert.That(initial.Succeeded).IsTrue();
        await Assert.That(initial.Value!.Value).IsEqualTo(InitialValue);
        await Assert.That(write.Succeeded).IsTrue();
        await Assert.That(fault.Succeeded).IsFalse();
        await Assert.That(disconnected.Succeeded).IsFalse();
        await Assert.That(recovered.Succeeded).IsTrue();
        await Assert.That(simulator.GetTagValue<int>(PhysicalTagName, default)).IsEqualTo(UpdatedValue);
        await Assert.That(simulator.OperationLog).IsNotEmpty();
        _ = simulator.ObserveErrorsAsyncObservable();
    }
}
