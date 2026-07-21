// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ModbusRx.Data;
using ModbusRx.Message;

namespace ModbusRx.UnitTests.Message;

/// <summary>Tests the DiagnosticsRequestResponseFixture behavior.</summary>
public class DiagnosticsRequestResponseFixture
{
    /// <summary>Converts to string_test.</summary>
    [TUnit.Core.Test]
    public void ToString_Test()
    {
        DiagnosticsRequestResponse response =
            new(Modbus.DiagnosticsReturnQueryData, Num.Value3, new RegisterCollection(Num.Value5));
        Assert.Equal("Diagnostics message, sub-function return query data - {5}.", response.ToString());
    }
}
