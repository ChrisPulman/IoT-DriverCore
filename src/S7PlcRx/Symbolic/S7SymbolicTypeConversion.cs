// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using System.IO;

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
namespace IoT.Driver.S7PlcRx.Symbolic;
#endif

/// <summary>Converts between PLC soft datatypes and supported CLR values.</summary>
internal static class S7SymbolicTypeConversion
{
    /// <summary>The s5 basis count protocol value.</summary>
    private const int S5BasisCount = 4;

    /// <summary>The nanosecond byte count protocol value.</summary>
    private const int NanosecondByteCount = 4;

    /// <summary>The date time fraction index protocol value.</summary>
    private const int DateTimeFractionIndex = 7;

    /// <summary>The date time millisecond index protocol value.</summary>
    private const int DateTimeMillisecondIndex = 6;

    /// <summary>The date time second index protocol value.</summary>
    private const int DateTimeSecondIndex = 5;

    /// <summary>The date time minute index protocol value.</summary>
    private const int DateTimeMinuteIndex = 4;

    /// <summary>The date time hour index protocol value.</summary>
    private const int DateTimeHourIndex = 3;

    /// <summary>The date time day index protocol value.</summary>
    private const int DateTimeDayIndex = 2;

    /// <summary>The dtl nanoseconds last index protocol value.</summary>
    private const int DtlNanosecondsLastIndex = 11;

    /// <summary>The dtl nanoseconds third index protocol value.</summary>
    private const int DtlNanosecondsThirdIndex = 10;

    /// <summary>The dtl nanoseconds second index protocol value.</summary>
    private const int DtlNanosecondsSecondIndex = 9;

    /// <summary>The dtl nanoseconds index protocol value.</summary>
    private const int DtlNanosecondsIndex = 8;

    /// <summary>The dtl second index protocol value.</summary>
    private const int DtlSecondIndex = 7;

    /// <summary>The dtl minute index protocol value.</summary>
    private const int DtlMinuteIndex = 6;

    /// <summary>The dtl hour index protocol value.</summary>
    private const int DtlHourIndex = 5;

    /// <summary>The dtl weekday index protocol value.</summary>
    private const int DtlWeekdayIndex = 4;

    /// <summary>The dtl day index protocol value.</summary>
    private const int DtlDayIndex = 3;

    /// <summary>The dtl month index protocol value.</summary>
    private const int DtlMonthIndex = 2;

    /// <summary>The date time byte count protocol value.</summary>
    private const int DateTimeByteCount = 8;

    /// <summary>The s5 basis shift protocol value.</summary>
    private const int S5BasisShift = 12;

    /// <summary>The nanoseconds per tick protocol value.</summary>
    private const int NanosecondsPerTick = 100;

    /// <summary>The PLC soft datatype identifier for Bool.</summary>
    private const uint BoolType = 1;

    /// <summary>The PLC soft datatype identifier for Byte.</summary>
    private const uint ByteType = 2;

    /// <summary>The PLC soft datatype identifier for Char.</summary>
    private const uint CharType = 3;

    /// <summary>The PLC soft datatype identifier for Word.</summary>
    private const uint WordType = 4;

    /// <summary>The PLC soft datatype identifier for Int.</summary>
    private const uint IntType = 5;

    /// <summary>The PLC soft datatype identifier for DWord.</summary>
    private const uint DWordType = 6;

    /// <summary>The PLC soft datatype identifier for DInt.</summary>
    private const uint DIntType = 7;

    /// <summary>The PLC soft datatype identifier for Real.</summary>
    private const uint RealType = 8;

    /// <summary>The PLC soft datatype identifier for Date.</summary>
    private const uint DateType = 9;

    /// <summary>The PLC soft datatype identifier for TimeOfDay.</summary>
    private const uint TimeOfDayType = 10;

    /// <summary>The PLC soft datatype identifier for Time.</summary>
    private const uint TimeType = 11;

    /// <summary>The PLC soft datatype identifier for S5Time.</summary>
    private const uint S5TimeType = 12;

    /// <summary>The PLC soft datatype identifier for Timer.</summary>
    private const uint TimerType = 13;

    /// <summary>The PLC soft datatype identifier for DateTime.</summary>
    private const uint DateTimeType = 14;

    /// <summary>The PLC soft datatype identifier for Struct.</summary>
    private const uint StructType = 17;

    /// <summary>The PLC soft datatype identifier for String.</summary>
    private const uint StringType = 19;

    /// <summary>The PLC soft datatype identifier for Counter.</summary>
    private const uint CounterType = 28;

    /// <summary>The PLC soft datatype identifier for Block.</summary>
    private const uint BlockType = 29;

    /// <summary>The PLC soft datatype identifier for BoolAlias.</summary>
    private const uint BoolAliasType = 40;

    /// <summary>The PLC soft datatype identifier for LReal.</summary>
    private const uint LRealType = 48;

