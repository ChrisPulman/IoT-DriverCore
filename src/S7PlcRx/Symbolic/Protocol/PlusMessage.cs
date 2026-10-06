// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic.Protocol;
#else
namespace IoT.Driver.S7PlcRx.Symbolic.Protocol;
#endif

/// <summary>Provides plus message.</summary>
internal sealed class PlusMessage
{
    /// <summary>Initializes a new instance of the <see cref="PlusMessage"/> class.</summary>
    /// <param name="version">The version.</param>
    /// <param name="payload">The payload.</param>
    internal PlusMessage(byte version, byte[] payload)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(payload);
#else
        if (payload is null)
        {
            throw new ArgumentNullException(nameof(payload));
        }
#endif
        Version = version;
        Payload = payload;
    }

    /// <summary>Gets version.</summary>
    internal byte Version { get; }

    /// <summary>Gets payload.</summary>
    internal byte[] Payload { get; }
}
