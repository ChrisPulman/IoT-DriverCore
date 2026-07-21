// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Optimization;
#else
namespace S7PlcRx.Optimization;
#endif

/// <summary>
/// Provides configuration options for optimizing write operations, including parallelism, verification, timing, and
/// concurrency settings.
/// </summary>
/// <remarks>Use this class to customize the behavior of write operations, such as enabling parallel writes,
/// specifying verification requirements, and controlling delays and timeouts. Adjusting these settings can help balance
/// performance and reliability based on application needs.</remarks>
public sealed class WriteOptimizationConfig
{
    /// <summary>Gets or sets whether parallel writes within data block groups are enabled.</summary>
    public bool EnableParallelWrites { get; set; }

    /// <summary>Gets or sets a value indicating whether writes are verified by reading them back.</summary>
    public bool VerifyWrites { get; set; }

    /// <summary>Gets or sets the delay between data block groups in milliseconds.</summary>
    public int InterGroupDelayMs { get; set; } = 50;

    /// <summary>Gets or sets the maximum number of concurrent writes.</summary>
    public int MaxConcurrentWrites { get; set; } = 5;

    /// <summary>Gets or sets the write timeout in milliseconds.</summary>
    public int WriteTimeoutMs { get; set; } = 5000;
}
