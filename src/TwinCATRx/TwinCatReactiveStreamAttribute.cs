// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.TwinCATRx.Reactive;
#else
namespace IoT.Driver.TwinCATRx;
#endif

/// <summary>Generates observable members for one TwinCAT variable.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class TwinCatReactiveStreamAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="TwinCatReactiveStreamAttribute"/> class.</summary>
    /// <param name="variable">The TwinCAT variable address.</param>
    /// <param name="dataType">The variable data type.</param>
    public TwinCatReactiveStreamAttribute(string variable, Type dataType)
    {
        Variable = variable;
        DataType = dataType;
    }

    /// <summary>Gets the TwinCAT variable address.</summary>
    public string Variable { get; }

    /// <summary>Gets the variable data type.</summary>
    public Type DataType { get; }

    /// <summary>Gets or sets the optional correlation identifier.</summary>
    public string? Id { get; set; }

    /// <summary>Gets or sets the generated property name.</summary>
    public string? PropertyName { get; set; }

    /// <summary>Gets or sets the generated observable name.</summary>
    public string? ObservableName { get; set; }
}
