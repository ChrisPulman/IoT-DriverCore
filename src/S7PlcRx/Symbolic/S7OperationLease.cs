// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
namespace IoT.Driver.S7PlcRx.Symbolic;
#endif

/// <summary>Returns an acquired operation permit exactly once.</summary>
internal sealed class S7OperationLease : IDisposable
{
    /// <summary>The gate whose permit is borrowed by this lease.</summary>
    private S7OperationGate? _gate;

    /// <summary>Initializes a new instance of the <see cref="S7OperationLease"/> class.</summary>
    /// <param name="gate">The gate owning the borrowed permit.</param>
    internal S7OperationLease(S7OperationGate gate) => _gate = gate;

    /// <summary>Returns the borrowed permit if this lease has not already been released.</summary>
    internal void Dispose() => Interlocked.Exchange(ref _gate, null)?.ReleaseAcquired();

    /// <summary>Returns the borrowed operation permit.</summary>
    void IDisposable.Dispose() => Dispose();
}
