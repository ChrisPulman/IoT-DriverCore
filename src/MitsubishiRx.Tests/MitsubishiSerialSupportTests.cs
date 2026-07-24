// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive.Tests;
#else

namespace IoT.DriverCore.MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiSerialSupportTests type.</summary>
internal sealed class MitsubishiSerialSupportTests
{
    /// <summary>Executes the SerialTransportKindEnumShouldExposeSerialMember operation.</summary>
    /// <returns>The SerialTransportKindEnumShouldExposeSerialMember operation result.</returns>
    [Test]
    internal async Task SerialTransportKindEnumShouldExposeSerialMemberAsync()
    {
        await Assert.That(Enum.GetNames<MitsubishiTransportKind>()).Contains("Serial");
    }

    /// <summary>Executes the SerialFrameTypesShouldExistForSerialMcProtocols operation.</summary>
    /// <returns>The SerialFrameTypesShouldExistForSerialMcProtocols operation result.</returns>
    [Test]
    internal async Task SerialFrameTypesShouldExistForSerialMcProtocolsAsync()
    {
        await Assert.That(Enum.GetNames<MitsubishiFrameType>()).Contains("OneC");
        await Assert.That(Enum.GetNames<MitsubishiFrameType>()).Contains("ThreeC");
        await Assert.That(Enum.GetNames<MitsubishiFrameType>()).Contains("FourC");
    }

    /// <summary>Executes the LibraryShouldDefineReactiveSerialOptionsType operation.</summary>
    /// <returns>The LibraryShouldDefineReactiveSerialOptionsType operation result.</returns>
    [Test]
    internal async Task LibraryShouldDefineReactiveSerialOptionsTypeAsync()
    {
        await Assert.That(typeof(MitsubishiSerialOptions).FullName)
            .IsEqualTo("IoT.DriverCore.MitsubishiRx.MitsubishiSerialOptions");
    }

    /// <summary>Executes the LibraryProjectShouldReferenceSerialportRxPackage operation.</summary>
    /// <returns>The LibraryProjectShouldReferenceSerialportRxPackage operation result.</returns>
    [Test]
    internal async Task LibraryProjectShouldReferenceSerialportRxPackageAsync()
    {
        var projectPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "MitsubishiRx",
            "MitsubishiRx.csproj");
        var projectText = await File.ReadAllTextAsync(projectPath);

        await Assert.That(projectText.Contains("SerialPortRx", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    /// <summary>Finds the checkout independently of the configured SDK output layout.</summary>
    /// <returns>The repository root.</returns>
    private static string GetRepositoryRoot()
    {
        foreach (string startingPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(startingPath); directory is not null; directory = directory.Parent)
            {
                string projectPath = Path.Combine(
                    directory.FullName,
                    "src",
                    "MitsubishiRx",
                    "MitsubishiRx.csproj");
                if (File.Exists(projectPath))
                {
                    return directory.FullName;
                }
            }
        }

        throw new DirectoryNotFoundException("Unable to locate the IoT-DriverCore repository.");
    }
}
