// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ModbusRx.Data;

namespace ModbusRx.UnitTests;

/// <summary>Unit tests for SimulationDataProvider.</summary>
public class SimulationDataProviderTests
{
    /// <summary>The sine-wave frequency used by the test.</summary>
    private const double SineWaveFrequency = 1.0;

    /// <summary>The sine-wave phase used by the test.</summary>
    private const double SineWavePhase = 0.0;

    /// <summary>The square-wave duty cycle used by the test.</summary>
    private const double SquareWaveDutyCycle = 0.5;

    /// <summary>Gets a value indicating whether the tests are running in CI environment.</summary>
    private static bool IsRunningInCI =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")) ||
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"));

    /// <summary>Tests that SimulationDataProvider can be created and disposed.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task SimulationDataProvider_CreateAndDispose_ShouldNotThrowAsync()
    {
        // Arrange & Act & Assert
        using var provider = new SimulationDataProvider();
        await TUnit.Assertions.Assert.That(provider).IsNotNull();
        var isRunning = await provider.IsRunning.FirstAsync().ToTask();
        await TUnit.Assertions.Assert.That(isRunning).IsFalse();
    }

    /// <summary>Tests that simulation can be started and stopped.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task SimulationDataProvider_StartAndStop_ShouldUpdateRunningStateAsync()
    {
        // Arrange
        using var provider = new SimulationDataProvider();
        var dataStore = DataStoreFactory.CreateDefaultDataStore();

        // Act
        provider.Start(
            dataStore,
            TimeSpan.FromMilliseconds(Num.Value100),
            SimulationType.CountingUp);

        // Assert
        var isRunning = await provider.IsRunning.FirstAsync().ToTask();
        await TUnit.Assertions.Assert.That(isRunning).IsTrue();

        // Act
        provider.Stop();

        // Assert
        isRunning = await provider.IsRunning.FirstAsync().ToTask();
        await TUnit.Assertions.Assert.That(isRunning).IsFalse();
    }

    /// <summary>Tests sine wave generation.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task SimulationDataProvider_GenerateSineWave_ShouldCreateValidDataAsync()
    {
        // Arrange
        const int length = 100;
        const double amplitude = 32_767;

        // Act
        var data = SimulationDataProvider.GenerateSineWave(
            length,
            amplitude,
            SineWaveFrequency,
            SineWavePhase);

        // Assert
        await TUnit.Assertions.Assert.That(data.Length).IsEqualTo(length);
        foreach (var value in data)
        {
            await TUnit.Assertions.Assert.That(value <= Num.Value65535).IsTrue();
        }

        // Should have some variation
        await TUnit.Assertions.Assert.That(HasVariation(data)).IsTrue();
    }

    /// <summary>Tests square wave generation.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task SimulationDataProvider_GenerateSquareWave_ShouldCreateValidDataAsync()
    {
        // Arrange
        const int length = 100;
        const ushort highValue = 65_535;
        const ushort lowValue = 0;

        // Act
        var data = SimulationDataProvider.GenerateSquareWave(
            length,
            highValue,
            lowValue,
            SquareWaveDutyCycle);

        // Assert
        await TUnit.Assertions.Assert.That(data.Length).IsEqualTo(length);
        foreach (var value in data)
        {
            await TUnit.Assertions.Assert.That(value is highValue or lowValue).IsTrue();
        }

        // Should have both high and low values
        await TUnit.Assertions.Assert.That(data.Contains(highValue)).IsTrue();
        await TUnit.Assertions.Assert.That(data.Contains(lowValue)).IsTrue();
    }

