// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.Serial.Reactive;
#else
namespace IoT.DriverCore.Serial;
#endif

/// <summary>
/// Owns two deterministic, connected <see cref="SerialPortRx"/> instances that exercise the normal serial wrapper
/// without requiring physical or virtual serial hardware.
/// </summary>
public sealed class InMemoryPortRxPair : IDisposable
{
    /// <summary>The shared duplex byte link.</summary>
    private readonly InMemorySerialLink _link = new();

    /// <summary>Tracks whether the pair has been disposed.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="InMemoryPortRxPair"/> class.</summary>
    public InMemoryPortRxPair()
        : this("MEMORY1", "MEMORY2")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InMemoryPortRxPair"/> class.</summary>
    /// <param name="firstPortName">The diagnostic name of the first endpoint.</param>
    /// <param name="secondPortName">The diagnostic name of the second endpoint.</param>
    public InMemoryPortRxPair(string firstPortName, string secondPortName)
    {
        First = new(
            owner => new InMemorySerialPortConnection(_link, 0, owner.Encoding, owner.NewLine, owner.ReadTimeout))
        {
            PortName = firstPortName,
        };
        Second = new(
            owner => new InMemorySerialPortConnection(_link, 1, owner.Encoding, owner.NewLine, owner.ReadTimeout))
        {
            PortName = secondPortName,
        };
    }

    /// <summary>Gets the first connected serial endpoint.</summary>
    public SerialPortRx First { get; }

    /// <summary>Gets the second connected serial endpoint.</summary>
    public SerialPortRx Second { get; }

    /// <summary>Injects a deterministic connection error into the first endpoint.</summary>
    /// <param name="exception">The error to publish.</param>
    public void InjectFirstError(Exception exception)
    {
        ArgumentGuard.ThrowIfNull(exception, nameof(exception));
        _link.InjectError(0, exception);
    }

    /// <summary>Injects a deterministic connection error into the second endpoint.</summary>
    /// <param name="exception">The error to publish.</param>
    public void InjectSecondError(Exception exception)
    {
        ArgumentGuard.ThrowIfNull(exception, nameof(exception));
        _link.InjectError(1, exception);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        First.Dispose();
        Second.Dispose();
        _disposed = true;
    }
}
