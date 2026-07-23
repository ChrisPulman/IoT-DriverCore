// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
using IoT.DriverCore.OmronPlcRx.Enums;
using IoT.DriverCore.OmronPlcRx.Results;
using IoT.DriverCore.OmronPlcRx.Tags;
using TUnit.Core;

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>Covers grouped FINS validation, conversion, and transport-failure paths.</summary>
public sealed class OmronLogicalBatchCoverageTests
{
    /// <summary>Gets the default fixed-string length.</summary>
    private const int DefaultStringLength = 16;

    /// <summary>Gets a two-word fixed-string length.</summary>
    private const int TwoWordStringLength = 4;

    /// <summary>Gets the expected number of words in a multi-word value.</summary>
    private const int ExpectedTwoWords = 2;

    /// <summary>Gets the third zero-based input index.</summary>
    private const int ThirdInputIndex = 2;

    /// <summary>Gets a deterministic 32-bit value spanning two FINS words.</summary>
    private const int MultiWordValue = 65_538;

    /// <summary>Gets a deterministic word value.</summary>
    private const short WordValue = 42;

    /// <summary>Gets a deterministic second write value.</summary>
    private const short SecondWordValue = 2;

    /// <summary>Gets a deterministic starting address.</summary>
    private const ushort WordAddress = 10;

    /// <summary>Gets a deterministic bit index.</summary>
    private const byte BitIndex = 15;

    /// <summary>Gets the first logical tag name.</summary>
    private const string FirstTagName = "First";

    /// <summary>Gets the second logical tag name.</summary>
    private const string SecondTagName = "Second";

    /// <summary>Verifies supported logical values and every FINS memory-area alias.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Codec_ConvertsSupportedValuesAndMemoryAliases()
    {
        var trueWords = OmronLogicalBatchCodec.GetWriteWords(
            NewItem(0, "BoolTrue", "D1", typeof(bool), true),
            0);
        var falseWords = OmronLogicalBatchCodec.GetWriteWords(
            NewItem(1, "BoolFalse", "D2", typeof(bool), false),
            0);
        var shortWords = OmronLogicalBatchCodec.GetWriteWords(
            NewItem(0, "Short", "D3", typeof(short), WordValue),
            0);
        var intWords = OmronLogicalBatchCodec.GetWriteWords(
            NewItem(0, "Integer", "D4", typeof(int), MultiWordValue),
            0);
        var stringWords = OmronLogicalBatchCodec.GetWriteWords(
            NewItem(0, "Text", "D6[4]", typeof(string), "AB"),
            TwoWordStringLength);

        await Assert.That(OmronLogicalBatchCodec.GetReadWordCount(typeof(bool), 0)).IsEqualTo(1);
        await Assert.That(
            OmronLogicalBatchCodec.GetReadWordCount(typeof(string), TwoWordStringLength))
            .IsEqualTo(ExpectedTwoWords);
        await Assert.That(trueWords).IsEquivalentTo([(short)1]);
        await Assert.That(falseWords).IsEquivalentTo([(short)0]);
        await Assert.That(shortWords).IsEquivalentTo([WordValue]);
        await Assert.That(intWords.Length).IsEqualTo(ExpectedTwoWords);
        await Assert.That(stringWords.Length).IsEqualTo(ExpectedTwoWords);
        await Assert.That((bool)OmronLogicalBatchCodec.DecodeWords(typeof(bool), 0, 1, trueWords))
            .IsTrue();
        await Assert.That((bool)OmronLogicalBatchCodec.DecodeWords(typeof(bool), 0, 1, falseWords))
            .IsFalse();
        await Assert.That(
            (int)OmronLogicalBatchCodec.DecodeWords(
                typeof(int),
                0,
                ExpectedTwoWords,
                intWords))
            .IsEqualTo(MultiWordValue);
        await Assert.That(
            (string)OmronLogicalBatchCodec.DecodeWords(
                typeof(string),
                TwoWordStringLength,
                ExpectedTwoWords,
                stringWords))
            .IsEqualTo("AB");
    }

