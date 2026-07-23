// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using IoT.DriverCore.S7PlcRx.Advanced;
using IoT.DriverCore.S7PlcRx.Enums;
using IoT.DriverCore.S7PlcRx.Mock;

namespace IoT.DriverCore.S7PlcRx.Tests;

/// <summary>Tests the async extension surface added on top of <see cref="IRxS7"/>.</summary>
[NotInParallel]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public class S7PlcRxAsyncExtensionsTests
{
    /// <summary>Gets the first database word address used by the tests.</summary>
    private const string DatabaseWordZeroAddress = "DB1.DBW0";

    /// <summary>Gets the second database word address used by the tests.</summary>
    private const string DatabaseWordTwoAddress = "DB1.DBW2";

    /// <summary>Gets the database size used by the mock server.</summary>
    private const int DatabaseSize = 16;

    /// <summary>Gets the PLC rack number used by the tests.</summary>
    private const short RackNumber = 0;

    /// <summary>Gets the PLC slot number used by the tests.</summary>
    private const short SlotNumber = 1;

    /// <summary>Gets the number representing no reads.</summary>
    private const int NoReads = 0;

    /// <summary>Gets the number representing one read.</summary>
    private const int OneRead = 1;

    /// <summary>Gets the number representing two reads.</summary>
    private const int TwoReads = 2;

    /// <summary>Gets the connection timeout in seconds.</summary>
    private const int ConnectionTimeoutSeconds = 10;

    /// <summary>Gets the write propagation delay in milliseconds.</summary>
    private const int WritePropagationDelayMilliseconds = 100;

    /// <summary>Gets the initial database offset.</summary>
    private const int InitialDatabaseOffset = 0;

    /// <summary>Gets the cached value used by the single-value read test.</summary>
    private const ushort CachedValue = 42;

    /// <summary>Gets the live value used by the single-value read test.</summary>
    private const ushort LiveValue = 7;

    /// <summary>Gets the first cached batch value.</summary>
    private const ushort FirstCachedBatchValue = 1;

    /// <summary>Gets the second cached batch value.</summary>
    private const ushort SecondCachedBatchValue = 2;

    /// <summary>Gets the first asynchronous batch value.</summary>
    private const ushort FirstAsyncBatchValue = 11;

    /// <summary>Gets the second asynchronous batch value.</summary>
    private const ushort SecondAsyncBatchValue = 22;

    /// <summary>Gets the deferred batch value.</summary>
    private const ushort DeferredBatchValue = 5;

    /// <summary>Gets the first value read from the mock PLC.</summary>
    private const ushort FirstServerValue = 100;

    /// <summary>Gets the second value read from the mock PLC.</summary>
    private const ushort SecondServerValue = 200;

    /// <summary>Gets the incorrect runtime cached value.</summary>
    private const int IncorrectRuntimeValue = 99;

    /// <summary>Gets the fallback read value.</summary>
    private const ushort FallbackReadValue = 77;

    /// <summary>Gets the first fallback write value.</summary>
    private const ushort FirstWriteValue = 10;

    /// <summary>Gets the second fallback write value.</summary>
    private const ushort SecondWriteValue = 20;

    /// <summary>Gets the first server write value.</summary>
    private const ushort FirstServerWriteValue = 321;

    /// <summary>Gets the second server write value.</summary>
    private const ushort SecondServerWriteValue = 654;

    /// <summary>Gets the value used by the value-batch read test.</summary>
    private const ushort ValueBatchReadValue = 9;

    /// <summary>Gets the value used by the value-batch write test.</summary>
    private const ushort ValueBatchWriteValue = 12;

#if NET8_0_OR_GREATER
    /// <summary>Gets the value emitted by the single-value observable test.</summary>
    private const ushort ObservedValue = 123;

    /// <summary>Gets the value emitted by the batch observable test.</summary>
    private const ushort ObservedBatchValue = 10;

    /// <summary>Gets the timeout used by observable tests.</summary>
    private static readonly TimeSpan ObservableTimeout = TimeSpan.FromSeconds(TwoReads);
#endif

    /// <summary>Gets the debugger display text.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => GetType().Name;

    /// <summary>Verifies cached reads complete synchronously.</summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task ReadValueAsync_WhenCachedValueExists_CompletesSynchronouslyAsync()
    {
        _ = DebuggerDisplay;
        using var plc = new TestPlc();
        plc.TagList.Add(new Tag("Cached", DatabaseWordZeroAddress, typeof(ushort)) { Value = CachedValue });

        var valueTask = AsyncExtensions.ReadValueAsync(plc, CachedValue, "Cached", CancellationToken.None);

        Assert.That(valueTask.IsCompleted, Is.True);
        Assert.That(await valueTask.AsTask().ConfigureAwait(false), Is.EqualTo(CachedValue));
        Assert.That(plc.SyncReadCount, Is.EqualTo(NoReads));
    }

    /// <summary>Verifies canceled reads surface an operation canceled exception.</summary>
    [Test]
    public void ReadValueAsync_WhenCanceled_ThrowsOperationCanceledException()
    {
        _ = DebuggerDisplay;
        using var plc = new TestPlc();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.That(
            async () => await AsyncExtensions.ReadValueAsync(plc, CachedValue, "Canceled", cts.Token)
                .AsTask()
                .ConfigureAwait(false),
            Throws.InstanceOf<OperationCanceledException>());
    }

    /// <summary>Verifies uncached reads use the underlying task-based read path.</summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task ReadValueAsync_WhenCacheMissing_UsesTaskReadPathAsync()
    {
        _ = DebuggerDisplay;
        using var plc = new TestPlc();
        plc.SetSyncValue("Live", LiveValue);

        var value = await AsyncExtensions.ReadValueAsync(plc, LiveValue, "Live", CancellationToken.None)
            .AsTask()
            .ConfigureAwait(false);

        Assert.That(value, Is.EqualTo(LiveValue));
        Assert.That(plc.SyncReadCount, Is.EqualTo(OneRead));
    }

    /// <summary>Verifies empty batch reads return an empty dictionary.</summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task ReadValuesAsync_WhenVariablesEmpty_ReturnsEmptyDictionaryAsync()
    {
        _ = DebuggerDisplay;
        using var plc = new TestPlc();

        var values = await AsyncExtensions.ReadValuesAsync(plc, CachedValue, [], CancellationToken.None)
            .AsTask()
            .ConfigureAwait(false);

        Assert.That(values, Is.EmptyValue);
    }

    /// <summary>Verifies cached batch reads complete synchronously.</summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task ReadValuesAsync_WhenCachedValuesExist_CompletesSynchronouslyAsync()
    {
        _ = DebuggerDisplay;
        using var plc = new TestPlc();
        plc.TagList.Add(new Tag("A", DatabaseWordZeroAddress, typeof(ushort)) { Value = FirstCachedBatchValue });
        plc.TagList.Add(new Tag("B", DatabaseWordTwoAddress, typeof(ushort)) { Value = SecondCachedBatchValue });

        var valueTask = AsyncExtensions.ReadValuesAsync(
            plc,
            CachedValue,
            ["A", "B"],
            CancellationToken.None);
        var values = await valueTask.AsTask().ConfigureAwait(false);

        Assert.That(valueTask.IsCompleted, Is.True);
        Assert.That(values["A"], Is.EqualTo(FirstCachedBatchValue));
        Assert.That(values["B"], Is.EqualTo(SecondCachedBatchValue));
        Assert.That(plc.SyncReadCount, Is.EqualTo(NoReads));
    }

    /// <summary>Verifies cancellable batch reads use the async read path.</summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task ReadValuesAsync_WhenCancellationTokenCanBeUsed_UsesAsyncReadPathAsync()
    {
        _ = DebuggerDisplay;
        using var plc = new TestPlc();
        plc.SetAsyncValue("A", FirstAsyncBatchValue);
        plc.SetAsyncValue("B", SecondAsyncBatchValue);
        using var cts = new CancellationTokenSource();

        var values = await AsyncExtensions.ReadValuesAsync(plc, CachedValue, ["A", "B"], cts.Token)
            .AsTask()
            .ConfigureAwait(false);

        Assert.That(values["A"], Is.EqualTo(FirstAsyncBatchValue));
        Assert.That(values["B"], Is.EqualTo(SecondAsyncBatchValue));
        Assert.That(plc.AsyncReadCount, Is.EqualTo(TwoReads));
    }

    /// <summary>Verifies deferred async batch reads are awaited to completion.</summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task ReadValuesAsync_WhenAsyncReadsAreDeferred_AwaitsCompletionAsync()
    {
        _ = DebuggerDisplay;
        using var plc = new TestPlc();
        var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        plc.SetAsyncFactory(
            "A",
            async cancellationToken =>
            {
                var completedTask = await Task.WhenAny(
                        completion.Task,
                        Task.Delay(Timeout.Infinite, cancellationToken))
                    .ConfigureAwait(false);
                if (completedTask != completion.Task)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                return await completion.Task.ConfigureAwait(false);
            });

        using var cts = new CancellationTokenSource();
        var valueTask = AsyncExtensions.ReadValuesAsync(plc, CachedValue, ["A"], cts.Token);

        Assert.That(valueTask.IsCompleted, Is.False);

        _ = completion.TrySetResult(DeferredBatchValue);
        var values = await valueTask.AsTask().ConfigureAwait(false);

        Assert.That(values["A"], Is.EqualTo(DeferredBatchValue));
    }

    /// <summary>Verifies the optimized RxS7 multi-variable read path is used when available.</summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task ReadValuesAsync_WhenRxMultiVarCanBeUsed_ReturnsExpectedValuesAsync()
    {
        _ = DebuggerDisplay;
        using var server = new MockServer { DefaultDb1Size = DatabaseSize };
        Assert.That(server.Start(), Is.EqualTo(NoReads));

        BinaryPrimitives.WriteUInt16BigEndian(
            server.DefaultDb1!.AsSpan(InitialDatabaseOffset, sizeof(ushort)),
            FirstServerValue);
        BinaryPrimitives.WriteUInt16BigEndian(
            server.DefaultDb1.AsSpan(sizeof(ushort), sizeof(ushort)),
            SecondServerValue);

        using var plc = new RxS7(new(new(CpuType.S71500, MockServer.Localhost, RackNumber, SlotNumber)));
        _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), "A", DatabaseWordZeroAddress).SetPolling(false);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), "B", DatabaseWordTwoAddress).SetPolling(false);

        await plc.IsConnected
            .Where(connected => connected)
            .Timeout(TimeSpan.FromSeconds(ConnectionTimeoutSeconds))
            .FirstAsync()
            .ConfigureAwait(false);
        var values = await AsyncExtensions.ReadValuesAsync(plc, CachedValue, ["A", "B"], CancellationToken.None)
            .AsTask()
            .ConfigureAwait(false);

        Assert.That(values["A"], Is.EqualTo(FirstServerValue));
        Assert.That(values["B"], Is.EqualTo(SecondServerValue));
    }

    /// <summary>Verifies cached values with incorrect runtime types fall back to a read.</summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task ReadValueAsync_WhenCachedRuntimeTypeMismatches_FallsBackToReadAsync()
    {
        _ = DebuggerDisplay;
        using var plc = new TestPlc();
        plc.TagList.Add(new Tag("A", DatabaseWordZeroAddress, typeof(ushort)) { Value = IncorrectRuntimeValue });
        plc.SetSyncValue("A", FallbackReadValue);

        var value = await AsyncExtensions.ReadValueAsync(plc, CachedValue, "A", CancellationToken.None)
            .AsTask()
            .ConfigureAwait(false);

        Assert.That(value, Is.EqualTo(FallbackReadValue));
        Assert.That(plc.SyncReadCount, Is.EqualTo(OneRead));
    }

    /// <summary>Verifies blank variable names are rejected.</summary>
    [Test]
    public void ReadValuesAsync_WhenVariableNameIsBlank_ThrowsArgumentNullException()
    {
        _ = DebuggerDisplay;
        using var plc = new TestPlc();

        Assert.That(
            async () => await AsyncExtensions.ReadValuesAsync(plc, CachedValue, [string.Empty], CancellationToken.None)
                .AsTask()
                .ConfigureAwait(false),
            Throws.InstanceOf<ArgumentNullException>());
    }

    /// <summary>Verifies fallback batch writes update each requested tag.</summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task WriteValuesAsync_WhenCalled_WritesExpectedValuesAsync()
    {
        _ = DebuggerDisplay;
        using var plc = new TestPlc();
        var values = new Dictionary<string, ushort>
        {
            ["A"] = FirstWriteValue,
            ["B"] = SecondWriteValue,
        };

        var valueTask = AsyncExtensions.WriteValuesAsync(plc, values, CancellationToken.None);
        await valueTask.AsTask().ConfigureAwait(false);

        Assert.That(valueTask.IsCompleted, Is.True);
        Assert.That(plc.WrittenValues["A"], Is.EqualTo(FirstWriteValue));
        Assert.That(plc.WrittenValues["B"], Is.EqualTo(SecondWriteValue));
    }

    /// <summary>Verifies canceled writes stop before dispatch.</summary>
    [Test]
    public void WriteValuesAsync_WhenCanceled_ThrowsOperationCanceledException()
    {
        _ = DebuggerDisplay;
        using var plc = new TestPlc();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.That(
            async () => await AsyncExtensions.WriteValuesAsync(
                    plc,
                    new Dictionary<string, ushort> { ["A"] = FirstWriteValue },
                    cts.Token)
                .AsTask()
                .ConfigureAwait(false),
            Throws.InstanceOf<OperationCanceledException>());
    }

    /// <summary>Verifies the optimized RxS7 multi-variable write path updates DB values.</summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task WriteValuesAsync_WhenRxMultiVarCanBeUsed_WritesExpectedValuesAsync()
    {
        _ = DebuggerDisplay;
        using var server = new MockServer { DefaultDb1Size = DatabaseSize };
        Assert.That(server.Start(), Is.EqualTo(NoReads));

        using var plc = new RxS7(new(new(CpuType.S71500, MockServer.Localhost, RackNumber, SlotNumber)));
        _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), "A", DatabaseWordZeroAddress).SetPolling(false);
        _ = TagOperations.AddUpdateTagItem(plc, typeof(ushort), "B", DatabaseWordTwoAddress).SetPolling(false);

        await plc.IsConnected
            .Where(connected => connected)
            .Timeout(TimeSpan.FromSeconds(ConnectionTimeoutSeconds))
            .FirstAsync()
            .ConfigureAwait(false);
        await AsyncExtensions.WriteValuesAsync(
                plc,
                new Dictionary<string, ushort>
                {
                    ["A"] = FirstServerWriteValue,
                    ["B"] = SecondServerWriteValue,
                },
                CancellationToken.None)
            .AsTask()
            .ConfigureAwait(false);
        await Task.Delay(WritePropagationDelayMilliseconds).ConfigureAwait(false);

        Assert.That(
            BinaryPrimitives.ReadUInt16BigEndian(
                server.DefaultDb1!.AsSpan(InitialDatabaseOffset, sizeof(ushort))),
            Is.EqualTo(FirstServerWriteValue));
        Assert.That(
            BinaryPrimitives.ReadUInt16BigEndian(server.DefaultDb1.AsSpan(sizeof(ushort), sizeof(ushort))),
            Is.EqualTo(SecondServerWriteValue));
    }

    /// <summary>Verifies the existing batch read helper routes through the async read extensions.</summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task ValueBatchAsync_WhenCalled_UsesAsyncReadExtensionsAsync()
    {
        _ = DebuggerDisplay;
        using var plc = new TestPlc();
        plc.TagList.Add(new Tag("A", DatabaseWordZeroAddress, typeof(ushort)) { Value = ValueBatchReadValue });

        var values = await AdvancedExtensions.ValueBatchAsync(plc, CachedValue, "A").ConfigureAwait(false);

        Assert.That(values["A"], Is.EqualTo(ValueBatchReadValue));
    }

    /// <summary>Verifies the existing batch write helper routes through the async write extensions.</summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task ValueBatchWriteAsync_WhenCalled_UsesAsyncWriteExtensionsAsync()
    {
        _ = DebuggerDisplay;
        using var plc = new TestPlc();

        await AdvancedExtensions.ValueBatchAsync(
                plc,
                new Dictionary<string, ushort> { ["A"] = ValueBatchWriteValue })
            .ConfigureAwait(false);

        Assert.That(plc.WrittenValues["A"], Is.EqualTo(ValueBatchWriteValue));
    }

