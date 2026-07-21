// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using CP.IoT.Core;
using ReactiveUI.Primitives.Disposables;
#if REACTIVE_SHIM
using OmronPlcRx.Reactive.Enums;
using OmronPlcRx.Reactive.Results;
using OmronPlcRx.Reactive.Tags;
#else
using OmronPlcRx.Enums;
using OmronPlcRx.Results;
using OmronPlcRx.Tags;
#endif

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive;

#else
namespace OmronPlcRx;

#endif

/// <summary>Defines high-level Omron PLC operations and tag access.</summary>
public interface IOmronPlcRx : IsDisposed
{
    /// <summary>Gets an observable of all tag change events.</summary>
    IObservable<IPlcTag?> ObserveAll { get; }

    /// <summary>Gets an observable of operational errors.</summary>
    IObservable<OmronPLCException?> Errors { get; }

    /// <summary>Gets the detected PLC type.</summary>
    PlcType PlcType { get; }

    /// <summary>Gets the PLC controller model string.</summary>
    string? ControllerModel { get; }

    /// <summary>Gets the PLC controller version string.</summary>
    string? ControllerVersion { get; }

    /// <summary>Registers or updates a tag definition.</summary>
    /// <typeparam name="T">Tag value type.</typeparam>
    /// <param name="tag">Typed PLC tag definition.</param>
    void AddUpdateTagItem<T>(PlcTag<T> tag);

    /// <summary>Removes a registered tag definition.</summary>
    /// <param name="tagName">Logical tag name.</param>
    /// <returns><see langword="true"/> when a tag was removed.</returns>
    bool RemoveTagItem(string tagName);

    /// <summary>Observes a tag value stream.</summary>
    /// <typeparam name="T">Tag type.</typeparam>
    /// <param name="tag">Registered typed tag.</param>
    /// <returns>Observable sequence of values.</returns>
    IObservable<T?> Observe<T>(LogicalTagKey<T> tag);

    /// <summary>Gets last cached value for a tag.</summary>
    /// <typeparam name="T">Tag type.</typeparam>
    /// <param name="tag">Registered typed tag.</param>
    /// <returns>Cached value or default.</returns>
    T? GetValue<T>(LogicalTagKey<T> tag);

    /// <summary>Reads a registered tag directly from the PLC and updates its cached value.</summary>
    /// <typeparam name="T">Tag type.</typeparam>
    /// <param name="tag">Registered typed tag.</param>
    /// <param name="value">Value to write.</param>
    void SetValue<T>(LogicalTagKey<T> tag, T? value);

    /// <summary>Reads a registered tag directly from the PLC and updates its cached value.</summary>
    /// <typeparam name="T">Tag type.</typeparam>
    /// <param name="tag">Registered typed tag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The value read from the PLC.</returns>
    Task<T?> ReadValueAsync<T>(
        LogicalTagKey<T> tag,
        CancellationToken cancellationToken);

    /// <summary>Writes a registered tag and awaits the PLC operation.</summary>
    /// <typeparam name="T">Tag type.</typeparam>
    /// <param name="tag">Registered typed tag.</param>
    /// <param name="value">Value to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the PLC write completes.</returns>
    Task WriteValueAsync<T>(
        LogicalTagKey<T> tag,
        T? value,
        CancellationToken cancellationToken);

    /// <summary>Reads the PLC real-time clock.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Clock read result.</returns>
    Task<ReadClockResult> ReadClockAsync(CancellationToken cancellationToken);

    /// <summary>Writes the PLC real-time clock (day-of-week inferred from date).</summary>
    /// <param name="newDateTime">New date/time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Clock write result.</returns>
    Task<WriteClockResult> WriteClockAsync(
        DateTimeOffset newDateTime,
        CancellationToken cancellationToken);

    /// <summary>Writes the PLC real-time clock with explicit day-of-week.</summary>
    /// <param name="newDateTime">New date/time.</param>
    /// <param name="newDayOfWeek">Day of week (0-6).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Clock write result.</returns>
    Task<WriteClockResult> WriteClockAsync(
        DateTimeOffset newDateTime,
        int newDayOfWeek,
        CancellationToken cancellationToken);

    /// <summary>Reads PLC scan cycle time statistics.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cycle time statistics.</returns>
    Task<ReadCycleTimeResult> ReadCycleTimeAsync(CancellationToken cancellationToken);
}
