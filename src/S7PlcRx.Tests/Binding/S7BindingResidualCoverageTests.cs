// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Reflection;
using IoT.DriverCore.S7PlcRx.Binding;
using IoT.DriverCore.S7PlcRx.Enums;
using IoT.DriverCore.S7PlcRx.PlcTypes;
using BclDateTime = System.DateTime;
using BclTimeSpan = System.TimeSpan;
using PlcString = IoT.DriverCore.S7PlcRx.PlcTypes.String;

namespace IoT.DriverCore.S7PlcRx.Tests.Binding;

/// <summary>Exercises residual runtime-binding and observable lifecycle behavior with an in-memory PLC.</summary>
[NotInParallel]
public sealed class S7BindingResidualCoverageTests
{
    /// <summary>Defines the polling interval used by deterministic read tests.</summary>
    private const int PollIntervalMilliseconds = 10;

    /// <summary>Defines the number of seconds allowed for timer-driven work.</summary>
    private const int WaitTimeoutSeconds = 2;

    /// <summary>Defines the complete readable byte-range length.</summary>
    private const int ReadRangeLength = 82;

    /// <summary>Defines the complete writable byte-range length.</summary>
    private const int WriteRangeLength = 83;

    /// <summary>Defines the short-value byte offset.</summary>
    private const int IntOffset = 2;

    /// <summary>Defines the unsigned-word byte offset.</summary>
    private const int WordOffset = 4;

    /// <summary>Defines the signed-double-word byte offset.</summary>
    private const int DIntOffset = 6;

    /// <summary>Defines the unsigned-double-word byte offset.</summary>
    private const int DWordOffset = 10;

    /// <summary>Defines the single-precision byte offset.</summary>
    private const int RealOffset = 14;

    /// <summary>Defines the double-precision byte offset.</summary>
    private const int LRealOffset = 18;

    /// <summary>Defines the text byte offset.</summary>
    private const int TextOffset = 26;

    /// <summary>Defines the byte-array byte offset.</summary>
    private const int BytesOffset = 30;

    /// <summary>Defines the signed-word-array byte offset.</summary>
    private const int IntsOffset = 34;

    /// <summary>Defines the unsigned-word-array byte offset.</summary>
    private const int WordsOffset = 38;

    /// <summary>Defines the signed-double-word-array byte offset.</summary>
    private const int DIntsOffset = 42;

    /// <summary>Defines the unsigned-double-word-array byte offset.</summary>
    private const int DWordsOffset = 50;

    /// <summary>Defines the single-precision-array byte offset.</summary>
    private const int RealsOffset = 58;

    /// <summary>Defines the double-precision-array byte offset.</summary>
    private const int LRealsOffset = 66;

    /// <summary>Defines the final writable-byte offset.</summary>
    private const int LastByteOffset = 82;

    /// <summary>Defines the number of elements in numeric array tags.</summary>
    private const int ArrayElementCount = 2;

    /// <summary>Defines the configured text byte length.</summary>
    private const int TextLength = 4;

    /// <summary>Defines the configured byte-array length.</summary>
    private const int ByteArrayLength = 3;

    /// <summary>Defines the signed double-word read value.</summary>
    private const int ReadDIntValue = -123_456_789;

    /// <summary>Defines the unsigned double-word read value.</summary>
    private const uint ReadDWordValue = 4_000_000_000U;

    /// <summary>Defines the single-precision read value.</summary>
    private const float ReadRealValue = 12.5F;

    /// <summary>Defines the double-precision read value.</summary>
    private const double ReadLRealValue = -25.25D;

    /// <summary>Defines the first single-precision array read value.</summary>
    private const float FirstReadRealArrayValue = -1.5F;

    /// <summary>Defines the second single-precision array read value.</summary>
    private const float SecondReadRealArrayValue = 2.5F;

    /// <summary>Defines the first double-precision array read value.</summary>
    private const double FirstReadLRealArrayValue = -3.5D;

    /// <summary>Defines the second double-precision array read value.</summary>
    private const double SecondReadLRealArrayValue = 4.5D;

    /// <summary>Defines the signed-word write value.</summary>
    private const short WrittenIntValue = -2;

    /// <summary>Defines the unsigned-word write value.</summary>
    private const ushort WrittenWordValue = 3;

    /// <summary>Defines the signed-double-word write value.</summary>
    private const int WrittenDIntValue = -4;

    /// <summary>Defines the unsigned-double-word write value.</summary>
    private const uint WrittenDWordValue = 5U;

    /// <summary>Defines the single-precision write value.</summary>
    private const float WrittenRealValue = 6.5F;

    /// <summary>Defines the double-precision write value.</summary>
    private const double WrittenLRealValue = 7.5D;

    /// <summary>Defines the first signed-word-array write value.</summary>
    private const short FirstWrittenIntArrayValue = -8;

    /// <summary>Defines the second signed-word-array write value.</summary>
    private const short SecondWrittenIntArrayValue = 9;

    /// <summary>Defines the first unsigned-word-array write value.</summary>
    private const ushort FirstWrittenWordArrayValue = 10;

    /// <summary>Defines the second unsigned-word-array write value.</summary>
    private const ushort SecondWrittenWordArrayValue = 11;

    /// <summary>Defines the first signed-double-word-array write value.</summary>
    private const int FirstWrittenDIntArrayValue = -12;

