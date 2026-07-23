// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace IoT.DriverCore.OmronPlcRx.Dashboard.ViewModels;

/// <summary>View model for Add Tag wizard/dialog.</summary>
public sealed partial class AddTagViewModel : ReactiveObject
{
    /// <summary>Stores the largest supported bit index.</summary>
    private const int MaximumBitIndex = 15;

    /// <summary>Matches supported PLC tag address syntax.</summary>
    private static readonly Regex AddressRegex = CreateAddressRegex();

    /// <summary>Initializes a new instance of the <see cref="AddTagViewModel"/> class.</summary>
    /// <param name="allowedTypes">The allowed types value.</param>
    public AddTagViewModel(IEnumerable<Type> allowedTypes)
    {
        AllowedTypes = new List<Type>(allowedTypes);
        _ = this.WhenAnyValue(x => x.Name, x => x.Address, x => x.SelectedType)
            .Select(_ => Validate(Name, Address))
            .SubscribeSafe(valid => CanAccept = valid, exception => ValidationMessage = exception.Message);
        OkCommand = ReactiveCommand.Create(static () => { }, this.WhenAnyValue(v => v.CanAccept));
    }

    /// <summary>Gets the allowed types value.</summary>
    public IReadOnlyList<Type> AllowedTypes { get; }

    /// <summary>Gets or sets the name value.</summary>
    public string Name
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    /// <summary>Gets or sets the address value.</summary>
    public string Address
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    /// <summary>Gets or sets the selected type value.</summary>
    public Type SelectedType
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = typeof(short);

    /// <summary>Gets the validation message value.</summary>
    public string ValidationMessage
    {
        get => field;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    /// <summary>Gets the can accept value.</summary>
    public bool CanAccept
    {
        get => field;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets the ok command value.</summary>
    public ReactiveCommand<RxVoid, RxVoid> OkCommand { get; }

    /// <summary>Creates the PLC address validation regex.</summary>
    /// <returns>The generated PLC address regex.</returns>
    [GeneratedRegex(@"^(?<area>[A-Za-z]{1,3})(?<word>\d+)(?:\.(?<bit>\d{1,2}))?(?:\[(?<len>\d{1,3})\])?$")]
    private static partial Regex CreateAddressRegex();

    /// <summary>Validates the requested tag name and PLC address.</summary>
    /// <param name="name">The tag name.</param>
    /// <param name="address">The PLC address.</param>
    /// <returns><see langword="true"/> when the tag can be accepted.</returns>
    private bool Validate(string? name, string? address)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ValidationMessage = "Name required";
            return false;
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            ValidationMessage = "Address required";
            return false;
        }

        var match = AddressRegex.Match(address.Trim());
        if (!match.Success)
        {
            ValidationMessage = "Invalid address format";
            return false;
        }

        if (match.Groups["bit"].Success && match.Groups["len"].Success)
        {
            ValidationMessage = "Cannot specify bit and length";
            return false;
        }

        if (match.Groups["bit"].Success
            && int.Parse(match.Groups["bit"].Value) > MaximumBitIndex)
        {
            ValidationMessage = "Bit index 0-15";
            return false;
        }

        ValidationMessage = string.Empty;
        return true;
    }
}
