// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if LIVE_ADS_READ_ONLY_TESTS
using System;
using System.Threading;
using System.Threading.Tasks;
using IoT.DriverCore.TwinCATRx.Core;
using TwinCAT.Ads;

namespace IoT.DriverCore.TwinCATRx.Tests.Rx;

/// <summary>Opt-in, read-only hardware probes for the documented TwinCAT routes.</summary>
[Category("LiveAds")]
public sealed class LiveAdsReadOnlyHardwareTests
{
    /// <summary>TwinCAT 3 AMS Net ID.</summary>
    private const string TwinCat3Address = "10.1.180.147.1.1";

    /// <summary>TwinCAT 3 PLC runtime port.</summary>
    private const int TwinCat3Port = 851;

    /// <summary>TwinCAT 2 AMS Net ID.</summary>
    private const string TwinCat2Address = "5.35.59.10.1.1";

    /// <summary>TwinCAT 2 PLC runtime port.</summary>
    private const int TwinCat2Port = 801;

    /// <summary>Default number of ADS state reads per endpoint.</summary>
    private const int DefaultEnduranceReads = 120;

    /// <summary>Maximum configured ADS state reads per endpoint.</summary>
    private const int MaximumEnduranceReads = 10_000;

    /// <summary>Default delay between ADS state reads.</summary>
    private const int DefaultReadDelayMilliseconds = 500;

    /// <summary>Maximum configured delay between ADS state reads.</summary>
    private const int MaximumReadDelayMilliseconds = 10_000;

    /// <summary>Verifies the TwinCAT 3 AMS route without issuing any PLC write.</summary>
    /// <param name="cancellationToken">The TUnit timeout token.</param>
    /// <returns>The test task.</returns>
    [Test]
    [Timeout(900_000)]
    public async Task TwinCat3_ReadOnlyRoute_IsReachable_AndLoadsSymbols(CancellationToken cancellationToken)
    {
        await VerifyEndpointAsync(TwinCat3Address, TwinCat3Port, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Verifies the TwinCAT 2 AMS route without issuing any PLC write.</summary>
    /// <param name="cancellationToken">The TUnit timeout token.</param>
    /// <returns>The test task.</returns>
    [Test]
    [Timeout(900_000)]
    public async Task TwinCat2_ReadOnlyRoute_IsReachable_AndLoadsSymbols(CancellationToken cancellationToken)
    {
        await VerifyEndpointAsync(TwinCat2Address, TwinCat2Port, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads ADS state repeatedly and loads the endpoint symbol table once.</summary>
    /// <param name="adsAddress">The AMS Net ID.</param>
    /// <param name="port">The ADS runtime port.</param>
    /// <param name="cancellationToken">The TUnit timeout token.</param>
    /// <returns>The test task.</returns>
    private static async Task VerifyEndpointAsync(
        string adsAddress,
        int port,
        CancellationToken cancellationToken)
    {
        var readCount = GetBoundedEnvironmentValue(
            "TWINCATRX_LIVE_ADS_READ_COUNT",
            DefaultEnduranceReads,
            1,
            MaximumEnduranceReads);
        var readDelayMilliseconds = GetBoundedEnvironmentValue(
            "TWINCATRX_LIVE_ADS_READ_DELAY_MS",
            DefaultReadDelayMilliseconds,
            0,
            MaximumReadDelayMilliseconds);
        using var client = new AdsClient();
        client.Connect(adsAddress, port);

        for (var attempt = 0; attempt < readCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = client.ReadState();
            await TUnitAssert.That(state.AdsState).IsEqualTo(AdsState.Run);
            if (attempt + 1 < readCount && readDelayMilliseconds > 0)
            {
                await Task.Delay(readDelayMilliseconds, cancellationToken).ConfigureAwait(false);
            }
        }

        using var generator = new CodeGenerator();
        var symbols = generator.LoadSymbols(adsAddress, port);
        await TUnitAssert.That(symbols.Count).IsGreaterThan(0);
    }

    /// <summary>Gets a bounded integer environment option.</summary>
    /// <param name="name">Environment variable name.</param>
    /// <param name="defaultValue">Value used when the variable is absent or invalid.</param>
    /// <param name="minimum">Minimum accepted value.</param>
    /// <param name="maximum">Maximum accepted value.</param>
    /// <returns>The configured or default value.</returns>
    private static int GetBoundedEnvironmentValue(
        string name,
        int defaultValue,
        int minimum,
        int maximum)
    {
        var configuredValue = Environment.GetEnvironmentVariable(name);
        return int.TryParse(configuredValue, out var value) && value >= minimum && value <= maximum
            ? value
            : defaultValue;
    }
}
#endif
