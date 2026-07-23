// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using IoT.DriverCore.Core;
#if REACTIVE_SHIM
using IoT.DriverCore.OmronPlcRx.Reactive.Core.Types;
#else
using IoT.DriverCore.OmronPlcRx.Core.Types;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive;
#else
namespace IoT.DriverCore.OmronPlcRx;
#endif

/// <summary>Contains conversion and result helpers for grouped logical-tag operations.</summary>
public sealed partial class OmronLogicalTagClient
{
    /// <summary>Marks every still-pending batch member as failed.</summary>
    /// <param name="items">Batch items.</param>
    /// <param name="results">Caller-positioned result array.</param>
    /// <param name="error">Failure detail.</param>
    private static void ApplyBatchFailure(
        IReadOnlyList<OmronLogicalBatchItem> items,
        TagOperationResult<LogicalTagValue>?[] results,
        string error)
    {
        foreach (var item in items)
        {
            results[item.InputIndex] = TagOperationResult<LogicalTagValue>.Failure(error);
        }
    }

    /// <summary>Completes missing result positions defensively and returns a non-null array.</summary>
    /// <param name="results">Caller-positioned result array.</param>
    /// <returns>The completed result array.</returns>
    private static TagOperationResult<LogicalTagValue>[] CompleteResults(
        TagOperationResult<LogicalTagValue>?[] results) =>
        results.Select(
            static result => result
                ?? TagOperationResult<LogicalTagValue>.Failure(
                    "The grouped FINS operation did not return a result."))
            .ToArray();

    /// <summary>Gets the runtime tag type represented by a logical definition.</summary>
    /// <param name="tag">Logical tag definition.</param>
    /// <returns>The runtime tag type.</returns>
    private static Type GetValueType(LogicalTag tag) =>
        GetTypeKey(tag) switch
        {
            "BOOLEAN" or "BOOL" => typeof(bool),
            "BYTE" => typeof(byte),
            "INT16" or "SHORT" => typeof(short),
            "UINT16" or "USHORT" => typeof(ushort),
            "INT32" or "INT" => typeof(int),
            "UINT32" or "UINT" => typeof(uint),
            "SINGLE" or "FLOAT" => typeof(float),
            "DOUBLE" => typeof(double),
            "STRING" => typeof(string),
            "BCD16" => typeof(Bcd16),
            "BCDU16" => typeof(BcdU16),
            "BCD32" => typeof(Bcd32),
            "BCDU32" => typeof(BcdU32),
            _ => throw new NotSupportedException(
                $"Logical tag data type '{tag.DataType}' is not supported by OmronPlcRx."),
        };

    /// <summary>Converts one logical write value to its registered runtime type.</summary>
    /// <param name="tag">Logical tag definition.</param>
    /// <param name="value">Logical value.</param>
    /// <returns>The converted value.</returns>
    private static object? ConvertBatchValue(LogicalTag tag, object? value) =>
        GetTypeKey(tag) switch
        {
            "BOOLEAN" or "BOOL" => ConvertValue<bool>(value),
            "BYTE" => ConvertValue<byte>(value),
            "INT16" or "SHORT" => ConvertValue<short>(value),
            "UINT16" or "USHORT" => ConvertValue<ushort>(value),
            "INT32" or "INT" => ConvertValue<int>(value),
            "UINT32" or "UINT" => ConvertValue<uint>(value),
            "SINGLE" or "FLOAT" => ConvertValue<float>(value),
            "DOUBLE" => ConvertValue<double>(value),
            "STRING" => ConvertValue<string>(value),
            "BCD16" => ConvertValue<Bcd16>(value),
            "BCDU16" => ConvertValue<BcdU16>(value),
            "BCD32" => ConvertValue<Bcd32>(value),
            "BCDU32" => ConvertValue<BcdU32>(value),
            _ => throw new NotSupportedException(
                $"Logical tag data type '{tag.DataType}' is not supported by OmronPlcRx."),
        };
}
