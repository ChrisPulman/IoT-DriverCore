// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Provides a thread-safe, deterministic Mitsubishi device-memory image.</summary>
/// <remarks>
/// Uninitialised devices read as zero. Word and bit devices share the same
/// addressable image while their public accessors validate the declared device kind.
/// </remarks>
public sealed class MitsubishiSimulatorMemory
{
    /// <summary>Synchronizes the memory image and version.</summary>
    private readonly object _gate = new();

    /// <summary>Stores values by device symbol and numeric address.</summary>
    private readonly Dictionary<DeviceKey, ushort> _values = [];

    /// <summary>Stores the monotonically increasing memory version.</summary>
    private long _version;

    /// <summary>Gets the monotonically increasing memory version.</summary>
    public long Version
    {
        get
        {
            lock (_gate)
            {
                return _version;
            }
        }
    }

    /// <summary>Reads one word device.</summary>
    /// <param name="address">The word-device address.</param>
    /// <returns>The current value, or zero when the device has not been written.</returns>
    public ushort ReadWord(
        string address) =>
        ReadWord(address, XyAddressNotation.Octal);

    /// <summary>Reads one word device.</summary>
    /// <param name="address">The word-device address.</param>
    /// <param name="addressNotation">The X/Y address notation.</param>
    /// <returns>The current value, or zero when the device has not been written.</returns>
    public ushort ReadWord(
        string address,
        XyAddressNotation addressNotation) =>
        ReadWords(MitsubishiDeviceAddress.Parse(address, addressNotation), 1)[0];

    /// <summary>Reads consecutive word devices.</summary>
    /// <param name="address">The first word-device address.</param>
    /// <param name="points">The number of words to read.</param>
    /// <returns>A detached value snapshot.</returns>
    public ushort[] ReadWords(
        string address,
        int points) =>
        ReadWords(address, points, XyAddressNotation.Octal);

    /// <summary>Reads consecutive word devices.</summary>
    /// <param name="address">The first word-device address.</param>
    /// <param name="points">The number of words to read.</param>
    /// <param name="addressNotation">The X/Y address notation.</param>
    /// <returns>A detached value snapshot.</returns>
    public ushort[] ReadWords(
        string address,
        int points,
        XyAddressNotation addressNotation) =>
        ReadWords(MitsubishiDeviceAddress.Parse(address, addressNotation), points);