    /// <summary>The PLC soft datatype identifier for ULInt.</summary>
    private const uint ULIntType = 49;

    /// <summary>The PLC soft datatype identifier for LInt.</summary>
    private const uint LIntType = 50;

    /// <summary>The PLC soft datatype identifier for LWord.</summary>
    private const uint LWordType = 51;

    /// <summary>The PLC soft datatype identifier for USInt.</summary>
    private const uint USIntType = 52;

    /// <summary>The PLC soft datatype identifier for UInt.</summary>
    private const uint UIntType = 53;

    /// <summary>The PLC soft datatype identifier for UDInt.</summary>
    private const uint UDIntType = 54;

    /// <summary>The PLC soft datatype identifier for SInt.</summary>
    private const uint SIntType = 55;

    /// <summary>The PLC soft datatype identifier for WChar.</summary>
    private const uint WCharType = 61;

    /// <summary>The PLC soft datatype identifier for WString.</summary>
    private const uint WStringType = 62;

    /// <summary>The PLC soft datatype identifier for LTime.</summary>
    private const uint LTimeType = 64;

    /// <summary>The PLC soft datatype identifier for LTimeOfDay.</summary>
    private const uint LTimeOfDayType = 65;

    /// <summary>The PLC soft datatype identifier for LDateTime.</summary>
    private const uint LDateTimeType = 66;

    /// <summary>The PLC soft datatype identifier for Dtl.</summary>
    private const uint DtlType = 67;

    /// <summary>The protocol constant for timestamp epoch year.</summary>
    private const int TimestampEpochYear = 1_970;

    /// <summary>The protocol constant for array flag.</summary>
    private const int ArrayFlag = 0x10;

    /// <summary>The protocol constant for hundred.</summary>
    private const int DecimalHundred = 100;

    /// <summary>The protocol constant for array flag mask.</summary>
    private const int ArrayFlagMask = 0x30;

    /// <summary>The protocol constant for milliseconds per day.</summary>
    private const int MillisecondsPerDay = 86_400_000;

    /// <summary>The protocol constant for nanoseconds per day.</summary>
    private const ulong NanosecondsPerDay = 86_400_000_000_000;

    /// <summary>The protocol constant for maximum narrow string length.</summary>
    private const int MaximumNarrowStringLength = 254;

    /// <summary>The protocol constant for string header length.</summary>
    private const int StringHeaderLength = 2;

    /// <summary>The protocol constant for bits per byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>The protocol constant for decimal radix.</summary>
    private const int DecimalRadix = 10;

    /// <summary>The protocol constant for nibble width.</summary>
    private const int NibbleWidth = 4;

    /// <summary>The protocol constant for nibble mask.</summary>
    private const int NibbleMask = 15;

    /// <summary>The protocol constant for maximum decimal digit.</summary>
    private const int MaximumDecimalDigit = 9;

    /// <summary>The protocol constant for date minimum year.</summary>
    private const int DateMinimumYear = 1_990;

    /// <summary>The protocol constant for date maximum year.</summary>
    private const int DateMaximumYear = 2_090;

    /// <summary>The protocol constant for days per week.</summary>
    private const int DaysPerWeek = 7;

    /// <summary>The protocol constant for date century cutoff.</summary>
    private const int DateCenturyCutoff = 90;

    /// <summary>The protocol constant for twentieth century.</summary>
    private const int TwentiethCentury = 1_900;

    /// <summary>The protocol constant for twenty first century.</summary>
    private const int TwentyFirstCentury = 2_000;

    /// <summary>The protocol constant for maximum s5 digits.</summary>
    private const int MaximumS5Digits = 999;

    /// <summary>The protocol constant for dtl byte count.</summary>
    private const int DtlByteCount = 12;

    /// <summary>The protocol constant for s5 reserved mask.</summary>
    private const int S5ReservedMask = 0xc000;

    /// <summary>The protocol constant for milliseconds per second.</summary>
    private const int MillisecondsPerSecond = 1_000;

    /// <summary>The protocol constant for maximum s5 time unit.</summary>
    private const int MaximumS5TimeUnit = 10_000;

    /// <summary>The protocol constant for dtl type id.</summary>
    private const uint DtlTypeId = 0x02000043;

    /// <summary>The protocol constant for high word shift.</summary>
    private const int HighWordShift = 24;

    /// <summary>The protocol constant for word bits.</summary>
    private const int WordBits = 16;

