// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if OMRON_CS1G_HARDWARE_TESTS
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using IoT.DriverCore.OmronPlcRx.Enums;
using TUnit.Core;

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>
/// Read-only endurance coverage for the dedicated CS1G CPU42H Toolbus connection.
/// Compile this file explicitly with <c>OMRON_CS1G_HARDWARE_TESTS</c>; it is excluded from
/// normal and CI test runs because it opens physical serial port COM3.
/// </summary>
[NotInParallel("omron-cs1g-com3")]
public sealed class OmronCs1gHardwareTests
{
    /// <summary>Local FINS source node for the direct Toolbus connection.</summary>
    private const byte LocalNode = 11;

    /// <summary>Direct CPU destination node.</summary>
    private const byte RemoteNode = 0;

    /// <summary>Maximum duration for a single serial request.</summary>
    private const int RequestTimeoutMilliseconds = 2_000;

    /// <summary>Default number of sequential read-only clock and cycle-time pairs.</summary>
    private const int DefaultReadPairCount = 100;

    /// <summary>Maximum number of configured clock and cycle-time pairs.</summary>
    private const int MaximumReadPairCount = 10_000;

    /// <summary>Default delay between read pairs.</summary>
    private const int DefaultReadPairDelayMilliseconds = 50;

    /// <summary>Maximum configured delay between read pairs.</summary>
    private const int MaximumReadPairDelayMilliseconds = 10_000;

    /// <summary>Default physical serial port assigned to the CS1G Toolbus connection.</summary>
    private const string DefaultPortName = "COM3";

    /// <summary>Controller model reported by the documented CS1G hardware.</summary>
    private const string ExpectedControllerModel = "CS1G_CPU42H";

    /// <summary>Controller version reported by the documented CS1G hardware.</summary>
    private const string ExpectedControllerVersion = "02.20";

    /// <summary>Poll interval used solely to initialize the read-only facade.</summary>
    private const int PollIntervalSeconds = 30;

    /// <summary>Maximum wait for initial controller-information discovery.</summary>
    private const int InitializationTimeoutSeconds = 10;

    /// <summary>Minimum supported CS1G clock year.</summary>
    private const int MinimumClockYear = 1998;

    /// <summary>Maximum supported CS1G clock year.</summary>
    private const int MaximumClockYear = 2069;

    /// <summary>Polling delay while controller information is being discovered.</summary>
    private const int InitializationPollMilliseconds = 50;

    /// <summary>
    /// Repeats only controller-information, clock-read, and cycle-time-read operations. No PLC
    /// memory or clock write command is issued.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token enforced by the TUnit timeout.</param>
    /// <returns>A task representing the hardware validation.</returns>
    [Test]
    [Timeout(900_000)]
    public async Task Cs1gToolbus_ReadOnlyClockAndCycleTimeEnduranceAsync(
        CancellationToken cancellationToken)
    {
        var portName = Environment.GetEnvironmentVariable("OMRON_CS1G_COM_PORT");
        var readPairCount = GetBoundedEnvironmentValue(
            "OMRON_CS1G_READ_PAIR_COUNT",
            DefaultReadPairCount,
            1,
            MaximumReadPairCount);
        var readPairDelayMilliseconds = GetBoundedEnvironmentValue(
            "OMRON_CS1G_READ_PAIR_DELAY_MS",
            DefaultReadPairDelayMilliseconds,
            0,
            MaximumReadPairDelayMilliseconds);
        var options = OmronSerialOptions.CreateToolbus(
            string.IsNullOrWhiteSpace(portName) ? DefaultPortName : portName);
        using var plc = new OmronPlcRx(
            LocalNode,
            RemoteNode,
            options,
            RequestTimeoutMilliseconds,
            retries: 0,
            pollInterval: TimeSpan.FromSeconds(PollIntervalSeconds));
        Exception? initializationError = null;
        using var errors = plc.Errors.Subscribe(
            new ActionObserver<OmronPLCException?>(error => initializationError ??= error));

        await WaitForControllerInformationAsync(
            plc,
            TimeSpan.FromSeconds(InitializationTimeoutSeconds),
            () => initializationError,
            cancellationToken).ConfigureAwait(false);

        await Assert.That(plc.PlcType).IsEqualTo(PlcType.C_Series);
        await Assert.That(plc.ControllerModel).IsEqualTo(ExpectedControllerModel);
        await Assert.That(plc.ControllerVersion).IsEqualTo(ExpectedControllerVersion);

        for (var iteration = 0; iteration < readPairCount; iteration++)
        {
            await AssertReadPairAsync(plc, cancellationToken).ConfigureAwait(false);
            if (iteration + 1 < readPairCount && readPairDelayMilliseconds > 0)
            {
                await Task.Delay(readPairDelayMilliseconds, cancellationToken).ConfigureAwait(false);
            }
        }

        Trace.WriteLine(
            $"Omron CS1G read-only endurance succeeded: port={options.PortName}; pairs={readPairCount}; delayMs={readPairDelayMilliseconds}.");
        Trace.WriteLine($"Controller model={plc.ControllerModel}; version={plc.ControllerVersion}.");
    }

