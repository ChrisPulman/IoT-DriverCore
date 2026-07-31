// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using IoT.Driver.S7PlcRx;
using IoT.Driver.S7PlcRx.Enums;
using IoT.Driver.S7PlcRx.Mock;
using IoT.Driver.S7PlcRx.TestApp;

namespace IoT.Driver.S7PlcRx.TestApp;

/// <summary>Provides the S7 mock-server sample application entry point.</summary>
public static class Program
{
    /// <summary>Maximum time to wait for the PLC connection.</summary>
    private const int ConnectionTimeoutSeconds = 10;

    /// <summary>Size of the simulated global-variable data block.</summary>
    private const int DefaultDataBlockSize = 10_088;

    /// <summary>Rack number of the simulated PLC.</summary>
    private const int PlcRack = 0;

    /// <summary>Slot number of the simulated PLC.</summary>
    private const int PlcSlot = 1;

    /// <summary>Duration of the global-variable simulation.</summary>
    private const int SimulationDurationMilliseconds = 250;

    /// <summary>Runs the S7 mock-server sample application.</summary>
    /// <returns>A task that represents the application lifetime.</returns>
    public static async Task Main()
    {
        using var server = new MockServer
        {
            DefaultDb1Size = DefaultDataBlockSize,
        };

        var rc = server.Start();

        if (rc != 0)
        {
            throw new InvalidOperationException($"MockServer.Start failed: {rc}");
        }

        // ── Connect PLC and register tag ───────────────────────────────────────
        using var plc = new RxS7(
            new(new(CpuType.S71500, MockServer.Localhost, PlcRack, PlcSlot)));

        _ = TagOperations.AddUpdateTagItem(
                plc,
                typeof(byte[]),
                "GlobalVariables",
                "DB1.DBB0",
                DefaultDataBlockSize)
            .SetPolling(false);

        // ── Wait for connection and read tag ───────────────────────────────────────
        await plc.IsConnected
            .Where(static isConnected => isConnected)
            .Timeout(System.TimeSpan.FromSeconds(ConnectionTimeoutSeconds))
            .FirstAsync();

        // Seed the tag with some data to read back
        var seedData = BuildGlobalVariablesSeedData(server.DefaultDb1Size, plc);

        plc.Value("GlobalVariables", seedData);

        using var simulationCancellationTokenSource = new CancellationTokenSource(
            System.TimeSpan.FromMilliseconds(SimulationDurationMilliseconds));

        try
        {
            await SimulateGlobalVariablesAsync(plc, TimeProvider.System, simulationCancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (simulationCancellationTokenSource.IsCancellationRequested)
        {
        }
    }

    /// <summary>Builds the initial simulated global-variable data block.</summary>
    /// <param name="size">The required data block size.</param>
    /// <param name="plc">The S7 client used to map seed values.</param>
    /// <returns>The populated data block.</returns>
    private static byte[] BuildGlobalVariablesSeedData(int size, RxS7 plc)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        ArgumentNullException.ThrowIfNull(plc);
        var builder = new GlobalVariableSeedBuilder(size, plc);

        foreach (var line in ReadEmbeddedLines("IoT.Driver.S7PlcRx.TestApp.GlobalVariablesSeed.csv"))
        {
            var fields = line.Split('|');
            object value = fields[1] switch
            {
                "bool" => bool.Parse(fields[2]),
                "byte" => byte.Parse(fields[2], CultureInfo.InvariantCulture),
                "short" => short.Parse(fields[2], CultureInfo.InvariantCulture),
                "ushort" => ushort.Parse(fields[2], CultureInfo.InvariantCulture),
                "int" => int.Parse(fields[2], CultureInfo.InvariantCulture),
                "uint" => uint.Parse(fields[2], CultureInfo.InvariantCulture),
                "float" => float.Parse(fields[2], CultureInfo.InvariantCulture),
                _ => throw new InvalidOperationException($"Unsupported seed type '{fields[1]}'.")
            };
            builder.Write(fields[0], value);
        }

        return builder.Data;
    }

    /// <summary>Reads an embedded line-delimited resource.</summary>
    /// <param name="resourceName">The manifest resource name.</param>
    /// <returns>The resource lines.</returns>
    private static List<string> ReadEmbeddedLines(string resourceName)
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The embedded resource '{resourceName}' is unavailable.");
        using var reader = new StreamReader(stream);
        var lines = new List<string>();

        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return lines;
    }

    /// <summary>Continuously updates the simulated global variables.</summary>
    /// <param name="plc">The S7 client to update.</param>
    /// <param name="timeProvider">The time source for waveform generation.</param>
    /// <param name="cancellationToken">The token that stops the simulation.</param>
    /// <returns>A task that represents the simulation loop.</returns>
    private static async Task SimulateGlobalVariablesAsync(
        RxS7 plc,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plc);
        ArgumentNullException.ThrowIfNull(timeProvider);

        const double FastWavePeriodSeconds = 2.5;

        const double SawWavePeriodSeconds = 10.0;

        const double SlowWavePeriodSeconds = 6.0;

        const float WaveShift = 1.0F;

        const int UpdateIntervalMilliseconds = 500;

        var simulationChannels = ReadEmbeddedLines("IoT.Driver.S7PlcRx.TestApp.GlobalVariablesSimulation.csv");
        var startTime = timeProvider.GetUtcNow();

        while (!cancellationToken.IsCancellationRequested)
        {
            var elapsedSeconds = (timeProvider.GetUtcNow() - startTime).TotalSeconds;
            var slowWave = MathF.Sin((float)(elapsedSeconds / SlowWavePeriodSeconds));
            var fastWave = MathF.Sin((float)(elapsedSeconds / FastWavePeriodSeconds));
            var sawWave = (float)((elapsedSeconds % SawWavePeriodSeconds) / SawWavePeriodSeconds);

            foreach (var channel in simulationChannels)
            {
                var fields = channel.Split('|');
                var baseline = float.Parse(fields[2], CultureInfo.InvariantCulture);
                var amplitude = float.Parse(fields[3], CultureInfo.InvariantCulture);
                object currentValue = fields[1] switch
                {
                    "directSlow" => baseline + (slowWave * amplitude),
                    "directFast" => baseline + (fastWave * amplitude),
                    "directSaw" => baseline + (sawWave * amplitude),
                    "inverseSaw" => baseline + ((WaveShift - sawWave) * amplitude),
                    "shiftedSlow" => baseline + ((slowWave + WaveShift) * amplitude),
                    "shiftedFast" => baseline + ((fastWave + WaveShift) * amplitude),
                    "nonnegativeFast" => !float.IsNegative(fastWave),
                    "constantTrue" => true,
                    _ => throw new InvalidOperationException($"Unsupported simulation formula '{fields[1]}'.")
                };
                plc.Value(fields[0], currentValue);
            }

            await Task.Delay(
                System.TimeSpan.FromMilliseconds(UpdateIntervalMilliseconds),
                cancellationToken).ConfigureAwait(false);
        }
    }
}