    /// <summary>Maps supported PLC soft datatypes to their wire datatypes.</summary>
    private static readonly Dictionary<uint, S7SymbolicDataType> WireTypes = new()
    {
        [BoolType] = S7SymbolicDataType.Bool,
        [BoolAliasType] = S7SymbolicDataType.Bool,
        [ByteType] = S7SymbolicDataType.Byte,
        [CharType] = S7SymbolicDataType.USInt,
        [DateTimeType] = S7SymbolicDataType.USInt,
        [StringType] = S7SymbolicDataType.USInt,
        [USIntType] = S7SymbolicDataType.USInt,
        [WordType] = S7SymbolicDataType.Word,
        [S5TimeType] = S7SymbolicDataType.Word,
        [TimerType] = S7SymbolicDataType.Word,
        [CounterType] = S7SymbolicDataType.Word,
        [BlockType] = S7SymbolicDataType.Word,
        [IntType] = S7SymbolicDataType.Int,
        [DWordType] = S7SymbolicDataType.DWord,
        [DIntType] = S7SymbolicDataType.DInt,
        [TimeType] = S7SymbolicDataType.DInt,
        [RealType] = S7SymbolicDataType.Real,
        [DateType] = S7SymbolicDataType.UInt,
        [UIntType] = S7SymbolicDataType.UInt,
        [WCharType] = S7SymbolicDataType.UInt,
        [WStringType] = S7SymbolicDataType.UInt,
        [TimeOfDayType] = S7SymbolicDataType.UDInt,
        [UDIntType] = S7SymbolicDataType.UDInt,
        [LRealType] = S7SymbolicDataType.LReal,
        [ULIntType] = S7SymbolicDataType.ULInt,
        [LTimeOfDayType] = S7SymbolicDataType.ULInt,
        [LIntType] = S7SymbolicDataType.LInt,
        [LWordType] = S7SymbolicDataType.LWord,
        [SIntType] = S7SymbolicDataType.SInt,
        [LTimeType] = S7SymbolicDataType.Timespan,
        [LDateTimeType] = S7SymbolicDataType.Timestamp,
        [StructType] = S7SymbolicDataType.Struct,
        [DtlType] = S7SymbolicDataType.Struct,
    };

    /// <summary>The calendar epoch used by PLC DATE values.</summary>
    private static readonly DateTime DateEpoch = new(DateMinimumYear, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    /// <summary>The UTC epoch used by PLC timestamps.</summary>
    private static readonly DateTime TimestampEpoch = new(TimestampEpochYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Encodes a supported CLR scalar or array as a PLC value.</summary>
    /// <param name="value">The value to convert or validate.</param>
    /// <param name="softtype">The PLC soft datatype identifier.</param>
    /// <param name="maxStringLength">The configured PLC string capacity.</param>
    /// <returns>The converted or validated value.</returns>
    internal static S7SymbolicValue FromClr(object value, uint softtype, int maxStringLength)
    {
#if NETFRAMEWORK
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }
#else
        ArgumentNullException.ThrowIfNull(value);
#endif
        if (value is S7SymbolicValue encoded)
        {
            return encoded;
        }

        if (value is Array array)
        {
            if (array.Rank != 1 || array.GetLowerBound(0) != 0)
            {
                throw new ArgumentException("Only zero-based, one-dimensional arrays are supported.", nameof(value));
            }

            S7SymbolicValue.CheckCount(array.Length);
            var entries = new object?[array.Length];
            var type = WireType(softtype);
            for (var i = 0; i < array.Length; i++)
            {
                var item = FromClr(array.GetValue(i) ?? throw new ArgumentException("Array elements cannot be null.", nameof(value)), softtype, maxStringLength);
                if (item.Flags != 0)
                {
                    throw new NotSupportedException("Arrays of encoded strings or date-and-time values require an explicit wire value.");
                }

                type = item.DataType;
                entries[i] = item.Value;
            }

            return new(type, entries, ArrayFlag);
        }

        return EncodeScalar(value, softtype, maxStringLength);
    }

    /// <summary>Decodes a PLC value into the requested CLR type.</summary>
    /// <typeparam name="T">The requested CLR value type.</typeparam>
    /// <param name="value">The value to convert or validate.</param>
    /// <param name="softtype">The PLC soft datatype identifier.</param>
    /// <returns>The converted or validated value.</returns>
    internal static T ToClr<T>(S7SymbolicValue value, uint softtype)
    {
#if NETFRAMEWORK
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }
#else
        ArgumentNullException.ThrowIfNull(value);
#endif
        return typeof(T) == typeof(S7SymbolicValue) ? (T)(object)value : (T)Decode(value, softtype, typeof(T));
    }

    /// <summary>Encodes a CLR scalar using its declared PLC soft datatype.</summary>
    /// <param name="value">The scalar to encode.</param>
    /// <param name="softtype">The PLC soft datatype identifier.</param>
    /// <param name="maxStringLength">The configured PLC string capacity.</param>
    /// <returns>The encoded PLC scalar.</returns>
    private static S7SymbolicValue EncodeScalar(object value, uint softtype, int maxStringLength)
    {
        var wireType = WireType(softtype);
        object payload = softtype switch
        {
            CharType => checked((byte)Require<char>(value)),
            WCharType => (ushort)Require<char>(value),
            StringType => EncodeString(Require<string>(value), maxStringLength, false),
            WStringType => EncodeString(Require<string>(value), maxStringLength, true),
            DtlType when value is S7SymbolicDtl dtl => EncodeDtl(dtl),
            _ => EncodeTemporal(value, softtype, wireType)
        };
        return new(wireType, payload, softtype is DateTimeType or StringType or WStringType ? (byte)ArrayFlag : (byte)0);
    }

