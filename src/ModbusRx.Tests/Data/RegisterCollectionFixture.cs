// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.ModbusRx.Data;

namespace IoT.DriverCore.ModbusRx.UnitTests.Data;

/// <summary>Tests the RegisterCollectionFixture behavior.</summary>
public class RegisterCollectionFixture
{
    /// <summary>Bytes the count.</summary>
    [TUnit.Core.Test]
    public void ByteCount()
    {
        var col = new RegisterCollection(1, Num.Value2, Num.Value3);
        Assert.Equal(Num.Value6, col.ByteCount);
    }

    /// <summary>Creates new registercollection.</summary>
    [TUnit.Core.Test]
    public void NewRegisterCollection()
    {
        var col = new RegisterCollection(Num.Value5, Num.Value3, Num.Value4, Num.Value6);
        _ = Assert.NotNull(col);
        Assert.Equal(Num.Value4, col.Count);
        Assert.Equal(Num.Value5, col[0]);
    }

    /// <summary>Creates new registercollectionfrombytes.</summary>
    [TUnit.Core.Test]
    public void NewRegisterCollectionFromBytes()
    {
        var col = new RegisterCollection([0, 1, 0, Num.Value2, 0, Num.Value3]);
        _ = Assert.NotNull(col);
        Assert.Equal(Num.Value3, col.Count);
        Assert.Equal(1, col[0]);
        Assert.Equal(Num.Value2, col[1]);
        Assert.Equal(Num.Value3, col[2]);
    }

    /// <summary>Registers the collection network bytes.</summary>
    [TUnit.Core.Test]
    public void RegisterCollectionNetworkBytes()
    {
        var col = new RegisterCollection(Num.Value5, Num.Value3, Num.Value4, Num.Value6);
        var bytes = col.NetworkBytes;
        _ = Assert.NotNull(bytes);
        Assert.Equal(Num.Value8, bytes.Length);
        Assert.Equal([0, Num.Value5, 0, Num.Value3, 0, Num.Value4, 0, Num.Value6], bytes);
    }

    /// <summary>Registers the collection empty.</summary>
    [TUnit.Core.Test]
    public void RegisterCollectionEmpty()
    {
        var col = new RegisterCollection();
        _ = Assert.NotNull(col);
        Assert.Empty(col.NetworkBytes);
    }

    /// <summary>Modifies the register.</summary>
    [TUnit.Core.Test]
    public void ModifyRegister()
    {
        var col = new RegisterCollection(1, Num.Value2, Num.Value3, Num.Value4)
        {
            [0] = Num.Value5,
        };
        Assert.Equal(Num.Value5, col[0]);
    }

    /// <summary>Adds the register.</summary>
    [TUnit.Core.Test]
    public void AddRegister()
    {
        var col = new RegisterCollection();
        Assert.Empty(col);

        col.Add(Num.Value45);
        _ = Assert.Single(col);
    }

    /// <summary>Removes the register.</summary>
    [TUnit.Core.Test]
    public void RemoveRegister()
    {
        var col = new RegisterCollection(Num.Value3, Num.Value4, Num.Value5);
        Assert.Equal(Num.Value3, col.Count);
        col.RemoveAt(Num.Value2);
        Assert.Equal(Num.Value2, col.Count);
    }
}
