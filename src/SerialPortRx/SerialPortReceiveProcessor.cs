// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace CP.IO.Ports.Reactive;
#else
namespace CP.IO.Ports;
#endif

/// <summary>Reads a raw serial receive batch and publishes its byte and character views.</summary>
internal static class SerialPortReceiveProcessor
{
    /// <summary>Reads the available bytes and publishes an immutable copy of the received batch.</summary>
    /// <param name="availableBytes">The number of bytes reported as available by the serial port.</param>
    /// <param name="buffer">The reusable receive buffer.</param>
    /// <param name="read">The raw-byte read operation.</param>
    /// <param name="publishByte">The byte publication action.</param>
    /// <param name="publishCharacter">The character publication action.</param>
    /// <param name="publishBatch">The batch publication action.</param>
    /// <returns>The number of bytes read.</returns>
    internal static int ReadAndPublish(
        int availableBytes,
        byte[] buffer,
        Func<byte[], int, int, int> read,
        Action<byte> publishByte,
        Action<char> publishCharacter,
        Action<byte[]> publishBatch)
    {
        var requestedByteCount = Math.Min(availableBytes, buffer.Length);
        if (requestedByteCount <= 0)
        {
            return 0;
        }

        var bytesRead = read(buffer, 0, requestedByteCount);
        if (bytesRead < 0 || bytesRead > requestedByteCount)
        {
            throw new InvalidOperationException("The serial port returned an invalid byte count.");
        }

        var batch = new byte[bytesRead];
        for (var i = 0; i < bytesRead; i++)
        {
            var value = buffer[i];
            batch[i] = value;
            publishByte(value);
            publishCharacter((char)value);
        }

        if (bytesRead > 0)
        {
            publishBatch(batch);
        }

        return bytesRead;
    }

    /// <summary>Drains all currently available raw bytes in buffer-sized batches.</summary>
    /// <param name="getAvailableBytes">Gets the number of bytes currently available from the transport.</param>
    /// <param name="buffer">The reusable receive buffer.</param>
    /// <param name="read">The raw-byte read operation.</param>
    /// <param name="publishByte">The byte publication action.</param>
    /// <param name="publishCharacter">The character publication action.</param>
    /// <param name="publishBatch">The batch publication action.</param>
    /// <returns>The total number of bytes read.</returns>
    internal static int DrainAndPublish(
        Func<int> getAvailableBytes,
        byte[] buffer,
        Func<byte[], int, int, int> read,
        Action<byte> publishByte,
        Action<char> publishCharacter,
        Action<byte[]> publishBatch)
    {
        var totalBytesRead = 0;
        while (getAvailableBytes() > 0)
        {
            var bytesRead = ReadAndPublish(
                getAvailableBytes(),
                buffer,
                read,
                publishByte,
                publishCharacter,
                publishBatch);
            totalBytesRead += bytesRead;

            if (bytesRead == 0)
            {
                break;
            }
        }

        return totalBytesRead;
    }
}