    /// <summary>Encodes calendar and duration values for temporal PLC datatypes.</summary>
    /// <param name="value">The temporal value or wire scalar.</param>
    /// <param name="softtype">The PLC soft datatype identifier.</param>
    /// <param name="wireType">The declared wire datatype.</param>
    /// <returns>The temporal payload or validated wire scalar.</returns>
    private static object EncodeTemporal(object value, uint softtype, S7SymbolicDataType wireType) => softtype switch
    {
        DateType => DateDays(value),
        TimeOfDayType => TimeMilliseconds(value, true),
        TimeType => SignedMilliseconds(value),
        S5TimeType => EncodeS5(value),
        DateTimeType => EncodeDateTime(Require<DateTime>(value)),
        LTimeType => SignedNanoseconds(value),
        LTimeOfDayType => TimeNanoseconds(value),
        LDateTimeType => TimestampNanoseconds(value),
        _ => RequireWireScalar(value, wireType)
    };

    /// <summary>Converts a duration or signed scalar to whole milliseconds.</summary>
    /// <param name="value">The duration or scalar to convert.</param>
    /// <returns>The signed number of milliseconds.</returns>
    private static int SignedMilliseconds(object value) =>
        value is TimeSpan time ? checked((int)ExactUnits(time.Ticks, TimeSpan.TicksPerMillisecond)) : Require<int>(value);

    /// <summary>Converts a duration or signed scalar to nanoseconds.</summary>
    /// <param name="value">The duration or scalar to convert.</param>
    /// <returns>The signed number of nanoseconds.</returns>
    private static long SignedNanoseconds(object value) =>
        value is TimeSpan duration ? checked(duration.Ticks * NanosecondsPerTick) : Require<long>(value);

    /// <summary>Converts a UTC calendar value or wire scalar to epoch nanoseconds.</summary>
    /// <param name="value">The calendar value or wire scalar.</param>
    /// <returns>The unsigned number of nanoseconds since the timestamp epoch.</returns>
    private static ulong TimestampNanoseconds(object value) =>
        value is DateTime date ? checked((ulong)(Utc(date).Ticks - TimestampEpoch.Ticks) * NanosecondsPerTick) : Require<ulong>(value);

    /// <summary>Decodes scalar and array values while checking the declared datatype.</summary>
    /// <param name="value">The value to convert or validate.</param>
    /// <param name="softtype">The PLC soft datatype identifier.</param>
    /// <param name="target">The requested CLR type.</param>
    /// <returns>The converted or validated value.</returns>
    private static object Decode(S7SymbolicValue value, uint softtype, Type target)
    {
        if (value.DataType != WireType(softtype))
        {
            throw new InvalidDataException("PLC value does not match the declared soft datatype.");
        }

        var scalarArray = IsScalarArray(softtype);
        if (IsNaturalArray(value, target, scalarArray))
        {
            return DecodeNaturalArray(value.DataType, Require<IList>(value.Value), softtype);
        }

        if (target.IsArray)
        {
            return DecodeTypedArray(value, softtype, target, scalarArray);
        }

        if (!scalarArray && value.Flags != 0)
        {
            throw new InvalidCastException("An array cannot be read as a scalar.");
        }

        return DecodeScalar(value, softtype, target);
    }

    /// <summary>Checks whether a PLC datatype stores its scalar payload as a wire array.</summary>
    /// <param name="softtype">The PLC soft datatype identifier.</param>
    /// <returns>True for scalar datatypes with array payloads.</returns>
    private static bool IsScalarArray(uint softtype) => softtype is DateTimeType or StringType or WStringType;

    /// <summary>Checks whether an array should use natural CLR element representations.</summary>
    /// <param name="value">The encoded PLC value.</param>
    /// <param name="target">The requested CLR type.</param>
    /// <param name="scalarArray">Whether the wire array represents a scalar datatype.</param>
    /// <returns>True when an object request represents a PLC array.</returns>
    private static bool IsNaturalArray(S7SymbolicValue value, Type target, bool scalarArray) =>
        target == typeof(object) && (value.Flags & ArrayFlagMask) != 0 && !scalarArray;

    /// <summary>Decodes array elements into their natural CLR representations.</summary>
    /// <param name="dataType">The wire datatype of each element.</param>
    /// <param name="items">The encoded array elements.</param>
    /// <param name="softtype">The PLC soft datatype identifier.</param>
    /// <returns>The decoded array.</returns>
    private static object[] DecodeNaturalArray(S7SymbolicDataType dataType, IList items, uint softtype)
    {
        var result = new object[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            result[i] = Decode(new(dataType, items[i]), softtype, typeof(object));
        }

