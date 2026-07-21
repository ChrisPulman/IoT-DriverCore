// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using S7PlcRx.Advanced;
using S7PlcRx.Enums;
using S7PlcRx.Optimization;
using S7PlcRx.Performance;
using S7PlcRx.Production;

namespace S7PlcRx.Examples;

/// <summary>
/// Comprehensive examples demonstrating S7PlcRx optimizations for industrial automation.
/// Shows batch operations, performance monitoring, and advanced PLC communication patterns.
/// </summary>
public static class AdvancedExamples
{
    /// <summary>The PLC IP address used by the examples.</summary>
    internal const string IpAddress = "172.16.13.1";

    /// <summary>The PLC CPU type used by the examples.</summary>
    internal const CpuType PlcType = CpuType.S71500;

    /// <summary>The timeout used by optimized batch-read examples.</summary>
    private const int DefaultBatchTimeoutMilliseconds = 5_000;

    /// <summary>
    /// Demonstrates basic batch reading optimization for multiple tags.
    /// Reduces network overhead by grouping operations by data block.
    /// </summary>
    /// <param name="plc">The PLC.</param>
    /// <returns>
    /// A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    public static async Task BasicBatchReadExampleAsync(IRxS7 plc)
    {
        const int BatchArrayLength = 20;

        if (plc is null)
        {
            throw new ArgumentNullException(nameof(plc), "PLC connection is not initialized");
        }

        Trace.WriteLine("=== BASIC BATCH READ 20 VALUES EXAMPLE ===");
        Stopwatch stopwatch = new();
        _ = TagOperations.AddUpdateTagItem(
                plc,
                typeof(float[]),
                "R_Values",
                "DB1.DBD20",
                BatchArrayLength)
            .SetPolling(false); // Disable polling for this tag
        stopwatch.Restart();
        var readValues = await plc.ReadAsync(new LogicalTagKey<float[]>("R_Values"));
        stopwatch.Stop();
        TraceInitialReadValues(readValues, stopwatch.ElapsedMilliseconds);

        // Define tag mapping for batch operations
        var tagMapping = new Dictionary<string, string>
        {
            ["Temperature1"] = "DB1.DBD0", // Process temperature 1
            ["Temperature2"] = "DB1.DBD4", // Process temperature 2
            ["Pressure1"] = "DB1.DBD8", // System pressure
            ["FlowRate"] = "DB1.DBD12", // Flow rate sensor
            ["Level"] = "DB1.DBD16", // Tank level
        };

        // Perform optimized batch read (80% faster than individual reads)
        var results = await AdvancedExtensions.ReadBatchOptimizedAsync(
            plc,
            default(float),
            tagMapping,
            DefaultBatchTimeoutMilliseconds);

        if (results.OverallSuccess)
        {
            Trace.WriteLine("=== BATCH READ RESULTS ===");
            Trace.WriteLine($"Successfully read {results.SuccessCount} out of {tagMapping.Count} tags");

            foreach (var kvp in results.Values)
            {
                Trace.WriteLine($"{kvp.Key}: {kvp.Value:F2}");
            }
        }
        else
        {
            Trace.WriteLine($"Batch read had {results.ErrorCount} errors:");
            foreach (var error in results.Errors)
            {
                Trace.WriteLine($"  {error.Key}: {error.Value}");
            }
        }
    }

