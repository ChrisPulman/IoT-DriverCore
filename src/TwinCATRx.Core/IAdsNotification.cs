// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using TwinCAT.Ads;

#if REACTIVE_SHIM
namespace IoT.Driver.TwinCATRx.Core.Reactive;
#else
namespace IoT.Driver.TwinCATRx.Core;
#endif

/// <summary>Configures a PLC notification delivered by ADS.</summary>
public interface IAdsNotification : INotification
{
    /// <summary>Gets the ADS transmission mode.</summary>
    AdsTransMode AdsTransMode { get; }

    /// <summary>Gets the notification cycle time in milliseconds.</summary>
    int CycleTime { get; }

    /// <summary>Gets the maximum delivery delay in milliseconds.</summary>
    int MaxDelay { get; }
}
