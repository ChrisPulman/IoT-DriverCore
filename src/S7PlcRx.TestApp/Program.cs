// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;
using MockS7Plc;
using S7PlcRx;
using S7PlcRx.TestApp;

const int ConnectionTimeoutSeconds = 10;

const int DefaultDataBlockSize = 10_088;

const int PlcRack = 0;

const int PlcSlot = 1;

const int SimulationDurationMilliseconds = 250;

using var server = new MockServer();

server.DefaultDb1Size = DefaultDataBlockSize;

var rc = server.Start();

if (rc != 0)
{
    throw new InvalidOperationException($"MockServer.Start failed: {rc}");
}

// ── Connect PLC and register tag ───────────────────────────────────────
using var plc = new RxS7(
    new(new(S7PlcRx.Enums.CpuType.S71500, MockServer.Localhost, PlcRack, PlcSlot)));

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
    await SimulateGlobalVariablesAsync(plc, simulationCancellationTokenSource.Token);
}
catch (OperationCanceledException) when (simulationCancellationTokenSource.IsCancellationRequested)
{
}

static byte[] BuildGlobalVariablesSeedData(int size, RxS7 plc)
{
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
    ArgumentNullException.ThrowIfNull(plc);
    var builder = new GlobalVariableSeedBuilder(size, plc);

    foreach (var line in ReadEmbeddedLines("S7PlcRx.TestApp.GlobalVariablesSeed.csv"))
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

static List<string> ReadEmbeddedLines(string resourceName)
{
    using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
        ?? throw new InvalidOperationException($"The embedded resource '{resourceName}' is unavailable.");
    using var reader = new StreamReader(stream);
    var lines = new List<string>();

    while (reader.ReadLine() is { } line)
    {
        lines.Add(line);
    }

    return lines;
}

static async Task SimulateGlobalVariablesAsync(RxS7 plc, CancellationToken cancellationToken)
{
    ArgumentNullException.ThrowIfNull(plc);

    const double FastWavePeriodSeconds = 2.5;

    const double SawWavePeriodSeconds = 10.0;

    const double SlowWavePeriodSeconds = 6.0;

    const float WaveShift = 1.0F;

    const int UpdateIntervalMilliseconds = 500;

    var simulationChannels = ReadEmbeddedLines("S7PlcRx.TestApp.GlobalVariablesSimulation.csv");
    var startTime = System.DateTime.UtcNow;

    while (!cancellationToken.IsCancellationRequested)
    {
        var elapsedSeconds = (System.DateTime.UtcNow - startTime).TotalSeconds;
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
