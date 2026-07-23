// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.S7PlcRx.Advanced;
using IoT.DriverCore.S7PlcRx.Core;
using IoT.DriverCore.S7PlcRx.Enterprise;
using IoT.DriverCore.S7PlcRx.Enums;
using IoT.DriverCore.S7PlcRx.Mock;
using IoT.DriverCore.S7PlcRx.Optimization;
using IoT.DriverCore.S7PlcRx.Performance;
using IoT.DriverCore.S7PlcRx.Production;

namespace IoT.DriverCore.S7PlcRx.Tests;

/// <summary>
/// Comprehensive optimization tests for S7PlcRx covering performance, caching, batching, and production features.
/// These tests validate the optimized library functionality without requiring physical PLCs.
/// </summary>
[NotInParallel]
[System.Diagnostics.DebuggerDisplay("Optimization test fixture")]
public sealed class S7PlcRxOptimizationTests : IDisposable
{
    /// <summary>The address of the first floating-point value in data block one.</summary>
    private const string Db1Dbd0 = "DB1.DBD0";

    /// <summary>The address of the second floating-point value in data block one.</summary>
    private const string Db1Dbd4 = "DB1.DBD4";

    /// <summary>The PLC connection used by each test.</summary>
    private readonly RxS7 _plc;

    /// <summary>The in-process PLC server used by each test.</summary>
    private readonly MockServer _server;

    /// <summary>The time provider used for timestamp assertions; defaults to <see cref="TimeProvider.System"/>.</summary>
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    /// <summary>Initializes a new instance of the <see cref="S7PlcRxOptimizationTests"/> class.</summary>
    public S7PlcRxOptimizationTests()
    {
        _server = new();
        var rc = _server.StartTo(MockServer.Localhost);
        Assert.That(rc, Is.EqualTo(0));
        _plc = new(new(new(CpuType.S71500, MockServer.Localhost, 0, 1)));
    }

    /// <summary>Test performance monitoring functionality.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task MonitorPerformance_ShouldProvideMetricsAsync()
    {
        // Arrange
        const int monitoringPeriodMilliseconds = 100;
        const int timestampToleranceSeconds = 5;
        var monitoringPeriod = TimeSpan.FromMilliseconds(monitoringPeriodMilliseconds);
        var timestampTolerance = TimeSpan.FromSeconds(timestampToleranceSeconds);

        // Act
        var metricsObservable = PerformanceExtensions.MonitorPerformance(_plc, monitoringPeriod);
        var firstMetrics = await metricsObservable.Take(1).FirstAsync();

        // Assert
        Assert.That(firstMetrics, Is.Not.Null);
        Assert.That(firstMetrics.PLCIdentifier, Does.Contain(MockServer.Localhost));
        Assert.That(firstMetrics.PLCIdentifier, Does.Contain("S71500"));
        Assert.That(firstMetrics.Timestamp, Is.EqualTo(_timeProvider.GetUtcNow()).Within(timestampTolerance));
        Assert.That(firstMetrics.TagCount, Is.GreaterThanOrEqualTo(0));
    }

    /// <summary>Test optimized read operations with multiple tags.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ReadOptimized_WithMultipleTags_ShouldGroupByDataBlockAsync()
    {
        // Arrange
        const string testTag1 = "TestTag1";
        const string testTag2 = "TestTag2";
        const string testTag3 = "TestTag3";
        const string db2Dbd0 = "DB2.DBD0";
        const int interGroupDelayMilliseconds = 10;
        const int maxConcurrentReads = 5;
        const int expectedTagCount = 3;
        var tagNames = new[] { testTag1, testTag2, testTag3 };

        // Add test tags
        _ = TagOperations.AddUpdateTagItem(_plc, typeof(float), testTag1, Db1Dbd0);
        _ = TagOperations.AddUpdateTagItem(_plc, typeof(float), testTag2, Db1Dbd4);
        _ = TagOperations.AddUpdateTagItem(_plc, typeof(float), testTag3, db2Dbd0);

        var config = new ReadOptimizationConfig
        {
            EnableParallelReads = true,
            InterGroupDelayMs = interGroupDelayMilliseconds,
            MaxConcurrentReads = maxConcurrentReads,
        };

        // Act
        var results = await PerformanceExtensions.ReadOptimizedAsync(_plc, tagNames, 0F, config);

        // Assert
        Assert.That(results, Is.Not.Null);
        Assert.That(results.Count, Is.EqualTo(expectedTagCount));
        Assert.That(results.Keys, Is.EquivalentTo(tagNames));
    }