    /// <summary>Reads consecutive word devices.</summary>
    /// <param name="address">The first word-device address.</param>
    /// <param name="points">The number of words to read.</param>
    /// <returns>A detached value snapshot.</returns>
    public ushort[] ReadWords(MitsubishiDeviceAddress address, int points)
    {
        ValidateAddress(address, DeviceValueKind.Word);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);
        return ReadValues(address, points);
    }

    /// <summary>Writes one word device.</summary>
    /// <param name="address">The word-device address.</param>
    /// <param name="value">The value to write.</param>
    public void WriteWord(
        string address,
        ushort value) =>
        WriteWord(address, value, XyAddressNotation.Octal);

    /// <summary>Writes one word device.</summary>
    /// <param name="address">The word-device address.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="addressNotation">The X/Y address notation.</param>
    public void WriteWord(
        string address,
        ushort value,
        XyAddressNotation addressNotation) =>
        WriteWords(MitsubishiDeviceAddress.Parse(address, addressNotation), [value]);

    /// <summary>Writes consecutive word devices.</summary>
    /// <param name="address">The first word-device address.</param>
    /// <param name="values">The values to write.</param>
    public void WriteWords(
        string address,
        IReadOnlyList<ushort> values) =>
        WriteWords(address, values, XyAddressNotation.Octal);

    /// <summary>Writes consecutive word devices.</summary>
    /// <param name="address">The first word-device address.</param>
    /// <param name="values">The values to write.</param>
    /// <param name="addressNotation">The X/Y address notation.</param>
    public void WriteWords(
        string address,
        IReadOnlyList<ushort> values,
        XyAddressNotation addressNotation)
    {
        ArgumentNullException.ThrowIfNull(values);
        WriteWords(MitsubishiDeviceAddress.Parse(address, addressNotation), values);
    }

    /// <summary>Writes consecutive word devices.</summary>
    /// <param name="address">The first word-device address.</param>
    /// <param name="values">The values to write.</param>
    public void WriteWords(MitsubishiDeviceAddress address, IReadOnlyList<ushort> values)
    {
        ValidateAddress(address, DeviceValueKind.Word);
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException("At least one value must be supplied.", nameof(values));
        }

        WriteValues(address, values);
    }

    /// <summary>Reads one bit device.</summary>
    /// <param name="address">The bit-device address.</param>
    /// <returns>The current value, or <see langword="false"/> when the device has not been written.</returns>
    public bool ReadBit(
        string address) =>
        ReadBit(address, XyAddressNotation.Octal);

    /// <summary>Reads one bit device.</summary>
    /// <param name="address">The bit-device address.</param>
    /// <param name="addressNotation">The X/Y address notation.</param>
    /// <returns>The current value, or <see langword="false"/> when the device has not been written.</returns>
    public bool ReadBit(
        string address,
        XyAddressNotation addressNotation) =>
        ReadBits(MitsubishiDeviceAddress.Parse(address, addressNotation), 1)[0];

    /// <summary>Reads consecutive bit devices.</summary>
    /// <param name="address">The first bit-device address.</param>
    /// <param name="points">The number of bits to read.</param>
    /// <returns>A detached value snapshot.</returns>
    public bool[] ReadBits(
        string address,
        int points) =>
        ReadBits(address, points, XyAddressNotation.Octal);

    /// <summary>Reads consecutive bit devices.</summary>
    /// <param name="address">The first bit-device address.</param>
    /// <param name="points">The number of bits to read.</param>
    /// <param name="addressNotation">The X/Y address notation.</param>
    /// <returns>A detached value snapshot.</returns>
    public bool[] ReadBits(
        string address,
        int points,
        XyAddressNotation addressNotation) =>
        ReadBits(MitsubishiDeviceAddress.Parse(address, addressNotation), points);

    /// <summary>Reads consecutive bit devices.</summary>
    /// <param name="address">The first bit-device address.</param>
    /// <param name="points">The number of bits to read.</param>
    /// <returns>A detached value snapshot.</returns>
    public bool[] ReadBits(MitsubishiDeviceAddress address, int points)
    {
        ValidateAddress(address, DeviceValueKind.Bit);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);
        return ReadValues(address, points).Select(static value => value != 0).ToArray();
    }

    /// <summary>Writes one bit device.</summary>
    /// <param name="address">The bit-device address.</param>
    /// <param name="value">The value to write.</param>
    public void WriteBit(
        string address,
        bool value) =>
        WriteBit(address, value, XyAddressNotation.Octal);

    /// <summary>Writes one bit device.</summary>
    /// <param name="address">The bit-device address.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="addressNotation">The X/Y address notation.</param>
    public void WriteBit(
        string address,
        bool value,
        XyAddressNotation addressNotation) =>
        WriteBits(MitsubishiDeviceAddress.Parse(address, addressNotation), [value]);

    /// <summary>Writes consecutive bit devices.</summary>
    /// <param name="address">The first bit-device address.</param>
    /// <param name="values">The values to write.</param>
    public void WriteBits(
        string address,
        IReadOnlyList<bool> values) =>
        WriteBits(address, values, XyAddressNotation.Octal);

    /// <summary>Writes consecutive bit devices.</summary>
    /// <param name="address">The first bit-device address.</param>
    /// <param name="values">The values to write.</param>
    /// <param name="addressNotation">The X/Y address notation.</param>
    public void WriteBits(
        string address,
        IReadOnlyList<bool> values,
        XyAddressNotation addressNotation)
    {
        ArgumentNullException.ThrowIfNull(values);
        WriteBits(MitsubishiDeviceAddress.Parse(address, addressNotation), values);
    }

    /// <summary>Writes consecutive bit devices.</summary>
    /// <param name="address">The first bit-device address.</param>
    /// <param name="values">The values to write.</param>
    public void WriteBits(MitsubishiDeviceAddress address, IReadOnlyList<bool> values)
    {
        ValidateAddress(address, DeviceValueKind.Bit);
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException("At least one value must be supplied.", nameof(values));
        }

        WriteValues(address, values.Select(static value => value ? (ushort)1 : (ushort)0).ToArray());
    }

    /// <summary>Gets a deterministic detached snapshot of the populated memory image.</summary>
    /// <returns>Values ordered by device symbol and numeric address.</returns>
    public IReadOnlyList<MitsubishiSimulatorDeviceValue> Snapshot()
    {
        lock (_gate)
        {
            return _values
                .OrderBy(static pair => pair.Key.Symbol, StringComparer.Ordinal)
                .ThenBy(static pair => pair.Key.Number)
                .Select(static pair => new MitsubishiSimulatorDeviceValue(
                    pair.Key.Symbol,
                    pair.Key.Number,
                    MitsubishiDeviceAddress.Metadata[pair.Key.Symbol].Kind,
                    pair.Value))
                .ToArray();
        }
    }

    /// <summary>Clears all populated devices.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            if (_values.Count == 0)
            {
                return;
            }

            _values.Clear();
            _version++;
        }
    }

    /// <summary>Reads raw consecutive values after the caller validates the device kind.</summary>
    /// <param name="address">The first device address.</param>
    /// <param name="points">The number of values.</param>
    /// <returns>A detached raw value snapshot.</returns>
    internal ushort[] ReadValues(MitsubishiDeviceAddress address, int points)
    {
        var result = new ushort[points];
        lock (_gate)
        {
            for (var offset = 0; offset < points; offset++)
            {
                _ = _values.TryGetValue(
                    new DeviceKey(address.Symbol, checked(address.Number + offset)),
                    out result[offset]);
            }
        }

        return result;
    }

    /// <summary>Writes raw consecutive values after the caller validates the device kind.</summary>
    /// <param name="address">The first device address.</param>
    /// <param name="values">The values to write.</param>
    internal void WriteValues(MitsubishiDeviceAddress address, IReadOnlyList<ushort> values)
    {
        lock (_gate)
        {
            for (var offset = 0; offset < values.Count; offset++)
            {
                _values[new DeviceKey(address.Symbol, checked(address.Number + offset))] =
                    values[offset];
            }

            _version++;
        }
    }

    /// <summary>Validates a device address and its expected value kind.</summary>
    /// <param name="address">The address to validate.</param>
    /// <param name="expectedKind">The required device kind.</param>
    private static void ValidateAddress(
        MitsubishiDeviceAddress address,
        DeviceValueKind expectedKind)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.Number < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(address), "Device addresses cannot be negative.");
        }

        if (address.Descriptor.Kind == expectedKind)
        {
            return;
        }

        throw new ArgumentException(
            $"Device '{address.Symbol}' is a {address.Descriptor.Kind} device, not a {expectedKind} device.",
            nameof(address));
    }

    /// <summary>Identifies one numeric device address.</summary>
    /// <param name="Symbol">The device symbol.</param>
    /// <param name="Number">The numeric address.</param>
    private readonly record struct DeviceKey(string Symbol, int Number);
}
