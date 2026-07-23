// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace IoT.DriverCore.ABPlcRx.Reactive;
#else
namespace IoT.DriverCore.ABPlcRx;
#endif

/// <summary>One deterministic simulator operation record.</summary>
public sealed class ABPlcSimulatorLogEntry
{
    /// <summary>Initializes a new instance of the <see cref="ABPlcSimulatorLogEntry"/> class.</summary>
    /// <param name="sequence">The monotonic operation sequence.</param>
    /// <param name="timestamp">The timestamp supplied by the simulator time provider.</param>
    /// <param name="operation">The operation that ran.</param>
    /// <param name="tagName">The physical PLC tag name, when known.</param>
    /// <param name="handle">The native-style handle.</param>
    /// <param name="statusCode">The resulting libplctag-compatible status.</param>
    internal ABPlcSimulatorLogEntry(
        long sequence,
        DateTimeOffset timestamp,
        ABPlcSimulatorOperation operation,
        string? tagName,
        int handle,
        int statusCode)
    {
        Sequence = sequence;
        Timestamp = timestamp;
        Operation = operation;
        TagName = tagName;
        Handle = handle;
        StatusCode = statusCode;
    }

    /// <summary>Gets the monotonic operation sequence.</summary>
    public long Sequence { get; }

    /// <summary>Gets the operation timestamp.</summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>Gets the operation.</summary>
    public ABPlcSimulatorOperation Operation { get; }

    /// <summary>Gets the physical PLC tag name, when known.</summary>
    public string? TagName { get; }

    /// <summary>Gets the native-style handle.</summary>
    public int Handle { get; }

    /// <summary>Gets the resulting libplctag-compatible status.</summary>
    public int StatusCode { get; }

    /// <inheritdoc/>
    public override string ToString() =>
        $"{Sequence}: {Operation} {TagName ?? "<none>"} ({Handle}) => {StatusCode}";
}
