// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive.Tests;
#else

namespace MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiUdpTests type.</summary>
internal sealed class MitsubishiUdpTests
{
    /// <summary>Stores the number of words read in the UDP test.</summary>
    private const int WordCount = 2;

    /// <summary>Executes the ReadWordsAsyncAsciiUdpRoundTripsThroughDynamicResponse operation.</summary>
    /// <returns>The ReadWordsAsyncAsciiUdpRoundTripsThroughDynamicResponse operation result.</returns>
    [Test]
    internal async Task ReadWordsAsyncAsciiUdpRoundTripsThroughDynamicResponseAsync()
    {
        await using var transport = new FakeTransport(request =>
        {
            _ = System.Text.Encoding.ASCII.GetString(request.Payload);
            return System.Text.Encoding.ASCII.GetBytes("D00000FF03FF000006000000425678");
        });

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5014,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Ascii,
            TransportKind: MitsubishiTransportKind.Udp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate);
        var result = await client.ReadWordsAsync("D100", WordCount, CancellationToken.None);
        if (!result.IsSucceed)
        {
            throw new InvalidOperationException(result.Err);
        }

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!.Select(static value => (int)value).ToArray()).IsEquivalentTo([0x0042, 0x5678]);
        await Assert.That(transport.Requests[0].Description).IsEqualTo("Read words D100");
    }
}
