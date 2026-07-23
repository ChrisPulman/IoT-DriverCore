// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.TwinCATRx.Reactive;
#else
namespace IoT.DriverCore.TwinCATRx;
#endif

/// <summary>Describes one symbol hosted by an <see cref="InMemoryAdsClient"/>.</summary>
public sealed class InMemoryAdsSymbol
{
    /// <summary>Initializes a new instance of the <see cref="InMemoryAdsSymbol"/> class.</summary>
    /// <param name="name">The case-insensitive ADS variable name.</param>
    /// <param name="value">The initial symbol value.</param>
    /// <param name="dataType">The declared value type.</param>
    /// <param name="arrayLength">The declared array or string length, or -1 for a scalar.</param>
    /// <param name="isReadable">Whether ADS reads are permitted.</param>
    /// <param name="isWritable">Whether ADS writes are permitted.</param>
    public InMemoryAdsSymbol(
        string name,
        object? value,
        Type dataType,
        int arrayLength,
        bool isReadable,
        bool isWritable)
    {
#if NET
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
#else
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("The symbol name cannot be null or whitespace.", nameof(name));
        }
#endif

        Name = name.Trim();
        Value = value;
        DataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
        ArrayLength = arrayLength;
        IsReadable = isReadable;
        IsWritable = isWritable;
    }

    /// <summary>Gets the declared array or string length, or -1 for a scalar.</summary>
    public int ArrayLength { get; }

    /// <summary>Gets the declared value type.</summary>
    public Type DataType { get; }

    /// <summary>Gets a value indicating whether ADS reads are permitted.</summary>
    public bool IsReadable { get; }

    /// <summary>Gets a value indicating whether ADS writes are permitted.</summary>
    public bool IsWritable { get; }

    /// <summary>Gets the case-insensitive ADS variable name.</summary>
    public string Name { get; }

    /// <summary>Gets the current symbol value.</summary>
    public object? Value { get; internal set; }
}
