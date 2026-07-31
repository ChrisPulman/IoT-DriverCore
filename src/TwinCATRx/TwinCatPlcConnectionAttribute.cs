// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.TwinCATRx.Reactive;
#else
namespace IoT.Driver.TwinCATRx;
#endif

/// <summary>Generates a TwinCAT PLC connection binding.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class TwinCatPlcConnectionAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="TwinCatPlcConnectionAttribute"/> class.</summary>
    /// <param name="adsAddress">The ADS address.</param>
    /// <param name="port">The ADS port.</param>
    public TwinCatPlcConnectionAttribute(string adsAddress, int port)
    {
        AdsAddress = adsAddress;
        Port = port;
    }

    /// <summary>Gets the ADS address.</summary>
    public string AdsAddress { get; }

    /// <summary>Gets the ADS port.</summary>
    public int Port { get; }

    /// <summary>Gets or sets the optional settings identifier.</summary>
    public string? SettingsId { get; set; }
}
