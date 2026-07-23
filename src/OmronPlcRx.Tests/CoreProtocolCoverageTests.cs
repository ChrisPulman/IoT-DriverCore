// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.OmronPlcRx.Core;
using IoT.DriverCore.OmronPlcRx.Core.Channels;
using IoT.DriverCore.OmronPlcRx.Core.Converters;
using IoT.DriverCore.OmronPlcRx.Core.Requests;
using IoT.DriverCore.OmronPlcRx.Core.Responses;
using IoT.DriverCore.OmronPlcRx.Core.Types;
using IoT.DriverCore.OmronPlcRx.Enums;
using TUnit.Core;

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>Tests deterministic protocol, conversion and validation paths.</summary>
public sealed partial class CoreProtocolCoverageTests
{
    /// <summary>Provides the controller model used by protocol fixtures.</summary>
    private const string ControllerModel = "CS1G-CPU42H";

    /// <summary>Provides the loopback host used by socket fixtures.</summary>
    private const string LoopbackHost = "127.0.0.1";

    /// <summary>Provides the common logical tag name.</summary>
    private const string SpeedTagName = "Speed";

    /// <summary>Provides the common UTC protocol date and time.</summary>
    private static readonly DateTimeOffset ProtocolDateTime =
        new(new DateOnly(2026, 6, 30), new TimeOnly(14, 25, 59), TimeSpan.Zero);

    /// <summary>Provides an unsupported PLC clock date before the valid range.</summary>
    private static readonly DateTimeOffset UnsupportedEarlyClockDate =
        new(new DateOnly(1997, 12, 31), TimeOnly.MinValue, TimeSpan.Zero);

    /// <summary>Provides an unsupported PLC clock date after the valid range.</summary>
    private static readonly DateTimeOffset UnsupportedLateClockDate =
        new(new DateOnly(2070, 1, 1), TimeOnly.MinValue, TimeSpan.Zero);

    /// <summary>Verifies BCD conversion round trips supported scalar widths.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task BcdConverter_RoundTripsSupportedScalarWidthsAsync()
    {
        const short expectedBcd16 = 1234;
        const ushort expectedBcdU16 = 9876;
        const int expectedBcd32 = 12_345_678;
        const uint expectedBcdU32 = 87_654_321U;
        const byte expectedBcdByte = 45;
        const byte expectedBcdByteEncoding = 0x45;

        var bcd16 = BCDConverter.GetBCDWord(expectedBcd16);
        var bcdU16 = BCDConverter.GetBCDWord(expectedBcdU16);
        var bcd32 = BCDConverter.GetBCDWords(expectedBcd32);
        var bcdU32 = BCDConverter.GetBCDWords(expectedBcdU32);
        await Assert.That(BCDConverter.ToByte(expectedBcdByteEncoding)).IsEqualTo(expectedBcdByte);
        await Assert.That(BCDConverter.GetBCDByte(expectedBcdByte)).IsEqualTo(expectedBcdByteEncoding);
        await Assert.That(BCDConverter.ToInt16(bcd16)).IsEqualTo(expectedBcd16);
        await Assert.That(BCDConverter.ToUInt16(bcdU16)).IsEqualTo(expectedBcdU16);
        await Assert.That(BCDConverter.ToInt32(bcd32[0], bcd32[1])).IsEqualTo(expectedBcd32);
        await Assert.That(BCDConverter.ToUInt32(bcdU32[0], bcdU32[1])).IsEqualTo(expectedBcdU32);
        await Assert.That(Convert.ToHexString(BCDConverter.GetBCDBytes(expectedBcd16))).IsEqualTo("3412");
        await Assert.That(Convert.ToHexString(BCDConverter.GetBCDBytes(expectedBcdU16))).IsEqualTo("7698");
        await Assert.That(Convert.ToHexString(BCDConverter.GetBCDBytes(expectedBcd32))).IsEqualTo("78563412");
        await Assert.That(Convert.ToHexString(BCDConverter.GetBCDBytes(expectedBcdU32))).IsEqualTo("21436587");
    }