    /// <summary>Tests sawtooth wave generation.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task SimulationDataProvider_GenerateSawtoothWave_ShouldCreateValidDataAsync()
    {
        // Arrange
        const int length = 100;
        const ushort maxValue = 1000;
        const ushort minValue = 0;

        // Act
        var data = SimulationDataProvider.GenerateSawtoothWave(length, maxValue, minValue);

        // Assert
        await TUnit.Assertions.Assert.That(data.Length).IsEqualTo(length);
        await TUnit.Assertions.Assert.That(data[0]).IsEqualTo(minValue);
        await TUnit.Assertions.Assert.That(data[^1]).IsEqualTo(maxValue);

        // Should be monotonically increasing
        for (var i = 1; i < length; i++)
        {
            await TUnit.Assertions.Assert.That(data[i] >= data[i - 1]).IsTrue();
        }
    }

    /// <summary>Tests random data generation.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task SimulationDataProvider_GenerateRandomData_ShouldCreateValidDataAsync()
    {
        // Arrange
        using var provider = new SimulationDataProvider();
        const int length = 100;
        const ushort minValue = 100;
        const ushort maxValue = 1000;

        // Act
        var data = provider.GenerateRandomData(length, minValue, maxValue);

        // Assert
        await TUnit.Assertions.Assert.That(data.Length).IsEqualTo(length);
        foreach (var value in data)
        {
            await TUnit.Assertions.Assert.That(value is >= minValue and <= maxValue).IsTrue();
        }

        // Should have variation
        await TUnit.Assertions.Assert.That(HasVariation(data)).IsTrue();
    }

    /// <summary>Tests test pattern loading.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task SimulationDataProvider_LoadTestPattern_ShouldUpdateDataStoreAsync()
    {
        // Arrange
        using var provider = new SimulationDataProvider();
        var dataStore = DataStoreFactory.CreateDefaultDataStore();

        // Act
        provider.LoadTestPattern(dataStore, TestPattern.CountingUp);

        // Assert - Check the actual data values, starting from index 1 in Modbus collections
        await TUnit.Assertions.Assert.That(dataStore.HoldingRegisters[1]).IsEqualTo((ushort)0);
        await TUnit.Assertions.Assert.That(dataStore.HoldingRegisters[2]).IsEqualTo((ushort)1);
        await TUnit.Assertions.Assert.That(dataStore.HoldingRegisters[3])
            .IsEqualTo((ushort)Num.Value2);
    }

    /// <summary>Tests continuous simulation updates data store.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task SimulationDataProvider_ContinuousSimulation_ShouldUpdateDataStoreAsync()
    {
        // Arrange
        using var provider = new SimulationDataProvider();
        var dataStore = DataStoreFactory.CreateDefaultDataStore();

        // Capture initial state of first real register (index 1)
        var initialValue = dataStore.HoldingRegisters[1];

        // Act
        provider.Start(
            dataStore,
            TimeSpan.FromMilliseconds(Num.Value50),
            SimulationType.CountingUp);

        // Use retry logic similar to ModbusServer tests for better reliability
        var maxRetries = IsRunningInCI ? Num.Value6 : Num.Value3;
        var baseWaitTime = IsRunningInCI
            ? TimeSpan.FromMilliseconds(Num.Value400)
            : TimeSpan.FromMilliseconds(Num.Value250);
        var dataChanged = false;

        for (var retry = 0; retry < maxRetries && !dataChanged; retry++)
        {
            await Task.Delay(baseWaitTime);
            var currentValue = dataStore.HoldingRegisters[1];
            dataChanged = currentValue != initialValue;

            if (!dataChanged && retry < maxRetries - 1)
            {
                // Give a bit more time between retries
                await Task.Delay(TimeSpan.FromMilliseconds(Num.Value100));
            }
        }

        provider.Stop();

        await TUnit.Assertions.Assert.That(dataChanged).IsTrue();
    }

