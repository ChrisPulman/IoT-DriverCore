// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
#if REACTIVE_SHIM
using IoT.Driver.S7PlcRx.Reactive.Symbolic.Protocol;
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
using IoT.Driver.S7PlcRx.Symbolic.Protocol;

namespace IoT.Driver.S7PlcRx.Symbolic;
#endif

/// <summary>Owns a PLC variable subscription. Dispose it to release PLC resources.</summary>
public sealed class S7SymbolicSubscription : IAsyncDisposable
{
    /// <summary>The stream.</summary>
    private readonly S7SymbolicSubscriptionStream<S7SymbolicChange> _stream;

    /// <summary>Initializes a new instance of the <see cref="S7SymbolicSubscription"/> class.</summary>
    /// <param name="stream">The stream.</param>
    internal S7SymbolicSubscription(S7SymbolicSubscriptionStream<S7SymbolicChange> stream) => _stream = stream;

    /// <summary>Gets changes including individual PLC access errors.</summary>
    /// <value>The stored value.</value>
    public IAsyncEnumerable<S7SymbolicChange> Changes => _stream.ReadAll();

    /// <summary>Deletes the PLC subscription.</summary>
    /// <returns>The operation result.</returns>
    public ValueTask DisposeAsync() => _stream.DisposeAsync();
}