    /// <summary>Verifies address metadata and every supported FINS memory-area alias.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Codec_ParsesAddressMetadataAndMemoryAliases()
    {
        var parsed = OmronLogicalBatchCodec.ParseAddress("dm10.15[4]");
        var stringMeta = OmronLogicalBatchCodec.ExtractStringMeta("D6[4]");
        var defaultMeta = OmronLogicalBatchCodec.ExtractStringMeta("D6");

        await Assert.That(parsed.Area).IsEqualTo("DM");
        await Assert.That(parsed.Address).IsEqualTo(WordAddress);
        await Assert.That(parsed.BitIndex).IsEqualTo(BitIndex);
        await Assert.That(stringMeta.BaseAddress).IsEqualTo("D6");
        await Assert.That(stringMeta.Length).IsEqualTo(TwoWordStringLength);
        await Assert.That(defaultMeta.BaseAddress).IsEqualTo("D6");
        await Assert.That(defaultMeta.Length).IsEqualTo(DefaultStringLength);
        await Assert.That(GetWordAliases()).IsEquivalentTo(
        [
            MemoryWordDataType.DataMemory,
            MemoryWordDataType.DataMemory,
            MemoryWordDataType.CommonIO,
            MemoryWordDataType.CommonIO,
            MemoryWordDataType.Work,
            MemoryWordDataType.Holding,
            MemoryWordDataType.Auxiliary,
        ]);
        await Assert.That(GetBitAliases()).IsEquivalentTo(
        [
            MemoryBitDataType.DataMemory,
            MemoryBitDataType.DataMemory,
            MemoryBitDataType.CommonIO,
            MemoryBitDataType.CommonIO,
            MemoryBitDataType.Work,
            MemoryBitDataType.Holding,
            MemoryBitDataType.Auxiliary,
        ]);
    }

    /// <summary>Verifies malformed addresses and unsupported values fail explicitly.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Codec_RejectsMalformedAddressesAndUnsupportedValues()
    {
        var unclosed = OmronLogicalBatchCodec.ExtractStringMeta("D1[4");
        var zeroLength = OmronLogicalBatchCodec.ExtractStringMeta("D1[0]");
        var textLength = OmronLogicalBatchCodec.ExtractStringMeta("D1[text]");

        await Assert.That(unclosed.BaseAddress).IsEqualTo("D1[4");
        await Assert.That(unclosed.Length).IsEqualTo(DefaultStringLength);
        await Assert.That(zeroLength.BaseAddress).IsEqualTo("D1[0]");
        await Assert.That(zeroLength.Length).IsEqualTo(DefaultStringLength);
        await Assert.That(textLength.BaseAddress).IsEqualTo("D1[text]");
        await Assert.That(textLength.Length).IsEqualTo(DefaultStringLength);
        await Assert.That(
            Throws<FormatException>(() => OmronLogicalBatchCodec.ParseAddress("D1[4")))
            .IsTrue();
        await Assert.That(
            Throws<NotSupportedException>(
                () => OmronLogicalBatchCodec.GetReadWordCount(typeof(DateTime), 0)))
            .IsTrue();
        await Assert.That(
            Throws<NotSupportedException>(
                () => OmronLogicalBatchCodec.GetWriteWords(
                    NewItem(0, "Unsupported", "D1", typeof(DateTime), default(DateTime)),
                    0)))
            .IsTrue();
        await Assert.That(
            Throws<FormatException>(() => OmronLogicalBatchCodec.ParseAddress("DM")))
            .IsTrue();
        await Assert.That(
            Throws<FormatException>(() => OmronLogicalBatchCodec.ParseAddress("D70000")))
            .IsTrue();
        await Assert.That(
            Throws<FormatException>(() => OmronLogicalBatchCodec.ParseAddress("D1.bad")))
            .IsTrue();
        await Assert.That(
            Throws<FormatException>(() => OmronLogicalBatchCodec.ParseAddress("D1.16")))
            .IsTrue();
        await Assert.That(
            Throws<ArgumentOutOfRangeException>(() => OmronLogicalBatchCodec.ToWordType("Z")))
            .IsTrue();
        await Assert.That(
            Throws<ArgumentOutOfRangeException>(() => OmronLogicalBatchCodec.ToBitType("Z")))
            .IsTrue();
    }