    /// <summary>Reads and validates one clock and cycle-time pair.</summary>
    /// <param name="plc">The live Omron client.</param>
    /// <param name="cancellationToken">The TUnit timeout token.</param>
    /// <returns>A task representing the read and assertions.</returns>
    private static async Task AssertReadPairAsync(
        OmronPlcRx plc,
        CancellationToken cancellationToken)
    {
        var clock = await plc.ReadClockAsync(cancellationToken).ConfigureAwait(false);
        var cycle = await plc.ReadCycleTimeAsync(cancellationToken).ConfigureAwait(false);

        await Assert.That(clock.BytesSent).IsGreaterThan(0);
        await Assert.That(clock.BytesReceived).IsGreaterThan(0);
        await Assert.That(clock.PacketsSent).IsEqualTo(1);
        await Assert.That(clock.PacketsReceived).IsEqualTo(1);
        await Assert.That(clock.Clock.Year).IsGreaterThanOrEqualTo(MinimumClockYear);
        await Assert.That(clock.Clock.Year).IsLessThanOrEqualTo(MaximumClockYear);
        await Assert.That(cycle.BytesSent).IsGreaterThan(0);
        await Assert.That(cycle.BytesReceived).IsGreaterThan(0);
        await Assert.That(cycle.PacketsSent).IsEqualTo(1);
        await Assert.That(cycle.PacketsReceived).IsEqualTo(1);
        await Assert.That(cycle.MinimumCycleTime).IsGreaterThanOrEqualTo(0D);
        await Assert.That(cycle.AverageCycleTime).IsGreaterThanOrEqualTo(cycle.MinimumCycleTime);
        await Assert.That(cycle.MaximumCycleTime).IsGreaterThanOrEqualTo(cycle.AverageCycleTime);
    }

    /// <summary>Waits for the facade's background initialization to identify the controller.</summary>
    /// <param name="plc">Read-only facade owning the Toolbus serial channel.</param>
    /// <param name="timeout">Maximum wait for controller information.</param>
    /// <param name="getInitializationError">Returns the serial initialization error, if one was raised.</param>
    /// <param name="cancellationToken">Cancellation token enforced by the TUnit timeout.</param>
    /// <returns>A task representing the wait.</returns>
    private static async Task WaitForControllerInformationAsync(
        OmronPlcRx plc,
        TimeSpan timeout,
        Func<Exception?> getInitializationError,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (plc.ControllerModel is not null && plc.ControllerVersion is not null)
            {
                return;
            }

            await Task.Delay(InitializationPollMilliseconds, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException(
            "CS1G Toolbus controller-information read did not complete within 10 seconds.",
            getInitializationError());
    }

    /// <summary>Gets a bounded integer environment option.</summary>
    /// <param name="name">Environment variable name.</param>
    /// <param name="defaultValue">Value used when the variable is absent or invalid.</param>
    /// <param name="minimum">Minimum accepted value.</param>
    /// <param name="maximum">Maximum accepted value.</param>
    /// <returns>The configured or default value.</returns>
    private static int GetBoundedEnvironmentValue(
        string name,
        int defaultValue,
        int minimum,
        int maximum)
    {
        var configuredValue = Environment.GetEnvironmentVariable(name);
        return int.TryParse(configuredValue, out var value) && value >= minimum && value <= maximum
            ? value
            : defaultValue;
    }

    /// <summary>Adapts a value callback to the BCL observable contract used by the public driver API.</summary>
    /// <typeparam name="T">Observable value type.</typeparam>
    private sealed class ActionObserver<T> : IObserver<T>
    {
        /// <summary>Receives values from the observed sequence.</summary>
        private readonly Action<T> _onNext;

        /// <summary>Initializes a new instance of the <see cref="ActionObserver{T}"/> class.</summary>
        /// <param name="onNext">Callback invoked for each observed value.</param>
        public ActionObserver(Action<T> onNext) => _onNext = onNext;

        /// <inheritdoc/>
        public void OnCompleted()
        {
        }

        /// <inheritdoc/>
        public void OnError(Exception error) => _onNext(default!);

        /// <inheritdoc/>
        public void OnNext(T value) => _onNext(value);
    }
}
#endif
