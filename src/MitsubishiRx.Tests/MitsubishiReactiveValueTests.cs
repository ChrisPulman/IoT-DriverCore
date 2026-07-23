// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive.Tests;
#else

namespace IoT.DriverCore.MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiReactiveValueTests type.</summary>
internal sealed class MitsubishiReactiveValueTests
{
    /// <summary>Stores the expected failed-response error code.</summary>
    private const int ResponseErrorCode = 7;

    /// <summary>Stores the expected recipe number value.</summary>
    private const int RecipeNumber = 42;

    /// <summary>Stores the age of a stale value in seconds.</summary>
    private const int StaleValueAgeSeconds = 2;

    /// <summary>Executes the ReactiveValueFromSuccessfulResponseUsesGoodQualityAndMetadata operation.</summary>
    /// <returns>The ReactiveValueFromSuccessfulResponseUsesGoodQualityAndMetadata operation result.</returns>
    [Test]
    internal async Task ReactiveValueFromSuccessfulResponseUsesGoodQualityAndMetadataAsync()
    {
        var timestamp = new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.Zero);
        var response = new Responce<ushort[]>([(ushort)0x1234]);

        var value = MitsubishiReactiveValue.FromResponse(response, timestamp, "Read words D100");

        await Assert.That(value.TimestampUtc).IsEqualTo(timestamp);
        await Assert.That(value.Quality).IsEqualTo(MitsubishiReactiveQuality.Good);
        await Assert.That(value.Source).IsEqualTo("Read words D100");
        await Assert.That(value.IsHeartbeat).IsFalse();
        await Assert.That(value.IsStale).IsFalse();
        await Assert.That(value.Error).IsEqualTo(string.Empty);
        await Assert.That(value.Value).IsEquivalentTo([(ushort)0x1234]);
    }

    /// <summary>Executes the ReactiveValueFromFailedResponseUsesErrorQualityAndCapturesError operation.</summary>
    /// <returns>The ReactiveValueFromFailedResponseUsesErrorQualityAndCapturesError operation result.</returns>
    [Test]
    internal async Task ReactiveValueFromFailedResponseUsesErrorQualityAndCapturesErrorAsync()
    {
        var timestamp = new DateTimeOffset(2026, 4, 21, 12, 1, 0, TimeSpan.Zero);
        var response = new Responce<ushort[]>
        {
            IsSucceed = false,
            Err = "boom",
            ErrCode = ResponseErrorCode,
        };

        var value = MitsubishiReactiveValue.FromResponse(response, timestamp, "Read words D200");

        await Assert.That(value.TimestampUtc).IsEqualTo(timestamp);
        await Assert.That(value.Quality).IsEqualTo(MitsubishiReactiveQuality.Error);
        await Assert.That(value.Source).IsEqualTo("Read words D200");
        await Assert.That(value.Error).IsEqualTo("boom");
        await Assert.That(value.ErrorCode).IsEqualTo(ResponseErrorCode);
        await Assert.That(value.Value is null).IsTrue();
    }

    /// <summary>Executes the HeartbeatAndStaleFactoriesStampQualityFlags operation.</summary>
    /// <returns>The HeartbeatAndStaleFactoriesStampQualityFlags operation result.</returns>
    [Test]
    internal async Task HeartbeatAndStaleFactoriesStampQualityFlagsAsync()
    {
        var timestamp = new DateTimeOffset(2026, 4, 21, 12, 2, 0, TimeSpan.Zero);
        var baseline = new MitsubishiReactiveValue<int>(
            RecipeNumber,
            timestamp,
            MitsubishiReactiveQuality.Good,
            false,
            false,
            "Tag:RecipeNumber",
            string.Empty);

        var heartbeat = MitsubishiReactiveValue.Heartbeat(baseline, timestamp.AddSeconds(1));
        var stale = MitsubishiReactiveValue.Stale(baseline, timestamp.AddSeconds(StaleValueAgeSeconds));

        await Assert.That(heartbeat.IsHeartbeat).IsTrue();
        await Assert.That(heartbeat.Quality).IsEqualTo(MitsubishiReactiveQuality.Heartbeat);
        await Assert.That(heartbeat.Value).IsEqualTo(RecipeNumber);

        await Assert.That(stale.IsStale).IsTrue();
        await Assert.That(stale.Quality).IsEqualTo(MitsubishiReactiveQuality.Stale);
        await Assert.That(stale.Value).IsEqualTo(RecipeNumber);
    }
}
