// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Net;
using System.Text;
#if REACTIVE_SHIM
using IoT.Driver.ModbusRx.Reactive.Utility;
#else
using IoT.Driver.ModbusRx.Utility;
#endif

#if REACTIVE_SHIM
namespace IoT.Driver.ModbusRx.Reactive.Data;
#else
namespace IoT.Driver.ModbusRx.Data;
#endif

/// <summary>Collection of 16 bit registers.</summary>
public class RegisterCollection : Collection<ushort>, IDataCollection
{
    /// <summary>Initializes a new instance of the <see cref="RegisterCollection" /> class.</summary>
    public RegisterCollection()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RegisterCollection" /> class.</summary>
    /// <param name="bytes">Array for register collection.</param>
    public RegisterCollection(byte[] bytes)
        : this((IList<ushort>)ModbusUtility.NetworkBytesToHostUInt16(bytes))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RegisterCollection" /> class.</summary>
    /// <param name="registers">Array for register collection.</param>
    public RegisterCollection(params ushort[] registers)
        : this((IList<ushort>)registers)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RegisterCollection" /> class.</summary>
    /// <param name="registers">List for register collection.</param>
    public RegisterCollection(IList<ushort> registers)
        : base(PrepareRegisters(registers))
    {
    }

    /// <summary>Gets the byte count.</summary>
    public byte ByteCount => (byte)(Count * Two);

    /// <summary>Creates the network-byte representation.</summary>
    /// <returns>A new byte array in Modbus network order.</returns>
    public byte[] ToNetworkBytes()
    {
        using var bytes = new MemoryStream(ByteCount);

        foreach (var register in this)
        {
            var b = BitConverter.GetBytes((ushort)IPAddress.HostToNetworkOrder((short)register));
            bytes.Write(b, 0, b.Length);
        }

        return bytes.ToArray();
    }

    /// <summary>Returns a string that represents the current object.</summary>
    /// <returns>
    ///     A <see cref="T:System.String" /> that represents the current <see cref="T:System.Object" />.
    /// </returns>
    public override string ToString()
    {
        var builder = new StringBuilder("{");
        for (var i = 0; i < Count; i++)
        {
            if (i > 0)
            {
                _ = builder.Append(", ");
            }

            _ = builder.Append(this[i]);
        }

        _ = builder.Append('}');
        return builder.ToString();
    }

    /// <summary>Validates and prepares register data for storage.</summary>
    /// <param name="registers">The source registers.</param>
    /// <returns>A writable register list.</returns>
    private static IList<ushort> PrepareRegisters(IList<ushort> registers)
    {
        registers = ArgumentGuard.NotNull(registers, nameof(registers));

        return registers.IsReadOnly ? [.. registers] : registers;
    }
}
