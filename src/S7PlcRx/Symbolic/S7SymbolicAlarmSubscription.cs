// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
namespace IoT.Driver.S7PlcRx.Symbolic;
#endif

/// <summary>Owns a PLC alarm subscription.</summary>
public sealed class S7SymbolicAlarmSubscription : IAsyncDisposable
{
    /// <summary>The stream.</summary>
    private readonly S7SymbolicSubscriptionStream<S7SymbolicAlarm> _stream;

    /// <summary>Initializes a new instance of the <see cref="S7SymbolicAlarmSubscription"/> class.</summary>
    /// <param name="stream">The stream.</param>
    internal S7SymbolicAlarmSubscription(S7SymbolicSubscriptionStream<S7SymbolicAlarm> stream) => _stream = stream;

    /// <summary>Gets the stream of PLC alarms.</summary>
    /// <value>The stored value.</value>
    public IAsyncEnumerable<S7SymbolicAlarm> Alarms => _stream.ReadAll();

    /// <summary>Deletes the PLC alarm subscription.</summary>
    /// <returns>The operation result.</returns>
    public ValueTask DisposeAsync() => _stream.DisposeAsync();
}
