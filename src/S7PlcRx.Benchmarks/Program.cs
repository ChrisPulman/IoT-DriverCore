// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reflection;
using IoT.DriverCore.S7PlcRx.Benchmarks;

try
{
    Trace.WriteLine($"AppBase: {AppContext.BaseDirectory}");

    // Ensure MockS7Plc is loadable before running harness
    var mockAssembly = Assembly.Load("MockS7Plc");
    Trace.WriteLine($"Loaded MockS7Plc: {mockAssembly.Location}");

    return await PerfHarness.RunAsync();
}
catch (Exception ex)
{
    Trace.WriteLine(ex);
    Trace.WriteLine("Files in AppBase:");
    foreach (var file in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.*"))
    {
        var name = Path.GetFileName(file);
        if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("snap7.dll", StringComparison.OrdinalIgnoreCase))
        {
            Trace.WriteLine($"  {name}");
        }
    }

    return 1;
}
