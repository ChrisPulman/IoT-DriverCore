// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.S7PlcRx.Core;
using IoT.DriverCore.S7PlcRx.Enterprise;
using IoT.DriverCore.S7PlcRx.Enums;

namespace IoT.DriverCore.S7PlcRx.Tests.Enterprise;

/// <summary>Provides deterministic coverage for S7 enterprise extensions.</summary>
public sealed class S7EnterpriseDeterministicCoverageTests
{
    /// <summary>Defines the temperature symbol name.</summary>
    private const string TemperatureSymbol = "Temperature";

    /// <summary>Defines the pressure symbol name.</summary>
    private const string PressureSymbol = "Pressure";

    /// <summary>Defines the initial symbol value.</summary>
    private const int InitialValue = 35;

    /// <summary>Defines the written symbol value.</summary>
    private const int WrittenValue = 42;

    /// <summary>Defines the expected symbol count.</summary>
    private const int ExpectedSymbolCount = 2;

    /// <summary>Defines an unsupported symbol-table format value.</summary>
    private const SymbolTableFormat UnsupportedSymbolTableFormat = (SymbolTableFormat)999;

    /// <summary>Defines a deterministic health-check interval.</summary>
    private static readonly TimeSpan HealthCheckInterval = TimeSpan.FromMinutes(1);

    /// <summary>Verifies CSV symbols support typed registration, symbolic reads, and writes through TestPlc.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SymbolTableLoadReadAndWriteUseTheInMemoryPlcAsync()
    {
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        plc.SetSyncValue(TemperatureSymbol, InitialValue);
        const string csv = "Name,Address,DataType,Length,Description\n" +
            "Temperature,DB1.DBW0,DINT,1,Process temperature\n" +
            "Pressure,DB1.DBW4,DINT,1,Process pressure";

        var table = await EnterpriseExtensions.LoadSymbolTableAsync(plc, csv, SymbolTableFormat.Csv);
        var read = await EnterpriseExtensions.ReadSymbolAsync(plc, TemperatureSymbol);
        EnterpriseExtensions.WriteSymbol(plc, TemperatureSymbol, WrittenValue);

        await TUnit.Assertions.Assert.That(table.Symbols.Count).IsEqualTo(ExpectedSymbolCount);
        await TUnit.Assertions.Assert.That(table.Symbols[PressureSymbol].Address).IsEqualTo("DB1.DBW4");
        await TUnit.Assertions.Assert.That(read).IsEqualTo(InitialValue);
        await TUnit.Assertions.Assert.That(plc.WrittenValues[TemperatureSymbol]).IsEqualTo(WrittenValue);
    }

    /// <summary>Verifies symbolic and factory argument validation avoids creating external connections.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnterpriseFactoriesValidateArgumentsWithoutExternalHardwareAsync()
    {
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        using var backup = new S7PlcRxAsyncExtensionsTests.TestPlc();
        var backups = new List<IRxS7> { backup };
        using var manager = EnterpriseExtensions.CreateHighAvailabilityConnection(plc, backups, HealthCheckInterval);
        var failover = await manager.TriggerFailoverAsync();
        var poolConfig = new ConnectionPoolConfig { MaxConnections = ExpectedSymbolCount };

        await TUnit.Assertions.Assert.That(manager.ActivePLC).IsEqualTo(plc);
        await TUnit.Assertions.Assert.That(failover).IsTrue();
        await TUnit.Assertions.Assert.That(
                () => EnterpriseExtensions.CreateHighAvailabilityConnection(null!, backups, HealthCheckInterval))
            .Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(
                () => EnterpriseExtensions.CreateConnectionPool([], poolConfig))
            .Throws<ArgumentException>();
        await TUnit.Assertions.Assert.That(
                () => EnterpriseExtensions.CreateConnectionPool(null!, poolConfig))
            .Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(
                () => EnterpriseExtensions.CreateConnectionPool(
                    [new PlcConnectionConfig { PLCType = CpuType.S71500, IPAddress = "127.0.0.1", Rack = 0, Slot = 1 }],
                    null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>Verifies symbol-table parsing and symbolic lookup validation errors are surfaced directly.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SymbolTableAndSymbolicAccessValidateInputsAsync()
    {
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        const string csv = "Temperature,DB1.DBW0,DINT";
        _ = await EnterpriseExtensions.LoadSymbolTableAsync(plc, csv, SymbolTableFormat.Csv);
        Func<Task> blankSymbolTable = async () => _ = await EnterpriseExtensions.LoadSymbolTableAsync(
            plc,
            " ",
            SymbolTableFormat.Csv);
        Func<Task> unsupportedSymbolTable = async () => _ = await EnterpriseExtensions.LoadSymbolTableAsync(
            plc,
            csv,
            UnsupportedSymbolTableFormat);

        await TUnit.Assertions.Assert.That(blankSymbolTable)
            .Throws<ArgumentException>();
        await TUnit.Assertions.Assert.That(unsupportedSymbolTable)
            .Throws<ArgumentOutOfRangeException>();
        await TUnit.Assertions.Assert.That(() => EnterpriseExtensions.WriteSymbol(plc, "Missing", WrittenValue))
            .Throws<ArgumentException>();
        await TUnit.Assertions.Assert.That(() => EnterpriseExtensions.ReadSymbolAsync(plc, "Missing"))
            .Throws<ArgumentException>();
    }

    /// <summary>Verifies JSON and XML symbol-table parsers populate the shared symbolic catalog.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SymbolTableLoadSupportsJsonAndXmlAsync()
    {
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        const string json = "[{\"Name\":\"JsonValue\",\"Address\":\"DB1.DBW0\",\"DataType\":\"WORD\",\"Length\":1}]";
        const string xml = "<Symbols><Symbol><Name>XmlValue</Name><Address>DB2.DBW0</Address><DataType>WORD</DataType><Length>1</Length></Symbol></Symbols>";

        var jsonTable = await EnterpriseExtensions.LoadSymbolTableAsync(plc, json, SymbolTableFormat.Json);
        var xmlTable = await EnterpriseExtensions.LoadSymbolTableAsync(plc, xml, SymbolTableFormat.Xml);

        await TUnit.Assertions.Assert.That(jsonTable.Symbols.ContainsKey("JsonValue")).IsTrue();
        await TUnit.Assertions.Assert.That(xmlTable.Symbols.ContainsKey("XmlValue")).IsTrue();
    }
}