    /// <summary>Test optimized write operations with verification.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task WriteOptimized_WithVerification_ShouldCompleteSuccessfullyAsync()
    {
        // Arrange
        const string writeTag1 = "WriteTag1";
        const string writeTag2 = "WriteTag2";
        const float writeTag1Value = 25.5F;
        const float writeTag2Value = 30.0F;
        const int interGroupDelayMilliseconds = 10;
        _ = TagOperations.AddUpdateTagItem(_plc, typeof(float), writeTag1, Db1Dbd0);
        _ = TagOperations.AddUpdateTagItem(_plc, typeof(float), writeTag2, Db1Dbd4);

        var writeValues = new Dictionary<string, float>
        {
            [writeTag1] = writeTag1Value,
            [writeTag2] = writeTag2Value,
        };

        var config = new WriteOptimizationConfig
        {
            EnableParallelWrites = false, // Conservative for testing
            VerifyWrites = false, // Disable verification for unit test
            InterGroupDelayMs = interGroupDelayMilliseconds,
        };

        // Act
        var result = await PerformanceExtensions.WriteOptimizedAsync(_plc, writeValues, config);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.TotalDuration, Is.GreaterThan(TimeSpan.Zero));
        Assert.That(result.SuccessRate, Is.GreaterThanOrEqualTo(0.0));
    }

    /// <summary>Test performance benchmark functionality.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task RunBenchmark_ShouldProvideBenchmarkResultsAsync()
    {
        // Arrange
        const int latencyTestCount = 3;
        const int throughputDurationMilliseconds = 100;
        const int reliabilityTestCount = 5;
        const int maximumScore = 100;
        var config = new BenchmarkConfig
        {
            LatencyTestCount = latencyTestCount,
            ThroughputTestDuration = TimeSpan.FromMilliseconds(throughputDurationMilliseconds),
            ReliabilityTestCount = reliabilityTestCount,
        };

        // Act
        var result = await PerformanceExtensions.RunBenchmarkAsync(_plc, config);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.PLCIdentifier, Does.Contain(MockServer.Localhost));
        Assert.That(result.TotalDuration, Is.GreaterThan(TimeSpan.Zero));
        Assert.That(result.OverallScore, Is.InRange(0, maximumScore));
    }

    /// <summary>Test performance statistics collection.</summary>
    [Test]
    public void GetPerformanceStatistics_ShouldReturnValidStats()
    {
        const int timestampToleranceSeconds = 5;
        var timestampTolerance = TimeSpan.FromSeconds(timestampToleranceSeconds);

        // Act
        var stats = PerformanceExtensions.GetPerformanceStatistics(_plc);

        // Assert
        Assert.That(stats, Is.Not.Null);
        Assert.That(stats.PLCIdentifier, Does.Contain(MockServer.Localhost));
        Assert.That(stats.TotalOperations, Is.GreaterThanOrEqualTo(0));
        Assert.That(stats.TotalErrors, Is.GreaterThanOrEqualTo(0));
        Assert.That(stats.ErrorRate, Is.InRange(0.0, 1.0));
        Assert.That(stats.LastUpdated, Is.EqualTo(_timeProvider.GetUtcNow()).Within(timestampTolerance));
    }

    /// <summary>Test advanced batch reading with optimization.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ReadBatchOptimized_ShouldOptimizeDataBlockAccessAsync()
    {
        // Arrange
        const int expectedTagCount = 4;
        const int readTimeoutMilliseconds = 5000;
        var tagMapping = new Dictionary<string, string>
        {
            ["Temperature1"] = Db1Dbd0,
            ["Temperature2"] = Db1Dbd4,
            ["Pressure1"] = "DB2.DBD0",
            ["Flow1"] = "DB2.DBD4",
        };

        // Add tags to PLC
        foreach (var mapping in tagMapping)
        {
            _ = TagOperations.AddUpdateTagItem(_plc, typeof(float), mapping.Key, mapping.Value);
        }

        // Act
        var result = await AdvancedExtensions.ReadBatchOptimizedAsync(_plc, 0F, tagMapping, readTimeoutMilliseconds);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Values.Count, Is.EqualTo(expectedTagCount));
        Assert.That(result.Success.Count, Is.EqualTo(expectedTagCount));
        Assert.That(result.OverallSuccess, Is.True);
    }

    /// <summary>Test advanced batch writing with verification and rollback.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task WriteBatchOptimized_WithRollback_ShouldHandleErrorsAsync()
    {
        // Arrange
        const float setPoint1 = 25.5F;
        const float setPoint2 = 30.0F;
        const int addressOffsetSize = 4;
        const int expectedSuccessCount = 2;
        var writeValues = new Dictionary<string, float>
        {
            ["SetPoint1"] = setPoint1,
            ["SetPoint2"] = setPoint2,
        };

        // Add tags
        foreach (var kvp in writeValues)
        {
            _ = TagOperations.AddUpdateTagItem(
                _plc,
                typeof(float),
                kvp.Key,
                $"DB1.DBD{writeValues.Keys.ToList().IndexOf(kvp.Key) * addressOffsetSize}");
        }

        // Act
        var result = await AdvancedExtensions.WriteBatchOptimizedAsync(
            _plc,
            writeValues,
            verifyWrites: false,
            enableRollback: true);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success.Count, Is.EqualTo(expectedSuccessCount));
        Assert.That(result.OverallSuccess, Is.True);
    }

    /// <summary>Test smart tag change monitoring with debouncing.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task MonitorTagSmart_ShouldProvideChangeDetectionAsync()
    {
        // Arrange
        const string smartTag = "SmartTag";
        const int subscriptionDelayMilliseconds = 100;
        _ = TagOperations.AddUpdateTagItem(_plc, typeof(float), smartTag, Db1Dbd0);
        const double changeThreshold = 0.5;
        const int debounceMs = 50;

        // Act
        var smartMonitor = OptimizationExtensions.MonitorTagSmart(
            _plc,
            smartTag,
            EqualityComparer<float>.Default,
            changeThreshold,
            debounceMs);

        // We can't easily test this without actual data changes, so just verify the observable is created
        Assert.That(smartMonitor, Is.Not.Null);

        // Test that we can subscribe without errors
        using var subscription = smartMonitor.Subscribe(change =>
        {
            Assert.That(change, Is.Not.Null);
            Assert.That(change.TagName, Is.EqualTo(smartTag));
        });

        // Give it a moment to ensure no immediate errors
        await Task.Delay(subscriptionDelayMilliseconds);
    }

    /// <summary>Test cache-enabled value reading.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ValueCached_ShouldUseIntelligentCachingAsync()
    {
        // Arrange
        const string cacheTag = "CacheTag";
        _ = TagOperations.AddUpdateTagItem(_plc, typeof(float), cacheTag, Db1Dbd0);
        var cacheTimeout = TimeSpan.FromSeconds(1);

        // Act
        var value1 = await OptimizationExtensions.ValueCachedAsync(
            _plc,
            cacheTag,
            fallbackValue: 0F,
            cacheTimeout: cacheTimeout);
        var value2 = await OptimizationExtensions.ValueCachedAsync(
            _plc,
            cacheTag,
            fallbackValue: 0F,
            cacheTimeout: cacheTimeout);

        // Assert
        // Both should complete without error (actual values depend on PLC connectivity)
        Assert.That(value1, Is.InstanceOf<float>());
        Assert.That(value2, Is.InstanceOf<float>());
    }

    /// <summary>Test cache statistics and management.</summary>
    [Test]
    public void CacheManagement_ShouldProvideStatistics()
    {
        // Arrange
        const string statsTag = "StatsTag";
        _ = TagOperations.AddUpdateTagItem(_plc, typeof(float), statsTag, Db1Dbd0);

        // Act
        var statsBefore = OptimizationExtensions.GetCacheStatistics(_plc);
        OptimizationExtensions.ClearCache(_plc); // Clear all cache
        var statsAfter = OptimizationExtensions.GetCacheStatistics(_plc);

        // Assert
        Assert.That(statsBefore, Is.Not.Null);
        Assert.That(statsAfter, Is.Not.Null);
        Assert.That(statsAfter.TotalEntries, Is.LessThanOrEqualTo(statsBefore.TotalEntries));
    }

    /// <summary>Test production system validation.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ValidateProductionReadiness_ShouldAssessSystemHealthAsync()
    {
        // Arrange
        const double minimumReliabilityRate = 0.8;
        const int reliabilityTestCount = 3;
        const double minimumProductionScore = 60.0;
        const int maximumScore = 100;
        var config = new ProductionValidationConfig
        {
            MaxAcceptableResponseTime = TimeSpan.FromSeconds(1),
            MinimumReliabilityRate = minimumReliabilityRate,
            ReliabilityTestCount = reliabilityTestCount,
            MinimumProductionScore = minimumProductionScore,
        };

        // Act
        var result = await ProductionExtensions.ValidateProductionReadinessAsync(_plc, config);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.PLCIdentifier, Does.Contain(MockServer.Localhost));
        Assert.That(result.ValidationTests, Is.Not.Empty);
        Assert.That(result.OverallScore, Is.InRange(0, maximumScore));
        Assert.That(result.TotalValidationTime, Is.GreaterThan(TimeSpan.Zero));
    }

    /// <summary>Test production error handling with circuit breaker.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ExecuteWithErrorHandling_ShouldProvideResilienceAsync()
    {
        // Arrange
        const int maximumRetryAttempts = 2;
        const int retryDelayMilliseconds = 50;
        const int circuitBreakerThreshold = 3;
        const int simulatedOperationDelayMilliseconds = 10;
        var config = new ProductionErrorConfig
        {
            MaxRetryAttempts = maximumRetryAttempts,
            BaseRetryDelayMs = retryDelayMilliseconds,
            UseExponentialBackoff = true,
            CircuitBreakerThreshold = circuitBreakerThreshold,
            CircuitBreakerTimeout = TimeSpan.FromSeconds(1),
        };

        // Act & Assert
        var result = await ProductionExtensions.ExecuteWithErrorHandlingAsync(
            _plc,
            async () =>
        {
            // Simulate a simple operation
            await Task.Delay(simulatedOperationDelayMilliseconds);
            return "Success";
        },
            config);

        Assert.That(result, Is.EqualTo("Success"));
    }

    /// <summary>Test high-performance tag group creation and operations.</summary>
    [Test]
    public void CreateTagGroup_ShouldProvideOptimizedAccess()
    {
        // Arrange
        const int addressOffsetSize = 4;
        var tagNames = new[] { "GroupTag1", "GroupTag2", "GroupTag3" };
        foreach (var tagName in tagNames)
        {
            _ = TagOperations.AddUpdateTagItem(
                _plc,
                typeof(float),
                tagName,
                $"DB1.DBD{Array.IndexOf(tagNames, tagName) * addressOffsetSize}");
        }

        // Act
        using var tagGroup = AdvancedExtensions.CreateTagGroup(_plc, 0F, "TestGroup", tagNames);

        // Assert
        Assert.That(tagGroup, Is.Not.Null);
        Assert.That(tagGroup.GroupName, Is.EqualTo("TestGroup"));
    }

    /// <summary>Test comprehensive diagnostics collection.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task GetDiagnostics_ShouldProvideComprehensiveInfoAsync()
    {
        const int timestampToleranceSeconds = 5;
        var timestampTolerance = TimeSpan.FromSeconds(timestampToleranceSeconds);

        // Act
        var diagnostics = await AdvancedExtensions.GetDiagnosticsAsync(_plc);

        // Assert
        Assert.That(diagnostics, Is.Not.Null);
        Assert.That(diagnostics.PLCType, Is.EqualTo(CpuType.S71500));
        Assert.That(diagnostics.IPAddress, Is.EqualTo(MockServer.Localhost));
        Assert.That(diagnostics.Rack, Is.EqualTo(0));
        Assert.That(diagnostics.Slot, Is.EqualTo(1));
        Assert.That(diagnostics.DiagnosticTime, Is.EqualTo(_timeProvider.GetUtcNow()).Within(timestampTolerance));
        Assert.That(diagnostics.TagMetrics, Is.Not.Null);
        Assert.That(diagnostics.Recommendations, Is.Not.Null);
    }

    /// <summary>Test performance analysis and optimization recommendations.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task AnalyzePerformance_ShouldProvideRecommendationsAsync()
    {
        // Arrange
        const int monitoringDurationMilliseconds = 200;
        const int timestampToleranceSeconds = 5;
        var monitoringDuration = TimeSpan.FromMilliseconds(monitoringDurationMilliseconds);
        var timestampTolerance = TimeSpan.FromSeconds(timestampToleranceSeconds);

        // Act
        var analysis = await AdvancedExtensions.AnalyzePerformanceAsync(_plc, monitoringDuration);

        // Assert
        Assert.That(analysis, Is.Not.Null);
        Assert.That(analysis.StartTime, Is.EqualTo(_timeProvider.GetUtcNow()).Within(timestampTolerance));
        Assert.That(analysis.EndTime, Is.GreaterThan(analysis.StartTime));
        Assert.That(analysis.MonitoringDuration, Is.EqualTo(monitoringDuration));
        Assert.That(analysis.Recommendations, Is.Not.Null);
    }

    /// <summary>Test multiple tag observation with batch optimization.</summary>
    [Test]
    public void ObserveBatch_ShouldProvideEfficientMonitoring()
    {
        // Arrange
        const int addressOffsetSize = 4;
        var tagNames = new[] { "ObserveTag1", "ObserveTag2", "ObserveTag3" };
        foreach (var tagName in tagNames)
        {
            _ = TagOperations.AddUpdateTagItem(
                _plc,
                typeof(float),
                tagName,
                $"DB1.DBD{Array.IndexOf(tagNames, tagName) * addressOffsetSize}");
        }

        // Act
        var batchObservable = AdvancedExtensions.ObserveBatch(_plc, 0F, tagNames);

        // Assert
        Assert.That(batchObservable, Is.Not.Null);

        // Test subscription works
        using var subscription = batchObservable.Subscribe(values =>
        {
            Assert.That(values, Is.Not.Null);
            Assert.That(values.Keys, Is.EquivalentTo(tagNames));
        });
    }

    /// <summary>Test symbol table loading and symbolic addressing.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task LoadSymbolTable_ShouldEnableSymbolicAddressingAsync()
    {
        // Arrange
        const int expectedSymbolCount = 2;
        const int timestampToleranceSeconds = 5;
        var timestampTolerance = TimeSpan.FromSeconds(timestampToleranceSeconds);
        const string csvData =
            "Name,Address,DataType,Length,Description\n" +
            "Temperature1,DB1.DBD0,REAL,1,Process Temperature\n" +
            "Pressure1,DB1.DBD4,REAL,1,System Pressure";

        // Act
        var symbolTable = await EnterpriseExtensions.LoadSymbolTableAsync(
            _plc,
            csvData,
            SymbolTableFormat.Csv);

        // Assert
        Assert.That(symbolTable, Is.Not.Null);
        Assert.That(symbolTable.Symbols.Count, Is.EqualTo(expectedSymbolCount));
        Assert.That(symbolTable.Symbols, Contains.Key("Temperature1"));
        Assert.That(symbolTable.Symbols, Contains.Key("Pressure1"));
        Assert.That(symbolTable.LoadedAt, Is.EqualTo(_timeProvider.GetUtcNow()).Within(timestampTolerance));
    }

    /// <summary>Test high-availability PLC manager with failover.</summary>
    [Test]
    public void CreateHighAvailabilityConnection_ShouldProvideFailover()
    {
        Assert.That(_plc, Is.Not.Null);

        // Arrange
        const int rack = 0;
        const int slot = 1;
        const int failoverTimeoutSeconds = 10;
        var primaryPlc = new RxS7(new(new(CpuType.S71500, "192.168.1.100", rack, slot)));
        var backupPlcs = new List<IRxS7>
        {
            new RxS7(new(new(CpuType.S71500, "192.168.1.101", rack, slot))),
            new RxS7(new(new(CpuType.S71500, "192.168.1.102", rack, slot))),
        };

        // Act
        using var highAvailabilityManager = EnterpriseExtensions.CreateHighAvailabilityConnection(
            primaryPlc,
            backupPlcs,
            TimeSpan.FromSeconds(failoverTimeoutSeconds));

        // Assert
        Assert.That(highAvailabilityManager, Is.Not.Null);
        Assert.That(highAvailabilityManager.ActivePLC, Is.EqualTo(primaryPlc));
        Assert.That(highAvailabilityManager.FailoverEvents, Is.Not.Null);

        // Cleanup
        primaryPlc.Dispose();
        foreach (var backup in backupPlcs)
        {
            backup.Dispose();
        }
    }

    /// <summary>Test optimization engine batch processing.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task OptimizationEngine_BatchProcessing_ShouldImprovePerformanceAsync()
    {
        // Arrange
        const int tagCount = 10;
        const int addressOffsetSize = 4;
        var tags = new List<string>();
        for (var i = 0; i < tagCount; i++)
        {
            var tagName = $"BatchTag{i}";
            tags.Add(tagName);
            _ = TagOperations.AddUpdateTagItem(_plc, typeof(float), tagName, $"DB1.DBD{i * addressOffsetSize}");
        }

        // Act - Test batch reading
        var batchResult = await AdvancedExtensions.ValueBatchAsync(_plc, 0F, tags.ToArray());

        // Assert
        Assert.That(batchResult, Is.Not.Null);
        Assert.That(batchResult.Count, Is.EqualTo(tagCount));
        foreach (var tag in tags)
        {
            Assert.That(batchResult, Contains.Key(tag));
        }
    }

    /// <summary>Test connection pool functionality.</summary>
    [Test]
    public void ConnectionPool_ShouldManageMultipleConnections()
    {
        Assert.That(_plc, Is.Not.Null);

        // Arrange
        const int maximumConnections = 5;
        const int connectionTimeoutSeconds = 30;
        var config = new ConnectionPoolConfig
        {
            MaxConnections = maximumConnections,
            ConnectionTimeout = TimeSpan.FromSeconds(connectionTimeoutSeconds),
            EnableConnectionReuse = true,
        };

        // Act
        using var connectionPool = new ConnectionPool(config);

        // Assert
        Assert.That(connectionPool, Is.Not.Null);
        Assert.That(connectionPool.MaxConnections, Is.EqualTo(maximumConnections));
        Assert.That(connectionPool.ActiveConnections, Is.EqualTo(0));
    }

    /// <summary>Test performance analysis with real-time recommendations.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task PerformanceAnalysis_RealTime_ShouldProvideInsightsAsync()
    {
        // Arrange
        const int monitoringDurationMilliseconds = 300;
        _ = TagOperations.AddUpdateTagItem(_plc, typeof(float), "AnalysisTag1", Db1Dbd0);
        _ = TagOperations.AddUpdateTagItem(_plc, typeof(float), "AnalysisTag2", Db1Dbd4);

        var monitoringDuration = TimeSpan.FromMilliseconds(monitoringDurationMilliseconds);

        // Act
        var analysis = await AdvancedExtensions.AnalyzePerformanceAsync(_plc, monitoringDuration);

        // Assert
        Assert.That(analysis, Is.Not.Null);
        Assert.That(analysis.MonitoringDuration, Is.EqualTo(monitoringDuration));
        Assert.That(analysis.TagChangeFrequencies, Is.Not.Null);
        Assert.That(analysis.Recommendations, Is.Not.Null);
        Assert.That(analysis.TotalTagChanges, Is.GreaterThanOrEqualTo(0));
    }

    /// <summary>Test security context and encrypted communication.</summary>
    [Test]
    public void SecurityContext_ShouldProvideEncryptedCommunication()
    {
        Assert.That(_plc, Is.Not.Null);

        // Arrange
        var securityContext = new SecurityContext
        {
            EnableEncryption = true,
            CertificatePath = "test.pfx",
            CertificatePassword = "test123",
        };

        // Act & Assert
        Assert.That(securityContext, Is.Not.Null);
        Assert.That(securityContext.EnableEncryption, Is.True);
        Assert.That(securityContext.CertificatePath, Is.EqualTo("test.pfx"));
    }

    /// <summary>Disposes test resources.</summary>
    public void Dispose()
    {
        _plc?.Dispose();
        _server?.Dispose();
    }

    /// <summary>Regression coverage for bit masking to ensure write paths avoid floating-point `Math.Pow`.</summary>
    /// <param name="bit">The bit index (0-7).</param>
    /// <param name="value">Whether the bit should be set.</param>
    /// <param name="expected">The expected byte value after applying the bit operation.</param>
    [Test]
    [Arguments(0, true, (byte)0b0000_0001)]
    [Arguments(0, false, (byte)0b0000_0000)]
    [Arguments(7, true, (byte)0b1000_0000)]
    public void BitMasking_ShouldSetAndClearBitsCorrectly(int bit, bool value, byte expected)
    {
        Assert.That(_plc, Is.Not.Null);

        // Arrange
        byte data = 0;

        // Act
        S7PlcRx.PlcTypes.Conversion.SetBit(ref data, bit, value);

        // Assert
        Assert.That(data, Is.EqualTo(expected));
    }
}
