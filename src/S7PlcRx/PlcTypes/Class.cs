// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
#if REACTIVE_SHIM
using IoT.DriverCore.S7PlcRx.Reactive.Enums;
#else
using IoT.DriverCore.S7PlcRx.Enums;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.S7PlcRx.Reactive.PlcTypes;
#else
namespace IoT.DriverCore.S7PlcRx.PlcTypes;
#endif

/// <summary>
/// Provides static methods for serializing and deserializing class and struct instances to and from byte arrays, as
/// well as calculating the size of a class in bytes for serialization purposes.
/// </summary>
/// <remarks>This class is intended for scenarios where objects need to be converted to a byte representation,
/// such as communication with PLCs or other systems requiring structured binary formats. All methods operate statically
/// and require the caller to supply instances and byte arrays as needed. Properties within serialized classes must be
/// accessible and, for string fields, decorated with the appropriate S7StringAttribute. Methods may throw exceptions if
/// required attributes are missing or if input values are invalid. Thread safety is not guaranteed; callers should
/// ensure appropriate synchronization if accessing shared objects.</remarks>
public static class Class
{
    /// <summary>The byte boundary required for S7 class fields.</summary>
    private const int ByteAlignment = 2;

    /// <summary>The fraction of a byte occupied by one Boolean value.</summary>
    private const double BitSizeInBytes = 0.125;

    /// <summary>Message used when a string property lacks serialization metadata.</summary>
    private const string MissingS7StringAttributeMessage = "Please add S7StringAttribute to the string field";

    /// <summary>Calculates the aligned serialized size of an object's accessible properties.</summary>
    /// <param name="instance">The object whose accessible properties are measured.</param>
    /// <returns>The aligned serialized size in bytes.</returns>
    public static double GetClassSize(object instance) => GetClassSize(instance, 0.0, false);

    /// <summary>Calculates the aligned serialized size from a specified byte offset.</summary>
    /// <param name="instance">The object whose accessible properties are measured.</param>
    /// <param name="numBytes">The initial byte offset.</param>
    /// <returns>The aligned serialized size in bytes.</returns>
    public static double GetClassSize(object instance, double numBytes) => GetClassSize(instance, numBytes, false);

