// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using CP.IoT.Core;

#if REACTIVE_SHIM
namespace ModbusRx.Reactive.LogicalTags;
#else
namespace ModbusRx.LogicalTags;
#endif

/// <summary>Maps a logical name to a strongly typed Modbus address.</summary>
public sealed class ModbusLogicalTag
{
    /// <summary>Gets the reserved unit-id metadata key.</summary>
    internal const string UnitIdMetadata = "modbus.unitId";

    /// <summary>Gets the reserved data-area metadata key.</summary>
    internal const string DataAreaMetadata = "modbus.dataArea";

    /// <summary>Gets the reserved point-count metadata key.</summary>
    internal const string CountMetadata = "modbus.count";

    /// <summary>Gets the reserved byte-order metadata key.</summary>
    internal const string ByteOrderMetadata = "modbus.byteOrder";

    /// <summary>The protocol maximum number of bits in one request.</summary>
    private const ushort MaximumBitCount = 2000;

    /// <summary>The protocol maximum number of registers in one request.</summary>
    private const ushort MaximumRegisterCount = 125;

    /// <summary>Maps supported CLR types to stable common-catalog names.</summary>
    private static readonly Dictionary<Type, string> DataTypeNames = new()
    {
        [typeof(bool)] = "System.Boolean",
        [typeof(ushort)] = "System.UInt16",
        [typeof(short)] = "System.Int16",
        [typeof(uint)] = "System.UInt32",
        [typeof(int)] = "System.Int32",
        [typeof(float)] = "System.Single",
        [typeof(double)] = "System.Double",
        [typeof(bool[])] = "System.Boolean[]",
        [typeof(ushort[])] = "System.UInt16[]",
        [typeof(short[])] = "System.Int16[]",
        [typeof(uint[])] = "System.UInt32[]",
        [typeof(int[])] = "System.Int32[]",
        [typeof(float[])] = "System.Single[]",
        [typeof(double[])] = "System.Double[]",
    };

    /// <summary>Maps stable common-catalog names and C# aliases to CLR types.</summary>
    private static readonly Dictionary<string, Type> DataTypes = CreateDataTypes();

    /// <summary>Initializes a new instance of the <see cref="ModbusLogicalTag"/> class.</summary>
    /// <param name="configuration">The address and behavior configuration.</param>
    public ModbusLogicalTag(ModbusTagConfiguration configuration)
    {
        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        ValidateName(configuration.Name);
        ValidateRange(configuration.DataArea, configuration.Address, configuration.Count);
        ClrDataType = configuration.ClrDataType ?? throw new ArgumentNullException(nameof(configuration));
        ModbusTagCodec.ValidateType(configuration.DataArea, configuration.Count, configuration.ClrDataType);
        ValidateOptions(
            configuration.DataArea,
            configuration.ByteOrder,
            configuration.AccessMode,
            configuration.ScanInterval);
        Name = configuration.Name.Trim();
        UnitId = configuration.UnitId;
        DataArea = configuration.DataArea;
        Address = configuration.Address;
        Count = configuration.Count;
        ByteOrder = configuration.ByteOrder;
        GroupName = configuration.GroupName?.Trim() ?? string.Empty;
        Description = configuration.Description?.Trim() ?? string.Empty;
        Metadata = CopyMetadata(configuration.Metadata);
        AccessMode = configuration.AccessMode;
        ScanInterval = configuration.ScanInterval;
    }

    /// <summary>Gets the unique logical name.</summary>
    public string Name { get; }

    /// <summary>Gets the Modbus unit identifier.</summary>
    public byte UnitId { get; }

    /// <summary>Gets the Modbus data area.</summary>
    public ModbusDataArea DataArea { get; }

    /// <summary>Gets the zero-based Modbus address.</summary>
    public ushort Address { get; }

    /// <summary>Gets the number of coils, inputs, or registers.</summary>
    public ushort Count { get; }

    /// <summary>Gets the CLR value type exposed by the tag.</summary>
    public Type ClrDataType { get; }

    /// <summary>Gets the register byte and word order.</summary>
    public ModbusByteOrder ByteOrder { get; }

    /// <summary>Gets the optional group name.</summary>
    public string GroupName { get; }

    /// <summary>Gets the optional description.</summary>
    public string Description { get; }

    /// <summary>Gets caller-defined metadata.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>Gets the permitted access mode.</summary>
    public LogicalTagAccessMode AccessMode { get; }

    /// <summary>Gets the preferred observation interval.</summary>
    public TimeSpan? ScanInterval { get; }

