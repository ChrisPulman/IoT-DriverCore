// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.Driver.S7PlcRx.Enums;
using IoT.Driver.S7PlcRx.Mock;

namespace IoT.Driver.S7PlcRx.Tests;

/// <summary>Basic functionality tests for S7PlcRx.</summary>
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public class S7PlcRxBasicTests
{
    /// <summary>Gets the PLC connection timeout in milliseconds.</summary>
    private const int ConnectionTimeoutMilliseconds = 100;

    /// <summary>Gets the tag name used by byte tag tests.</summary>
    private const string TestByteTagName = "TestByte";

    /// <summary>Gets the PLC address used by byte tag tests.</summary>
    private const string TestByteAddress = "DB1.DBB0";

    /// <summary>Gets the array length used by byte array tag tests.</summary>
    private const int TestByteArrayLength = 64;

    /// <summary>Gets the watchdog PLC address.</summary>
    private const string WatchdogAddress = "DB10.DBW0";

    /// <summary>Gets the watchdog value written during tests.</summary>
    private const int WatchdogValue = 5_000;

    /// <summary>Gets the watchdog write interval in seconds.</summary>
    private const int WatchdogWriteIntervalSeconds = 15;

    /// <summary>Gets the expected default watchdog value.</summary>
    private const int ExpectedDefaultWatchdogValue = 4_500;

    /// <summary>Gets the expected default watchdog interval in seconds.</summary>
    private const int ExpectedDefaultWatchdogIntervalSeconds = 10;

    /// <summary>Gets the argument name used by RxS7 options validation.</summary>
    private const string OptionsParameterName = "options";

    /// <summary>Gets the maximum wait time for asynchronous tag mutations in milliseconds.</summary>
    private const int TagMutationTimeoutMilliseconds = 1_000;

    /// <summary>Gets a debugger-friendly test fixture name.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => GetType().Name;

    /// <summary>Test that S71500 factory creates correct instance.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task S71500_Create_ShouldSetCorrectProperties()
    {
        _ = DebuggerDisplay;

        // Arrange & Act
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, ConnectionTimeoutMilliseconds);

        // Assert
        await Assert.That(plc, Is.Not.Null);
        await Assert.That(plc.IP, Is.EqualTo(MockServer.Localhost));
        await Assert.That(plc.PLCType, Is.EqualTo(CpuType.S71500));
        await Assert.That(plc.Rack, Is.EqualTo(0));
        await Assert.That(plc.Slot, Is.EqualTo(1));
    }

    /// <summary>Test that different PLC types can be created.</summary>
    /// <param name="cpuType">The CPU type to test.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Arguments(CpuType.S71500)]
    [Arguments(CpuType.S7300)]
    [Arguments(CpuType.S7400)]
    [Arguments(CpuType.S71200)]
    [Arguments(CpuType.S7200)]
    public async Task RxS7_Create_DifferentTypes_ShouldSetCorrectCpuType(CpuType cpuType)
    {
        _ = DebuggerDisplay;

        // Arrange & Act
        using var plc = new RxS7(new(new(cpuType, MockServer.Localhost, 0, 1)));

        // Assert
        await Assert.That(plc, Is.Not.Null);
        await Assert.That(plc.PLCType, Is.EqualTo(cpuType));
    }

    /// <summary>Test adding tags.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task AddUpdateTagItem_ShouldAddTagToCollection()
    {
        _ = DebuggerDisplay;

        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, ConnectionTimeoutMilliseconds);

        // Act
        var (tag, _) = TagOperations.AddUpdateTagItem(plc, typeof(byte), TestByteTagName, TestByteAddress);

        // Assert
        await Assert.That(tag, Is.Not.Null);
        await Assert.That(plc.TagList.ContainsKey(TestByteTagName), Is.True);
    }

    /// <summary>Test array tags with specified length.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task AddUpdateTagItem_ArrayWithLength_ShouldSetCorrectArrayLength()
    {
        _ = DebuggerDisplay;

        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, ConnectionTimeoutMilliseconds);

        // Act
        var (tag, _) = TagOperations.AddUpdateTagItem(
            plc,
            typeof(byte[]),
            "TestByteArray",
            TestByteAddress,
            TestByteArrayLength);

        // Assert
        await Assert.That(tag, Is.Not.Null);
        await Assert.That(plc.TagList.ContainsKey("TestByteArray"), Is.True);
    }

    /// <summary>Test removing tags.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task RemoveTagItem_ShouldRemoveTagFromCollection()
    {
        _ = DebuggerDisplay;

        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, ConnectionTimeoutMilliseconds);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(byte), TestByteTagName, TestByteAddress);

        // Act
        TagOperations.RemoveTagItem(plc, TestByteTagName);

        // Assert
        await Assert.That(plc.TagList.ContainsKey(TestByteTagName), Is.False);
    }

    /// <summary>Test that a failed tag add does not prevent later tag mutations.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AddUpdateTagItem_InvalidName_ShouldNotBlockLaterTagMutationsAsync()
    {
        _ = DebuggerDisplay;

        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, ConnectionTimeoutMilliseconds);

        // Act
        _ = await Assert.Throws<ArgumentException>(
            () => _ = TagOperations.AddUpdateTagItem(plc, typeof(byte), null!, TestByteAddress));
        var addTask = Task.Run(() => TagOperations.AddUpdateTagItem(plc, typeof(byte), "ValidByte", "DB1.DBB1"));
        using var timeoutCancellation = new CancellationTokenSource();
        var timeoutTask = Task.Delay(TagMutationTimeoutMilliseconds, timeoutCancellation.Token);
        var completedTask = await Task.WhenAny(addTask, timeoutTask);
