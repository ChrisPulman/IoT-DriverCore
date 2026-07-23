// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.S7PlcRx.Enums;
using IoT.DriverCore.S7PlcRx.Mock;

namespace IoT.DriverCore.S7PlcRx.Tests;

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
    [Test]
    public void S71500_Create_ShouldSetCorrectProperties()
    {
        _ = DebuggerDisplay;

        // Arrange & Act
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, ConnectionTimeoutMilliseconds);

        // Assert
        Assert.That(plc, Is.Not.Null);
        Assert.That(plc.IP, Is.EqualTo(MockServer.Localhost));
        Assert.That(plc.PLCType, Is.EqualTo(CpuType.S71500));
        Assert.That(plc.Rack, Is.EqualTo(0));
        Assert.That(plc.Slot, Is.EqualTo(1));
    }

    /// <summary>Test that different PLC types can be created.</summary>
    /// <param name="cpuType">The CPU type to test.</param>
    [Test]
    [Arguments(CpuType.S71500)]
    [Arguments(CpuType.S7300)]
    [Arguments(CpuType.S7400)]
    [Arguments(CpuType.S71200)]
    [Arguments(CpuType.S7200)]
    public void RxS7_Create_DifferentTypes_ShouldSetCorrectCpuType(CpuType cpuType)
    {
        _ = DebuggerDisplay;

        // Arrange & Act
        using var plc = new RxS7(new(new(cpuType, MockServer.Localhost, 0, 1)));

        // Assert
        Assert.That(plc, Is.Not.Null);
        Assert.That(plc.PLCType, Is.EqualTo(cpuType));
    }

    /// <summary>Test adding tags.</summary>
    [Test]
    public void AddUpdateTagItem_ShouldAddTagToCollection()
    {
        _ = DebuggerDisplay;

        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, ConnectionTimeoutMilliseconds);

        // Act
        var (tag, _) = TagOperations.AddUpdateTagItem(plc, typeof(byte), TestByteTagName, TestByteAddress);

        // Assert
        Assert.That(tag, Is.Not.Null);
        Assert.That(plc.TagList.ContainsKey(TestByteTagName), Is.True);
    }

    /// <summary>Test array tags with specified length.</summary>
    [Test]
    public void AddUpdateTagItem_ArrayWithLength_ShouldSetCorrectArrayLength()
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
        Assert.That(tag, Is.Not.Null);
        Assert.That(plc.TagList.ContainsKey("TestByteArray"), Is.True);
    }

    /// <summary>Test removing tags.</summary>
    [Test]
    public void RemoveTagItem_ShouldRemoveTagFromCollection()
    {
        _ = DebuggerDisplay;

        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, ConnectionTimeoutMilliseconds);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(byte), TestByteTagName, TestByteAddress);

        // Act
        TagOperations.RemoveTagItem(plc, TestByteTagName);

        // Assert
        Assert.That(plc.TagList.ContainsKey(TestByteTagName), Is.False);
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
        _ = Assert.Throws<ArgumentException>(
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
        Assert.That(completedTask, Is.SameAs(addTask));
        Assert.That(plc.TagList.ContainsKey("ValidByte"), Is.True);
    }

    /// <summary>Test observables are created correctly.</summary>
    [Test]
    public void Observables_ShouldBeCreated()
    {
        _ = DebuggerDisplay;

        // Arrange & Act
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, ConnectionTimeoutMilliseconds);

        // Assert
        Assert.That(plc.IsConnected, Is.Not.Null);
        Assert.That(plc.LastError, Is.Not.Null);
        Assert.That(plc.LastErrorCode, Is.Not.Null);
        Assert.That(plc.Status, Is.Not.Null);
        Assert.That(plc.ObserveAll, Is.Not.Null);
        Assert.That(plc.IsPaused, Is.Not.Null);
    }

    /// <summary>Test invalid rack parameter throws exception.</summary>
    /// <param name="invalidRack">Invalid rack value to test.</param>
    [Test]
    [Arguments(-1)]
    [Arguments(8)]
    public void S71500_Create_InvalidRack_ShouldThrowArgumentOutOfRangeException(short invalidRack)
    {
        _ = DebuggerDisplay;

        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => S71500.Create(MockServer.Localhost, invalidRack, 1));
        Assert.That(ex?.ParamName, Is.EqualTo("rack"));
    }

    /// <summary>Test invalid slot parameter throws exception.</summary>
    /// <param name="invalidSlot">Invalid slot value to test.</param>
    [Test]
    [Arguments(0)]
    [Arguments(32)]
    public void S71500_Create_InvalidSlot_ShouldThrowArgumentOutOfRangeException(short invalidSlot)
    {
        _ = DebuggerDisplay;

        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => S71500.Create(MockServer.Localhost, 0, invalidSlot));
        Assert.That(ex?.ParamName, Is.EqualTo("slot"));
    }

    /// <summary>Test watchdog configuration.</summary>
    [Test]
    public void RxS7_WithWatchdog_ShouldSetWatchdogProperties()
    {
        _ = DebuggerDisplay;

        // Arrange & Act
        using var plc = new RxS7(
            new(
                new(CpuType.S71500, MockServer.Localhost, 0, 1),
                watchdog: new(WatchdogAddress, WatchdogValue, WatchdogWriteIntervalSeconds)));

        // Assert
        Assert.That(plc.WatchDogAddress, Is.EqualTo(WatchdogAddress));
        Assert.That(plc.WatchDogValueToWrite, Is.EqualTo(WatchdogValue));
        Assert.That(plc.WatchDogWritingTime, Is.EqualTo(WatchdogWriteIntervalSeconds));
    }

    /// <summary>Test invalid watchdog address throws exception.</summary>
    [Test]
    public void RxS7_WithInvalidWatchdogAddress_ShouldThrowArgumentException()
    {
        _ = DebuggerDisplay;

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(
            () => _ = new RxS7(
                new(
                    new(CpuType.S71500, MockServer.Localhost, 0, 1),
                    watchdog: new("DB10.DBB0", WatchdogValue, WatchdogWriteIntervalSeconds))));
        Assert.That(ex?.Message, Does.Contain("WatchDogAddress must be a DBW address"));
    }

    /// <summary>Verifies the composed options use stable polling and watchdog defaults.</summary>
    [Test]
    public void RxS7Options_WithDefaults_ShouldComposeExpectedSettings()
    {
        _ = DebuggerDisplay;

        var options = new RxS7Options(new(CpuType.S71500, MockServer.Localhost, 0, 1));

        Assert.That(options.Polling.IntervalMilliseconds, Is.EqualTo(S7PollingOptions.DefaultIntervalMilliseconds));
        Assert.That(options.Watchdog, Is.NullValue);
        Assert.That(S7WatchdogOptions.DefaultValueToWrite, Is.EqualTo(ExpectedDefaultWatchdogValue));
        Assert.That(S7WatchdogOptions.DefaultIntervalSeconds, Is.EqualTo(ExpectedDefaultWatchdogIntervalSeconds));
    }

    /// <summary>Verifies null composed options are rejected before native resources are allocated.</summary>
    [Test]
    public void RxS7_WithNullOptions_ShouldThrowArgumentNullException()
    {
        _ = DebuggerDisplay;

        var exception = Assert.Throws<ArgumentNullException>(() => _ = new RxS7(null!));

        Assert.That(exception?.ParamName, Is.EqualTo(OptionsParameterName));
    }

    /// <summary>Verifies null connection settings are rejected before native resources are allocated.</summary>
    [Test]
    public void RxS7_WithNullConnectionOptions_ShouldThrowArgumentNullException()
    {
        _ = DebuggerDisplay;

        var exception = Assert.Throws<ArgumentNullException>(() => _ = new RxS7(new(null!)));

        Assert.That(exception?.ParamName, Is.EqualTo(OptionsParameterName));
    }

    /// <summary>Verifies invalid watchdog timing is rejected before native resources are allocated.</summary>
    [Test]
    public void RxS7_WithInvalidWatchdogInterval_ShouldThrowArgumentOutOfRangeException()
    {
        _ = DebuggerDisplay;

        var options = new RxS7Options(
            new(CpuType.S71500, MockServer.Localhost, 0, 1),
            watchdog: new(WatchdogAddress, intervalSeconds: 0));

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => _ = new RxS7(options));

        Assert.That(exception?.ParamName, Is.EqualTo(OptionsParameterName));
    }

    /// <summary>Test disposing of resources.</summary>
    [Test]
    public void Dispose_ShouldCleanupResources()
    {
        _ = DebuggerDisplay;

        // Arrange
        var plc = S71500.Create(MockServer.Localhost, 0, 1, null, ConnectionTimeoutMilliseconds);

        // Act
        plc.Dispose();

        // Assert
        Assert.That(plc.IsDisposed, Is.True);
    }
}
