// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net.Sockets;

namespace IoT.DriverCore.S7PlcRx.Tests.Testing;

/// <summary>Composes socket operations that retain .NET Framework-compatible overloads.</summary>
internal static class NetworkCompatibility
{
    /// <summary>Writes an entire byte array to a network stream.</summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="buffer">The bytes to write.</param>
    /// <returns>A task representing the asynchronous write.</returns>
    internal static Task WriteAsync(NetworkStream stream, byte[] buffer)
    {
        Guard.NotNull(stream, nameof(stream));
        Guard.NotNull(buffer, nameof(buffer));
        return stream.WriteAsync(buffer, 0, buffer.Length);
    }

    /// <summary>Reads into an entire byte array from a network stream.</summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="buffer">The destination buffer.</param>
    /// <returns>The number of bytes read.</returns>
    internal static Task<int> ReadAsync(NetworkStream stream, byte[] buffer)
    {
        Guard.NotNull(stream, nameof(stream));
        Guard.NotNull(buffer, nameof(buffer));
        return stream.ReadAsync(buffer, 0, buffer.Length);
    }

    /// <summary>Reads into one segment of a byte array from a network stream.</summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="buffer">The destination buffer.</param>
    /// <param name="offset">The destination offset.</param>
    /// <param name="count">The maximum number of bytes to read.</param>
    /// <returns>The number of bytes read.</returns>
    internal static Task<int> ReadAsync(
        NetworkStream stream,
        byte[] buffer,
        int offset,
        int count)
    {
        Guard.NotNull(stream, nameof(stream));
        Guard.NotNull(buffer, nameof(buffer));
        return stream.ReadAsync(buffer, offset, count);
    }

    /// <summary>Creates a disposable lifetime that stops a TCP listener.</summary>
    /// <param name="listener">The listener to stop during disposal.</param>
    /// <returns>The composed listener lifetime.</returns>
    internal static IDisposable StopOnDispose(TcpListener listener)
    {
        Guard.NotNull(listener, nameof(listener));
        return new ListenerLifetime(listener);
    }

    /// <summary>Owns the stop action for a TCP listener.</summary>
    private sealed class ListenerLifetime : IDisposable
    {
        /// <summary>Contains the listener until disposal.</summary>
        private TcpListener? _listener;

        /// <summary>Initializes a new instance of the <see cref="ListenerLifetime"/> class.</summary>
        /// <param name="listener">The owned listener.</param>
        public ListenerLifetime(TcpListener listener) => _listener = listener;

        /// <inheritdoc />
        public void Dispose() => Interlocked.Exchange(ref _listener, null)?.Stop();
    }
}
