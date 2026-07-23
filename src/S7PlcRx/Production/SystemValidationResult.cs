// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.S7PlcRx.Reactive.Production;
#else
namespace IoT.DriverCore.S7PlcRx.Production;
#endif

/// <summary>Represents the result of system validation.</summary>
/// <remarks>Use this class to capture and inspect the outcome of a system validation run, such as for a PLC or
/// similar automated system. It provides details about the validation period, individual test results, critical errors,
/// and an overall score indicating system readiness for production.</remarks>
public sealed class SystemValidationResult
{
    /// <summary>Gets or sets the validation start time.</summary>
    public DateTimeOffset ValidationStartTime { get; set; }

    /// <summary>Gets or sets the validation end time.</summary>
    public DateTimeOffset ValidationEndTime { get; set; }

    /// <summary>Gets or sets the PLC identifier.</summary>
    public string PLCIdentifier { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the system is production ready.</summary>
    public bool IsProductionReady { get; set; }

    /// <summary>Gets or sets the overall validation score (0-100).</summary>
    public double OverallScore { get; set; }

    /// <summary>Gets the individual validation tests.</summary>
    public List<ValidationTest> ValidationTests { get; } = [];

    /// <summary>Gets critical errors that prevent production use.</summary>
    public List<string> CriticalErrors { get; } = [];

    /// <summary>Gets the total validation time.</summary>
    public TimeSpan TotalValidationTime => ValidationEndTime - ValidationStartTime;
}
