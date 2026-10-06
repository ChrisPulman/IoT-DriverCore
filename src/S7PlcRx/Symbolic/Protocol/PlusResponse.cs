// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic.Protocol;
#else
namespace IoT.Driver.S7PlcRx.Symbolic.Protocol;
#endif

/// <summary>Provides plus response.</summary>
internal sealed class PlusResponse
{
    /// <summary>Initializes a new instance of the <see cref="PlusResponse"/> class.</summary>
    /// <param name="message">The message.</param>
    internal PlusResponse(PlusMessage message)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(message);
#else
        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }
#endif
        Version = message.Version;
        Body = new(message.Payload);
        if (Body.Byte() != 0x32)
        {
            throw new InvalidDataException("Expected an S7Plus response.");
        }

        Body.Skip(0x2);
        Function = Body.UInt16();
        Body.Skip(0x2);
        Sequence = Body.UInt16();
        Flags = Body.Byte();
    }

    /// <summary>Gets function.</summary>
    internal ushort Function { get; }

    /// <summary>Gets sequence.</summary>
    internal ushort Sequence { get; }

    /// <summary>Gets flags.</summary>
    internal byte Flags { get; }

    /// <summary>Gets version.</summary>
    internal byte Version { get; }

    /// <summary>Gets body.</summary>
    internal PlusReader Body { get; }

    /// <summary>Gets or sets expected integrity.</summary>
    internal uint? ExpectedIntegrity { get; set; }
}
