// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace ModbusRx.UnitTests.Message;

/// <summary>Tests the MessageUtility behavior.</summary>
public static class MessageUtility
{
    /// <summary>Creates a collection initialized to a default value.</summary>
    /// <typeparam name="TCollection">The collection type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="collection">The collection to initialize.</param>
    /// <param name="defaultValue">The default value.</param>
    /// <param name="size">The size.</param>
    /// <returns>A value of T.</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">size - Collection size cannot be less than 0.</exception>
    public static TCollection CreateDefaultCollection<TCollection, TValue>(
        TCollection collection,
        TValue defaultValue,
        int size)
        where TCollection : ICollection<TValue>
    {
        if (size < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Collection size cannot be less than 0.");
        }

        for (var i = 0; i < size; i++)
        {
            collection.Add(defaultValue);
        }

        return collection;
    }
}
