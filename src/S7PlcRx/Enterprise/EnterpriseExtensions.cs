// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
#if REACTIVE_SHIM
using IoT.DriverCore.S7PlcRx.Reactive.Core;
#else
using IoT.DriverCore.S7PlcRx.Core;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.S7PlcRx.Reactive.Enterprise;
#else
namespace IoT.DriverCore.S7PlcRx.Enterprise;
#endif

/// <summary>Provides enterprise PLC connectivity and symbolic-addressing extensions.</summary>
public static class EnterpriseExtensions
{
    /// <summary>Defines the minimum number of CSV columns.</summary>
    private const int CsvMinimumColumnCount = 3;

    /// <summary>Defines the CSV data type column index.</summary>
    private const int CsvDataTypeColumnIndex = 2;

    /// <summary>Defines the optional CSV length column index.</summary>
    private const int CsvLengthColumnIndex = 3;

    /// <summary>Defines the optional CSV description column index.</summary>
    private const int CsvDescriptionColumnIndex = 4;

    /// <summary>Gets the cache of symbol tables by PLC endpoint.</summary>
    private static ConcurrentDictionary<string, SymbolTable> SymbolTables { get; } = new();

    /// <summary>Gets the cache of connection pools.</summary>
    private static ConcurrentDictionary<string, ConnectionPool> ConnectionPools { get; } = new();

    /// <summary>Gets the runtime type mapping for scalar S7 symbol types.</summary>
    private static Dictionary<string, Type> SymbolTypes { get; } =
        new(StringComparer.Ordinal)
        {
            ["BOOL"] = typeof(bool),
            ["BYTE"] = typeof(byte),
            ["WORD"] = typeof(ushort),
            ["DWORD"] = typeof(uint),
            ["INT"] = typeof(short),
            ["DINT"] = typeof(int),
            ["REAL"] = typeof(float),
            ["LREAL"] = typeof(double),
            ["STRING"] = typeof(string),
        };

    /// <summary>Loads and caches a CSV symbol table for symbolic addressing support.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="symbolTableData">CSV symbol table data.</param>
    /// <returns>The loaded symbol table.</returns>
    public static Task<SymbolTable> LoadSymbolTableAsync(IRxS7 plc, string symbolTableData) =>
        LoadSymbolTableAsync(plc, symbolTableData, SymbolTableFormat.Csv);

    /// <summary>Loads and caches a symbol table for symbolic addressing support.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="symbolTableData">Symbol table data.</param>
    /// <param name="format">The format of the symbol table data.</param>
    /// <returns>The loaded symbol table.</returns>
    public static async Task<SymbolTable> LoadSymbolTableAsync(
        IRxS7 plc,
        string symbolTableData,
        SymbolTableFormat format)
    {
        Guard.NotNull(plc, nameof(plc));
        Guard.NotNullOrWhiteSpace(symbolTableData, nameof(symbolTableData));

        var symbolTable = format switch
        {
            SymbolTableFormat.Csv => ParseCsvSymbolTable(symbolTableData),
            SymbolTableFormat.Json => ParseJsonSymbolTable(symbolTableData),
            SymbolTableFormat.Xml => ParseXmlSymbolTable(symbolTableData),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported symbol table format."),
        };

        _ = SymbolTables.AddOrUpdate(GetSymbolTableKey(plc), symbolTable, (_, _) => symbolTable);

        foreach (var symbol in symbolTable.Symbols.Values)
        {
            if (!plc.TagList.ContainsKey(symbol.Name))
            {
                _ = TagOperations.AddUpdateTagItem(
                    plc,
                    GetTagType(symbol.DataType),
                    symbol.Name,
                    symbol.Address,
                    symbol.Length);
            }
    }

        await Task.CompletedTask;
        return symbolTable;
        }

    /// <summary>Reads the value of the specified symbol from the PLC.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="symbolName">The symbol name.</param>
    /// <returns>A task containing the symbol value.</returns>
    public static Task<object?> ReadSymbolAsync(IRxS7 plc, string symbolName) =>
        plc.ReadAsync(new LogicalTagKey<object>(GetRequiredSymbol(plc, symbolName).Name));

