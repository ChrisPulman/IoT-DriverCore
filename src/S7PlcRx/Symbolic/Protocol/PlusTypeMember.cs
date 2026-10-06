// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic.Protocol;
#else
namespace IoT.Driver.S7PlcRx.Symbolic.Protocol;
#endif

/// <summary>Describes a decoded symbolic type member.</summary>
internal sealed class PlusTypeMember
{
    /// <summary>The bias used to round a Boolean row up to a whole byte.</summary>
    private const int BooleanPaddingBias = 7;

    /// <summary>The number of Boolean bits in a byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>The packed Boolean PLC soft datatype.</summary>
    private const int PackedBooleanSoftType = 40;

    /// <summary>Gets or sets the local identifier.</summary>
    internal uint LocalId { get; set; }

    /// <summary>Gets or sets the symbol checksum.</summary>
    internal uint Crc { get; set; }

    /// <summary>Gets or sets the soft datatype.</summary>
    internal uint SoftType { get; set; }

    /// <summary>Gets or sets the type flags.</summary>
    internal ushort Flags { get; set; }

    /// <summary>Gets or sets the related type identifier.</summary>
    internal uint Relation { get; set; }

    /// <summary>Gets or sets the maximum string length.</summary>
    internal int MaximumStringLength { get; set; }

    /// <summary>Gets the array lower bounds.</summary>
    internal int[] LowerBounds { get; private set; } = [];

    /// <summary>Gets the array lengths.</summary>
    internal uint[] Lengths { get; private set; } = [];

    /// <summary>Gets or sets the symbol name.</summary>
    internal string Name { get; set; } = string.Empty;

    /// <summary>Sets the decoded array dimensions.</summary>
    /// <param name="lowerBounds">The array lower bounds.</param>
    /// <param name="lengths">The array lengths.</param>
    internal void SetDimensions(int[] lowerBounds, uint[] lengths)
    {
        LowerBounds = lowerBounds;
        Lengths = lengths;
    }

    /// <summary>Flattens array coordinates into a native element offset.</summary>
    /// <param name="indices">Array coordinates in path order.</param>
    /// <returns>The native element offset.</returns>
    internal uint Flatten(int[] indices)
    {
        if (indices.Length != Lengths.Length)
        {
            throw new ArgumentException("Array rank does not match the symbol.", nameof(indices));
        }

        ulong offset = 0;
        ulong stride = 1;
        for (var d = indices.Length - 1; d >= 0; d--)
        {
            var coordinate = (long)indices[d] - LowerBounds[d];
            if (coordinate < 0 || (ulong)coordinate >= Lengths[d])
            {
                throw new ArgumentOutOfRangeException(nameof(indices));
            }

            offset = checked(offset + ((ulong)coordinate * stride));
            var width = (ulong)Lengths[d];
            if (d == indices.Length - 1 && (SoftType is 1 or PackedBooleanSoftType) && indices.Length > 1)
            {
                width = checked(((width + BooleanPaddingBias) / BitsPerByte) * BitsPerByte);
            }

            stride = checked(stride * width);
        }

        return checked((uint)offset);
    }
}
