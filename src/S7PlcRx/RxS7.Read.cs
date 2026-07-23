// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using IoT.DriverCore.S7PlcRx.Reactive.Enums;
using IoT.DriverCore.S7PlcRx.Reactive.PlcTypes;
#else
using IoT.DriverCore.S7PlcRx.Enums;
using IoT.DriverCore.S7PlcRx.PlcTypes;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.S7PlcRx.Reactive;
#else
namespace IoT.DriverCore.S7PlcRx;
#endif

/// <summary>Contains read-operation members for <see cref="RxS7"/>.</summary>
public partial class RxS7
{
    /// <summary>Publishes CPU information after the PLC connection becomes available.</summary>
    /// <param name="observer">The observer that receives CPU information.</param>
    /// <param name="cancellationToken">The token used to stop retrying CPU information requests.</param>
    /// <returns>A task that represents the asynchronous publish operation.</returns>
    private async Task PublishCpuInfoAsync(IObserver<string[]> observer, CancellationToken cancellationToken)
    {
        var cpuData = GetSzlDataSynchronized(CpuInformationSzlId);
        while (cpuData.Data.Length == 0 && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(SzlRetryDelayMilliseconds, cancellationToken).ConfigureAwait(true);
            cpuData = GetSzlDataSynchronized(CpuInformationSzlId);
        }

        var orderCode = GetSzlDataSynchronized(CpuOrderCodeSzlId);
        while (orderCode.Data.Length == 0 && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(SzlRetryDelayMilliseconds, cancellationToken).ConfigureAwait(true);
            orderCode = GetSzlDataSynchronized(CpuOrderCodeSzlId);
        }

        if (cpuData.Data.Length < CpuInformationMinimumLength ||
            orderCode.Data.Length < CpuOrderCodeMinimumLength)
        {
            return;
        }

        var information = new List<string>
        {
            PlcTypes.String.FromByteArray(cpuData.Data, CpuAsNameOffset, CpuAsNameLength)
                .Replace("\0", string.Empty),
            PlcTypes.String.FromByteArray(cpuData.Data, CpuModuleNameOffset, CpuModuleNameLength)
                .Replace("\0", string.Empty),
            PlcTypes.String.FromByteArray(cpuData.Data, CpuCopyrightOffset, CpuCopyrightLength)
                .Replace("\0", string.Empty),
            PlcTypes.String.FromByteArray(cpuData.Data, CpuSerialNumberOffset, CpuSerialNumberLength)
                .Replace("\0", string.Empty),
            PlcTypes.String.FromByteArray(cpuData.Data, CpuModuleTypeOffset, CpuModuleTypeLength)
                .Replace("\0", string.Empty),
            PlcTypes.String.FromByteArray(orderCode.Data, CpuOrderCodeOffset, CpuOrderCodeLength)
                .Replace("\0", string.Empty),
            $"V1: {orderCode.Data[orderCode.Size - CpuVersionMajorDistanceFromEnd]}",
            $"V2: {orderCode.Data[orderCode.Size - CpuVersionMinorDistanceFromEnd]}",
            $"V3: {orderCode.Data[orderCode.Size - 1]}",
        };
        observer.OnNext([.. information]);
        observer.OnCompleted();
    }

    /// <summary>Reads SZL data while excluding concurrent polling and value operations on the shared socket.</summary>
    /// <param name="szlId">The system status list identifier.</param>
    /// <returns>The SZL payload and reported size.</returns>
    private (byte[] Data, ushort Size) GetSzlDataSynchronized(ushort szlId)
    {
        lock (_socketLock)
        {
            return _socketRx.GetSZLData(szlId);
        }
    }

