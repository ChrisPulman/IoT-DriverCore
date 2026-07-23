// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.Data;
#else
namespace IoT.DriverCore.ModbusRx.Data;
#endif

/// <summary>Provides simulation data for Modbus testing and development.</summary>
public sealed class SimulationDataProvider : IDisposable
{
    /// <summary>Stores the random Number Generator value.</summary>
    private readonly RandomNumberGenerator _randomNumberGenerator = RandomNumberGenerator.Create();

    /// <summary>Stores the is Running value.</summary>
    private readonly BehaviorSignal<bool> _isRunning = new(false);

    /// <summary>Stores the disposables value.</summary>
    private readonly CompositeDisposable _disposables = new();

    /// <summary>Stores the time provider value.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>Stores the timer value.</summary>
    private IObservable<long>? _timer;

    /// <summary>Initializes a new instance of the <see cref="SimulationDataProvider"/> class.</summary>
    public SimulationDataProvider()
        : this(TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SimulationDataProvider"/> class.</summary>
    /// <param name="timeProvider">The time provider used for simulation timing.</param>
    public SimulationDataProvider(TimeProvider? timeProvider)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _disposables.Add(_isRunning);
    }

    /// <summary>Gets an observable indicating if simulation is running.</summary>
    public IObservable<bool> IsRunning => _isRunning.AsObservable();

    /// <summary>Generates sine wave pattern data.</summary>
    /// <param name="length">The number of data points.</param>
    /// <param name="amplitude">The amplitude of the sine wave.</param>
    /// <param name="frequency">The frequency of the sine wave.</param>
    /// <param name="phase">The phase offset.</param>
    /// <returns>An array of sine wave values.</returns>
    public static ushort[] GenerateSineWave(int length, double amplitude, double frequency, double phase)
    {
        var result = new ushort[length];
        for (var i = 0; i < length; i++)
        {
            var value = (amplitude * Math.Sin((Two * Math.PI * frequency * i / length) + phase)) + amplitude;
            result[i] = (ushort)Math.Max(0, Math.Min(UShortMaximum, value));
        }

        return result;
    }

    /// <summary>Generates square wave pattern data.</summary>
    /// <param name="length">The number of data points.</param>
    /// <param name="highValue">The high value of the square wave.</param>
    /// <param name="lowValue">The low value of the square wave.</param>
    /// <param name="dutyCycle">The duty cycle (0.0 to 1.0).</param>
    /// <returns>An array of square wave values.</returns>
    public static ushort[] GenerateSquareWave(int length, ushort highValue, ushort lowValue, double dutyCycle)
    {
        var result = new ushort[length];
        var switchPoint = (int)(length * dutyCycle);

        for (var i = 0; i < length; i++)
        {
            result[i] = (i % length) < switchPoint ? highValue : lowValue;
        }

        return result;
    }

    /// <summary>Generates sawtooth wave pattern data.</summary>
    /// <param name="length">The number of data points.</param>
    /// <param name="maxValue">The maximum value.</param>
    /// <param name="minValue">The minimum value.</param>
    /// <returns>An array of sawtooth wave values.</returns>
    public static ushort[] GenerateSawtoothWave(int length, ushort maxValue, ushort minValue)
    {
        var result = new ushort[length];
        var range = maxValue - minValue;

        for (var i = 0; i < length; i++)
        {
            // Use floating point for accurate calculation, then round
            var value = minValue + (range * (double)i / (length - 1));
            result[i] = (ushort)Math.Round(value);
        }

        return result;
    }

    /// <summary>Starts the simulation with the specified interval.</summary>
    /// <param name="dataStore">The data store to update.</param>
    /// <param name="interval">The update interval.</param>
    /// <param name="simulationType">The type of simulation to run.</param>
    public void Start(DataStore dataStore, TimeSpan interval, SimulationType simulationType)
    {
        if (dataStore is null)
        {
            throw new ArgumentNullException(nameof(dataStore));
        }

        if (_isRunning.Value)
        {
            return;
        }

        UpdateData(dataStore, simulationType);
        _timer = Observable.Interval(interval);

        _disposables.Add(_timer.Subscribe(_ => UpdateData(dataStore, simulationType)));
        _isRunning.OnNext(true);
    }

    /// <summary>Stops the simulation.</summary>
    public void Stop()
    {
        if (!_isRunning.Value)
        {
            return;
        }

        _isRunning.OnNext(false);
        _timer = null;
    }

