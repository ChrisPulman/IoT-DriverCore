// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive;

#else
namespace OmronPlcRx;

#endif

/// <summary>Marks a partial class as a generated PLC tag binding container.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PlcTagBindingAttribute : Attribute;
