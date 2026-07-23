// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Windows;
using IoT.DriverCore.OmronPlcRx.Dashboard.ViewModels;

namespace IoT.DriverCore.OmronPlcRx.Dashboard.Views;

/// <summary>Dialog used to add a PLC tag.</summary>
public partial class AddTagDialog
{
    /// <summary>Initializes a new instance of the <see cref="AddTagDialog"/> class.</summary>
    public AddTagDialog() => InitializeComponent();

    /// <summary>Gets the dialog view model.</summary>
    public AddTagViewModel? ViewModel => DataContext as AddTagViewModel;

    /// <summary>Accepts the dialog when the view model is valid.</summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.CanAccept != true)
        {
            return;
        }

        DialogResult = true;
    }
}
