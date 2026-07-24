// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.TwinCATRx.Core;

namespace IoT.DriverCore.TwinCATRx.Tests.Core;

/// <summary>Exercises deterministic public guard and disposal branches of the code generator.</summary>
public sealed class CodeGeneratorBranchClosureTests
{
    /// <summary>Verifies null roots, missing output names, invalid composition, and repeated disposal are stable.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Public_Guards_And_Repeated_Disposal_Are_DeterministicAsync()
    {
        await TUnitAssert.That(() => new CodeGenerator(null, null!)).Throws<ArgumentNullException>();
        using var generator = new CodeGenerator();

        await TUnitAssert.That(() => generator.CreateCSharpCode(null!, string.Empty, isTwinCat3: false))
            .Throws<SimpleTypeException>();
        generator.Dispose();
        generator.Dispose();

        await TUnitAssert.That(() => generator.CreateCSharpCodeString(null)).Throws<SimpleTypeException>();
    }
}
