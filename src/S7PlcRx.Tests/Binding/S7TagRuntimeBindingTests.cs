// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.S7PlcRx.Binding;
using IoT.DriverCore.S7PlcRx.Enums;
using IoT.DriverCore.S7PlcRx.PlcTypes;

namespace IoT.DriverCore.S7PlcRx.Tests.Binding;

/// <summary>Tests runtime grouped byte-array PLC binding operations.</summary>
[NotInParallel]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class S7TagRuntimeBindingTests
{
    /// <summary>Defines the temperature tag name.</summary>
    private const string TemperatureTagName = "Temperature";

    /// <summary>Defines the pressure tag name.</summary>
    private const string PressureTagName = "Pressure";

    /// <summary>Defines the coalesced byte-range tag name.</summary>
    private const string CoalescedRangeTagName = "__s7_binding_db1_0_8";

    /// <summary>Defines the size of a floating-point PLC value.</summary>
    private const int FloatByteLength = 4;

    /// <summary>Defines the coalesced byte-range length.</summary>
    private const int CoalescedRangeLength = 8;

    /// <summary>Defines the read polling interval.</summary>
    private const int ReadPollingIntervalMilliseconds = 25;

    /// <summary>Defines the asynchronous wait timeout.</summary>
    private const int WaitTimeoutSeconds = 2;

    /// <summary>Defines the asynchronous wait retry delay.</summary>
    private const int WaitRetryDelayMilliseconds = 20;

    /// <summary>Defines the first expected floating-point value.</summary>
    private const float FirstValue = 1.25F;

    /// <summary>Defines the second expected floating-point value.</summary>
    private const float SecondValue = 2.5F;

    /// <summary>Defines the first written floating-point value.</summary>
    private const float WrittenTemperatureValue = 12.5F;

    /// <summary>Defines the second written floating-point value.</summary>
    private const float WrittenPressureValue = 25.25F;

    /// <summary>Gets the debugger display text.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => ToString() ?? string.Empty;

    /// <summary>Ensures multiple property writes in the same DB are coalesced into one byte-array write.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Write_WithSameDbTags_ShouldCoalesceToSingleByteArrayWriteAsync()
    {
        System.Diagnostics.Debug.WriteLine(DebuggerDisplay);
        using var plc = new RecordingPlc();
        var definitions = new[]
        {
            new S7TagDefinition(
                TemperatureTagName,
                "DB1.DBD0",
                typeof(float),
                0,
                S7TagDirection.WriteOnly,
                1),
            new S7TagDefinition(
                PressureTagName,
                "DB1.DBD4",
                typeof(float),
                0,
                S7TagDirection.WriteOnly,
                1),
        };

        using var binding = S7TagRuntimeBinding.Bind(plc, definitions, (_, _) => { });
        binding.Write(TemperatureTagName, WrittenTemperatureValue);
        binding.Write(PressureTagName, WrittenPressureValue);

        await WaitUntilAsync(() => plc.Writes.Count > 0);

        await TUnit.Assertions.Assert.That(plc.Writes.Count).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(plc.Writes[0].TagName).IsEqualTo(CoalescedRangeTagName);
        await TUnit.Assertions.Assert.That(plc.Writes[0].Bytes.Length).IsEqualTo(CoalescedRangeLength);
    }

    /// <summary>Ensures interval reads for same-DB tags are coalesced into one byte-array read.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ReadInterval_WithSameDbTags_ShouldCoalesceToSingleByteArrayReadAsync()
    {
        System.Diagnostics.Debug.WriteLine(DebuggerDisplay);
        using var plc = new RecordingPlc();
        Real.ToSpan(FirstValue, plc.ReadBuffer.AsSpan(0, FloatByteLength));
        Real.ToSpan(SecondValue, plc.ReadBuffer.AsSpan(FloatByteLength, FloatByteLength));
        var applied = new Dictionary<string, object?>(StringComparer.InvariantCultureIgnoreCase);
        var definitions = new[]
        {
            new S7TagDefinition(
                TemperatureTagName,
                "DB1.DBD0",
                typeof(float),
                ReadPollingIntervalMilliseconds,
                S7TagDirection.ReadOnly,
                1),
            new S7TagDefinition(
                PressureTagName,
                "DB1.DBD4",
                typeof(float),
                ReadPollingIntervalMilliseconds,
                S7TagDirection.ReadOnly,
                1),
        };

        using var binding = S7TagRuntimeBinding.Bind(plc, definitions, (name, value) => applied[name] = value);

        await WaitUntilAsync(() => plc.Reads.Contains(CoalescedRangeTagName, StringComparer.Ordinal) &&
            applied.ContainsKey(TemperatureTagName) &&
            applied.ContainsKey(PressureTagName));

        await TUnit.Assertions.Assert.That(plc.Reads).Contains(CoalescedRangeTagName);
        await TUnit.Assertions.Assert.That(applied[TemperatureTagName]).IsEqualTo(FirstValue);
        await TUnit.Assertions.Assert.That(applied[PressureTagName]).IsEqualTo(SecondValue);
    }

    /// <summary>Waits for the supplied condition until its timeout expires.</summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="timeProvider">The time provider to use for deadline tracking; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <returns>A task that completes when the condition succeeds or the timeout expires.</returns>
    private static async Task WaitUntilAsync(Func<bool> condition, TimeProvider? timeProvider = null)
    {
        var tp = timeProvider ?? TimeProvider.System;
        var timeoutAt = tp.GetUtcNow().UtcDateTime.AddSeconds(WaitTimeoutSeconds);
        while (tp.GetUtcNow().UtcDateTime < timeoutAt)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(WaitRetryDelayMilliseconds);
        }
    }

    /// <summary>Records the PLC operations performed by a runtime binding.</summary>
    private sealed class RecordingPlc : IRxS7
    {
        /// <summary>Gets the recorded byte-array writes.</summary>
        public List<(string TagName, byte[] Bytes)> Writes { get; } = [];

        /// <summary>Gets the recorded byte-array reads.</summary>
        public List<string> Reads { get; } = [];

        /// <summary>Gets the byte buffer supplied by byte-array reads.</summary>
        public byte[] ReadBuffer { get; } = new byte[CoalescedRangeLength];

        /// <summary>Gets the configured IP address.</summary>
        public string IP => "127.0.0.1";

        /// <summary>Gets an observable indicating that the fake PLC is connected.</summary>
        public IObservable<bool> IsConnected => Observable.Return(true);

        /// <summary>Gets a value indicating that the fake PLC is connected.</summary>
        public bool IsConnectedValue => true;

        /// <summary>Gets an empty observable of PLC error messages.</summary>
        public IObservable<string> LastError => Observable.Empty<string>();

        /// <summary>Gets an empty observable of PLC error codes.</summary>
        public IObservable<ErrorCode> LastErrorCode => Observable.Empty<ErrorCode>();

        /// <summary>Gets an empty observable of tag updates.</summary>
        public IObservable<Tag?> ObserveAll => Observable.Empty<Tag?>();

        /// <summary>Gets the PLC CPU type.</summary>
        public CpuType PLCType => CpuType.S71500;

        /// <summary>Gets the PLC rack number.</summary>
        public short Rack => 0;

        /// <summary>Gets the PLC slot number.</summary>
        public short Slot => 1;

        /// <summary>Gets an observable indicating that the fake PLC is not paused.</summary>
        public IObservable<bool> IsPaused => Observable.Return(false);

        /// <summary>Gets an empty observable of PLC status messages.</summary>
        public IObservable<string> Status => Observable.Empty<string>();

        /// <summary>Gets the fake PLC tag collection.</summary>
        public global::IoT.DriverCore.S7PlcRx.Tags TagList { get; } = [];

        /// <summary>Gets or sets a value indicating whether watchdog writes are shown.</summary>
        public bool ShowWatchDogWriting { get; set; }

        /// <summary>Gets the optional watchdog address.</summary>
        public string? WatchDogAddress => null;

        /// <summary>Gets or sets the watchdog value.</summary>
        public ushort WatchDogValueToWrite { get; set; }

        /// <summary>Gets the watchdog write interval.</summary>
        public int WatchDogWritingTime => 0;

        /// <summary>Gets an empty observable of read timings.</summary>
        public IObservable<long> ReadTime => Observable.Empty<long>();

        /// <summary>Gets a value indicating whether the fake PLC has been disposed.</summary>
        public bool IsDisposed { get; private set; }

        /// <summary>Observes the specified logical tag.</summary>
        /// <typeparam name="T">The tag value type.</typeparam>
        /// <param name="tag">The tag to observe.</param>
        /// <returns>An empty observable.</returns>
        public IObservable<T?> Observe<T>(LogicalTagKey<T> tag) => Observable.Empty<T?>();

        /// <summary>Reads the specified logical tag.</summary>
        /// <typeparam name="T">The tag value type.</typeparam>
        /// <param name="tag">The tag to read.</param>
        /// <returns>A task containing the recorded byte buffer when applicable.</returns>
        public Task<T?> ReadAsync<T>(LogicalTagKey<T> tag) => ReadAsync(tag, CancellationToken.None);

        /// <summary>Reads the specified logical tag with cancellation support.</summary>
        /// <typeparam name="T">The tag value type.</typeparam>
        /// <param name="tag">The tag to read.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task containing the recorded byte buffer when applicable.</returns>
        public Task<T?> ReadAsync<T>(LogicalTagKey<T> tag, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (typeof(T) != typeof(byte[]))
            {
                return Task.FromResult(default(T));
            }

            Reads.Add(tag.Name);
            object bytes = ReadBuffer.ToArray();
            return Task.FromResult((T?)bytes);
        }

        /// <summary>Records byte-array writes performed by the runtime binding.</summary>
        /// <typeparam name="T">The written value type.</typeparam>
        /// <param name="variable">The target variable name.</param>
        /// <param name="value">The value to record.</param>
        public void Value<T>(string? variable, T? value)
        {
            if (value is not byte[] bytes || variable is null)
            {
                return;
            }

            Writes.Add((variable, bytes));
        }

        /// <summary>Gets an empty observable of CPU information.</summary>
        /// <returns>An empty observable.</returns>
        public IObservable<string[]> GetCpuInfo() => Observable.Empty<string[]>();

        /// <summary>Marks the fake PLC as disposed.</summary>
        public void Dispose() => IsDisposed = true;
    }
}