    /// <summary>
    /// Writes a single variable from the PLC, takes in input strings like "DB1.DBX0.0",
    /// "DB20.DBD200", "MB20", "T45", etc. If the write was not successful, check LastErrorCode
    /// or LastErrorString.
    /// </summary>
    /// <param name="tag">The tag. For the Address Input strings like "DB1.DBX0.0", "DB20.DBD200", "MB20", "T45",
    /// etc.</param>
    private void QueueWrite(Tag tag)
    {
#if NET8_0_OR_GREATER
        Guard.NotNullOrWhiteSpace(tag.Address, nameof(tag));
#else
        if (string.IsNullOrWhiteSpace(tag.Address))
        {
            throw new ArgumentNullException(nameof(tag.Address));
        }
#endif

        if (tag.NewValue is null)
        {
            throw new ArgumentNullException(nameof(tag.NewValue));
        }

        _plcRequestSubject.OnNext(new(PlcRequestType.Write, tag));
    }

    /// <summary>
    /// Writes up to 200 bytes to the PLC and returns NoError if successful. You must specify
    /// the memory area type, memory are address, byte start address and bytes count. If the
    /// read was not successful, check LastErrorCode or LastErrorString.
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
    /// <param name="value">
    /// Bytes to write. The length of this parameter can't be higher than 200. If you need more,
    /// use recursion.
    /// </param>
    /// <returns>NoError if it was successful, or the error is specified.</returns>
    private bool WriteBytes(Tag tag, DataType dataType, int db, int startByteAdr, byte[] value)
    {
        lock (_socketLock)
        {
            var receiveBuffer = new byte[WriteResponseBufferSize];
            try
            {
                var varCount = value.Length;

                // first create the header
                var packageSize = WriteRequestBaseSize + value.Length;
                using var package = new ByteArray(packageSize);

                package.Add(TpktHeaderPrefix);
                package.Add((byte)packageSize);
                package.Add(WriteRequestHeaderBody);
                package.Add(Word.ToByteArray((ushort)(varCount - 1)));
                package.Add(Word.ToByteArray(WriteParameterSize));
                package.Add(Word.ToByteArray((ushort)(varCount + WriteDataLengthOverhead)));
                package.Add(WriteRequestItemPrefix);
                package.Add(Word.ToByteArray((ushort)varCount));
                package.Add(Word.ToByteArray((ushort)db));
                package.Add((byte)dataType);
                var overflow = startByteAdr * BitsPerByte / ushort.MaxValue;
                package.Add((byte)overflow);
                package.Add(Word.ToByteArray((ushort)(startByteAdr * BitsPerByte)));
                package.Add(WriteDataTransportPrefix);
                package.Add(Word.ToByteArray((ushort)(varCount * BitsPerByte)));

                // now join the header and the data
                package.Add(value);

                var sent = _socketRx.Send(tag, package.Array, package.Array.Length);
                if (package.Array.Length != sent)
                {
                    return false;
                }

                _ = _socketRx.Receive(tag, receiveBuffer, WriteResponseBufferSize);

                if (receiveBuffer[ResponseReturnCodeOffset] != 0xff)
                {
                    _lastErrorCode.OnNext(ErrorCode.WriteData);
                    _lastError.OnNext(
                        $"Tag {tag.Name} failed to write - {nameof(ErrorCode.WrongNumberReceivedBytes)} " +
                        $"code {receiveBuffer[ResponseReturnCodeOffset]}");
                    return false;
                }

                _lastErrorCode.OnNext(ErrorCode.NoError);
                return true;
            }
            catch (Exception exc)
            {
                _lastErrorCode.OnNext(ErrorCode.WriteData);
                _lastError.OnNext(exc.Message);
                return false;
            }
        }
    }

    /// <summary>Attempts to read a value for the specified tag and assigns it to the tag if successful.</summary>
    /// <remarks>If the tag cannot be read or an error occurs, the tag's value is not modified.</remarks>
    /// <param name="tag">The tag for which to retrieve and assign a value. No action occurs if it is null.
    /// is taken.</param>
    private void GetTagValue(Tag? tag)
    {
        var result = Read(tag);
        if (tag is null || result is null || result is ErrorCode)
        {
            return;
        }

        tag.Value = result;
    }

