// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.S7PlcRx.Tests.Testing;

/// <summary>Provides target-independent array operations used by the test suite.</summary>
internal static class TestArray
{
    /// <summary>Assigns one value to every array element.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="array">The array to fill.</param>
    /// <param name="value">The value assigned to every element.</param>
    internal static void Fill<T>(T[] array, T value)
    {
        Guard.NotNull(array, nameof(array));
        for (var index = 0; index < array.Length; index++)
        {
            array[index] = value;
        }
    }
}