    /// <summary>Generates random data within specified bounds.</summary>
    /// <param name="length">The number of data points.</param>
    /// <param name="minValue">The minimum value.</param>
    /// <param name="maxValue">The maximum value.</param>
    /// <returns>An array of random values.</returns>
    public ushort[] GenerateRandomData(int length, ushort minValue, ushort maxValue)
    {
        var result = new ushort[length];
        for (var i = 0; i < length; i++)
        {
            result[i] = (ushort)GetRandomInt32(minValue, maxValue + 1);
        }

        return result;
    }

    /// <summary>Generates boolean pattern for discrete values.</summary>
    /// <param name="length">The number of data points.</param>
    /// <param name="pattern">The pattern type.</param>
    /// <returns>An array of boolean values.</returns>
    public bool[] GenerateBooleanPattern(int length, BooleanPattern pattern)
    {
        var result = new bool[length];

        switch (pattern)
        {
            case BooleanPattern.AllTrue:
                {
                    for (var i = 0; i < length; i++)
                    {
                        result[i] = true;
                    }

                    break;
                }

            case BooleanPattern.AllFalse:
                break;

            case BooleanPattern.Alternating:
                {
                    for (var i = 0; i < length; i++)
                    {
                        result[i] = i % Two == 0;
                    }

                    break;
                }

            case BooleanPattern.Random:
                {
                    for (var i = 0; i < length; i++)
                    {
                        result[i] = GetRandomBoolean();
                    }

                    break;
                }
        }

        return result;
    }

    /// <summary>Loads predefined test patterns into a data store.</summary>
    /// <param name="dataStore">The data store to populate.</param>
    /// <param name="pattern">The pattern type to load.</param>
    public void LoadTestPattern(DataStore dataStore, TestPattern pattern)
    {
        const int dataLength = 1000;
        if (dataStore is null)
        {
            throw new ArgumentNullException(nameof(dataStore), "Data store cannot be null.");
        }

        dataStore.Lock.EnterWriteLock();
        try
        {
            switch (pattern)
            {
                case TestPattern.CountingUp:
                    {
                        LoadCountingPattern(dataStore, dataLength, true);
                        break;
                    }

                case TestPattern.CountingDown:
                    {
                        LoadCountingPattern(dataStore, dataLength, false);
                        break;
                    }

                case TestPattern.SineWave:
                    {
                        LoadSineWavePattern(dataStore, dataLength);
                        break;
                    }

                case TestPattern.SquareWave:
                    {
                        LoadSquareWavePattern(dataStore, dataLength);
                        break;
                    }

                case TestPattern.Random:
                    {
                        LoadRandomPattern(dataStore, dataLength);
                        break;
                    }

                case TestPattern.AllZeros:
                    {
                        LoadConstantPattern(dataStore, dataLength, 0, false);
                        break;
                    }

                case TestPattern.AllOnes:
                    {
                        LoadConstantPattern(dataStore, dataLength, UShortMaximum, true);
                        break;
                    }
            }
        }
        finally
        {
            dataStore.Lock.ExitWriteLock();
        }
    }

    /// <summary>Disposes the simulation data provider.</summary>
    public void Dispose()
    {
        Stop();
        _disposables.Dispose();
        _isRunning.Dispose();
        _randomNumberGenerator.Dispose();
    }

    /// <summary>Executes the Update Counting Data operation.</summary>
    /// <param name="dataStore">The data Store value.</param>
    /// <param name="count">The count value.</param>
    /// <param name="countUp">The count Up value.</param>
    private static void UpdateCountingData(DataStore dataStore, int count, bool countUp)
    {
        for (var i = 1; i < Math.Min(count + 1, dataStore.HoldingRegisters.Count); i++)
        {
            var currentValue = dataStore.HoldingRegisters[i];
            dataStore.HoldingRegisters[i] = countUp
                ? (ushort)((currentValue + 1) % UShortRange)
                : (ushort)((currentValue == 0) ? UShortMaximum : currentValue - 1);

            dataStore.InputRegisters[i] = dataStore.HoldingRegisters[i];
            dataStore.CoilDiscretes[i] = (dataStore.HoldingRegisters[i] % Two) == 1;
            dataStore.InputDiscretes[i] = !dataStore.CoilDiscretes[i];
        }
    }

