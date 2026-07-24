// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if LIVE_S7_TESTS
using System.Diagnostics;
using TUnitAssert = TUnit.Assertions.Assert;

namespace IoT.DriverCore.S7PlcRx.Tests;

/// <summary>Read-only hardware integration coverage for an S7-1500 test endpoint.</summary>
/// <remarks>
/// This source is compiled only when <c>LIVE_S7_TESTS</c> is explicitly defined. It creates no watchdog,
/// registers no tags, and performs only ISO-on-TCP/S7 setup plus CPU diagnostic reads.
/// </remarks>
[NotInParallel]
public sealed class S7LiveHardwareIntegrationTests
{
    /// <summary>Default authorized S7-1500 endpoint.</summary>
    private const string DefaultEndpoint = "172.16.13.1";

    /// <summary>Default number of CPU diagnostic reads in a live endurance run.</summary>
    private const int DefaultReadCount = 120;

    /// <summary>Upper bound for an explicitly configured live endurance run.</summary>
    private const int MaximumReadCount = 10_000;

    /// <summary>Default delay between diagnostic reads so endurance runs do not hammer the CPU.</summary>
    private const int DefaultReadDelayMilliseconds = 250;

    /// <summary>Upper bound for an explicitly configured delay between diagnostic reads.</summary>
    private const int MaximumReadDelayMilliseconds = 10_000;

    /// <summary>Connection and individual CPU-read timeout.</summary>
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Repeatedly connects to the S7-1500 and reads CPU diagnostics without accessing user data or writing.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task S71500_ConnectsAndReadsCpuDiagnostics_ReadOnlyEnduranceAsync()
    {
        var endpoint = GetEndpoint();
        var readCount = GetReadCount();
        var readDelay = GetReadDelay();
        var stopwatch = Stopwatch.StartNew();
        var completedReads = 0;

        using var plc = S71500.Create(endpoint, interval: 250);

        var connected = await plc.IsConnected
            .Where(static value => value)
            .Take(1)
            .Timeout(OperationTimeout)
            .FirstAsync();
        await TUnitAssert.That(connected).IsTrue();

        for (var index = 0; index < readCount; index++)
        {
            var cpuInfo = await plc.GetCpuInfo()
                .Take(1)
                .Timeout(OperationTimeout)
                .FirstAsync();

            await TUnitAssert.That(cpuInfo).IsNotNull();
            await TUnitAssert.That(cpuInfo.Length).IsGreaterThan(0);
            completedReads++;
            if (index + 1 < readCount && readDelay > TimeSpan.Zero)
            {
                await Task.Delay(readDelay);
            }
        }

        stopwatch.Stop();
        Trace.WriteLine(
            $"S7-1500 read-only endurance succeeded: endpoint={endpoint}; reads={completedReads}; delayMs={readDelay.TotalMilliseconds:F0}; elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F0}.");
        await TUnitAssert.That(completedReads).IsEqualTo(readCount);
        await TUnitAssert.That(plc.IsConnectedValue).IsTrue();
    }

    /// <summary>Gets the explicit endpoint override or the authorized default endpoint.</summary>
    /// <returns>The endpoint to connect to.</returns>
    private static string GetEndpoint()
    {
        var endpoint = Environment.GetEnvironmentVariable("S7PLCRX_LIVE_IP");
        return string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint;
    }

    /// <summary>Gets a bounded positive iteration count from the environment.</summary>
    /// <returns>The number of diagnostic reads to perform.</returns>
    private static int GetReadCount()
    {
        var configuredReadCount = Environment.GetEnvironmentVariable("S7PLCRX_LIVE_READ_COUNT");
        return int.TryParse(configuredReadCount, out var readCount) && readCount is > 0 and <= MaximumReadCount
            ? readCount
            : DefaultReadCount;
    }

    /// <summary>Gets a bounded delay between diagnostic reads from the environment.</summary>
    /// <returns>The delay between reads.</returns>
    private static TimeSpan GetReadDelay()
    {
        var configuredDelay = Environment.GetEnvironmentVariable("S7PLCRX_LIVE_READ_DELAY_MS");
        var delayMilliseconds =
            int.TryParse(configuredDelay, out var readDelay)
            && readDelay is >= 0 and <= MaximumReadDelayMilliseconds
                ? readDelay
                : DefaultReadDelayMilliseconds;
        return TimeSpan.FromMilliseconds(delayMilliseconds);
    }
}
#endif
