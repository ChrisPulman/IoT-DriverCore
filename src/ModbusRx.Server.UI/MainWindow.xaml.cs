// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.ModbusRx.Server.UI.Data;
using IoT.DriverCore.ModbusRx.Server.UI.Services;
using IoT.DriverCore.ModbusRx.Server.UI.Visualization;
using Microsoft.EntityFrameworkCore;
using ReactiveUI.SourceGenerators;

namespace IoT.DriverCore.ModbusRx.Server.UI;

/// <summary>Interaction logic for MainWindow.xaml.</summary>
[IViewFor<ModbusServerViewModel>]
public partial class MainWindow
{
    /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
    public MainWindow()
    {
        InitializeComponent();
        SetupDependencies();
    }

    /// <summary>Configures the data context and application services.</summary>
    private void SetupDependencies()
    {
        // Setup Entity Framework
        var optionsBuilder = new DbContextOptionsBuilder<ModbusServerContext>();
        _ = optionsBuilder.UseSqlite("Data Source=modbusrx.db");

        var context = new ModbusServerContext(optionsBuilder.Options);
        _ = context.Database.EnsureCreated();

        // Setup services
        var configurationService = new ConfigurationService(context, TimeProvider.System);

        // Create and set view model
        ViewModel = new(
            configurationService,
            action =>
            {
                if (Dispatcher.CheckAccess())
                {
                    action();
                    return;
                }

                _ = Dispatcher.InvokeAsync(action);
            });
        DataContext = ViewModel;

        // Setup window events
        Closed += (_, _) => ViewModel?.Dispose();

        // Handle window closing to ensure proper cleanup
        Closing += (sender, e) =>
        {
            if (ViewModel?.IsServerRunning != true)
            {
                return;
            }

            // Let the ViewModel handle the exit confirmation
            _ = ViewModel.ExitApplicationCommand.Execute().Subscribe();
            e.Cancel = true; // Cancel the close to let the command handle it
        };
    }
}