    /// <summary>Executes the Update Sine Wave Data operation.</summary>
    /// <param name="dataStore">The data Store value.</param>
    /// <param name="count">The count value.</param>
    /// <param name="timeProvider">The time provider value.</param>
    private static void UpdateSineWaveData(DataStore dataStore, int count, TimeProvider timeProvider)
    {
        var time = timeProvider.GetLocalNow().LocalDateTime.Millisecond / OneThousandDouble;

        for (var i = 1; i < Math.Min(count + 1, dataStore.HoldingRegisters.Count); i++)
        {
            var value = (Int16Maximum * Math.Sin((Two * Math.PI * OneTenth * time) + (i * OneTenth))) + Int16Maximum;
            dataStore.HoldingRegisters[i] = (ushort)Math.Max(0, Math.Min(UShortMaximum, value));
            dataStore.InputRegisters[i] = dataStore.HoldingRegisters[i];
            dataStore.CoilDiscretes[i] = dataStore.HoldingRegisters[i] > Int16Maximum;
            dataStore.InputDiscretes[i] = !dataStore.CoilDiscretes[i];
        }
    }

    /// <summary>Executes the Update Square Wave Data operation.</summary>
    /// <param name="dataStore">The data Store value.</param>
    /// <param name="count">The count value.</param>
    /// <param name="timeProvider">The time provider value.</param>
    private static void UpdateSquareWaveData(DataStore dataStore, int count, TimeProvider timeProvider)
    {
        var time = timeProvider.GetLocalNow().LocalDateTime.Second;
        var isHigh = (time % Four) < Two;

        for (var i = 1; i < Math.Min(count + 1, dataStore.HoldingRegisters.Count); i++)
        {
            dataStore.HoldingRegisters[i] = isHigh ? UShortMaximum : (ushort)0;
            dataStore.InputRegisters[i] = dataStore.HoldingRegisters[i];
            dataStore.CoilDiscretes[i] = isHigh;
            dataStore.InputDiscretes[i] = !isHigh;
        }
    }

    /// <summary>Executes the Load Counting Pattern operation.</summary>
    /// <param name="dataStore">The data Store value.</param>
    /// <param name="length">The length value.</param>
    /// <param name="countUp">The count Up value.</param>
    private static void LoadCountingPattern(DataStore dataStore, int length, bool countUp)
    {
        for (var i = 1; i < Math.Min(length + 1, dataStore.HoldingRegisters.Count); i++)
        {
            var value = countUp ? (i - 1) : (length - i);
            dataStore.HoldingRegisters[i] = (ushort)(value % UShortRange);
            dataStore.InputRegisters[i] = dataStore.HoldingRegisters[i];
            dataStore.CoilDiscretes[i] = (value % Two) == 1;
            dataStore.InputDiscretes[i] = !dataStore.CoilDiscretes[i];
        }
    }

    /// <summary>Executes the Load Sine Wave Pattern operation.</summary>
    /// <param name="dataStore">The data Store value.</param>
    /// <param name="length">The length value.</param>
    private static void LoadSineWavePattern(DataStore dataStore, int length)
    {
        var sineData = GenerateSineWave(length, Int16Maximum, 1.0, 0.0);
        for (var i = 1; i < Math.Min(length + 1, dataStore.HoldingRegisters.Count); i++)
        {
            dataStore.HoldingRegisters[i] = sineData[i - 1]; // Offset for 1-based indexing
            dataStore.InputRegisters[i] = sineData[i - 1];
            dataStore.CoilDiscretes[i] = sineData[i - 1] > Int16Maximum;
            dataStore.InputDiscretes[i] = !dataStore.CoilDiscretes[i];
        }
    }

    /// <summary>Executes the Load Square Wave Pattern operation.</summary>
    /// <param name="dataStore">The data Store value.</param>
    /// <param name="length">The length value.</param>
    private static void LoadSquareWavePattern(DataStore dataStore, int length)
    {
        var squareData = GenerateSquareWave(length, UShortMaximum, 0, OneHalf);
        for (var i = 1; i < Math.Min(length + 1, dataStore.HoldingRegisters.Count); i++)
        {
            dataStore.HoldingRegisters[i] = squareData[i - 1]; // Offset for 1-based indexing
            dataStore.InputRegisters[i] = squareData[i - 1];
            dataStore.CoilDiscretes[i] = squareData[i - 1] > 0;
            dataStore.InputDiscretes[i] = !dataStore.CoilDiscretes[i];
        }
    }

