// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Composes a logical tag, byte address, and typed codec for a simulator client.</summary>
public sealed class SimulatorTagBinding
{
    /// <summary>Decodes bytes to the logical value.</summary>
    private readonly Func<IReadOnlyList<byte>, object?> _decoder;

    /// <summary>Encodes the logical value to bytes.</summary>
    private readonly Func<object?, IReadOnlyList<byte>> _encoder;

    /// <summary>Initializes a new instance of the <see cref="SimulatorTagBinding"/> class.</summary>
    /// <param name="tag">The logical tag.</param>
    /// <param name="address">The byte address.</param>
    /// <param name="decoder">The untyped decoder.</param>
    /// <param name="encoder">The untyped encoder.</param>
    private SimulatorTagBinding(
        LogicalTag tag,
        TagTransportAddress address,
        Func<IReadOnlyList<byte>, object?> decoder,
        Func<object?, IReadOnlyList<byte>> encoder)
    {
        Tag = tag;
        Address = address;
        _decoder = decoder;
        _encoder = encoder;
    }

    /// <summary>Gets the logical tag definition.</summary>
    public LogicalTag Tag { get; }

    /// <summary>Gets the byte address used by the memory image and transfer planner.</summary>
    public TagTransportAddress Address { get; }

    /// <summary>Creates a strongly typed binding from caller-provided encoder and decoder functions.</summary>
    /// <typeparam name="T">The logical value type.</typeparam>
    /// <param name="tag">The logical tag definition.</param>
    /// <param name="address">The byte address; its access field is replaced for each operation.</param>
    /// <param name="decoder">The function that converts an exact byte snapshot to a logical value.</param>
    /// <param name="encoder">The function that converts a logical value to an exact byte snapshot.</param>
    /// <returns>The composed simulator binding.</returns>
    public static SimulatorTagBinding Create<T>(
        LogicalTag tag,
        TagTransportAddress address,
        Func<IReadOnlyList<byte>, T> decoder,
        Func<T, IReadOnlyList<byte>> encoder)
    {
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (decoder is null)
        {
            throw new ArgumentNullException(nameof(decoder));
        }

        if (encoder is null)
        {
            throw new ArgumentNullException(nameof(encoder));
        }

        return new(
            tag,
            address,
            bytes => decoder(bytes),
            value =>
            {
                if (value is T typed)
                {
                    return encoder(typed);
                }

                if (value is null && default(T) is null)
                {
                    return encoder((T)value!);
                }

                throw new InvalidCastException(
                    $"Tag '{tag.Name}' requires values assignable to {typeof(T).FullName}.");
            });
    }

    /// <summary>Decodes a byte snapshot to a logical value.</summary>
    /// <param name="bytes">The byte snapshot.</param>
    /// <returns>The logical value.</returns>
    internal object? Decode(IReadOnlyList<byte> bytes) => _decoder(bytes);

    /// <summary>Encodes a logical value to a byte snapshot.</summary>
    /// <param name="value">The logical value.</param>
    /// <returns>The encoded byte snapshot.</returns>
    internal IReadOnlyList<byte> Encode(object? value) => _encoder(value);

    /// <summary>Copies the binding address with the requested operation access.</summary>
    /// <param name="access">The operation access.</param>
    /// <returns>The operation-specific address.</returns>
    internal TagTransportAddress GetAddress(TagTransferAccess access) =>
        new(
            Address.TransportPartition,
            Address.MemoryArea,
            Address.Encoding,
            access,
            Address.Route,
            Address.Offset,
            Address.Length);
}
