// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using System.Text;

#if REACTIVE_SHIM

namespace IoT.Driver.MitsubishiRx.Reactive.Tests;

#else

namespace IoT.Driver.MitsubishiRx.Tests;

#endif

/// <summary>Exercises defensive type, frame, and simulator branches with deterministic inputs.</summary>
internal sealed class MitsubishiResidualBranchCoverageTests
{
    /// <summary>Stores private static reflection flags.</summary>
    private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

    /// <summary>Stores private instance reflection flags.</summary>
    private const BindingFlags PrivateInstance =
        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

    /// <summary>Stores private method names used by repeated reflection calls.</summary>
    private const string CanWriteTagValueMethod = "CanWriteTagValue";

    /// <summary>Stores the private numeric conversion method name.</summary>
    private const string ReadNumericTagValueMethod = "ReadNumericTagValue";

    /// <summary>Stores the private legacy point-count conversion method name.</summary>
    private const string ConvertPointCountMethod = "ConvertPointCountToLegacyByte";

    /// <summary>Stores the private binary 4C decoder method name.</summary>
    private const string Decode4CBinaryMethod = "Decode4CBinary";

    /// <summary>Stores the private bit parser method name.</summary>
    private const string ParseBitsMethod = "ParseBits";

    /// <summary>Stores the private type-name parser method name.</summary>
    private const string ParseTypeNameMethod = "ParseTypeName";

    /// <summary>Stores the private loopback parser method name.</summary>
    private const string ParseLoopbackMethod = "ParseLoopback";

    /// <summary>Stores the private word parser method name.</summary>
    private const string ParseWordsMethod = "ParseWords";

    /// <summary>Stores the private legacy expected-length method name.</summary>
    private const string GetOneEExpectedLengthMethod = "GetOneEExpectedLength";

    /// <summary>Stores the private rollout validation method name.</summary>
    private const string ValidateRolloutPolicyMethod = "ValidateRolloutPolicy";

    /// <summary>Stores the defensive tag name.</summary>
    private const string NearMissTagName = "NearMiss";

    /// <summary>Stores schema type names shared by test data.</summary>
    private const string StringDataType = "String";

    /// <summary>Stores the unsigned 16-bit schema type.</summary>
    private const string UInt16DataType = "UInt16";

    /// <summary>Stores the unsigned 32-bit schema type.</summary>
    private const string UInt32DataType = "UInt32";

    /// <summary>Stores the signed 32-bit schema type.</summary>
    private const string Int32DataType = "Int32";

    /// <summary>Stores the signed 16-bit schema type.</summary>
    private const string Int16DataType = "Int16";

    /// <summary>Stores the maximum legacy point count.</summary>
    private const int MaximumLegacyPointCount = 256;

    /// <summary>Stores an invalid legacy point count.</summary>
    private const int InvalidLegacyPointCount = MaximumLegacyPointCount + 1;

    /// <summary>Stores the default client timeout in seconds.</summary>
    private const int DefaultTimeoutSeconds = 4;

    /// <summary>Stores the octal radix.</summary>
    private const int OctalRadix = 8;

    /// <summary>Stores the hexadecimal radix.</summary>
    private const int HexadecimalRadix = 16;

    /// <summary>Stores the one-C response offsets.</summary>
    private const int OneCMinimumLength = 4;

    /// <summary>Stores the one-C ACK payload offset.</summary>
    private const int OneCAckPayloadStart = 3;

    /// <summary>Stores the three-C response offsets.</summary>
    private const int ThreeCMinimumLength = 6;

    /// <summary>Stores the three-C ACK payload offset.</summary>
    private const int ThreeCAckPayloadStart = 5;

    /// <summary>Stores the four-C response offsets.</summary>
    private const int FourCMinimumLength = 12;

    /// <summary>Stores the four-C ACK payload offset.</summary>
    private const int FourCAckPayloadStart = 11;

    /// <summary>Stores the deterministic MC port.</summary>
    private const int McPort = 5000;

    /// <summary>Stores a two-byte loopback length.</summary>
    private const int LoopbackLength = 2;

    /// <summary>Stores the defensive two-word test payload.</summary>
    private static readonly ushort[] DefensiveTwoWordPayload = [1, 0];

    /// <summary>Stores a single unsigned word with all bits set.</summary>
    private static readonly ushort[] UnsignedMaxWord = [0xFFFF];

    /// <summary>Stores the representative unsigned 32-bit word payload.</summary>
    private static readonly ushort[] UInt32WordPayload = [0x1122, 0x3344];

    /// <summary>Stores the little-endian string word payload.</summary>
    private static readonly ushort[] LittleEndianStringWordPayload = [0x4241];

    /// <summary>Stores the big-endian string word payload.</summary>
    private static readonly ushort[] BigEndianStringWordPayload = [0x4142];

    /// <summary>Stores the single bit word payload.</summary>
    private static readonly ushort[] BitWordPayload = [1];

    /// <summary>Stores the populated word block payload.</summary>
    private static readonly ushort[] PopulatedWordBlockPayload = [1];

    /// <summary>Stores the populated bit block payload.</summary>
    private static readonly bool[] PopulatedBitBlockPayload = [true];

    /// <summary>Stores every valid scalar type and representative raw words.</summary>
    private static readonly (string? DataType, object Value, ushort[] Words)[] ScalarCases =
    [
        ("Bit", true, [1]),
        (StringDataType, "AB", [0x4241]),
        ("Float", 1.0F, [0, 0x3F80]),
        ("DWord", 1U, [1, 0]),
        (UInt32DataType, 1U, [1, 0]),
        (Int32DataType, 1, [1, 0]),
        (Int16DataType, (short)-1, [0xFFFF]),
        (UInt16DataType, (ushort)1, [1]),
        ("Word", (ushort)1, [1]),
        (null, (ushort)1, [1]),
    ];

    /// <summary>Stores every legacy 1E command mapping.</summary>
    private static readonly (ushort Command, ushort Subcommand)[] LegacyCommands =
    [
        (MitsubishiCommandCodes.DeviceRead, 0x0000),
        (MitsubishiCommandCodes.DeviceRead, 0x0001),
        (MitsubishiCommandCodes.DeviceWrite, 0x0002),
        (MitsubishiCommandCodes.DeviceWrite, 0x0003),
        (MitsubishiCommandCodes.RandomWrite, 0x0001),
        (MitsubishiCommandCodes.RandomWrite, 0x0000),
        (MitsubishiCommandCodes.EntryMonitorDevice, 0x0000),
        (MitsubishiCommandCodes.ExecuteMonitor, 0x0000),
        (MitsubishiCommandCodes.RemoteRun, 0x0000),
        (MitsubishiCommandCodes.RemoteStop, 0x0000),
        (MitsubishiCommandCodes.ReadTypeName, 0x0000),
        (MitsubishiCommandCodes.LoopbackTest, 0x0000),
    ];

