// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Windows;
using ReactiveUI.Builder;
using Splat;

namespace IoT.DriverCore.ModbusRx.Server.UI;

/// <summary>Interaction logic for App.xaml.</summary>
public partial class App
{
    /// <summary>Initializes a new instance of the <see cref="App"/> class.</summary>
    public App() =>
        Locator.CurrentMutable.RegisterConstant<ILogger>(new DebugLogger());

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _ = RxAppBuilder.CreateReactiveUIBuilder()
            .WithWpf()
            .BuildApp();
    }
}
