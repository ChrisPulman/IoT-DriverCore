// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
#if REACTIVE_SHIM
using S7PlcRx.Reactive.Enums;
using S7PlcRx.Reactive.PlcTypes;
#else
using S7PlcRx.Enums;
using S7PlcRx.PlcTypes;
#endif

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive;
#else
namespace S7PlcRx;
#endif

/// <summary>Contains address parsing members for <see cref="RxS7"/>.</summary>
public partial class RxS7
{
    /// <summary>
    /// Attempts to convert the specified read-only character span representation of a number to its 32-bit signed
    /// integer equivalent.
    /// </summary>
    /// <remarks>The conversion uses the invariant culture and expects the input to be in a valid integer
    /// format. No exception is thrown if the conversion fails.</remarks>
    /// <param name="s">A read-only span of characters that contains the number to convert.</param>
    /// <param name="value">When this method returns, contains the 32-bit signed integer value equivalent to the number
    /// contained
    /// in
    /// <paramref name="s"/>, if the conversion succeeded, or zero if the conversion failed. This parameter is passed
    /// uninitialized.</param>
    /// <returns><see langword="true"/> if the conversion succeeded; otherwise, <see langword="false"/>.</returns>
    private static bool TryParseInt(ReadOnlySpan<char> s, out int value) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    /// <summary>Attempts to parse a data block address for a multi-variable tag and extract its components.</summary>
    /// <remarks>The method supports addresses in the format "DBx.DBByy", "DBx.DBWyy", or "DBx.DBDyy", where x
    /// is the data block number and yy is the byte offset. The variable type is inferred from both the address and the
    /// tag's Type property. This method does not throw exceptions for invalid input; instead, it returns false and sets
    /// output values to their defaults.</remarks>
    /// <param name="tag">The tag containing the address and type information. Its Address property must be non-empty
    /// string, and ArrayLength must have a value.</param>
    /// <param name="db">The parsed data block number if parsing succeeds; otherwise, zero.</param>
    /// <param name="startByte">The starting byte offset within the data block if parsing succeeds;
    /// otherwise, zero.</param>
    /// <param name="varType">The variable type determined from the address and tag type if parsing
    /// succeeds; otherwise, <see cref="VarType.Byte"/>.</param>
    /// <param name="countBytes">The total bytes required for the variable or array if parsing
    /// succeeds; otherwise, zero.</param>
    /// <returns>true if the address was successfully parsed and all output values were set; otherwise, false.</returns>
    private static bool TryParseDbAddressForMultiVar(
        Tag tag,
        out int db,
        out int startByte,
        out VarType varType,
        out int countBytes)
    {
        db = 0;
        startByte = 0;
        varType = VarType.Byte;
        countBytes = 0;

        if (tag.ArrayLength is null || !TryParseDbAddressParts(tag.Address, out db, out var dbType, out startByte))
        {
            return false;
        }

        switch (dbType)
        {
            case "DBB":
                {
                    varType = VarType.Byte;
                    break;
                }

            case "DBW":
                {
                    varType = tag.Type == typeof(short) || tag.Type == typeof(short[]) ? VarType.Int : VarType.Word;
                    break;
                }

            case "DBD":
                {
                    varType = GetDbdMultiVarType(tag.Type);
                    break;
                }

            default:
                {
                    return false;
                }
        }

        countBytes = VarTypeToByteLength(varType, tag.ArrayLength.Value);
        return countBytes > 0;
    }