    /// <summary>Defines the second signed-double-word-array write value.</summary>
    private const int SecondWrittenDIntArrayValue = 13;

    /// <summary>Defines the first unsigned-double-word-array write value.</summary>
    private const uint FirstWrittenDWordArrayValue = 14;

    /// <summary>Defines the second unsigned-double-word-array write value.</summary>
    private const uint SecondWrittenDWordArrayValue = 15;

    /// <summary>Defines the first single-precision-array write value.</summary>
    private const float FirstWrittenRealArrayValue = 16.5F;

    /// <summary>Defines the second single-precision-array write value.</summary>
    private const float SecondWrittenRealArrayValue = 17.5F;

    /// <summary>Defines the first double-precision-array write value.</summary>
    private const double FirstWrittenLRealArrayValue = 18.5D;

    /// <summary>Defines the second double-precision-array write value.</summary>
    private const double SecondWrittenLRealArrayValue = 19.5D;

    /// <summary>Defines the value attempted after binding disposal.</summary>
    private const int PostDisposeValue = 42;

    /// <summary>Defines the delay used to prove that a disposed binding does not write.</summary>
    private const int PostDisposeDelayMilliseconds = 40;

    /// <summary>Defines the expected number of separated range writes.</summary>
    private const int SeparatedRangeCount = 3;

    /// <summary>Defines the second replay-observable value.</summary>
    private const int SecondReplayValue = 2;

    /// <summary>Defines the third replay-observable value.</summary>
    private const int ThirdReplayValue = 3;

    /// <summary>Defines the adapter's published value.</summary>
    private const int AdapterPublishedValue = 10;

    /// <summary>Defines the value published after source completion.</summary>
    private const int PostCompletionValue = 11;

    /// <summary>Defines the value published after enumerator disposal.</summary>
    private const int PostDisposalValue = 12;

    /// <summary>Defines the bit tag name.</summary>
    private const string BitTagName = nameof(Bit);

    /// <summary>Defines the byte tag name.</summary>
    private const string ByteTagName = nameof(System.Byte);

    /// <summary>Defines the signed-word tag name.</summary>
    private const string IntTagName = nameof(Int);

    /// <summary>Defines the unsigned-word tag name.</summary>
    private const string WordTagName = nameof(Word);

    /// <summary>Defines the signed-double-word tag name.</summary>
    private const string DIntTagName = nameof(DInt);

    /// <summary>Defines the unsigned-double-word tag name.</summary>
    private const string DWordTagName = nameof(DWord);

    /// <summary>Defines the single-precision tag name.</summary>
    private const string RealTagName = nameof(Real);

    /// <summary>Defines the double-precision tag name.</summary>
    private const string LRealTagName = nameof(LReal);

    /// <summary>Defines the text tag name.</summary>
    private const string TextTagName = "Text";

    /// <summary>Defines the byte-array tag name.</summary>
    private const string BytesTagName = "Bytes";

    /// <summary>Defines the signed-word-array tag name.</summary>
    private const string IntsTagName = "Ints";

    /// <summary>Defines the unsigned-word-array tag name.</summary>
    private const string WordsTagName = "Words";

    /// <summary>Defines the signed-double-word-array tag name.</summary>
    private const string DIntsTagName = "DInts";

    /// <summary>Defines the unsigned-double-word-array tag name.</summary>
    private const string DWordsTagName = "DWords";

    /// <summary>Defines the single-precision-array tag name.</summary>
    private const string RealsTagName = "Reals";

    /// <summary>Defines the double-precision-array tag name.</summary>
    private const string LRealsTagName = "LReals";

    /// <summary>Defines the final byte tag name.</summary>
    private const string LastByteTagName = "LastByte";

    /// <summary>Defines the read-only guard tag name.</summary>
    private const string ReadOnlyTagName = "ReadOnly";

    /// <summary>Defines the fixed readable text.</summary>
    private const string ReadText = "S7";

    /// <summary>Defines the fixed writable text.</summary>
    private const string WrittenText = "RX";

    /// <summary>Defines the reflected scalar encoder method name.</summary>
    private const string ToScalarBytesMethodName = "ToScalarBytes";

    /// <summary>Defines the reflected array encoder method name.</summary>
    private const string ToArrayBytesMethodName = "ToArrayBytes";

    /// <summary>Defines the reflected asynchronous write-flush method name.</summary>
    private const string FlushWritesAsyncMethodName = "FlushWritesAsync";

    /// <summary>Defines the unsupported scalar binding name.</summary>
    private const string UnsupportedScalarTagName = "UnsupportedScalar";

    /// <summary>Defines the unsupported array binding name.</summary>
    private const string UnsupportedArrayTagName = "UnsupportedArray";

    /// <summary>Defines the synthetic grouped-write tag name.</summary>
    private const string GroupedWriteTagName = "__s7_binding_db2_0_83";

    /// <summary>Defines the minimal range tag name for a write containing only the bit.</summary>
    private const string SingleBitWriteTagName = "__s7_binding_db2_0_1";

    /// <summary>Defines a tag name that is deliberately absent from the binding.</summary>
    private const string MissingTagName = nameof(MissingTagName);

    /// <summary>Defines the timeout used while waiting for timer-driven binding work.</summary>
    private static readonly BclTimeSpan WaitTimeout = BclTimeSpan.FromSeconds(WaitTimeoutSeconds);

