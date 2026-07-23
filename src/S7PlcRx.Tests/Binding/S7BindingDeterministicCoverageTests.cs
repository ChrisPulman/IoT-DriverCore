// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.S7PlcRx.Binding;

namespace IoT.DriverCore.S7PlcRx.Tests.Binding;

/// <summary>Provides deterministic S7 binding and observable-adapter coverage.</summary>
public sealed class S7BindingDeterministicCoverageTests
{
    /// <summary>Defines the bound tag name.</summary>
    private const string TagName = "Value";

    /// <summary>Defines the bound tag address.</summary>
    private const string TagAddress = "DB1.DBW0";

    /// <summary>Defines the written value.</summary>
    private const ushort WrittenValue = 12;

    /// <summary>Verifies binding registration, writes, rebinding, invalid definitions, and disposal paths.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuntimeBindingRegistersWritesRebindsAndValidatesAsync()
    {
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        var definitions = new[]
        {
            new S7TagDefinition(TagName, TagAddress, typeof(ushort), 0, S7TagDirection.ReadWrite, 1),
        };
        var applied = new List<object?>();
        using var binding = S7TagRuntimeBinding.Bind(plc, definitions, (_, value) => applied.Add(value));
        using var rebound = S7TagRuntimeBinding.Bind(plc, definitions, (_, value) => applied.Add(value));

        binding.Write(TagName, WrittenValue);
        binding.Write("Missing", WrittenValue);
        binding.Dispose();
        binding.Write(TagName, WrittenValue);

        await TUnit.Assertions.Assert.That(applied.Count).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(
                () => S7TagRuntimeBinding.Bind(plc, [new S7TagDefinition("Bad", "MW0", typeof(int), 0, S7TagDirection.ReadWrite, 1)], (_, _) => { }))
            .Throws<ArgumentException>();
        await TUnit.Assertions.Assert.That(
                () => S7TagRuntimeBinding.Bind(null!, definitions, (_, _) => { }))
            .Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(
                () => S7TagRuntimeBinding.Bind(plc, [new S7TagDefinition("Bit", "DB1.DBX0.8", typeof(bool), 0, S7TagDirection.ReadWrite, 1)], (_, _) => { }))
            .Throws<ArgumentOutOfRangeException>();
        await TUnit.Assertions.Assert.That(
                () => S7TagRuntimeBinding.Bind(plc, [new S7TagDefinition(nameof(Type), "DB1.DBQ0", typeof(int), 0, S7TagDirection.ReadWrite, 1)], (_, _) => { }))
            .Throws<ArgumentException>();
    }

    /// <summary>Verifies async observable adaptation projects values, completion, cancellation, null, and errors.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObservableAdapterProjectsValuesCompletionCancellationAndErrorsAsync()
    {
        var values = S7TagObservableAdapter.ToAsyncEnumerable(Observable.Return(WrittenValue));
        await using var enumerator = values.GetAsyncEnumerator();
        var moved = await enumerator.MoveNextAsync();
        var completed = await enumerator.MoveNextAsync();
        using var cancellation = new CancellationTokenSource();
        await AsyncCompatibility.CancelAsync(cancellation);
        await using var canceled = values.GetAsyncEnumerator(cancellation.Token);
        var failure = S7TagObservableAdapter.ToAsyncEnumerable(Observable.Throw<ushort>(new InvalidOperationException("fault")));
        await using var failed = failure.GetAsyncEnumerator();

        await TUnit.Assertions.Assert.That(moved).IsTrue();
        await TUnit.Assertions.Assert.That(enumerator.Current).IsEqualTo(WrittenValue);
        await TUnit.Assertions.Assert.That(completed).IsFalse();
        await TUnit.Assertions.Assert.That(() => canceled.MoveNextAsync().AsTask()).Throws<OperationCanceledException>();
        await TUnit.Assertions.Assert.That(() => failed.MoveNextAsync().AsTask()).Throws<InvalidOperationException>();
        await TUnit.Assertions.Assert.That(() => S7TagObservableAdapter.ToAsyncEnumerable<ushort>(null!))
            .Throws<ArgumentNullException>();
    }
}
