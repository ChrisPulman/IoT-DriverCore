// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Core.Channels;
#else
namespace IoT.DriverCore.OmronPlcRx.Core.Channels;
#endif

/// <summary>Provides Toolbus serial frame buffer helpers.</summary>
internal static class SerialToolbusFrameBuffer
{
    /// <summary>Removes leading noise before a Toolbus frame start marker.</summary>
    /// <param name="received">The accumulated received bytes.</param>
    internal static void TrimBeforeFrameStart(List<byte> received)
    {
        var frameStart = received.IndexOf(0xAB);
        if (frameStart < 0)
        {
            received.Clear();
            return;
        }

        if (frameStart == 0)
        {
            return;
        }

        received.RemoveRange(0, frameStart);
    }

    /// <summary>Checks whether the synchronization frame exists in the received bytes.</summary>
    /// <param name="received">The accumulated received bytes.</param>
    /// <param name="sync">The synchronization frame.</param>
    /// <returns>A value indicating whether the synchronization frame was found.</returns>
    internal static bool ContainsSynchronizationFrame(List<byte> received, byte[] sync)
    {
        for (var i = 0; i <= received.Count - sync.Length; i++)
        {
            if (received[i] != sync[0] || received[i + 1] != sync[1])
            {
                continue;
            }

            return true;
        }

        return false;
    }
}
