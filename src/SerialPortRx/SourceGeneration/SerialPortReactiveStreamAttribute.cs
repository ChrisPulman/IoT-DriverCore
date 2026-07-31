// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.Serial.Reactive.SourceGeneration;
#else
namespace IoT.Driver.Serial.SourceGeneration;
#endif

/// <summary>Generates a property plus classic and async observable streams from serial data.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class SerialPortReactiveStreamAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="SerialPortReactiveStreamAttribute"/> class.</summary>
    /// <param name="propertyName">The generated property name.</param>
    /// <param name="propertyType">The generated property type.</param>
    public SerialPortReactiveStreamAttribute(string propertyName, Type propertyType)
        : this(propertyName, propertyType, pattern: null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SerialPortReactiveStreamAttribute"/> class.</summary>
    /// <param name="propertyName">The generated property name.</param>
    /// <param name="propertyType">The generated property type.</param>
    /// <param name="pattern">The optional regular expression used to identify relevant data.</param>
    public SerialPortReactiveStreamAttribute(string propertyName, Type propertyType, string? pattern)
    {
        PropertyName = propertyName;
        PropertyType = propertyType;
        Pattern = pattern;
    }

    /// <summary>Gets the generated property name.</summary>
    public string PropertyName { get; }

    /// <summary>Gets the generated property type.</summary>
    public Type PropertyType { get; }

    /// <summary>Gets the optional regular expression used to identify relevant data.</summary>
    public string? Pattern { get; }

    /// <summary>Gets or sets the serial stream source.</summary>
    public SerialPortReactiveSource Source { get; set; } = SerialPortReactiveSource.Lines;

    /// <summary>Gets or sets the named regular expression group converted into the property value.</summary>
    public string? GroupName { get; set; } = "value";

    /// <summary>Gets or sets the fallback regular expression group number converted into the property value.</summary>
    public int GroupNumber { get; set; } = 1;

    /// <summary>Gets or sets a value indicating whether pattern matching ignores case.</summary>
    public bool IgnoreCase { get; set; }
}
