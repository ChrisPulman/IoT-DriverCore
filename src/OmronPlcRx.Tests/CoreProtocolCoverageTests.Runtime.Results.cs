// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.OmronPlcRx.Results;
using TUnit.Core;

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>Contains result and exception runtime coverage tests.</summary>
public sealed partial class CoreProtocolCoverageTests
{
    /// <summary>Verifies protocol exceptions expose constructor values.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Exceptions_ExposeConstructorValuesAsync()
    {
        const string finsExceptionMessage = "Exception of type 'IoT.DriverCore.OmronPlcRx.FINSException' was thrown.";
        const string plcExceptionMessage = "Exception of type 'IoT.DriverCore.OmronPlcRx.OmronPLCException' was thrown.";
        const string finsMessage = "fins";
        const string plcMessage = "plc";
        const string innerExceptionMessage = "inner";

        var finsInner = new InvalidOperationException(innerExceptionMessage);
        var plcInner = new InvalidOperationException(innerExceptionMessage);

        await Assert.That(new FINSException().Message).IsEqualTo(finsExceptionMessage);
        await Assert.That(new FINSException(finsMessage).Message).IsEqualTo(finsMessage);
        await Assert.That(new FINSException(finsMessage, finsInner).InnerException).IsEqualTo(finsInner);
        await Assert.That(new OmronPLCException().Message).IsEqualTo(plcExceptionMessage);
        await Assert.That(new OmronPLCException(plcMessage).Message).IsEqualTo(plcMessage);
        await Assert.That(new OmronPLCException(plcMessage, plcInner).InnerException).IsEqualTo(plcInner);
    }

    /// <summary>Verifies bit and word read results expose assigned metrics and values.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task BitAndWordReadResults_ExposeAssignedValuesAsync()
    {
        const short expectedReadWord = 123;
        const int expectedMetricSum = SingleWordCount + PairCount + TripleCount + FourCount;

        var readBits = new ReadBitsResult
        {
            BytesSent = SingleWordCount,
            PacketsSent = PairCount,
            BytesReceived = TripleCount,
            PacketsReceived = FourCount,
            Duration = ResultDuration,
            Values = [true],
        };
        var readWords = new ReadWordsResult
        {
            BytesSent = SingleWordCount,
            PacketsSent = PairCount,
            BytesReceived = TripleCount,
            PacketsReceived = FourCount,
            Duration = ResultDuration,
            Values = [expectedReadWord],
        };

        await Assert.That(
                readBits.BytesSent + readBits.PacketsSent + readBits.BytesReceived + readBits.PacketsReceived)
            .IsEqualTo(expectedMetricSum);
        await Assert.That(readBits.Duration).IsEqualTo(ResultDuration);
        await Assert.That(readBits.Values[0]).IsTrue();
        await Assert.That(readWords.Values[0]).IsEqualTo(expectedReadWord);
    }

    /// <summary>Verifies clock and cycle-time read results expose assigned values.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ClockAndCycleReadResults_ExposeAssignedValuesAsync()
    {
        const double minimumCycleTime = 1.2D;
        const double averageCycleTime = 2.3D;
        const double maximumCycleTime = 3.4D;

        var expectedClock = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var readClock = new ReadClockResult
        {
            BytesSent = SingleWordCount,
            PacketsSent = PairCount,
            BytesReceived = TripleCount,
            PacketsReceived = FourCount,
            Duration = ResultDuration,
            Clock = expectedClock,
            DayOfWeek = Weekday,
        };
        var readCycleTime = new ReadCycleTimeResult
        {
            BytesSent = SingleWordCount,
            PacketsSent = PairCount,
            BytesReceived = TripleCount,
            PacketsReceived = FourCount,
            Duration = ResultDuration,
            MinimumCycleTime = minimumCycleTime,
            MaximumCycleTime = maximumCycleTime,
            AverageCycleTime = averageCycleTime,
        };

        await Assert.That(readClock.Clock).IsEqualTo(expectedClock);
        await Assert.That(readClock.DayOfWeek).IsEqualTo(Weekday);
        await Assert.That(readCycleTime.MinimumCycleTime).IsEqualTo(minimumCycleTime);
        await Assert.That(readCycleTime.MaximumCycleTime).IsEqualTo(maximumCycleTime);
        await Assert.That(readCycleTime.AverageCycleTime).IsEqualTo(averageCycleTime);
    }

    /// <summary>Verifies write results and tag validation expose their assigned values.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task WriteResultsAndTagValidation_ExposeAssignedValuesAsync()
    {
        const string dataMemoryAddressText = "D100";
        const double combinedResultDuration = ResultDuration * TripleCount;

        var writeBits = new WriteBitsResult
        {
            BytesSent = SingleWordCount,
            PacketsSent = PairCount,
            BytesReceived = TripleCount,
            PacketsReceived = FourCount,
            Duration = ResultDuration,
        };
        var writeWords = new WriteWordsResult
        {
            BytesSent = SingleWordCount,
            PacketsSent = PairCount,
            BytesReceived = TripleCount,
            PacketsReceived = FourCount,
            Duration = ResultDuration,
        };
        var writeClock = new WriteClockResult
        {
            BytesSent = SingleWordCount,
            PacketsSent = PairCount,
            BytesReceived = TripleCount,
            PacketsReceived = FourCount,
            Duration = ResultDuration,
        };

        await Assert.That(writeBits.Duration + writeWords.Duration + writeClock.Duration)
            .IsEqualTo(combinedResultDuration);
        await Assert.That(
                CaptureException<ArgumentNullException>(() => _ = CreateTag<int>(null!, dataMemoryAddressText)))
            .IsNotNull();
        await Assert.That(
                CaptureException<ArgumentNullException>(() => _ = CreateTag<int>(SpeedTagName, null!)))
            .IsNotNull();
    }
}