    /// <summary>Verifies BCD conversion validates null and invalid byte lengths.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task BcdConverter_RejectsInvalidByteInputsAsync()
    {
        var invalidInt16Input = new byte[] { 0x12 };
        var invalidInt32Input = new byte[] { 0x12, 0x34 };
        var invalidUInt32Input = new byte[] { 0x12, 0x34, 0x56 };
        var oversizedUInt32Input = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x90 };
        await Assert.That(CaptureException<ArgumentNullException>(() => BCDConverter.ToInt16(null!))).IsNotNull();
        await Assert.That(CaptureException<ArgumentOutOfRangeException>(() => BCDConverter.ToInt16([]))).IsNotNull();
        await Assert.That(CaptureException<ArgumentNullException>(() => BCDConverter.ToUInt16(null!))).IsNotNull();
        var invalidUInt16Exception =
            CaptureException<ArgumentOutOfRangeException>(() => BCDConverter.ToUInt16(invalidInt16Input));
        await Assert.That(invalidUInt16Exception).IsNotNull();
        await Assert.That(CaptureException<ArgumentNullException>(() => BCDConverter.ToInt32(null!))).IsNotNull();
        var invalidInt32Exception =
            CaptureException<ArgumentOutOfRangeException>(() => BCDConverter.ToInt32(invalidInt32Input));
        await Assert.That(invalidInt32Exception).IsNotNull();
        await Assert.That(CaptureException<ArgumentNullException>(() => BCDConverter.ToUInt32(null!))).IsNotNull();
        var invalidUInt32Exception =
            CaptureException<ArgumentOutOfRangeException>(() => BCDConverter.ToUInt32(invalidUInt32Input));
        var oversizedUInt32Exception =
            CaptureException<ArgumentOutOfRangeException>(() => BCDConverter.ToUInt32(oversizedUInt32Input));
        await Assert.That(invalidUInt32Exception).IsNotNull();
        await Assert.That(oversizedUInt32Exception).IsNotNull();
    }

    /// <summary>Verifies PLC tag values convert to write words and back to typed read values.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task PlcTagValueCodec_ConvertsSupportedWordTypesAsync()
    {
        const byte expectedByte = 12;
        const ushort expectedUShort = 0xFEDC;
        const short expectedShort = -7;
        const short expectedBcd16 = 1234;
        const ushort expectedBcdU16 = 9876;
        const int expectedInteger = 0x12345678;
        const uint expectedUnsignedInteger = 0x89ABCDEFU;
        const float expectedSingle = 123.5F;
        const double expectedDouble = 123.5D;
        const int expectedBcd32 = 12_345_678;
        const uint expectedBcdU32 = 87_654_321U;
        await Assert.That(PlcTagValueCodec.TryGetSingleWord(typeof(byte), expectedByte, out var byteWord)).IsTrue();
        await Assert.That(byteWord).IsEqualTo((short)expectedByte);
        await Assert.That(
            PlcTagValueCodec.TryGetSingleWord(typeof(ushort), expectedUShort, out var ushortWord))
            .IsTrue();
        await Assert.That(Convert.ToHexString(BitConverter.GetBytes(ushortWord))).IsEqualTo("DCFE");
        await Assert.That(PlcTagValueCodec.TryGetSingleWord(typeof(short), expectedShort, out var shortWord)).IsTrue();
        await Assert.That(shortWord).IsEqualTo(expectedShort);
        await Assert.That(
            PlcTagValueCodec.TryGetSingleWord(typeof(Bcd16), new Bcd16(expectedBcd16), out var bcd16Word))
            .IsTrue();
        await Assert.That(BCDConverter.ToInt16(bcd16Word)).IsEqualTo(expectedBcd16);
        await Assert.That(
            PlcTagValueCodec.TryGetSingleWord(typeof(BcdU16), new BcdU16(expectedBcdU16), out var bcdU16Word))
            .IsTrue();
        await Assert.That(BCDConverter.ToUInt16(bcdU16Word)).IsEqualTo(expectedBcdU16);
        await Assert.That(PlcTagValueCodec.TryGetSingleWord(typeof(string), "x", out _)).IsFalse();
        await Assert.That(PlcTagValueCodec.TryGetWordArray(typeof(int), expectedInteger, out var intWords)).IsTrue();
        await Assert.That(PlcTagValueCodec.ConvertReadWords(typeof(int), intWords)).IsEqualTo(expectedInteger);
        await Assert.That(
            PlcTagValueCodec.TryGetWordArray(typeof(uint), expectedUnsignedInteger, out var uintWords))
            .IsTrue();
        await Assert.That(PlcTagValueCodec.ConvertReadWords(typeof(uint), uintWords))
            .IsEqualTo(expectedUnsignedInteger);
        await Assert.That(PlcTagValueCodec.TryGetWordArray(typeof(float), expectedSingle, out var floatWords)).IsTrue();
        await Assert.That(PlcTagValueCodec.ConvertReadWords(typeof(float), floatWords)).IsEqualTo(expectedSingle);
        await Assert.That(
            PlcTagValueCodec.TryGetWordArray(typeof(double), expectedDouble, out var doubleWords))
            .IsTrue();
        await Assert.That(PlcTagValueCodec.ConvertReadWords(typeof(double), doubleWords)).IsEqualTo(expectedDouble);
        await Assert.That(
            PlcTagValueCodec.TryGetWordArray(typeof(Bcd32), new Bcd32(expectedBcd32), out var bcd32Words))
            .IsTrue();
        var bcd32 = (Bcd32)PlcTagValueCodec.ConvertReadWords(typeof(Bcd32), bcd32Words);
        await Assert.That(bcd32.Value).IsEqualTo(expectedBcd32);
        await Assert.That(
            PlcTagValueCodec.TryGetWordArray(typeof(BcdU32), new BcdU32(expectedBcdU32), out var bcdU32Words))
            .IsTrue();
        var bcdU32 = (BcdU32)PlcTagValueCodec.ConvertReadWords(typeof(BcdU32), bcdU32Words);
        await Assert.That(bcdU32.Value).IsEqualTo(expectedBcdU32);
        await Assert.That(PlcTagValueCodec.TryGetWordArray(typeof(string), "x", out _)).IsFalse();
    }

    /// <summary>Verifies PLC tag string conversions handle null padding and length trimming.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task PlcTagValueCodec_ConvertsStringsAndReadWordCountsAsync()
    {
        const int paddedStringWordCount = 5;
        const int stringCharacterCount = 3;
        const int expectedShortWordCount = 1;
        const int expectedDoubleWordCount = 4;

        var words = PlcTagValueCodec.GetStringWords("ABCD", paddedStringWordCount);

        await Assert.That(Convert.ToHexString(ToBigEndianBytes(words))).IsEqualTo("414243440000");
        await Assert.That(PlcTagValueCodec.GetStringFromWords(words, paddedStringWordCount, stringCharacterCount))
            .IsEqualTo("ABCD");
        var unpaddedWords = new short[] { 0x4142, 0x4344 };
        const int unpaddedWordCount = expectedShortWordCount + expectedShortWordCount;
        await Assert.That(PlcTagValueCodec.GetStringFromWords(unpaddedWords, stringCharacterCount, unpaddedWordCount))
            .IsEqualTo("ABC");
        await Assert.That(PlcTagValueCodec.GetReadWordCount(typeof(short))).IsEqualTo(expectedShortWordCount);
        await Assert.That(PlcTagValueCodec.GetReadWordCount(typeof(double))).IsEqualTo(expectedDoubleWordCount);
        await Assert.That(PlcTagValueCodec.GetReadWordCount(typeof(string))).IsEqualTo(0);
        var bitIndexedStringException = CaptureException<NotSupportedException>(
            () => PlcTagValueCodec.ThrowIfBitIndexedString(expectedShortWordCount));
        var unsupportedDecimalException = CaptureException<NotSupportedException>(
            () => PlcTagValueCodec.ConvertReadWords(typeof(decimal), [expectedShortWordCount]));

        await Assert.That(bitIndexedStringException).IsNotNull();
        await Assert.That(unsupportedDecimalException).IsNotNull();
    }

    /// <summary>Verifies internal FINS request builders produce stable protocol bytes.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task FinsRequests_BuildExpectedMessageBytesAsync()
    {
        const int dataMemoryAddress = 100;
        const int wordCount = 2;
        const int bitAddress = 3;
        const int bitCount = 2;
        const short secondWriteWord = -2;
        const byte weekday = 2;

        using var plc = CreateConnection();
        var readWords = ReadMemoryAreaWordRequest.CreateNew(
            plc,
            dataMemoryAddress,
            wordCount,
            MemoryWordDataType.DataMemory);
        var writeWords = WriteMemoryAreaWordRequest.CreateNew(
            plc,
            dataMemoryAddress,
            MemoryWordDataType.DataMemory,
            [0x1234, secondWriteWord]);
        var readBits = ReadMemoryAreaBitRequest.CreateNew(
            plc,
            dataMemoryAddress,
            bitAddress,
            bitCount,
            MemoryBitDataType.CommonIO);
        var writeBits = WriteMemoryAreaBitRequest.CreateNew(
            plc,
            dataMemoryAddress,
            bitAddress,
            MemoryBitDataType.CommonIO,
            [true, false, true]);
        var writeClock = WriteClockRequest.CreateNew(
            plc,
            new DateTime(2026, 6, 30, 14, 25, 59, DateTimeKind.Utc),
            weekday);

        await Assert.That(ToHex(readWords.BuildMessage(0x44))).IsEqualTo("800002000200000100440101820064000002");
        var writeWordsMessage = ToHex(writeWords.BuildMessage(0x45));
        await Assert.That(writeWordsMessage).IsEqualTo("8000020002000001004501028200640000021234FFFE");
        await Assert.That(ToHex(readBits.BuildMessage(0x46))).IsEqualTo("800002000200000100460101300064030002");
        await Assert.That(ToHex(writeBits.BuildMessage(0x47))).IsEqualTo("800002000200000100470102300064030003010001");
        await Assert.That(ToHex(writeClock.BuildMessage(0x48))).IsEqualTo("80000200020000010048070226063014255902");
    }

    /// <summary>Verifies FINS response creation validates and extracts response payload data.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task FinsResponses_ValidateAndExtractPayloadsAsync()
    {
        const int dataMemoryAddress = 100;
        const int wordCount = 2;
        const int bitCount = 3;
        const byte expectedWeekday = 2;
        const double expectedAverageCycleTime = 12.3D;
        const double expectedMaximumCycleTime = 45.6D;

        using var plc = CreateConnection();
        var readWords = ReadMemoryAreaWordRequest.CreateNew(
            plc,
            dataMemoryAddress,
            wordCount,
            MemoryWordDataType.DataMemory);
        _ = readWords.BuildMessage(0x51);
        var wordResponse = CreateResponse(readWords, [0x12, 0x34, 0xFF, 0xFE]);

        var readBits = ReadMemoryAreaBitRequest.CreateNew(
            plc,
            dataMemoryAddress,
            0,
            bitCount,
            MemoryBitDataType.CommonIO);
        _ = readBits.BuildMessage(0x52);
        var bitResponse = CreateResponse(readBits, [1, 0, expectedWeekday]);

        var readClock = ReadClockRequest.CreateNew(plc);
        _ = readClock.BuildMessage(0x53);
        var clockResponse = CreateResponse(
            readClock,
            [0x26, 0x06, 0x30, 0x14, 0x25, 0x59, 0x02]);
        var clock = ReadClockResponse.ExtractClock(readClock, clockResponse);

        var cycleTime = ReadCycleTimeRequest.CreateNew(plc);
        _ = cycleTime.BuildMessage(0x54);
        var cycleResponse = CreateResponse(
            cycleTime,
            [0x00, 0x00, 0x01, 0x23, 0x00, 0x00, 0x04, 0x56, 0x00, 0x00, 0x00, 0x00]);
        var cycle = ReadCycleTimeResponse.ExtractCycleTime(cycleTime, cycleResponse);

        var cpuData = ReadCPUUnitDataRequest.CreateNew(plc);
        _ = cpuData.BuildMessage(0x55);
        var cpuResponse = CreateResponse(cpuData, BuildCpuUnitData(ControllerModel, "1.23"));
        var cpu = ReadCPUUnitDataResponse.ExtractData(cpuResponse);

        var extractedWords = ReadMemoryAreaWordResponse.ExtractValues(readWords, wordResponse);
        await Assert.That(Convert.ToHexString(ToBigEndianBytes(extractedWords))).IsEqualTo("1234FFFE");
        await Assert.That(ToBitText(ReadMemoryAreaBitResponse.ExtractValues(readBits, bitResponse))).IsEqualTo("1,0,1");
        await Assert.That(clock.ClockDateTime).IsEqualTo(
            new DateTime(2026, 6, 30, 14, 25, 59, DateTimeKind.Utc));
        await Assert.That(clock.DayOfWeek).IsEqualTo(expectedWeekday);
        await Assert.That(cycle.AverageCycleTime).IsEqualTo(expectedAverageCycleTime);
        await Assert.That(cycle.MaximumCycleTime).IsEqualTo(expectedMaximumCycleTime);
        await Assert.That(cycle.MinimumCycleTime).IsEqualTo(0D);
        await Assert.That(cpu.ControllerModel).IsEqualTo(ControllerModel);
        await Assert.That(cpu.ControllerVersion).IsEqualTo("1.23");
    }

    /// <summary>Verifies FINS response creation reports protocol mismatches and PLC response errors.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task FinsResponses_RejectInvalidFramesAndResponseCodesAsync()
    {
        const int dataMemoryAddress = 100;
        const int wordCount = 1;

        using var plc = CreateConnection();
        var request = ReadMemoryAreaWordRequest.CreateNew(
            plc,
            dataMemoryAddress,
            wordCount,
            MemoryWordDataType.DataMemory);
        _ = request.BuildMessage(0x61);

        var tooShortFrameException = CaptureException<FINSException>(
            () => FINSResponse.CreateNew(new byte[13], request));
        var invalidFunctionCodeFrame = BuildResponseFrame(request, [], functionCode: 0xFF);
        var invalidFunctionCodeException = CaptureException<FINSException>(
            () => FINSResponse.CreateNew(invalidFunctionCodeFrame, request));
        var invalidSubFunctionCodeFrame = BuildResponseFrame(request, [], subFunctionCode: 0xFF);
        var invalidSubFunctionCodeException = CaptureException<FINSException>(
            () => FINSResponse.CreateNew(invalidSubFunctionCodeFrame, request));
        var networkRelayFrame = BuildResponseFrame(request, [], mainResponseCode: 0x82);
        var networkRelayException = CaptureException<FINSException>(
            () => FINSResponse.CreateNew(networkRelayFrame, request));
        var timeoutFrame = BuildResponseFrame(request, [], mainResponseCode: 0x02, subResponseCode: 0x05);
        var timeoutException = CaptureException<FINSException>(
            () => FINSResponse.CreateNew(timeoutFrame, request));
        var serviceIdFrame = BuildResponseFrame(request, [], serviceId: 0x62);
        var serviceIdException = CaptureException<FINSException>(
            () => FINSResponse.CreateNew(serviceIdFrame, request));

        await Assert.That(tooShortFrameException.Message).Contains("too short");
        await Assert.That(invalidFunctionCodeException.Message).Contains("Invalid Function Code");
        await Assert.That(invalidSubFunctionCodeException.Message).Contains("Invalid Sub Function Code");
        await Assert.That(networkRelayException.Message).Contains("Network Relay");
        await Assert.That(timeoutException.Message).Contains("Response Timeout");
        await Assert.That(serviceIdException.Message).Contains("Service ID");
        await Assert.That(FINSResponse.ValidateFunctionCode(0x01) && !FINSResponse.ValidateFunctionCode(0xFF)).IsTrue();
        var validSubFunctionCode = FINSResponse.ValidateSubFunctionCode(0x01, 0x01);
        await Assert.That(validSubFunctionCode != FINSResponse.ValidateSubFunctionCode(0x01, 0xFF)).IsTrue();
    }

    /// <summary>Verifies response extractors validate short payloads.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ResponseExtractors_RejectShortPayloadsAsync()
    {
        const int dataMemoryAddress = 100;
        const int wordCount = 2;

        using var plc = CreateConnection();
        var readWords = ReadMemoryAreaWordRequest.CreateNew(
            plc,
            dataMemoryAddress,
            wordCount,
            MemoryWordDataType.DataMemory);
        _ = readWords.BuildMessage(0x71);
        var readBits = ReadMemoryAreaBitRequest.CreateNew(
            plc,
            dataMemoryAddress,
            0,
            wordCount,
            MemoryBitDataType.CommonIO);
        _ = readBits.BuildMessage(0x72);
        var readClock = ReadClockRequest.CreateNew(plc);
        _ = readClock.BuildMessage(0x73);
        var cycleTime = ReadCycleTimeRequest.CreateNew(plc);
        _ = cycleTime.BuildMessage(0x74);
        var cpuData = ReadCPUUnitDataRequest.CreateNew(plc);
        _ = cpuData.BuildMessage(0x75);
        var shortWordResponse = CreateResponse(readWords, [1]);
        var shortBitResponse = CreateResponse(readBits, [1]);
        var shortClockResponse = CreateResponse(readClock, [0x26]);
        var invalidClockResponse = CreateResponse(readClock, [0xA0, 0x01, 0x01, 0, 0, 0, 0]);
        var shortCycleResponse = CreateResponse(cycleTime, [0]);
        var shortCpuResponse = CreateResponse(cpuData, [0]);

        var shortWordException = CaptureException<FINSException>(
            () => ReadMemoryAreaWordResponse.ExtractValues(readWords, shortWordResponse));
        var shortBitException = CaptureException<FINSException>(
            () => ReadMemoryAreaBitResponse.ExtractValues(readBits, shortBitResponse));
        _ = CaptureException<FINSException>(
            () => ReadClockResponse.ExtractClock(readClock, shortClockResponse));
        _ = CaptureException<FINSException>(
            () => ReadClockResponse.ExtractClock(readClock, invalidClockResponse));
        _ = CaptureException<FINSException>(
            () => ReadCycleTimeResponse.ExtractCycleTime(cycleTime, shortCycleResponse));
        _ = CaptureException<FINSException>(
            () => ReadCPUUnitDataResponse.ExtractData(shortCpuResponse));
        await Assert.That(shortWordException is not null && shortBitException is not null).IsTrue();
    }

    /// <summary>Verifies write acknowledgement validators reject null requests and responses.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ResponseAcknowledgements_RejectNullInputsAsync()
    {
        const int dataMemoryAddress = 100;
        const byte weekday = 2;
        using var plc = CreateConnection();
        var writeWords = WriteMemoryAreaWordRequest.CreateNew(
            plc,
            dataMemoryAddress,
            MemoryWordDataType.DataMemory,
            [1]);
        var writeBits = WriteMemoryAreaBitRequest.CreateNew(
            plc,
            dataMemoryAddress,
            0,
            MemoryBitDataType.CommonIO,
            [true]);
        var writeClock = WriteClockRequest.CreateNew(
            plc,
            new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
            weekday);
        var response = CreateResponse(writeWords, []);

        var nullWordRequestException = CaptureException<ArgumentNullException>(
            () => WriteMemoryAreaWordResponse.Validate(null!, response));
        var nullWordResponseException = CaptureException<ArgumentNullException>(
            () => WriteMemoryAreaWordResponse.Validate(writeWords, null!));
        _ = CaptureException<ArgumentNullException>(
            () => WriteMemoryAreaBitResponse.Validate(null!, response));
        _ = CaptureException<ArgumentNullException>(
            () => WriteMemoryAreaBitResponse.Validate(writeBits, null!));
        _ = CaptureException<ArgumentNullException>(
            () => WriteClockResponse.Validate(null!, response));
        _ = CaptureException<ArgumentNullException>(
            () => WriteClockResponse.Validate(writeClock, null!));

        await Assert.That(nullWordRequestException is not null && nullWordResponseException is not null).IsTrue();
    }

    /// <summary>Verifies Host Link codec network framing and additional validation paths.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task HostLinkFinsFrameCodec_HandlesNetworkModeAndValidationFailuresAsync()
    {
        var options = new OmronSerialOptions("COM1") { FrameMode = OmronHostLinkFinsFrameMode.Network };
        var codec = new HostLinkFinsFrameCodec(options);
        var fins = Convert.FromHexString("800002000100000B00010101820064000002");
        var body = $"@00FA0{Convert.ToHexString(fins)}";
        var frame = $"{body}{HostLinkFinsFrameCodec.CalculateFcs(body)}*\r";
        var responseBody = $"@00FA00{Convert.ToHexString(fins)}";
        var responseFrame = $"{responseBody}{HostLinkFinsFrameCodec.CalculateFcs(responseBody)}*\r";

        await Assert.That(codec.EncodeRequest(fins)).IsEqualTo(frame);
        var decodedResponse = codec.DecodeResponse(responseFrame).ToArray();
        await Assert.That(Convert.ToHexString(decodedResponse)).IsEqualTo(Convert.ToHexString(fins));
        await Assert.That(CaptureException<ArgumentNullException>(() => _ = CreateHostLinkCodec(null!))).IsNotNull();
        var nullFcsException = CaptureException<ArgumentNullException>(
            () => HostLinkFinsFrameCodec.CalculateFcs(null!));
        await Assert.That(nullFcsException).IsNotNull();
        await Assert.That(CaptureException<ArgumentException>(() => codec.EncodeRequest(new byte[11]))).IsNotNull();
        await Assert.That(CaptureException<ArgumentNullException>(() => codec.DecodeResponse(null!))).IsNotNull();
        await Assert.That(CaptureException<OmronPLCException>(() => codec.DecodeResponse("@00FA0000"))).IsNotNull();
        await Assert.That(CaptureException<OmronPLCException>(() => codec.DecodeResponse("@00FA0000*\r"))).IsNotNull();
        const string invalidStartBody = "#00FA0000";
        const string invalidUnitBody = "@01FA0000";
        const string invalidHeaderBody = "@00FB0000";
        var invalidStartFrame = $"{invalidStartBody}{HostLinkFinsFrameCodec.CalculateFcs(invalidStartBody)}*\r";
        var invalidUnitFrame = $"{invalidUnitBody}{HostLinkFinsFrameCodec.CalculateFcs(invalidUnitBody)}*\r";
        var invalidHeaderFrame = $"{invalidHeaderBody}{HostLinkFinsFrameCodec.CalculateFcs(invalidHeaderBody)}*\r";
        var invalidStartException = CaptureException<OmronPLCException>(() => codec.DecodeResponse(invalidStartFrame));
        var invalidUnitException = CaptureException<OmronPLCException>(() => codec.DecodeResponse(invalidUnitFrame));
        var invalidHeaderException = CaptureException<OmronPLCException>(
            () => codec.DecodeResponse(invalidHeaderFrame));

        await Assert.That(invalidStartException.Message).Contains("'@'");
        await Assert.That(invalidUnitException.Message).Contains("unit");
        await Assert.That(invalidHeaderException.Message).Contains("header");

        var directCodec = new HostLinkFinsFrameCodec(new OmronSerialOptions("COM1"));
        const string ShortDirectBody = "@00FA00010203";
        var shortDirectFrame = $"{ShortDirectBody}{HostLinkFinsFrameCodec.CalculateFcs(ShortDirectBody)}*\r";
        var shortDirectException = CaptureException<OmronPLCException>(
            () => directCodec.DecodeResponse(shortDirectFrame));
        await Assert.That(shortDirectException.Message).Contains("too short");
    }

    /// <summary>Verifies serial option and connection metadata validation paths.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task SerialOptionsAndConnectionMetadata_ValidateInputsAsync()
    {
        const int invalidProtocolValue = 255;
        const int invalidHostLinkUnitNumber = 32;
        const int invalidResponseWaitTime = 16;
        const int invalidLowDataBits = 4;
        const int invalidHighDataBits = 9;
        const int validDestinationNode = 2;
        const int nodeIdentifierMaximum = 255;

        await Assert.That(CaptureException<ArgumentException>(() => _ = CreateSerialOptions(" "))).IsNotNull();
        var invalidProtocolOptions = new OmronSerialOptions("COM1")
        {
            Protocol = (OmronSerialProtocol)invalidProtocolValue,
        };
        var invalidUnitOptions = new OmronSerialOptions("COM1") { HostLinkUnitNumber = invalidHostLinkUnitNumber };
        var invalidResponseWaitOptions = new OmronSerialOptions("COM1") { ResponseWaitTime = invalidResponseWaitTime };
        var invalidLowDataBitsOptions = new OmronSerialOptions("COM1") { DataBits = invalidLowDataBits };
        var invalidHighDataBitsOptions = new OmronSerialOptions("COM1") { DataBits = invalidHighDataBits };
        var invalidFrameLengthOptions = new OmronSerialOptions("COM1") { MaximumFrameLength = 0 };

        await Assert.That(CaptureException<ArgumentOutOfRangeException>(invalidProtocolOptions.Validate)).IsNotNull();
        await Assert.That(CaptureException<ArgumentOutOfRangeException>(invalidUnitOptions.Validate)).IsNotNull();
        var invalidResponseWaitException = CaptureException<ArgumentOutOfRangeException>(
            invalidResponseWaitOptions.Validate);
        var invalidBaudRateException = CaptureException<ArgumentOutOfRangeException>(
            () => new OmronSerialOptions("COM1") { BaudRate = 0 }.Validate());
        var invalidLowDataBitsException = CaptureException<ArgumentOutOfRangeException>(
            invalidLowDataBitsOptions.Validate);
        var invalidHighDataBitsException = CaptureException<ArgumentOutOfRangeException>(
            invalidHighDataBitsOptions.Validate);
        var invalidFrameLengthException = CaptureException<ArgumentOutOfRangeException>(
            invalidFrameLengthOptions.Validate);
        var missingSourceNodeException = CaptureException<ArgumentOutOfRangeException>(
            () => OmronPLCConnectionMetadata.ValidateNodeIdentifiers(0, validDestinationNode, ConnectionMethod.UDP));
        var missingDestinationNodeException = CaptureException<ArgumentOutOfRangeException>(
            () => OmronPLCConnectionMetadata.ValidateNodeIdentifiers(1, 0, ConnectionMethod.UDP));
        var oversizedNodeException = CaptureException<ArgumentOutOfRangeException>(
            () => OmronPLCConnectionMetadata.ValidateNodeIdentifiers(1, nodeIdentifierMaximum, ConnectionMethod.UDP));
        var equalNodeException = CaptureException<ArgumentException>(
            () => OmronPLCConnectionMetadata.ValidateNodeIdentifiers(1, 1, ConnectionMethod.UDP));
        var nullRemoteHostException = CaptureException<ArgumentNullException>(
            () => OmronPLCConnectionMetadata.ValidateRemoteHost(null!));
        var emptyRemoteHostException = CaptureException<ArgumentException>(
            () => OmronPLCConnectionMetadata.ValidateRemoteHost(string.Empty));
        var invalidUdpPortException = CaptureException<ArgumentOutOfRangeException>(
            () => OmronPLCConnectionMetadata.ValidatePort(ConnectionMethod.UDP, 0));

        await Assert.That(invalidResponseWaitException).IsNotNull();
        await Assert.That(invalidBaudRateException).IsNotNull();
        await Assert.That(invalidLowDataBitsException).IsNotNull();
        await Assert.That(invalidHighDataBitsException).IsNotNull();
        await Assert.That(invalidFrameLengthException).IsNotNull();
        await Assert.That(missingSourceNodeException).IsNotNull();
        await Assert.That(missingDestinationNodeException).IsNotNull();
        await Assert.That(oversizedNodeException).IsNotNull();
        await Assert.That(equalNodeException).IsNotNull();
        await Assert.That(nullRemoteHostException).IsNotNull();
        await Assert.That(emptyRemoteHostException).IsNotNull();
        await Assert.That(invalidUdpPortException).IsNotNull();
    }

    /// <summary>Verifies serial connection metadata accepts valid values and identifies PLC model families.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task SerialConnectionMetadata_AcceptsValidValuesAndIdentifiesModelsAsync()
    {
        await Assert.That(OmronPLCConnectionMetadata.ValidateRemoteHost(LoopbackHost)).IsEqualTo(LoopbackHost);
        OmronPLCConnectionMetadata.ValidatePort(ConnectionMethod.Serial, 0);
        var modelsAreClassified = OmronPLCConnectionMetadata.GetPLCType(ControllerModel) == PlcType.C_Series &&
            OmronPLCConnectionMetadata.GetPLCType("NJ501-1300") == PlcType.NJ501;
        var extendedSeriesModel = OmronPLCConnectionMetadata.GetPLCType("NY532-5400");
        var remainingModelsAreClassified = extendedSeriesModel == PlcType.NJ_NX_NY_Series &&
            OmronPLCConnectionMetadata.GetPLCType("UNKNOWN") == PlcType.Unknown;
        await Assert.That(modelsAreClassified && remainingModelsAreClassified).IsTrue();
    }

    /// <summary>Verifies serial Toolbus frame-buffer helpers trim noise and find sync frames.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task SerialToolbusFrameBuffer_TrimsNoiseAndFindsSynchronizationFramesAsync()
    {
        var noisy = new List<byte> { 0x00, 0x01, 0xAB, 0x02 };
        SerialToolbusFrameBuffer.TrimBeforeFrameStart(noisy);

        var noFrame = new List<byte> { 0x00, 0x01 };
        SerialToolbusFrameBuffer.TrimBeforeFrameStart(noFrame);

        var aligned = new List<byte> { 0xAB, 0x02 };
        SerialToolbusFrameBuffer.TrimBeforeFrameStart(aligned);

        await Assert.That(Convert.ToHexString(noisy.ToArray())).IsEqualTo("AB02");
        await Assert.That(noFrame.Count).IsEqualTo(0);
        await Assert.That(Convert.ToHexString(aligned.ToArray())).IsEqualTo("AB02");
        var matchingSynchronizationFrame = SerialToolbusFrameBuffer.ContainsSynchronizationFrame(
            [0x00, 0xAC, 0x01],
            [0xAC, 0x01]);
        var nonMatchingSynchronizationFrame = SerialToolbusFrameBuffer.ContainsSynchronizationFrame(
            [0x00, 0xAC, 0x02],
            [0xAC, 0x01]);
        await Assert.That(matchingSynchronizationFrame).IsTrue();
        await Assert.That(nonMatchingSynchronizationFrame).IsFalse();
    }
}
