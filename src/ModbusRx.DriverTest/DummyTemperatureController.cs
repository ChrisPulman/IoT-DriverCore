// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;

namespace ModbusRx.DriverTest;

/// <summary>Tests the DummyTemperatureController behavior.</summary>
public class DummyTemperatureController
{
    /// <summary>The number of samples in the normalized noise range.</summary>
    private const int NoiseSampleCount = 1_000_001;

    /// <summary>The maximum normalized noise sample value.</summary>
    private const double NoiseSampleMaximum = 1_000_000.0;

    /// <summary>The noise range midpoint.</summary>
    private const double NoiseOffset = 0.5;

    /// <summary>The maximum noise amplitude.</summary>
    private const double NoiseAmplitude = 0.2;

    /// <summary>The temperature register scale.</summary>
    private const double TemperatureScale = 10.0;

    /// <summary>The gain register scale.</summary>
    private const double GainScale = 1000.0;

    /// <summary>The setpoint register address.</summary>
    private const ushort SetpointAddress = 1;

    /// <summary>The gain register address.</summary>
    private const ushort GainAddress = 2;

    /// <summary>Gets the current temperature.</summary>
    /// <value>
    /// The current temperature.
    /// </value>
    public double CurrentTemperature { get; private set; } = 20.0;

    /// <summary>Gets or sets the setpoint.</summary>
    /// <value>
    /// The setpoint.
    /// </value>
    public double Setpoint { get; set; } = 50.0;

    /// <summary>Gets or sets the k.</summary>
    /// <value>
    /// The k.
    /// </value>
    public double K { get; set; } = 0.1; // Rate of change

    /// <summary>Updates this instance.</summary>
    public void Update()
    {
        // Add a little noise
        var noise =
            ((RandomNumberGenerator.GetInt32(0, NoiseSampleCount) / NoiseSampleMaximum) - NoiseOffset) * NoiseAmplitude;

        // Move toward setpoint
        var delta = (Setpoint - CurrentTemperature) * K;

        CurrentTemperature += delta + noise;
    }

    /// <summary>Reads the register.</summary>
    /// <param name="address">The address.</param>
    /// <returns>A ushort.</returns>
    public ushort ReadRegister(ushort address) => address switch
    {
        0 => (ushort)(CurrentTemperature * TemperatureScale), // e.g. 25.3°C → 253
        SetpointAddress => (ushort)(Setpoint * TemperatureScale),
        GainAddress => (ushort)(K * GainScale),
        _ => throw new ArgumentOutOfRangeException(nameof(address))
    };

    /// <summary>Writes the register.</summary>
    /// <param name="address">The address.</param>
    /// <param name="value">The value.</param>
    public void WriteRegister(ushort address, ushort value)
    {
        switch (address)
        {
            case SetpointAddress:
                {
                    Setpoint = value / TemperatureScale;
                    break;
                }

            case GainAddress:
                {
                    K = value / GainScale;
                    break;
                }
        }
    }
}
