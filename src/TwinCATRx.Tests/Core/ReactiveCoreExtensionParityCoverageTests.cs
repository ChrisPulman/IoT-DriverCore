// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveCoreExtensions = CP.TwinCatRx.Core.Reactive.TwinCatRxExtensions;
using ReactiveNode = CP.TwinCatRx.Core.Reactive.INodeEmulator;
using ReactiveSettings = CP.TwinCatRx.Core.Reactive.Settings;

namespace TwinCATRx.Tests.Core;

/// <summary>Closes deterministic extension parity gaps in the Reactive Core package.</summary>
public class ReactiveCoreExtensionParityCoverageTests
{
    /// <summary>The expected number of source subscriptions.</summary>
    private const int ExpectedAttemptCount = 2;

    /// <summary>The value returned after retrying.</summary>
    private const int SuccessfulValue = 17;

    /// <summary>Verifies untyped retry and typed overload/guard paths.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Retry_Overloads_And_Guards_Match_Lean_CoreAsync()
    {
        var attempts = 0;
        var source = System.Reactive.Linq.Observable.Defer(() =>
        {
            attempts++;
            return attempts == 1
                ? System.Reactive.Linq.Observable.Throw<int>(new InvalidOperationException("retry"))
                : System.Reactive.Linq.Observable.Return(SuccessfulValue);
        });
        var retried = ReactiveCoreExtensions.OnErrorRetry(source);
        var value = System.Reactive.Linq.Observable.ToEnumerable(retried).Single();

        await TUnitAssert.That(value).IsEqualTo(SuccessfulValue);
        await TUnitAssert.That(attempts).IsEqualTo(ExpectedAttemptCount);

        IObservable<int> nullSource = null!;
        var valid = System.Reactive.Linq.Observable.Return(1);
        await TUnitAssert.That(() => ReactiveCoreExtensions.OnErrorRetry(nullSource)).Throws<ArgumentNullException>();
        await TUnitAssert.That(() =>
                ReactiveCoreExtensions.OnErrorRetry<int, InvalidOperationException>(valid, null!))
            .Throws<ArgumentNullException>();
        await TUnitAssert.That(() =>
                ReactiveCoreExtensions.OnErrorRetry<int, InvalidOperationException>(
                    valid,
                    _ => { },
                    ExpectedAttemptCount,
                    TimeSpan.Zero,
                    null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>Verifies null settings guards and successful dynamic assembly loading.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Settings_And_Assembly_Helpers_Match_Lean_CoreAsync()
    {
        CP.TwinCatRx.Core.Reactive.ISettings? nullSettings = null;
        ReactiveCoreExtensions.AddNotification(nullSettings, ".Ignored");
        ReactiveCoreExtensions.AddWriteVariable(nullSettings, ".Ignored");

        var sourcePath = typeof(ReactiveSettings).Assembly.Location;
        var assemblyPath = Path.Combine(Path.GetTempPath(), $"TwinCATRx_Core_Reactive_{Guid.NewGuid()}.dll");
        try
        {
            await Task.Run(() => File.Copy(sourcePath, assemblyPath));
            var assembly = ReactiveCoreExtensions.AssemblyLoad(assemblyPath);
            var typeName = typeof(ReactiveSettings).FullName
                ?? throw new InvalidOperationException("The Reactive settings type has no full name.");
            var resolvedType = ReactiveCoreExtensions.GetType(assemblyPath, typeName);

            await TUnitAssert.That(assembly).IsNotNull();
            await TUnitAssert.That(resolvedType).IsNotNull();
        }
        finally
        {
            File.Delete(assemblyPath);
        }
    }

    /// <summary>Verifies recursive Reactive node disposal.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Node_Disposal_Matches_Lean_CoreAsync()
    {
        var nodeType = typeof(ReactiveSettings).Assembly.GetType("CP.TwinCatRx.Core.Reactive.NodeEmulator")
            ?? throw new InvalidOperationException("Reactive NodeEmulator was not found.");
        var node = Activator.CreateInstance(nodeType)
            ?? throw new InvalidOperationException("Reactive NodeEmulator could not be created.");
        var child = new DisposableNode();
        var nodes = nodeType.GetProperty("Nodes")?.GetValue(node) as HashSet<ReactiveNode>
            ?? throw new InvalidOperationException("Reactive node collection was not found.");
        _ = nodes.Add(child);
        var tag = nodeType.GetProperty("Tag") ?? throw new InvalidOperationException("Tag property was not found.");
        tag.SetValue(node, new object());

        var dispose = nodeType.GetMethod("Dispose")
            ?? throw new InvalidOperationException("Dispose method was not found.");
        _ = dispose.Invoke(node, null);
        _ = dispose.Invoke(node, null);

        await TUnitAssert.That(child.DisposeCount).IsEqualTo(1);
        await TUnitAssert.That(nodeType.GetProperty("Nodes")?.GetValue(node)).IsNull();
        await TUnitAssert.That(tag.GetValue(node)).IsNull();
    }

    /// <summary>A disposable Reactive node used to verify recursive disposal.</summary>
    private sealed class DisposableNode : ReactiveNode
    {
        /// <summary>Gets the number of dispose calls.</summary>
        public int DisposeCount { get; private set; }

        /// <inheritdoc/>
        public HashSet<ReactiveNode>? Nodes { get; } = [];

        /// <inheritdoc/>
        public object? Tag { get; set; }

        /// <inheritdoc/>
        public string Text { get; set; } = string.Empty;

        /// <inheritdoc/>
        public void Dispose() => DisposeCount++;
    }
}
