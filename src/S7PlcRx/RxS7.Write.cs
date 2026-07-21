// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
#if REACTIVE_SHIM
using S7PlcRx.Reactive.Enums;
#else
using S7PlcRx.Enums;
#endif

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive;
#else
namespace S7PlcRx;
#endif

/// <summary>Contains write-operation members for <see cref="RxS7"/>.</summary>
public partial class RxS7
{
    /// <summary>
    /// Takes in input an object and tries to parse it to an array of values. This can be used
    /// to write many data, all of the same type. You must specify the memory area type, memory
    /// are address, byte start address and bytes count. If the read was not successful, check
    /// LastErrorCode or LastErrorString.
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <param name="dataType">
    /// Data type of the memory area, can be DB, Timer, Counter, Memory, Input, Output.
    /// </param>
    /// <param name="db">
    /// Address of the memory area (if you want to read DB1, this is set to 1). This must be set
    /// also for other memory area types: counters, timers,etc.
    /// </param>
    /// <param name="startByteAdr">
    /// Start byte address. If you want to read DB1.DBW200, this is 200.
    /// </param>
    /// <returns>NoError if it was successful, or the error is specified.</returns>
    private bool WriteCore(Tag tag, DataType dataType, int db, int startByteAdr)
    {
        if (tag.NewValue is null)
        {
            return false;
        }

        if (!TrySerializeTagNewValue(tag, out _, out var package))
        {
            _lastErrorCode.OnNext(ErrorCode.WrongVarFormat);
            return false;
        }

        return package.Length > 200 && dataType == DataType.DataBlock
            ? WriteMultipleBytes(tag, [.. package], db, startByteAdr)
            : WriteBytes(tag, dataType, db, startByteAdr, package);
    }

    /// <summary>Writes bytes to the specified data block starting at the given byte address.</summary>
    /// <remarks>The method writes the bytes in chunks, with each chunk containing up to 200 bytes. If an
    /// error occurs during writing, the operation stops and returns false.</remarks>
    /// <param name="tag">The tag to which the bytes will be written.</param>
    /// <param name="bytes">The list of bytes to write to the data block. Cannot be null or empty.</param>
    /// <param name="db">The data block number within the tag where the bytes will be written.</param>
    /// <param name="startByteAdr">The starting byte address within the data block at which to begin writing. Defaults
    /// to 0.</param>
    /// <returns>true if all bytes are written successfully; otherwise, false.</returns>
    private bool WriteMultipleBytes(Tag tag, List<byte> bytes, int db, int startByteAdr = 0)
    {
        var errCode = false;
        var index = startByteAdr;
        try
        {
            while (bytes.Count > 0)
            {
                var maxToWrite = Math.Min(bytes.Count, MaximumWriteChunkSize);
                var part = new byte[maxToWrite];
                bytes.CopyTo(0, part, 0, maxToWrite);
                errCode = WriteBytes(tag, DataType.DataBlock, db, index, part);
                bytes.RemoveRange(0, maxToWrite);
                index += maxToWrite;
                if (!errCode)
                {
                    break;
                }
            }
        }
        catch (Exception exc)
        {
            _lastErrorCode.OnNext(ErrorCode.WriteData);
            _lastError.OnNext($"An error occurred while writing data:{exc.Message}");
        }

        return errCode;
    }

    /// <summary>Writes the specified tag value to the PLC memory area identified by its address.</summary>
    /// <remarks>Supported address formats include data blocks (e.g., DBB, DBW, DBD, DBX, DBS), inputs (E or
    /// I), outputs (A or O), memory (M), timers (T), and counters (C or Z). The method validates address syntax and bit
    /// ranges, and updates error state if parsing fails. If the tag is null or the address is invalid, the method
    /// returns false and sets error information.</remarks>
    /// <param name="tag">The tag containing the address and value to write. The address must be in a supported PLC
    /// address format.
    /// Cannot
    /// be null.</param>
    /// <returns>true if the value was successfully written to the PLC; otherwise, false.</returns>
    private bool WriteString(Tag? tag)
    {
        if (tag is null)
        {
            return false;
        }

        var tagAddress = tag.Address!.ToUpper();
        tagAddress = tagAddress.Replace(" ", string.Empty); // Remove spaces

        try
        {
            return tagAddress[..AreaAddressCodeLength] switch
            {
                "DB" => WriteDataBlockAddress(tag, tagAddress),
                "EB" or "EW" or "ED" => WriteCore(
                    tag,
                    DataType.Input,
                    0,
                    int.Parse(tagAddress[AreaAddressCodeLength..])),
                "AB" or "AW" or "AD" => WriteCore(
                    tag,
                    DataType.Output,
                    0,
                    int.Parse(tagAddress[AreaAddressCodeLength..])),
                "MB" or "MW" or "MD" => WriteCore(
                    tag,
                    DataType.Memory,
                    0,
                    int.Parse(tagAddress[AreaAddressCodeLength..])),
                _ => WriteSpecialOrBitAddress(tag, tagAddress),
            };
        }
        catch (Exception exc)
        {
            _lastErrorCode.OnNext(ErrorCode.WrongVarFormat);
            _lastError.OnNext(
                string.Concat(
                    $"The variable'{tag}' could not be parsed. Please check the syntax and try again.\n",
                    $"Exception: {exc.Message}"));
            return false;
        }
    }

