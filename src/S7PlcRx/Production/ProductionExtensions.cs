// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

#if REACTIVE_SHIM
namespace IoT.DriverCore.S7PlcRx.Reactive.Production;
#else
namespace IoT.DriverCore.S7PlcRx.Production;
#endif

/// <summary>
/// Provides extension methods for enabling production-grade error handling, retry logic, and system validation on PLC
/// instances using the circuit breaker pattern.
/// </summary>
/// <remarks>These extension methods are intended to enhance the reliability and readiness of PLC-based systems in
/// production environments. They offer mechanisms for robust error handling, configurable retry strategies, and
/// comprehensive validation routines to assess system health and readiness for production deployment. All methods
/// require a valid IRxS7 PLC instance and may utilize user-supplied or default configuration objects. Thread safety is
/// ensured for shared resources such as circuit breakers.</remarks>
public static class ProductionExtensions
{
    /// <summary>Defines the delay between production reliability checks.</summary>
    private const int ReliabilityCheckDelayMilliseconds = 100;

    /// <summary>Defines the multiplier used to express a ratio as a percentage.</summary>
    private const double PercentageScale = 100;

    /// <summary>Gets the circuit breakers used for production error handling.</summary>
    private static ConcurrentDictionary<string, CircuitBreaker> CircuitBreakers { get; } = new();

    /// <summary>Enables production error handling for the specified PLC using the provided configuration.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="config">The configuration settings to use for production error handling.</param>
    /// <returns>A new instance of <see cref="ProductionErrorHandler"/> configured for the specified PLC.</returns>
    public static ProductionErrorHandler EnableProductionErrorHandling(
        IRxS7 plc,
        ProductionErrorConfig config)
    {
        if (plc is null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        if (config is null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        return new ProductionErrorHandler(config);
    }

    /// <summary>Executes an asynchronous PLC operation with the default error-handling configuration.</summary>
    /// <typeparam name="T">The type of the result returned by the operation.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="operation">A function that represents the asynchronous operation to execute.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static Task<T> ExecuteWithErrorHandlingAsync<T>(IRxS7 plc, Func<Task<T>> operation) =>
        ExecuteWithErrorHandlingAsync(plc, operation, new ProductionErrorConfig());

    /// <summary>Executes an asynchronous PLC operation with error handling and circuit-breaker protection.</summary>
    /// <typeparam name="T">The type of the result returned by the operation.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="operation">A function that represents the asynchronous operation to execute.</param>
    /// <param name="config">The error-handling and circuit-breaker configuration.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static Task<T> ExecuteWithErrorHandlingAsync<T>(
        IRxS7 plc,
        Func<Task<T>> operation,
        ProductionErrorConfig config)
    {
        Guard.NotNull(plc, nameof(plc));
        Guard.NotNull(operation, nameof(operation));
        Guard.NotNull(config, nameof(config));

        var circuitBreakerKey = $"{plc.IP}_{plc.PLCType}";
        var circuitBreaker = CircuitBreakers.GetOrAdd(circuitBreakerKey, _ => new CircuitBreaker(config));
        return circuitBreaker.ExecuteAsync(operation);
    }

    /// <summary>Validates production readiness using the default validation configuration.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static Task<SystemValidationResult> ValidateProductionReadinessAsync(IRxS7 plc) =>
        ValidateProductionReadinessAsync(plc, new ProductionValidationConfig());

    /// <summary>Validates whether the PLC is ready for production deployment.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="validationConfig">The validation parameters and thresholds.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static Task<SystemValidationResult> ValidateProductionReadinessAsync(
        IRxS7 plc,
        ProductionValidationConfig validationConfig) =>
        ValidateProductionReadinessAsync(plc, validationConfig, TimeProvider.System);

