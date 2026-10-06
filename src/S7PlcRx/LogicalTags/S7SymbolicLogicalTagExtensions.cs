// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using IoT.Driver.Core;
#if REACTIVE_SHIM
using IoT.Driver.S7PlcRx.Reactive.Symbolic;
namespace IoT.Driver.S7PlcRx.Reactive.LogicalTags;
#else
using IoT.Driver.S7PlcRx.Symbolic;
namespace IoT.Driver.S7PlcRx.LogicalTags;
#endif

/// <summary>Builds logical catalogs and clients for native symbolic access.</summary>
public static class S7SymbolicLogicalTagExtensions
{
    /// <summary>Defines the S5TIME PLC soft datatype.</summary>
    private const uint SoftTypeS5Time = 12;

    /// <summary>Defines the BOOL PLC soft datatype.</summary>
    private const uint SoftTypeBool = 1;

    /// <summary>Defines the BYTE PLC soft datatype.</summary>
    private const uint SoftTypeByte = 2;

    /// <summary>Defines the CHAR PLC soft datatype.</summary>
    private const uint SoftTypeChar = 3;

    /// <summary>Defines the WORD PLC soft datatype.</summary>
    private const uint SoftTypeWord = 4;

    /// <summary>Defines the INT PLC soft datatype.</summary>
    private const uint SoftTypeInt = 5;

    /// <summary>Defines the DWORD PLC soft datatype.</summary>
    private const uint SoftTypeDword = 6;

    /// <summary>Defines the DINT PLC soft datatype.</summary>
    private const uint SoftTypeDint = 7;

    /// <summary>Defines the REAL PLC soft datatype.</summary>
    private const uint SoftTypeReal = 8;

    /// <summary>Defines the DATE PLC soft datatype.</summary>
    private const uint SoftTypeDate = 9;

    /// <summary>Defines the TIME_OF_DAY PLC soft datatype.</summary>
    private const uint SoftTypeTimeOFDAY = 10;

    /// <summary>Defines the TIME PLC soft datatype.</summary>
    private const uint SoftTypeTime = 11;

    /// <summary>Defines the DATE_AND_TIME PLC soft datatype.</summary>
    private const uint SoftTypeDateANDTIME = 14;

    /// <summary>Defines the STRING PLC soft datatype.</summary>
    private const uint SoftTypeString = 19;

    /// <summary>Defines the POINTER PLC soft datatype.</summary>
    private const uint SoftTypePointer = 20;

    /// <summary>Defines the ANY PLC soft datatype.</summary>
    private const uint SoftTypeAny = 21;

    /// <summary>Defines the BLOCK_FB PLC soft datatype.</summary>
    private const uint SoftTypeBlockfb = 23;

    /// <summary>Defines the BLOCK_FC PLC soft datatype.</summary>
    private const uint SoftTypeBlockfc = 24;

    /// <summary>Defines the BLOCK_DB PLC soft datatype.</summary>
    private const uint SoftTypeBlockdb = 25;

    /// <summary>Defines the BLOCK_SDB PLC soft datatype.</summary>
    private const uint SoftTypeBlocksdb = 26;

    /// <summary>Defines the COUNTER PLC soft datatype.</summary>
    private const uint SoftTypeCounter = 30;

    /// <summary>Defines the TIMER PLC soft datatype.</summary>
    private const uint SoftTypeTimeR = 31;

    /// <summary>Defines the LREAL PLC soft datatype.</summary>
    private const uint SoftTypeLreal = 48;

    /// <summary>Defines the ULINT PLC soft datatype.</summary>
    private const uint SoftTypeUlint = 49;

    /// <summary>Defines the LINT PLC soft datatype.</summary>
    private const uint SoftTypeLint = 50;

    /// <summary>Defines the LWORD PLC soft datatype.</summary>
    private const uint SoftTypeLword = 51;

    /// <summary>Defines the USINT PLC soft datatype.</summary>
    private const uint SoftTypeUsint = 52;

    /// <summary>Defines the UINT PLC soft datatype.</summary>
    private const uint SoftTypeUint = 53;

    /// <summary>Defines the UDINT PLC soft datatype.</summary>
    private const uint SoftTypeUdint = 54;

    /// <summary>Defines the SINT PLC soft datatype.</summary>
    private const uint SoftTypeSint = 55;

    /// <summary>Defines the WCHAR PLC soft datatype.</summary>
    private const uint SoftTypeWchar = 61;

    /// <summary>Defines the WSTRING PLC soft datatype.</summary>
    private const uint SoftTypeWstring = 62;