    /// <summary>Invokes the simulator's span-based availability guard without reflection boxing.</summary>
    /// <param name="bytes">The available bytes.</param>
    /// <param name="offset">The requested offset.</param>
    /// <param name="length">The requested length.</param>
    private delegate void EnsureAvailableInvoker(
        ReadOnlySpan<byte> bytes,
        int offset,
        int length);

    /// <summary>Verifies all scalar type switches and their defensive fallbacks.</summary>
    /// <returns>A task that completes after the mappings have been verified.</returns>
    [Test]
    internal async Task ScalarMappingsExerciseEveryTypeAndFallbackAsync()
    {
        foreach (var scalar in ScalarCases)
        {
            var tag = CreateTag(scalar.DataType);
            var validArguments = new object?[] { tag, scalar.Value, null };
            var invalidArguments = new object?[] { tag, new object(), null };
            await Assert.That((bool)InvokeStatic(
                typeof(MitsubishiRx),
                CanWriteTagValueMethod,
                validArguments)!).IsTrue();
            await Assert.That((bool)InvokeStatic(
                typeof(MitsubishiRx),
                CanWriteTagValueMethod,
                invalidArguments)!).IsFalse();
        }

        var nullArguments = new object?[] { CreateTag(UInt16DataType), null, null };
        await Assert.That((bool)InvokeStatic(
            typeof(MitsubishiRx),
            CanWriteTagValueMethod,
            nullArguments)!).IsFalse();
        ExerciseNumericConversionBranches();
    }

    /// <summary>Verifies string-switch equality guards with same-shape non-matching type names.</summary>
    /// <returns>A task that completes after every guarded string shape has been exercised.</returns>
    [Test]
    internal async Task DataTypeSwitchNearMissesExerciseEqualityGuardsAsync()
    {
        await using var simulator = new MitsubishiSimulatorTransport();
        await using var client = new MitsubishiRx(CreateMcOptions(), simulator, Scheduler.Immediate);

        foreach (string dataType in CreateDataTypeNearMisses())
        {
            var tag = CreateTag(dataType);
            var canWriteArguments = new object?[] { tag, new object(), null };
            var canWrite = (bool)InvokeStatic(
                typeof(MitsubishiRx),
                CanWriteTagValueMethod,
                canWriteArguments)!;
            await Assert.That(canWrite).IsFalse();

            _ = Assert.Throws<TargetInvocationException>(
                () => InvokeStatic(
                    typeof(MitsubishiRx),
                    ReadNumericTagValueMethod,
                    tag,
                    DefensiveTwoWordPayload));
            _ = Assert.Throws<TargetInvocationException>(
                () => InvokeStatic(typeof(MitsubishiRx), "GetWordCountForScaledRead", tag));

            var converted = InvokeStatic(
                typeof(MitsubishiRx),
                "ConvertTagWordsToObject",
                tag,
                DefensiveTwoWordPayload);
            await Assert.That(converted).IsEqualTo((ushort)1);

            var writeTask = InvokeInstance(
                client,
                "CreateWriteTagValueTask",
                "Missing",
                tag,
                new(),
                CancellationToken.None);
            await Assert.That(writeTask).IsNull();
        }
    }

    /// <summary>Verifies the private tag read dispatcher with normalized-shape defensive type names.</summary>
    /// <returns>A task that completes after each read-switch equality guard has been exercised.</returns>
    [Test]
    internal async Task ReadTagDispatcherNearMissesExerciseEqualityGuardsAsync()
    {
        var database = new MitsubishiTagDatabase([]);
        var tagsField = typeof(MitsubishiTagDatabase).GetField("_tags", PrivateInstance)
            ?? throw new MissingFieldException(typeof(MitsubishiTagDatabase).FullName, "_tags");
        var tags = (Dictionary<string, MitsubishiTagDefinition>)tagsField.GetValue(database)!;
        await using var simulator = new MitsubishiSimulatorTransport();
        await using var client = new MitsubishiRx(CreateMcOptions(), simulator, Scheduler.Immediate)
        {
            TagDatabase = database,
        };

        foreach (string dataType in CreateDataTypeNearMisses())
        {
            tags[NearMissTagName] = new(NearMissTagName, "D0", dataType);
            var result = await client.ReadTagAsync(NearMissTagName, CancellationToken.None);
            await Assert.That(result.IsSucceed).IsTrue();
        }
    }

    /// <summary>Verifies private parser guard branches with failed, null, short, and valid payloads.</summary>
    /// <returns>A task that completes after every parser shape has been exercised.</returns>
    [Test]
    internal async Task ParserGuardsExerciseFailureShortAndFormatBranchesAsync()
    {
        var failed = new Responce<byte[]>().Fail("Expected failure.");
        var missing = new Responce<byte[]>();
        await using var binarySimulator = new MitsubishiSimulatorTransport();
        await using var binaryClient = new MitsubishiRx(
            CreateMcOptions(),
            binarySimulator,
            Scheduler.Immediate);

        var failedBits = InvokeInstance(binaryClient, ParseBitsMethod, failed, 1);
        var missingBits = InvokeInstance(binaryClient, ParseBitsMethod, missing, 1);
        var shortBits = InvokeInstance(
            binaryClient,
            ParseBitsMethod,
            new Responce<byte[]>([]),
            -1);
        await Assert.That(((Responce<bool[]>)failedBits!).IsSucceed).IsFalse();
        await Assert.That(((Responce<bool[]>)missingBits!).Value).IsNull();
        await Assert.That(((Responce<bool[]>)shortBits!).IsSucceed).IsFalse();

        var failedType = (Responce<MitsubishiTypeName>)InvokeInstance(
            binaryClient,
            ParseTypeNameMethod,
            failed)!;
        var shortBinaryType = (Responce<MitsubishiTypeName>)InvokeInstance(
            binaryClient,
            ParseTypeNameMethod,
            new Responce<byte[]>([]))!;
        var binaryType = (Responce<MitsubishiTypeName>)InvokeInstance(
            binaryClient,
            ParseTypeNameMethod,
            new Responce<byte[]>("CPU\0\x34\x12"u8.ToArray()))!;
        await Assert.That(failedType.IsSucceed).IsFalse();
        await Assert.That(shortBinaryType.Value!.ModelCode).IsEqualTo((ushort)0);
        await Assert.That(binaryType.Value!.ModelCode).IsEqualTo((ushort)0x1234);

        var asciiOptions = CreateMcOptions() with { DataCode = CommunicationDataCode.Ascii };
        await using var asciiSimulator = new MitsubishiSimulatorTransport();
        await using var asciiClient = new MitsubishiRx(asciiOptions, asciiSimulator, Scheduler.Immediate);
        var shortAsciiType = (Responce<MitsubishiTypeName>)InvokeInstance(
            asciiClient,
            ParseTypeNameMethod,
            new Responce<byte[]>("CPU"u8.ToArray()))!;
        var asciiType = (Responce<MitsubishiTypeName>)InvokeInstance(
            asciiClient,
            ParseTypeNameMethod,
            new Responce<byte[]>("CPU1234"u8.ToArray()))!;
        await Assert.That(shortAsciiType.Value!.ModelCode).IsEqualTo((ushort)0);
        await Assert.That(asciiType.Value!.ModelCode).IsEqualTo((ushort)0x1234);

        await ExerciseLoopbackAndWordParserGuardsAsync(binaryClient);
    }