    /// <summary>Attempts to parse the DB number, type, and start byte from an S7 DB address.</summary>
    /// <param name="address">The source address.</param>
    /// <param name="db">The parsed DB number.</param>
    /// <param name="dbType">The parsed DB type prefix.</param>
    /// <param name="startByte">The parsed start byte.</param>
    /// <returns>true when the address parts are valid; otherwise, false.</returns>
    private static bool TryParseDbAddressParts(string? address, out int db, out string dbType, out int startByte)
    {
        db = 0;
        dbType = string.Empty;
        startByte = 0;

        if (address is null || string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        var normalized = address.Replace(" ", string.Empty).ToUpperInvariant();
        if (!normalized.AsSpan().StartsWith("DB".AsSpan(), StringComparison.Ordinal))
        {
            return false;
        }

        var parts = normalized.Split(['.']);
        if (parts.Length < DataBlockAddressComponentCount ||
            !TryParseInt(parts[0].AsSpan(DataBlockPrefixLength), out db))
        {
            return false;
        }

        var item = parts[1];
        if (item.Length < AddressTypeCodeLength || !TryParseInt(item.AsSpan(AddressTypeCodeLength), out startByte))
        {
            return false;
        }

        dbType = item[..AddressTypeCodeLength];
        return true;
    }

    /// <summary>Gets the multi-var type for a DBD address from the tag value type.</summary>
    /// <param name="tagType">The tag value type.</param>
    /// <returns>The multi-var type.</returns>
    private static VarType GetDbdMultiVarType(Type tagType) => tagType switch
    {
        _ when tagType == typeof(double) || tagType == typeof(double[]) => VarType.LReal,
        _ when tagType == typeof(float) || tagType == typeof(float[]) => VarType.Real,
        _ when tagType == typeof(int) || tagType == typeof(int[]) => VarType.DInt,
        _ => VarType.DWord,
    };

    /// <summary>
    /// Determines whether the specified tag is non-null and its type and value are compatible with the specified type
    /// parameter.
    /// </summary>
    /// <remarks>If the type parameter is object, any non-null tag is considered valid regardless of its type
    /// or value.</remarks>
    /// <typeparam name="T">The type to validate against the tag's type and value.</typeparam>
    /// <param name="tag">The tag to validate. May be null.</param>
    /// <returns>true if the tag is not null and its type and value match the specified type; otherwise,
    /// false.</returns>
    private static bool TagValueIsValid<T>(Tag? tag) =>
        tag is not null &&
        (typeof(T) == typeof(object) ||
            (tag.Type == typeof(T) && tag.Value?.GetType() == typeof(T)));

    /// <summary>
    /// Determines whether the specified tag's name matches the given variable and its type and value are compatible
    /// with the specified type parameter.
    /// </summary>
    /// <remarks>If T is object, only the name comparison is performed. For other types, both the tag's type
    /// and the runtime type of its value must match T.</remarks>
    /// <typeparam name="T">The type to compare against the tag's type and value.</typeparam>
    /// <param name="tag">The tag to validate. May be null.</param>
    /// <param name="variable">The variable name to compare with the tag name. The comparison is
    /// case-insensitive.</param>
    /// <returns>true if the tag's name matches the variable and the tag's type and value are compatible with type T;
    /// otherwise,
    /// false.</returns>
    private static bool TagValueIsValid<T>(Tag? tag, string? variable) =>
        string.Equals(tag?.Name, variable, StringComparison.InvariantCultureIgnoreCase) &&
        (typeof(T) == typeof(object) ||
            (tag?.Type == typeof(T) && tag.Value?.GetType() == typeof(T)));

    /// <summary>
    /// Creates a request package for reading data from a PLC using the specified data type, data block, start address,
    /// and count.
    /// </summary>
    /// <remarks>The structure of the request package varies depending on the specified data type. For Timer
    /// and Counter types, addressing and formatting differ from other data types. Ensure that the parameters are within
    /// valid ranges supported by the PLC protocol.</remarks>
    /// <param name="dataType">The data type to read from the PLC; it determines the request format and
    /// addressing.</param>
    /// <param name="db">The data block number from which to read. Must be a non-negative integer.</param>
    /// <param name="startByteAdr">The starting byte address within the data block. It must be non-negative.</param>
    /// <param name="count">The number of data items to read. Must be a positive integer. The default value is
    /// 1.</param>
    /// <returns>A <see cref="ByteArray"/> request package for reading the specified PLC data.</returns>
    private static ByteArray CreateReadDataRequestPackage(DataType dataType, int db, int startByteAdr, int count = 1)
    {
        var package = new ByteArray(ReadRequestItemSize);
        package.Add(ReadRequestItemPrefix);
        switch (dataType)
        {
            case DataType.Timer or DataType.Counter:
                {
                    package.Add((byte)dataType);
                    break;
                }

            default:
                {
                    package.Add(StandardReadTransportSize);
                    break;
                }
        }

        package.Add(Word.ToByteArray((ushort)count));
        package.Add(Word.ToByteArray((ushort)db));
        package.Add((byte)dataType);
        var overflow = startByteAdr * BitsPerByte / ushort.MaxValue;
        package.Add((byte)overflow);
        switch (dataType)
        {
            case DataType.Timer or DataType.Counter:
                {
                    package.Add(Word.ToByteArray((ushort)startByteAdr));
                    break;
                }

            default:
                {
                    package.Add(Word.ToByteArray((ushort)(startByteAdr * BitsPerByte)));
                    break;
                }
        }

        return package;
    }

    /// <summary>Creates a header package for a specified number of requests.</summary>
    /// <remarks>The returned header package is formatted with a fixed header size and includes fields that
    /// depend on the specified amount. This method is intended for use in constructing protocol-compliant request
    /// headers.</remarks>
    /// <param name="amount">The number of requests to include in the header package. It must be at least 1. The default
    /// is 1.</param>
    /// <returns>A ByteArray containing the constructed header package for the specified number of requests.</returns>
    private static ByteArray ReadHeaderPackage(int amount = 1)
    {
        var package = new ByteArray(ReadRequestHeaderSize);
        package.Add(TpktHeaderPrefix);

        // complete package size
        package.Add((byte)(ReadRequestHeaderSize + (ReadRequestItemSize * amount)));
        package.Add(ReadRequestHeaderBody);

        // data part size
        package.Add(Word.ToByteArray((ushort)(ReadParameterBaseSize + (amount * ReadRequestItemSize))));
        package.Add(ReadRequestFunction);

        // amount of requests
        package.Add((byte)amount);

        return package;
    }

    /// <summary>Calculates the byte count required for a value or array of the specified variable type.</summary>
    /// <remarks>This method is typically used to determine buffer sizes or offsets when working with raw data
    /// representations of various variable types. The calculation depends on the size of each type and the number of
    /// elements specified.</remarks>
    /// <param name="varType">The variable type for which to determine byte length.</param>
    /// <param name="varCount">The number of elements of the specified type. It must be at least 1.</param>
    /// <returns>The total bytes required to store the specified number of elements. Returns
    /// 0 if the variable type is not recognized.</returns>
    private static int VarTypeToByteLength(VarType varType, int varCount = 1) => varType switch
    {
        VarType.Bit or VarType.String => varCount,
        VarType.Byte => (varCount < 1) ? 1 : varCount,
        VarType.Word or VarType.Timer or VarType.Int or VarType.Counter => varCount * WordByteLength,
        VarType.DWord or VarType.DInt or VarType.Real => varCount * DoubleWordByteLength,
        VarType.LReal => varCount * LongRealByteLength,
        _ => 0,
    };
}
