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

/// <summary>An alarm notification containing PLC metadata.</summary>
public sealed class S7SymbolicAlarm
{
    /// <summary>Initializes a new instance of the <see cref="S7SymbolicAlarm"/> class.</summary>
    /// <param name="timestamp">The timestamp.</param>
    /// <param name="sequence">The sequence.</param>
    /// <param name="metadata">The metadata.</param>
    internal S7SymbolicAlarm(DateTimeOffset? timestamp, uint sequence, S7SymbolicAlarmMetadata metadata)
    {
        Timestamp = timestamp;
        Sequence = sequence;
        Metadata = metadata;
    }

    /// <summary>Gets the notification timestamp when present.</summary>
    /// <value>The stored value.</value>
    public DateTimeOffset? Timestamp { get; }

    /// <summary>Gets the notification sequence.</summary>
    /// <value>The stored value.</value>
    public uint Sequence { get; }

    /// <summary>Gets the alarm attributes including state, texts and associated values.</summary>
    /// <value>The stored value.</value>
    public S7SymbolicAlarmMetadata Metadata { get; }
}
