// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using ReactiveCoreExtensions = IoT.Driver.TwinCATRx.Core.Reactive.TwinCatRxExtensions;
using ReactiveNode = IoT.Driver.TwinCATRx.Core.Reactive.INodeEmulator;
using ReactiveSettings = IoT.Driver.TwinCATRx.Core.Reactive.Settings;

namespace IoT.Driver.TwinCATRx.Tests.Core;

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
        var value = GetSingleValue(System.Reactive.Linq.Observable.ToEnumerable(retried));

        await TUnitAssert.That(value).IsEqualTo(SuccessfulValue);
        await TUnitAssert.That(attempts).IsEqualTo(ExpectedAttemptCount);

        await TUnitAssert.That(static () => ReactiveCoreExtensions.OnErrorRetry((IObservable<int>)null!)).Throws<ArgumentNullException>();
        await TUnitAssert.That(static () =>
                ReactiveCoreExtensions.OnErrorRetry<int, InvalidOperationException>(System.Reactive.Linq.Observable.Return(1), null!))
            .Throws<ArgumentNullException>();
        await TUnitAssert.That(static () =>
                ReactiveCoreExtensions.OnErrorRetry<int, InvalidOperationException>(
                    System.Reactive.Linq.Observable.Return(1),
                    static _ => { },
                    ExpectedAttemptCount,
                    TimeSpan.Zero,
                    null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>Verifies null settings guards and path-based dynamic assembly loading.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Settings_And_Assembly_Helpers_Match_Lean_CoreAsync()
    {
        IoT.Driver.TwinCATRx.Core.Reactive.ISettings? nullSettings = null;
        ReactiveCoreExtensions.AddNotification(nullSettings, ".Ignored");
        ReactiveCoreExtensions.AddWriteVariable(nullSettings, ".Ignored");

        var directory = Path.Combine(AppContext.BaseDirectory, "AssemblyLoadPathTests");
        _ = Directory.CreateDirectory(directory);
        var generatedTypeName = $"GeneratedReactiveType_{Guid.NewGuid():N}";
        var assemblyPath = Path.Combine(directory, $"{generatedTypeName}.dll");
        using var generator = new IoT.Driver.TwinCATRx.Core.Reactive.CodeGenerator();
        await TUnitAssert.That(generator.CreateDll($"public sealed class {generatedTypeName} {{ }}", assemblyPath)).IsTrue();
        var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);

#if NET8_0_OR_GREATER
        await TUnitAssert.That(() => System.Reflection.Assembly.Load(assemblyName)).Throws<FileNotFoundException>();
#else
        _ = assemblyName;
#endif

        var assembly = ReactiveCoreExtensions.AssemblyLoad(assemblyPath);
        var resolvedType = ReactiveCoreExtensions.GetType(assemblyPath, generatedTypeName);

        await TUnitAssert.That(assembly).IsNotNull();
        await TUnitAssert.That(resolvedType).IsNotNull();
        await TUnitAssert.That(assembly?.Location).IsEqualTo(Path.GetFullPath(assemblyPath));
    }

    /// <summary>Verifies recursive Reactive node disposal.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Node_Disposal_Matches_Lean_CoreAsync()
    {
        var nodeType = typeof(ReactiveSettings).Assembly.GetType("IoT.Driver.TwinCATRx.Core.Reactive.NodeEmulator")
            ?? throw new InvalidOperationException("Reactive NodeEmulator was not found.");
        var node = Activator.CreateInstance(nodeType)
            ?? throw new InvalidOperationException("Reactive NodeEmulator could not be created.");
        var child = new DisposableNode();
        var nodes = nodeType.GetProperty("Nodes")?.GetValue(node) as HashSet<ReactiveNode>
            ?? throw new InvalidOperationException("Reactive node collection was not found.");
        _ = nodes.Add(child);
        var tag = nodeType.GetProperty("Tag") ?? throw new InvalidOperationException("Tag property was not found.");
        tag.SetValue(node, new());

        var dispose = nodeType.GetMethod("Dispose")
            ?? throw new InvalidOperationException("Dispose method was not found.");
        _ = dispose.Invoke(node, null);
        _ = dispose.Invoke(node, null);

        await TUnitAssert.That(child.DisposeCount).IsEqualTo(1);
        await TUnitAssert.That(nodeType.GetProperty("Nodes")?.GetValue(node)).IsNull();
        await TUnitAssert.That(tag.GetValue(node)).IsNull();
    }

    /// <summary>Returns the sole value from a finite sequence.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="values">The sequence to inspect.</param>
    /// <returns>The only value.</returns>
    private static T GetSingleValue<T>(IEnumerable<T> values)
    {
        using var enumerator = values.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            throw new InvalidOperationException("The sequence was empty.");
        }

        var value = enumerator.Current;
        if (enumerator.MoveNext())
        {
            throw new InvalidOperationException("The sequence contained multiple values.");
        }

        return value;
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