    /// <summary>Verifies empty pipeline drains and protocol-address format branches.</summary>
    /// <returns>A task that completes after the defensive helpers have been exercised.</returns>
    [Test]
    internal async Task PipelineAndProtocolHelpersExerciseResidualBranchesAsync()
    {
        using var queued = new MitsubishiReactiveWritePipeline<int>(
            Scheduler.Immediate,
            "D0",
            MitsubishiReactiveWriteMode.Queued,
            static _ => Task.FromResult(new Responce()),
            null);
        using var latest = new MitsubishiReactiveWritePipeline<int>(
            Scheduler.Immediate,
            "D0",
            MitsubishiReactiveWriteMode.LatestWins,
            static _ => Task.FromResult(new Responce()),
            null);
        using var coalescing = new MitsubishiReactiveWritePipeline<int>(
            Scheduler.Immediate,
            "D0",
            MitsubishiReactiveWriteMode.Coalescing,
            static _ => Task.FromResult(new Responce()),
            TimeSpan.Zero);
        await Assert.That(await (Task<Unit>)InvokeInstance(queued, "DrainQueuedAsync")!).IsEqualTo(Unit.Default);
        await Assert.That(await (Task<Unit>)InvokeInstance(latest, "DrainLatestWinsAsync")!).IsEqualTo(Unit.Default);
        await Assert.That(await (Task<Unit>)InvokeInstance(coalescing, "FlushCoalescedAsync")!).IsEqualTo(Unit.Default);
        queued.Dispose();
        await Assert.That(queued.Mode).IsEqualTo(MitsubishiReactiveWriteMode.Queued);

        var address = MitsubishiDeviceAddress.Parse("D1", XyAddressNotation.Octal);
        foreach (var options in new[]
                 {
                     CreateMcOptions(),
                     CreateMcOptions() with { DataCode = CommunicationDataCode.Ascii },
                 })
        {
            foreach (bool legacy in new[] { false, true })
            {
                var buffer = new List<byte>();
                _ = InvokeStatic(
                    typeof(MitsubishiProtocolEncoding),
                    "AppendDeviceAddress",
                    buffer,
                    address,
                    options,
                    legacy);
                await Assert.That(buffer).Count().IsGreaterThan(0);
            }
        }
    }

    /// <summary>Verifies residual protocol, schema, transport, and simulator guard decisions.</summary>
    /// <returns>A task that completes after each private defensive branch has been exercised.</returns>
    [Test]
    internal async Task PrivateProtocolAndSimulatorGuardsExerciseEveryOutcomeAsync()
    {
        await ExerciseProtocolAndCsvGuardsAsync();
        var options = CreateMcOptions();
        await using var simulator = new MitsubishiSimulatorTransport();
        await ExerciseSimulatorGuardBranchesAsync(simulator, options);
        await ExerciseTransportAndRolloutGuardsAsync(simulator, options);
    }

