// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace IoT.Driver.ABPlcRx.Reactive.SourceGeneration;
#else
namespace IoT.Driver.ABPlcRx.SourceGeneration;
#endif

/// <summary>Marks a partial type as a PLC reactive stream model.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class PlcModelAttribute : Attribute
{
    /// <summary>Gets debugger-only type information without affecting the public API.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => ToString() ?? GetType().Name;
}