        return result;
    }

    /// <summary>Validates and decodes an array into its requested CLR element type.</summary>
    /// <param name="value">The encoded PLC array.</param>
    /// <param name="softtype">The PLC soft datatype identifier.</param>
    /// <param name="target">The requested CLR array type.</param>
    /// <param name="scalarArray">Whether the wire array represents a scalar datatype.</param>
    /// <returns>The decoded array.</returns>
    private static Array DecodeTypedArray(S7SymbolicValue value, uint softtype, Type target, bool scalarArray)
    {
        if (target.GetArrayRank() != 1 || (value.Flags & ArrayFlagMask) == 0 || value.Value is not IList items || scalarArray)
        {
            throw new InvalidCastException("The PLC value is not a supported CLR array.");
        }

        var elementType = target.GetElementType()!;
        var result = Array.CreateInstance(elementType, items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            result.SetValue(Decode(new(value.DataType, items[i]), softtype, elementType), i);
        }

        return result;
    }

    /// <summary>Decodes a scalar using its declared PLC soft datatype.</summary>
    /// <param name="value">The value to convert or validate.</param>
    /// <param name="softtype">The PLC soft datatype identifier.</param>
    /// <param name="target">The requested CLR type.</param>
    /// <returns>The converted or validated value.</returns>
    private static object DecodeScalar(S7SymbolicValue value, uint softtype, Type target)
    {
        var raw = value.Value;
        object converted = softtype switch
        {
            CharType => (char)Require<byte>(raw),
            WCharType => (char)Require<ushort>(raw),
            DateType => DateEpoch.AddDays(Require<ushort>(raw)),
            DateTimeType => DecodeDateTime(Bytes(value)),
            StringType => DecodeString(value, false),
            WStringType => DecodeString(value, true),
            _ => DecodeTemporal(raw, softtype, target)
        };
        if (!target.IsInstanceOfType(converted))
        {
            throw new InvalidCastException("Requested CLR type does not match the PLC datatype.");
        }

        return converted;
    }

    /// <summary>Decodes temporal PLC values when the requested CLR type supports them.</summary>
    /// <param name="raw">The temporal wire payload.</param>
    /// <param name="softtype">The PLC soft datatype identifier.</param>
    /// <param name="target">The requested CLR type.</param>
    /// <returns>The temporal value or unchanged wire scalar.</returns>
    private static object DecodeTemporal(object? raw, uint softtype, Type target) => softtype switch
    {
        TimeOfDayType when IsTarget(target, typeof(TimeSpan)) => TimeSpan.FromTicks(checked((long)Require<uint>(raw) * TimeSpan.TicksPerMillisecond)),
        TimeType when IsTarget(target, typeof(TimeSpan)) => TimeSpan.FromTicks(checked((long)Require<int>(raw) * TimeSpan.TicksPerMillisecond)),
        S5TimeType when IsTarget(target, typeof(TimeSpan)) => DecodeS5(Require<ushort>(raw)),
        LTimeType when IsTarget(target, typeof(TimeSpan)) => TimeSpan.FromTicks(ExactUnits(Require<long>(raw), NanosecondsPerTick)),
        LTimeOfDayType when IsTarget(target, typeof(TimeSpan)) => TimeSpan.FromTicks(checked((long)(Require<ulong>(raw) / NanosecondsPerTick))),
        LDateTimeType when IsTarget(target, typeof(DateTime)) => TimestampEpoch.AddTicks(checked((long)(Require<ulong>(raw) / NanosecondsPerTick))),
        DtlType when IsTarget(target, typeof(S7SymbolicDtl)) => DecodeDtl(Require<S7SymbolicPackedStruct>(raw)),
        _ => raw ?? throw new InvalidDataException("PLC scalar is null.")
    };

    /// <summary>Checks whether a conversion accepts the requested CLR type.</summary>
    /// <param name="target">The requested CLR type.</param>
    /// <param name="natural">The natural CLR type for the PLC datatype.</param>
    /// <returns>True for the natural type or an object request.</returns>
    private static bool IsTarget(Type target, Type natural) => target == natural || target == typeof(object);

    /// <summary>Gets the wire datatype for a supported PLC soft datatype.</summary>
    /// <param name="type">The PLC datatype to validate.</param>
    /// <returns>The converted or validated value.</returns>
    private static S7SymbolicDataType WireType(uint type) =>
        WireTypes.TryGetValue(type, out var wireType) ? wireType : throw new NotSupportedException($"PLC soft datatype {type} requires an explicit wire value.");