    /// <summary>Verifies simple public fallback properties and validation branches.</summary>
    /// <returns>A task that completes after each result has been verified.</returns>
    [Test]
    internal async Task PublicFallbackPropertiesExerciseBothOutcomesAsync()
    {
        var emptyBlocks = new MitsubishiBlockRequest();
        var populatedBlocks = new MitsubishiBlockRequest(
            [new(MitsubishiDeviceAddress.Parse("D0", XyAddressNotation.Octal), PopulatedWordBlockPayload)],
            [new(MitsubishiDeviceAddress.Parse("M0", XyAddressNotation.Octal), PopulatedBitBlockPayload)]);
        await Assert.That(emptyBlocks.ResolvedWordBlocks).IsEmpty();
        await Assert.That(emptyBlocks.ResolvedBitBlocks).IsEmpty();
        await Assert.That(populatedBlocks.ResolvedWordBlocks).Count().IsEqualTo(1);
        await Assert.That(populatedBlocks.ResolvedBitBlocks).Count().IsEqualTo(1);

        var defaults = CreateMcOptions();
        var configured = defaults with
        {
            Timeout = TimeSpan.FromSeconds(1),
            Serial = new("COM1"),
        };
        await Assert.That(defaults.ResolvedTimeout).IsEqualTo(TimeSpan.FromSeconds(DefaultTimeoutSeconds));
        await Assert.That(configured.ResolvedTimeout).IsEqualTo(TimeSpan.FromSeconds(1));
        _ = Assert.Throws<InvalidOperationException>(() => _ = defaults.ResolvedSerial);
        await Assert.That(configured.ResolvedSerial.PortName).IsEqualTo("COM1");
        _ = Assert.Throws<FormatException>(
            static () => MitsubishiDeviceAddress.Parse("D!", XyAddressNotation.Octal));

        var variable = new MitsubishiDeviceMetadata(
            "X",
            0x009C,
            0x582A,
            DeviceValueKind.Bit,
            DeviceNumberFormat.XyVariable);
        await Assert.That(variable.GetRadix(XyAddressNotation.Octal)).IsEqualTo(OctalRadix);
        await Assert.That(variable.GetRadix(XyAddressNotation.Hexadecimal)).IsEqualTo(HexadecimalRadix);
        var invalid = variable with { NumberFormat = (DeviceNumberFormat)int.MaxValue };
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => invalid.GetRadix(XyAddressNotation.Octal));
    }

    /// <summary>Verifies all public tag dispatch branches through the stateful simulator.</summary>
    /// <returns>A task that completes after every declared tag type has been dispatched.</returns>
    [Test]
    internal async Task PublicTagDispatchExercisesEveryReadAndInvalidWriteBranchAsync()
    {
        var tags = CreateDispatchTags();
        var options = CreateMcOptions();
        await using var simulator = new MitsubishiSimulatorTransport();
        await using var client = new MitsubishiRx(options, simulator, Scheduler.Immediate)
        {
            TagDatabase = new(tags),
        };

        foreach (var tag in tags)
        {
            var read = await client.ReadTagAsync(tag.Name, CancellationToken.None);
            var invalidWrite = await client.WriteTagAsync(tag.Name, new(), CancellationToken.None);
            await Assert.That(read.IsSucceed).IsTrue();
            await Assert.That(invalidWrite.IsSucceed).IsFalse();
        }
    }

    /// <summary>Verifies the complete legacy command table and point-count boundaries.</summary>
    /// <returns>A task that completes after the command mappings have been verified.</returns>
    [Test]
    internal async Task LegacyProtocolMappingsExerciseEveryCommandAndBoundaryAsync()
    {
        foreach (var command in LegacyCommands)
        {
            var result = (byte)InvokeStatic(
                typeof(MitsubishiProtocolEncoding),
                "Get1ECommand",
                command.Command,
                command.Subcommand)!;
            await Assert.That(result).IsNotEqualTo(byte.MaxValue);
        }

        await Assert.That((byte)InvokeStatic(
            typeof(MitsubishiProtocolEncoding),
            ConvertPointCountMethod,
            1)!).IsEqualTo((byte)1);
        await Assert.That((byte)InvokeStatic(
            typeof(MitsubishiProtocolEncoding),
            ConvertPointCountMethod,
            MaximumLegacyPointCount)!).IsEqualTo((byte)0);
        _ = Assert.Throws<TargetInvocationException>(
            static () => InvokeStatic(typeof(MitsubishiProtocolEncoding), ConvertPointCountMethod, 0));
        _ = Assert.Throws<TargetInvocationException>(
            static () => InvokeStatic(
                typeof(MitsubishiProtocolEncoding),
                ConvertPointCountMethod,
                InvalidLegacyPointCount));
    }

    /// <summary>Verifies serial response decoders across short, NAK, ACK, and binary guard paths.</summary>
    /// <returns>A task that completes after every decoder guard has been exercised.</returns>
    [Test]
    internal async Task SerialDecodersExerciseShortErrorAndPayloadBranchesAsync()
    {
        var oneC = CreateSerialOptions(MitsubishiFrameType.OneC, MitsubishiSerialMessageFormat.Format1);
        var threeC = CreateSerialOptions(MitsubishiFrameType.ThreeC, MitsubishiSerialMessageFormat.Format1);
        var fourC = CreateSerialOptions(MitsubishiFrameType.FourC, MitsubishiSerialMessageFormat.Format1);
        var binary = CreateSerialOptions(
            MitsubishiFrameType.FourC,
            MitsubishiSerialMessageFormat.Format5,
            CommunicationDataCode.Binary);

        ExerciseAsciiDecoder(
            "Decode1C",
            oneC,
            OneCMinimumLength,
            OneCAckPayloadStart,
            OneCMinimumLength);
        ExerciseAsciiDecoder(
            "Decode3C",
            threeC,
            ThreeCMinimumLength,
            ThreeCAckPayloadStart,
            ThreeCMinimumLength);
        ExerciseAsciiDecoder(
            "Decode4CAscii",
            fourC.ResolvedSerial.MessageFormat,
            FourCMinimumLength,
            FourCAckPayloadStart,
            FourCMinimumLength);
        ExerciseBinaryDecoder(binary);

        _ = InvokeStatic(typeof(MitsubishiSerialProtocolEncoding), "EnsureAscii", oneC);
        _ = Assert.Throws<TargetInvocationException>(
            () => InvokeStatic(typeof(MitsubishiSerialProtocolEncoding), "EnsureAscii", binary));
        await Assert.That((string)InvokeStatic(
            typeof(MitsubishiSerialProtocolEncoding),
            "FormatPointCount",
            MaximumLegacyPointCount)!).IsEqualTo("00");
    }

    /// <summary>Verifies simulator command aliases and every legacy decoder command.</summary>
    /// <returns>A task that completes after the simulator mappings have been invoked.</returns>
    [Test]
    internal async Task StatefulSimulatorMappingsExerciseAliasesAndFallbackAsync()
    {
        await using var simulator = new MitsubishiSimulatorTransport();
        var options = CreateMcOptions();
        ushort[] simpleCommands =
        [
            MitsubishiCommandCodes.RemotePause,
            MitsubishiCommandCodes.RemoteReset,
            MitsubishiCommandCodes.ClearError,
            0xFFFF,
        ];
        foreach (ushort command in simpleCommands)
        {
            var request = CreateDecodedSimulatorRequest(command);
            var result = (byte[])InvokeInstance(
                simulator,
                "ExecuteDecodedRequest",
                options,
                request)!;
            await Assert.That(result).IsNotNull();
        }

        foreach (byte command in new byte[] { 0x00, 0x01, 0x02, 0x03, 0x06, 0x08, 0x13, 0x14, 0x15, 0x16, 0xFF })
        {
            var payload = new byte[4];
            payload[0] = command;
            var decoded = InvokeStatic(typeof(MitsubishiSimulatorTransport), "DecodeLegacyMcRequest", options, payload);
            await Assert.That(decoded).IsNotNull();
        }

        await ExerciseDecodedCommandNearMissesAsync(simulator, options);
    }

    /// <summary>Exercises command-map and CSV parsing guard branches.</summary>
    /// <returns>A task that completes after the guard results have been verified.</returns>
    private static async Task ExerciseProtocolAndCsvGuardsAsync()
    {
        foreach (var command in new[]
                 {
                     MitsubishiCommandCodes.DeviceRead,
                     MitsubishiCommandCodes.DeviceWrite,
                     MitsubishiCommandCodes.RandomWrite,
                 })
        {
            _ = Assert.Throws<TargetInvocationException>(
                () => InvokeStatic(
                    typeof(MitsubishiProtocolEncoding),
                    "Get1ECommand",
                    command,
                    ushort.MaxValue));
        }

        var parseCsvTag = typeof(MitsubishiTagDatabase).GetMethod("ParseCsvTag", PrivateStatic)
            ?? throw new MissingMethodException(typeof(MitsubishiTagDatabase).FullName, "ParseCsvTag");
        object?[] emptyArguments =
        [
            string.Empty,
            1,
            new Dictionary<string, int>(StringComparer.Ordinal),
            null,
        ];
        object?[] whitespaceArguments =
        [
            " , ",
            2,
            new Dictionary<string, int>(StringComparer.Ordinal),
            null,
        ];
        await Assert.That(parseCsvTag.Invoke(null, emptyArguments)).IsNull();
        await Assert.That(parseCsvTag.Invoke(null, whitespaceArguments)).IsNull();
        await Assert.That((string)InvokeStatic(
            typeof(MitsubishiTagDatabase),
            "EscapeCsv",
            "plain")!).IsEqualTo("plain");
        await Assert.That((string)InvokeStatic(
            typeof(MitsubishiTagDatabase),
            "EscapeCsv",
            "with,comma")!).IsEqualTo("\"with,comma\"");
    }

    /// <summary>Exercises simulator decoder and span-boundary guards.</summary>
    /// <param name="simulator">The simulator instance.</param>
    /// <param name="options">The Ethernet options.</param>
    /// <returns>A task that completes after decoded values have been verified.</returns>
    private static async Task ExerciseSimulatorGuardBranchesAsync(
        MitsubishiSimulatorTransport simulator,
        MitsubishiClientOptions options)
    {
        _ = Assert.Throws<TargetInvocationException>(
            () => InvokeInstance(simulator, "ExecuteMonitor"));
        var legacyLoopback = (byte[])InvokeStatic(
            typeof(MitsubishiSimulatorTransport),
            "ExecuteLoopback",
            CreateDecodedSimulatorRequest(
                MitsubishiCommandCodes.LoopbackTest,
                body: "02AB"u8.ToArray(),
                isAscii: true,
                isLegacy: true))!;
        var modernLoopback = (byte[])InvokeStatic(
            typeof(MitsubishiSimulatorTransport),
            "ExecuteLoopback",
            CreateDecodedSimulatorRequest(
                MitsubishiCommandCodes.LoopbackTest,
                body: "0002AB"u8.ToArray(),
                isAscii: true))!;
        await Assert.That(Encoding.ASCII.GetString(legacyLoopback)).IsEqualTo("AB");
        await Assert.That(Encoding.ASCII.GetString(modernLoopback)).IsEqualTo("AB");

        _ = Assert.Throws<TargetInvocationException>(
            () => InvokeStatic(
                typeof(MitsubishiSimulatorTransport),
                "DecodeMcRequest",
                options with { FrameType = MitsubishiFrameType.ThreeC },
                Array.Empty<byte>()));
        _ = Assert.Throws<TargetInvocationException>(
            () => InvokeStatic(
                typeof(MitsubishiSimulatorTransport),
                "ReadDeviceAddress",
                options,
                CreateDecodedSimulatorRequest(
                    MitsubishiCommandCodes.DeviceRead,
                    body: [0, 0, 0, 0x7F]),
                0));
        _ = Assert.Throws<TargetInvocationException>(
            () => InvokeStatic(
                typeof(MitsubishiSimulatorTransport),
                "ReadDeviceAddress",
                options with { FrameType = MitsubishiFrameType.OneE },
                CreateDecodedSimulatorRequest(
                    MitsubishiCommandCodes.DeviceRead,
                    body: [0, 0, 0, 0, 0xFF, 0xFF],
                    isLegacy: true,
                    legacyCommand: 0),
                0));
        ExerciseAvailabilityGuards();
    }

    /// <summary>Exercises transport selection, encoding, observation, and rollout-policy guards.</summary>
    /// <param name="simulator">The simulator transport.</param>
    /// <param name="options">The Ethernet options.</param>
    /// <returns>A task that completes after disposable transports have been released.</returns>
    private static async Task ExerciseTransportAndRolloutGuardsAsync(
        MitsubishiSimulatorTransport simulator,
        MitsubishiClientOptions options)
    {
        _ = InvokeStatic(typeof(MitsubishiRx), "BuildEndPoint", options);
        _ = InvokeStatic(
            typeof(MitsubishiRx),
            "BuildEndPoint",
            options with { Host = "controller.local" });
        var tcpTransport = (IMitsubishiTransport)InvokeStatic(
            typeof(MitsubishiRx),
            "CreateDefaultTransport",
            options)!;
        var serialTransport = (IMitsubishiTransport)InvokeStatic(
            typeof(MitsubishiRx),
            "CreateDefaultTransport",
            CreateSerialOptions(MitsubishiFrameType.OneC, MitsubishiSerialMessageFormat.Format1))!;
        await tcpTransport.DisposeAsync();
        await serialTransport.DisposeAsync();

        foreach (string? encoding in new[] { "Utf8", "Utf16", "Ascii", null })
        {
            _ = InvokeStatic(
                typeof(MitsubishiRx),
                "GetTextEncoding",
                new MitsubishiTagDefinition("Text", "D0", StringDataType, Encoding: encoding));
        }

        await using var observableClient = new MitsubishiRx(options, simulator, Scheduler.Immediate);
        _ = observableClient.ObserveTagGroup("Group", TimeSpan.FromSeconds(1), null);
        _ = observableClient.ObserveTagGroup(
            "Group",
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(1));
        ExerciseRolloutPolicyGuards();
    }

    /// <summary>Exercises the rollout-policy switch and disallowed-change branch.</summary>
    private static void ExerciseRolloutPolicyGuards()
    {
        _ = InvokeStatic(
            typeof(MitsubishiRx),
            ValidateRolloutPolicyMethod,
            MitsubishiTagDatabaseDiff.Empty,
            MitsubishiTagRolloutPolicy.AllowAll);
        _ = InvokeStatic(
            typeof(MitsubishiRx),
            ValidateRolloutPolicyMethod,
            MitsubishiTagDatabaseDiff.Empty,
            MitsubishiTagRolloutPolicy.SafeMetadataAndGroups);
        _ = InvokeStatic(
            typeof(MitsubishiRx),
            ValidateRolloutPolicyMethod,
            new MitsubishiTagDatabaseDiff(
                [new MitsubishiTagDefinition("Added", "D0")],
                [],
                [],
                [],
                [],
                []),
            MitsubishiTagRolloutPolicy.SafeMetadataAndGroups);
        _ = InvokeStatic(
            typeof(MitsubishiRx),
            ValidateRolloutPolicyMethod,
            MitsubishiTagDatabaseDiff.Empty,
            (MitsubishiTagRolloutPolicy)int.MaxValue);
    }

    /// <summary>Exercises every operand of the simulator's span availability guard.</summary>
    private static void ExerciseAvailabilityGuards()
    {
        var ensureMethod = typeof(MitsubishiSimulatorTransport).GetMethod(
            "EnsureAvailable",
            PrivateStatic)
            ?? throw new MissingMethodException(
                typeof(MitsubishiSimulatorTransport).FullName,
                "EnsureAvailable");
        var ensureAvailable = ensureMethod.CreateDelegate<EnsureAvailableInvoker>();
        ensureAvailable([], 0, 0);
        _ = Assert.Throws<InvalidDataException>(() => ensureAvailable([], -1, 0));
        _ = Assert.Throws<InvalidDataException>(() => ensureAvailable([], 0, -1));
        _ = Assert.Throws<InvalidDataException>(() => ensureAvailable([], 1, 0));
    }

    /// <summary>Exercises decision paths surrounding every decoded simulator command.</summary>
    /// <param name="simulator">The simulator instance.</param>
    /// <param name="options">The client options.</param>
    /// <returns>A task that completes after the near misses have been verified.</returns>
    private static async Task ExerciseDecodedCommandNearMissesAsync(
        MitsubishiSimulatorTransport simulator,
        MitsubishiClientOptions options)
    {
        ushort[] decodedCommands =
        [
            MitsubishiCommandCodes.DeviceRead,
            MitsubishiCommandCodes.DeviceWrite,
            MitsubishiCommandCodes.RandomRead,
            MitsubishiCommandCodes.RandomWrite,
            MitsubishiCommandCodes.BlockRead,
            MitsubishiCommandCodes.BlockWrite,
            MitsubishiCommandCodes.EntryMonitorDevice,
            MitsubishiCommandCodes.ExecuteMonitor,
            MitsubishiCommandCodes.MemoryRead,
            MitsubishiCommandCodes.ExtendUnitRead,
            MitsubishiCommandCodes.MemoryWrite,
            MitsubishiCommandCodes.ExtendUnitWrite,
            MitsubishiCommandCodes.ReadTypeName,
            MitsubishiCommandCodes.RemoteRun,
            MitsubishiCommandCodes.RemoteStop,
            MitsubishiCommandCodes.RemotePause,
            MitsubishiCommandCodes.RemoteReset,
            MitsubishiCommandCodes.ClearError,
            MitsubishiCommandCodes.LoopbackTest,
        ];
        var commandSet = new HashSet<ushort>();
        foreach (ushort decodedCommand in decodedCommands)
        {
            _ = commandSet.Add(decodedCommand);
        }

        foreach (ushort command in decodedCommands)
        {
            foreach (ushort nearMiss in new[] { unchecked((ushort)(command - 1)), unchecked((ushort)(command + 1)) })
            {
                if (commandSet.Contains(nearMiss))
                {
                    continue;
                }

                var result = (byte[])InvokeInstance(
                    simulator,
                    "ExecuteDecodedRequest",
                    options,
                    CreateDecodedSimulatorRequest(nearMiss))!;
                await Assert.That(result).IsEmpty();
            }
        }
    }

    /// <summary>Creates same-length defensive values for all scalar data type switches.</summary>
    /// <returns>The non-matching data type values.</returns>
    private static string[] CreateDataTypeNearMisses()
    {
        var dataTypes = new List<string>();
        var seenDataTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scalar in ScalarCases)
        {
            if (scalar.DataType is { } dataType && seenDataTypes.Add(dataType))
            {
                dataTypes.Add(dataType);
            }
        }

        var nearMisses = new List<string>();
        foreach (string dataType in dataTypes)
        {
            for (var characterIndex = 0; characterIndex < dataType.Length; characterIndex++)
            {
                nearMisses.Add(string.Create(
                    dataType.Length,
                    (dataType, characterIndex),
                    static (characters, state) =>
                    {
                        state.dataType.AsSpan().CopyTo(characters);
                        characters[state.characterIndex] =
                            characters[state.characterIndex] == '?' ? '!' : '?';
                    }));
            }
        }

        return [.. nearMisses];
    }

    /// <summary>Exercises loopback, word parser, legacy monitor, and expected-length guard paths.</summary>
    /// <param name="ethernetClient">A deterministic Ethernet client.</param>
    /// <returns>A task that completes after the guards have been verified.</returns>
    private static async Task ExerciseLoopbackAndWordParserGuardsAsync(MitsubishiRx ethernetClient)
    {
        await ExerciseBasicParserGuardsAsync(ethernetClient);
        await ExerciseSerialLoopbackGuardsAsync(ethernetClient);
        await ExerciseLegacyOneCGuardsAsync();
    }

    /// <summary>Exercises common loopback and word parser guard paths.</summary>
    /// <param name="ethernetClient">A deterministic Ethernet client.</param>
    /// <returns>A task that completes after the guards have been verified.</returns>
    private static async Task ExerciseBasicParserGuardsAsync(MitsubishiRx ethernetClient)
    {
        var failed = new Responce<byte[]>().Fail("Expected failure.");
        var missing = new Responce<byte[]>();
        var failedLoopback = (Responce<byte[]>)InvokeInstance(
            ethernetClient,
            ParseLoopbackMethod,
            failed)!;
        var missingLoopback = (Responce<byte[]>)InvokeInstance(
            ethernetClient,
            ParseLoopbackMethod,
            missing)!;
        var ethernetLoopback = (Responce<byte[]>)InvokeInstance(
            ethernetClient,
            ParseLoopbackMethod,
            new Responce<byte[]>([1]))!;
        var failedWords = (Responce<ushort[]>)InvokeInstance(
            ethernetClient,
            ParseWordsMethod,
            failed,
            1)!;
        var missingWords = (Responce<ushort[]>)InvokeInstance(
            ethernetClient,
            ParseWordsMethod,
            missing,
            1)!;
        var shortWords = (Responce<ushort[]>)InvokeInstance(
            ethernetClient,
            ParseWordsMethod,
            new Responce<byte[]>([1]),
            1)!;
        await Assert.That(failedLoopback.IsSucceed).IsFalse();
        await Assert.That(missingLoopback.Value).IsNull();
        await Assert.That(ethernetLoopback.Value).IsEquivalentTo((byte[])[1]);
        await Assert.That(failedWords.IsSucceed).IsFalse();
        await Assert.That(missingWords.Value).IsNull();
        await Assert.That(shortWords.IsSucceed).IsFalse();
    }

    /// <summary>Exercises serial loopback and expected-length guard paths.</summary>
    /// <param name="ethernetClient">A deterministic Ethernet client.</param>
    /// <returns>A task that completes after the guards have been verified.</returns>
    private static async Task ExerciseSerialLoopbackGuardsAsync(MitsubishiRx ethernetClient)
    {
        await using var oneESimulator = new MitsubishiSimulatorTransport();
        await using var oneEClient = new MitsubishiRx(
            CreateMcOptions() with { FrameType = MitsubishiFrameType.OneE },
            oneESimulator,
            Scheduler.Immediate);
        await using var serialBinarySimulator = new MitsubishiSimulatorTransport();
        await using var serialBinaryClient = new MitsubishiRx(
            CreateSerialOptions(
                MitsubishiFrameType.FourC,
                MitsubishiSerialMessageFormat.Format5,
                CommunicationDataCode.Binary),
            serialBinarySimulator,
            Scheduler.Immediate);
        await using var serialAsciiSimulator = new MitsubishiSimulatorTransport();
        await using var serialAsciiClient = new MitsubishiRx(
            CreateSerialOptions(MitsubishiFrameType.FourC, MitsubishiSerialMessageFormat.Format1),
            serialAsciiSimulator,
            Scheduler.Immediate);

        await Assert.That((int?)InvokeInstance(ethernetClient, GetOneEExpectedLengthMethod, 1)).IsNull();
        await Assert.That((int?)InvokeInstance(oneEClient, GetOneEExpectedLengthMethod, 1)).IsEqualTo(1);
        await Assert.That((int?)InvokeInstance(serialAsciiClient, GetOneEExpectedLengthMethod, 1)).IsNull();
        await Assert.That((int?)InvokeInstance(ethernetClient, "GetSerialExpectedWordCount", 1)).IsNull();
        await Assert.That((int?)InvokeInstance(serialAsciiClient, "GetSerialExpectedWordCount", 1))
            .IsEqualTo(1);

        var shortBinary = (Responce<byte[]>)InvokeInstance(
            serialBinaryClient,
            ParseLoopbackMethod,
            new Responce<byte[]>([1]))!;
        var binary = (Responce<byte[]>)InvokeInstance(
            serialBinaryClient,
            ParseLoopbackMethod,
            new Responce<byte[]>([LoopbackLength, 0, 1]))!;
        var shortAscii = (Responce<byte[]>)InvokeInstance(
            serialAsciiClient,
            ParseLoopbackMethod,
            new Responce<byte[]>("001"u8.ToArray()))!;
        var invalidAsciiLength = (Responce<byte[]>)InvokeInstance(
            serialAsciiClient,
            ParseLoopbackMethod,
            new Responce<byte[]>("ZZZZDATA"u8.ToArray()))!;
        var emptyAscii = (Responce<byte[]>)InvokeInstance(
            serialAsciiClient,
            ParseLoopbackMethod,
            new Responce<byte[]>("0000"u8.ToArray()))!;
        await Assert.That(shortBinary.IsSucceed).IsFalse();
        await Assert.That(binary.Value).IsEquivalentTo((byte[])[1]);
        await Assert.That(shortAscii.IsSucceed).IsFalse();
        await Assert.That(invalidAsciiLength.Value).IsEmpty();
        await Assert.That(emptyAscii.Value).IsEmpty();
    }

    /// <summary>Exercises empty and invalid legacy one-C helper paths.</summary>
    /// <returns>A task that completes after the guards have been verified.</returns>
    private static async Task ExerciseLegacyOneCGuardsAsync()
    {
        await using var oneCSimulator = new MitsubishiSimulatorTransport();
        await using var oneCClient = new MitsubishiRx(
            CreateSerialOptions(MitsubishiFrameType.OneC, MitsubishiSerialMessageFormat.Format1),
            oneCSimulator,
            Scheduler.Immediate);
        var emptyWriteTask = (Task<Responce>)InvokeInstance(
            oneCClient,
            "RandomWriteWordsOneCAsync",
            (KeyValuePair<string, ushort>[])[],
            CancellationToken.None)!;
        var emptyMonitor = (Responce)InvokeInstance(
            oneCClient,
            "RegisterMonitorOneC",
            (object)(string[])[])!;
        var bitMonitor = (Responce)InvokeInstance(
            oneCClient,
            "RegisterMonitorOneC",
            (object)(string[])["M0"])!;
        var executeMonitorTask = (Task<Responce<byte[]>>)InvokeInstance(
            oneCClient,
            "ExecuteMonitorOneCAsync",
            CancellationToken.None)!;
        await Assert.That((await emptyWriteTask).IsSucceed).IsFalse();
        await Assert.That(emptyMonitor.IsSucceed).IsFalse();
        await Assert.That(bitMonitor.IsSucceed).IsFalse();
        await Assert.That((await executeMonitorTask).IsSucceed).IsFalse();
    }

    /// <summary>Exercises numeric conversion branches that are not observable through valid public inputs.</summary>
    private static void ExerciseNumericConversionBranches()
    {
        foreach (var scalar in ScalarCases)
        {
            if (scalar.DataType is "Bit" or StringDataType)
            {
                continue;
            }

            _ = InvokeStatic(
                typeof(MitsubishiRx),
                ReadNumericTagValueMethod,
                CreateTag(scalar.DataType),
                scalar.Words);
        }

        _ = InvokeStatic(
            typeof(MitsubishiRx),
            ReadNumericTagValueMethod,
            CreateTag("Word", signed: true),
            UnsignedMaxWord);
        _ = InvokeStatic(
            typeof(MitsubishiRx),
            "ConvertToUInt32",
            UInt32WordPayload,
            CreateTag(UInt32DataType));
        _ = InvokeStatic(
            typeof(MitsubishiRx),
            "ConvertToUInt32",
            UInt32WordPayload,
            CreateTag(UInt32DataType, byteOrder: MitsubishiMessages.BigEndian));
        _ = InvokeStatic(
            typeof(MitsubishiRx),
            "ConvertFromUInt32",
            0x11223344U,
            CreateTag(UInt32DataType));
        _ = InvokeStatic(
            typeof(MitsubishiRx),
            "ConvertFromUInt32",
            0x11223344U,
            CreateTag(UInt32DataType, byteOrder: MitsubishiMessages.BigEndian));
        _ = InvokeStatic(
            typeof(MitsubishiRx),
            "DecodeStringFromWords",
            LittleEndianStringWordPayload,
            CreateTag(StringDataType));
        _ = InvokeStatic(
            typeof(MitsubishiRx),
            "DecodeStringFromWords",
            BigEndianStringWordPayload,
            CreateTag(StringDataType, byteOrder: MitsubishiMessages.BigEndian));
        _ = Assert.Throws<TargetInvocationException>(
            static () => InvokeStatic(
                typeof(MitsubishiRx),
                ReadNumericTagValueMethod,
                CreateTag("Bit"),
                BitWordPayload));
    }

    /// <summary>Exercises one ASCII serial decoder with deterministic text shapes.</summary>
    /// <param name="methodName">The decoder method.</param>
    /// <param name="optionsOrFormat">The decoder's first argument.</param>
    /// <param name="minimumLength">The minimum accepted text length.</param>
    /// <param name="ackPayloadStart">The ACK payload offset.</param>
    /// <param name="otherPayloadStart">The non-ACK payload offset.</param>
    private static void ExerciseAsciiDecoder(
        string methodName,
        object optionsOrFormat,
        int minimumLength,
        int ackPayloadStart,
        int otherPayloadStart)
    {
        _ = InvokeStatic(
            typeof(MitsubishiSerialProtocolEncoding),
            methodName,
            optionsOrFormat,
            Frame("X"));
        _ = InvokeStatic(
            typeof(MitsubishiSerialProtocolEncoding),
            methodName,
            optionsOrFormat,
            Frame($"{(char)0x15}{new string('A', minimumLength - 1)}"));
        _ = InvokeStatic(
            typeof(MitsubishiSerialProtocolEncoding),
            methodName,
            optionsOrFormat,
            Frame($"{(char)0x15}{new string('A', minimumLength - 1)}51"));
        _ = InvokeStatic(
            typeof(MitsubishiSerialProtocolEncoding),
            methodName,
            optionsOrFormat,
            Frame($"{(char)0x06}{new string('A', ackPayloadStart - 1)}DATA"));
        _ = InvokeStatic(
            typeof(MitsubishiSerialProtocolEncoding),
            methodName,
            optionsOrFormat,
            Frame($"X{new string('A', otherPayloadStart - 1)}DATA"));
    }

    /// <summary>Exercises the binary 4C decoder's validation branches.</summary>
    /// <param name="options">Binary serial options.</param>
    private static void ExerciseBinaryDecoder(MitsubishiClientOptions options)
    {
        _ = InvokeStatic(
            typeof(MitsubishiSerialProtocolEncoding),
            Decode4CBinaryMethod,
            Array.Empty<byte>());
        var incomplete = new byte[8];
        incomplete[0] = 0x10;
        incomplete[1] = 0x02;
        _ = InvokeStatic(typeof(MitsubishiSerialProtocolEncoding), Decode4CBinaryMethod, incomplete);

        var invalidIdentifier = MitsubishiSimulatorTransport.CreateSuccessResponse(options, []);
        invalidIdentifier[12] ^= 0xFF;
        _ = InvokeStatic(
            typeof(MitsubishiSerialProtocolEncoding),
            Decode4CBinaryMethod,
            invalidIdentifier);
        var plcError = MitsubishiSimulatorTransport.CreateSuccessResponse(options, []);
        plcError[14] = 1;
        _ = InvokeStatic(typeof(MitsubishiSerialProtocolEncoding), Decode4CBinaryMethod, plcError);
        _ = InvokeStatic(
            typeof(MitsubishiSerialProtocolEncoding),
            Decode4CBinaryMethod,
            MitsubishiSimulatorTransport.CreateSuccessResponse(options, []));
    }

    /// <summary>Creates tags covering every public dispatch arm.</summary>
    /// <returns>The tag definitions.</returns>
    private static MitsubishiTagDefinition[] CreateDispatchTags() =>
    [
        new("Bit", "M0", "Bit"),
        new(StringDataType, "D0", StringDataType, Length: 2),
        new("Float", "D10", "Float"),
        new("DWord", "D20", "DWord"),
        new(UInt32DataType, "D30", UInt32DataType),
        new(Int32DataType, "D40", Int32DataType),
        new(Int16DataType, "D50", Int16DataType),
        new(UInt16DataType, "D60", UInt16DataType),
        new("Word", "D70", "Word"),
        new("DefaultWord", "D80"),
        new("Engineering", "D90", "Word", Scale: 2.0, Offset: 1.0),
    ];

    /// <summary>Creates a representative tag definition.</summary>
    /// <param name="dataType">The data type.</param>
    /// <param name="signed">Whether words are signed.</param>
    /// <param name="byteOrder">The byte order.</param>
    /// <returns>The tag.</returns>
    private static MitsubishiTagDefinition CreateTag(
        string? dataType,
        bool signed = false,
        string? byteOrder = null) =>
        new("Value", dataType == "Bit" ? "M0" : "D0", dataType, Length: 2, Signed: signed, ByteOrder: byteOrder);

    /// <summary>Creates deterministic Ethernet options.</summary>
    /// <returns>The options.</returns>
    private static MitsubishiClientOptions CreateMcOptions() =>
        new(
            "127.0.0.1",
            McPort,
            MitsubishiFrameType.ThreeE,
            CommunicationDataCode.Binary,
            MitsubishiTransportKind.Tcp);

    /// <summary>Creates deterministic serial options.</summary>
    /// <param name="frameType">The frame type.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="dataCode">The data code.</param>
    /// <returns>The options.</returns>
    private static MitsubishiClientOptions CreateSerialOptions(
        MitsubishiFrameType frameType,
        MitsubishiSerialMessageFormat messageFormat,
        CommunicationDataCode dataCode = CommunicationDataCode.Ascii) =>
        new(
            "COM1",
            0,
            frameType,
            dataCode,
            MitsubishiTransportKind.Serial,
            Serial: new MitsubishiSerialOptions("COM1", MessageFormat: messageFormat));

    /// <summary>Creates a checksum-bearing ASCII test frame.</summary>
    /// <param name="text">The decoded text.</param>
    /// <returns>The frame.</returns>
    private static byte[] Frame(string text) => Encoding.ASCII.GetBytes($"{text}00");

    /// <summary>Creates a private simulator request record.</summary>
    /// <param name="command">The command.</param>
    /// <param name="subcommand">The subcommand.</param>
    /// <param name="body">The decoded request body.</param>
    /// <param name="isAscii">Whether the body is ASCII encoded.</param>
    /// <param name="isLegacy">Whether the body uses legacy framing.</param>
    /// <param name="legacyCommand">The optional legacy command byte.</param>
    /// <returns>The request record.</returns>
    private static object CreateDecodedSimulatorRequest(
        ushort command,
        ushort subcommand = 0,
        byte[]? body = null,
        bool isAscii = false,
        bool isLegacy = false,
        byte? legacyCommand = null)
    {
        var requestType = typeof(MitsubishiSimulatorTransport).GetNestedType(
            "DecodedSimulatorRequest",
            BindingFlags.NonPublic)!;
        return Activator.CreateInstance(
            requestType,
            PrivateInstance,
            binder: null,
            args:
            [
                command,
                subcommand,
                body ?? Array.Empty<byte>(),
                isAscii,
                isLegacy,
                legacyCommand,
            ],
            culture: null)!;
    }

    /// <summary>Invokes one private static method.</summary>
    /// <param name="type">The declaring type.</param>
    /// <param name="methodName">The method name.</param>
    /// <param name="arguments">The method arguments.</param>
    /// <returns>The method result.</returns>
    private static object? InvokeStatic(Type type, string methodName, params object?[] arguments)
    {
        var method = type.GetMethod(methodName, PrivateStatic)
            ?? throw new MissingMethodException(type.FullName, methodName);
        return method.Invoke(null, arguments);
    }

    /// <summary>Invokes one private instance method.</summary>
    /// <param name="instance">The target instance.</param>
    /// <param name="methodName">The method name.</param>
    /// <param name="arguments">The method arguments.</param>
    /// <returns>The method result.</returns>
    private static object? InvokeInstance(object instance, string methodName, params object?[] arguments)
    {
        var method = instance.GetType().GetMethod(methodName, PrivateInstance)
            ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
        return method.Invoke(instance, arguments);
    }
}