    /// <summary>
    /// Demonstrates advanced batch writing with verification and rollback.
    /// Ensures data integrity in critical industrial operations.
    /// </summary>
    /// <param name="plc">The PLC.</param>
    /// <returns>
    /// A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    public static async Task AdvancedBatchWriteExampleAsync(IRxS7 plc)
    {
        const float PressureSetPoint = 1.8F;

        const float TemperatureSetPoint = 25.5F;

        const int ActiveRecipeNumber = 42;

        // Add tags for writing
        _ = TagOperations.AddUpdateTagItem(plc, typeof(float), "SetPoint1", "DB3.DBD0").SetPolling(false);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(float), "SetPoint2", "DB3.DBD4").SetPolling(false);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(bool), "EnableProcess", "DB3.DBX8.0").SetPolling(false);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(int), "RecipeNumber", "DB3.DBD10").SetPolling(false);

        // Define values to write
        var writeValues = new Dictionary<string, object>
        {
            ["SetPoint1"] = TemperatureSetPoint,
            ["SetPoint2"] = PressureSetPoint,
            ["EnableProcess"] = true, // Enable flag
            ["RecipeNumber"] = ActiveRecipeNumber,
        };

        Trace.WriteLine("=== ADVANCED BATCH WRITE ===");
        Trace.WriteLine("Writing values with verification and rollback enabled...");

        // Perform batch write with verification and rollback protection
        var writeResult = await AdvancedExtensions.WriteBatchOptimizedAsync(
            plc,
            writeValues,
            verifyWrites: true, // Read back to verify writes
            enableRollback: true); // Rollback on any failure

        if (writeResult.OverallSuccess)
        {
            Trace.WriteLine($"✅ All {writeResult.SuccessCount} writes completed successfully");
        }
        else
        {
            Trace.WriteLine($"❌ {writeResult.ErrorCount} writes failed");
            if (writeResult.RollbackPerformed)
            {
                Trace.WriteLine("🔄 Rollback performed - system restored to previous state");
            }

            foreach (var error in writeResult.Errors)
            {
                Trace.WriteLine($"  {error.Key}: {error.Value}");
            }
        }
    }

    /// <summary>Demonstrates high-performance tag groups for related operations.</summary>
    /// <param name="plc">The PLC.</param>
    /// <returns>
    /// A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    public static async Task HighPerformanceTagGroupExampleAsync(IRxS7 plc)
    {
        const int MonitoringDurationMilliseconds = 30_000;

        // Create specialized tag groups for different process areas
        var temperatureGroup = AdvancedExtensions.CreateTagGroup(
            plc,
            default(float),
            "Temperatures",
            "DB4.DBD0", // Reactor temperature
            "DB4.DBD4", // Cooling temperature
            "DB4.DBD8", // Ambient temperature
            "DB4.DBD12"); // Exhaust temperature

        var pressureGroup = AdvancedExtensions.CreateTagGroup(
            plc,
            default(float),
            "Pressures",
            "DB5.DBD0", // System pressure
            "DB5.DBD4", // Line pressure
            "DB5.DBD8"); // Vacuum pressure

        Trace.WriteLine("=== HIGH-PERFORMANCE TAG GROUPS ===");

        // Read all temperatures efficiently
        var temperatures = await temperatureGroup.ReadAllAsync();
        Trace.WriteLine("Temperature Readings:");
        foreach (var temp in temperatures)
        {
            Trace.WriteLine($"  {temp.Key}: {temp.Value:F1}°C");
        }

        // Read all pressures efficiently
        var pressures = await pressureGroup.ReadAllAsync();
        Trace.WriteLine("Pressure Readings:");
        foreach (var pressure in pressures)
        {
            Trace.WriteLine($"  {pressure.Key}: {pressure.Value:F6} bar");
        }

        // Monitor group changes in real-time
        var subscription = temperatureGroup.ObserveGroup().Subscribe(groupData =>
        {
            var avgTemp = groupData.Values.Average();
            Trace.WriteLine($"Average Temperature: {avgTemp:F1}°C");
        });

        // Keep monitoring for 30 seconds
        await Task.Delay(MonitoringDurationMilliseconds);
        subscription.Dispose();

        // Clean up
        temperatureGroup.Dispose();
        pressureGroup.Dispose();
    }

    /// <summary>
    /// Demonstrates intelligent monitoring with change detection and filtering.
    /// Reduces noise and focuses on significant changes only.
    /// </summary>
    /// <param name="plc">The PLC.</param>
    /// <returns>
    /// A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    public static async Task IntelligentMonitoringExampleAsync(IRxS7 plc)
    {
        const string ProcessValueTagName = "ProcessValue";

        const int MonitoringDurationMilliseconds = 60_000;

        // Add monitoring tags
        _ = TagOperations.AddUpdateTagItem(plc, typeof(float), ProcessValueTagName, "DB6.DBD0");
        _ = TagOperations.AddUpdateTagItem(plc, typeof(float), "AnalogInput1", "DB6.DBD4");
        _ = TagOperations.AddUpdateTagItem(plc, typeof(bool), "AlarmStatus", "DB6.DBX8.0");

        Trace.WriteLine("=== INTELLIGENT MONITORING ===");

        // Monitor multiple values with batch optimization
        var batchObserver = AdvancedExtensions.ObserveBatch(
            plc,
            default(object),
            ProcessValueTagName,
            "AnalogInput1",
            "AlarmStatus");

        var monitoringSubscription = batchObserver.Subscribe(values =>
        {
            Trace.WriteLine($"[{DateTime.Now:HH':'mm':'ss}] Batch Update:");
            foreach (var kvp in values)
            {
                var valueStr = kvp.Value switch
                {
                    float f => $"{f:F2}",
                    bool b => b ? "ACTIVE" : "INACTIVE",
                    _ => kvp.Value?.ToString() ?? "NULL"
                };
                Trace.WriteLine($"  {kvp.Key}: {valueStr}");
            }

            Trace.WriteLine(string.Empty);
        });

        // Monitor with intelligent change detection (only significant changes)
        // Uses 0.5 threshold for analog values and 100ms debounce
        var smartMonitor = OptimizationExtensions.MonitorTagSmart(
            plc,
            ProcessValueTagName,
            EqualityComparer<float>.Default,
            changeThreshold: 0.5,
            debounceMs: 100);

        var smartSubscription = smartMonitor.Subscribe(change =>
        {
            Trace.WriteLine("🔔 Significant Change Detected:");
            Trace.WriteLine($"   Tag: {change.TagName}");
            Trace.WriteLine($"   Previous: {change.PreviousValue:F2}");
            Trace.WriteLine($"   Current: {change.CurrentValue:F2}");
            Trace.WriteLine($"   Change: {change.ChangeAmount:F2}");
            Trace.WriteLine($"   Time: {change.ChangeTime:HH':'mm':'ss'.'fff}");
            Trace.WriteLine(string.Empty);
        });

        // Run monitoring for 60 seconds
        Trace.WriteLine("Monitoring for 60 seconds... (only significant changes will be shown)");
        await Task.Delay(MonitoringDurationMilliseconds);

        // Clean up
        monitoringSubscription.Dispose();
        smartSubscription.Dispose();
    }

    /// <summary>
    /// Demonstrates comprehensive performance analysis and optimization recommendations.
    /// Provides actionable insights for system optimization.
    /// </summary>
    /// <param name="plc">The PLC.</param>
    /// <returns>
    /// A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    public static async Task PerformanceAnalysisExampleAsync(IRxS7 plc)
    {
        const int AnalysisDurationMinutes = 2;

        Trace.WriteLine("=== PERFORMANCE ANALYSIS ===");
        var diagnostics = await AdvancedExtensions.GetDiagnosticsAsync(plc);
        TraceDiagnostics(diagnostics);
        Trace.WriteLine("Performing detailed performance analysis (2 minutes)...");
        var analysis = await AdvancedExtensions.AnalyzePerformanceAsync(
            plc,
            TimeSpan.FromMinutes(AnalysisDurationMinutes));
        TracePerformanceAnalysis(analysis);
    }

    /// <summary>Gets the most frequently changing tags.</summary>
    /// <param name="frequencies">The tag change frequencies.</param>
    /// <param name="maxCount">The maximum result count.</param>
    /// <returns>The tags ordered by descending change frequency.</returns>
    public static List<KeyValuePair<string, int>> GetTopChangingTags(
        IReadOnlyDictionary<string, int> frequencies,
        int maxCount)
    {
        var topChangingTags = new List<KeyValuePair<string, int>>(maxCount);
        foreach (var frequency in frequencies)
        {
            var insertIndex = topChangingTags.Count;
            for (var i = 0; i < topChangingTags.Count; i++)
            {
                if (frequency.Value > topChangingTags[i].Value)
                {
                    insertIndex = i;
                    break;
                }
            }

            if (insertIndex >= maxCount)
            {
                continue;
            }

            topChangingTags.Insert(insertIndex, frequency);
            if (topChangingTags.Count > maxCount)
            {
                topChangingTags.RemoveAt(maxCount);
            }
        }

        return topChangingTags;
    }

    /// <summary>
    /// Demonstrates complete production workflow with all optimizations.
    /// Shows integration of batch operations, monitoring, and error handling.
    /// </summary>
    /// <param name="plc">The PLC.</param>
    /// <returns>
    /// A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    public static async Task ProductionWorkflowExampleAsync(IRxS7 plc)
    {
        Trace.WriteLine("=== PRODUCTION WORKFLOW EXAMPLE ===");
        Trace.WriteLine("Simulating a complete production cycle with optimizations...");
        Trace.WriteLine(string.Empty);

        try
        {
            await TraceInitialSystemStateAsync(plc);
            if (!await ConfigureRecipeAsync(plc))
            {
                return;
            }

            await MonitorProductionAsync(plc);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"❌ Production workflow error: {ex.Message}");
        }
    }

    /// <summary>Entry point for running all optimization examples.</summary>
    /// <param name="plc">The PLC connection used by the examples.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task RunAllExamplesAsync(IRxS7 plc)
    {
        ArgumentNullException.ThrowIfNull(plc);

        Trace.WriteLine("S7PlcRx optimization examples");
        await BasicBatchReadExampleAsync(plc);
        await AdvancedBatchWriteExampleAsync(plc);
        await HighPerformanceTagGroupExampleAsync(plc);
        await IntelligentMonitoringExampleAsync(plc);
        await PerformanceAnalysisExampleAsync(plc);
        await ProductionWorkflowExampleAsync(plc);
        Trace.WriteLine("All optimization examples completed.");
    }

    /// <summary>Registers and writes the production recipe.</summary>
    /// <param name="plc">The PLC connection.</param>
    /// <returns><see langword="true"/> when all recipe values were written.</returns>
    private static async Task<bool> ConfigureRecipeAsync(IRxS7 plc)
    {
        const float RecipePressureSetPoint = 2.1F;
        const float RecipeTemperatureSetPoint = 85.5F;
        const int MixSpeedSetPoint = 150;
        const int ProcessDurationSeconds = 3_600;
        const int RecipeAddressStride = 4;
        const int RecipeIdentifier = 12_345;
        Trace.WriteLine("2️⃣ Recipe Setup");
        var recipeParameters = new Dictionary<string, object>
        {
            ["Temperature_SP"] = RecipeTemperatureSetPoint,
            ["Pressure_SP"] = RecipePressureSetPoint,
            ["MixSpeed_SP"] = MixSpeedSetPoint,
            ["ProcessTime"] = ProcessDurationSeconds,
            ["RecipeID"] = RecipeIdentifier,
        };
        var databaseIndex = 0;
        foreach (var parameter in recipeParameters)
        {
            var address = $"DB9.DBD{databaseIndex}";
            databaseIndex += RecipeAddressStride;
            _ = TagOperations.AddUpdateTagItem(plc, parameter.Value.GetType(), parameter.Key, address)
                .SetPolling(false);
        }

        var result = await AdvancedExtensions.WriteBatchOptimizedAsync(
            plc,
            recipeParameters,
            verifyWrites: true,
            enableRollback: true);
        Trace.WriteLine($"   Recipe parameters written: {result.SuccessCount}/{recipeParameters.Count}");
        if (result.OverallSuccess)
        {
            Trace.WriteLine(string.Empty);
            return true;
        }

        Trace.WriteLine("   ⚠️  Recipe setup had errors - aborting");
        return false;
    }

    /// <summary>Monitors the simulated production process.</summary>
    /// <param name="plc">The PLC connection.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task MonitorProductionAsync(IRxS7 plc)
    {
        const int MonitoringDurationMilliseconds = 30_000;
        Trace.WriteLine("3️⃣ Process Monitoring Setup");
        using var processGroup = AdvancedExtensions.CreateTagGroup(
            plc,
            default(float),
            "ProcessMonitoring",
            "DB7.DBD0",
            "DB7.DBD4",
            "DB7.DBD8");
        using var alarmGroup = AdvancedExtensions.CreateTagGroup(
            plc,
            default(bool),
            "AlarmMonitoring",
            "DB8.DBX0.0",
            "DB8.DBX0.1",
            "DB8.DBX0.2",
            "DB8.DBX0.3",
            "DB7.DBX10.0");
        Trace.WriteLine("   ✅ Monitoring groups created");
        Trace.WriteLine("4️⃣ Real-time Process Monitoring (30 seconds)");
        using var processSubscription = SubscribeToProcessGroup(processGroup);
        using var alarmSubscription = SubscribeToAlarmGroup(alarmGroup);
        await Task.Delay(MonitoringDurationMilliseconds);
        await TraceFinalDiagnosticsAsync(plc);
        Trace.WriteLine("✅ Production workflow completed successfully!");
    }

    /// <summary>Subscribes to active production alarms.</summary>
    /// <param name="alarmGroup">The alarm tag group.</param>
    /// <returns>The alarm subscription.</returns>
    private static IDisposable SubscribeToAlarmGroup(HighPerformanceTagGroup<bool> alarmGroup)
        => alarmGroup.ObserveGroup().Subscribe(alarmData =>
        {
            var activeAlarms = new List<string>();
            foreach (var alarm in alarmData)
            {
                if (!alarm.Value)
                {
                    continue;
                }

                activeAlarms.Add(alarm.Key);
            }

            if (activeAlarms.Count == 0)
            {
                return;
            }

            Trace.WriteLine($"   🚨 ALARMS ACTIVE: {string.Join(", ", activeAlarms)}");
        });

    /// <summary>Subscribes to production process values.</summary>
    /// <param name="processGroup">The process tag group.</param>
    /// <returns>The process subscription.</returns>
    private static IDisposable SubscribeToProcessGroup(HighPerformanceTagGroup<float> processGroup)
        => processGroup.ObserveGroup().Subscribe(processData =>
        {
            Trace.WriteLine($"   [{DateTime.Now:HH':'mm':'ss}] Process Values:");
            foreach (var value in processData)
            {
                Trace.WriteLine($"     {value.Key}: {value.Value}");
            }
        });

    /// <summary>Writes final production diagnostics.</summary>
    /// <param name="plc">The PLC connection.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task TraceFinalDiagnosticsAsync(IRxS7 plc)
    {
        Trace.WriteLine(string.Empty);
        Trace.WriteLine("5️⃣ Performance Analysis");
        var diagnostics = await AdvancedExtensions.GetDiagnosticsAsync(plc);
        Trace.WriteLine($"   Connection Latency: {diagnostics.ConnectionLatencyMs:F0}ms");
        Trace.WriteLine($"   Active Tags: {diagnostics.TagMetrics.ActiveTags}");
        Trace.WriteLine($"   Recommendations: {diagnostics.Recommendations.Count}");
        Trace.WriteLine(string.Empty);
    }

    /// <summary>Writes production diagnostics to the trace output.</summary>
    /// <param name="diagnostics">The diagnostics to write.</param>
    private static void TraceDiagnostics(ProductionDiagnostics diagnostics)
    {
        Trace.WriteLine("System Overview:");
        Trace.WriteLine($"  PLC Type: {diagnostics.PLCType}");
        Trace.WriteLine($"  IP Address: {diagnostics.IPAddress}");
        Trace.WriteLine($"  Connection Status: {(diagnostics.IsConnected ? "✅ Connected" : "❌ Disconnected")}");
        Trace.WriteLine($"  Connection Latency: {diagnostics.ConnectionLatencyMs:F0}ms");
        Trace.WriteLine("Tag Statistics:");
        Trace.WriteLine($"  Total Tags: {diagnostics.TagMetrics.TotalTags}");
        Trace.WriteLine($"  Active Tags: {diagnostics.TagMetrics.ActiveTags}");
        Trace.WriteLine($"  Inactive Tags: {diagnostics.TagMetrics.InactiveTags}");
        Trace.WriteLine("Data Block Distribution:");
        foreach (var dataBlock in diagnostics.TagMetrics.DataBlockDistribution)
        {
            Trace.WriteLine($"  {dataBlock.Key}: {dataBlock.Value} tags");
        }

        if (diagnostics.CPUInformation.Count > 0)
        {
            Trace.WriteLine("CPU Information:");
            foreach (var information in diagnostics.CPUInformation)
            {
                Trace.WriteLine($"  {information}");
            }
        }

        Trace.WriteLine("Optimization Recommendations:");
        if (diagnostics.Recommendations.Count == 0)
        {
            Trace.WriteLine("  ✅ System is well optimized!");
            return;
        }

        foreach (var recommendation in diagnostics.Recommendations)
        {
            Trace.WriteLine($"  💡 {recommendation}");
        }
    }

    /// <summary>Reads and writes the initial production state.</summary>
    /// <param name="plc">The PLC connection.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task TraceInitialSystemStateAsync(IRxS7 plc)
    {
        Trace.WriteLine("1️⃣ System Initialization");
        var tags = new Dictionary<string, string>
        {
            ["SystemReady"] = "DB7.DBX0.0",
            ["RecipeLoaded"] = "DB7.DBX0.1",
            ["ProcessStep"] = "DB7.DBW2",
            ["BatchNumber"] = "DB7.DBD4",
        };
        var state = await AdvancedExtensions.ReadBatchOptimizedAsync(
            plc,
            default(object),
            tags,
            DefaultBatchTimeoutMilliseconds);
        Trace.WriteLine($"   System Ready: {state.Values["SystemReady"]}");
        Trace.WriteLine($"   Recipe Loaded: {state.Values["RecipeLoaded"]}");
        Trace.WriteLine($"   Current Step: {state.Values["ProcessStep"]}");
        Trace.WriteLine($"   Batch Number: {state.Values["BatchNumber"]}");
        Trace.WriteLine(string.Empty);
    }

    /// <summary>Writes the initial batch-read values.</summary>
    /// <param name="values">The values that were read.</param>
    /// <param name="elapsedMilliseconds">The read duration in milliseconds.</param>
    private static void TraceInitialReadValues(float[]? values, long elapsedMilliseconds)
    {
        Trace.WriteLine($"Read {values?.Length ?? 0} values in {elapsedMilliseconds} ms");
        if (values?.Length > 0)
        {
            for (var i = 0; i < values.Length; i++)
            {
                Trace.WriteLine($"R_Values[{i}]: {values[i]:F2}");
            }
        }
        else
        {
            Trace.WriteLine("No values read from R_Values tag.");
        }
    }

    /// <summary>Writes a performance analysis to the trace output.</summary>
    /// <param name="analysis">The performance analysis.</param>
    private static void TracePerformanceAnalysis(PerformanceAnalysis analysis)
    {
        const int TopChangingTagCount = 10;
        Trace.WriteLine("Performance Analysis Results:");
        Trace.WriteLine($"  Analysis Duration: {analysis.MonitoringDuration.TotalMinutes:F1} minutes");
        Trace.WriteLine($"  Total Tag Changes: {analysis.TotalTagChanges}");
        Trace.WriteLine($"  Average Changes per Tag: {analysis.AverageChangesPerTag:F1}");
        Trace.WriteLine("Tag Change Frequencies:");
        foreach (var tag in GetTopChangingTags(analysis.TagChangeFrequencies, TopChangingTagCount))
        {
            var changesPerMinute = tag.Value / analysis.MonitoringDuration.TotalMinutes;
            Trace.WriteLine($"  {tag.Key}: {tag.Value} changes ({changesPerMinute:F1}/min)");
        }

        Trace.WriteLine("Performance Recommendations:");
        foreach (var recommendation in analysis.Recommendations)
        {
            Trace.WriteLine($"  🎯 {recommendation}");
        }
    }
}