    /// <summary>Checks that a scalar matches the wire datatype.</summary>
    /// <param name="value">The value to convert or validate.</param>
    /// <param name="type">The PLC datatype to validate.</param>
    /// <returns>The converted or validated value.</returns>
    private static object RequireWireScalar(object value, S7SymbolicDataType type) => type switch
    {
        S7SymbolicDataType.Bool => Require<bool>(value),
        S7SymbolicDataType.Byte or S7SymbolicDataType.USInt => Require<byte>(value),
        S7SymbolicDataType.Word or S7SymbolicDataType.UInt => Require<ushort>(value),
        S7SymbolicDataType.DWord or S7SymbolicDataType.UDInt => Require<uint>(value),
        S7SymbolicDataType.LWord or S7SymbolicDataType.ULInt => Require<ulong>(value),
        _ => RequireSignedScalar(value, type)
    };

    /// <summary>Validates signed numeric and packed structure wire scalars.</summary>
    /// <param name="value">The scalar to validate.</param>
    /// <param name="type">The declared wire datatype.</param>
    /// <returns>The validated scalar.</returns>
    private static object RequireSignedScalar(object value, S7SymbolicDataType type) => type switch
    {
        S7SymbolicDataType.SInt => Require<sbyte>(value),
        S7SymbolicDataType.Int => Require<short>(value),
        S7SymbolicDataType.DInt => Require<int>(value),
        S7SymbolicDataType.LInt => Require<long>(value),
        S7SymbolicDataType.Real => Require<float>(value),
        S7SymbolicDataType.LReal => Require<double>(value),
        S7SymbolicDataType.Struct when value is S7SymbolicPackedStruct or S7SymbolicStruct => value,
        _ => throw new ArgumentException("CLR value does not match the PLC datatype.", nameof(value))
    };

    /// <summary>Requires a value with the requested CLR type.</summary>
    /// <typeparam name="T">The requested CLR value type.</typeparam>
    /// <param name="value">The value to convert or validate.</param>
    /// <returns>The converted or validated value.</returns>
    private static T Require<T>(object? value) => value is T typed ? typed : throw new InvalidCastException($"Expected {typeof(T).Name}.");

    /// <summary>Converts a calendar date to days since the PLC DATE epoch.</summary>
    /// <param name="value">The value to convert or validate.</param>
    /// <returns>The converted or validated value.</returns>
    private static ushort DateDays(object value)
    {
        if (value is not DateTime date)
        {
            return Require<ushort>(value);
        }

#if NETFRAMEWORK
        if (date.Ticks % TimeSpan.TicksPerDay != 0)
#else
        if (TimeOnly.FromDateTime(date) != TimeOnly.MinValue)
#endif
        {
            throw new ArgumentException("DATE cannot represent a time of day.", nameof(value));
        }

        return checked((ushort)((date.Ticks - DateEpoch.Ticks) / TimeSpan.TicksPerDay));
    }

    /// <summary>Converts a duration or unsigned scalar to milliseconds.</summary>
    /// <param name="value">The value to convert or validate.</param>
    /// <param name="timeOfDay">True to require a time within one day.</param>
    /// <returns>The converted or validated value.</returns>
    private static uint TimeMilliseconds(object value, bool timeOfDay)
    {
        var milliseconds = value is TimeSpan time ? checked((uint)ExactUnits(time.Ticks, TimeSpan.TicksPerMillisecond)) : Require<uint>(value);
        if (timeOfDay && milliseconds >= MillisecondsPerDay)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return milliseconds;
    }

    /// <summary>Converts a time of day or unsigned scalar to nanoseconds.</summary>
    /// <param name="value">The value to convert or validate.</param>
    /// <returns>The converted or validated value.</returns>
    private static ulong TimeNanoseconds(object value)
    {
        var nanoseconds = value is TimeSpan time ? checked((ulong)time.Ticks * NanosecondsPerTick) : Require<ulong>(value);
        if (nanoseconds >= NanosecondsPerDay)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return nanoseconds;
    }

    /// <summary>Converts time units without losing precision.</summary>
    /// <param name="value">The value to convert or validate.</param>
    /// <param name="divisor">The number of source units per destination unit.</param>
    /// <returns>The converted or validated value.</returns>
    private static long ExactUnits(long value, long divisor) => value % divisor == 0 ? value / divisor : throw new ArgumentException("The PLC or CLR datatype cannot represent this time precision.");

