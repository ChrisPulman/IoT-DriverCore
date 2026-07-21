// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using ModbusRx.Data;

namespace ModbusRx.UnitTests.Data;

/// <summary>Tests the DataStoreEventArgsFixture behavior.</summary>
public class DataStoreEventArgsFixture
{
    /// <summary>Creates the data store event arguments.</summary>
    [TUnit.Core.Test]
    public void CreateDataStoreEventArgs()
    {
        var eventArgs = DataStoreEventArgs.CreateDataStoreEventArgs(
            Num.Value5,
            ModbusDataType.HoldingRegister,
            [(ushort)1, Num.UShortValue2, Num.UShortValue3]);
        Assert.Equal(ModbusDataType.HoldingRegister, eventArgs.ModbusDataType);
        Assert.Equal(Num.Value5, eventArgs.StartAddress);
        Assert.Equal<IEnumerable<ushort>>(
            [(ushort)1, Num.UShortValue2, Num.UShortValue3],
            Assert.NotNull(eventArgs.Data!.B));
    }

    /// <summary>Creates the type of the data store event arguments invalid.</summary>
    [TUnit.Core.Test]
    public void CreateDataStoreEventArgs_InvalidType() =>
        Assert.Throws<ArgumentException>(() =>
            DataStoreEventArgs.CreateDataStoreEventArgs(
                Num.Value5,
                ModbusDataType.HoldingRegister,
                [ 1, Num.Value2, Num.Value3]));

    /// <summary>Creates the data store event arguments data null.</summary>
    [TUnit.Core.Test]
    public void CreateDataStoreEventArgs_DataNull() =>
        Assert.Throws<ArgumentNullException>(() =>
            DataStoreEventArgs.CreateDataStoreEventArgs(
                Num.Value5,
                ModbusDataType.HoldingRegister,
                default(ushort[])!));
}
