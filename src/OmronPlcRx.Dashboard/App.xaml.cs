// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Windows;
using ReactiveUI.Builder;

namespace IoT.DriverCore.OmronPlcRx.Dashboard;

/// <summary>Dashboard application entry point.</summary>
public partial class App : Application
{
    /// <summary>Initializes a new instance of the <see cref="App"/> class.</summary>
    public App()
    {
        _ = ReactiveUI.Builder.RxAppBuilder.CreateReactiveUIBuilder()
            .WithWpf()
            .Build();
    }
}
