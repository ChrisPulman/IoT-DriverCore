// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
namespace IoT.Driver.S7PlcRx.Symbolic;
#endif

/// <summary>Selects the symbolic session authentication exchange.</summary>
public enum S7SymbolicAuthenticationMode
{
    /// <summary>Choose the exchange advertised by the controller.</summary>
    Auto,

    /// <summary>Use the legacy password challenge.</summary>
    Legacy,

    /// <summary>Use the TLS exporter based authentication exchange.</summary>
    Modern,
}
