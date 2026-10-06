// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.Driver.S7PlcRx.Symbolic;
using TUnitAssert = TUnit.Assertions.Assert;

namespace IoT.Driver.S7PlcRx.Tests.Symbolic;

/// <summary>Verifies operation admission and queued client shutdown without timing sleeps.</summary>
public sealed class S7OperationGateTests
{
    /// <summary>Checks that a queued resolver is rejected while both disposal callers wait for an active lease.</summary>
    /// <param name="cancellationToken">Cancels the test if its bounded timeout expires.</param>
    /// <returns>A task completing after the queued operation and both disposals have completed.</returns>
    [Test]
    [Timeout(10_000)]
    public async Task ClientDisposalCancelsQueuedResolverAndSharesPendingShutdown(CancellationToken cancellationToken)
    {
        await using var gate = new S7OperationGate();
        await using var client = new S7SymbolicClient(new("unit-test.invalid"), gate);
        using var active = await gate.AcquireAsync(cancellationToken).ConfigureAwait(false);
        const string path = "DB.Value";
        var queued = client.ResolveAsync(path, cancellationToken);
        await TUnitAssert.That(queued.IsCompleted).IsFalse();
        var first = client.DisposeAsync().AsTask();
        var second = client.DisposeAsync().AsTask();
        try
        {
            await TUnitAssert.That(first.IsCompleted).IsFalse();
            await TUnitAssert.That(second.IsCompleted).IsFalse();
            await TUnitAssert.That(async () => await queued.ConfigureAwait(false)).Throws<ObjectDisposedException>();
        }
        finally
        {
            active.Dispose();
        }

        await first.ConfigureAwait(false);
        await second.ConfigureAwait(false);
        await TUnitAssert.That(async () => await client.ResolveAsync(path, cancellationToken).ConfigureAwait(false)).Throws<ObjectDisposedException>();
    }

    /// <summary>Checks that caller cancellation removes a queued entry without consuming the active permit.</summary>
    /// <param name="cancellationToken">Cancels the test if its bounded timeout expires.</param>
    /// <returns>A task completing after cancellation and a subsequent successful acquisition.</returns>
    [Test]
    [Timeout(10_000)]
    public async Task CallerCancellationDoesNotConsumeTheActivePermit(CancellationToken cancellationToken)
    {
        await using var gate = new S7OperationGate();
        using var active = await gate.AcquireAsync(cancellationToken).ConfigureAwait(false);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var queued = gate.AcquireAsync(cancellation.Token);
#if NETFRAMEWORK
        cancellation.Cancel();
#else
        await cancellation.CancelAsync().ConfigureAwait(false);
#endif
        await TUnitAssert.That(async () => await queued.ConfigureAwait(false)).Throws<OperationCanceledException>();
        active.Dispose();
        using var subsequent = await gate.AcquireAsync(cancellationToken).ConfigureAwait(false);
        await TUnitAssert.That(subsequent).IsNotNull();
    }
}
