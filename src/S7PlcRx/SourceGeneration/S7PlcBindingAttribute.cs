// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.S7PlcRx.Reactive.SourceGeneration;

#else
namespace IoT.DriverCore.S7PlcRx.SourceGeneration;

#endif

/// <summary>Marks a class as an S7 PLC binding target.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class S7PlcBindingAttribute : Attribute;