    /// <summary>Parses a byte array into an object of the specified variable type and count.</summary>
    /// <remarks>The returned object type depends on the specified <paramref name="varType"/> and <paramref
    /// name="varCount"/>. For example, if <paramref name="varType"/> is <c>Word</c> and <paramref name="varCount"/> is
    /// 1, a single <c>Word</c> object is returned; if <paramref name="varCount"/> is greater than 1, an array of
    /// <c>Word</c> objects is returned. For <c>Bit</c> type, a <see cref="bool"/> is returned. If an error
    /// occurs during parsing, the method returns <see langword="null"/>.</remarks>
    /// <param name="varType">The variable type used to parse the byte array. It determines the interpretation of
    /// <paramref
    /// name="bytes"/>.</param>
    /// <param name="bytes">The byte array containing raw data. If it is <see langword="null"/>, the method returns <see
    /// langword="null"/>.</param>
    /// <param name="varCount">The variables to parse. It must be 1 for scalars; a larger value returns an
    /// array of values.</param>
    /// <param name="expectedType">The declared CLR type used to preserve signed counter representations.</param>
    /// <returns>An object representing the parsed value(s) according to <paramref name="varType"/> and <paramref
    /// name="varCount"/>. Returns a single value if <paramref name="varCount"/> is 1, or an array of values if greater
    /// than 1. Returns <see langword="null"/> if <paramref name="bytes"/> is <see langword="null"/> or if the type is
    /// not recognized.</returns>
    private object? ParseBytes(VarType varType, byte[] bytes, int varCount, Type expectedType)
    {
        try
        {
            if (bytes is null)
            {
                return default;
            }

            if (varType == VarType.Counter)
            {
                if (expectedType == typeof(short))
                {
                    return Int.FromByteArray(bytes);
                }

                if (expectedType == typeof(short[]))
                {
                    return Int.ToArray(bytes);
                }
            }

            return RxS7ValueHelpers.ParseNonNullBytes(varType, bytes, varCount);
        }
        catch (Exception ex)
        {
            _lastError.OnNext(ex.Message);
        }

        return default;
    }

    /// <summary>
    /// Read and decode a certain number of bytes of the "VarType" provided. This can be used to
    /// read multiple consecutive variables of the same type (Word, DWord, Int, etc). If the
    /// read was not successful, check LastErrorCode or LastErrorString.
    /// </summary>
    /// <typeparam name="T">The t value type.</typeparam>
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
    /// <param name="varType">Type of the variable/s that you are reading.</param>
    /// <returns>An object.</returns>
    private T? Read<T>(Tag tag, DataType dataType, int db, int startByteAdr, VarType varType)
    {
        try
        {
            _lock.Wait();
            var cntBytes = VarTypeToByteLength(varType, tag.ArrayLength!.Value);
            var bytes = ReadMultipleBytes(tag, dataType, db, startByteAdr, cntBytes);
            return bytes?.Length > 0
                ? (T?)ParseBytes(varType, bytes!, tag.ArrayLength!.Value, typeof(T))
                : default;
        }
        catch (Exception ex)
        {
            _lastError.OnNext(ex.Message);
        }
        finally
        {
            _ = _lock.Release();
        }

        return default;
    }

    /// <summary>
    /// Reads the value from the PLC at the address specified by the given tag, interpreting the data type and memory
    /// area based on the tag's address and type.
    /// </summary>
    /// <remarks>The method determines the PLC memory area and data type to read based on the format of the
    /// tag's Address property and the Type property. Supported areas include data blocks, inputs, outputs, memory,
    /// timers, and counters. The caller should ensure that the tag's Type matches the expected data at the specified
    /// address. If the address format is invalid or unsupported, the method returns <see langword="false"/> and sets
    /// error information.</remarks>
    /// <param name="tag">The tag that specifies the PLC address and expected data type. Its Address must not be
    /// null, empty, or whitespace.</param>
    /// <returns>An object containing the value read from the PLC. The type of the returned object
    /// depends on the tag's Type property and the address format. Returns <see langword="false"/> if the read operation
    /// fails due to an invalid address or format.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="tag"/> or its Address property is null, empty,
    /// or
    /// whitespace.</exception>
    private object? Read(Tag? tag)
    {
#if NET8_0_OR_GREATER
        Guard.NotNullOrWhiteSpace(tag?.Address, nameof(tag));
#else
        if (string.IsNullOrWhiteSpace(tag?.Address))
        {
            throw new ArgumentNullException(nameof(tag));
        }
#endif

