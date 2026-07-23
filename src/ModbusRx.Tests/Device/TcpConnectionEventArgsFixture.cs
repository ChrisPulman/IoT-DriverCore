// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using IoT.DriverCore.ModbusRx.Device;

namespace IoT.DriverCore.ModbusRx.UnitTests.Device;

/// <summary>Tests the TcpConnectionEventArgsFixture behavior.</summary>
public class TcpConnectionEventArgsFixture
{
    /// <summary>TCPs the connection event arguments null end point.</summary>
    [TUnit.Core.Test]
    public void TcpConnectionEventArgs_NullEndPoint() =>
        Assert.Throws<ArgumentNullException>(() => _ = new TcpConnectionEventArgs(null!));

    /// <summary>TCPs the connection event arguments empty end point.</summary>
    [TUnit.Core.Test]
    public void TcpConnectionEventArgs_EmptyEndPoint() =>
        Assert.Throws<ArgumentException>(() => _ = new TcpConnectionEventArgs(string.Empty));

    /// <summary>TCPs the connection event arguments.</summary>
    [TUnit.Core.Test]
    public void TcpConnectionEventArgs()
    {
        var args = new TcpConnectionEventArgs("foo");

        Assert.Equal("foo", args.EndPoint);
    }
}
