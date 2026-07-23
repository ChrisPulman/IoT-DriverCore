// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive.Tests;
#else

namespace IoT.DriverCore.MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiDeviceAddressTests type.</summary>
internal sealed class MitsubishiDeviceAddressTests
{
    /// <summary>Executes the ParseUsesExpectedRadix operation.</summary>
    /// <param name="value">The value parameter.</param>
    /// <param name="notation">The notation parameter.</param>
    /// <param name="expected">The expected parameter.</param>
    /// <returns>The ParseUsesExpectedRadix operation result.</returns>
    [Test]
    [Arguments("X10", XyAddressNotation.Octal, 8)]
    [Arguments("X10", XyAddressNotation.Hexadecimal, 16)]
    [Arguments("Y17", XyAddressNotation.Octal, 15)]
    [Arguments("B10", XyAddressNotation.Hexadecimal, 16)]
    [Arguments("D100", XyAddressNotation.Octal, 100)]
    internal async Task ParseUsesExpectedRadixAsync(string value, XyAddressNotation notation, int expected)
    {
        var address = MitsubishiDeviceAddress.Parse(value, notation);

        await Assert.That(address.Number).IsEqualTo(expected);
    }
}
