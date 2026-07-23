// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using IoT.DriverCore.ModbusRx.Data;
using IoT.DriverCore.ModbusRx.UnitTests.Message;

namespace IoT.DriverCore.ModbusRx.UnitTests.Utility;

/// <summary>Tests the CollectionUtilityFixture behavior.</summary>
public class CollectionUtilityFixture
{
    /// <summary>Slices the middle.</summary>
    [TUnit.Core.Test]
    public void SliceMiddle()
    {
        byte[] test = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        Assert.Equal(
            [Num.Value3, Num.Value4, Num.Value5, Num.Value6, Num.Value7],
            test.Skip(Num.Value2).Take(Num.Value5));
    }

    /// <summary>Slices the beginning.</summary>
    [TUnit.Core.Test]
    public void SliceBeginning()
    {
        byte[] test = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        Assert.Equal([1, Num.Value2], test.Take(Num.Value2));
    }

    /// <summary>Slices the end.</summary>
    [TUnit.Core.Test]
    public void SliceEnd()
    {
        byte[] test = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        Assert.Equal([Num.Value9, Num.Value10], test.Skip(Num.Value8).Take(Num.Value2));
    }

    /// <summary>Slices the collection.</summary>
    [TUnit.Core.Test]
    public void SliceCollection()
    {
        var col = new Collection<bool>([ true, false, false, false, true, true]);
        Assert.Equal([false, false, true], col.Skip(Num.Value2).Take(Num.Value3));
    }

    /// <summary>Slices the read only collection.</summary>
    [TUnit.Core.Test]
    public void SliceReadOnlyCollection()
    {
        var col = new ReadOnlyCollection<bool>([ true, false, false, false, true, true]);
        Assert.Equal([false, false, true], col.Skip(Num.Value2).Take(Num.Value3));
    }

    /// <summary>Slices the null i collection.</summary>
    [TUnit.Core.Test]
    public void SliceNullICollection()
    {
        ICollection<bool> col = null!;
        _ = Assert.Throws<ArgumentNullException>(() => _ = col.Skip(1));
    }

    /// <summary>Slices the null array.</summary>
    [TUnit.Core.Test]
    public void SliceNullArray()
    {
        bool[] array = null!;
        _ = Assert.Throws<ArgumentNullException>(() => _ = array.Skip(1));
    }

    /// <summary>Creates the default size of the collection negative.</summary>
    [TUnit.Core.Test]
    public void CreateDefaultCollectionNegativeSize() => Assert.Throws<ArgumentOutOfRangeException>(
        () => MessageUtility.CreateDefaultCollection(new RegisterCollection(), (ushort)0, -1));

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
