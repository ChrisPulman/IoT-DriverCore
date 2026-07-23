// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using IoT.DriverCore.OmronPlcRx.Core.Channels;

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>Provides a deterministic in-memory serial port for Omron channel tests.</summary>
internal sealed class ScriptedOmronSerialPort : IOmronSerialPort
{
    /// <summary>Stores unread response bytes.</summary>
    private readonly ConcurrentQueue<byte> _received = new();

    /// <summary>Creates response bytes for each write.</summary>
    private readonly Func<byte[], byte[]> _responseFactory;

    /// <summary>Initializes a new instance of the <see cref="ScriptedOmronSerialPort"/> class.</summary>
    /// <param name="responseFactory">Response factory invoked for every write.</param>
    internal ScriptedOmronSerialPort(Func<byte[], byte[]> responseFactory) =>
        _responseFactory = responseFactory ?? throw new ArgumentNullException(nameof(responseFactory));

    /// <inheritdoc />
    public int BytesToRead => _received.Count;

    /// <inheritdoc />
    public bool RtsEnable { get; set; }

    /// <inheritdoc />
    public bool DtrEnable { get; set; }

    /// <summary>Gets a value indicating whether the port was opened.</summary>
    internal bool WasOpened { get; private set; }

    /// <summary>Gets a value indicating whether the port was closed.</summary>
    internal bool WasClosed { get; private set; }

    /// <summary>Gets the write calls observed by the port.</summary>
    internal List<byte[]> Writes { get; } = [];

    /// <inheritdoc />
    public Task OpenAsync()
    {
        WasOpened = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Close() => WasClosed = true;

    /// <inheritdoc />
    public void DiscardInBuffer()
    {
        while (_received.TryDequeue(out var discarded))
        {
            _ = discarded;
        }
    }

    /// <inheritdoc />
    public void Write(byte[] buffer, int offset, int count)
    {
        var request = new byte[count];
        Array.Copy(buffer, offset, request, 0, count);
        Writes.Add(request);
        foreach (var value in _responseFactory(request))
        {
            _received.Enqueue(value);
        }
    }

    /// <inheritdoc />
    public int Read(byte[] buffer, int offset, int count)
    {
        var read = 0;
        while (read < count && _received.TryDequeue(out var value))
        {
            buffer[offset + read] = value;
            read++;
        }

        return read;
    }

    /// <inheritdoc />
    public void Dispose() => Close();
}