    /// <summary>Executes the Load Constant Pattern operation.</summary>
    /// <param name="dataStore">The data Store value.</param>
    /// <param name="length">The length value.</param>
    /// <param name="value">The value.</param>
    /// <param name="boolValue">The bool value.</param>
    private static void LoadConstantPattern(DataStore dataStore, int length, ushort value, bool boolValue)
    {
        for (var i = 1; i < Math.Min(length + 1, dataStore.HoldingRegisters.Count); i++)
        {
            dataStore.HoldingRegisters[i] = value;
            dataStore.InputRegisters[i] = value;
            dataStore.CoilDiscretes[i] = boolValue;
            dataStore.InputDiscretes[i] = !boolValue;
        }
    }

    /// <summary>Executes the Update Data operation.</summary>
    /// <param name="dataStore">The data Store value.</param>
    /// <param name="simulationType">The simulation Type value.</param>
    private void UpdateData(DataStore dataStore, SimulationType simulationType)
    {
        const int updateSize = 100;

        dataStore.Lock.EnterWriteLock();
        try
        {
            switch (simulationType)
            {
                case SimulationType.Random:
                    {
                        UpdateRandomData(dataStore, updateSize);
                        break;
                    }

                case SimulationType.CountingUp:
                    {
                        UpdateCountingData(dataStore, updateSize, true);
                        break;
                    }

                case SimulationType.CountingDown:
                    {
                        UpdateCountingData(dataStore, updateSize, false);
                        break;
                    }

                case SimulationType.SineWave:
                    {
                        UpdateSineWaveData(dataStore, updateSize, _timeProvider);
                        break;
                    }

                case SimulationType.SquareWave:
                    {
                        UpdateSquareWaveData(dataStore, updateSize, _timeProvider);
                        break;
                    }
            }
        }
        finally
        {
            dataStore.Lock.ExitWriteLock();
        }
    }

    /// <summary>Executes the Update Random Data operation.</summary>
    /// <param name="dataStore">The data Store value.</param>
    /// <param name="count">The count value.</param>
    private void UpdateRandomData(DataStore dataStore, int count)
    {
        for (var i = 1; i < Math.Min(count + 1, dataStore.HoldingRegisters.Count); i++)
        {
            dataStore.HoldingRegisters[i] = (ushort)GetRandomInt32(UShortRange);
            dataStore.InputRegisters[i] = (ushort)GetRandomInt32(UShortRange);
            dataStore.CoilDiscretes[i] = GetRandomBoolean();
            dataStore.InputDiscretes[i] = GetRandomBoolean();
        }
    }

    /// <summary>Executes the Load Random Pattern operation.</summary>
    /// <param name="dataStore">The data Store value.</param>
    /// <param name="length">The length value.</param>
    private void LoadRandomPattern(DataStore dataStore, int length)
    {
        var randomData = GenerateRandomData(length, 0, UShortMaximum);
        var boolData = GenerateBooleanPattern(length, BooleanPattern.Random);

        for (var i = 1; i < Math.Min(length + 1, dataStore.HoldingRegisters.Count); i++)
        {
            dataStore.HoldingRegisters[i] = randomData[i - 1]; // Offset for 1-based indexing
            dataStore.InputRegisters[i] = randomData[i - 1];
            dataStore.CoilDiscretes[i] = boolData[i - 1];
            dataStore.InputDiscretes[i] = !boolData[i - 1];
        }
    }

    /// <summary>Executes the Get Random Boolean operation.</summary>
    /// <returns>The result.</returns>
    private bool GetRandomBoolean() => GetRandomInt32(Two) == 1;

    /// <summary>Executes the Get Random Int32 operation.</summary>
    /// <param name="maxExclusive">The max Exclusive value.</param>
    /// <returns>The result.</returns>
    private int GetRandomInt32(int maxExclusive) => GetRandomInt32(0, maxExclusive);

    /// <summary>Executes the Get Random Int32 operation.</summary>
    /// <param name="minInclusive">The min Inclusive value.</param>
    /// <param name="maxExclusive">The max Exclusive value.</param>
    /// <returns>The result.</returns>
    private int GetRandomInt32(int minInclusive, int maxExclusive)
    {
        if (minInclusive >= maxExclusive)
        {
            return minInclusive;
        }

        var range = (uint)(maxExclusive - minInclusive);
        var limit = uint.MaxValue - (uint.MaxValue % range);
        var bytes = new byte[sizeof(uint)];
        uint value;

        do
        {
            _randomNumberGenerator.GetBytes(bytes);
            value = BitConverter.ToUInt32(bytes, 0);
        }
        while (value >= limit);

        return minInclusive + (int)(value % range);
    }
}