    /// <summary>Verifies preparation failures remain isolated and retain caller order.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Executor_RetainsPreparationFailuresAndNullWriteSuccess()
    {
        var memory = new MemoryAreaDouble
        {
            MaximumReadWordCount = 1,
            MaximumWriteWordCount = 1,
            ReadWordsResult = [WordValue],
        };
        var executor = new OmronLogicalBatchExecutor(memory);

        var reads = await executor.ReadManyAsync(
        [
            NewItem(0, "TooWide", "D1[4]", typeof(string), null),
            NewItem(1, "Valid", "D3", typeof(short), null),
            NewItem(ThirdInputIndex, "BadArea", "Z4", typeof(short), null),
        ],
            CancellationToken.None);
        var writes = await executor.WriteManyAsync(
        [
            NewItem(0, "Null", "D5", typeof(short), null),
            NewItem(1, "TooWide", "D6", typeof(int), MultiWordValue),
            NewItem(
                ThirdInputIndex,
                "Unsupported",
                "D8",
                typeof(DateTime),
                default(DateTime)),
        ],
            CancellationToken.None);

        await Assert.That(reads[0].InputIndex).IsEqualTo(0);
        await Assert.That(reads[0].Succeeded).IsFalse();
        await Assert.That(reads[0].Error).Contains("read range limit");
        await Assert.That(reads[1].InputIndex).IsEqualTo(1);
        await Assert.That(reads[1].Succeeded).IsTrue();
        await Assert.That((short)reads[1].Value!).IsEqualTo(WordValue);
        await Assert.That(reads[ThirdInputIndex].InputIndex).IsEqualTo(ThirdInputIndex);
        await Assert.That(reads[ThirdInputIndex].Error).Contains("Unsupported word area");
        await Assert.That(writes[0].InputIndex).IsEqualTo(0);
        await Assert.That(writes[0].Succeeded).IsTrue();
        await Assert.That(writes[0].Value).IsNull();
        await Assert.That(writes[1].Error).Contains("write range limit");
        await Assert.That(writes[ThirdInputIndex].Error).Contains("not supported");
        await Assert.That(memory.ReadWordsCount).IsEqualTo(1);
        await Assert.That(memory.WriteWordsCount).IsEqualTo(0);
    }

    /// <summary>Verifies native word failures are returned for only their affected range.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Executor_ReturnsNativeReadAndWriteFailuresPerRange()
    {
        var memory = new MemoryAreaDouble
        {
            ReadException = new InvalidOperationException("read failed"),
            WriteException = new InvalidOperationException("write failed"),
        };
        var executor = new OmronLogicalBatchExecutor(memory);
        var reads = await executor.ReadManyAsync(
        [
            NewItem(0, FirstTagName, "D10", typeof(short), null),
            NewItem(1, SecondTagName, "D11", typeof(short), null),
        ],
            CancellationToken.None);
        var writes = await executor.WriteManyAsync(
        [
            NewItem(0, FirstTagName, "D10", typeof(short), (short)1),
            NewItem(1, SecondTagName, "D11", typeof(short), SecondWordValue),
        ],
            CancellationToken.None);

        await Assert.That(reads.All(static result => !result.Succeeded)).IsTrue();
        await Assert.That(reads.All(static result => result.Error == "read failed")).IsTrue();
        await Assert.That(writes.All(static result => !result.Succeeded)).IsTrue();
        await Assert.That(writes.All(static result => result.Error == "write failed")).IsTrue();
        await Assert.That(memory.ReadWordsCount).IsEqualTo(1);
        await Assert.That(memory.WriteWordsCount).IsEqualTo(1);
    }

    /// <summary>Verifies caller cancellation is rethrown rather than converted to item failures.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Executor_RethrowsCallerCancellation()
    {
        var memory = new MemoryAreaDouble();
        var executor = new OmronLogicalBatchExecutor(memory);
        using var source = new CancellationTokenSource();
        source.Cancel();
        var token = source.Token;

        await Assert.That(
            await ThrowsAsync<OperationCanceledException>(
                () => executor.ReadManyAsync(
                    [NewItem(0, "Read", "D1", typeof(short), null)],
                    token)))
            .IsTrue();
        await Assert.That(
            await ThrowsAsync<OperationCanceledException>(
                () => executor.WriteManyAsync(
                    [NewItem(0, "Write", "D1", typeof(short), WordValue)],
                    token)))
            .IsTrue();
    }

