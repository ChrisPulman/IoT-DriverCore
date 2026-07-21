// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if SERIAL
using System;
using ModbusRx.Device;
#endif

namespace ModbusRx.IntegrationTests;

/// <summary>Tests the NModbusSerialAsciiMasterFixture behavior.</summary>
public class ModbusRxSerialAsciiMasterFixture : NetworkTestBase
{
#if SERIAL
    private const int ReadTimeoutMilliseconds = 1000;
#endif

    /// <summary>Tests the modbus ASCII master read timeout.</summary>
    [TUnit.Core.Test]
    public void ModbusRxAsciiMaster_ReadTimeout()
    {
        // Skip this test in CI environments as serial ports are not available
        Skip.IfNot(!IsRunningInCI, "Serial port tests require physical hardware not available in CI");

#if SERIAL
        var port = ModbusRxMasterFixtureBase.CreateAndOpenSerialPort(
            ModbusRxMasterFixtureBase.DefaultMasterSerialPortName);
        RegisterDisposable(port);
        
        using IModbusSerialMaster master = ModbusSerialMaster.CreateAscii(port);
        master.Transport!.ReadTimeout = master.Transport.WriteTimeout = ReadTimeoutMilliseconds;
        Assert.Throws<TimeoutException>(() => master.ReadCoils(100, 1, 1));
#else
        // When SERIAL symbol is not defined, skip with explanation
        Skip.If(true, "SERIAL conditional compilation symbol not defined");
#endif
    }
}
