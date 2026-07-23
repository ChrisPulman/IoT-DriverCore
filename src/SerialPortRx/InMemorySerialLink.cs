// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.Serial.Reactive;
#else
namespace IoT.DriverCore.Serial;
#endif

/// <summary>Provides a deterministic two-ended byte link for in-memory serial connections.</summary>
internal sealed class InMemorySerialLink
{
    /// <summary>Synchronizes active endpoint access.</summary>
    private readonly object _sync = new();

    /// <summary>The currently open endpoints.</summary>
    private readonly InMemorySerialPortConnection?[] _endpoints = new InMemorySerialPortConnection?[2];

    /// <summary>Delivers one immutable byte batch to the opposite endpoint.</summary>
    /// <param name="sourceSide">The source side.</param>
    /// <param name="batch">The byte batch.</param>
    internal void Deliver(int sourceSide, byte[] batch)
    {
        InMemorySerialPortConnection? target;
        lock (_sync)
        {
            target = _endpoints[1 - sourceSide];
        }

        target?.Receive(batch);
    }

    /// <summary>Gets whether the opposite endpoint is currently open.</summary>
    /// <param name="sourceSide">The source side.</param>
    /// <returns><see langword="true"/> when the peer is open.</returns>
    internal bool IsPeerOpen(int sourceSide)
    {
        lock (_sync)
        {
            return _endpoints[1 - sourceSide]?.IsOpen == true;
        }
    }

    /// <summary>Publishes a deterministic error to an endpoint.</summary>
    /// <param name="side">The destination side.</param>
    /// <param name="exception">The error to publish.</param>
    internal void InjectError(int side, Exception exception)
    {
        InMemorySerialPortConnection? target;
        lock (_sync)
        {
            target = _endpoints[side];
        }

        target?.InjectError(exception);
    }

    /// <summary>Registers an opened endpoint.</summary>
    /// <param name="side">The endpoint side.</param>
    /// <param name="connection">The connection to register.</param>
    internal void Register(int side, InMemorySerialPortConnection connection)
    {
        lock (_sync)
        {
            if (_endpoints[side]?.IsOpen == true)
            {
                throw new InvalidOperationException("The in-memory endpoint is already open.");
            }

            _endpoints[side] = connection;
        }
    }

    /// <summary>Unregisters a closed endpoint.</summary>
    /// <param name="side">The endpoint side.</param>
    /// <param name="connection">The connection to unregister.</param>
    internal void Unregister(int side, InMemorySerialPortConnection connection)
    {
        lock (_sync)
        {
            if (ReferenceEquals(_endpoints[side], connection))
            {
                _endpoints[side] = null;
            }
        }
    }
}