#if NET8_0_OR_GREATER
    /// <summary>Verifies async observable value projections emit tag updates.</summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task ObserveValue_WhenTagChanges_EmitsUpdatedValueAsync()
    {
        _ = DebuggerDisplay;
        using var plc = new TestPlc();
        plc.TagList.Add(new Tag("A", DatabaseWordZeroAddress, typeof(ushort)));

        var completion = new TaskCompletionSource<ushort>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var observer = new DelegatingObserverAsync<ushort>((value, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value == ObservedValue)
            {
                _ = completion.TrySetResult(value);
            }

            return ValueTask.CompletedTask;
        });
        await using var subscription = await AsyncExtensions.ObserveValue(plc, CachedValue, "A")
            .SubscribeAsync(observer, CancellationToken.None)
            .ConfigureAwait(false);

        plc.PublishObservedValue("A", ObservedValue, typeof(ushort));

        var result = await completion.Task.WaitAsync(ObservableTimeout).ConfigureAwait(false);

        Assert.That(result, Is.EqualTo(ObservedValue));
    }

    /// <summary>Verifies async observable single-value wrappers reject blank variable names.</summary>
    [Test]
    public void ObserveValue_WhenVariableBlank_ThrowsArgumentNullException()
    {
        _ = DebuggerDisplay;
        using var plc = new TestPlc();

        _ = Assert.Throws<ArgumentNullException>(
            () => _ = AsyncExtensions.ObserveValue(plc, CachedValue, string.Empty));
    }

    /// <summary>Verifies async observable batch projections emit updated dictionaries.</summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task ObserveValues_WhenTagsChange_EmitsUpdatedDictionaryAsync()
    {
        _ = DebuggerDisplay;
        using var plc = new TestPlc();
        plc.TagList.Add(new Tag("A", DatabaseWordZeroAddress, typeof(ushort)));

        var completion = new TaskCompletionSource<Dictionary<string, ushort>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var observer = new DelegatingObserverAsync<Dictionary<string, ushort>>(
            (values, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (values.TryGetValue("A", out var value) && value == ObservedBatchValue)
                {
                    _ = completion.TrySetResult(values);
                }

                return ValueTask.CompletedTask;
            });
        await using var subscription = await AsyncExtensions.ObserveValues(plc, CachedValue, "A")
            .SubscribeAsync(observer, CancellationToken.None)
            .ConfigureAwait(false);

        plc.PublishObservedValue("A", ObservedBatchValue, typeof(ushort));

        var values = await completion.Task.WaitAsync(ObservableTimeout).ConfigureAwait(false);

        Assert.That(values["A"], Is.EqualTo(ObservedBatchValue));
    }

    /// <summary>Verifies async observable batch wrappers reject empty variable lists.</summary>
    [Test]
    public void ObserveValues_WhenVariablesEmpty_ThrowsArgumentException()
    {
        _ = DebuggerDisplay;
        using var plc = new TestPlc();

        _ = Assert.Throws<ArgumentException>(() => _ = AsyncExtensions.ObserveValues(plc, CachedValue));
    }

    /// <summary>Verifies async observable batch wrappers reject null variable arrays.</summary>
    [Test]
    public void ObserveValues_WhenVariablesNull_ThrowsArgumentNullException()
    {
        _ = DebuggerDisplay;
        using var plc = new TestPlc();

        _ = Assert.Throws<ArgumentNullException>(
            () => _ = AsyncExtensions.ObserveValues(plc, CachedValue, null!));
    }

    /// <summary>Adapts a delegate to an asynchronous observer.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="onNext">The delegate that handles observed values.</param>
    [System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
    public sealed class DelegatingObserverAsync<T>(Func<T, CancellationToken, ValueTask> onNext) : IObserverAsync<T>
    {
        /// <summary>Gets the debugger display text.</summary>
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => GetType().Name;

        /// <summary>Releases resources held by this observer.</summary>
        /// <returns>A completed task.</returns>
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        /// <summary>Handles completion of the observed sequence.</summary>
        /// <param name="result">The completion result.</param>
        /// <returns>A completed task.</returns>
        public ValueTask OnCompletedAsync(Result result) => ValueTask.CompletedTask;

        /// <summary>Propagates an observed error.</summary>
        /// <param name="error">The observed error.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>This method does not return because it throws the error.</returns>
        public ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken) =>
            ValueTask.FromException(error);

        /// <summary>Forwards the next observed value to the wrapped delegate.</summary>
        /// <param name="value">The observed value.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the delegate invocation.</returns>
        public ValueTask OnNextAsync(T value, CancellationToken cancellationToken) => onNext(value, cancellationToken);
    }
