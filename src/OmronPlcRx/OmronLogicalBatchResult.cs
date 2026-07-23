// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive;
#else
namespace IoT.DriverCore.OmronPlcRx;
#endif

/// <summary>Represents one indexed result returned from grouped FINS operations.</summary>
/// <param name="inputIndex">Original caller position.</param>
/// <param name="succeeded">Whether the individual item succeeded.</param>
/// <param name="value">Read or written value when successful.</param>
/// <param name="error">Failure detail when unsuccessful.</param>
internal sealed class OmronLogicalBatchResult(
    int inputIndex,
    bool succeeded,
    object? value,
    string? error)
{
    /// <summary>Gets the original caller position.</summary>
    internal int InputIndex { get; } = inputIndex;

    /// <summary>Gets a value indicating whether the individual item succeeded.</summary>
    internal bool Succeeded { get; } = succeeded;

    /// <summary>Gets the read or written value.</summary>
    internal object? Value { get; } = value;

    /// <summary>Gets the failure detail.</summary>
    internal string? Error { get; } = error;

    /// <summary>Creates a successful indexed result.</summary>
    /// <param name="inputIndex">Original caller position.</param>
    /// <param name="value">Read or written value.</param>
    /// <returns>The successful result.</returns>
    internal static OmronLogicalBatchResult Success(int inputIndex, object? value) =>
        new(inputIndex, true, value, null);

    /// <summary>Creates a failed indexed result.</summary>
    /// <param name="inputIndex">Original caller position.</param>
    /// <param name="error">Failure detail.</param>
    /// <returns>The failed result.</returns>
    internal static OmronLogicalBatchResult Failure(int inputIndex, string error) =>
        new(inputIndex, false, null, error);
}
