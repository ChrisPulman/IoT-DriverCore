// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic.Protocol;
#else
namespace IoT.Driver.S7PlcRx.Symbolic.Protocol;
#endif

/// <summary>Decodes PLC notification values, item status and alarm objects.</summary>
internal sealed class PlusNotification
{
    /// <summary>The Unix epoch in .NET ticks.</summary>
    private const long EpochTicks = 621_355_968_000_000_000L;

    /// <summary>The .NET ticks per wire microsecond.</summary>
    private const int TicksPerMicrosecond = 10;

    /// <summary>The reserved notification header size.</summary>
    private const int ReservedHeaderLength = 6;

    /// <summary>Gets the PLC subscription identifier.</summary>
    /// <value>The subscription identifier.</value>
    internal uint ObjectId { get; private set; }

    /// <summary>Gets the PLC credit tick.</summary>
    /// <value>The credit tick.</value>
    internal byte CreditTick { get; private set; }

    /// <summary>Gets the notification sequence.</summary>
    /// <value>The notification sequence.</value>
    internal uint Sequence { get; private set; }

    /// <summary>Gets the PLC timestamp when present.</summary>
    /// <value>The optional timestamp.</value>
    internal DateTimeOffset? Timestamp { get; private set; }

    /// <summary>Gets successful values by subscription item reference.</summary>
    /// <value>The successful values.</value>
    internal Dictionary<uint, S7SymbolicValue> Values { get; } = new();

    /// <summary>Gets PLC error status by subscription item reference.</summary>
    /// <value>The item errors.</value>
    internal Dictionary<uint, byte> Errors { get; } = new();

    /// <summary>Gets alarm objects.</summary>
    /// <value>The alarm objects.</value>
    internal List<PlusObject> AlarmObjects { get; } = new();

    /// <summary>Parses one complete notification, rejecting unrecognized and truncated formats.</summary>
    /// <param name="message">The assembled PLC message.</param>
    /// <returns>The decoded notification.</returns>
    internal static PlusNotification Parse(PlusMessage message)
    {
        var reader = new PlusReader(message.Payload);
        if (reader.Byte() != 0x33)
        {
            throw new InvalidDataException("Expected a notification.");
        }

        var result = ReadHeader(reader);
        byte status;
        while ((status = reader.Byte()) != 0)
        {
            result.ReadItem(reader, status);
        }

        result.ReadAlarms(reader, message.Payload);
        while (reader.Remaining > 0)
        {
            if (reader.Byte() != 0)
            {
                throw new InvalidDataException("Unexpected notification trailer.");
            }
        }

        return result;
    }

    /// <summary>Reads the notification header and optional microsecond timestamp.</summary>
    /// <param name="reader">The message reader.</param>
    /// <returns>The notification header.</returns>
    private static PlusNotification ReadHeader(PlusReader reader)
    {
        var result = new PlusNotification
        {
            ObjectId = reader.UInt32()
        };
        reader.Skip(ReservedHeaderLength);
        result.CreditTick = reader.Byte();
        result.Sequence = reader.VarUInt32();
        if (reader.Byte() != 0)
        {
            return result;
        }

        reader.Position--;
        var microseconds = reader.UInt64();
        if (microseconds > (ulong)((DateTime.MaxValue.Ticks - EpochTicks) / TicksPerMicrosecond))
        {
            throw new InvalidDataException("Notification timestamp is outside the supported range.");
        }

        result.Timestamp = new DateTimeOffset(EpochTicks + checked((long)microseconds * TicksPerMicrosecond), TimeSpan.Zero);
        _ = reader.Byte();
        return result;
    }

    /// <summary>Reads an individual value or addressing status.</summary>
    /// <param name="reader">The message reader.</param>
    /// <param name="status">The PLC item encoding and status.</param>
    private void ReadItem(PlusReader reader, byte status)
    {
        var id = status switch
        {
            0x9b => reader.VarUInt32(),
            0x92 or 0x03 or 0x13 => reader.UInt32(),
            _ => throw new InvalidDataException("Unsupported notification item format."),
        };
        if (Values.ContainsKey(id) || Errors.ContainsKey(id))
        {
            throw new InvalidDataException("Duplicate notification item reference.");
        }

        S7SymbolicValue.CheckCount(Values.Count + Errors.Count + 1);
        if (status is 0x92 or 0x9b)
        {
            Values.Add(id, S7SymbolicValue.ReadFrom(reader));
        }
        else
        {
            Errors.Add(id, status);
        }
    }

    /// <summary>Reads the optional alarm object section.</summary>
    /// <param name="reader">The message reader.</param>
    /// <param name="payload">The original message payload.</param>
    private void ReadAlarms(PlusReader reader, byte[] payload)
    {
        if (reader.Remaining < sizeof(uint))
        {
            return;
        }

        var position = reader.Position;
        if (reader.UInt32() == 0)
        {
            reader.Position = position;
            return;
        }

        _ = reader.UInt16();
        if (reader.Byte() != 0x81)
        {
            throw new InvalidDataException("Unsupported alarm notification format.");
        }

        while (reader.Remaining > 0 && payload[reader.Position] == 0xa1)
        {
            S7SymbolicValue.CheckCount(AlarmObjects.Count + 1);
            AlarmObjects.Add(PlusObject.ReadFrom(reader));
        }
    }
}