    /// <summary>Verifies every supported scalar and array decoder, including signed DInt values.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task RuntimeBindingReadsSupportedScalarsAndArraysAsync()
    {
        using var plc = new DeterministicPlc();
        var definitions = CreateReadDefinitions();
        var bytes = new byte[ReadRangeLength];
        bytes[0] = 0b0000_0010;
        bytes[1] = byte.MaxValue;
        Copy(Int.ToByteArray(short.MinValue), bytes, IntOffset);
        Copy(Word.ToByteArray(ushort.MaxValue), bytes, WordOffset);
        Copy(DInt.ToByteArray(ReadDIntValue), bytes, DIntOffset);
        Copy(DWord.ToByteArray(ReadDWordValue), bytes, DWordOffset);
        Copy(Real.ToByteArray(ReadRealValue), bytes, RealOffset);
        Copy(LReal.ToByteArray(ReadLRealValue), bytes, LRealOffset);
        Copy(PlcString.ToByteArray(ReadText), bytes, TextOffset);
        Copy([0x10, 0x20, 0x30], bytes, BytesOffset);
        Copy(Int.ToByteArray([-1, ArrayElementCount]), bytes, IntsOffset);
        Copy(Word.ToByteArray([1, ushort.MaxValue]), bytes, WordsOffset);
        Copy(DInt.ToByteArray([int.MinValue, int.MaxValue]), bytes, DIntsOffset);
        Copy(DWord.ToByteArray([0U, uint.MaxValue]), bytes, DWordsOffset);
        Copy(
            Real.ToByteArray([FirstReadRealArrayValue, SecondReadRealArrayValue]),
            bytes,
            RealsOffset);
        Copy(
            LReal.ToByteArray([FirstReadLRealArrayValue, SecondReadLRealArrayValue]),
            bytes,
            LRealsOffset);
        plc.SetReadBuffer("__s7_binding_db1_0_82", bytes);
        var applied = new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        using var binding = S7TagRuntimeBinding.Bind(
            plc,
            definitions,
            (name, value) => applied[name] = value);

        await WaitUntilAsync(() => applied.Count == definitions.Length);

        await TUnit.Assertions.Assert.That((bool)applied[BitTagName]!).IsTrue();
        await TUnit.Assertions.Assert.That((byte)applied[ByteTagName]!).IsEqualTo(byte.MaxValue);
        await TUnit.Assertions.Assert.That((short)applied[IntTagName]!).IsEqualTo(short.MinValue);
        await TUnit.Assertions.Assert.That((ushort)applied[WordTagName]!).IsEqualTo(ushort.MaxValue);
        await TUnit.Assertions.Assert.That((int)applied[DIntTagName]!).IsEqualTo(ReadDIntValue);
        await TUnit.Assertions.Assert.That((uint)applied[DWordTagName]!).IsEqualTo(ReadDWordValue);
        await TUnit.Assertions.Assert.That((float)applied[RealTagName]!).IsEqualTo(ReadRealValue);
        await TUnit.Assertions.Assert.That((double)applied[LRealTagName]!).IsEqualTo(ReadLRealValue);
        await TUnit.Assertions.Assert.That((string)applied[TextTagName]!).IsEqualTo(ReadText);
        await TUnit.Assertions.Assert.That((byte[])applied[BytesTagName]!).IsEquivalentTo(
            (byte[])[0x10, 0x20, 0x30]);
        await TUnit.Assertions.Assert.That((short[])applied[IntsTagName]!).IsEquivalentTo(
            (short[])[-1, ArrayElementCount]);
        await TUnit.Assertions.Assert.That((ushort[])applied[WordsTagName]!).IsEquivalentTo(
            (ushort[])[1, ushort.MaxValue]);
        await TUnit.Assertions.Assert.That((int[])applied[DIntsTagName]!).IsEquivalentTo(
            (int[])[int.MinValue, int.MaxValue]);
        await TUnit.Assertions.Assert.That((uint[])applied[DWordsTagName]!).IsEquivalentTo(
            (uint[])[0U, uint.MaxValue]);
        await TUnit.Assertions.Assert.That((float[])applied[RealsTagName]!).IsEquivalentTo(
            (float[])[FirstReadRealArrayValue, SecondReadRealArrayValue]);
        await TUnit.Assertions.Assert.That((double[])applied[LRealsTagName]!).IsEquivalentTo(
            (double[])[FirstReadLRealArrayValue, SecondReadLRealArrayValue]);
    }

