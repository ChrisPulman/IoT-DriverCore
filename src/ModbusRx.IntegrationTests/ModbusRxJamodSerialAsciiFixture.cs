// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if JAMOD
using System.Threading.Tasks;
using IoT.DriverCore.ModbusRx.Device;

namespace IoT.DriverCore.ModbusRx.IntegrationTests;

/// <summary>Provides ModbusRx serial ASCII master tests against a Jamod serial ASCII slave.</summary>
/// <seealso cref="ModbusRxMasterFixtureBase" />
[TUnit.Core.InheritsTests]
public class ModbusRxJamodSerialAsciiFixture : ModbusRxMasterFixtureBase
{
    /// <summary>Initializes a new instance of the <see cref="ModbusRxJamodSerialAsciiFixture"/> class.</summary>
    public ModbusRxJamodSerialAsciiFixture()
    {
        StartJamodSlaveAsync(Program).GetAwaiter().GetResult();

        MasterSerialPort = CreateAndOpenSerialPort(DefaultMasterSerialPortName);
        Master = ModbusSerialMaster.CreateAscii(MasterSerialPort);
    }

    /// <summary>Gets the transport used by the fixture.</summary>
    protected override string TransportName => "Jamod serial ASCII";

    /// <summary>Gets the Jamod serial ASCII slave program arguments.</summary>
    private static string Program => $"SerialSlave {DefaultSlaveSerialPortName} ASCII";

    /// <summary>Does not execute unsupported read/write-multiple-registers behavior.</summary>
    /// <returns>A completed task.</returns>
    public override Task ReadWriteMultipleRegistersAsync() =>
        Task.CompletedTask;

    /// <summary>Reads coils through the configured Jamod slave.</summary>
    /// <returns>A task that represents the test operation.</returns>
    [TUnit.Core.Test]
    public override Task ReadCoilsAsync() =>
        base.ReadCoilsAsync();
}
#endif
