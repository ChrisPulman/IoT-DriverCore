// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if LIVE_S7_TESTS
using IoT.Driver.S7PlcRx.Symbolic;
using TAssert = TUnit.Assertions.Assert;

namespace IoT.Driver.S7PlcRx.Tests.Symbolic;

/// <summary>Provides opt-in, read-only S7CommPlus hardware tests.</summary>
public sealed class PlusLiveHardwareTests
{
    /// <summary>The live controller endpoint environment variable.</summary>
    private const string EndpointEnvironmentVariable = "S7PLCRX_LIVE_IP";

    /// <summary>The required live certificate pin environment variable.</summary>
    private const string CertificatePinEnvironmentVariable = "S7PLCRX_LIVE_CERTIFICATE_SHA256";

    /// <summary>The optional live username environment variable.</summary>
    private const string UsernameEnvironmentVariable = "S7PLCRX_LIVE_USERNAME";

    /// <summary>The optional live password environment variable.</summary>
    private const string PasswordEnvironmentVariable = "S7PLCRX_LIVE_PASSWORD";

    /// <summary>The default live controller endpoint.</summary>
    private const string DefaultEndpoint = "172.16.13.1";

    /// <summary>The PLC soft datatype for Boolean values.</summary>
    private const uint BoolSoftType = 1;

    /// <summary>The PLC soft datatype for date and time values.</summary>
    private const uint DateTimeSoftType = 14;

    /// <summary>The first additional numeric scalar PLC soft datatype.</summary>
    private const uint ExtendedScalarSoftTypeStart = 48;

    /// <summary>The last additional numeric scalar PLC soft datatype.</summary>
    private const uint ExtendedScalarSoftTypeEnd = 55;

    /// <summary>The PLC soft datatype for narrow strings.</summary>
    private const uint StringSoftType = 19;

    /// <summary>The PLC soft datatype for counters.</summary>
    private const uint CounterSoftType = 28;

    /// <summary>The PLC soft datatype for blocks.</summary>
    private const uint BlockSoftType = 29;

    /// <summary>The PLC soft datatype for Boolean aliases.</summary>
    private const uint BoolAliasSoftType = 40;

    /// <summary>The PLC soft datatype for wide characters.</summary>
    private const uint WideCharSoftType = 61;

    /// <summary>The PLC soft datatype for wide strings.</summary>
    private const uint WideStringSoftType = 62;

    /// <summary>The PLC soft datatype for long times.</summary>
    private const uint LongTimeSoftType = 64;

    /// <summary>The PLC soft datatype for long time-of-day values.</summary>
    private const uint LongTimeOfDaySoftType = 65;

    /// <summary>The PLC soft datatype for long date-time values.</summary>
    private const uint LongDateTimeSoftType = 66;

    /// <summary>The maximum number of logger candidates to read.</summary>
    private const int MaximumLoggerReadCount = 3;

    /// <summary>Connects, browses symbols, and reads a few accessible logger scalars.</summary>
    /// <param name="cancellationToken">Cancels live controller operations.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LiveController_BrowseAndReadLoggerScalars(CancellationToken cancellationToken)
    {
        var endpoint = Environment.GetEnvironmentVariable(EndpointEnvironmentVariable);
        var pin = Environment.GetEnvironmentVariable(CertificatePinEnvironmentVariable);
        await TAssert.That(string.IsNullOrWhiteSpace(pin)).IsFalse();

        S7SymbolicConnectionOptions options = new(endpoint ?? DefaultEndpoint)
        {
            CertificateSha256 = pin,
            Username = Environment.GetEnvironmentVariable(UsernameEnvironmentVariable) ?? string.Empty,
            Password = Environment.GetEnvironmentVariable(PasswordEnvironmentVariable) ?? string.Empty,
        };

        await using S7SymbolicClient client = new(options);
        await client.ConnectAsync(cancellationToken);
        var symbols = await client.BrowseAsync(cancellationToken);
        await TAssert.That(symbols.Length > 0).IsTrue();

        var loggerReadCount = 0;
        foreach (var symbol in symbols)
        {
            if (loggerReadCount >= MaximumLoggerReadCount ||
                !symbol.IsAccessible ||
                symbol.ArrayLengths.Count != 0 ||
                !IsScalarSoftType(symbol.SoftDataType) ||
                !symbol.Path.Contains("logger", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = await client.ReadAsync(symbol.Path, cancellationToken);
            await TAssert.That(value).IsNotNull();
            loggerReadCount++;
        }

        await TAssert.That(loggerReadCount).IsGreaterThan(0);
    }

    /// <summary>Checks whether a PLC soft datatype describes a supported scalar.</summary>
    /// <param name="softDataType">The PLC soft datatype identifier.</param>
    /// <returns>True when the datatype can describe a scalar value.</returns>
    private static bool IsScalarSoftType(uint softDataType) =>
        softDataType is >= BoolSoftType and <= DateTimeSoftType ||
        IsAdditionalScalarSoftType(softDataType) || IsLongScalarSoftType(softDataType);

    /// <summary>Checks string and additional numeric scalar identifiers.</summary>
    /// <param name="softDataType">The PLC soft datatype identifier.</param>
    /// <returns>True for these supported scalar identifiers.</returns>
    private static bool IsAdditionalScalarSoftType(uint softDataType) =>
        softDataType is StringSoftType or CounterSoftType or BlockSoftType or BoolAliasSoftType or
            (>= ExtendedScalarSoftTypeStart and <= ExtendedScalarSoftTypeEnd);

    /// <summary>Checks wide characters and long temporal scalar identifiers.</summary>
    /// <param name="softDataType">The PLC soft datatype identifier.</param>
    /// <returns>True for these supported scalar identifiers.</returns>
    private static bool IsLongScalarSoftType(uint softDataType) =>
        softDataType is WideCharSoftType or WideStringSoftType or LongTimeSoftType or LongTimeOfDaySoftType or LongDateTimeSoftType;
}
#endif