    /// <summary>Verifies supported write encoders, grouped ranges, guards, and post-disposal behavior.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task RuntimeBindingWritesSupportedValuesAndHonorsGuardsAsync()
    {
        using var plc = new DeterministicPlc();
        plc.SetReadBuffer(GroupedWriteTagName, [0]);
        var definitions = CreateWriteDefinitions();
        using var binding = S7TagRuntimeBinding.Bind(plc, definitions, (_, _) => { });

        QueueSupportedWrites(binding);

        await WaitUntilAsync(() => plc.Writes.Any(write =>
            string.Equals(write.Name, GroupedWriteTagName, StringComparison.Ordinal)));
        var write = plc.Writes.Last(item =>
            string.Equals(item.Name, GroupedWriteTagName, StringComparison.Ordinal));
        var bytes = (byte[])write.Value!;

        await AssertSupportedWriteBytesAsync(bytes);

        await Task.Delay(PostDisposeDelayMilliseconds);
        binding.Write(BitTagName, false);
        await InvokePrivateBindingTaskAsync(binding, FlushWritesAsyncMethodName);
        await WaitUntilAsync(() =>
            plc.Writes.LastOrDefault(item =>
                string.Equals(item.Name, SingleBitWriteTagName, StringComparison.Ordinal)).Value
            is byte[] candidate &&
            !Bit.FromByte(candidate[0], 1));
        var clearedBitBytes = (byte[])plc.Writes.Last(item =>
            string.Equals(item.Name, SingleBitWriteTagName, StringComparison.Ordinal)).Value!;
        await TUnit.Assertions.Assert.That(Bit.FromByte(clearedBitBytes[0], 1)).IsFalse();

        binding.Dispose();
        binding.Dispose();
        var writeCount = plc.Writes.Count;
        binding.Write(DIntTagName, PostDisposeValue);
        await Task.Delay(PostDisposeDelayMilliseconds);

        await TUnit.Assertions.Assert.That(plc.Writes.Count).IsEqualTo(writeCount);
    }

    /// <summary>Verifies unsupported read types and fallback write encoders remain deterministic.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task RuntimeBindingFallbackCodecsReturnNullOrEmptyValuesAsync()
    {
        using var plc = new DeterministicPlc();
        S7TagDefinition[] definitions =
        [
            new(
                UnsupportedScalarTagName,
                "DB6.DBB0",
                typeof(decimal),
                PollIntervalMilliseconds,
                S7TagDirection.ReadOnly,
                1),
            new(
                UnsupportedArrayTagName,
                "DB6.DBB1",
                typeof(decimal[]),
                PollIntervalMilliseconds,
                S7TagDirection.ReadOnly,
                ArrayElementCount),
        ];
        plc.SetReadBuffer("__s7_binding_db6_0_3", new byte[SeparatedRangeCount]);
        var applied = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        using var binding = S7TagRuntimeBinding.Bind(
            plc,
            definitions,
            (name, value) => applied[name] = value is null);

        await WaitUntilAsync(() => applied.Count == definitions.Length);

        var scalarEncoder = GetPrivateStaticMethod(ToScalarBytesMethodName);
        var arrayEncoder = GetPrivateStaticMethod(ToArrayBytesMethodName);
        var falseBytes = (byte[])scalarEncoder.Invoke(null, [typeof(bool), false])!;
        var unsupportedScalar = (byte[])scalarEncoder.Invoke(
            null,
            [typeof(decimal), decimal.Zero])!;
        var unsupportedArray = (byte[])arrayEncoder.Invoke(
            null,
            [typeof(decimal[]), Array.Empty<decimal>()])!;

        await TUnit.Assertions.Assert.That(applied[UnsupportedScalarTagName]).IsTrue();
        await TUnit.Assertions.Assert.That(applied[UnsupportedArrayTagName]).IsTrue();
        await TUnit.Assertions.Assert.That(falseBytes).IsEquivalentTo((byte[])[0]);
        await TUnit.Assertions.Assert.That(unsupportedScalar.Length).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(unsupportedArray.Length).IsEqualTo(0);
    }

