// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.S7PlcRx.Advanced;
using IoT.DriverCore.S7PlcRx.Core;
using IoT.DriverCore.S7PlcRx.Enterprise;
using IoT.DriverCore.S7PlcRx.Enums;
using IoT.DriverCore.S7PlcRx.Production;
using IoT.DriverCore.S7PlcRx.SourceGeneration;

namespace IoT.DriverCore.S7PlcRx.Tests.Core;

/// <summary>Verifies deterministic public S7 configuration and factory API behaviour.</summary>
public sealed class S7MiscApiResidualCoverageTests
{
    /// <summary>Defines the loopback endpoint used without opening a connection.</summary>
    private const string LoopbackAddress = "127.0.0.1";

    /// <summary>Defines the S7-1200 rack used by the factory test.</summary>
    private const short Factory1200Rack = 2;

    /// <summary>Defines the S7-300 rack used by the factory test.</summary>
    private const short Factory300Rack = 3;

    /// <summary>Defines the S7-300 slot used by the factory test.</summary>
    private const short Factory300Slot = 4;

    /// <summary>Defines the S7-400 rack used by the factory test.</summary>
    private const short Factory400Rack = 5;

    /// <summary>Defines the S7-400 slot used by the factory test.</summary>
    private const short Factory400Slot = 6;

    /// <summary>Defines the highest supported rack.</summary>
    private const short MaximumRack = 7;

    /// <summary>Defines the highest supported slot.</summary>
    private const short MaximumSlot = 31;

    /// <summary>Defines the default watchdog interval in seconds.</summary>
    private const int DefaultWatchdogInterval = 10;

    /// <summary>Defines the default watchdog value.</summary>
    private const ushort DefaultWatchdogValue = 4500;

    /// <summary>Defines the legacy polling interval.</summary>
    private const double LegacyPollingInterval = 25D;

    /// <summary>Defines the explicitly supplied polling interval.</summary>
    private const double ExplicitPollingInterval = 42D;

    /// <summary>Defines the configured polling interval.</summary>
    private const double ConfiguredPollingInterval = 33D;

    /// <summary>Defines the first watchdog write value.</summary>
    private const ushort FirstWatchdogValue = 1234;

    /// <summary>Defines the second watchdog write value.</summary>
    private const ushort SecondWatchdogValue = 4321;

    /// <summary>Defines the first watchdog interval.</summary>
    private const int FirstWatchdogInterval = 2;

    /// <summary>Defines the second watchdog interval.</summary>
    private const int SecondWatchdogInterval = 3;

    /// <summary>Defines the configured rack.</summary>
    private const short ConfiguredRack = 1;

    /// <summary>Defines the configured slot.</summary>
    private const short ConfiguredSlot = 2;

    /// <summary>Defines the default source-generated tag polling interval.</summary>
    private const int DefaultTagPollIntervalMilliseconds = 100;

    /// <summary>Defines the default source-generated tag array length.</summary>
    private const int DefaultTagArrayLength = 1;

    /// <summary>Defines a deterministic validation duration in seconds.</summary>
    private const int ValidationDurationSeconds = 2;

    /// <summary>Defines the active security-session timeout in minutes.</summary>
    private const int SecuritySessionTimeoutMinutes = 1;

    /// <summary>Defines the address retained by a source-generated tag attribute.</summary>
    private const string AttributeAddress = "DB1.DBW0";

