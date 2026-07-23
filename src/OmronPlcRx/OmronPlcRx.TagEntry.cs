// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
#if REACTIVE_SHIM
using IoT.DriverCore.OmronPlcRx.Reactive.Core;
using IoT.DriverCore.OmronPlcRx.Reactive.Tags;
#else
using IoT.DriverCore.OmronPlcRx.Core;
using IoT.DriverCore.OmronPlcRx.Tags;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive;
#else
namespace IoT.DriverCore.OmronPlcRx;
#endif

/// <summary>Contains typed tag-entry polling behavior.</summary>
public sealed partial class OmronPlcRx
{
    /// <summary>Tag entry abstraction used internally for polymorphic read access.</summary>
    private interface ITagEntry
    {
        /// <summary>Gets the last cached value as a boxed object.</summary>
        IPlcTag? Tag { get; }

        /// <summary>Reads the tag value from the PLC updating internal state.</summary>
        /// <param name="plc">PLC connection.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if the value changed; otherwise false.</returns>
        Task<bool> ReadAsync(OmronPLCConnection plc, CancellationToken ct);

        /// <summary>Updates the cached tag value from a grouped operation.</summary>
        /// <param name="value">New boxed value.</param>
        /// <returns>True when the value changed; otherwise false.</returns>
        bool UpdateValue(object? value);
    }

    /// <summary>Represents a typed tag entry.</summary>
    /// <typeparam name="T">Tag value type.</typeparam>
    /// <param name="tag">Typed tag definition.</param>
    private sealed class TagEntry<T>(PlcTag<T> tag) : ITagEntry
    {
        /// <summary>Gets the tag value.</summary>
        public IPlcTag Tag { get; } = tag;

        /// <inheritdoc />
        public async Task<bool> ReadAsync(OmronPLCConnection plc, CancellationToken ct)
        {
            var newValue = await ReadValueAsync(plc, ct).ConfigureAwait(false);
            return UpdateValue(newValue);
        }

        /// <summary>Updates the cached tag value.</summary>
        /// <param name="value">The new value.</param>
        /// <returns>True when the cached value changed.</returns>
        public bool UpdateValue(object? value)
        {
            if (Equals(value, Tag.Value) || Tag is not PlcTag<T> plcTag)
            {
                return false;
            }

            plcTag.Value = value is null ? default : (T)value;
            return true;
        }

        /// <summary>Reads a value from the PLC.</summary>
        /// <param name="plc">The PLC connection.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>The read value.</returns>
        private async Task<object> ReadValueAsync(OmronPLCConnection plc, CancellationToken ct)
        {
            if (typeof(T) == typeof(string))
            {
                return await ReadStringValueAsync(plc, ct).ConfigureAwait(false);
            }

            var (area, address, bitIndex) = ParseAddress(Tag.Address);
            if (typeof(T) == typeof(bool))
            {
                return await PlcTagValueCodec
                    .ReadBooleanValueAsync(
                        plc,
                        ToWordType(area),
                        ToBitType(area),
                        address,
                        bitIndex,
                        ct)
                    .ConfigureAwait(false);
            }

            var wordCount = PlcTagValueCodec.GetReadWordCount(typeof(T));
            if (wordCount == 0)
            {
                throw new NotSupportedException($"Tag type '{nameof(T)}' not supported.");
            }

            var words = await plc
                .ReadWordsAsync(address, (ushort)wordCount, ToWordType(area), ct)
                .ConfigureAwait(false);
            return PlcTagValueCodec.ConvertReadWords(typeof(T), words.Values);
        }

        /// <summary>Reads a string value from the PLC.</summary>
        /// <param name="plc">The PLC connection.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>The string value.</returns>
        private async Task<object> ReadStringValueAsync(
            OmronPLCConnection plc,
            CancellationToken ct)
        {
            var (baseAddress, length) = ExtractStringMeta(Tag.Address);
            var (area, address, bitIndex) = ParseAddress(baseAddress);
            PlcTagValueCodec.ThrowIfBitIndexedString(bitIndex);
            var wordCount = (length + 1) / ProtocolConstants.Two;
            var words = await plc
                .ReadWordsAsync(address, (ushort)wordCount, ToWordType(area), ct)
                .ConfigureAwait(false);
            return PlcTagValueCodec.GetStringFromWords(words.Values, length, wordCount);
        }
    }
}
