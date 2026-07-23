// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using IoT.DriverCore.ModbusRx.Data;

namespace IoT.DriverCore.ModbusRx.UnitTests.Data;

/// <summary>Tests the DiscreteCollectionFixture behavior.</summary>
public class DiscreteCollectionFixture
{
    /// <summary>Bytes the count.</summary>
    [TUnit.Core.Test]
    public void ByteCount()
    {
        var col = new DiscreteCollection(true, true, false, false, false, false, false, false, false);
        Assert.Equal(Num.Value2, col.ByteCount);
    }

    /// <summary>Bytes the count even.</summary>
    [TUnit.Core.Test]
    public void ByteCountEven()
    {
        var col = new DiscreteCollection(true, true, false, false, false, false, false, false);
        Assert.Equal(1, col.ByteCount);
    }

    /// <summary>Networks the bytes.</summary>
    [TUnit.Core.Test]
    public void NetworkBytes()
    {
        var col = new DiscreteCollection(true, true);
        Assert.Equal([Num.Value3], col.NetworkBytes);
    }

    /// <summary>Creates the new discrete collection initialize.</summary>
    [TUnit.Core.Test]
    public void CreateNewDiscreteCollectionInitialize()
    {
        var col = new DiscreteCollection(true, true, true);
        Assert.Equal(Num.Value3, col.Count);
        Assert.DoesNotContain(false, col);
    }

    /// <summary>Creates the new discrete collection from bool parameters.</summary>
    [TUnit.Core.Test]
    public void CreateNewDiscreteCollectionFromBoolParams()
    {
        var col = new DiscreteCollection(true, false, true);
        Assert.Equal(Num.Value3, col.Count);
    }

    /// <summary>Creates the new discrete collection from bytes parameters.</summary>
    [TUnit.Core.Test]
    public void CreateNewDiscreteCollectionFromBytesParams()
    {
        var col = new DiscreteCollection(1, Num.Value2, Num.Value3);
        Assert.Equal(Num.Value24, col.Count);
        var expected = new bool[]
        {
            true, false, false, false, false, false, false, false,
            false, true, false, false, false, false, false, false,
            true, true, false, false, false, false, false, false,
        };

        Assert.Equal(expected, col);
    }

    /// <summary>Creates the new discrete collection from bytes parameters zero length array.</summary>
    [TUnit.Core.Test]
    public void CreateNewDiscreteCollectionFromBytesParams_ZeroLengthArray()
    {
        byte[] bytes = [];
        var col = new DiscreteCollection(bytes);
        Assert.Empty(col);
    }

    /// <summary>Creates the new discrete collection from bytes parameters null array.</summary>
    [TUnit.Core.Test]
    public void CreateNewDiscreteCollectionFromBytesParams_NullArray() =>
        Assert.Throws<ArgumentNullException>(() => _ = new DiscreteCollection((byte[])null!));

    /// <summary>Creates the new discrete collection from bytes parameters order.</summary>
    [TUnit.Core.Test]
    public void CreateNewDiscreteCollectionFromBytesParamsOrder()
    {
        var col = new DiscreteCollection(Num.Value194);
        Assert.Equal<IEnumerable<bool>>([false, true, false, false, false, false, true, true], col);
    }

    /// <summary>Creates the new discrete collection from bytes parameters order2.</summary>
    [TUnit.Core.Test]
    public void CreateNewDiscreteCollectionFromBytesParamsOrder2()
    {
        var col = new DiscreteCollection(Num.Value157, Num.Value7);
        Assert.Equal(
            [
                true, false, true, true, true, false, false, true, true, true, true, false, false, false, false, false,
            ],
            col);
    }

    /// <summary>Resizes this instance.</summary>
    [TUnit.Core.Test]
    public void Resize()
    {
        var col = new DiscreteCollection(byte.MaxValue, byte.MaxValue);
        Assert.Equal(Num.Value16, col.Count);
        col.RemoveAt(Num.Value3);
        Assert.Equal(Num.Value15, col.Count);
    }

    /// <summary>Byteses the persistence.</summary>
    [TUnit.Core.Test]
    public void BytesPersistence()
    {
        var col = new DiscreteCollection(byte.MaxValue, byte.MaxValue);
        Assert.Equal(Num.Value16, col.Count);
        var originalBytes = col.NetworkBytes;
        col.RemoveAt(Num.Value3);
        Assert.Equal(Num.Value15, col.Count);
        Assert.NotEqual(originalBytes, col.NetworkBytes);
    }

    /// <summary>Adds the coil.</summary>
    [TUnit.Core.Test]
    public void AddCoil()
    {
        var col = new DiscreteCollection();
        Assert.Empty(col);

        col.Add(true);
        _ = Assert.Single(col);
    }
}