    /// <summary>Verifies each factory applies its documented CPU family and endpoint values.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FactoriesCreateConfiguredConnectionsWithoutOpeningTransportAsync()
    {
        using var defaultPlc1200 = S71200.Create(LoopbackAddress);
        using var defaultPlc1500 = S71500.Create(LoopbackAddress);
        using var plc1200 = S71200.Create(LoopbackAddress, rack: Factory1200Rack);
        using var plc300 = S7300.Create(LoopbackAddress, rack: Factory300Rack, slot: Factory300Slot);
        using var plc400 = S7400.Create(LoopbackAddress, rack: Factory400Rack, slot: Factory400Slot);
        using var plc1500 = S71500.Create(LoopbackAddress, rack: MaximumRack, slot: MaximumSlot, watchDogAddress: "DB1.DBW2", interval: LegacyPollingInterval);

        await TUnit.Assertions.Assert.That(plc1200.PLCType).IsEqualTo(CpuType.S71200);
        await TUnit.Assertions.Assert.That(defaultPlc1200.PLCType).IsEqualTo(CpuType.S71200);
        await TUnit.Assertions.Assert.That(defaultPlc1500.PLCType).IsEqualTo(CpuType.S71500);
        await TUnit.Assertions.Assert.That(plc1200.Rack).IsEqualTo(Factory1200Rack);
        await TUnit.Assertions.Assert.That(plc1200.Slot).IsEqualTo(ConfiguredRack);
        await TUnit.Assertions.Assert.That(plc300.PLCType).IsEqualTo(CpuType.S7300);
        await TUnit.Assertions.Assert.That(plc300.Rack).IsEqualTo(Factory300Rack);
        await TUnit.Assertions.Assert.That(plc300.Slot).IsEqualTo(Factory300Slot);
        await TUnit.Assertions.Assert.That(plc400.PLCType).IsEqualTo(CpuType.S7400);
        await TUnit.Assertions.Assert.That(plc400.Rack).IsEqualTo(Factory400Rack);
        await TUnit.Assertions.Assert.That(plc400.Slot).IsEqualTo(Factory400Slot);
        await TUnit.Assertions.Assert.That(plc1500.PLCType).IsEqualTo(CpuType.S71500);
        await TUnit.Assertions.Assert.That(plc1500.Rack).IsEqualTo(MaximumRack);
        await TUnit.Assertions.Assert.That(plc1500.Slot).IsEqualTo(MaximumSlot);
        await TUnit.Assertions.Assert.That(plc1500.WatchDogAddress).IsEqualTo("DB1.DBW2");
        await TUnit.Assertions.Assert.That(plc1500.WatchDogWritingTime).IsEqualTo(DefaultWatchdogInterval);
    }

    /// <summary>Verifies factory overloads preserve polling and watchdog option values.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FactoriesAcceptExplicitComposedOptionsAsync()
    {
        var polling = new S7PollingOptions(ExplicitPollingInterval);
        var watchdog = new S7WatchdogOptions("DB2.DBW4", FirstWatchdogValue, FirstWatchdogInterval);
        using var plc1200 = S71200.Create(LoopbackAddress, 0, polling, watchdog);
        using var plc300 = S7300.Create(LoopbackAddress, 0, 1, polling, watchdog);
        using var plc400 = S7400.Create(LoopbackAddress, 0, 1, polling, watchdog);
        using var plc200 = S7200.Create(LoopbackAddress, 0, 0, polling, watchdog);

        await TUnit.Assertions.Assert.That(plc1200.WatchDogValueToWrite).IsEqualTo(FirstWatchdogValue);
        await TUnit.Assertions.Assert.That(plc300.WatchDogWritingTime).IsEqualTo(FirstWatchdogInterval);
        await TUnit.Assertions.Assert.That(plc400.WatchDogAddress).IsEqualTo("DB2.DBW4");
        await TUnit.Assertions.Assert.That(plc200.PLCType).IsEqualTo(CpuType.S7200);
        await TUnit.Assertions.Assert.That(plc200.Slot).IsEqualTo((short)0);
    }

    /// <summary>Verifies composed options retain values and apply the polling default when omitted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComposedOptionsRetainConnectionPollingAndWatchdogValuesAsync()
    {
        var connection = new S7ConnectionOptions(CpuType.S7300, LoopbackAddress, ConfiguredRack, ConfiguredSlot);
        var watchdog = new S7WatchdogOptions("DB3.DBW6", SecondWatchdogValue, SecondWatchdogInterval);
        var defaults = new RxS7Options(connection);
        var configured = new RxS7Options(connection, new S7PollingOptions(ConfiguredPollingInterval), watchdog);

        await TUnit.Assertions.Assert.That(defaults.Connection).IsSameReferenceAs(connection);
        await TUnit.Assertions.Assert.That(defaults.Polling.IntervalMilliseconds).IsEqualTo(S7PollingOptions.DefaultIntervalMilliseconds);
        await TUnit.Assertions.Assert.That(defaults.Watchdog).IsNull();
        await TUnit.Assertions.Assert.That(configured.Connection.CpuType).IsEqualTo(CpuType.S7300);
        await TUnit.Assertions.Assert.That(configured.Connection.IpAddress).IsEqualTo(LoopbackAddress);
        await TUnit.Assertions.Assert.That(configured.Connection.Rack).IsEqualTo(ConfiguredRack);
        await TUnit.Assertions.Assert.That(configured.Connection.Slot).IsEqualTo(ConfiguredSlot);
        await TUnit.Assertions.Assert.That(configured.Polling.IntervalMilliseconds).IsEqualTo(ConfiguredPollingInterval);
        await TUnit.Assertions.Assert.That(configured.Watchdog).IsSameReferenceAs(watchdog);
        await TUnit.Assertions.Assert.That(watchdog.ValueToWrite).IsEqualTo(SecondWatchdogValue);
        await TUnit.Assertions.Assert.That(watchdog.IntervalSeconds).IsEqualTo(SecondWatchdogInterval);
        await TUnit.Assertions.Assert.That(S7WatchdogOptions.DefaultValueToWrite).IsEqualTo(DefaultWatchdogValue);
        await TUnit.Assertions.Assert.That(S7WatchdogOptions.DefaultIntervalSeconds).IsEqualTo(DefaultWatchdogInterval);
    }

