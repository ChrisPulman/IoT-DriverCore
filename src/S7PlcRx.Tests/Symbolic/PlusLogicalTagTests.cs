// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.Driver.Core;
using IoT.Driver.S7PlcRx.LogicalTags;
using IoT.Driver.S7PlcRx.Symbolic;

namespace IoT.Driver.S7PlcRx.Tests.Symbolic;

/// <summary>Tests native symbolic logical catalog composition.</summary>
public sealed class PlusLogicalTagTests
{
    /// <summary>Defines the native controller symbol path.</summary>
    private const string NativePath = "Process.Counter";

    /// <summary>Defines the read-only logical alias.</summary>
    private const string ReadOnlyAlias = "ReadOnly";

    /// <summary>Defines an unknown logical alias.</summary>
    private const string MissingAlias = "Missing";

    /// <summary>Defines the browsed native access area.</summary>
    private const uint AccessArea = 0x8a0e0000;

    /// <summary>Defines the DINT soft datatype.</summary>
    private const uint DintSoftType = 7;

    /// <summary>Defines accessible read-only symbol flags.</summary>
    private const ushort ReadOnlyFlags = 0x600;

    /// <summary>Defines the controller symbol checksum.</summary>
    private const uint SymbolCrc = 12;

    /// <summary>Defines the array lower coordinate.</summary>
    private const int LowerBound = -2;

    /// <summary>Defines the array element count.</summary>
    private const uint ArrayLength = 5;

    /// <summary>Defines the expected batch result count.</summary>
    private const int BatchCount = 3;

    /// <summary>Defines the second input payload.</summary>
    private const int SecondValue = 2;

    /// <summary>Defines the third input payload.</summary>
    private const int ThirdValue = 3;

    /// <summary>Verifies paths and access metadata survive catalog creation.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task CatalogPreservesNativeSymbolicPathsAsync()
    {
        var symbol = new S7Symbol(NativePath, new(AccessArea, 0, [1]), DintSoftType, ReadOnlyFlags, SymbolCrc, ([LowerBound], [ArrayLength], 0));
        S7Symbol[] symbols = [symbol];
        using var catalog = symbols.CreateLogicalTagCatalog();
        var tag = catalog.List()[0];
        await TUnit.Assertions.Assert.That(tag.Address).IsEqualTo(NativePath);
        await TUnit.Assertions.Assert.That(tag.DataType).IsEqualTo("DINT[]");
        await TUnit.Assertions.Assert.That(tag.AccessMode).IsEqualTo(LogicalTagAccessMode.Read);
        await TUnit.Assertions.Assert.That(tag.Metadata["Protocol"]).IsEqualTo("S7CommPlus");
        await TUnit.Assertions.Assert.That(tag.Metadata["ArrayLowerBounds"]).IsEqualTo("-2");
        await TUnit.Assertions.Assert.That(tag.Metadata["ArrayLengths"]).IsEqualTo("5");
    }

    /// <summary>Verifies repeated names each retain a result and access failures remain isolated.</summary>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>The test task.</returns>
    [Test]
    public async Task BatchFailuresPreserveInputOrderAsync(CancellationToken cancellationToken)
    {
        await using var symbolic = S71500.CreateSymbolic("127.0.0.1");
        using var catalog = new LogicalTagCatalog();
        catalog.Upsert(new(ReadOnlyAlias, NativePath, "DINT", new LogicalTagOptions { AccessMode = LogicalTagAccessMode.Read }));
        var client = symbolic.CreateSymbolicLogicalTagClient(catalog);
        LogicalTagValue[] inputs = [
            new(MissingAlias, 1, TimeProvider.System.GetUtcNow()),
            new(ReadOnlyAlias, SecondValue, TimeProvider.System.GetUtcNow()),
            new(MissingAlias, ThirdValue, TimeProvider.System.GetUtcNow())];
        var results = await client.WriteManyAsync(inputs, cancellationToken);
        await TUnit.Assertions.Assert.That(results.Count).IsEqualTo(BatchCount);
        await TUnit.Assertions.Assert.That(results[0].Error).Contains(MissingAlias);
        await TUnit.Assertions.Assert.That(results[1].Error).Contains(ReadOnlyAlias);
        await TUnit.Assertions.Assert.That(results[SecondValue].Error).Contains(MissingAlias);
        foreach (var result in results)
        {
            await TUnit.Assertions.Assert.That(result.Succeeded).IsFalse();
        }
    }

    /// <summary>Verifies cancellation is preserved instead of becoming a failed tag result.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ReadCancellationPropagatesAsync()
    {
        await using var symbolic = S71200.CreateSymbolic("127.0.0.1");
        using var catalog = new LogicalTagCatalog();
        var client = symbolic.CreateSymbolicLogicalTagClient(catalog);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        await TUnit.Assertions.Assert.That(async () => await client.ReadAsync(MissingAlias, source.Token)).Throws<OperationCanceledException>();
    }
}
