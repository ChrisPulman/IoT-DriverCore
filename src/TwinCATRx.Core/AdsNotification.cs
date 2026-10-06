// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using TwinCAT.Ads;

#if REACTIVE_SHIM
namespace IoT.Driver.TwinCATRx.Core.Reactive;
#else
namespace IoT.Driver.TwinCATRx.Core;
#endif

/// <summary>Stores the settings for a native ADS notification.</summary>
/// <param name="variable">The PLC variable name.</param>
/// <param name="adsTransMode">The ADS transmission mode.</param>
/// <param name="cycleTime">The cycle time in milliseconds.</param>
/// <param name="maxDelay">The maximum delay in milliseconds.</param>
[Serializable]
internal sealed class AdsNotification(string variable, AdsTransMode adsTransMode, int cycleTime, int maxDelay) : IAdsNotification
{
    /// <inheritdoc/>
    public string Variable { get; } = variable;

    /// <inheritdoc/>
    public AdsTransMode AdsTransMode { get; } = adsTransMode;

    /// <inheritdoc/>
    public int CycleTime { get; } = cycleTime;

    /// <inheritdoc/>
    public int MaxDelay { get; } = maxDelay;

    /// <inheritdoc/>
    public int UpdateRate => CycleTime;

    /// <inheritdoc/>
    public int ArraySize => -1;
}