    /// <summary>Writes a value to the specified PLC symbol by name.</summary>
    /// <typeparam name="T">The type of the value to write.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="symbolName">The symbol name.</param>
    /// <param name="value">The value to write.</param>
    public static void WriteSymbol<T>(IRxS7 plc, string symbolName, T value)
    {
        Guard.NotNull(plc, nameof(plc));
        Guard.NotNullOrWhiteSpace(symbolName, nameof(symbolName));

        plc.Value(GetRequiredSymbol(plc, symbolName).Name, value);
    }

    /// <summary>Creates a high-availability manager using the default health-check interval.</summary>
    /// <param name="primaryPlc">The primary PLC.</param>
    /// <param name="backupPlcs">The backup PLCs.</param>
    /// <returns>A high-availability PLC manager.</returns>
    public static HighAvailabilityPlcManager CreateHighAvailabilityConnection(
        IRxS7 primaryPlc,
        IList<IRxS7> backupPlcs) => new(primaryPlc, backupPlcs);

    /// <summary>Creates a high-availability manager using a specified health-check interval.</summary>
    /// <param name="primaryPlc">The primary PLC.</param>
    /// <param name="backupPlcs">The backup PLCs.</param>
    /// <param name="healthCheckInterval">The health-check interval.</param>
    /// <returns>A high-availability PLC manager.</returns>
    public static HighAvailabilityPlcManager CreateHighAvailabilityConnection(
        IRxS7 primaryPlc,
        IList<IRxS7> backupPlcs,
        TimeSpan healthCheckInterval) => new(primaryPlc, backupPlcs, healthCheckInterval);

    /// <summary>Creates and registers a connection pool.</summary>
    /// <param name="connectionConfigs">PLC connection configurations.</param>
    /// <param name="poolConfig">Connection-pool settings.</param>
    /// <returns>The created connection pool.</returns>
    public static ConnectionPool CreateConnectionPool(
        IEnumerable<PlcConnectionConfig> connectionConfigs,
        ConnectionPoolConfig poolConfig) =>
        CreateConnectionPool(connectionConfigs, poolConfig, TimeProvider.System);

    /// <summary>Creates and registers a connection pool.</summary>
    /// <param name="connectionConfigs">PLC connection configurations.</param>
    /// <param name="poolConfig">Connection-pool settings.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <returns>The created connection pool.</returns>
    public static ConnectionPool CreateConnectionPool(
        IEnumerable<PlcConnectionConfig> connectionConfigs,
        ConnectionPoolConfig poolConfig,
        TimeProvider timeProvider)
    {
        Guard.NotNull(connectionConfigs, nameof(connectionConfigs));
        Guard.NotNull(poolConfig, nameof(poolConfig));

        var configs = new List<PlcConnectionConfig>(connectionConfigs);
        if (configs.Count == 0)
        {
            throw new ArgumentException(
                "At least one connection configuration is required.",
                nameof(connectionConfigs));
        }

        var pool = new ConnectionPool(configs, poolConfig);
        _ = ConnectionPools.AddOrUpdate($"Pool_{timeProvider.GetUtcNow().UtcDateTime.Ticks}", pool, (_, _) => pool);
        return pool;
    }

    /// <summary>Gets a required symbol from the PLC symbol table.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="symbolName">The symbol name.</param>
    /// <returns>The required symbol.</returns>
    private static Symbol GetRequiredSymbol(IRxS7 plc, string symbolName)
    {
        var symbolTable = GetSymbolTable(plc);
        if (symbolTable?.Symbols.TryGetValue(symbolName, out var symbol) == true)
        {
            return symbol;
        }

        throw new ArgumentException($"Symbol '{symbolName}' was not found in the symbol table.", nameof(symbolName));
    }

    /// <summary>Gets the symbol table associated with a PLC.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <returns>The symbol table, if loaded.</returns>
    private static SymbolTable? GetSymbolTable(IRxS7 plc) =>
        SymbolTables.TryGetValue(GetSymbolTableKey(plc), out var symbolTable) ? symbolTable : null;

    /// <summary>Creates the cache key for a PLC.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <returns>The cache key.</returns>
    private static string GetSymbolTableKey(IRxS7 plc) =>
        $"{plc.IP}_{plc.PLCType}_{plc.Rack}_{plc.Slot}";

