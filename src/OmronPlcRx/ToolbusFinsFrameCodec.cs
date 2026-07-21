// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive;
#else
namespace OmronPlcRx;
#endif

/// <summary>Encodes and decodes Omron Toolbus serial frames carrying binary FINS messages.</summary>
public static class ToolbusFinsFrameCodec
{
    /// <summary>Stores the m in im um fi ns re qu es tl en gt h value.</summary>
    private const int MinimumFinsRequestLength = 12;

    /// <summary>Stores the m in im um fi ns re sp on se le ng th value.</summary>
    private const int MinimumFinsResponseLength = 14;

    /// <summary>Gets the synchronization frame value.</summary>
    public static ReadOnlyMemory<byte> SynchronizationFrame => new byte[] { 0xAC, 0x01 };

    /// <summary>Encodes a binary FINS message into a Toolbus frame.</summary>
    /// <param name="finsMessage">Binary FINS request message.</param>
    /// <returns>Toolbus frame: 0xAB + length + FINS payload + checksum.</returns>
    public static Memory<byte> EncodeRequest(ReadOnlyMemory<byte> finsMessage)
    {
        if (finsMessage.Length < MinimumFinsRequestLength)
        {
            throw new ArgumentException("The FINS request is too short.", nameof(finsMessage));
        }

        if (finsMessage.Length > ushort.MaxValue - ProtocolConstants.Two)
        {
            throw new ArgumentOutOfRangeException(
                nameof(finsMessage),
                "The FINS message is too long for a Toolbus frame.");
        }

        var frameLength = finsMessage.Length + ProtocolConstants.Two;
        var frame = new byte[ProtocolConstants.Three + frameLength];
        frame[0] = 0xAB;
        frame[1] = (byte)((frameLength >> 8) & 0xFF);
        frame[2] = (byte)(frameLength & 0xFF);
        finsMessage.CopyTo(frame.AsMemory(ProtocolConstants.Three));
        var checksum = CalculateChecksum(frame.AsSpan(0, frame.Length - ProtocolConstants.Two));
        frame[^ProtocolConstants.Two] = (byte)((checksum >> 8) & 0xFF);
        frame[^1] = (byte)(checksum & 0xFF);
        return frame;
    }

    /// <summary>Decodes a Toolbus response frame into the contained binary FINS response message.</summary>
    /// <param name="frame">Complete Toolbus frame.</param>
    /// <returns>Binary FINS response message.</returns>
    public static Memory<byte> DecodeResponse(ReadOnlyMemory<byte> frame)
    {
        if (frame.Length < 5)
        {
            throw new OmronPLCException("The Toolbus response frame was too short.");
        }

        var span = frame.Span;
        if (span[0] != 0xAB)
        {
            throw new OmronPLCException("The Toolbus response frame did not start with 0xAB.");
        }

        var declaredLength = (span[1] << 8) | span[2];
        if (declaredLength < ProtocolConstants.Two)
        {
            throw new OmronPLCException("The Toolbus response frame length was invalid.");
        }

        if (frame.Length != declaredLength + ProtocolConstants.Three)
        {
            throw new OmronPLCException(
                $"The Toolbus response frame length {frame.Length} did not match declared length "
                    + $"{declaredLength}.");
        }

        var expectedChecksum = CalculateChecksum(span.Slice(0, frame.Length - ProtocolConstants.Two));
        var receivedChecksum = (ushort)((span[frame.Length - ProtocolConstants.Two] << 8) | span[frame.Length - 1]);
        if (expectedChecksum != receivedChecksum)
        {
            throw new OmronPLCException(
                $"The Toolbus response checksum was invalid. Expected 0x{expectedChecksum:X4},"
                    + $" received 0x{receivedChecksum:X4}.");
        }

        var payloadLength = declaredLength - ProtocolConstants.Two;
        if (payloadLength < MinimumFinsResponseLength)
        {
            throw new OmronPLCException("The Toolbus response FINS payload was too short.");
        }

        if (span[3] != 0xC0 && span[3] != 0xC1)
        {
            throw new OmronPLCException("The Toolbus response FINS header was invalid.");
        }

        var payload = new byte[payloadLength];
        span.Slice(ProtocolConstants.Three, payloadLength).CopyTo(payload);
        return payload;
    }

    /// <summary>Calculates the Toolbus additive checksum over the supplied bytes.</summary>
    /// <param name="data">Frame bytes excluding the checksum tail.</param>
    /// <returns>16-bit additive checksum.</returns>
    public static ushort CalculateChecksum(ReadOnlySpan<byte> data)
    {
        uint checksum = 0;
        foreach (var value in data)
        {
            checksum += value;
        }

        return (ushort)(checksum & 0xFFFF);
    }
}
