// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using IoT.Driver.ModbusRx.Reactive.Utility;
#else
using IoT.Driver.ModbusRx.Utility;
#endif

#if REACTIVE_SHIM
namespace IoT.Driver.ModbusRx.Reactive.Unme.Common;
#else
namespace IoT.Driver.ModbusRx.Unme.Common;
#endif

/// <summary>Provides sequence slicing helpers.</summary>
internal static class SequenceExtensions
{
    /// <summary>Returns a bounded slice of the source sequence.</summary>
    /// <typeparam name="T">The extension receiver item type.</typeparam>
    /// <param name="source">The extension receiver.</param>
    /// <param name="startIndex">The zero-based index where the slice starts.</param>
    /// <param name="size">The number of items to include.</param>
    /// <returns>The sliced items.</returns>
    internal static IEnumerable<T> Slice<T>(IEnumerable<T> source, int startIndex, int size)
    {
        source = ModbusGuard.NotNull(source, nameof(source));

        var enumerable = (source as T[]) ?? Materialize(source);
        var num = enumerable.Length;

        if (startIndex < 0 || num < startIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(startIndex));
        }

        if (size < 0 || startIndex + size > num)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        var result = new T[size];
        Array.Copy(enumerable, startIndex, result, 0, size);
        return result;
    }

    /// <summary>Materializes a sequence into an array without using LINQ.</summary>
    /// <typeparam name="T">The sequence item type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <returns>The materialized items.</returns>
    private static T[] Materialize<T>(IEnumerable<T> source)
    {
        return new List<T>(source).ToArray();
    }
}
