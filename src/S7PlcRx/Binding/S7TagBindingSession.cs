// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.S7PlcRx.Reactive.Binding;

#else
namespace IoT.DriverCore.S7PlcRx.Binding;

#endif

/// <summary>Owns the runtime and common logical clients created for one generated binding session.</summary>
public sealed class S7TagBindingSession : IDisposable
{
    /// <summary>Contains the runtime binding until disposal.</summary>
    private IDisposable? _runtimeBinding;

    /// <summary>Contains the logical client until disposal.</summary>
    private IDisposable? _logicalClient;

    /// <summary>Initializes a new instance of the <see cref="S7TagBindingSession"/> class.</summary>
    /// <param name="runtimeBinding">The runtime binding.</param>
    /// <param name="logicalClient">The common logical client.</param>
    public S7TagBindingSession(IDisposable runtimeBinding, IDisposable logicalClient)
    {
        _runtimeBinding = runtimeBinding ?? throw new ArgumentNullException(nameof(runtimeBinding));
        _logicalClient = logicalClient ?? throw new ArgumentNullException(nameof(logicalClient));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Interlocked.Exchange(ref _runtimeBinding, null)?.Dispose();
        Interlocked.Exchange(ref _logicalClient, null)?.Dispose();
    }
}