    /// <summary>Verifies null executor arguments are rejected at their API boundary.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Executor_RejectsNullDependenciesAndCollections()
    {
        var executor = new OmronLogicalBatchExecutor(new MemoryAreaDouble());

        await Assert.That(
            Throws<ArgumentNullException>(() => _ = new OmronLogicalBatchExecutor(null!)))
            .IsTrue();
        await Assert.That(
            await ThrowsAsync<ArgumentNullException>(
                () => executor.ReadManyAsync(null!, CancellationToken.None)))
            .IsTrue();
        await Assert.That(
            await ThrowsAsync<ArgumentNullException>(
                () => executor.WriteManyAsync(null!, CancellationToken.None)))
            .IsTrue();
    }

    /// <summary>Verifies the client retains validation and grouped-provider failures per item.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Client_ReturnsGroupedProviderAndPreparationFailures()
    {
        using var plc = new BatchPlcDouble
        {
            ReadBatchException = new InvalidOperationException("batch read failed"),
            WriteBatchException = new InvalidOperationException("batch write failed"),
        };
        using var client = new OmronLogicalTagClient(plc);
        _ = client.CreateTag(new PlcTag<short>(FirstTagName, "D1"));
        _ = client.CreateTag(new PlcTag<short>(SecondTagName, "D2"));
        var timestamp = TimeProvider.System.GetUtcNow();
        LogicalTagValue[] validWrites =
        [
            new(FirstTagName, WordValue, timestamp),
            new(SecondTagName, SecondWordValue, timestamp),
        ];
        LogicalTagValue[] invalidWrites =
        [
            null!,
            new("Missing", WordValue, timestamp),
            new("First", "invalid short", timestamp),
        ];

        var readFailures = await client.ReadManyAsync(
            [FirstTagName, SecondTagName],
            CancellationToken.None);
        var writeFailures = await client.WriteManyAsync(validWrites, CancellationToken.None);
        var validationFailures = await client.WriteManyAsync(
            invalidWrites,
            CancellationToken.None);
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.That(readFailures.All(static result => !result.Succeeded)).IsTrue();
        await Assert.That(readFailures.All(static result => result.Error == "batch read failed"))
            .IsTrue();
        await Assert.That(writeFailures.All(static result => !result.Succeeded)).IsTrue();
        await Assert.That(writeFailures.All(static result => result.Error == "batch write failed"))
            .IsTrue();
        await Assert.That(validationFailures.All(static result => !result.Succeeded)).IsTrue();
        await Assert.That(validationFailures[0].Error).Contains("null entries");
        await Assert.That(validationFailures[1].Error).Contains("not registered");
        await Assert.That(
            await ThrowsAsync<OperationCanceledException>(
                () => client.ReadManyAsync([FirstTagName, SecondTagName], source.Token)))
            .IsTrue();
        await Assert.That(
            await ThrowsAsync<OperationCanceledException>(
                () => client.WriteManyAsync(validWrites, source.Token)))
            .IsTrue();
    }

    /// <summary>Creates one grouped-operation item.</summary>
    /// <param name="inputIndex">Original caller position.</param>
    /// <param name="tagName">Logical tag name.</param>
    /// <param name="address">Omron memory address.</param>
    /// <param name="valueType">Runtime value type.</param>
    /// <param name="value">Optional write value.</param>
    /// <returns>The grouped-operation item.</returns>
    private static OmronLogicalBatchItem NewItem(
        int inputIndex,
        string tagName,
        string address,
        Type valueType,
        object? value) =>
        new(inputIndex, tagName, address, valueType, value);