    /// <summary>Maps a PLC symbol data type to its runtime tag type.</summary>
    /// <param name="dataType">The symbol data type.</param>
    /// <returns>The runtime tag type.</returns>
    private static Type GetTagType(string dataType)
    {
        if (SymbolTypes.TryGetValue(dataType, out var type))
        {
            return type;
        }

        return dataType.Contains("ARRAY", StringComparison.Ordinal) ? typeof(byte[]) : typeof(object);
    }

    /// <summary>Parses CSV symbol table data.</summary>
    /// <param name="csvData">The CSV data.</param>
    /// <returns>The parsed symbol table.</returns>
    private static SymbolTable ParseCsvSymbolTable(string csvData)
    {
        var symbolTable = new SymbolTable();
        var lines = csvData.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var startIndex = lines.Length > 0 && (lines[0].Contains("Name") || lines[0].Contains("Address")) ? 1 : 0;

        for (var i = startIndex; i < lines.Length; i++)
        {
            var values = lines[i].Split(',');
            if (values.Length >= CsvMinimumColumnCount)
            {
                var symbol = new Symbol
                {
                    Name = values[0].Trim('"'),
                    Address = values[1].Trim('"'),
                    DataType = values[CsvDataTypeColumnIndex].Trim('"'),
                    Length = GetCsvSymbolLength(values),
                    Description = GetCsvSymbolDescription(values),
                };
                symbolTable.Symbols[symbol.Name] = symbol;
            }
        }

        return symbolTable;
    }

    /// <summary>Parses JSON symbol table data.</summary>
    /// <param name="jsonData">The JSON data.</param>
    /// <returns>The parsed symbol table.</returns>
    private static SymbolTable ParseJsonSymbolTable(string jsonData)
    {
        var symbolTable = new SymbolTable();
        foreach (var symbol in System.Text.Json.JsonSerializer.Deserialize<List<Symbol>>(jsonData) ?? [])
        {
            symbolTable.Symbols[symbol.Name] = symbol;
        }

        return symbolTable;
    }

    /// <summary>Parses XML symbol table data.</summary>
    /// <param name="xmlData">The XML data.</param>
    /// <returns>The parsed symbol table.</returns>
    private static SymbolTable ParseXmlSymbolTable(string xmlData)
    {
        var symbolTable = new SymbolTable();
        foreach (var element in System.Xml.Linq.XDocument.Parse(xmlData).Descendants(nameof(Symbol)))
        {
            var symbol = new Symbol
            {
                Name = GetXmlElementValue(element, "Name"),
                Address = GetXmlElementValue(element, "Address"),
                DataType = GetXmlElementValue(element, "DataType"),
                Length = GetXmlSymbolLength(element),
                Description = GetXmlElementValue(element, "Description"),
            };
            if (!string.IsNullOrEmpty(symbol.Name))
            {
                symbolTable.Symbols[symbol.Name] = symbol;
            }
        }

        return symbolTable;
    }

    /// <summary>Gets an XML child element value.</summary>
    /// <param name="element">The parent element.</param>
    /// <param name="name">The child element name.</param>
    /// <returns>The child value.</returns>
    private static string GetXmlElementValue(System.Xml.Linq.XElement element, string name) =>
        element.Element(name)?.Value ?? string.Empty;

    /// <summary>Gets an XML symbol length.</summary>
    /// <param name="element">The symbol element.</param>
    /// <returns>The parsed length.</returns>
    private static int GetXmlSymbolLength(System.Xml.Linq.XElement element) =>
        int.TryParse(element.Element("Length")?.Value, out var length) ? length : 1;

    /// <summary>Gets the CSV symbol length.</summary>
    /// <param name="values">The CSV values.</param>
    /// <returns>The parsed length.</returns>
    private static int GetCsvSymbolLength(string[] values) =>
        values.Length > CsvLengthColumnIndex && int.TryParse(values[CsvLengthColumnIndex], out var length) ? length : 1;

    /// <summary>Gets the CSV symbol description.</summary>
    /// <param name="values">The CSV values.</param>
    /// <returns>The symbol description.</returns>
    private static string GetCsvSymbolDescription(string[] values) =>
        values.Length > CsvDescriptionColumnIndex ? values[CsvDescriptionColumnIndex].Trim('"') : string.Empty;
}