    /// <summary>Tests that different simulation types produce different patterns.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task SimulationDataProvider_DifferentSimulationTypes_ShouldProduceDifferentPatternsAsync()
    {
        // Arrange
        using var provider = new SimulationDataProvider();
        var dataStore1 = DataStoreFactory.CreateDefaultDataStore();
        var dataStore2 = DataStoreFactory.CreateDefaultDataStore();

        // Use longer wait times for .NET Framework 4.8 CI reliability
        var baseWaitTime = IsRunningInCI
            ? TimeSpan.FromMilliseconds(Num.Value400)
            : TimeSpan.FromMilliseconds(Num.Value250);
        var maxRetries = IsRunningInCI ? Num.Value4 : Num.Value2;

        // Act - Start first simulation (CountingUp)
        provider.Start(
            dataStore1,
            TimeSpan.FromMilliseconds(Num.Value50),
            SimulationType.CountingUp);

        var simulation1Succeeded = false;
        for (var retry = 0; retry < maxRetries && !simulation1Succeeded; retry++)
        {
            await Task.Delay(baseWaitTime);
            var currentData = CopyFirst(dataStore1.HoldingRegisters, Num.Value5);

            // CountingUp should produce sequential values starting from 0
            simulation1Succeeded = ContainsPositiveValue(currentData) || HasVariation(currentData);
        }

        provider.Stop();

        // Start second simulation (Random)
        provider.Start(
            dataStore2,
            TimeSpan.FromMilliseconds(Num.Value50),
            SimulationType.Random);

        var simulation2Succeeded = false;
        for (var retry = 0; retry < maxRetries && !simulation2Succeeded; retry++)
        {
            await Task.Delay(baseWaitTime);
            var currentData = CopyFirst(dataStore2.HoldingRegisters, Num.Value5);

            // Random should produce varied values
            simulation2Succeeded = ContainsPositiveValue(currentData) || HasVariation(currentData);
        }

        provider.Stop();

        // Assert - Get final values for comparison
        var values1 = CopyFirst(dataStore1.HoldingRegisters, Num.Value10);
        var values2 = CopyFirst(dataStore2.HoldingRegisters, Num.Value10);

        // More flexible assertion - different simulation types should produce different results
        var patternsAreDifferent = !SequenceEqual(values1, values2) ||
                                  CountDistinct(values1) != CountDistinct(values2);

        await TUnit.Assertions.Assert.That(patternsAreDifferent).IsTrue();
    }

    /// <summary>Copies the first values from a sequence.</summary>
    /// <param name="values">The source values.</param>
    /// <param name="count">The maximum number of values to copy.</param>
    /// <returns>The copied values.</returns>
    private static ushort[] CopyFirst(IEnumerable<ushort> values, int count)
    {
        var result = new List<ushort>(count);
        foreach (var value in values)
        {
            if (result.Count == count)
            {
                break;
            }

            result.Add(value);
        }

        return result.ToArray();
    }

    /// <summary>Determines whether values contain more than one distinct value.</summary>
    /// <param name="values">The values to inspect.</param>
    /// <returns>A value indicating whether variation exists.</returns>
    private static bool HasVariation(IReadOnlyList<ushort> values) => CountDistinct(values) > 1;

    /// <summary>Determines whether values contain a positive value.</summary>
    /// <param name="values">The values to inspect.</param>
    /// <returns>A value indicating whether any value is positive.</returns>
    private static bool ContainsPositiveValue(IEnumerable<ushort> values)
    {
        foreach (var value in values)
        {
            if (value > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Counts distinct values with explicit iteration.</summary>
    /// <param name="values">The values to inspect.</param>
    /// <returns>The number of distinct values.</returns>
    private static int CountDistinct(IEnumerable<ushort> values)
    {
        var distinct = new HashSet<ushort>();
        foreach (var value in values)
        {
            _ = distinct.Add(value);
        }

        return distinct.Count;
    }

    /// <summary>Compares two value arrays.</summary>
    /// <param name="left">The left values.</param>
    /// <param name="right">The right values.</param>
    /// <returns>A value indicating whether both arrays contain the same values.</returns>
    private static bool SequenceEqual(ushort[] left, ushort[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }
}