    /// <summary>Gets every supported FINS word-area alias.</summary>
    /// <returns>The resolved word-area values.</returns>
    private static MemoryWordDataType[] GetWordAliases() =>
    [
        OmronLogicalBatchCodec.ToWordType("D"),
        OmronLogicalBatchCodec.ToWordType("DM"),
        OmronLogicalBatchCodec.ToWordType("C"),
        OmronLogicalBatchCodec.ToWordType("CIO"),
        OmronLogicalBatchCodec.ToWordType("W"),
        OmronLogicalBatchCodec.ToWordType("H"),
        OmronLogicalBatchCodec.ToWordType("A"),
    ];

    /// <summary>Gets every supported FINS bit-area alias.</summary>
    /// <returns>The resolved bit-area values.</returns>
    private static MemoryBitDataType[] GetBitAliases() =>
    [
        OmronLogicalBatchCodec.ToBitType("D"),
        OmronLogicalBatchCodec.ToBitType("DM"),
        OmronLogicalBatchCodec.ToBitType("C"),
        OmronLogicalBatchCodec.ToBitType("CIO"),
        OmronLogicalBatchCodec.ToBitType("W"),
        OmronLogicalBatchCodec.ToBitType("H"),
        OmronLogicalBatchCodec.ToBitType("A"),
    ];

