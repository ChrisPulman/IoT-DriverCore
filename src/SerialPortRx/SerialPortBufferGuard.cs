// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.Serial.Reactive;
#else
namespace IoT.DriverCore.Serial;
#endif

/// <summary>Validates serial buffer segments.</summary>
internal static class SerialPortBufferGuard
{
    /// <summary>Validates array segment arguments.</summary>
    /// <param name="length">The array length.</param>
    /// <param name="offset">The requested offset.</param>
    /// <param name="count">The requested count.</param>
    internal static void Validate(int length, int offset, int count)
    {
        if (offset < 0 || offset > length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (count >= 0 && count <= length - offset)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(count));
    }
}