    /// <summary>Converts a common logical tag to a validated Modbus definition.</summary>
    /// <param name="tag">The common logical-tag definition.</param>
    /// <returns>The validated Modbus definition.</returns>
    public static ModbusLogicalTag FromLogicalTag(LogicalTag tag)
    {
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        var configuration = new ModbusTagConfiguration(
            tag.Name,
            ParseByte(tag.Metadata, UnitIdMetadata),
            ParseEnum<ModbusDataArea>(tag.Metadata, DataAreaMetadata),
            ParseUShort(tag.Address, nameof(tag.Address)),
            ParseUShort(tag.Metadata, CountMetadata),
            ResolveDataType(tag.DataType))
        {
            ByteOrder = ParseEnum<ModbusByteOrder>(tag.Metadata, ByteOrderMetadata),
            GroupName = tag.GroupName,
            Description = tag.Description,
            Metadata = RemoveProtocolMetadata(tag.Metadata),
            AccessMode = tag.AccessMode,
            ScanInterval = tag.ScanInterval,
        };
        return new ModbusLogicalTag(configuration);
    }

    /// <summary>Converts this definition to the common logical-tag representation.</summary>
    /// <returns>The common definition.</returns>
    public LogicalTag ToLogicalTag()
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in Metadata)
        {
            metadata.Add(pair.Key, pair.Value);
        }

        metadata[UnitIdMetadata] = UnitId.ToString(CultureInfo.InvariantCulture);
        metadata[DataAreaMetadata] = DataArea.ToString();
        metadata[CountMetadata] = Count.ToString(CultureInfo.InvariantCulture);
        metadata[ByteOrderMetadata] = ByteOrder.ToString();
        var groupName = GroupName;
        var description = Description;
        var accessMode = AccessMode;
        var scanInterval = ScanInterval;

        return new LogicalTag(
            Name,
            Address.ToString(CultureInfo.InvariantCulture),
            GetDataTypeName(ClrDataType),
            new LogicalTagOptions
            {
                GroupName = groupName,
                Description = description,
                Metadata = metadata,
                AccessMode = accessMode,
                ScanInterval = scanInterval,
            });
    }

    /// <summary>Copies caller metadata after checking reserved keys.</summary>
    /// <param name="metadata">The caller metadata.</param>
    /// <returns>The validated copy.</returns>
    private static Dictionary<string, string> CopyMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (metadata is null)
        {
            return result;
        }

        foreach (var pair in metadata)
        {
            ValidateMetadataKey(pair.Key, metadata);
            if (pair.Key is UnitIdMetadata or DataAreaMetadata or CountMetadata or ByteOrderMetadata)
            {
                throw new ArgumentException($"Metadata key '{pair.Key}' is reserved by ModbusRx.", nameof(metadata));
            }

            result.Add(pair.Key, pair.Value ?? string.Empty);
        }

        return result;
    }

    /// <summary>Removes Modbus-owned metadata from a common definition.</summary>
    /// <param name="metadata">The common metadata.</param>
    /// <returns>The caller-owned metadata.</returns>
    private static Dictionary<string, string> RemoveProtocolMetadata(IReadOnlyDictionary<string, string> metadata)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in metadata)
        {
            if (pair.Key is not (UnitIdMetadata or DataAreaMetadata or CountMetadata or ByteOrderMetadata))
            {
                result.Add(pair.Key, pair.Value);
            }
        }

        return result;
    }

    /// <summary>Parses a byte metadata value.</summary>
    /// <param name="metadata">The common metadata.</param>
    /// <param name="key">The metadata key.</param>
    /// <returns>The parsed value.</returns>
    private static byte ParseByte(IReadOnlyDictionary<string, string> metadata, string key)
    {
        var value = GetMetadata(metadata, key);
        return byte.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"Metadata '{key}' is not a valid byte value.");
    }

    /// <summary>Parses a UInt16 metadata value.</summary>
    /// <param name="metadata">The common metadata.</param>
    /// <param name="key">The metadata key.</param>
    /// <returns>The parsed value.</returns>
    private static ushort ParseUShort(IReadOnlyDictionary<string, string> metadata, string key) =>
        ParseUShort(GetMetadata(metadata, key), key);

    /// <summary>Parses a UInt16 field value.</summary>
    /// <param name="value">The serialized value.</param>
    /// <param name="fieldName">The field name.</param>
    /// <returns>The parsed value.</returns>
    private static ushort ParseUShort(string value, string fieldName) =>
        ushort.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"'{fieldName}' is not a valid UInt16 value.");

    /// <summary>Parses an enum metadata value.</summary>
    /// <typeparam name="T">The enum type.</typeparam>
    /// <param name="metadata">The common metadata.</param>
    /// <param name="key">The metadata key.</param>
    /// <returns>The parsed value.</returns>
    private static T ParseEnum<T>(IReadOnlyDictionary<string, string> metadata, string key)
        where T : struct
    {
        var value = GetMetadata(metadata, key);
        return Enum.TryParse<T>(value, true, out var parsed) && Enum.IsDefined(typeof(T), parsed)
            ? parsed
            : throw new FormatException($"Metadata '{key}' is not a valid {nameof(T)} value.");
    }

    /// <summary>Gets a required metadata value.</summary>
    /// <param name="metadata">The common metadata.</param>
    /// <param name="key">The metadata key.</param>
    /// <returns>The serialized value.</returns>
    private static string GetMetadata(IReadOnlyDictionary<string, string> metadata, string key) =>
        metadata.TryGetValue(key, out var value)
            ? value
            : throw new FormatException($"Required Modbus metadata '{key}' is missing.");

    /// <summary>Gets a stable common-catalog type name.</summary>
    /// <param name="type">The CLR type.</param>
    /// <returns>The stable name.</returns>
    private static string GetDataTypeName(Type type) =>
        DataTypeNames.TryGetValue(type, out var value)
            ? value
            : throw new NotSupportedException($"CLR type '{type}' is not supported by ModbusRx logical tags.");

    /// <summary>Resolves a stable common-catalog type name.</summary>
    /// <param name="dataType">The stable name or C# alias.</param>
    /// <returns>The CLR type.</returns>
    private static Type ResolveDataType(string dataType) =>
        DataTypes.TryGetValue(dataType, out var value)
            ? value
            : throw new FormatException($"CLR data type '{dataType}' is not supported by ModbusRx.");

    /// <summary>Creates the stable-name and C#-alias type lookup.</summary>
    /// <returns>The type lookup.</returns>
    private static Dictionary<string, Type> CreateDataTypes()
    {
        var result = DataTypeNames.ToDictionary(
            static pair => pair.Value,
            static pair => pair.Key,
            StringComparer.Ordinal);
        foreach (var pair in new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["bool"] = typeof(bool), ["ushort"] = typeof(ushort), ["short"] = typeof(short),
            ["uint"] = typeof(uint), ["int"] = typeof(int), ["float"] = typeof(float), ["double"] = typeof(double),
            ["bool[]"] = typeof(bool[]), ["ushort[]"] = typeof(ushort[]), ["short[]"] = typeof(short[]),
            ["uint[]"] = typeof(uint[]),
            ["int[]"] = typeof(int[]),
            ["float[]"] = typeof(float[]),
            ["double[]"] = typeof(double[]),
        })
        {
            result.Add(pair.Key, pair.Value);
        }

        return result;
    }

    /// <summary>Validates the logical name.</summary>
    /// <param name="name">The logical name.</param>
    private static void ValidateName(string name)
    {
        _ = !string.IsNullOrWhiteSpace(name)
            ? true
            : throw new ArgumentException("A non-empty value is required.", nameof(name));
    }

    /// <summary>Validates the selected data-area range.</summary>
    /// <param name="dataArea">The data area.</param>
    /// <param name="address">The starting address.</param>
    /// <param name="count">The point count.</param>
    private static void ValidateRange(ModbusDataArea dataArea, ushort address, ushort count)
    {
        if (dataArea is < ModbusDataArea.Coil or > ModbusDataArea.InputRegister)
        {
            throw new ArgumentOutOfRangeException(nameof(dataArea));
        }

        var maximumCount = dataArea is ModbusDataArea.Coil or ModbusDataArea.DiscreteInput
            ? MaximumBitCount
            : MaximumRegisterCount;
        _ = count > 0 && count <= maximumCount && (uint)address + count <= ushort.MaxValue + 1U
            ? true
            : throw new ArgumentOutOfRangeException(
                nameof(count),
                "The requested range exceeds the Modbus data-area limits.");
    }

    /// <summary>Validates access, ordering, and scan options.</summary>
    /// <param name="dataArea">The data area.</param>
    /// <param name="byteOrder">The byte order.</param>
    /// <param name="accessMode">The access mode.</param>
    /// <param name="scanInterval">The optional scan interval.</param>
    private static void ValidateOptions(
        ModbusDataArea dataArea,
        ModbusByteOrder byteOrder,
        LogicalTagAccessMode accessMode,
        TimeSpan? scanInterval)
    {
        if (byteOrder is < ModbusByteOrder.BigEndian or > ModbusByteOrder.LittleEndianWordSwap)
        {
            throw new ArgumentOutOfRangeException(nameof(byteOrder));
        }

        if (dataArea is ModbusDataArea.DiscreteInput or ModbusDataArea.InputRegister &&
            accessMode != LogicalTagAccessMode.Read)
        {
            throw new ArgumentException("Modbus input areas are read-only.", nameof(accessMode));
        }

        _ = scanInterval is null || scanInterval > TimeSpan.Zero
            ? true
            : throw new ArgumentOutOfRangeException(nameof(scanInterval));
    }

    /// <summary>Validates a caller metadata key.</summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="metadata">The metadata dictionary used for the parameter name.</param>
    private static void ValidateMetadataKey(string key, IReadOnlyDictionary<string, string>? metadata)
    {
        _ = !string.IsNullOrWhiteSpace(key)
            ? true
            : throw new ArgumentException("Metadata keys cannot be empty.", nameof(metadata));
    }
}