    /// <summary>Reports whether a synchronous operation throws the expected exception.</summary>
    /// <typeparam name="TException">Expected exception type.</typeparam>
    /// <param name="action">Operation to invoke.</param>
    /// <returns>True when the expected exception is thrown.</returns>
    private static bool Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }

    /// <summary>Reports whether an asynchronous operation throws the expected exception.</summary>
    /// <typeparam name="TException">Expected exception type.</typeparam>
    /// <param name="action">Operation to invoke.</param>
    /// <returns>True when the expected exception is thrown.</returns>
    private static async Task<bool> ThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }

    /// <summary>Configurable native FINS memory-area test double.</summary>
    private sealed class MemoryAreaDouble : IOmronMemoryAreaOperations
    {
        /// <inheritdoc />
        public string RouteIdentity => "test-route";

        /// <inheritdoc />
        public int MaximumReadWordCount { get; init; } = ushort.MaxValue;

        /// <inheritdoc />
        public int MaximumWriteWordCount { get; init; } = ushort.MaxValue;

        /// <summary>Gets the configured word-read response.</summary>
        public short[] ReadWordsResult { get; init; } = [WordValue];

        /// <summary>Gets the optional native read failure.</summary>
        public Exception? ReadException { get; init; }

        /// <summary>Gets the optional native write failure.</summary>
        public Exception? WriteException { get; init; }

        /// <summary>Gets the number of native word reads.</summary>
        public int ReadWordsCount { get; private set; }

        /// <summary>Gets the number of native word writes.</summary>
        public int WriteWordsCount { get; private set; }

        /// <inheritdoc />
        public Task<short[]> ReadWordsAsync(
            ushort address,
            ushort length,
            MemoryWordDataType dataType,
            CancellationToken cancellationToken)
        {
            ReadWordsCount++;
            cancellationToken.ThrowIfCancellationRequested();
            return ReadException is null
                ? Task.FromResult(ReadWordsResult)
                : Task.FromException<short[]>(ReadException);
        }

        /// <inheritdoc />
        public Task WriteWordsAsync(
            short[] values,
            ushort address,
            MemoryWordDataType dataType,
            CancellationToken cancellationToken)
        {
            WriteWordsCount++;
            cancellationToken.ThrowIfCancellationRequested();
            return WriteException is null
                ? Task.CompletedTask
                : Task.FromException(WriteException);
        }

        /// <inheritdoc />
        public Task<bool[]> ReadBitsAsync(
            ushort address,
            byte bitIndex,
            byte length,
            MemoryBitDataType dataType,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new bool[length]);
        }

        /// <inheritdoc />
        public Task WriteBitsAsync(
            bool[] values,
            ushort address,
            byte bitIndex,
            MemoryBitDataType dataType,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    /// <summary>Composes the standard PLC fake with configurable grouped operations.</summary>
    private sealed class BatchPlcDouble : IOmronPlcRx, IOmronLogicalBatchOperations
    {
        /// <summary>Gets the underlying sequential PLC fake.</summary>
        private readonly FakeOmronPlcRx _inner = new();

        /// <summary>Gets the configured grouped-read failure.</summary>
        public Exception? ReadBatchException { get; init; }

        /// <summary>Gets the configured grouped-write failure.</summary>
        public Exception? WriteBatchException { get; init; }

        /// <inheritdoc />
        public IObservable<IPlcTag?> ObserveAll => _inner.ObserveAll;

        /// <inheritdoc />
        public IObservable<OmronPLCException?> Errors => _inner.Errors;

        /// <inheritdoc />
        public PlcType PlcType => _inner.PlcType;

        /// <inheritdoc />
        public string? ControllerModel => _inner.ControllerModel;

        /// <inheritdoc />
        public string? ControllerVersion => _inner.ControllerVersion;

        /// <inheritdoc />
        public bool IsDisposed => _inner.IsDisposed;

        /// <inheritdoc />
        public void AddUpdateTagItem<T>(PlcTag<T> tag) => _inner.AddUpdateTagItem(tag);

        /// <inheritdoc />
        public bool RemoveTagItem(string tagName) => _inner.RemoveTagItem(tagName);

        /// <inheritdoc />
        public IObservable<T?> Observe<T>(LogicalTagKey<T> tag) => _inner.Observe(tag);

        /// <inheritdoc />
        public T? GetValue<T>(LogicalTagKey<T> tag) => _inner.GetValue(tag);

        /// <inheritdoc />
        public Task<T?> ReadValueAsync<T>(
            LogicalTagKey<T> tag,
            CancellationToken cancellationToken) =>
            _inner.ReadValueAsync(tag, cancellationToken);

        /// <inheritdoc />
        public void SetValue<T>(LogicalTagKey<T> tag, T? value) => _inner.SetValue(tag, value);

        /// <inheritdoc />
        public Task WriteValueAsync<T>(
            LogicalTagKey<T> tag,
            T? value,
            CancellationToken cancellationToken) =>
            _inner.WriteValueAsync(tag, value, cancellationToken);

        /// <inheritdoc />
        public Task<ReadClockResult> ReadClockAsync(CancellationToken cancellationToken) =>
            _inner.ReadClockAsync(cancellationToken);

        /// <inheritdoc />
        public Task<WriteClockResult> WriteClockAsync(
            DateTimeOffset newDateTime,
            CancellationToken cancellationToken) =>
            _inner.WriteClockAsync(newDateTime, cancellationToken);

        /// <inheritdoc />
        public Task<WriteClockResult> WriteClockAsync(
            DateTimeOffset newDateTime,
            int newDayOfWeek,
            CancellationToken cancellationToken) =>
            _inner.WriteClockAsync(newDateTime, newDayOfWeek, cancellationToken);

        /// <inheritdoc />
        public Task<ReadCycleTimeResult> ReadCycleTimeAsync(CancellationToken cancellationToken) =>
            _inner.ReadCycleTimeAsync(cancellationToken);

        /// <inheritdoc />
        public void Dispose() => _inner.Dispose();

        /// <inheritdoc />
        Task<IReadOnlyList<OmronLogicalBatchResult>>
            IOmronLogicalBatchOperations.ReadManyAsync(
                IReadOnlyList<OmronLogicalBatchItem> items,
                CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ReadBatchException is null
                ? Task.FromResult<IReadOnlyList<OmronLogicalBatchResult>>(
                    items
                        .Select(static item => OmronLogicalBatchResult.Success(item.InputIndex, null))
                        .ToArray())
                : Task.FromException<IReadOnlyList<OmronLogicalBatchResult>>(ReadBatchException);
        }

        /// <inheritdoc />
        Task<IReadOnlyList<OmronLogicalBatchResult>>
            IOmronLogicalBatchOperations.WriteManyAsync(
                IReadOnlyList<OmronLogicalBatchItem> items,
                CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return WriteBatchException is null
                ? Task.FromResult<IReadOnlyList<OmronLogicalBatchResult>>(
                    items
                        .Select(
                            static item =>
                                OmronLogicalBatchResult.Success(item.InputIndex, item.Value))
                        .ToArray())
                : Task.FromException<IReadOnlyList<OmronLogicalBatchResult>>(WriteBatchException);
        }
    }
}