#if NETFRAMEWORK
        timeoutCancellation.Cancel();
#else
        await timeoutCancellation.CancelAsync();
#endif

        // Assert
        await Assert.That(completedTask, Is.SameAs(addTask));
        await Assert.That(plc.TagList.ContainsKey("ValidByte"), Is.True);
    }

    /// <summary>Test observables are created correctly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Observables_ShouldBeCreated()
    {
        _ = DebuggerDisplay;

        // Arrange & Act
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, ConnectionTimeoutMilliseconds);

        // Assert
        await Assert.That(plc.IsConnected, Is.Not.Null);
        await Assert.That(plc.LastError, Is.Not.Null);
        await Assert.That(plc.LastErrorCode, Is.Not.Null);
        await Assert.That(plc.Status, Is.Not.Null);
        await Assert.That(plc.ObserveAll, Is.Not.Null);
        await Assert.That(plc.IsPaused, Is.Not.Null);
    }

    /// <summary>Test invalid rack parameter throws exception.</summary>
    /// <param name="invalidRack">Invalid rack value to test.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Arguments(-1)]
    [Arguments(8)]
    public async Task S71500_Create_InvalidRack_ShouldThrowArgumentOutOfRangeException(short invalidRack)
    {
        _ = DebuggerDisplay;

        // Act & Assert
        var ex = await Assert.Throws<ArgumentOutOfRangeException>(
            () => S71500.Create(MockServer.Localhost, invalidRack, 1));
        await Assert.That(ex?.ParamName, Is.EqualTo("rack"));
    }

    /// <summary>Test invalid slot parameter throws exception.</summary>
    /// <param name="invalidSlot">Invalid slot value to test.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(32)]
    public async Task S71500_Create_InvalidSlot_ShouldThrowArgumentOutOfRangeException(short invalidSlot)
    {
        _ = DebuggerDisplay;

        // Act & Assert
        var ex = await Assert.Throws<ArgumentOutOfRangeException>(
            () => S71500.Create(MockServer.Localhost, 0, invalidSlot));
        await Assert.That(ex?.ParamName, Is.EqualTo("slot"));
    }

    /// <summary>Test watchdog configuration.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task RxS7_WithWatchdog_ShouldSetWatchdogProperties()
    {
        _ = DebuggerDisplay;

        // Arrange & Act
        using var plc = new RxS7(
            new(
                new(CpuType.S71500, MockServer.Localhost, 0, 1),
                watchdog: new(WatchdogAddress, WatchdogValue, WatchdogWriteIntervalSeconds)));

        // Assert
        await Assert.That(plc.WatchDogAddress, Is.EqualTo(WatchdogAddress));
        await Assert.That(plc.WatchDogValueToWrite, Is.EqualTo(WatchdogValue));
        await Assert.That(plc.WatchDogWritingTime, Is.EqualTo(WatchdogWriteIntervalSeconds));
    }

    /// <summary>Test invalid watchdog address throws exception.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task RxS7_WithInvalidWatchdogAddress_ShouldThrowArgumentException()
    {
        _ = DebuggerDisplay;

        // Act & Assert
        var ex = await Assert.Throws<ArgumentException>(
            static () => _ = new RxS7(
                new(
                    new(CpuType.S71500, MockServer.Localhost, 0, 1),
                    watchdog: new("DB10.DBB0", WatchdogValue, WatchdogWriteIntervalSeconds))));
        await Assert.That(ex?.Message, Does.Contain("WatchDogAddress must be a DBW address"));
    }

    /// <summary>Verifies the composed options use stable polling and watchdog defaults.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task RxS7Options_WithDefaults_ShouldComposeExpectedSettings()
    {
        _ = DebuggerDisplay;

        var options = new RxS7Options(new(CpuType.S71500, MockServer.Localhost, 0, 1));

        await Assert.That(options.Polling.IntervalMilliseconds, Is.EqualTo(S7PollingOptions.DefaultIntervalMilliseconds));
        await Assert.That(options.Watchdog, Is.NullValue);
        await Assert.That(S7WatchdogOptions.DefaultValueToWrite, Is.EqualTo(ExpectedDefaultWatchdogValue));
        await Assert.That(
            S7WatchdogOptions.DefaultIntervalSeconds,
            Is.EqualTo(ExpectedDefaultWatchdogIntervalSeconds));
    }

    /// <summary>Verifies null composed options are rejected before native resources are allocated.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task RxS7_WithNullOptions_ShouldThrowArgumentNullException()
    {
        _ = DebuggerDisplay;

        var exception = await Assert.Throws<ArgumentNullException>(static () => _ = new RxS7(null!));

        await Assert.That(exception?.ParamName, Is.EqualTo(OptionsParameterName));
    }

    /// <summary>Verifies null connection settings are rejected before native resources are allocated.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task RxS7_WithNullConnectionOptions_ShouldThrowArgumentNullException()
    {
        _ = DebuggerDisplay;

        var exception = await Assert.Throws<ArgumentNullException>(static () => _ = new RxS7(new(null!)));

        await Assert.That(exception?.ParamName, Is.EqualTo(OptionsParameterName));
    }

    /// <summary>Verifies invalid watchdog timing is rejected before native resources are allocated.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task RxS7_WithInvalidWatchdogInterval_ShouldThrowArgumentOutOfRangeException()
    {
        _ = DebuggerDisplay;

        var options = new RxS7Options(
            new(CpuType.S71500, MockServer.Localhost, 0, 1),
            watchdog: new(WatchdogAddress, intervalSeconds: 0));

        var exception = await Assert.Throws<ArgumentOutOfRangeException>(() => _ = new RxS7(options));

        await Assert.That(exception?.ParamName, Is.EqualTo(OptionsParameterName));
    }

    /// <summary>Test disposing of resources.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Dispose_ShouldCleanupResources()
    {
        _ = DebuggerDisplay;

        // Arrange
        var plc = S71500.Create(MockServer.Localhost, 0, 1, null, ConnectionTimeoutMilliseconds);

        // Act
        plc.Dispose();

        // Assert
        await Assert.That(plc.IsDisposed, Is.True);
    }
}
