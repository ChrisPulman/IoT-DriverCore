// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace IoT.DriverCore.ABPlcRx.Reactive;
#else
namespace IoT.DriverCore.ABPlcRx;
#endif

/// <summary>Tag size definition.</summary>
internal static class DataLength
{
    /// <summary>The int8.</summary>
    internal const int INT8 = 1;

    /// <summary>The uint8.</summary>
    internal const int UINT8 = INT8;

    /// <summary>The int16.</summary>
    internal const int INT16 = 2;

    /// <summary>The uint16.</summary>
    internal const int UINT16 = INT16;

    /// <summary>The int32.</summary>
    internal const int INT32 = 4;

    /// <summary>The uint32.</summary>
    internal const int UINT32 = INT32;

    /// <summary>The int64.</summary>
    internal const int INT64 = 8;

    /// <summary>The uint64.</summary>
    internal const int UINT64 = INT64;

    /// <summary>The float32.</summary>
    internal const int FLOAT32 = 4;

    /// <summary>The float64.</summary>
    internal const int FLOAT64 = 8;

    /// <summary>The string.</summary>
    internal const int STRING = 88;

    /// <summary>Gets native type definition.</summary>
    /// <value>
    /// The native types.
    /// </value>
    internal static IReadOnlyDictionary<Type, int> NativeTypes { get; } = new Dictionary<Type, int>
    {
        { typeof(bool), UINT8 },
        { typeof(long), INT64 },
        { typeof(ulong), UINT64 },
        { typeof(int), INT32 },
        { typeof(uint), UINT32 },
        { typeof(short), INT16 },
        { typeof(ushort), UINT16 },
        { typeof(sbyte), INT8 },
        { typeof(byte), UINT8 },
        { typeof(float), FLOAT32 },
        { typeof(double), FLOAT64 },
        { typeof(string), STRING },
    };

    /// <summary>Get size from object.</summary>
    /// <param name="obj">The object.</param>
    /// <returns>A Value.</returns>
    internal static int GetSizeObject(object? obj)
    {
        if (obj is null)
        {
            return 0;
        }

        var size = 0;

        var type = obj.GetType();
        if (type.IsArray)
        {
            var array = TagHelper.GetArray(obj);
            if (array is null)
            {
                return size;
            }

            foreach (var el in array)
            {
                size += GetSizeObject(el);
            }
        }
        else if (!NativeTypes.TryGetValue(type, out size) && type.IsClass && !type.IsAbstract)
        {
            size += TagHelper.GetAccessableProperties(type)
                             .Sum(a => GetSizeObject(a.GetValue(obj)));
        }

        return size;
    }

    /// <summary>Check type is native type.</summary>
    /// <param name="type">The type.</param>
    /// <returns>
    ///   <c>true</c> if [is native type] [the specified type]; otherwise, <c>false</c>.
    /// </returns>
    internal static bool IsNativeType(Type type) => NativeTypes.ContainsKey(type);
}
