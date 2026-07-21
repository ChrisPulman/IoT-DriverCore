// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

#if REACTIVE_SHIM
namespace ModbusRx.Reactive;
#else
namespace ModbusRx;
#endif

/// <summary>Routes Modbus diagnostic messages through the platform debug infrastructure.</summary>
internal static class ModbusDiagnostics
{
    /// <summary>Writes a diagnostic message.</summary>
    /// <param name="message">The diagnostic message.</param>
    internal static void Write(string message) => Debug.WriteLine(message);
}
