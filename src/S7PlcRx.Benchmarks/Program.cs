// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reflection;
using IoT.Driver.S7PlcRx.Benchmarks;

namespace IoT.Driver.S7PlcRx.Benchmarks;

/// <summary>Provides the benchmark harness entry point.</summary>
public static class Program
{
    /// <summary>Runs the benchmark harness.</summary>
    /// <returns>The process exit code.</returns>
    public static async Task<int> Main()
    {
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
    }
}
