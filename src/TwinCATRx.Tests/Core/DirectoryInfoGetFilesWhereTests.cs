// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using IoT.DriverCore.TwinCATRx.Core;

namespace IoT.DriverCore.TwinCATRx.Tests.Core;

/// <summary>Tests for DirectoryInfoGetFilesWhere extension methods.</summary>
public class DirectoryInfoGetFilesWhereTests
{
    /// <summary>The expected number of files accepted by the compound predicate.</summary>
    private const int ExpectedFilteredFileCount = 2;

    /// <summary>Verifies basic predicate filtering.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task GetFilesWhere_Basic_Filter_WorksAsync()
    {
        var dir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"GetFilesWhere_{Guid.NewGuid()}"));
        dir.Create();
        try
        {
            await WriteEmptyFileAsync(Path.Combine(dir.FullName, "a.txt"));
            await WriteEmptyFileAsync(Path.Combine(dir.FullName, "b.cs"));
            await WriteEmptyFileAsync(Path.Combine(dir.FullName, "c.asp"));

            var files = DirectoryInfoExtensions.GetFilesWhere(dir, f => f.Extension == ".txt" || f.Extension == ".cs");
            await TUnitAssert.That(files.Length).IsEqualTo(ExpectedFilteredFileCount);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>Verifies search-pattern filtering.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task GetFilesWhere_With_SearchPattern_WorksAsync()
    {
        var dir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"GetFilesWhere2_{Guid.NewGuid()}"));
        dir.Create();
        try
        {
            await WriteEmptyFileAsync(Path.Combine(dir.FullName, "a.txt"));
            await WriteEmptyFileAsync(Path.Combine(dir.FullName, "b.cs"));
            await WriteEmptyFileAsync(Path.Combine(dir.FullName, "c.asp"));

            var files = DirectoryInfoExtensions.GetFilesWhere(dir, "*.cs", f => true);
            await TUnitAssert.That(files.Length).IsEqualTo(1);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>Writes an empty file to the supplied path.</summary>
    /// <param name="path">The path to write.</param>
    /// <returns>The write task.</returns>
    private static Task WriteEmptyFileAsync(string path)
    {
#if NETFRAMEWORK
        return Task.Run(() => File.WriteAllText(path, string.Empty));
#else
        return File.WriteAllTextAsync(path, string.Empty);
#endif
    }
}
