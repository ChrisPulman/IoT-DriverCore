// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive.Tests;
#else

namespace MitsubishiRx.Tests;
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
        var assembly = typeof(MitsubishiRx).Assembly;
        var serialOptionsType = assembly.GetType("MitsubishiRx.MitsubishiSerialOptions");
        await Assert.That(serialOptionsType is not null).IsTrue();
    }

    /// <summary>Executes the LibraryProjectShouldReferenceSerialportRxPackage operation.</summary>
    /// <returns>The LibraryProjectShouldReferenceSerialportRxPackage operation result.</returns>
    [Test]
    internal async Task LibraryProjectShouldReferenceSerialportRxPackageAsync()
    {
        var projectPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "MitsubishiRx",
            "MitsubishiRx.csproj");
        projectPath = Path.GetFullPath(projectPath);
        var projectText = await File.ReadAllTextAsync(projectPath);

        await Assert.That(projectText.Contains("SerialPortRx", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }
}