        // remove spaces
        var correctVariable = tag!.Address!.ToUpper().Replace(" ", string.Empty);

        try
        {
            return correctVariable[..AreaAddressCodeLength] switch
            {
                "DB" => ReadDataBlockAddress(tag, correctVariable),
                "EB" => ReadByteAddress(tag, DataType.Input, int.Parse(correctVariable[AreaAddressCodeLength..])),
                "EW" => ReadWordAddress(
                    tag,
                    DataType.Input,
                    int.Parse(correctVariable[AreaAddressCodeLength..]),
                    VarType.Word,
                    VarType.Word),
                "ED" => ReadAreaDWordAddress(tag, DataType.Input, int.Parse(correctVariable[AreaAddressCodeLength..])),
                "AB" => ReadByteAddress(tag, DataType.Output, int.Parse(correctVariable[AreaAddressCodeLength..])),
                "AW" => ReadWordAddress(
                    tag,
                    DataType.Output,
                    int.Parse(correctVariable[AreaAddressCodeLength..]),
                    VarType.Word,
                    VarType.Word),
                "AD" => ReadAreaDWordAddress(tag, DataType.Output, int.Parse(correctVariable[AreaAddressCodeLength..])),
                "MB" => ReadByteAddress(tag, DataType.Memory, int.Parse(correctVariable[AreaAddressCodeLength..])),
                "MW" => ReadWordAddress(
                    tag,
                    DataType.Memory,
                    int.Parse(correctVariable[AreaAddressCodeLength..]),
                    VarType.Word,
                    VarType.Word),
                "MD" => ReadMemoryDWordAddress(tag, int.Parse(correctVariable[AreaAddressCodeLength..])),
                _ => ReadSpecialOrBitAddress(tag, correctVariable),
            };
        }
        catch
        {
            _lastErrorCode.OnNext(ErrorCode.WrongVarFormat);
            _lastError.OnNext(
                $"The variable'{tag.Address}' could not be read. Please check the syntax and try again.");
            return false;
        }
    }

    /// <summary>Reads a data block address.</summary>
    /// <param name="tag">The tag to read.</param>
    /// <param name="correctVariable">The normalized variable address.</param>
    /// <returns>The value read from the data block address.</returns>
    private object? ReadDataBlockAddress(Tag tag, string correctVariable)
    {
        var strings = correctVariable.Split(['.']);
        if (strings.Length < DataBlockAddressComponentCount)
        {
            throw new ArgumentException($"Cannot parse DB address '{correctVariable}'.", nameof(tag));
        }

        var dbNumber = int.Parse(strings[0][DataBlockPrefixLength..]);
        var dbType = strings[1][..AddressTypeCodeLength];
        var dbIndex = int.Parse(strings[1][AddressTypeCodeLength..]);
        return dbType switch
        {
            "DBB" => ReadByteAddress(tag, DataType.DataBlock, dbIndex, dbNumber),
            "DBW" => ReadWordAddress(tag, DataType.DataBlock, dbIndex, VarType.Int, VarType.Word, dbNumber),
            "DBD" => ReadDataBlockDWordAddress(tag, dbNumber, dbIndex),
            "DBX" => ReadDataBlockBitAddress(tag, strings, dbNumber, dbIndex),
            _ => throw new ArgumentException($"Unable to parse DB address type '{dbType}'.", nameof(tag)),
        };
    }

    /// <summary>Reads a byte address from the specified data area.</summary>
    /// <param name="tag">The tag to read.</param>
    /// <param name="dataType">The data area type.</param>
    /// <param name="startByteAdr">The byte offset.</param>
    /// <param name="db">The data block number.</param>
    /// <returns>The byte value or array.</returns>
    private object? ReadByteAddress(Tag tag, DataType dataType, int startByteAdr, int db = 0) =>
        tag.Type == typeof(byte[])
            ? Read<byte[]>(tag, dataType, db, startByteAdr, VarType.Byte)
            : Read<byte>(tag, dataType, db, startByteAdr, VarType.Byte);