    /// <summary>Extends browsed symbol sequences with catalog creation.</summary>
    /// <param name="symbols">The browsed controller symbols.</param>
    extension(IEnumerable<S7Symbol> symbols)
    {
        /// <summary>Builds a logical catalog retaining each symbol's native path and metadata.</summary>
        /// <returns>The logical catalog.</returns>
        public LogicalTagCatalog CreateLogicalTagCatalog()
        {
            Guard.NotNull(symbols, nameof(symbols));
            var catalog = new LogicalTagCatalog();
            foreach (var symbol in symbols)
            {
                Guard.NotNull(symbol, nameof(symbols));
                var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Protocol"] = "S7CommPlus",
                    ["SoftDataType"] = symbol.SoftDataType.ToString(CultureInfo.InvariantCulture),
                    ["IsAccessible"] = symbol.IsAccessible.ToString(),
                    ["IsOptimized"] = symbol.IsOptimized.ToString(),
                    ["ArrayLowerBounds"] = JoinBounds(symbol.ArrayLowerBounds),
                    ["ArrayLengths"] = JoinLengths(symbol.ArrayLengths),
                    ["MaximumStringLength"] = symbol.MaximumStringLength.ToString(CultureInfo.InvariantCulture),
                };
                catalog.Upsert(new(symbol.Path, symbol.Path, TypeName(symbol.SoftDataType) + (symbol.ArrayLengths.Count > 0 ? "[]" : string.Empty), new LogicalTagOptions
                {
                    AccessMode = symbol.IsReadOnly ? LogicalTagAccessMode.Read : LogicalTagAccessMode.ReadWrite,
                    Metadata = metadata,
                }));
            }

            return catalog;
        }
    }

    /// <summary>Extends a symbolic connection with logical alias composition.</summary>
    /// <param name="client">The symbolic connection.</param>
    extension(S7SymbolicClient client)
    {
        /// <summary>Creates a logical alias adapter over a symbolic connection.</summary>
        /// <param name="catalog">The alias catalog.</param>
        /// <param name="cycle">The requested controller notification interval.</param>
        /// <returns>The logical alias adapter.</returns>
        public S7SymbolicLogicalTagClient CreateSymbolicLogicalTagClient(ILogicalTagCatalog catalog, TimeSpan? cycle) => new(client, catalog, cycle);

        /// <summary>Creates a logical alias adapter using the default controller notification interval.</summary>
        /// <param name="catalog">The alias catalog.</param>
        /// <returns>The logical alias adapter.</returns>
        public S7SymbolicLogicalTagClient CreateSymbolicLogicalTagClient(ILogicalTagCatalog catalog) => new(client, catalog);
    }

    /// <summary>Formats lower bounds using invariant coordinates.</summary>
    /// <param name="bounds">The lower bounds.</param>
    /// <returns>The comma separated coordinates.</returns>
    private static string JoinBounds(IReadOnlyList<int> bounds)
    {
        var formatted = new string[bounds.Count];
        for (var index = 0; index < bounds.Count; index++)
        {
            formatted[index] = bounds[index].ToString(CultureInfo.InvariantCulture);
        }

        return string.Join(",", formatted);
    }

    /// <summary>Formats array lengths using invariant coordinates.</summary>
    /// <param name="lengths">The array lengths.</param>
    /// <returns>The comma separated lengths.</returns>
    private static string JoinLengths(IReadOnlyList<uint> lengths)
    {
        var formatted = new string[lengths.Count];
        for (var index = 0; index < lengths.Count; index++)
        {
            formatted[index] = lengths[index].ToString(CultureInfo.InvariantCulture);
        }

        return string.Join(",", formatted);
    }

    /// <summary>Maps the Siemens soft datatype to its catalog name.</summary>
    /// <param name="type">The controller soft datatype.</param>
    /// <returns>The catalog datatype name.</returns>
    private static string TypeName(uint type) => type switch
    {
        SoftTypeBool => "BOOL",
        SoftTypeByte => "BYTE",
        SoftTypeChar => "CHAR",
        SoftTypeWord => "WORD",
        SoftTypeInt => "INT",
        SoftTypeDword => "DWORD",
        SoftTypeDint => "DINT",
        SoftTypeReal => "REAL",
        SoftTypeDate => "DATE",
        SoftTypeTimeOFDAY => "TIME_OF_DAY",
        SoftTypeTime => "TIME",
        SoftTypeS5Time => "S5TIME",
        SoftTypeDateANDTIME => "DATE_AND_TIME",
        SoftTypeString => "STRING",
        SoftTypePointer => "POINTER",
        SoftTypeAny => "ANY",
        SoftTypeBlockfb => "BLOCK_FB",
        SoftTypeBlockfc => "BLOCK_FC",
        SoftTypeBlockdb => "BLOCK_DB",
        SoftTypeBlocksdb => "BLOCK_SDB",
        SoftTypeCounter => "COUNTER",
        SoftTypeTimeR => "TIMER",
        SoftTypeLreal => "LREAL",
        SoftTypeUlint => "ULINT",
        SoftTypeLint => "LINT",
        SoftTypeLword => "LWORD",
        SoftTypeUsint => "USINT",
        SoftTypeUint => "UINT",
        SoftTypeUdint => "UDINT",
        SoftTypeSint => "SINT",
        SoftTypeWchar => "WCHAR",
        SoftTypeWstring => "WSTRING",
        _ => $"S7SoftType{type.ToString(CultureInfo.InvariantCulture)}",
    };
}