    /// <summary>Calculates the serialized size from a specified byte offset.</summary>
    /// <param name="instance">The object whose accessible properties are measured.</param>
    /// <param name="numBytes">The initial byte offset.</param>
    /// <param name="isInnerProperty">Whether the object is nested and should not receive final alignment.</param>
    /// <returns>The serialized size in bytes.</returns>
    public static double GetClassSize(object instance, double numBytes, bool isInnerProperty)
    {
        if (instance is null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        foreach (var property in GetAccessableProperties(instance.GetType()))
        {
            if (property.PropertyType.IsArray)
            {
                var elementType = property.PropertyType.GetElementType() ??
                    throw new InvalidOperationException("Array properties must declare an element type.");
                var array = (Array?)property.GetValue(instance, null) ??
                    throw new ArgumentException(
                        $"Property {property.Name} on {instance} must have a non-null value to get it's size.",
                        nameof(instance));

                if (array.Length == 0)
                {
                    throw new InvalidOperationException(
                        "Cannot determine size of class because an array is defined with no fixed size greater than " +
                        "zero.");
                }

                IncrementToEven(ref numBytes);
                for (var i = 0; i < array.Length; i++)
                {
                    numBytes = GetIncreasedNumberOfBytes(numBytes, elementType, property);
                }
            }
            else
            {
                numBytes = GetIncreasedNumberOfBytes(numBytes, property.PropertyType, property);
            }
        }

        if (!isInnerProperty)
        {
            // Enlarge numBytes to the next even number because S7-Structs in a DB use an even byte count.
            numBytes = Math.Ceiling(numBytes);
            if ((numBytes / ByteAlignment) > Math.Floor(numBytes / ByteAlignment))
            {
                numBytes++;
            }
        }

        return numBytes;
    }

    /// <summary>Deserializes accessible properties from the beginning of a byte array.</summary>
    /// <param name="sourceClass">The object whose properties receive deserialized values.</param>
    /// <param name="bytes">The source byte array.</param>
    /// <returns>The number of bytes consumed.</returns>
    public static double FromBytes(object sourceClass, byte[] bytes) => FromBytes(sourceClass, bytes, 0, false);

    /// <summary>Deserializes accessible properties from a specified byte offset.</summary>
    /// <param name="sourceClass">The object whose properties receive deserialized values.</param>
    /// <param name="bytes">The source byte array.</param>
    /// <param name="numBytes">The initial byte offset.</param>
    /// <returns>The number of bytes consumed.</returns>
    public static double FromBytes(object sourceClass, byte[] bytes, double numBytes) =>
        FromBytes(sourceClass, bytes, numBytes, false);

    /// <summary>Deserializes accessible properties from a specified byte offset.</summary>
    /// <param name="sourceClass">The object whose properties receive deserialized values.</param>
    /// <param name="bytes">The source byte array.</param>
    /// <param name="numBytes">The initial byte offset.</param>
    /// <param name="isInnerClass">Whether the object is nested within another serialized object.</param>
    /// <returns>The number of bytes consumed.</returns>
    public static double FromBytes(object sourceClass, byte[] bytes, double numBytes, bool isInnerClass)
    {
        if (bytes is null)
        {
            return numBytes;
        }

        if (sourceClass is null)
        {
            throw new ArgumentNullException(nameof(sourceClass));
        }

        foreach (var property in GetAccessableProperties(sourceClass.GetType()))
        {
            if (property.PropertyType.IsArray)
            {
                var array = (Array?)property.GetValue(sourceClass, null) ??
                    throw new ArgumentException(
                        $"Property {property.Name} on sourceClass must be an array instance.",
                        nameof(sourceClass));

                IncrementToEven(ref numBytes);
                var elementType = property.PropertyType.GetElementType() ??
                    throw new InvalidOperationException("Array properties must declare an element type.");
                for (var i = 0; i < array.Length && numBytes < bytes.Length; i++)
                {
                    array.SetValue(
                        GetPropertyValue(elementType, property, bytes, ref numBytes),
                        i);
                }
            }
            else
            {
                property.SetValue(
                    sourceClass,
                    GetPropertyValue(property.PropertyType, property, bytes, ref numBytes),
                    null);
            }
        }

        return numBytes;
    }

    /// <summary>Serializes accessible properties to the beginning of a byte array.</summary>
    /// <param name="sourceClass">The object whose properties are serialized.</param>
    /// <param name="bytes">The destination byte array.</param>
    /// <returns>The number of bytes written.</returns>
    public static double ToBytes(object sourceClass, byte[] bytes) => ToBytes(sourceClass, bytes, 0.0);

    /// <summary>Serializes accessible properties to a specified byte offset.</summary>
    /// <param name="sourceClass">The object whose properties are serialized.</param>
    /// <param name="bytes">The destination byte array.</param>
    /// <param name="numBytes">The initial byte offset.</param>
    /// <returns>The number of bytes written.</returns>
    public static double ToBytes(object sourceClass, byte[] bytes, double numBytes)
    {
        if (sourceClass is null)
        {
            throw new ArgumentNullException(nameof(sourceClass));
        }

        if (bytes is null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }

        foreach (var property in GetAccessableProperties(sourceClass.GetType()))
        {
            var value = property.GetValue(sourceClass, null) ??
                throw new ArgumentException(
                    $"Property {property.Name} on sourceClass can't be null.",
                    nameof(sourceClass));

            if (property.PropertyType.IsArray)
            {
                var array = (Array)value;
                IncrementToEven(ref numBytes);
                for (var i = 0; i < array.Length && numBytes < bytes.Length; i++)
                {
                    var arrayValue = array.GetValue(i) ??
                        throw new ArgumentException(
                            $"Property {property.Name} on sourceClass cannot contain null values.",
                            nameof(sourceClass));
                    numBytes = SetBytesFromProperty(arrayValue, property, bytes, numBytes);
                }
            }
            else
            {
                numBytes = SetBytesFromProperty(value, property, bytes, numBytes);
            }
        }

        return numBytes;
    }

    /// <summary>Gets public instance properties with accessible setters.</summary>
    /// <remarks>Only properties with public set methods are included. Properties with non-public or
    /// inaccessible setters are excluded.</remarks>
    /// <param name="classType">The type to inspect for public instance properties with accessible setters.
    /// Cannot be null.</param>
    /// <returns>An enumerable collection of PropertyInfo objects representing the public instance properties
    /// of the specified
    /// type that can be set. The collection will be empty if no such properties exist.</returns>
    private static IEnumerable<PropertyInfo> GetAccessableProperties(Type classType)
    {
        foreach (var property in classType.GetProperties(
            BindingFlags.SetProperty |
            BindingFlags.Public |
            BindingFlags.Instance))
        {
            if (property.GetSetMethod() is not null)
            {
                yield return property;
            }
        }
    }

    /// <summary>
    /// Calculates the increased number of bytes required to represent a value of the specified type, optionally
    /// considering custom property attributes.
    /// </summary>
    /// <remarks>This method supports primitive types, strings with S7StringAttribute, and complex types by
    /// recursively calculating their size. The calculation may align sizes to even byte boundaries for certain
    /// types.</remarks>
    /// <param name="numBytes">The initial number of bytes to be increased based on the type and property
    /// information.</param>
    /// <param name="type">The type of the value for which the byte size is being calculated. Determines how
    /// the number of bytes is
    /// adjusted.</param>
    /// <param name="propertyInfo">Optional property metadata used to retrieve custom attributes, such as
    /// S7StringAttribute, which may affect the
    /// byte calculation for certain types.</param>
    /// <returns>The total number of bytes required to represent the value, adjusted according to the type and
    /// any relevant
    /// property attributes.</returns>
    /// <exception cref="ArgumentException">Thrown if the type is 'String' and the property does not have an
    /// S7StringAttribute, or if an instance of the
    /// specified type cannot be created.</exception>
    private static double GetIncreasedNumberOfBytes(double numBytes, Type type, PropertyInfo? propertyInfo)
    {
        switch (Type.GetTypeCode(type))
        {
            case TypeCode.Boolean:
                {
                    numBytes += BitSizeInBytes;
                    break;
                }

            case TypeCode.Byte:
                {
                    numBytes = Math.Ceiling(numBytes);
                    numBytes++;
                    break;
                }

            case TypeCode.Int16 or TypeCode.UInt16:
                {
                    IncrementToEven(ref numBytes);
                    numBytes += sizeof(short);
                    break;
                }

            case TypeCode.Int32 or TypeCode.UInt32:
                {
                    IncrementToEven(ref numBytes);
                    numBytes += sizeof(int);
                    break;
                }

            case TypeCode.Single:
                {
                    IncrementToEven(ref numBytes);
                    numBytes += sizeof(float);
                    break;
                }

            case TypeCode.Double:
                {
                    IncrementToEven(ref numBytes);
                    numBytes += sizeof(double);
                    break;
                }

            case TypeCode.String:
                {
                    var attribute = propertyInfo is null ? null : GetS7StringAttribute(propertyInfo);
                    if (attribute == default(S7StringAttribute))
                    {
                        throw new ArgumentException(MissingS7StringAttributeMessage);
                    }

                    IncrementToEven(ref numBytes);
                    numBytes += attribute.ReservedLengthInBytes;
                    break;
                }

            default:
                {
                    var propertyClass = Activator.CreateInstance(type) ??
                        throw new ArgumentException($"Failed to create instance of type {type}.", nameof(type));
                    numBytes = GetClassSize(propertyClass, numBytes, true);
                    break;
                }
        }

        return numBytes;
    }

    /// <summary>Reads a property value from bytes using its property type.</summary>
    /// <remarks>This method supports several primitive types, including Boolean, Byte, Int16, UInt16, Int32,
    /// UInt32, Single, Double, and String, as well as custom types. For string properties, an S7StringAttribute must be
    /// present to specify the string format and length. The method advances the numBytes reference to track the
    /// position in the byte array after reading each value.</remarks>
    /// <param name="propertyType">The type of the property to extract. Determines how the bytes are
    /// interpreted and what value is returned.</param>
    /// <param name="propertyInfo">Metadata about the property, used for extracting additional information
    /// such as custom attributes. Can be null
    /// if not required for the property type.</param>
    /// <param name="bytes">The byte array containing the raw data from which the property value is extracted.</param>
    /// <param name="numBytes">A reference to the current position within the byte array. Updated to reflect
    /// the number of bytes consumed
    /// during extraction.</param>
    /// <returns>An object representing the extracted property value, typed according to the specified
    /// property type. Returns
    /// null if the value cannot be determined.</returns>
    /// <exception cref="ArgumentException">Thrown if the property type is string and the property does not
    /// have a required S7StringAttribute, or if an
    /// invalid string type is specified for the S7StringAttribute. Also thrown if an instance of the specified property
    /// type cannot be created.</exception>
    private static object? GetPropertyValue(
        Type propertyType,
        PropertyInfo? propertyInfo,
        byte[] bytes,
        ref double numBytes) => Type.GetTypeCode(propertyType) switch
    {
        TypeCode.Boolean or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or
            TypeCode.Single or TypeCode.Double =>
            GetPrimitivePropertyValue(Type.GetTypeCode(propertyType), bytes, ref numBytes),
        TypeCode.String => GetStringPropertyValue(propertyInfo, bytes, ref numBytes),
        _ => GetNestedPropertyValue(propertyType, bytes, ref numBytes),
    };

    /// <summary>Deserializes a supported primitive property value.</summary>
    /// <param name="typeCode">The primitive type code to deserialize.</param>
    /// <param name="bytes">The source byte array.</param>
    /// <param name="numBytes">The current byte offset, updated after deserialization.</param>
    /// <returns>The deserialized primitive value.</returns>
    private static object GetPrimitivePropertyValue(
        TypeCode typeCode,
        byte[] bytes,
        ref double numBytes) => typeCode switch
    {
        TypeCode.Boolean => GetBooleanPropertyValue(bytes, ref numBytes),
        TypeCode.Byte => GetBytePropertyValue(bytes, ref numBytes),
        TypeCode.Int16 => GetInt16PropertyValue(bytes, ref numBytes),
        TypeCode.UInt16 => GetUInt16PropertyValue(bytes, ref numBytes),
        TypeCode.Int32 => GetInt32PropertyValue(bytes, ref numBytes),
        TypeCode.UInt32 => GetUInt32PropertyValue(bytes, ref numBytes),
        TypeCode.Single => GetSinglePropertyValue(bytes, ref numBytes),
        TypeCode.Double => GetDoublePropertyValue(bytes, ref numBytes),
        _ => throw new ArgumentOutOfRangeException(nameof(typeCode)),
    };

    /// <summary>Deserializes a Boolean value from the current bit offset.</summary>
    /// <param name="bytes">The source byte array.</param>
    /// <param name="numBytes">The current byte offset, updated after deserialization.</param>
    /// <returns>The deserialized Boolean value.</returns>
    private static bool GetBooleanPropertyValue(byte[] bytes, ref double numBytes)
    {
        var bytePos = (int)Math.Floor(numBytes);
        var bitPos = (int)((numBytes - bytePos) / BitSizeInBytes);
        numBytes += BitSizeInBytes;
        return (bytes[bytePos] & (1 << bitPos)) != 0;
    }

    /// <summary>Deserializes a byte value from the current offset.</summary>
    /// <param name="bytes">The source byte array.</param>
    /// <param name="numBytes">The current byte offset, updated after deserialization.</param>
    /// <returns>The deserialized byte value.</returns>
    private static byte GetBytePropertyValue(byte[] bytes, ref double numBytes)
    {
        numBytes = Math.Ceiling(numBytes);
        var value = bytes[(int)numBytes];
        numBytes++;
        return value;
    }

    /// <summary>Deserializes a signed 16-bit value from the current offset.</summary>
    /// <param name="bytes">The source byte array.</param>
    /// <param name="numBytes">The current byte offset, updated after deserialization.</param>
    /// <returns>The deserialized signed 16-bit value.</returns>
    private static short GetInt16PropertyValue(byte[] bytes, ref double numBytes)
    {
        IncrementToEven(ref numBytes);
        var value = ConversionExtensions.ConvertToShort(Word.FromBytes(bytes[(int)numBytes + 1], bytes[(int)numBytes]));
        numBytes += sizeof(short);
        return value;
    }

    /// <summary>Deserializes an unsigned 16-bit value from the current offset.</summary>
    /// <param name="bytes">The source byte array.</param>
    /// <param name="numBytes">The current byte offset, updated after deserialization.</param>
    /// <returns>The deserialized unsigned 16-bit value.</returns>
    private static ushort GetUInt16PropertyValue(byte[] bytes, ref double numBytes)
    {
        IncrementToEven(ref numBytes);
        var value = Word.FromBytes(bytes[(int)numBytes + 1], bytes[(int)numBytes]);
        numBytes += sizeof(ushort);
        return value;
    }

    /// <summary>Deserializes a signed 32-bit value from the current offset.</summary>
    /// <param name="bytes">The source byte array.</param>
    /// <param name="numBytes">The current byte offset, updated after deserialization.</param>
    /// <returns>The deserialized signed 32-bit value.</returns>
    private static int GetInt32PropertyValue(byte[] bytes, ref double numBytes)
    {
        IncrementToEven(ref numBytes);
        var value = ConversionExtensions.ConvertToInt(DWord.FromByteArray(GetBytes(bytes, numBytes, sizeof(int))));
        numBytes += sizeof(int);
        return value;
    }

    /// <summary>Deserializes an unsigned 32-bit value from the current offset.</summary>
    /// <param name="bytes">The source byte array.</param>
    /// <param name="numBytes">The current byte offset, updated after deserialization.</param>
    /// <returns>The deserialized unsigned 32-bit value.</returns>
    private static uint GetUInt32PropertyValue(byte[] bytes, ref double numBytes)
    {
        IncrementToEven(ref numBytes);
        var value = DWord.FromByteArray(GetBytes(bytes, numBytes, sizeof(uint)));
        numBytes += sizeof(uint);
        return value;
    }

    /// <summary>Deserializes a single-precision value from the current offset.</summary>
    /// <param name="bytes">The source byte array.</param>
    /// <param name="numBytes">The current byte offset, updated after deserialization.</param>
    /// <returns>The deserialized single-precision value.</returns>
    private static float GetSinglePropertyValue(byte[] bytes, ref double numBytes)
    {
        IncrementToEven(ref numBytes);
        var value = Real.FromSpan(bytes.AsSpan((int)numBytes, sizeof(float)));
        numBytes += sizeof(float);
        return value;
    }

    /// <summary>Deserializes a double-precision value from the current offset.</summary>
    /// <param name="bytes">The source byte array.</param>
    /// <param name="numBytes">The current byte offset, updated after deserialization.</param>
    /// <returns>The deserialized double-precision value.</returns>
    private static double GetDoublePropertyValue(byte[] bytes, ref double numBytes)
    {
        IncrementToEven(ref numBytes);
        var value = LReal.FromByteArray(GetBytes(bytes, numBytes, sizeof(double)));
        numBytes += sizeof(double);
        return value;
    }

    /// <summary>Deserializes a string property using its S7 string metadata.</summary>
    /// <param name="propertyInfo">The property metadata that supplies S7 string settings.</param>
    /// <param name="bytes">The source byte array.</param>
    /// <param name="numBytes">The current byte offset, updated after deserialization.</param>
    /// <returns>The deserialized string value.</returns>
    private static string GetStringPropertyValue(PropertyInfo? propertyInfo, byte[] bytes, ref double numBytes)
    {
        var attribute = propertyInfo is null ? null : GetS7StringAttribute(propertyInfo);
        if (attribute == default(S7StringAttribute))
        {
            throw new ArgumentException(MissingS7StringAttributeMessage);
        }

        IncrementToEven(ref numBytes);
        var stringData = GetBytes(bytes, numBytes, attribute.ReservedLengthInBytes);
        numBytes += stringData.Length;
        return attribute.Type == S7StringType.S7String
            ? S7String.FromByteArray(stringData)
            : S7WString.FromByteArray(stringData);
    }

    /// <summary>Deserializes a nested property instance.</summary>
    /// <param name="propertyType">The type of nested object to create and deserialize.</param>
    /// <param name="bytes">The source byte array.</param>
    /// <param name="numBytes">The current byte offset, updated after deserialization.</param>
    /// <returns>The deserialized nested object.</returns>
    private static object GetNestedPropertyValue(Type propertyType, byte[] bytes, ref double numBytes)
    {
        var propertyValue = Activator.CreateInstance(propertyType) ??
            throw new ArgumentException($"Failed to create instance of type {propertyType}.", nameof(propertyType));
        numBytes = FromBytes(propertyValue, bytes, numBytes);
        return propertyValue;
    }

    /// <summary>Copies a contiguous byte range from the current offset.</summary>
    /// <param name="bytes">The source byte array.</param>
    /// <param name="numBytes">The offset at which to begin copying.</param>
    /// <param name="length">The number of bytes to copy.</param>
    /// <returns>A new array containing the requested byte range.</returns>
    private static byte[] GetBytes(byte[] bytes, double numBytes, int length)
    {
        var result = new byte[length];
        Array.Copy(bytes, (int)numBytes, result, 0, result.Length);
        return result;
    }

    /// <summary>
    /// Serializes the specified property value into the provided byte array, starting at the given offset, and returns
    /// the updated offset after writing the value.
    /// </summary>
    /// <remarks>For string properties, the method relies on the S7StringAttribute to determine the
    /// serialization format and reserved length. The method supports multiple primitive types and will use
    /// type-specific serialization logic. If the property type is not explicitly handled, a generic serialization
    /// method is invoked. The caller is responsible for ensuring that the byte array has sufficient capacity for the
    /// serialized data.</remarks>
    /// <param name="propertyValue">The value of the property to serialize. Supported types include Boolean,
    /// Byte, Int16, UInt16, Int32, UInt32,
    /// Single, Double, and String.</param>
    /// <param name="propertyInfo">Metadata about the property being serialized. Required for string
    /// properties to retrieve custom serialization
    /// attributes; can be null for other types.</param>
    /// <param name="bytes">The byte array into which the property value will be written. The array must be
    /// large enough to accommodate the
    /// serialized data at the specified offset.</param>
    /// <param name="numBytes">The starting offset, in bytes, within the array where the property value will
    /// be written. The method returns the
    /// offset after writing.</param>
    /// <returns>The offset, in bytes, immediately following the serialized property value in the array.</returns>
    /// <exception cref="ArgumentException">Thrown if the property value is a string and the corresponding
    /// property does not have a valid S7StringAttribute,
    /// or if the attribute specifies an unsupported string type.</exception>
    private static double SetBytesFromProperty(
        object propertyValue,
        PropertyInfo? propertyInfo,
        byte[] bytes,
        double numBytes)
    {
        if (propertyValue is bool booleanValue)
        {
            SetBooleanPropertyBytes(booleanValue, bytes, ref numBytes);
            return numBytes;
        }

        if (propertyValue is byte byteValue)
        {
            SetBytePropertyBytes(byteValue, bytes, ref numBytes);
            return numBytes;
        }

        var valueBytes = GetPropertyBytes(propertyValue, propertyInfo, bytes, ref numBytes);
        if (valueBytes is null)
        {
            return numBytes;
        }

        WriteAlignedBytes(valueBytes, bytes, ref numBytes);
        return numBytes;
    }

    /// <summary>Serializes a Boolean value at the current bit offset.</summary>
    /// <param name="value">The Boolean value to serialize.</param>
    /// <param name="bytes">The destination byte array.</param>
    /// <param name="numBytes">The current byte offset, updated after serialization.</param>
    private static void SetBooleanPropertyBytes(bool value, byte[] bytes, ref double numBytes)
    {
        var bytePos = (int)Math.Floor(numBytes);
        var bitPos = (int)((numBytes - bytePos) / BitSizeInBytes);
        if (value)
        {
            bytes[bytePos] |= (byte)(1 << bitPos);
        }
        else
        {
            bytes[bytePos] &= (byte)~(1 << bitPos);
        }

        numBytes += BitSizeInBytes;
    }

    /// <summary>Serializes a byte value at the current offset.</summary>
    /// <param name="value">The byte value to serialize.</param>
    /// <param name="bytes">The destination byte array.</param>
    /// <param name="numBytes">The current byte offset, updated after serialization.</param>
    private static void SetBytePropertyBytes(byte value, byte[] bytes, ref double numBytes)
    {
        numBytes = Math.Ceiling(numBytes);
        bytes[(int)numBytes] = value;
        numBytes++;
    }

    /// <summary>Serializes a non-Boolean property value to its byte representation.</summary>
    /// <param name="propertyValue">The value to serialize.</param>
    /// <param name="propertyInfo">The property metadata used for string serialization.</param>
    /// <param name="destination">The destination byte array for nested values.</param>
    /// <param name="numBytes">The current byte offset, updated for nested values.</param>
    /// <returns>The serialized bytes, or null when a nested value was written directly.</returns>
    private static byte[]? GetPropertyBytes(
        object propertyValue,
        PropertyInfo? propertyInfo,
        byte[] destination,
        ref double numBytes)
    {
        return Type.GetTypeCode(propertyValue.GetType()) switch
        {
            TypeCode.Int16 => Int.ToByteArray((short)propertyValue),
            TypeCode.UInt16 => Word.ToByteArray((ushort)propertyValue),
            TypeCode.Int32 => DInt.ToByteArray((int)propertyValue),
            TypeCode.UInt32 => DWord.ToByteArray((uint)propertyValue),
            TypeCode.Single => Real.ToByteArray((float)propertyValue),
            TypeCode.Double => LReal.ToByteArray((double)propertyValue),
            TypeCode.String => GetStringPropertyBytes((string)propertyValue, propertyInfo),
            _ => SerializeNestedProperty(propertyValue, destination, ref numBytes),
        };
    }

    /// <summary>Serializes a string value using its S7 string metadata.</summary>
    /// <param name="value">The string value to serialize.</param>
    /// <param name="propertyInfo">The property metadata that supplies S7 string settings.</param>
    /// <returns>The serialized string bytes.</returns>
    private static byte[] GetStringPropertyBytes(string value, PropertyInfo? propertyInfo)
    {
        var attribute = propertyInfo is null ? null : GetS7StringAttribute(propertyInfo);
        if (attribute == default(S7StringAttribute))
        {
            throw new ArgumentException(MissingS7StringAttributeMessage);
        }

        return attribute.Type == S7StringType.S7String
            ? S7String.ToByteArray(value, attribute.ReservedLength)
            : S7WString.ToByteArray(value, attribute.ReservedLength);
    }

    /// <summary>Serializes a nested property directly into the destination array.</summary>
    /// <param name="propertyValue">The nested value to serialize.</param>
    /// <param name="bytes">The destination byte array.</param>
    /// <param name="numBytes">The current byte offset, updated after serialization.</param>
    /// <returns>Null after writing the nested value directly to the destination.</returns>
    private static byte[]? SerializeNestedProperty(object propertyValue, byte[] bytes, ref double numBytes)
    {
        numBytes = ToBytes(propertyValue, bytes, numBytes);
        return null;
    }

    /// <summary>Writes bytes at the next aligned destination offset.</summary>
    /// <param name="source">The bytes to write.</param>
    /// <param name="destination">The destination byte array.</param>
    /// <param name="numBytes">The current byte offset, updated after writing.</param>
    private static void WriteAlignedBytes(byte[] source, byte[] destination, ref double numBytes)
    {
        IncrementToEven(ref numBytes);
        var bytePos = (int)numBytes;
        for (var index = 0; index < source.Length; index++)
        {
            destination[bytePos + index] = source[index];
        }

        numBytes += source.Length;
    }

    /// <summary>Rounds the specified value up to the nearest even integer.</summary>
    /// <remarks>This method first rounds the value up to the nearest integer, then increments it if the
    /// result is odd to ensure it is even. The input value is modified in place.</remarks>
    /// <param name="numBytes">A reference to the value to be rounded. The value will be updated to the next
    /// even integer greater than or equal
    /// to its original value.</param>
    private static void IncrementToEven(ref double numBytes)
    {
        numBytes = Math.Ceiling(numBytes);
        if (numBytes % ByteAlignment == 0)
        {
            return;
        }

        numBytes++;
    }

    /// <summary>Gets the S7 string attribute for a member.</summary>
    /// <param name="memberInfo">The member to inspect.</param>
    /// <returns>The S7 string attribute, or null when one is not present.</returns>
    private static S7StringAttribute? GetS7StringAttribute(MemberInfo memberInfo)
    {
        S7StringAttribute? result = null;
        foreach (var attribute in memberInfo.GetCustomAttributes<S7StringAttribute>())
        {
            if (result is not null)
            {
                throw new InvalidOperationException(
                    $"Multiple {nameof(S7StringAttribute)} attributes were found on {memberInfo.Name}.");
            }

            result = attribute;
        }

        return result;
    }
}