    /// <summary>Reads a word address from the specified data area.</summary>
    /// <param name="tag">The tag to read.</param>
    /// <param name="dataType">The data area type.</param>
    /// <param name="startByteAdr">The byte offset.</param>
    /// <param name="signedVarType">The variable type for signed values.</param>
    /// <param name="unsignedVarType">The variable type for unsigned values.</param>
    /// <param name="db">The data block number.</param>
    /// <returns>The word value or array.</returns>
    private object? ReadWordAddress(
        Tag tag,
        DataType dataType,
        int startByteAdr,
        VarType signedVarType,
        VarType unsignedVarType,
        int db = 0)
    {
        if (tag.Type == typeof(short[]))
        {
            return Read<short[]>(tag, dataType, db, startByteAdr, signedVarType);
        }

        if (tag.Type == typeof(short))
        {
            return Read<short>(tag, dataType, db, startByteAdr, signedVarType);
        }

        return tag.Type == typeof(ushort[])
            ? Read<ushort[]>(tag, dataType, db, startByteAdr, unsignedVarType)
            : Read<ushort>(tag, dataType, db, startByteAdr, unsignedVarType);
    }

    /// <summary>Reads a data block double-word address.</summary>
    /// <param name="tag">The tag to read.</param>
    /// <param name="db">The data block number.</param>
    /// <param name="startByteAdr">The byte offset.</param>
    /// <returns>The double-word value or array.</returns>
    private object? ReadDataBlockDWordAddress(Tag tag, int db, int startByteAdr)
    {
        if (tag.Type == typeof(double))
        {
            return Read<double>(tag, DataType.DataBlock, db, startByteAdr, VarType.LReal);
        }

        if (tag.Type == typeof(double[]))
        {
            return Read<double[]>(tag, DataType.DataBlock, db, startByteAdr, VarType.LReal);
        }

        if (tag.Type == typeof(float))
        {
            return Read<float>(tag, DataType.DataBlock, db, startByteAdr, VarType.Real);
        }

        if (tag.Type == typeof(float[]))
        {
            return Read<float[]>(tag, DataType.DataBlock, db, startByteAdr, VarType.Real);
        }

        if (tag.Type == typeof(int))
        {
            return Read<int>(tag, DataType.DataBlock, db, startByteAdr, VarType.DInt);
        }

        if (tag.Type == typeof(int[]))
        {
            return Read<int[]>(tag, DataType.DataBlock, db, startByteAdr, VarType.DInt);
        }

        return tag.Type == typeof(uint[])
            ? Read<uint[]>(tag, DataType.DataBlock, db, startByteAdr, VarType.DWord)
            : Read<uint>(tag, DataType.DataBlock, db, startByteAdr, VarType.DWord);
    }

    /// <summary>Reads a non-data-block double-word address.</summary>
    /// <param name="tag">The tag to read.</param>
    /// <param name="dataType">The data area type.</param>
    /// <param name="startByteAdr">The byte offset.</param>
    /// <returns>The double-word value or array.</returns>
    private object? ReadAreaDWordAddress(Tag tag, DataType dataType, int startByteAdr)
    {
        if (tag.Type == typeof(uint[]))
        {
            return Read<uint[]>(tag, dataType, 0, startByteAdr, VarType.DWord);
        }

        if (tag.Type == typeof(int[]))
        {
            return Read<int[]>(tag, dataType, 0, startByteAdr, VarType.DInt);
        }

        return tag.Type == typeof(int)
            ? Read<int>(tag, dataType, 0, startByteAdr, VarType.DInt)
            : Read<uint>(tag, dataType, 0, startByteAdr, VarType.DWord);
    }

    /// <summary>Reads a memory double-word address.</summary>
    /// <param name="tag">The tag to read.</param>
    /// <param name="startByteAdr">The byte offset.</param>
    /// <returns>The memory double-word value or array.</returns>
    private object? ReadMemoryDWordAddress(Tag tag, int startByteAdr) =>
        tag.Type == typeof(double[])
            ? Read<double[]>(tag, DataType.Memory, 0, startByteAdr, VarType.LReal)
            : Read<double>(tag, DataType.Memory, 0, startByteAdr, VarType.LReal);
}