    /// <summary>Verifies separated DB ranges, null/short reads, address validation, and rebinding.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task RuntimeBindingGroupsRangesRebindsAndValidatesDefinitionsAsync()
    {
        using var plc = new DeterministicPlc();
        var writes = new[]
        {
            new S7TagDefinition("First", "DB3.DBB0", typeof(byte), 0, S7TagDirection.WriteOnly, 1),
            new S7TagDefinition("Gap", "DB3.DBB40", typeof(byte), 0, S7TagDirection.WriteOnly, 1),
            new S7TagDefinition("OtherDb", "DB4.DBB0", typeof(byte), 0, S7TagDirection.WriteOnly, 1),
        };
        using var first = S7TagRuntimeBinding.Bind(plc, writes, (_, _) => { });
        using var rebound = S7TagRuntimeBinding.Bind(plc, writes, (_, _) => { });
        first.Write("First", (byte)1);
        first.Write("Gap", (byte)SecondReplayValue);
        first.Write("OtherDb", (byte)ThirdReplayValue);

        await WaitUntilAsync(() => plc.Writes.Count >= SeparatedRangeCount);

        await TUnit.Assertions.Assert.That(plc.Writes.Select(write => write.Name)).Contains(
            "__s7_binding_db3_0_1");
        await TUnit.Assertions.Assert.That(plc.Writes.Select(write => write.Name)).Contains(
            "__s7_binding_db3_40_1");
        await TUnit.Assertions.Assert.That(plc.Writes.Select(write => write.Name)).Contains(
            "__s7_binding_db4_0_1");
        await TUnit.Assertions.Assert.That(
                () => S7TagRuntimeBinding.Bind(plc, null!, (_, _) => { }))
            .Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(
                () => S7TagRuntimeBinding.Bind(plc, writes, null!))
            .Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(
                () => BindInvalid(plc, "M0"))
            .Throws<ArgumentException>();
        await TUnit.Assertions.Assert.That(
                () => BindInvalid(plc, "DBX.DBW0"))
            .Throws<ArgumentException>();
        await TUnit.Assertions.Assert.That(
                () => BindInvalid(plc, "DB1.X0"))
            .Throws<ArgumentException>();
        await TUnit.Assertions.Assert.That(
                () => BindInvalid(plc, "DB1.DBQ0"))
            .Throws<ArgumentException>();
        await TUnit.Assertions.Assert.That(
                () => BindInvalid(plc, "DB1.DBX0"))
            .Throws<ArgumentException>();
        await TUnit.Assertions.Assert.That(
                () => BindInvalid(plc, "DB1.DBXbad.0"))
            .Throws<ArgumentException>();
        await TUnit.Assertions.Assert.That(
                () => BindInvalid(plc, "DB1.DBX0.bad"))
            .Throws<ArgumentException>();
        await TUnit.Assertions.Assert.That(
                () => BindInvalid(plc, "DB1.DBX0.8"))
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Verifies replay, active publication, unsubscription, and idempotent subscription disposal.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task TagValueObservableReplaysPublishesAndUnsubscribesAsync()
    {
        var observable = new S7TagValueObservable<int>();
        var first = new RecordingObserver<int>();
        var second = new RecordingObserver<int>();
        observable.Publish(1);
        using var firstSubscription = observable.Subscribe(first);
        var secondSubscription = observable.Subscribe(second);
        observable.Publish(SecondReplayValue);
        secondSubscription.Dispose();
        foreach (var disposable in (IDisposable[])[secondSubscription])
        {
            disposable.Dispose();
        }

        observable.Publish(ThirdReplayValue);

        await TUnit.Assertions.Assert.That(first.Values).IsEquivalentTo(
            (int[])[1, SecondReplayValue, ThirdReplayValue]);
        await TUnit.Assertions.Assert.That(second.Values).IsEquivalentTo(
            (int[])[1, SecondReplayValue]);
        await TUnit.Assertions.Assert.That(
                () => observable.Subscribe(null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>Verifies session and async-observable completion, error, cancellation, and disposal lifecycles.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task BindingSessionAndObservableAdapterHonorTerminalLifecyclesAsync()
    {
        var runtime = new CountingDisposable();
        var logical = new CountingDisposable();
        var session = new S7TagBindingSession(runtime, logical);
        foreach (var disposable in (IDisposable[])[session, session])
        {
            disposable.Dispose();
        }

        await TUnit.Assertions.Assert.That(runtime.DisposeCount).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(logical.DisposeCount).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(
                () => new S7TagBindingSession(null!, logical))
            .Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(
                () => new S7TagBindingSession(runtime, null!))
            .Throws<ArgumentNullException>();

        var source = new ManualObservable<int>();
        var enumerator = S7TagObservableAdapter.ToAsyncEnumerable(source).GetAsyncEnumerator();
        var pendingValue = enumerator.MoveNextAsync().AsTask();
        source.Publish(AdapterPublishedValue);
        await TUnit.Assertions.Assert.That(await pendingValue).IsTrue();
        await TUnit.Assertions.Assert.That(enumerator.Current).IsEqualTo(AdapterPublishedValue);
        var pendingCompletion = enumerator.MoveNextAsync().AsTask();
        source.Complete();
        await TUnit.Assertions.Assert.That(await pendingCompletion).IsFalse();
        source.Publish(PostCompletionValue);
        await enumerator.DisposeAsync();
        await enumerator.DisposeAsync();
        source.PublishRetained(PostDisposalValue);
        source.CompleteRetained();
        await TUnit.Assertions.Assert.That(source.ActiveSubscriptions).IsEqualTo(0);

        var errors = new ManualObservable<int>();
        await using var failed = S7TagObservableAdapter.ToAsyncEnumerable(errors).GetAsyncEnumerator();
        var pendingError = failed.MoveNextAsync().AsTask();
        errors.Fail(new InvalidOperationException("binding failure"));
        await TUnit.Assertions.Assert.That(() => pendingError).Throws<InvalidOperationException>();

        var canceledSource = new ManualObservable<int>();
        using var cancellation = new CancellationTokenSource();
        await using var canceled = S7TagObservableAdapter
            .ToAsyncEnumerable(canceledSource)
            .GetAsyncEnumerator(cancellation.Token);
        var pendingCancellation = canceled.MoveNextAsync().AsTask();
        await AsyncCompatibility.CancelAsync(cancellation);
        await TUnit.Assertions.Assert.That(() => pendingCancellation).Throws<OperationCanceledException>();
        await TUnit.Assertions.Assert.That(
                () => S7TagObservableAdapter.ToAsyncEnumerable<int>(null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>Queues every supported binding value plus public write guards.</summary>
    /// <param name="binding">The runtime binding under test.</param>
    private static void QueueSupportedWrites(S7TagRuntimeBinding binding)
    {
        binding.Write(BitTagName, true);
        binding.Write(ByteTagName, (byte)0xA5);
        binding.Write(IntTagName, WrittenIntValue);
        binding.Write(WordTagName, WrittenWordValue);
        binding.Write(DIntTagName, WrittenDIntValue);
        binding.Write(DWordTagName, WrittenDWordValue);
        binding.Write(RealTagName, WrittenRealValue);
        binding.Write(LRealTagName, WrittenLRealValue);
        binding.Write(TextTagName, WrittenText);
        binding.Write(BytesTagName, (byte[])[0x80, 0x81, 0x82]);
        binding.Write(
            IntsTagName,
            (short[])[FirstWrittenIntArrayValue, SecondWrittenIntArrayValue]);
        binding.Write(
            WordsTagName,
            (ushort[])[FirstWrittenWordArrayValue, SecondWrittenWordArrayValue]);
        binding.Write(
            DIntsTagName,
            (int[])[FirstWrittenDIntArrayValue, SecondWrittenDIntArrayValue]);
        binding.Write(
            DWordsTagName,
            (uint[])[FirstWrittenDWordArrayValue, SecondWrittenDWordArrayValue]);
        binding.Write(
            RealsTagName,
            (float[])[FirstWrittenRealArrayValue, SecondWrittenRealArrayValue]);
        binding.Write(
            LRealsTagName,
            (double[])[FirstWrittenLRealArrayValue, SecondWrittenLRealArrayValue]);
        binding.Write(LastByteTagName, (byte)0x22);
        binding.Write(MissingTagName, 1);
        binding.Write(" ", 1);
        binding.Write(ReadOnlyTagName, 1);
    }

    /// <summary>Verifies the encoded payload produced by all supported binding writes.</summary>
    /// <param name="bytes">The grouped binding payload.</param>
    /// <returns>A task representing the asynchronous assertions.</returns>
    private static async Task AssertSupportedWriteBytesAsync(byte[] bytes)
    {
        await TUnit.Assertions.Assert.That(bytes.Length).IsEqualTo(WriteRangeLength);
        await TUnit.Assertions.Assert.That(Bit.FromByte(bytes[0], 1)).IsTrue();
        await TUnit.Assertions.Assert.That(bytes[1]).IsEqualTo((byte)0xA5);
        await TUnit.Assertions.Assert.That(
            Int.FromSpan(bytes.AsSpan(IntOffset, sizeof(short)))).IsEqualTo(WrittenIntValue);
        await TUnit.Assertions.Assert.That(
            Word.FromSpan(bytes.AsSpan(WordOffset, sizeof(ushort)))).IsEqualTo(WrittenWordValue);
        await TUnit.Assertions.Assert.That(
            DInt.FromSpan(bytes.AsSpan(DIntOffset, sizeof(int)))).IsEqualTo(WrittenDIntValue);
        await TUnit.Assertions.Assert.That(
            DWord.FromSpan(bytes.AsSpan(DWordOffset, sizeof(uint)))).IsEqualTo(WrittenDWordValue);
        await TUnit.Assertions.Assert.That(
            Real.FromSpan(bytes.AsSpan(RealOffset, sizeof(float)))).IsEqualTo(WrittenRealValue);
        await TUnit.Assertions.Assert.That(
            LReal.FromSpan(bytes.AsSpan(LRealOffset, sizeof(double)))).IsEqualTo(WrittenLRealValue);
        await TUnit.Assertions.Assert.That(
            bytes.AsSpan(TextOffset, WrittenText.Length).ToArray()).IsEquivalentTo(
            PlcString.ToByteArray(WrittenText));
        await TUnit.Assertions.Assert.That(
            DInt.ToArray(bytes.AsSpan(DIntsOffset, sizeof(int) * ArrayElementCount))).IsEquivalentTo(
            (int[])[FirstWrittenDIntArrayValue, SecondWrittenDIntArrayValue]);
        await TUnit.Assertions.Assert.That(
            bytes.AsSpan(LastByteOffset, 1).ToArray()).IsEquivalentTo((byte[])[0x22]);
    }

    /// <summary>Creates the complete set of readable binding definitions.</summary>
    /// <returns>The readable definitions.</returns>
    private static S7TagDefinition[] CreateReadDefinitions() =>
    [
        Read(BitTagName, "DB1.DBX0.1", typeof(bool)),
        Read(ByteTagName, "DB1.DBB1", typeof(byte)),
        Read(IntTagName, "DB1.DBW2", typeof(short)),
        Read(WordTagName, "DB1.DBW4", typeof(ushort)),
        Read(DIntTagName, "DB1.DBD6", typeof(int)),
        Read(DWordTagName, "DB1.DBD10", typeof(uint)),
        Read(RealTagName, "DB1.DBD14", typeof(float)),
        Read(LRealTagName, "DB1.DBD18", typeof(double)),
        Read(TextTagName, "DB1.DBB26", typeof(string), TextLength),
        Read(BytesTagName, "DB1.DBB30", typeof(byte[]), ByteArrayLength),
        Read(IntsTagName, "DB1.DBW34", typeof(short[]), ArrayElementCount),
        Read(WordsTagName, "DB1.DBW38", typeof(ushort[]), ArrayElementCount),
        Read(DIntsTagName, "DB1.DBD42", typeof(int[]), ArrayElementCount),
        Read(DWordsTagName, "DB1.DBD50", typeof(uint[]), ArrayElementCount),
        Read(RealsTagName, "DB1.DBD58", typeof(float[]), ArrayElementCount),
        Read(LRealsTagName, "DB1.DBD66", typeof(double[]), ArrayElementCount),
    ];

    /// <summary>Creates the complete set of writable binding definitions.</summary>
    /// <returns>The writable definitions.</returns>
    private static S7TagDefinition[] CreateWriteDefinitions() =>
    [
        Write(BitTagName, "DB2.DBX0.1", typeof(bool)),
        Write(ByteTagName, "DB2.DBB1", typeof(byte)),
        Write(IntTagName, "DB2.DBW2", typeof(short)),
        Write(WordTagName, "DB2.DBW4", typeof(ushort)),
        Write(DIntTagName, "DB2.DBD6", typeof(int)),
        Write(DWordTagName, "DB2.DBD10", typeof(uint)),
        Write(RealTagName, "DB2.DBD14", typeof(float)),
        Write(LRealTagName, "DB2.DBD18", typeof(double)),
        Write(TextTagName, "DB2.DBB26", typeof(string), TextLength),
        Write(BytesTagName, "DB2.DBB30", typeof(byte[]), ByteArrayLength),
        Write(IntsTagName, "DB2.DBW34", typeof(short[]), ArrayElementCount),
        Write(WordsTagName, "DB2.DBW38", typeof(ushort[]), ArrayElementCount),
        Write(DIntsTagName, "DB2.DBD42", typeof(int[]), ArrayElementCount),
        Write(DWordsTagName, "DB2.DBD50", typeof(uint[]), ArrayElementCount),
        Write(RealsTagName, "DB2.DBD58", typeof(float[]), ArrayElementCount),
        Write(LRealsTagName, "DB2.DBD66", typeof(double[]), ArrayElementCount),
        Write(LastByteTagName, "DB2.DBB82", typeof(byte)),
        new S7TagDefinition(
            ReadOnlyTagName,
            "DB5.DBB0",
            typeof(byte),
            0,
            S7TagDirection.ReadOnly,
            1),
    ];

    /// <summary>Creates a readable definition.</summary>
    /// <param name="name">The definition name.</param>
    /// <param name="address">The PLC address.</param>
    /// <param name="type">The value type.</param>
    /// <param name="length">The array or string length.</param>
    /// <returns>The readable definition.</returns>
    private static S7TagDefinition Read(string name, string address, Type type, int length = 1) =>
        new(name, address, type, PollIntervalMilliseconds, S7TagDirection.ReadOnly, length);

    /// <summary>Creates a writable definition.</summary>
    /// <param name="name">The definition name.</param>
    /// <param name="address">The PLC address.</param>
    /// <param name="type">The value type.</param>
    /// <param name="length">The array or string length.</param>
    /// <returns>The writable definition.</returns>
    private static S7TagDefinition Write(string name, string address, Type type, int length = 1) =>
        new(name, address, type, 0, S7TagDirection.WriteOnly, length);

    /// <summary>Binds one invalid definition to exercise public validation behavior.</summary>
    /// <param name="plc">The deterministic PLC.</param>
    /// <param name="address">The invalid address.</param>
    /// <returns>The binding when validation unexpectedly accepts the address.</returns>
    private static S7TagRuntimeBinding BindInvalid(IRxS7 plc, string address) =>
        S7TagRuntimeBinding.Bind(
            plc,
            [new S7TagDefinition("Invalid", address, typeof(int), 0, S7TagDirection.WriteOnly, 1)],
            (_, _) => { });

    /// <summary>Gets a private static runtime-binding helper method.</summary>
    /// <param name="methodName">The helper method name.</param>
    /// <returns>The reflected helper method.</returns>
    private static MethodInfo GetPrivateStaticMethod(string methodName) =>
        typeof(S7TagRuntimeBinding).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new InvalidOperationException($"{methodName} was not found.");

    /// <summary>Invokes a private asynchronous runtime-binding method.</summary>
    /// <param name="binding">The runtime binding.</param>
    /// <param name="methodName">The method name.</param>
    /// <returns>A task representing the private operation.</returns>
    private static async Task InvokePrivateBindingTaskAsync(
        S7TagRuntimeBinding binding,
        string methodName)
    {
        var method = typeof(S7TagRuntimeBinding).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException($"{methodName} was not found.");
        var task = (Task?)method.Invoke(binding, null) ??
            throw new InvalidOperationException($"{methodName} did not return a task.");
        await task.ConfigureAwait(false);
    }

    /// <summary>Copies bytes into a larger PLC range.</summary>
    /// <param name="source">The bytes to copy.</param>
    /// <param name="destination">The destination PLC range.</param>
    /// <param name="offset">The destination offset.</param>
    private static void Copy(byte[] source, byte[] destination, int offset) =>
        source.CopyTo(destination, offset);

    /// <summary>Waits for an asynchronous binding condition.</summary>
    /// <param name="condition">The condition that signals completion.</param>
    /// <returns>A task representing the asynchronous wait.</returns>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var expires = BclDateTime.UtcNow + WaitTimeout;
        while (!condition() && BclDateTime.UtcNow < expires)
        {
            await Task.Delay(PollIntervalMilliseconds);
        }

        await TUnit.Assertions.Assert.That(condition()).IsTrue();
    }

    /// <summary>Records values received through an observable subscription.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    private sealed class RecordingObserver<T> : IObserver<T>
    {
        /// <summary>Gets the received values.</summary>
        public List<T> Values { get; } = [];

        /// <inheritdoc />
        public void OnCompleted()
        {
        }

        /// <inheritdoc />
        public void OnError(Exception error)
        {
        }

        /// <inheritdoc />
        public void OnNext(T value) => Values.Add(value);
    }

    /// <summary>Provides deterministic source notifications and tracks subscription disposal.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    private sealed class ManualObservable<T> : IObservable<T>
    {
        /// <summary>Contains current observers.</summary>
        private readonly List<IObserver<T>> _observers = [];

        /// <summary>Contains the most recently subscribed observer for terminal-race simulation.</summary>
        private IObserver<T>? _lastObserver;

        /// <summary>Gets the number of active subscriptions.</summary>
        public int ActiveSubscriptions => _observers.Count;

        /// <inheritdoc />
        public IDisposable Subscribe(IObserver<T> observer)
        {
            _observers.Add(observer);
            _lastObserver = observer;
            return new CallbackDisposable(() => _ = _observers.Remove(observer));
        }

        /// <summary>Publishes one value to active observers.</summary>
        /// <param name="value">The value to publish.</param>
        public void Publish(T value)
        {
            foreach (var observer in _observers.ToArray())
            {
                observer.OnNext(value);
            }
        }

        /// <summary>Completes active observers.</summary>
        public void Complete()
        {
            foreach (var observer in _observers.ToArray())
            {
                observer.OnCompleted();
            }
        }

        /// <summary>Fails active observers.</summary>
        /// <param name="error">The source error.</param>
        public void Fail(Exception error)
        {
            foreach (var observer in _observers.ToArray())
            {
                observer.OnError(error);
            }
        }

        /// <summary>Publishes through the retained observer to model a misbehaving source after unsubscription.</summary>
        /// <param name="value">The retained value to publish.</param>
        public void PublishRetained(T value) => _lastObserver?.OnNext(value);

        /// <summary>Completes through the retained observer to model disposal racing a terminal notification.</summary>
        public void CompleteRetained() => _lastObserver?.OnCompleted();
    }

    /// <summary>Runs one callback at most once.</summary>
    /// <param name="callback">The callback to run during disposal.</param>
    private sealed class CallbackDisposable(Action callback) : IDisposable
    {
        /// <summary>Contains the callback until disposal.</summary>
        private Action? _callback = callback;

        /// <inheritdoc />
        public void Dispose() => Interlocked.Exchange(ref _callback, null)?.Invoke();
    }

    /// <summary>Counts disposal calls for binding-session ownership tests.</summary>
    private sealed class CountingDisposable : IDisposable
    {
        /// <summary>Gets the number of times disposal was requested.</summary>
        public int DisposeCount { get; private set; }

        /// <inheritdoc />
        public void Dispose() => DisposeCount++;
    }

    /// <summary>Provides deterministic in-memory PLC reads and records binding writes.</summary>
    private sealed class DeterministicPlc : IRxS7
    {
        /// <summary>Contains byte buffers indexed by generated range tag name.</summary>
        private readonly ConcurrentDictionary<string, byte[]> _readBuffers =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Gets recorded writes.</summary>
        public ConcurrentQueue<(string Name, object? Value)> Writes { get; } = new();

        /// <inheritdoc />
        public string IP => "127.0.0.1";

        /// <inheritdoc />
        public IObservable<bool> IsConnected => Observable.Return(true);

        /// <inheritdoc />
        public bool IsConnectedValue => true;

        /// <inheritdoc />
        public IObservable<string> LastError => Observable.Empty<string>();

        /// <inheritdoc />
        public IObservable<ErrorCode> LastErrorCode => Observable.Empty<ErrorCode>();

        /// <inheritdoc />
        public IObservable<Tag?> ObserveAll => Observable.Empty<Tag?>();

        /// <inheritdoc />
        public CpuType PLCType => CpuType.S71500;

        /// <inheritdoc />
        public short Rack => 0;

        /// <inheritdoc />
        public short Slot => 1;

        /// <inheritdoc />
        public IObservable<bool> IsPaused => Observable.Return(false);

        /// <inheritdoc />
        public IObservable<string> Status => Observable.Empty<string>();

        /// <inheritdoc />
        public global::IoT.DriverCore.S7PlcRx.Tags TagList { get; } = [];

        /// <inheritdoc />
        public bool IsDisposed { get; private set; }

        /// <inheritdoc />
        public bool ShowWatchDogWriting { get; set; }

        /// <inheritdoc />
        public string? WatchDogAddress => null;

        /// <inheritdoc />
        public ushort WatchDogValueToWrite { get; set; }

        /// <inheritdoc />
        public int WatchDogWritingTime => 0;

        /// <inheritdoc />
        public IObservable<long> ReadTime => Observable.Empty<long>();

        /// <summary>Configures the response for a generated byte-range read.</summary>
        /// <param name="tagName">The generated range tag name.</param>
        /// <param name="bytes">The response bytes.</param>
        public void SetReadBuffer(string tagName, byte[] bytes) => _readBuffers[tagName] = bytes;

        /// <inheritdoc />
        public IObservable<T?> Observe<T>(LogicalTagKey<T> tag) => Observable.Empty<T?>();

        /// <inheritdoc />
        public Task<T?> ReadAsync<T>(LogicalTagKey<T> tag) =>
            ReadAsync(tag, CancellationToken.None);

        /// <inheritdoc />
        public Task<T?> ReadAsync<T>(LogicalTagKey<T> tag, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (typeof(T) != typeof(byte[]))
            {
                return Task.FromResult(default(T));
            }

            if (!_readBuffers.TryGetValue(tag.Name, out var bytes))
            {
                return Task.FromResult(default(T));
            }

            object clone = bytes.ToArray();
            return Task.FromResult((T?)clone);
        }

        /// <inheritdoc />
        public void Value<T>(string? variable, T? value)
        {
            if (variable is null)
            {
                return;
            }

            Writes.Enqueue((variable, value));
        }

        /// <inheritdoc />
        public IObservable<string[]> GetCpuInfo() => Observable.Return<string[]>([]);

        /// <inheritdoc />
        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