    /// <summary>Writes a data block address.</summary>
    /// <param name="tag">The tag to write.</param>
    /// <param name="tagAddress">The normalized tag address.</param>
    /// <returns>true if the write succeeds; otherwise, false.</returns>
    private bool WriteDataBlockAddress(Tag tag, string tagAddress)
    {
        var strings = tagAddress.Split(['.']);
        if (strings.Length < DataBlockAddressComponentCount)
        {
            throw new ArgumentException($"Cannot parse DB address '{tagAddress}'.", nameof(tag));
        }

        var dbNumber = int.Parse(strings[0][DataBlockPrefixLength..]);
        var dbType = strings[1][..AddressTypeCodeLength];
        var dbIndex = int.Parse(strings[1][AddressTypeCodeLength..]);
        return dbType switch
        {
            "DBB" or "DBW" or "DBD" or "DBS" => WriteCore(tag, DataType.DataBlock, dbNumber, dbIndex),
            "DBX" => WriteDataBlockBitAddress(tag, strings, dbNumber, dbIndex),
            _ => throw new ArgumentException(
                string.Concat(
                    $"Addressing Error: Unable to parse address {dbType}. ",
                    "Supported formats include DBB (BYTE), DBW (WORD), DBD (DWORD), DBX (BITWISE), DBS (STRING)."),
                nameof(tag)),
        };
    }

    /// <summary>Writes a data block bit address.</summary>
    /// <param name="tag">The tag to write.</param>
    /// <param name="strings">The parsed address parts.</param>
    /// <param name="dbNumber">The data block number.</param>
    /// <param name="byteOffset">The byte offset.</param>
    /// <returns>true if the write succeeds; otherwise, false.</returns>
    private bool WriteDataBlockBitAddress(Tag tag, string[] strings, int dbNumber, int byteOffset)
    {
        var bitOffset = int.Parse(strings[BitAddressComponentIndex]);
        RxS7ValueHelpers.EnsureBitOffsetIsValid(bitOffset, tag);
        var value = Read<byte>(tag, DataType.DataBlock, dbNumber, byteOffset, VarType.Byte);
        tag.NewValue = RxS7ValueHelpers.SetBit(
            value,
            bitOffset,
            Convert.ToInt32(tag.NewValue, CultureInfo.InvariantCulture) == 1);
        return WriteCore(tag, DataType.DataBlock, dbNumber, byteOffset);
    }

    /// <summary>Writes a timer, counter, or bit address outside data blocks.</summary>
    /// <param name="tag">The tag to write.</param>
    /// <param name="tagAddress">The normalized tag address.</param>
    /// <returns>true if the write succeeds; otherwise, false.</returns>
    private bool WriteSpecialOrBitAddress(Tag tag, string tagAddress) => tagAddress[..1] switch
    {
        "E" or "I" => WriteBitAddress(tag, tagAddress, DataType.Input),
        "A" or "O" => WriteBitAddress(tag, tagAddress, DataType.Output),
        "M" => WriteBitAddress(tag, tagAddress, DataType.Memory),
        "T" => WriteCore(tag, DataType.Timer, 0, int.Parse(tagAddress[1..])),
        "Z" or "C" => WriteCore(tag, DataType.Counter, 0, int.Parse(tagAddress[1..])),
        _ => throw new ArgumentException($"Unknown variable type {tagAddress[..1]}.", nameof(tag)),
    };

    /// <summary>Writes a bit address from the specified data area.</summary>
    /// <param name="tag">The tag to write.</param>
    /// <param name="tagAddress">The normalized tag address.</param>
    /// <param name="writeDataType">The data area type.</param>
    /// <returns>true if the write succeeds; otherwise, false.</returns>
    private bool WriteBitAddress(Tag tag, string tagAddress, DataType writeDataType)
    {
        var addressLocation = tagAddress[1..];
        var decimalPointIndex = addressLocation.IndexOf('.');
        if (decimalPointIndex == -1)
        {
            throw new ArgumentException(
                string.Concat(
                    $"Cannot parse variable {addressLocation}. ",
                    "Input, Output, Memory Address, Timer, and Counter types ",
                    "require bit-level addressing (e.g. I0.1)."),
                nameof(tag));
        }

        var byteOffset = int.Parse(addressLocation[..decimalPointIndex]);
        var bitOffset = int.Parse(addressLocation[(decimalPointIndex + 1)..]);
        RxS7ValueHelpers.EnsureBitOffsetIsValid(bitOffset, tag);

        var value = Read<byte>(tag, writeDataType, 0, byteOffset, VarType.Byte);
        tag.NewValue = RxS7ValueHelpers.ApplyBitWriteValue(value, tag.NewValue!, bitOffset);
        return WriteCore(tag, writeDataType, 0, byteOffset);
    }
}
