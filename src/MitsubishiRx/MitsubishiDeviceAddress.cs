// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiDeviceAddress record.</summary>
/// <param name="Symbol">The Symbol parameter.</param>
/// <param name="Number">The Number parameter.</param>
/// <param name="Notation">The Notation parameter.</param>
/// <param name="Original">The Original parameter.</param>
public sealed partial record MitsubishiDeviceAddress(
    string Symbol,
    int Number,
    XyAddressNotation Notation,
    string Original)
{
    /// <summary>Stores the device metadata lookup.</summary>
    private static readonly ReadOnlyDictionary<string, MitsubishiDeviceMetadata> DeviceMetadataLookup = new(
        new Dictionary<string, MitsubishiDeviceMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            ["X"] = new("X", 0x9C, 0x5820, DeviceValueKind.Bit, DeviceNumberFormat.XyVariable),
            ["Y"] = new("Y", 0x9D, 0x5920, DeviceValueKind.Bit, DeviceNumberFormat.XyVariable),
            ["M"] = new("M", 0x90, 0x4D20, DeviceValueKind.Bit, DeviceNumberFormat.Decimal),
            ["L"] = new("L", 0x92, 0x4C20, DeviceValueKind.Bit, DeviceNumberFormat.Decimal),
            ["B"] = new("B", 0xA0, 0x4220, DeviceValueKind.Bit, DeviceNumberFormat.Hexadecimal),
            ["D"] = new("D", 0xA8, 0x4420, DeviceValueKind.Word, DeviceNumberFormat.Decimal),
            ["W"] = new("W", 0xB4, 0x5720, DeviceValueKind.Word, DeviceNumberFormat.Hexadecimal),
            ["R"] = new("R", 0xAF, 0x5220, DeviceValueKind.Word, DeviceNumberFormat.Decimal),
            ["ZR"] = new("ZR", 0xB0, 0x5A52, DeviceValueKind.Word, DeviceNumberFormat.Hexadecimal),
            ["TN"] = new("TN", 0xC2, 0x544E, DeviceValueKind.Word, DeviceNumberFormat.Decimal),
            ["TS"] = new("TS", 0xC1, 0x5453, DeviceValueKind.Bit, DeviceNumberFormat.Decimal),
            ["TC"] = new("TC", 0xC0, 0x5443, DeviceValueKind.Bit, DeviceNumberFormat.Decimal),
            ["CN"] = new("CN", 0xC5, 0x434E, DeviceValueKind.Word, DeviceNumberFormat.Decimal),
            ["CS"] = new("CS", 0xC4, 0x4353, DeviceValueKind.Bit, DeviceNumberFormat.Decimal),
            ["CC"] = new("CC", 0xC3, 0x4343, DeviceValueKind.Bit, DeviceNumberFormat.Decimal),
            ["SM"] = new("SM", 0x91, 0x534D, DeviceValueKind.Bit, DeviceNumberFormat.Decimal),
            ["SD"] = new("SD", 0xA9, 0x5344, DeviceValueKind.Word, DeviceNumberFormat.Decimal),
        });

    /// <summary>Gets or sets the Metadata property.</summary>
    public static IReadOnlyDictionary<string, MitsubishiDeviceMetadata> Metadata => DeviceMetadataLookup;

    /// <summary>Gets or sets the Descriptor property.</summary>
    public MitsubishiDeviceMetadata Descriptor => DeviceMetadataLookup[Symbol];

    /// <summary>Executes the Parse operation.</summary>
    /// <param name="value">The value parameter.</param>
    /// <param name="addressNotation">The addressNotation parameter.</param>
    /// <returns>The Parse operation result.</returns>
    public static MitsubishiDeviceAddress Parse(
        string value,
        XyAddressNotation addressNotation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var trimmed = value.Trim().ToUpperInvariant();
        var match = DeviceRegex().Match(trimmed);
        if (!match.Success)
        {
            throw new FormatException($"Invalid Mitsubishi device address '{value}'.");
        }

        var symbol = match.Groups[1].Value;
        var numberText = match.Groups[2].Value;
        if (!DeviceMetadataLookup.TryGetValue(symbol, out var metadata))
        {
            throw new NotSupportedException($"Device '{symbol}' is not currently supported.");
        }

        var number = Convert.ToInt32(numberText, metadata.GetRadix(addressNotation));
        return new MitsubishiDeviceAddress(symbol, number, addressNotation, trimmed);
    }

    /// <summary>Executes the DeviceRegex operation.</summary>
    /// <returns>The DeviceRegex operation result.</returns>
    [GeneratedRegex("^([A-Z]+)([0-9A-F]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex DeviceRegex();
}
