// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Describes the latency and result of one scripted simulator transfer.</summary>
public sealed class SimulatorOutcome
{
    /// <summary>Initializes a new instance of the <see cref="SimulatorOutcome"/> class.</summary>
    /// <param name="latency">The non-negative latency.</param>
    /// <param name="error">The optional expected error.</param>
    /// <param name="exception">The optional exceptional error.</param>
    private SimulatorOutcome(TimeSpan latency, string? error, Exception? exception)
    {
        if (latency < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(latency));
        }

        Latency = latency;
        Error = error ?? string.Empty;
        Exception = exception;
    }

    /// <summary>Gets the delay applied before the outcome is returned or thrown.</summary>
    public TimeSpan Latency { get; }

    /// <summary>Gets the expected failure message, or an empty string for success and exception outcomes.</summary>
    public string Error { get; }

    /// <summary>Gets the exceptional failure to throw, or <see langword="null"/> otherwise.</summary>
    public Exception? Exception { get; }

    /// <summary>Gets whether this outcome represents a successful transfer.</summary>
    public bool Succeeded => Error.Length == 0 && Exception is null;

    /// <summary>Creates a successful transfer outcome.</summary>
    /// <returns>The successful outcome.</returns>
    public static SimulatorOutcome Success() => Success(TimeSpan.Zero);

    /// <summary>Creates a successful transfer outcome.</summary>
    /// <param name="latency">The non-negative latency applied before success.</param>
    /// <returns>The successful outcome.</returns>
    public static SimulatorOutcome Success(TimeSpan latency) => new(latency, null, null);

    /// <summary>Creates an expected transfer failure.</summary>
    /// <param name="error">The non-empty failure message.</param>
    /// <returns>The expected failure outcome.</returns>
    public static SimulatorOutcome Failure(string error) => Failure(error, TimeSpan.Zero);

    /// <summary>Creates an expected transfer failure.</summary>
    /// <param name="error">The non-empty failure message.</param>
    /// <param name="latency">The non-negative latency applied before failure.</param>
    /// <returns>The expected failure outcome.</returns>
    public static SimulatorOutcome Failure(string error, TimeSpan latency) =>
        new(latency, LogicalTag.Required(error, nameof(error)), null);

    /// <summary>Creates an exceptional transfer outcome.</summary>
    /// <param name="exception">The exception thrown after the configured latency.</param>
    /// <returns>The exceptional outcome.</returns>
    public static SimulatorOutcome Throw(Exception exception) => Throw(exception, TimeSpan.Zero);

    /// <summary>Creates an exceptional transfer outcome.</summary>
    /// <param name="exception">The exception thrown after the configured latency.</param>
    /// <param name="latency">The non-negative latency applied before the exception.</param>
    /// <returns>The exceptional outcome.</returns>
    public static SimulatorOutcome Throw(Exception exception, TimeSpan latency) =>
        new(latency, null, exception ?? throw new ArgumentNullException(nameof(exception)));
}
