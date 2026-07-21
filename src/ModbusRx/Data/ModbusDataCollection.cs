// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

#if REACTIVE_SHIM
namespace ModbusRx.Reactive.Data;
#else
namespace ModbusRx.Data;
#endif

/// <summary>A 1 origin collection represetative of the Modbus Data Model.</summary>
/// <typeparam name="TData">The type of the data.</typeparam>
/// <seealso cref="System.Collections.ObjectModel.Collection&lt;TData&gt;" />
public class ModbusDataCollection<TData> : Collection<TData>
    where TData : struct
{
    /// <summary>Describes the supported collection element types.</summary>
    private const string SupportedTypesMessage = "Only bool and ushort supported";

    /// <summary>Describes the invalid zero-address operation.</summary>
    private const string InvalidZeroAddressMessage = "0 is not a valid address for a Modbus data collection.";

    /// <summary>Stores the allow Zero Element value.</summary>
    private bool _allowZeroElement;

    /// <summary>Initializes a new instance of the <see cref="ModbusDataCollection&lt;TData&gt;" /> class.</summary>
    public ModbusDataCollection()
        : base(CreateInitialData())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ModbusDataCollection&lt;TData&gt;" /> class.</summary>
    /// <param name="data">The data.</param>
    public ModbusDataCollection(params TData[] data)
        : this((IList<TData>)data)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ModbusDataCollection&lt;TData&gt;" /> class.</summary>
    /// <param name="data">The data.</param>
    public ModbusDataCollection(IList<TData> data)
        : base(PrepareData(data))
    {
        _allowZeroElement = false;
    }

    /// <summary>Gets or sets the Modbus Data Type value.</summary>
    internal ModbusDataType ModbusDataType { get; set; }

    /// <summary>Inserts an element into the collection at the specified index.</summary>
    /// <param name="index">The zero-based index at which item should be inserted.</param>
    /// <param name="item">The object to insert. The value can be null for reference types.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    ///     index is less than zero.-or-index is greater than
    ///     <see cref="P:System.Collections.ObjectModel.Collection`1.Count"></see>.
    /// </exception>
    protected override void InsertItem(int index, TData item)
    {
        if (!_allowZeroElement && index == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                InvalidZeroAddressMessage);
        }

        base.InsertItem(index, item);
    }

    /// <summary>Replaces the element at the specified index.</summary>
    /// <param name="index">The zero-based index of the element to replace.</param>
    /// <param name="item">The new value for the element at the specified index.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    ///     index is less than zero.-or-index is greater than
    ///     <see cref="P:System.Collections.ObjectModel.Collection`1.Count"></see>.
    /// </exception>
    protected override void SetItem(int index, TData item)
    {
        if (index == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                InvalidZeroAddressMessage);
        }

        base.SetItem(index, item);
    }

    /// <summary>Removes the element at the specified index.</summary>
    /// <param name="index">The zero-based index of the element to remove.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    ///     index is less than zero.-or-index is equal to or greater than
    ///     <see cref="P:System.Collections.ObjectModel.Collection`1.Count"></see>.
    /// </exception>
    protected override void RemoveItem(int index)
    {
        if (index == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                InvalidZeroAddressMessage);
        }

        base.RemoveItem(index);
    }

    /// <summary>Removes all elements from the collection.</summary>
    protected override void ClearItems()
    {
        _allowZeroElement = true;
        base.ClearItems();
        _ = AddDefault(this);
        _allowZeroElement = false;
    }

    /// <summary>Adds a default element to the collection.</summary>
    /// <param name="data">The data.</param>
    /// <returns>The result.</returns>
    private static IList<TData> AddDefault(IList<TData> data)
    {
        data.Insert(0, default);
        return data;
    }

    /// <summary>Creates the initial one-based collection storage.</summary>
    /// <returns>The initial collection storage.</returns>
    private static IList<TData> CreateInitialData()
    {
        EnsureSupportedType();
        return [default];
    }

    /// <summary>Ensures that the collection element type is supported.</summary>
    private static void EnsureSupportedType()
    {
        if (typeof(TData).Equals(typeof(bool)) || typeof(TData).Equals(typeof(ushort)))
        {
            return;
        }

        throw new NotSupportedException(SupportedTypesMessage);
    }

    /// <summary>Validates and prepares source data for the one-based collection.</summary>
    /// <param name="data">The source data.</param>
    /// <returns>The prepared data.</returns>
    private static IList<TData> PrepareData(IList<TData> data)
    {
        EnsureSupportedType();

        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        var preparedData = data.IsReadOnly ? [.. data] : data;
        return AddDefault(preparedData);
    }
}