    /// <summary>Interprets a calendar value as UTC, converting local values.</summary>
    /// <param name="value">The value to convert or validate.</param>
    /// <returns>The converted or validated value.</returns>
    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    /// <summary>Encodes a string with a PLC capacity and length header.</summary>
    /// <param name="value">The value to convert or validate.</param>
    /// <param name="maximum">The configured PLC string capacity.</param>
    /// <param name="wide">True to use UTF-16 words instead of bytes.</param>
    /// <returns>The converted or validated value.</returns>
    private static object EncodeString(string value, int maximum, bool wide)
    {
        if (maximum < 0 || maximum > (wide ? ushort.MaxValue : MaximumNarrowStringLength) || value.Length > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum));
        }

        if (wide)
        {
            var words = new ushort[maximum + StringHeaderLength];
            words[0] = (ushort)maximum;
            words[1] = (ushort)value.Length;
            for (var i = 0; i < value.Length; i++)
            {
                words[i + StringHeaderLength] = value[i];
            }

            return words;
        }

        var bytes = new byte[maximum + StringHeaderLength];
        bytes[0] = (byte)maximum;
        bytes[1] = (byte)value.Length;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] > byte.MaxValue)
            {
                throw new ArgumentException("STRING cannot represent a character outside Latin-1.", nameof(value));
            }

            bytes[i + StringHeaderLength] = (byte)value[i];
        }

        return bytes;
    }

    /// <summary>Decodes a PLC string after validating its header.</summary>
    /// <param name="value">The value to convert or validate.</param>
    /// <param name="wide">True to use UTF-16 words instead of bytes.</param>
    /// <returns>The converted or validated value.</returns>
    private static string DecodeString(S7SymbolicValue value, bool wide)
    {
        var items = Require<IList>(value.Value);
        if ((value.Flags & ArrayFlagMask) == 0 || items.Count < StringHeaderLength)
        {
            throw new InvalidDataException("Invalid PLC string header.");
        }

        var maximum = wide ? Require<ushort>(items[0]) : Require<byte>(items[0]);
        var length = wide ? Require<ushort>(items[1]) : Require<byte>(items[1]);
        if (length > maximum || length > items.Count - StringHeaderLength)
        {
            throw new InvalidDataException("Invalid PLC string length.");
        }

        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = wide ? (char)Require<ushort>(items[i + StringHeaderLength]) : (char)Require<byte>(items[i + StringHeaderLength]);
        }

        return new(chars);
    }

    /// <summary>Reads the eight bytes of a PLC DATE_AND_TIME value.</summary>
    /// <param name="value">The value to convert or validate.</param>
    /// <returns>The converted or validated value.</returns>
    private static byte[] Bytes(S7SymbolicValue value)
    {
        var items = Require<IList>(value.Value);
        if ((value.Flags & ArrayFlagMask) == 0 || items.Count != DateTimeByteCount)
        {
            throw new InvalidDataException("DATE_AND_TIME requires eight bytes.");
        }

        var bytes = new byte[DateTimeByteCount];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Require<byte>(items[i]);
        }

        return bytes;
    }

    /// <summary>Encodes a decimal value as binary coded decimal.</summary>
    /// <param name="value">The value to convert or validate.</param>
    /// <returns>The converted or validated value.</returns>
    private static byte Bcd(int value) => (byte)(((value / DecimalRadix) << NibbleWidth) | (value % DecimalRadix));

    /// <summary>Decodes binary coded decimal after validating both digits.</summary>
    /// <param name="value">The value to convert or validate.</param>
    /// <returns>The converted or validated value.</returns>
    private static int Unbcd(byte value) =>
        (value & NibbleMask) <= MaximumDecimalDigit && (value >> NibbleWidth) <= MaximumDecimalDigit
            ? ((value >> NibbleWidth) * DecimalRadix) + (value & NibbleMask)
            : throw new InvalidDataException("Invalid BCD digit.");

    /// <summary>Encodes a calendar time at millisecond precision.</summary>
    /// <param name="value">The value to convert or validate.</param>
    /// <returns>The converted or validated value.</returns>
    private static byte[] EncodeDateTime(DateTime value)
    {
        if (value.Year < DateMinimumYear || value.Year >= DateMaximumYear || value.Ticks % TimeSpan.TicksPerMillisecond != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return
        [
            Bcd(value.Year % DecimalHundred), Bcd(value.Month), Bcd(value.Day), Bcd(value.Hour),
            Bcd(value.Minute), Bcd(value.Second), Bcd(value.Millisecond / DecimalRadix),
            (byte)(((value.Millisecond % DecimalRadix) << NibbleWidth) | ((int)value.DayOfWeek + 1))
        ];
    }

    /// <summary>Decodes a PLC DATE_AND_TIME calendar value.</summary>
    /// <param name="bytes">The encoded PLC calendar bytes.</param>
    /// <returns>The converted or validated value.</returns>
    private static DateTime DecodeDateTime(byte[] bytes)
    {
        var year = Unbcd(bytes[0]);
        if ((bytes[DateTimeFractionIndex] >> NibbleWidth) > MaximumDecimalDigit || (bytes[DateTimeFractionIndex] & NibbleMask) > DaysPerWeek)
        {
            throw new InvalidDataException("Invalid DATE_AND_TIME digits.");
        }

        return new(
            year >= DateCenturyCutoff ? year + TwentiethCentury : year + TwentyFirstCentury,
            Unbcd(bytes[1]),
            Unbcd(bytes[DateTimeDayIndex]),
            Unbcd(bytes[DateTimeHourIndex]),
            Unbcd(bytes[DateTimeMinuteIndex]),
            Unbcd(bytes[DateTimeSecondIndex]),
            (Unbcd(bytes[DateTimeMillisecondIndex]) * DecimalRadix) + (bytes[DateTimeFractionIndex] >> NibbleWidth),
            DateTimeKind.Unspecified);
    }

    /// <summary>Encodes a duration using a supported S5TIME basis.</summary>
    /// <param name="value">The value to convert or validate.</param>
    /// <returns>The converted or validated value.</returns>
    private static ushort EncodeS5(object value)
    {
        if (value is not TimeSpan time)
        {
            return Require<ushort>(value);
        }

        var milliseconds = ExactUnits(time.Ticks, TimeSpan.TicksPerMillisecond);
        long unit = DecimalRadix;
        for (var basis = 0; basis < S5BasisCount; basis++, unit *= DecimalRadix)
        {
            if (milliseconds >= 0 && milliseconds % unit == 0 && milliseconds / unit <= MaximumS5Digits)
            {
                var digits = (int)(milliseconds / unit);
                return (ushort)((basis << S5BasisShift) | ((digits / DecimalHundred) << BitsPerByte) | Bcd(digits % DecimalHundred));
            }
        }

        throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>Decodes an S5TIME duration after validating its digits.</summary>
    /// <param name="value">The value to convert or validate.</param>
    /// <returns>The converted or validated value.</returns>
    private static TimeSpan DecodeS5(ushort value)
    {
        if ((value & S5ReservedMask) != 0 || ((value >> BitsPerByte) & NibbleMask) > MaximumDecimalDigit)
        {
            throw new InvalidDataException("Invalid S5TIME value.");
        }

        var digits = (((value >> BitsPerByte) & NibbleMask) * DecimalHundred) + Unbcd((byte)value);
        var unit = (value >> S5BasisShift) switch
        {
            0 => DecimalRadix,
            1 => DecimalHundred,
            StringHeaderLength => MillisecondsPerSecond,
            _ => MaximumS5TimeUnit
        };
        return TimeSpan.FromTicks((long)digits * unit * TimeSpan.TicksPerMillisecond);
    }

    /// <summary>Encodes a DTL calendar value and its packed structure metadata.</summary>
    /// <param name="value">The value to convert or validate.</param>
    /// <returns>The converted or validated value.</returns>
    private static S7SymbolicPackedStruct EncodeDtl(S7SymbolicDtl value)
    {
        var date = value.DateTime;
        var bytes = new byte[DtlByteCount];
        bytes[0] = (byte)(date.Year >> BitsPerByte);
        bytes[1] = (byte)date.Year;
        bytes[DtlMonthIndex] = (byte)date.Month;
        bytes[DtlDayIndex] = (byte)date.Day;
        bytes[DtlWeekdayIndex] = (byte)((int)date.DayOfWeek + 1);
        bytes[DtlHourIndex] = (byte)date.Hour;
        bytes[DtlMinuteIndex] = (byte)date.Minute;
        bytes[DtlSecondIndex] = (byte)date.Second;
        for (var i = 0; i < NanosecondByteCount; i++)
        {
            bytes[i + DtlNanosecondsIndex] = (byte)(value.Nanoseconds >> ((NanosecondByteCount - 1 - i) * BitsPerByte));
        }

        return new(DtlTypeId, value.InterfaceTimestamp, value.TransportFlags, bytes);
    }

    /// <summary>Decodes a packed DTL calendar value and its metadata.</summary>
    /// <param name="value">The value to convert or validate.</param>
    /// <returns>The converted or validated value.</returns>
    private static S7SymbolicDtl DecodeDtl(S7SymbolicPackedStruct value)
    {
        var bytes = value.Data;
        if (value.TypeId != DtlTypeId || bytes.Length != DtlByteCount || bytes[DtlWeekdayIndex] > DaysPerWeek)
        {
            throw new InvalidDataException("Invalid packed DTL value.");
        }

        var date = new DateTime((bytes[0] << BitsPerByte) | bytes[1], bytes[DtlMonthIndex], bytes[DtlDayIndex], bytes[DtlHourIndex], bytes[DtlMinuteIndex], bytes[DtlSecondIndex], DateTimeKind.Utc);
        var nanoseconds = ((uint)bytes[DtlNanosecondsIndex] << HighWordShift) |
            ((uint)bytes[DtlNanosecondsSecondIndex] << WordBits) |
            ((uint)bytes[DtlNanosecondsThirdIndex] << BitsPerByte) |
            bytes[DtlNanosecondsLastIndex];
        return new(new DateTimeOffset(date), nanoseconds, value.InterfaceTimestamp, value.TransportFlags);
    }
}
