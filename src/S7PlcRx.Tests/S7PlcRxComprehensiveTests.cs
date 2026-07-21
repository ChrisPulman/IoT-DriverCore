// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using MockS7Plc;
using S7PlcRx.Enums;

namespace S7PlcRx.Tests;

/// <summary>
/// Comprehensive tests for S7PlcRx functionality covering all PLC types and operations.
/// These tests validate the complete S7PlcRx library functionality without requiring physical PLCs.
/// </summary>
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public class S7PlcRxComprehensiveTests
{
    /// <summary>Standard byte tag address.</summary>
    private const string ByteAddress = "DB1.DBB0";

    /// <summary>Standard byte tag name.</summary>
    private const string ByteTagName = "TestByte";

    /// <summary>Standard Boolean tag name.</summary>
    private const string BoolTagName = "TestBool";

    /// <summary>Standard double-integer tag address.</summary>
    private const string DIntAddress = "DB1.DBD10";

    /// <summary>Process array tag name.</summary>
    private const string ProcessArrayTagName = "ProcessArray";

    /// <summary>Process bit tag name.</summary>
    private const string ProcessBitTagName = "ProcessBit";

    /// <summary>Process byte tag name.</summary>
    private const string ProcessByteTagName = "ProcessByte";

    /// <summary>Process real tag name.</summary>
    private const string ProcessRealTagName = "ProcessReal";

    /// <summary>Process word tag name.</summary>
    private const string ProcessWordTagName = "ProcessWord";

    /// <summary>Standard real tag address.</summary>
    private const string RealAddress = "DB1.DBD4";

    /// <summary>Standard real-array tag name.</summary>
    private const string RealArrayTagName = "TestRealArray";

    /// <summary>Standard real tag name.</summary>
    private const string RealTagName = "TestReal";

    /// <summary>Standard test tag name.</summary>
    private const string TagName = "TestTag";

    /// <summary>Standard word tag address.</summary>
    private const string WordAddress = "DB1.DBW2";

    /// <summary>Standard word tag name.</summary>
    private const string WordTagName = "TestWord";

    /// <summary>Standard watchdog tag address.</summary>
    private const string WatchdogAddress = "DB1.DBW0";

    /// <summary>Values used when testing array writes.</summary>
    private static readonly float[] Value = [1.1F, 2.2F, 3.3F, 4.4F, 5.5F];

    /// <summary>Gets the debugger display text.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => GetType().Name;

    /// <summary>Test creation of all supported PLC types.</summary>
    /// <param name="cpuType">Type of the cpu.</param>
    [Test]
    [Arguments(CpuType.S71500)]
    [Arguments(CpuType.S7400)]
    [Arguments(CpuType.S7300)]
    [Arguments(CpuType.S71200)]
    [Arguments(CpuType.S7200)]
    [Arguments(CpuType.Logo0BA8)]
    public void CreatePLC_AllSupportedTypes_ShouldSetCorrectProperties(CpuType cpuType)
    {
        _ = DebuggerDisplay;

        // Arrange & Act
        using var plc = new RxS7(new(new(cpuType, MockServer.Localhost, 0, 1)));

        // Assert
        Assert.That(plc, Is.Not.Null);
        Assert.That(plc.PLCType, Is.EqualTo(cpuType));
        Assert.That(plc.IP, Is.EqualTo(MockServer.Localhost));
        Assert.That(plc.Rack, Is.EqualTo(0));
        Assert.That(plc.Slot, Is.EqualTo(1));
        Assert.That(plc.IsDisposed, Is.False);
    }

    /// <summary>Test S71500 factory method with different configurations.</summary>
    [Test]
    public void S71500Factory_WithDifferentConfigurations_ShouldCreateCorrectly()
    {
        const int defaultIntervalMilliseconds = 100;
        const int fastIntervalMilliseconds = 50;
        _ = DebuggerDisplay;

        // Test basic creation
        using var plc1 = S71500.Create(MockServer.Localhost, 0, 1);
        Assert.That(plc1.PLCType, Is.EqualTo(CpuType.S71500));

        // Test with interval
        using var plc2 = S71500.Create(MockServer.Localhost, 0, 1, null, fastIntervalMilliseconds);
        Assert.That(plc2.PLCType, Is.EqualTo(CpuType.S71500));

        // Test with watchdog
        using var plc3 = S71500.Create(MockServer.Localhost, 0, 1, WatchdogAddress, defaultIntervalMilliseconds);
        Assert.That(plc3.WatchDogAddress, Is.EqualTo(WatchdogAddress));
    }

    /// <summary>Test comprehensive tag creation for all supported data types.</summary>
    [Test]
    public void TagCreation_AllDataTypes_ShouldWorkCorrectly()
    {
        const int byteArrayLength = 10;
        const int arrayLength = 5;
        const int realArrayLength = 8;
        const int registeredTagCount = 11;
        const int defaultIntervalMilliseconds = 100;
        _ = DebuggerDisplay;

        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, defaultIntervalMilliseconds);

        // Act & Assert - Basic types
        var (byteTag, _) = TagOperations.AddUpdateTagItem(plc, typeof(byte), ByteTagName, ByteAddress);
        ValidateTag(byteTag);

        var (wordTag, _) = TagOperations.AddUpdateTagItem(plc, typeof(ushort), WordTagName, WordAddress);
        ValidateTag(wordTag);

        var (intTag, _) =
            TagOperations.AddUpdateTagItem(plc, typeof(short), "TestInt", "DB1.DBW4");
        ValidateTag(intTag);

        var (dwordTag, _) =
            TagOperations.AddUpdateTagItem(plc, typeof(uint), "TestDWord", "DB1.DBD6");
        ValidateTag(dwordTag);

        var (dintTag, _) = TagOperations.AddUpdateTagItem(plc, typeof(int), "TestDInt", DIntAddress);
        ValidateTag(dintTag);

        var (realTag, _) = TagOperations.AddUpdateTagItem(plc, typeof(float), RealTagName, "DB1.DBD14");
        ValidateTag(realTag);

        var (lrealTag, _) = TagOperations.AddUpdateTagItem(plc, typeof(double), "TestLReal", "DB1.DBD18");
        ValidateTag(lrealTag);

        var (boolTag, _) =
            TagOperations.AddUpdateTagItem(plc, typeof(bool), BoolTagName, "DB1.DBX26.0");
        ValidateTag(boolTag);

        // Array types
        var (byteArrayTag, _) = TagOperations.AddUpdateTagItem(
            plc,
            typeof(byte[]),
            "TestByteArray",
            "DB1.DBB30",
            byteArrayLength);
        ValidateArrayTag(byteArrayTag);

        var (wordArrayTag, _) = TagOperations.AddUpdateTagItem(
            plc,
            typeof(ushort[]),
            "TestWordArray",
            "DB1.DBW40",
            arrayLength);
        ValidateArrayTag(wordArrayTag);

        var (realArrayTag, _) = TagOperations.AddUpdateTagItem(
            plc,
            typeof(float[]),
            RealArrayTagName,
            "DB1.DBD50",
            realArrayLength);
        ValidateArrayTag(realArrayTag);

        // Verify all tags are in TagList
        Assert.That(plc.TagList.Count, Is.EqualTo(registeredTagCount));
        foreach (var key in new[] { ByteTagName, WordTagName, RealTagName, RealArrayTagName })
        {
            Assert.That(plc.TagList.ContainsKey(key), Is.True, $"TagList should contain key '{key}'");
        }
    }

    /// <summary>Test memory area addressing for all supported types.</summary>
    [Test]
    public void MemoryAreaAddressing_AllTypes_ShouldBeSupported()
    {
        const int arrayLength = 5;
        const int defaultIntervalMilliseconds = 100;
        _ = DebuggerDisplay;

        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, defaultIntervalMilliseconds);

        // Data Block addressing
        var dbTests = new[]
        {
            () => TagOperations.AddUpdateTagItem(plc, typeof(byte), "DB_Byte", "DB1.DBB0"),
            () => TagOperations.AddUpdateTagItem(plc, typeof(ushort), "DB_Word", WatchdogAddress),
            () => TagOperations.AddUpdateTagItem(plc, typeof(uint), "DB_DWord", "DB1.DBD0"),
            () => TagOperations.AddUpdateTagItem(plc, typeof(bool), "DB_Bit", "DB1.DBX0.0"),
            () => TagOperations.AddUpdateTagItem(plc, typeof(byte[]), "DB_ByteArray", "DB1.DBB10", arrayLength),
        };

        foreach (var test in dbTests)
        {
            Assert.DoesNotThrow(() => test(), "Data Block addressing should work");
        }

        // Input addressing
        var inputTests = new[]
        {
            () => TagOperations.AddUpdateTagItem(plc, typeof(byte), "Input_Byte", "IB0"),
            () => TagOperations.AddUpdateTagItem(plc, typeof(ushort), "Input_Word", "IW0"),
            () => TagOperations.AddUpdateTagItem(plc, typeof(uint), "Input_DWord", "ID0"),
            () => TagOperations.AddUpdateTagItem(plc, typeof(bool), "Input_Bit", "I0.0"),
        };

        foreach (var test in inputTests)
        {
            Assert.DoesNotThrow(() => test(), "Input addressing should work");
        }

        // Output addressing
        var outputTests = new[]
        {
            () => TagOperations.AddUpdateTagItem(plc, typeof(byte), "Output_Byte", "QB0"),
            () => TagOperations.AddUpdateTagItem(plc, typeof(ushort), "Output_Word", "QW0"),
            () => TagOperations.AddUpdateTagItem(plc, typeof(uint), "Output_DWord", "QD0"),
            () => TagOperations.AddUpdateTagItem(plc, typeof(bool), "Output_Bit", "Q0.0"),
        };

        foreach (var test in outputTests)
        {
            Assert.DoesNotThrow(() => test(), "Output addressing should work");
        }

        // Memory addressing
        var memoryTests = new[]
        {
            () => TagOperations.AddUpdateTagItem(plc, typeof(byte), "Memory_Byte", "MB0"),
            () => TagOperations.AddUpdateTagItem(plc, typeof(ushort), "Memory_Word", "MW0"),
            () => TagOperations.AddUpdateTagItem(plc, typeof(double), "Memory_LReal", "MD0"),
            () => TagOperations.AddUpdateTagItem(plc, typeof(bool), "Memory_Bit", "M0.0"),
        };

        foreach (var test in memoryTests)
        {
            Assert.DoesNotThrow(() => test(), "Memory addressing should work");
        }

        // Timer and Counter
        Assert.That(TagOperations.AddUpdateTagItem(plc, typeof(double), "Timer_Test", "T1"), Is.Not.Null);
        Assert.That(TagOperations.AddUpdateTagItem(plc, typeof(ushort), "Counter_Test", "C1"), Is.Not.Null);
    }

    /// <summary>Test tag management operations.</summary>
    [Test]
    public void TagManagement_Operations_ShouldWorkCorrectly()
    {
        const int initialTagCount = 3;
        const int remainingTagCount = 2;
        const int defaultIntervalMilliseconds = 100;
        _ = DebuggerDisplay;

        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, defaultIntervalMilliseconds);

        // Add initial tags
        _ = TagOperations.AddUpdateTagItem(plc, typeof(byte), "Tag1", ByteAddress);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), "Tag2", WordAddress);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(float), "Tag3", RealAddress);

        Assert.That(plc.TagList.Count, Is.EqualTo(initialTagCount));

        // Test tag update (adding existing tag should update it)
        var (updatedTag, _) =
            TagOperations.AddUpdateTagItem(plc, typeof(byte), "Tag1", "DB1.DBB10"); // Different address
        Assert.That(updatedTag, Is.Not.Null);
        Assert.That(plc.TagList.Count, Is.EqualTo(initialTagCount)); // Count should remain the same

        // Test tag removal
        TagOperations.RemoveTagItem(plc, "Tag2");
        Assert.That(plc.TagList.Count, Is.EqualTo(remainingTagCount));
        Assert.That(plc.TagList.ContainsKey("Tag2"), Is.False);

        // Test removing non-existent tag (should not throw)
        Assert.DoesNotThrow(() => TagOperations.RemoveTagItem(plc, "NonExistentTag"));

        // Test tag retrieval
        var (retrievedTag, _) = TagOperations.GetTag(plc, "Tag1");
        Assert.That(retrievedTag, Is.Not.Null);
        Assert.That(retrievedTag, Is.Not.Null);

        var (nonExistentTag, _) = TagOperations.GetTag(plc, "NonExistentTag");
        Assert.That(nonExistentTag, Is.NullValue);
    }

    /// <summary>Test observable creation and basic functionality.</summary>
    [Test]
    public void Observables_ShouldBeCreatedAndFunctional()
    {
        const int defaultIntervalMilliseconds = 100;
        _ = DebuggerDisplay;

        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, defaultIntervalMilliseconds);

        // Test core observables exist
        Assert.That(plc.IsConnected, Is.Not.Null);
        Assert.That(plc.LastError, Is.Not.Null);
        Assert.That(plc.LastErrorCode, Is.Not.Null);
        Assert.That(plc.Status, Is.Not.Null);
        Assert.That(plc.ObserveAll, Is.Not.Null);
        Assert.That(plc.IsPaused, Is.Not.Null);
        Assert.That(plc.ReadTime, Is.Not.Null);

        // Test tag observables
        _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), TagName, WatchdogAddress);
        var tagObservable = plc.Observe(new LogicalTagKey<ushort>(TagName));
        Assert.That(tagObservable, Is.Not.Null);
        Assert.That(tagObservable, Is.AssignableTo<IObservable<ushort>>());

        // Test GetCpuInfo observable
        var cpuInfoObservable = plc.GetCpuInfo();
        Assert.That(cpuInfoObservable, Is.Not.Null);
        Assert.That(cpuInfoObservable, Is.AssignableTo<IObservable<string[]>>());
    }

    /// <summary>Test watchdog configuration and validation.</summary>
    [Test]
    public void WatchdogConfiguration_ShouldWorkCorrectly()
    {
        const int watchdogIntervalSeconds = 15;
        const int watchdogValue = 4500;
        _ = DebuggerDisplay;

        // Test valid watchdog configuration
        using var plc1 = new RxS7(
            new(
                new(CpuType.S71500, MockServer.Localhost, 0, 1),
                watchdog: new("DB10.DBW100", intervalSeconds: watchdogIntervalSeconds)));
        Assert.That(plc1.WatchDogAddress, Is.EqualTo("DB10.DBW100"));
        Assert.That(plc1.WatchDogValueToWrite, Is.EqualTo(watchdogValue));
        Assert.That(plc1.WatchDogWritingTime, Is.EqualTo(watchdogIntervalSeconds));

        // Test invalid watchdog address (non-DBW)
        var ex = Assert.Throws<ArgumentException>(
            () =>
            {
                _ = new RxS7(
                    new(new(CpuType.S71500, MockServer.Localhost, 0, 1), watchdog: new("DB10.DBB100")));
            });
        Assert.That(ex?.Message, Does.Contain("WatchDogAddress must be a DBW address"));

        // Test without watchdog
        using var plc2 = new RxS7(new(new(CpuType.S71500, MockServer.Localhost, 0, 1)));
        Assert.That(plc2.WatchDogAddress, Is.NullValue);
    }

    /// <summary>Test error handling for invalid parameters.</summary>
    [Test]
    public void ErrorHandling_InvalidParameters_ShouldThrowCorrectExceptions()
    {
        const int invalidRack = 8;
        const int invalidSlot = 32;
        const int defaultIntervalMilliseconds = 100;
        _ = DebuggerDisplay;

        // Invalid rack values
        var ex1 = Assert.Throws<ArgumentOutOfRangeException>(() => S71500.Create(MockServer.Localhost, -1, 1));
        Assert.That(ex1?.ParamName, Is.EqualTo("rack"));

        var ex2 = Assert.Throws<ArgumentOutOfRangeException>(() => S71500.Create(MockServer.Localhost, invalidRack, 1));
        Assert.That(ex2?.ParamName, Is.EqualTo("rack"));

        // Invalid slot values
        var ex3 = Assert.Throws<ArgumentOutOfRangeException>(() => S71500.Create(MockServer.Localhost, 0, 0));
        Assert.That(ex3?.ParamName, Is.EqualTo("slot"));

        var ex4 = Assert.Throws<ArgumentOutOfRangeException>(() => S71500.Create(MockServer.Localhost, 0, invalidSlot));
        Assert.That(ex4?.ParamName, Is.EqualTo("slot"));

        // Invalid tag operations
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, defaultIntervalMilliseconds);

        _ = Assert.Throws<ArgumentException>(() => TagOperations.RemoveTagItem(plc, null!));

        _ = Assert.Throws<ArgumentException>(() => TagOperations.RemoveTagItem(plc, string.Empty));
    }

    /// <summary>Test performance characteristics and resource usage.</summary>
    [Test]
    public void Performance_HighVolumeOperations_ShouldBeEfficient()
    {
        const int fastIntervalMilliseconds = 10;
        const int tagCount = 100;
        const int tagAddressStride = 2;
        const int requiredCreationRate = 1000;
        _ = DebuggerDisplay;

        // Test rapid tag creation
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, fastIntervalMilliseconds); // Fast interval

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < tagCount; i++)
        {
            _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), $"PerfTag{i}", $"DB1.DBW{i * tagAddressStride}");
        }

        stopwatch.Stop();

        var creationRate = tagCount / stopwatch.Elapsed.TotalSeconds;
        Assert.That(creationRate, Is.GreaterThan(requiredCreationRate), "Tag creation should be very fast");

        Assert.That(plc.TagList.Count, Is.EqualTo(tagCount));

        // Test rapid observable creation
        stopwatch.Restart();
        var observables = new List<IObservable<ushort>>();

        for (var i = 0; i < tagCount; i++)
        {
            observables.Add(plc.Observe(new LogicalTagKey<ushort>($"PerfTag{i}")));
        }

        stopwatch.Stop();

        var observableCreationRate = tagCount / stopwatch.Elapsed.TotalSeconds;
        Assert.That(
            observableCreationRate,
            Is.GreaterThan(requiredCreationRate),
            "Observable creation should be very fast");
    }

    /// <summary>Test memory usage patterns.</summary>
    [Test]
    public void MemoryUsage_MultipleInstances_ShouldBeReasonable()
    {
        const int instanceCount = 20;
        const int firstHostOctet = 100;
        const int defaultIntervalMilliseconds = 100;
        const int tagsPerInstance = 5;
        const int tagAddressStride = 2;
        const int maximumBytesPerInstance = 500_000;
        _ = DebuggerDisplay;

        // Force garbage collection before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryBefore = GC.GetTotalMemory(false);

        // Create multiple PLC instances
        var plcInstances = new List<IRxS7>();

        try
        {
            for (var i = 0; i < instanceCount; i++)
            {
                var plc = S71500.Create(
                    $"192.168.1.{firstHostOctet + i}",
                    0,
                    1,
                    null,
                    defaultIntervalMilliseconds);
                plcInstances.Add(plc);

                // Add some tags to each instance
                for (var j = 0; j < tagsPerInstance; j++)
                {
                    _ = TagOperations.AddUpdateTagItem(
                        plc,
                        typeof(ushort),
                        $"Tag{j}",
                        $"DB1.DBW{j * tagAddressStride}");
                }
            }

            var memoryAfterCreation = GC.GetTotalMemory(false);
            var memoryPerInstance = (memoryAfterCreation - memoryBefore) / instanceCount;

            Assert.That(
                memoryPerInstance,
                Is.LessThan(maximumBytesPerInstance),
                $"Memory usage per PLC instance should be reasonable. Actual: {memoryPerInstance} bytes");
        }
        finally
        {
            // Cleanup
            foreach (var plc in plcInstances)
            {
                plc.Dispose();
            }
        }

        // Force garbage collection after disposal
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    /// <summary>Test disposal and resource cleanup.</summary>
    [Test]
    public void Disposal_ShouldCleanupResourcesProperly()
    {
        const int defaultIntervalMilliseconds = 100;
        _ = DebuggerDisplay;

        // Arrange
        var plc = S71500.Create(MockServer.Localhost, 0, 1, null, defaultIntervalMilliseconds);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(byte), TagName, ByteAddress);

        Assert.That(plc.IsDisposed, Is.False);

        // Act
        plc.Dispose();

        // Assert
        Assert.That(plc.IsDisposed, Is.True);

        // Test multiple dispose calls
        Assert.DoesNotThrow(() => plc.Dispose(), "Multiple dispose calls should be safe");

        Assert.That(plc.IsDisposed, Is.True);
    }

    /// <summary>Test tag polling control.</summary>
    [Test]
    public void TagPolling_Control_ShouldWorkCorrectly()
    {
        const int defaultIntervalMilliseconds = 100;
        _ = DebuggerDisplay;

        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, defaultIntervalMilliseconds);

        // Test tag polling configuration
        var (tag, _) = TagOperations.AddUpdateTagItem(plc, typeof(ushort), TagName, WatchdogAddress);
        Assert.That(tag, Is.Not.Null);

        // Test disabling polling
        _ = TagOperations.GetTag(plc, TagName).SetPolling(false);

        // Test enabling polling
        _ = TagOperations.GetTag(plc, TagName).SetPolling(true);
    }

    /// <summary>Test value operations (synchronous API).</summary>
    [Test]
    public void ValueOperations_SynchronousAPI_ShouldWorkCorrectly()
    {
        const int defaultIntervalMilliseconds = 100;
        const ushort wordValue = 12_345;
        const float realValue = 3.14159F;
        const byte byteValue = 255;
        _ = DebuggerDisplay;

        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, defaultIntervalMilliseconds);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), WordTagName, WatchdogAddress);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(float), RealTagName, RealAddress);

        // Test setting values (these will be queued for when connection is available)
        Assert.DoesNotThrow(() => plc.Value(WordTagName, wordValue), "Setting Word value should not throw");

        Assert.DoesNotThrow(() => plc.Value(RealTagName, realValue), "Setting Real value should not throw");

        // Test with different data types
        _ = TagOperations.AddUpdateTagItem(plc, typeof(byte), ByteTagName, "DB1.DBB8");
        Assert.DoesNotThrow(() => plc.Value(ByteTagName, byteValue), "Setting Byte value should not throw");

        _ = TagOperations.AddUpdateTagItem(plc, typeof(bool), BoolTagName, "DB1.DBX10.0");
        Assert.DoesNotThrow(() => plc.Value(BoolTagName, true), "Setting Bool value should not throw");
    }

    /// <summary>Test reactive extensions integration.</summary>
    [Test]
    public void ReactiveExtensions_Integration_ShouldWorkCorrectly()
    {
        const int defaultIntervalMilliseconds = 100;
        _ = DebuggerDisplay;

        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, defaultIntervalMilliseconds);

        // Test tag to dictionary conversion
        _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), "Tag1", WatchdogAddress);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), "Tag2", WordAddress);

        var observable = plc.Observe(new LogicalTagKey<ushort>("Tag1"));
        var tagValueObservable = TagOperations.ToTagValue(observable, "Tag1");

        Assert.That(tagValueObservable, Is.Not.Null);
        Assert.That(tagValueObservable, Is.AssignableTo<IObservable<(string Tag, ushort Value)>>());

        // Test ObserveAll to dictionary conversion
        var dictionaryObservable = TagOperations.TagToDictionary(plc.ObserveAll);
        Assert.That(dictionaryObservable, Is.Not.Null);
        Assert.That(dictionaryObservable, Is.AssignableTo<IObservable<IDictionary<string, object>>>());
    }

    /// <summary>Test comprehensive scenario with mixed operations.</summary>
    [Test]
    public void ComprehensiveScenario_MixedOperations_ShouldWorkTogether()
    {
        const int fastIntervalMilliseconds = 50;
        _ = DebuggerDisplay;

        // Arrange - Create PLC with watchdog
        using var plc = new RxS7(
            new(
                new(CpuType.S71500, MockServer.Localhost, 0, 1),
                new(fastIntervalMilliseconds),
                new("DB100.DBW0")));
        RegisterScenarioTags(plc);
        ValidateScenarioObservables(plc);
        ValidateScenarioValues(plc);
        ValidateScenarioRuntimeOperations(plc);
    }

    /// <summary>Registers the tags required by the comprehensive scenario.</summary>
    /// <param name="plc">The PLC under test.</param>
    private static void RegisterScenarioTags(RxS7 plc)
    {
        const int processArrayLength = 10;
        const int processedTagCount = 12;
        _ = TagOperations.AddUpdateTagItem(plc, typeof(byte), ProcessByteTagName, ByteAddress);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), ProcessWordTagName, WordAddress);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(float), ProcessRealTagName, RealAddress);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(bool), ProcessBitTagName, "DB1.DBX8.0");
        _ = TagOperations.AddUpdateTagItem(
            plc,
            typeof(float[]),
            ProcessArrayTagName,
            DIntAddress,
            processArrayLength);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), "InputWord", "IW0");
        _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), "OutputWord", "QW0");
        _ = TagOperations.AddUpdateTagItem(plc, typeof(byte), "MemoryByte", "MB100");
        _ = TagOperations.AddUpdateTagItem(plc, typeof(bool), "MemoryBit", "M100.0");
        _ = TagOperations.AddUpdateTagItem(plc, typeof(double), "Timer1", "T1");
        _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), "Counter1", "C1");
        Assert.That(plc.TagList.Count, Is.EqualTo(processedTagCount));
    }

    /// <summary>Validates observables created for the comprehensive scenario.</summary>
    /// <param name="plc">The PLC under test.</param>
    private static void ValidateScenarioObservables(RxS7 plc)
    {
        var byteObs = plc.Observe(new LogicalTagKey<byte>(ProcessByteTagName));
        var wordObs = plc.Observe(new LogicalTagKey<ushort>(ProcessWordTagName));
        var realObs = plc.Observe(new LogicalTagKey<float>(ProcessRealTagName));
        var boolObs = plc.Observe(new LogicalTagKey<bool>(ProcessBitTagName));
        var arrayObs = plc.Observe(new LogicalTagKey<float[]>(ProcessArrayTagName));
        Assert.That(byteObs, Is.Not.Null);
        Assert.That(wordObs, Is.Not.Null);
        Assert.That(realObs, Is.Not.Null);
        Assert.That(boolObs, Is.Not.Null);
        Assert.That(arrayObs, Is.Not.Null);
    }

    /// <summary>Validates writes for the comprehensive scenario.</summary>
    /// <param name="plc">The PLC under test.</param>
    private static void ValidateScenarioValues(RxS7 plc)
    {
        const byte processByteValue = 100;
        const ushort processWordValue = 1000;
        const float processRealValue = 123.456F;
        const ushort inputWordValue = 2000;
        const ushort outputWordValue = 3000;
        const byte memoryByteValue = 200;
        var valueOperations = new Action[]
        {
            () => plc.Value(ProcessByteTagName, processByteValue),
            () => plc.Value(ProcessWordTagName, processWordValue),
            () => plc.Value(ProcessRealTagName, processRealValue),
            () => plc.Value(ProcessBitTagName, true),
            () => plc.Value(ProcessArrayTagName, Value),
            () => plc.Value("InputWord", inputWordValue),
            () => plc.Value("OutputWord", outputWordValue),
            () => plc.Value("MemoryByte", memoryByteValue),
            () => plc.Value("MemoryBit", false),
        };

        foreach (var operation in valueOperations)
        {
            Assert.DoesNotThrow(() => operation(), "Value setting operations should not throw");
        }
    }

    /// <summary>Validates run-time tag management and PLC observables.</summary>
    /// <param name="plc">The PLC under test.</param>
    private static void ValidateScenarioRuntimeOperations(RxS7 plc)
    {
        const int processedTagCount = 12;
        const int remainingProcessedTagCount = 11;
        const int watchdogValue = 4500;
        plc.RemoveTagItemInternal(ProcessBitTagName);
        Assert.That(plc.TagList.Count, Is.EqualTo(remainingProcessedTagCount));

        var (newTag, _) = TagOperations.AddUpdateTagItem(plc, typeof(int), "ProcessDInt", "DB1.DBD20");
        Assert.That(newTag, Is.Not.Null);
        Assert.That(plc.TagList.Count, Is.EqualTo(processedTagCount));
        Assert.That(plc.WatchDogAddress, Is.EqualTo("DB100.DBW0"));
        Assert.That(plc.WatchDogValueToWrite, Is.EqualTo(watchdogValue));
        Assert.That(plc.WatchDogWritingTime, Is.EqualTo(S7WatchdogOptions.DefaultIntervalSeconds));
        _ = TagOperations.GetTag(plc, ProcessWordTagName).SetPolling(false);
        var statusObs = plc.Status;
        var errorObs = plc.LastError;
        var connectedObs = plc.IsConnected;
        Assert.That(statusObs, Is.Not.Null);
        Assert.That(errorObs, Is.Not.Null);
        Assert.That(connectedObs, Is.Not.Null);
        Assert.Pass("Comprehensive scenario completed successfully");
    }

    /// <summary>Validates that a scalar tag was registered.</summary>
    /// <param name="tag">The tag to validate.</param>
    private static void ValidateTag(ITag? tag) => Assert.That(tag, Is.Not.Null);

    /// <summary>Validates that an array tag was registered.</summary>
    /// <param name="tag">The tag to validate.</param>
    private static void ValidateArrayTag(ITag? tag) => ValidateTag(tag);
}