    /// <summary>Validates whether the PLC is ready for production deployment.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="validationConfig">The validation parameters and thresholds.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task<SystemValidationResult> ValidateProductionReadinessAsync(
        IRxS7 plc,
        ProductionValidationConfig validationConfig,
        TimeProvider timeProvider)
    {
        Guard.NotNull(plc, nameof(plc));
        Guard.NotNull(validationConfig, nameof(validationConfig));

        var result = new SystemValidationResult
        {
            ValidationStartTime = timeProvider.GetUtcNow(),
            PLCIdentifier = $"{plc.IP}:{plc.PLCType}",
        };

        try
        {
            await ValidateConnectivityAsync(plc, result, timeProvider);
            await ValidatePerformanceAsync(plc, result, validationConfig, timeProvider);
            await ValidateReliabilityAsync(plc, result, validationConfig, timeProvider);

            result.OverallScore = CalculateOverallScore(result);
            result.IsProductionReady = result.OverallScore >= validationConfig.MinimumProductionScore;
        }
        catch (Exception ex)
        {
            result.CriticalErrors.Add($"Validation failed: {ex.Message}");
            result.IsProductionReady = false;
        }

        result.ValidationEndTime = timeProvider.GetUtcNow();
        return result;
    }

    /// <summary>
    /// Performs a connectivity validation test on the specified PLC and records the result in the provided validation
    /// result object.
    /// </summary>
    /// <remarks>This method adds a new connectivity test entry to the <paramref name="result"/> object,
    /// including details about the PLC's CPU information if available. The method does not throw exceptions; any errors
    /// encountered during validation are captured in the test result.</remarks>
    /// <param name="plc">The non-null PLC instance to validate. It should already be connected.</param>
    /// <param name="result">The non-null validation result object to which the connectivity result is added.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task ValidateConnectivityAsync(IRxS7 plc, SystemValidationResult result, TimeProvider timeProvider)
    {
        var connectivityTest = new ValidationTest { TestName = "Connectivity", StartTime = timeProvider.GetUtcNow() };

        try
        {
            if (!plc.IsConnectedValue)
            {
                connectivityTest.Success = false;
                connectivityTest.ErrorMessage = "PLC is not connected";
            }
            else
            {
                var cpuInfo = await plc.GetCpuInfo();
                connectivityTest.Success = cpuInfo?.Length > 0;
                connectivityTest.Details.Add($"CPU Info: {string.Join(", ", cpuInfo ?? [])}");
            }
        }
        catch (Exception ex)
        {
            connectivityTest.Success = false;
            connectivityTest.ErrorMessage = ex.Message;
        }

        connectivityTest.EndTime = timeProvider.GetUtcNow();
        result.ValidationTests.Add(connectivityTest);
    }

    /// <summary>
    /// Performs a performance validation test on the specified PLC and records the results in the provided validation
    /// result object.
    /// </summary>
    /// <remarks>This method measures the response time of the PLC by invoking a CPU information request and
    /// compares it to the maximum acceptable response time specified in the configuration. The outcome, including
    /// success status and response time details, is recorded in the validation result. If an exception occurs during
    /// the test, the error message is captured in the result.</remarks>
    /// <param name="plc">The PLC interface to be tested for performance. Cannot be null.</param>
    /// <param name="result">The non-null result object to which the performance validation outcome is added.</param>
    /// <param name="config">The non-null configuration that defines acceptable performance thresholds.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private static async Task ValidatePerformanceAsync(
        IRxS7 plc,
        SystemValidationResult result,
        ProductionValidationConfig config,
        TimeProvider timeProvider)
    {
        var performanceTest = new ValidationTest { TestName = "Performance", StartTime = timeProvider.GetUtcNow() };

        try
        {
            var responseStart = timeProvider.GetUtcNow().UtcDateTime;
            await plc.GetCpuInfo();
            var responseTime = timeProvider.GetUtcNow().UtcDateTime - responseStart;

            performanceTest.Details.Add($"Response Time: {responseTime.TotalMilliseconds:F0}ms");

            if (responseTime > config.MaxAcceptableResponseTime)
            {
                performanceTest.Success = false;
                performanceTest.ErrorMessage =
                    $"Response time ({responseTime.TotalMilliseconds:F0}ms) exceeds maximum " +
                    $"({config.MaxAcceptableResponseTime.TotalMilliseconds:F0}ms)";
            }
            else
            {
                performanceTest.Success = true;
            }
        }
        catch (Exception ex)
        {
            performanceTest.Success = false;
            performanceTest.ErrorMessage = ex.Message;
        }

        performanceTest.EndTime = timeProvider.GetUtcNow();
        result.ValidationTests.Add(performanceTest);
    }

    /// <summary>Performs a reliability validation test and records the results in the provided validation result.</summary>
    /// <remarks>The method executes a series of consecutive operations against the PLC to determine its
    /// reliability, based on the configured number of attempts and minimum reliability rate. The outcome, including
    /// success rate and any error messages, is recorded in the validation result. This method does not throw on
    /// individual operation failures; instead, it aggregates results to determine overall reliability.</remarks>
    /// <param name="plc">The PLC interface to be tested for reliability. Cannot be null.</param>
    /// <param name="result">The non-null validation result object to which the reliability result is added.</param>
    /// <param name="config">The non-null configuration that defines test iterations and minimum reliability.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private static async Task ValidateReliabilityAsync(
        IRxS7 plc,
        SystemValidationResult result,
        ProductionValidationConfig config,
        TimeProvider timeProvider)
    {
        var reliabilityTest = new ValidationTest { TestName = "Reliability", StartTime = timeProvider.GetUtcNow() };

        try
        {
            var consecutiveOperations = config.ReliabilityTestCount;
            var successCount = 0;

            for (var i = 0; i < consecutiveOperations; i++)
            {
                try
                {
                    await plc.GetCpuInfo();
                    successCount++;
                }
                catch (Exception ex)
                {
                    reliabilityTest.Details.Add($"Reliability operation {i + 1} failed: {ex.Message}");
                }

                if (i < consecutiveOperations - 1)
                {
                    await Task.Delay(ReliabilityCheckDelayMilliseconds);
                }
            }

            var successRate = (double)successCount / consecutiveOperations;
            reliabilityTest.Details.Add($"Success Rate: {successRate:P2} ({successCount}/{consecutiveOperations})");

            if (successRate >= config.MinimumReliabilityRate)
            {
                reliabilityTest.Success = true;
            }
            else
            {
                reliabilityTest.Success = false;
                reliabilityTest.ErrorMessage =
                    $"Reliability rate ({successRate:P2}) below minimum " +
                    $"({config.MinimumReliabilityRate:P2})";
            }
        }
        catch (Exception ex)
        {
            reliabilityTest.Success = false;
            reliabilityTest.ErrorMessage = ex.Message;
        }

        reliabilityTest.EndTime = timeProvider.GetUtcNow();
        result.ValidationTests.Add(reliabilityTest);
    }

    /// <summary>Calculates the overall success rate of validation tests.</summary>
    /// <param name="result">The non-null result object containing the validation tests to evaluate.</param>
    /// <returns>
    /// The percentage of successful validation tests, or zero when no validation tests are present.
    /// </returns>
    private static double CalculateOverallScore(SystemValidationResult result)
    {
        if (result.ValidationTests.Count == 0)
        {
            return 0;
        }

        var successfulTests = 0;
        foreach (var test in result.ValidationTests)
        {
            if (test.Success)
            {
                successfulTests++;
            }
        }

        return (double)successfulTests / result.ValidationTests.Count * PercentageScale;
    }
}
