// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.Driver.S7PlcRx.Enums;
using IoT.Driver.S7PlcRx.Mock;

namespace IoT.Driver.S7PlcRx.Tests;

/// <summary>Integration tests covering controlled S7PlcRx scenarios.</summary>
[NotInParallel]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public class S7PlcRxIntegrationTests
{
    /// <summary>Defines the default PLC rack.</summary>
    private const short DefaultRack = 0;

    /// <summary>Defines the default PLC slot.</summary>
    private const short DefaultSlot = 1;

    /// <summary>Defines the standard PLC polling interval.</summary>
    private const int StandardPollingIntervalMilliseconds = 100;

    /// <summary>Defines the fast PLC polling interval.</summary>
    private const int FastPollingIntervalMilliseconds = 10;

    /// <summary>Defines the maximum time allowed for a simulated connection state transition.</summary>
    private const int ConnectionStateTimeoutSeconds = 60;

    /// <summary>Defines the interval used to observe simulated connection state transitions.</summary>
    private const int ConnectionStatePollMilliseconds = 25;

    /// <summary>Defines the invalid maximum rack value.</summary>
    private const short InvalidRack = 8;

    /// <summary>Defines the invalid maximum slot value.</summary>
    private const short InvalidMaximumSlot = 32;

    /// <summary>Defines the byte tag name.</summary>
    private const string ByteTagName = "TestByte";

    /// <summary>Defines the byte tag address.</summary>
    private const string ByteTagAddress = "DB1.DBB0";

    /// <summary>Defines the word tag name.</summary>
    private const string WordTagName = "TestWord";

    /// <summary>Defines the word tag address.</summary>
    private const string WordTagAddress = "DB1.DBW0";

    /// <summary>Defines the default watchdog value.</summary>
    private const ushort DefaultWatchdogValue = 4500;

    /// <summary>Defines the performance tag count.</summary>
    private const int PerformanceTagCount = 50;

    /// <summary>Defines the maximum expected per-PLC memory usage.</summary>
    private const long MaximumMemoryUsagePerPlc = 1_000_000;

    /// <summary>Defines the synchronous tag value used by the test.</summary>
    private const ushort SynchronousTagValue = 1234;

    /// <summary>Defines the byte-array tag length.</summary>
    private const int ByteArrayLength = 10;

    /// <summary>Defines the real-array tag length.</summary>
    private const int RealArrayLength = 5;

    /// <summary>Defines the number of PLCs used by the memory test.</summary>
    private const int MemoryTestPlcCount = 10;

    /// <summary>Defines the number of tags created per PLC by the memory test.</summary>
    private const int MemoryTestTagCount = 10;

    /// <summary>Defines the byte offset multiplier for word tags.</summary>
    private const int WordByteOffsetMultiplier = 2;

    /// <summary>Gets a debugger-friendly test fixture name.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => GetType().Name;

    /// <summary>Test basic PLC creation and configuration.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task S7PlcCreation_WithDifferentTypes_ShouldWorkCorrectly()
    {
        _ = DebuggerDisplay;

        // Test S71500 creation
        using var plc1500 = S71500.Create(
            MockServer.Localhost,
            DefaultRack,
            DefaultSlot,
            null,
            StandardPollingIntervalMilliseconds);
        await Assert.That(plc1500, Is.Not.Null);
        await Assert.That(plc1500.PLCType, Is.EqualTo(CpuType.S71500));

        // Test S7400 creation
        using var plc400 = new RxS7(new(new(CpuType.S7400, MockServer.Localhost, DefaultRack, DefaultSlot)));
        await Assert.That(plc400, Is.Not.Null);
        await Assert.That(plc400.PLCType, Is.EqualTo(CpuType.S7400));

        // Test S7300 creation
        using var plc300 = new RxS7(new(new(CpuType.S7300, MockServer.Localhost, DefaultRack, DefaultSlot)));
        await Assert.That(plc300, Is.Not.Null);
        await Assert.That(plc300.PLCType, Is.EqualTo(CpuType.S7300));

        // Test S71200 creation
        using var plc1200 = new RxS7(new(new(CpuType.S71200, MockServer.Localhost, DefaultRack, DefaultSlot)));
        await Assert.That(plc1200, Is.Not.Null);
        await Assert.That(plc1200.PLCType, Is.EqualTo(CpuType.S71200));

        // Test S7200 creation
        using var plc200 = new RxS7(new(new(CpuType.S7200, MockServer.Localhost, DefaultRack, DefaultSlot)));
        await Assert.That(plc200, Is.Not.Null);
        await Assert.That(plc200.PLCType, Is.EqualTo(CpuType.S7200));
    }

    /// <summary>Test tag creation and management for different data types.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TagManagement_WithDifferentDataTypes_ShouldWorkCorrectly()
    {
        _ = DebuggerDisplay;

        // Arrange
        using var plc = S71500.Create(
            MockServer.Localhost,
            DefaultRack,
            DefaultSlot,
            null,
            StandardPollingIntervalMilliseconds);

        // Act & Assert - Test different data types
        var (byteTag, _) = TagOperations.AddUpdateTagItem(plc, typeof(byte), ByteTagName, ByteTagAddress);
        await Assert.That(byteTag, Is.Not.Null);

        var (wordTag, _) = TagOperations.AddUpdateTagItem(plc, typeof(ushort), WordTagName, "DB1.DBW2");
        await Assert.That(wordTag, Is.Not.Null);

        var (intTag, _) = TagOperations.AddUpdateTagItem(plc, typeof(short), "TestInt", "DB1.DBW4");
        await Assert.That(intTag, Is.Not.Null);

        var (dwordTag, _) = TagOperations.AddUpdateTagItem(plc, typeof(uint), "TestDWord", "DB1.DBD6");
        await Assert.That(dwordTag, Is.Not.Null);

        var (dintTag, _) = TagOperations.AddUpdateTagItem(plc, typeof(int), "TestDInt", "DB1.DBD10");
        await Assert.That(dintTag, Is.Not.Null);

        var (realTag, _) = TagOperations.AddUpdateTagItem(plc, typeof(float), "TestReal", "DB1.DBD14");
        await Assert.That(realTag, Is.Not.Null);

        var (lrealTag, _) = TagOperations.AddUpdateTagItem(plc, typeof(double), "TestLReal", "DB1.DBD18");
        await Assert.That(lrealTag, Is.Not.Null);

        // Test arrays
        var (byteArrayTag, _) = TagOperations.AddUpdateTagItem(
            plc,
            typeof(byte[]),
            "TestByteArray",
            "DB1.DBB26",
            ByteArrayLength);
        await Assert.That(byteArrayTag, Is.Not.Null);

        var (realArrayTag, _) = TagOperations.AddUpdateTagItem(
            plc,
            typeof(float[]),
            "TestRealArray",
            "DB1.DBD36",
            RealArrayLength);
        await Assert.That(realArrayTag, Is.Not.Null);

        // Verify tags are in TagList
        await Assert.That(plc.TagList.ContainsKey(ByteTagName), Is.True);
        await Assert.That(plc.TagList.ContainsKey(WordTagName), Is.True);
        await Assert.That(plc.TagList.ContainsKey("TestReal"), Is.True);
        await Assert.That(plc.TagList.ContainsKey("TestRealArray"), Is.True);
    }

    /// <summary>Test tag removal functionality.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TagRemoval_ShouldWorkCorrectly()
    {
        _ = DebuggerDisplay;

        // Arrange
        using var plc = S71500.Create(
            MockServer.Localhost,
            DefaultRack,
            DefaultSlot,
            null,
            StandardPollingIntervalMilliseconds);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(byte), ByteTagName, ByteTagAddress);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), WordTagName, "DB1.DBW2");

        // Act
        TagOperations.RemoveTagItem(plc, ByteTagName);

        // Assert
        await Assert.That(plc.TagList.ContainsKey(ByteTagName), Is.False);
        await Assert.That(plc.TagList.ContainsKey(WordTagName), Is.True);

        // Cleanup remaining tag
        TagOperations.RemoveTagItem(plc, WordTagName);
        await Assert.That(plc.TagList.ContainsKey(WordTagName), Is.False);
    }

    /// <summary>Test tag observables creation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TagObservables_ShouldBeCreated()
    {
        _ = DebuggerDisplay;

        // Arrange
        using var plc = S71500.Create(
            MockServer.Localhost,
            DefaultRack,
            DefaultSlot,
            null,
            StandardPollingIntervalMilliseconds);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), WordTagName, WordTagAddress);

        // Act
        var observable = plc.Observe(new LogicalTagKey<ushort>(WordTagName));

        // Assert
        await Assert.That(observable, Is.Not.Null);
        await Assert.That(observable, Is.AssignableTo<IObservable<ushort>>());
    }

    /// <summary>Test watchdog configuration.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WatchdogConfiguration_ShouldWorkCorrectly()
    {
        _ = DebuggerDisplay;

        // Arrange & Act
        using var plc = new RxS7(
            new(
                new(CpuType.S71500, MockServer.Localhost, DefaultRack, DefaultSlot),
                watchdog: new("DB10.DBW100")));

        // Assert
        await Assert.That(plc.WatchDogAddress, Is.EqualTo("DB10.DBW100"));
        await Assert.That(plc.WatchDogValueToWrite, Is.EqualTo(DefaultWatchdogValue));
        await Assert.That(plc.WatchDogWritingTime, Is.EqualTo(S7WatchdogOptions.DefaultIntervalSeconds));

        // Test invalid watchdog address
        var ex = await Assert.Throws<ArgumentException>(static () =>
        {
            _ = new RxS7(
                new(
                    new(CpuType.S71500, MockServer.Localhost, DefaultRack, DefaultSlot),
                    watchdog: new("DB10.DBB100")));
        });
        await Assert.That(ex?.Message, Does.Contain("WatchDogAddress must be a DBW address"));
    }

    /// <summary>Test PLC status observables.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PLCStatusObservables_ShouldBeCreated()
    {
        _ = DebuggerDisplay;

        // Arrange
        using var plc = S71500.Create(
            MockServer.Localhost,
            DefaultRack,
            DefaultSlot,
            null,
            StandardPollingIntervalMilliseconds);

        // Act & Assert
        await Assert.That(plc.IsConnected, Is.Not.Null);
        await Assert.That(plc.LastError, Is.Not.Null);
        await Assert.That(plc.LastErrorCode, Is.Not.Null);
        await Assert.That(plc.Status, Is.Not.Null);
        await Assert.That(plc.ObserveAll, Is.Not.Null);
        await Assert.That(plc.IsPaused, Is.Not.Null);
        await Assert.That(plc.ReadTime, Is.Not.Null);
    }

    /// <summary>Test error handling with invalid parameters.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ErrorHandling_WithInvalidParameters_ShouldThrowCorrectExceptions()
    {
        _ = DebuggerDisplay;

        // Test invalid rack
        var ex1 = await Assert.Throws<ArgumentOutOfRangeException>(
            static () => S71500.Create(MockServer.Localhost, -1, DefaultSlot));
        await Assert.That(ex1?.ParamName, Is.EqualTo("rack"));

        var ex2 = await Assert.Throws<ArgumentOutOfRangeException>(
            static () => S71500.Create(MockServer.Localhost, InvalidRack, DefaultSlot));
        await Assert.That(ex2?.ParamName, Is.EqualTo("rack"));

        // Test invalid slot
        var ex3 = await Assert.Throws<ArgumentOutOfRangeException>(
            static () => S71500.Create(MockServer.Localhost, DefaultRack, DefaultRack));
        await Assert.That(ex3?.ParamName, Is.EqualTo("slot"));

        var ex4 = await Assert.Throws<ArgumentOutOfRangeException>(
            static () => S71500.Create(MockServer.Localhost, DefaultRack, InvalidMaximumSlot));
        await Assert.That(ex4?.ParamName, Is.EqualTo("slot"));
    }

    /// <summary>Test address parsing for different memory areas.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AddressParsing_WithDifferentMemoryAreas_ShouldWorkCorrectly()
    {
        _ = DebuggerDisplay;

        // Arrange
        using var plc = S71500.Create(
            MockServer.Localhost,
            DefaultRack,
            DefaultSlot,
            null,
            StandardPollingIntervalMilliseconds);

        // Act & Assert - Data Block addresses
        await Assert.DoesNotThrow(() => _ = TagOperations.AddUpdateTagItem(plc, typeof(byte), "DB_Test", ByteTagAddress));

        await Assert.DoesNotThrow(() => _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), "DBW_Test", WordTagAddress));

        await Assert.DoesNotThrow(() => _ = TagOperations.AddUpdateTagItem(plc, typeof(uint), "DBD_Test", "DB1.DBD0"));

        await Assert.DoesNotThrow(() => _ = TagOperations.AddUpdateTagItem(plc, typeof(bool), "DBX_Test", "DB1.DBX0.0"));

        // Input addresses
        await Assert.DoesNotThrow(() => _ = TagOperations.AddUpdateTagItem(plc, typeof(byte), "IB_Test", "IB0"));

        await Assert.DoesNotThrow(() => _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), "IW_Test", "IW0"));

        await Assert.DoesNotThrow(() => _ = TagOperations.AddUpdateTagItem(plc, typeof(bool), "I_Test", "I0.0"));

        // Output addresses
        await Assert.DoesNotThrow(() => _ = TagOperations.AddUpdateTagItem(plc, typeof(byte), "QB_Test", "QB0"));

        await Assert.DoesNotThrow(() => _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), "QW_Test", "QW0"));

        // Memory addresses
        await Assert.DoesNotThrow(() => _ = TagOperations.AddUpdateTagItem(plc, typeof(byte), "MB_Test", "MB0"));

        await Assert.DoesNotThrow(() => _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), "MW_Test", "MW0"));

        // Timer and Counter
        await Assert.DoesNotThrow(() => _ = TagOperations.AddUpdateTagItem(plc, typeof(double), "T_Test", "T1"));

        await Assert.DoesNotThrow(() => _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), "C_Test", "C1"));
    }

    /// <summary>Test CPU information observable creation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetCpuInfo_ShouldReturnObservable()
    {
        _ = DebuggerDisplay;

        // Arrange
        using var plc = S71500.Create(
            MockServer.Localhost,
            DefaultRack,
            DefaultSlot,
            null,
            StandardPollingIntervalMilliseconds);

        // Act
        var cpuInfoObservable = plc.GetCpuInfo();

        // Assert
        await Assert.That(cpuInfoObservable, Is.Not.Null);
        await Assert.That(cpuInfoObservable, Is.AssignableTo<IObservable<string[]>>());
    }

    /// <summary>Test high-frequency tag operations simulation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HighFrequencyOperations_Simulation_ShouldBeStable()
    {
        _ = DebuggerDisplay;

        // Arrange
        // Fast interval.
        using var plc = S71500.Create(
            MockServer.Localhost,
            DefaultRack,
            DefaultSlot,
            null,
            FastPollingIntervalMilliseconds);

        // Act - Create many tags quickly
        var startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();

        for (var i = 0; i < PerformanceTagCount; i++)
        {
            _ = TagOperations.AddUpdateTagItem(
                plc,
                typeof(ushort),
                $"PerfTag{i}",
                $"DB1.DBW{i * WordByteOffsetMultiplier}");
        }

        // Assert
        var creationRate = PerformanceTagCount / System.Diagnostics.Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
        await Assert.That(
            creationRate,
            Is.GreaterThan(StandardPollingIntervalMilliseconds),
            "Tag creation should be fast");

        await Assert.That(plc.TagList.Count, Is.EqualTo(PerformanceTagCount), "All tags should be created");
    }

    /// <summary>Test memory usage patterns.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemoryUsage_WithManyTags_ShouldBeReasonable()
    {
        _ = DebuggerDisplay;

        // Force garbage collection before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryBefore = GC.GetTotalMemory(false);

        // Create and dispose of multiple PLCs
        for (var i = 0; i < MemoryTestPlcCount; i++)
        {
            using var plc = S71500.Create(
                MockServer.Localhost,
                DefaultRack,
                DefaultSlot,
                null,
                StandardPollingIntervalMilliseconds);

            // Add tags
            for (var j = 0; j < MemoryTestTagCount; j++)
            {
                _ = TagOperations.AddUpdateTagItem(
                    plc,
                    typeof(ushort),
                    $"Tag{j}",
                    $"DB1.DBW{j * WordByteOffsetMultiplier}");
            }

            // Simulate some operations
            var observable = plc.Observe(new LogicalTagKey<ushort>("Tag0"));
            await Assert.That(observable, Is.Not.Null);
        }

        // Force garbage collection after operations
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryAfter = GC.GetTotalMemory(false);
        var memoryUsed = memoryAfter - memoryBefore;

        // Assert reasonable memory usage
        var memoryPerPLC = memoryUsed / MemoryTestPlcCount;

        await Assert.That(
            memoryPerPLC,
            Is.LessThan(MaximumMemoryUsagePerPlc),
            $"Memory usage should be reasonable. Actual: {memoryPerPLC} bytes per PLC");
    }

    /// <summary>Test resource disposal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ResourceDisposal_ShouldCleanupCorrectly()
    {
        _ = DebuggerDisplay;

        // Arrange
        var plc = S71500.Create(
            MockServer.Localhost,
            DefaultRack,
            DefaultSlot,
            null,
            StandardPollingIntervalMilliseconds);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(byte), "TestTag", ByteTagAddress);

        // Act
        plc.Dispose();

        // Assert
        await Assert.That(plc.IsDisposed, Is.True, "PLC should be marked as disposed");

        // Verify multiple dispose calls don't cause issues
        await Assert.DoesNotThrow(() => plc.Dispose(), "Multiple dispose calls should be safe");
    }

    /// <summary>Test comprehensive PLC type coverage.</summary>
    /// <param name="cpuType">Type of the cpu.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments(CpuType.S71500)]
    [Arguments(CpuType.S7400)]
    [Arguments(CpuType.S7300)]
    [Arguments(CpuType.S71200)]
    [Arguments(CpuType.S7200)]
    public async Task PLCTypeSupport_ShouldCoverAllTypes(CpuType cpuType)
    {
        _ = DebuggerDisplay;

        // Arrange & Act
        using var plc = new RxS7(new(new(cpuType, MockServer.Localhost, DefaultRack, DefaultSlot)));

        // Assert
        await Assert.That(plc, Is.Not.Null);
        await Assert.That(plc.PLCType, Is.EqualTo(cpuType));
        await Assert.That(plc.IP, Is.EqualTo(MockServer.Localhost));
        await Assert.That(plc.Rack, Is.EqualTo(DefaultRack));
        await Assert.That(plc.Slot, Is.EqualTo(DefaultSlot));

        // Test tag creation works for all PLC types
        var (tag, _) = TagOperations.AddUpdateTagItem(plc, typeof(ushort), "TestTag", WordTagAddress);
        await Assert.That(tag, Is.Not.Null);
    }

    /// <summary>Test tag value setting and getting (synchronous).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TagValueOperations_Synchronous_ShouldWorkCorrectly()
    {
        _ = DebuggerDisplay;

        // Arrange
        using var plc = S71500.Create(
            MockServer.Localhost,
            DefaultRack,
            DefaultSlot,
            null,
            StandardPollingIntervalMilliseconds);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), WordTagName, WordTagAddress);

        // Act - Set value (this will be queued for when connection is available)
        await Assert.DoesNotThrow(
            () => plc.Value(WordTagName, SynchronousTagValue),
            "Setting value should not throw");

        // The actual value setting will be attempted when PLC connects
        // For this test, we just verify the API works
        await Assert.Pass("Tag value operations API test completed");
    }

    /// <summary>Test connection reconnection after simulated cable unplug.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ConnectionReconnection_AfterCableUnplug_ShouldReconnectAsync()
    {
        _ = DebuggerDisplay;

        // Arrange
        using var server = new MockServer();
        await TUnit.Assertions.Assert.That(server.Start()).IsEqualTo(0);
        using var plc = S71500.Create(
            MockServer.Localhost,
            DefaultRack,
            DefaultSlot,
            null,
            StandardPollingIntervalMilliseconds);

        // Wait for initial connection.
        await WaitForConnectionStateAsync(plc, true);

        // Act - Simulate cable unplug by stopping server
        _ = server.Stop();

        // Wait for disconnection.
        await WaitForConnectionStateAsync(plc, false);

        // Restart server to simulate cable plug back
        await TUnit.Assertions.Assert.That(server.Start()).IsEqualTo(0);

        // Wait for reconnection.
        await WaitForConnectionStateAsync(plc, true);

        // Assert
        await Assert.That(plc.IsConnectedValue, Is.True, "PLC should reconnect after cable is plugged back");
    }

    /// <summary>Test connection reconnection after PLC stop and run.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ConnectionReconnection_AfterPLCStopRun_ShouldReconnectAsync()
    {
        _ = DebuggerDisplay;

        // Arrange
        using var server = new MockServer();
        await TUnit.Assertions.Assert.That(server.Start()).IsEqualTo(0);
        using var plc = S71500.Create(
            MockServer.Localhost,
            DefaultRack,
            DefaultSlot,
            null,
            StandardPollingIntervalMilliseconds);

        // Wait for initial connection.
        await WaitForConnectionStateAsync(plc, true);

        // Act - Simulate PLC stop by stopping server
        _ = server.Stop();

        // Wait for disconnection.
        await WaitForConnectionStateAsync(plc, false);

        // Simulate PLC run by starting server
        await TUnit.Assertions.Assert.That(server.Start()).IsEqualTo(0);

        // Wait for reconnection.
        await WaitForConnectionStateAsync(plc, true);

        // Assert
        await Assert.That(plc.IsConnectedValue, Is.True, "PLC should reconnect after PLC is run again");
    }

    /// <summary>Waits for the PLC to report the expected connection state within a bounded interval.</summary>
    /// <param name="plc">The PLC whose state is observed.</param>
    /// <param name="expectedState">The expected connection state.</param>
    /// <returns>A task that represents the bounded wait.</returns>
    private static async Task WaitForConnectionStateAsync(IRxS7 plc, bool expectedState)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(ConnectionStateTimeoutSeconds));
        while (plc.IsConnectedValue != expectedState)
        {
            await Task.Delay(ConnectionStatePollMilliseconds, cancellation.Token).ConfigureAwait(false);
        }
    }
}
