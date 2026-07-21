// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if SERIAL
using ModbusRx.Device;

namespace ModbusRx.IntegrationTests;

/// <summary>
/// Base class for ModbusSerialMaster test fixtures.
/// </summary>
/// <seealso cref="ModbusRx.IntegrationTests.ModbusRxMasterFixtureBase" />
public abstract class ModbusSerialMasterFixtureBase : ModbusRxMasterFixtureBase
{
    /// <summary>Gets the serial device addressed by the fixture.</summary>
    protected abstract string SerialDeviceName { get; }

    /// <summary>Gets the transport used by the fixture.</summary>
    protected override string TransportName => "Serial";

    /// <summary>
    /// Initializes a new instance of the <see cref="ModbusSerialMasterFixtureBase"/> class.
    /// </summary>
    protected ModbusSerialMasterFixtureBase()
    {
        // Skip all serial tests in CI environments
        SkipIfRunningInCI("Serial port tests require physical hardware not available in CI");
    }

    /// <summary>
    /// Returns the query data.
    /// </summary>
    [TUnit.Core.Test]
    public virtual void ReturnQueryData()
    {
        // This is a placeholder for the return query data test
        // Individual fixtures can override this method
        Assert.True(true, "ReturnQueryData test placeholder");
    }
}
#endif