#endif

    /// <summary>Provides an in-memory <see cref="IRxS7"/> implementation for these tests.</summary>
    [System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
    public sealed class TestPlc : IRxS7
    {
        /// <summary>Gets values returned from cancellable reads.</summary>
        public Dictionary<string, object?> AsyncValues { get; } = new(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>Gets factories used to defer cancellable reads.</summary>
        public Dictionary<string, Func<CancellationToken, Task<object?>>> AsyncValueFactories { get; } =
            new(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>Gets the signal that publishes tag updates to observers.</summary>
        public Signal<Tag?> ObserveAllSubject { get; } = new();

        /// <summary>Gets values returned from non-cancellable reads.</summary>
        public Dictionary<string, object?> SyncValues { get; } = new(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>Gets the number of cancellable reads performed.</summary>
        public int AsyncReadCount { get; private set; }

        /// <summary>Gets the number of non-cancellable reads performed.</summary>
        public int SyncReadCount { get; private set; }

        /// <summary>Gets the values written by the test subject.</summary>
        public Dictionary<string, object?> WrittenValues { get; } = new(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>Gets the test PLC address.</summary>
        public string IP => MockServer.Localhost;

        /// <summary>Gets an observable that reports a connected PLC.</summary>
        public IObservable<bool> IsConnected => Observable.Return(true);

        /// <summary>Gets a value indicating that the test PLC is connected.</summary>
        public bool IsConnectedValue => true;

        /// <summary>Gets a value indicating whether the test PLC has been disposed.</summary>
        public bool IsDisposed { get; private set; }

        /// <summary>Gets an observable with no errors.</summary>
        public IObservable<string> LastError => Observable.Empty<string>();

        /// <summary>Gets an observable with no error codes.</summary>
        public IObservable<ErrorCode> LastErrorCode => Observable.Empty<ErrorCode>();

        /// <summary>Gets all observed tag updates.</summary>
        public IObservable<Tag?> ObserveAll => ObserveAllSubject.AsObservable();

        /// <summary>Gets the configured PLC CPU type.</summary>
        public CpuType PLCType => CpuType.S71500;

        /// <summary>Gets the configured rack number.</summary>
        public short Rack => RackNumber;

        /// <summary>Gets the configured slot number.</summary>
        public short Slot => SlotNumber;

        /// <summary>Gets an observable that reports the test PLC is not paused.</summary>
        public IObservable<bool> IsPaused => Observable.Return(false);

        /// <summary>Gets an observable with no status messages.</summary>
        public IObservable<string> Status => Observable.Empty<string>();

        /// <summary>Gets the tags registered with the test PLC.</summary>
        public global::IoT.DriverCore.S7PlcRx.Tags TagList { get; } = [];

        /// <summary>Gets or sets a value indicating whether watchdog writes are shown.</summary>
        public bool ShowWatchDogWriting { get; set; }

        /// <summary>Gets the absent watchdog address.</summary>
        public string? WatchDogAddress => null;

        /// <summary>Gets or sets the watchdog value.</summary>
        public ushort WatchDogValueToWrite { get; set; }

        /// <summary>Gets the watchdog write interval.</summary>
        public int WatchDogWritingTime => NoReads;

        /// <summary>Gets an observable with no read-time values.</summary>
        public IObservable<long> ReadTime => Observable.Empty<long>();

        /// <summary>Gets the debugger display text.</summary>
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => GetType().Name;

        /// <summary>Releases resources used by the test PLC.</summary>
        public void Dispose()
        {
            IsDisposed = true;
            ObserveAllSubject.Dispose();
        }

        /// <summary>Observes values for a typed logical tag.</summary>
        /// <typeparam name="T">The tag value type.</typeparam>
        /// <param name="tag">The typed logical tag to observe.</param>
        /// <returns>An observable for matching tag updates.</returns>
        public IObservable<T?> Observe<T>(LogicalTagKey<T> tag) => ObserveAllSubject
            .Where(observedTag =>
                string.Equals(observedTag?.Name, tag.Name, StringComparison.InvariantCultureIgnoreCase))
            .Where(observedTag => observedTag?.Value is T)
            .Select(observedTag => (T?)observedTag!.Value);

        /// <summary>Reads a value using the typed logical tag overload.</summary>
        /// <typeparam name="T">The expected value type.</typeparam>
        /// <param name="tag">The typed logical tag to read.</param>
        /// <returns>A task containing the configured value.</returns>
        public Task<T?> ReadAsync<T>(LogicalTagKey<T> tag)
        {
            SyncReadCount++;
            return Task.FromResult(
                SyncValues.TryGetValue(tag.Name, out var value) && value is T typed ? typed : default);
        }

        /// <summary>Reads a value using the cancellable typed logical tag overload.</summary>
        /// <typeparam name="T">The expected value type.</typeparam>
        /// <param name="tag">The typed logical tag to read.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task containing the configured value.</returns>
        public async Task<T?> ReadAsync<T>(LogicalTagKey<T> tag, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AsyncReadCount++;

            if (AsyncValueFactories.TryGetValue(tag.Name, out var factory))
            {
                var factoryValue = await factory(cancellationToken).ConfigureAwait(false);
                return factoryValue is T factoryTyped ? factoryTyped : default;
            }

            return AsyncValues.TryGetValue(tag.Name, out var value) && value is T typed ? typed : default;
        }

        /// <summary>Writes a value to the in-memory PLC.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="variable">The variable name.</param>
        /// <param name="value">The value to write.</param>
        public void Value<T>(string? variable, T? value) => WrittenValues[variable!] = value;

        /// <summary>Gets an observable with no CPU information.</summary>
        /// <returns>An observable with an empty CPU-information array.</returns>
        public IObservable<string[]> GetCpuInfo() => Observable.Return<string[]>([]);

        /// <summary>Registers a deferred value factory.</summary>
        /// <param name="variable">The variable name.</param>
        /// <param name="factory">The deferred value factory.</param>
        public void SetAsyncFactory(string variable, Func<CancellationToken, Task<object?>> factory) =>
            AsyncValueFactories[variable] = factory;

        /// <summary>Registers a value returned by cancellable reads.</summary>
        /// <param name="variable">The variable name.</param>
        /// <param name="value">The value to return.</param>
        public void SetAsyncValue(string variable, object value) => AsyncValues[variable] = value;

        /// <summary>Registers a value returned by non-cancellable reads.</summary>
        /// <param name="variable">The variable name.</param>
        /// <param name="value">The value to return.</param>
        public void SetSyncValue(string variable, object value) => SyncValues[variable] = value;

        /// <summary>Publishes a tag update to observers.</summary>
        /// <param name="variable">The variable name.</param>
        /// <param name="value">The updated value.</param>
        /// <param name="type">The declared tag value type.</param>
        public void PublishObservedValue(string variable, object? value, Type type)
        {
#if NETFRAMEWORK
            if (string.IsNullOrWhiteSpace(variable))
            {
                throw new ArgumentException("The variable name cannot be empty.", nameof(variable));
            }
#else
            ArgumentException.ThrowIfNullOrWhiteSpace(variable);
#endif

            var tag = TagList[variable] ?? new Tag(variable, variable, type);
            tag.Value = value;

            if (TagList[variable] is null)
            {
                TagList.Add(tag);
            }

            ObserveAllSubject.OnNext(tag);
        }
    }
}
