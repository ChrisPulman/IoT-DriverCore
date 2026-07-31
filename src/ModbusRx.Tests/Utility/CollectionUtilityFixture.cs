// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using IoT.Driver.ModbusRx.Data;
using IoT.Driver.ModbusRx.UnitTests.Message;

namespace IoT.Driver.ModbusRx.UnitTests.Utility;

/// <summary>Tests the CollectionUtilityFixture behavior.</summary>
public class CollectionUtilityFixture
{
    /// <summary>Slices the middle.</summary>
    [TUnit.Core.Test]
    public void SliceMiddle()
    {
        byte[] test = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        Assert.Equal<IEnumerable<byte>>(
            [Num.Value3, Num.Value4, Num.Value5, Num.Value6, Num.Value7],
            new ArraySegment<byte>(test, Num.Value2, Num.Value5));
    }

    /// <summary>Slices the beginning.</summary>
    [TUnit.Core.Test]
    public void SliceBeginning()
    {
        byte[] test = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        Assert.Equal<IEnumerable<byte>>([1, Num.Value2], new ArraySegment<byte>(test, 0, Num.Value2));
    }

    /// <summary>Slices the end.</summary>
    [TUnit.Core.Test]
    public void SliceEnd()
    {
        byte[] test = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        Assert.Equal<IEnumerable<byte>>([Num.Value9, Num.Value10], new ArraySegment<byte>(test, Num.Value8, Num.Value2));
    }

    /// <summary>Slices the collection.</summary>
    [TUnit.Core.Test]
    public void SliceCollection()
    {
        var col = new Collection<bool>([ true, false, false, false, true, true]);
        Assert.Equal<IEnumerable<bool>>([false, false, true], new List<bool>(col).GetRange(Num.Value2, Num.Value3));
    }

    /// <summary>Slices the read only collection.</summary>
    [TUnit.Core.Test]
    public void SliceReadOnlyCollection()
    {
        var col = new ReadOnlyCollection<bool>([ true, false, false, false, true, true]);
        Assert.Equal<IEnumerable<bool>>([false, false, true], new List<bool>(col).GetRange(Num.Value2, Num.Value3));
    }

    /// <summary>Slices the null i collection.</summary>
    [TUnit.Core.Test]
    public void SliceNullICollection()
    {
        ICollection<bool> col = null!;
        _ = Assert.Throws<ArgumentNullException>(() => _ = new List<bool>(col));
    }

    /// <summary>Slices the null array.</summary>
    [TUnit.Core.Test]
    public void SliceNullArray()
    {
        bool[] array = null!;
        _ = Assert.Throws<ArgumentNullException>(() => _ = new List<bool>(array));
    }

    /// <summary>Creates the default size of the collection negative.</summary>
    [TUnit.Core.Test]
    public void CreateDefaultCollectionNegativeSize() => Assert.Throws<ArgumentOutOfRangeException>(
        static () => MessageUtility.CreateDefaultCollection(new RegisterCollection(), (ushort)0, -1));

    /// <summary>Creates the default collection.</summary>
    [TUnit.Core.Test]
    public void CreateDefaultCollection()
    {
        var col = MessageUtility.CreateDefaultCollection(
            new RegisterCollection(),
            (ushort)Num.Value3,
            Num.Value5);
        Assert.Equal(Num.Value5, col.Count);
        Assert.Equal([Num.Value3, Num.Value3, Num.Value3, Num.Value3, Num.Value3], col);
    }
}
