// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using IoT.DriverCore.S7PlcRx.Advanced;
using IoT.DriverCore.S7PlcRx.Enums;
using IoT.DriverCore.S7PlcRx.Mock;

namespace IoT.DriverCore.S7PlcRx.Benchmarks;

/// <summary>Runs repeatable PLC operation benchmarks against the mock server.</summary>
internal static class PerfHarness
{
    /// <summary>Runs all PLC operation benchmarks.</summary>
    /// <returns>The process exit code.</returns>
    internal static async Task<int> RunAsync()
    {
        const string BenchmarkTagName = "BenchWord";
        const int Iterations = 500;
        const int TagCount = 8;
        const int WarmUpIterations = 50;
        using var server = new MockServer();
        var resultCode = server.Start();
        if (resultCode != 0)
        {
            Trace.WriteLine($"MockServer.Start failed: {resultCode}");
            return resultCode;
        }

        using var plc = new RxS7(new(new(CpuType.S71500, MockServer.Localhost, 0, 1), new(1)));
        var benchmarkTag = new LogicalTagKey<ushort>(BenchmarkTagName);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), BenchmarkTagName, "DB1.DBW0")
            .SetPolling(false);
        var connectionStopwatch = Stopwatch.StartNew();
        await plc.IsConnected.Where(static x => x).FirstAsync();
        connectionStopwatch.Stop();
        Trace.WriteLine($"Connect time: {connectionStopwatch.ElapsedMilliseconds} ms");
        var tagNames = RegisterTags(plc, TagCount, BenchmarkTagName);
        await WarmUpAsync(plc, benchmarkTag, WarmUpIterations);
        var readElapsed = await MeasureReadAsync(plc, benchmarkTag, Iterations);
        var writeElapsed = MeasureWrite(plc, BenchmarkTagName, Iterations);
        var readWriteElapsed = await MeasureReadWriteAsync(plc, benchmarkTag, Iterations);
        var singleWriteElapsed = await MeasureMultiTagAsync(plc, tagNames, Iterations);
        var batchWriteElapsed = await MeasureBatchWriteAsync(plc, tagNames, Iterations);
        Trace.WriteLine($"Read avg:  {PerOperationMilliseconds(readElapsed, Iterations):F3} ms/op");
        Trace.WriteLine($"Write avg: {PerOperationMilliseconds(writeElapsed, Iterations):F3} ms/op");
        Trace.WriteLine($"R+W avg:   {PerOperationMilliseconds(readWriteElapsed, Iterations):F3} ms/op");
        TraceCycleResult("Single-tag write", singleWriteElapsed, Iterations, TagCount);
        TraceCycleResult("Batch write", batchWriteElapsed, Iterations, TagCount);
        return 0;
    }

    /// <summary>Measures batch writes to all benchmark tags.</summary>
    /// <param name="plc">The PLC connection.</param>
    /// <param name="tagNames">The benchmark tag names.</param>
    /// <param name="iterations">The number of iterations.</param>
    /// <returns>The elapsed time.</returns>
    private static async Task<TimeSpan> MeasureBatchWriteAsync(RxS7 plc, string[] tagNames, int iterations)
    {
        var stopwatch = Stopwatch.StartNew();
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var values = new Dictionary<string, ushort>(tagNames.Length);
            for (var index = 0; index < tagNames.Length; index++)
            {
                values[tagNames[index]] = (ushort)(iteration + index);
            }

            await AdvancedExtensions.ValueBatchAsync(plc, values);
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    /// <summary>Measures single writes and reads across all benchmark tags.</summary>
    /// <param name="plc">The PLC connection.</param>
    /// <param name="tagNames">The benchmark tag names.</param>
    /// <param name="iterations">The number of iterations.</param>
    /// <returns>The elapsed time.</returns>
    private static async Task<TimeSpan> MeasureMultiTagAsync(RxS7 plc, string[] tagNames, int iterations)
    {
        var stopwatch = Stopwatch.StartNew();
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            for (var index = 0; index < tagNames.Length; index++)
            {
                plc.Value(tagNames[index], (ushort)(iteration + index));
            }

            foreach (var tagName in tagNames)
            {
                _ = await plc.ReadAsync(new LogicalTagKey<ushort>(tagName));
            }
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    /// <summary>Measures repeated reads.</summary>
    /// <param name="plc">The PLC connection.</param>
    /// <param name="tag">The benchmark tag.</param>
    /// <param name="iterations">The number of iterations.</param>
    /// <returns>The elapsed time.</returns>
    private static async Task<TimeSpan> MeasureReadAsync(
        RxS7 plc,
        LogicalTagKey<ushort> tag,
        int iterations)
    {
        var stopwatch = Stopwatch.StartNew();
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            _ = await plc.ReadAsync(tag);
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    /// <summary>Measures repeated write-and-read cycles.</summary>
    /// <param name="plc">The PLC connection.</param>
    /// <param name="tag">The benchmark tag.</param>
    /// <param name="iterations">The number of iterations.</param>
    /// <returns>The elapsed time.</returns>
    private static async Task<TimeSpan> MeasureReadWriteAsync(
        RxS7 plc,
        LogicalTagKey<ushort> tag,
        int iterations)
    {
        var stopwatch = Stopwatch.StartNew();
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            plc.Value(tag.Name, (ushort)iteration);
            _ = await plc.ReadAsync(tag);
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    /// <summary>Measures repeated writes.</summary>
    /// <param name="plc">The PLC connection.</param>
    /// <param name="tagName">The benchmark tag name.</param>
    /// <param name="iterations">The number of iterations.</param>
    /// <returns>The elapsed time.</returns>
    private static TimeSpan MeasureWrite(RxS7 plc, string tagName, int iterations)
    {
        var stopwatch = Stopwatch.StartNew();
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            plc.Value(tagName, (ushort)iteration);
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    /// <summary>Calculates the average operation time.</summary>
    /// <param name="elapsed">The total elapsed time.</param>
    /// <param name="iterations">The number of operations.</param>
    /// <returns>The average milliseconds per operation.</returns>
    private static double PerOperationMilliseconds(TimeSpan elapsed, int iterations)
        => elapsed.TotalMilliseconds / iterations;

    /// <summary>Registers the set of tags used by multi-tag measurements.</summary>
    /// <param name="plc">The PLC connection.</param>
    /// <param name="tagCount">The number of tags.</param>
    /// <param name="tagPrefix">The tag-name prefix.</param>
    /// <returns>The registered tag names.</returns>
    private static string[] RegisterTags(RxS7 plc, int tagCount, string tagPrefix)
    {
        var tagNames = new string[tagCount];
        for (var index = 0; index < tagCount; index++)
        {
            tagNames[index] = $"{tagPrefix}{index}";
            _ = TagOperations.AddUpdateTagItem(
                    plc,
                    typeof(ushort),
                    tagNames[index],
                    $"DB1.DBW{index}")
                .SetPolling(false);
        }

        return tagNames;
    }

    /// <summary>Writes a formatted multi-tag measurement to the trace output.</summary>
    /// <param name="label">The measurement label.</param>
    /// <param name="elapsed">The total elapsed time.</param>
    /// <param name="iterations">The number of cycles.</param>
    /// <param name="tagCount">The number of tags in each cycle.</param>
    private static void TraceCycleResult(string label, TimeSpan elapsed, int iterations, int tagCount)
    {
        const double MicrosecondsPerMillisecond = 1_000.0;
        var millisecondsPerCycle = elapsed.TotalMilliseconds / iterations;
        var microsecondsPerTag = millisecondsPerCycle * MicrosecondsPerMillisecond / tagCount;
        Trace.WriteLine(
            $"{label} ({tagCount} tags per cycle) avg: {millisecondsPerCycle:F3} ms/cycle " +
            $"({microsecondsPerTag:F1} us/tag)");
    }

    /// <summary>Warms the PLC communication path before measurements begin.</summary>
    /// <param name="plc">The PLC connection.</param>
    /// <param name="tag">The benchmark tag.</param>
    /// <param name="iterations">The number of warm-up iterations.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task WarmUpAsync(RxS7 plc, LogicalTagKey<ushort> tag, int iterations)
    {
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            plc.Value(tag.Name, (ushort)iteration);
            _ = await plc.ReadAsync(tag);
        }
    }
}