    /// <summary>Verifies public exception constructors retain their protocol error context.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicExceptionsPreserveErrorCodeMessageAndInnerExceptionAsync()
    {
        var inner = new InvalidOperationException("inner");
        var defaultPlcException = new PlcException();
        var messagePlcException = new PlcException("message");
        var nestedMessagePlcException = new PlcException("nested message", inner);
        var codePlcException = new PlcException(ErrorCode.ReadData);
        var nestedPlcException = new PlcException(ErrorCode.WriteData, "write failed", inner);
        var defaultS7Exception = new S7Exception();
        var nestedS7Exception = new S7Exception("s7 failed", inner);

        await TUnit.Assertions.Assert.That(defaultPlcException.ErrorCode).IsEqualTo(ErrorCode.NoError);
        await TUnit.Assertions.Assert.That(defaultPlcException.Message).IsEqualTo("PLC communication failed.");
        await TUnit.Assertions.Assert.That(messagePlcException.Message).IsEqualTo("message");
        await TUnit.Assertions.Assert.That(nestedMessagePlcException.InnerException).IsSameReferenceAs(inner);
        await TUnit.Assertions.Assert.That(codePlcException.ErrorCode).IsEqualTo(ErrorCode.ReadData);
        await TUnit.Assertions.Assert.That(nestedPlcException.ErrorCode).IsEqualTo(ErrorCode.WriteData);
        await TUnit.Assertions.Assert.That(nestedPlcException.InnerException).IsSameReferenceAs(inner);
        await TUnit.Assertions.Assert.That(defaultS7Exception.Message).IsNotEmpty();
        await TUnit.Assertions.Assert.That(nestedS7Exception.Message).IsEqualTo("s7 failed");
        await TUnit.Assertions.Assert.That(nestedS7Exception.InnerException).IsSameReferenceAs(inner);
    }

    /// <summary>Verifies public metadata DTO defaults, derived values, and comparer null behavior.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicMetadataDtosExposeDeterministicDefaultsAndDerivedValuesAsync()
    {
        var tagAttribute = new S7TagAttribute(AttributeAddress);
        var operation = new OperationDetail();
        var metrics = new ProductionMetrics();
        var dataBlock = new DataBlockInfo();
        var start = TestTime.UnixEpoch;
        var validation = new ValidationTest
        {
            StartTime = start,
            EndTime = start.AddSeconds(ValidationDurationSeconds),
        };
        var security = new SecurityContext
        {
            IsEnabled = true,
            SessionStartTime = DateTimeOffset.MaxValue.AddMinutes(-SecuritySessionTimeoutMinutes),
            SessionTimeout = TimeSpan.FromMinutes(SecuritySessionTimeoutMinutes),
        };
        var comparer = new DictionaryEqualityComparer<string, int>();

        await TUnit.Assertions.Assert.That(tagAttribute.Address).IsEqualTo(AttributeAddress);
        await TUnit.Assertions.Assert.That(tagAttribute.PollIntervalMs)
            .IsEqualTo(DefaultTagPollIntervalMilliseconds);
        await TUnit.Assertions.Assert.That(tagAttribute.ArrayLength).IsEqualTo(DefaultTagArrayLength);
        await TUnit.Assertions.Assert.That(operation.TagName).IsEmpty();
        await TUnit.Assertions.Assert.That(operation.OperationType).IsEmpty();
        await TUnit.Assertions.Assert.That(metrics.PLCIdentifier).IsEmpty();
        await TUnit.Assertions.Assert.That(dataBlock.TagNames.Count).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(validation.Duration)
            .IsEqualTo(TimeSpan.FromSeconds(ValidationDurationSeconds));
        await TUnit.Assertions.Assert.That(security.IsSessionValid).IsTrue();
        await TUnit.Assertions.Assert.That(comparer.GetHashCode(null!)).IsEqualTo(0);
    }
}
