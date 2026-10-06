// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
namespace IoT.Driver.S7PlcRx.Symbolic;
#endif

/// <summary>Serializes operations and drains admitted entries before releasing synchronization resources.</summary>
internal sealed class S7OperationGate : IAsyncDisposable
{
    /// <summary>Protects admission and shutdown state.</summary>
    private readonly Lock _sync = new();

    /// <summary>Provides exclusive access to the guarded operation.</summary>
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>Cancels acquisitions that have not entered the guarded operation.</summary>
    private readonly CancellationTokenSource _closing = new();

    /// <summary>Completes when all admitted entries have returned their resources.</summary>
    private readonly TaskCompletionSource<bool> _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Shares one shutdown operation with concurrent disposal callers.</summary>
    private Task? _disposeTask;

    /// <summary>Counts active and queued entries admitted before shutdown.</summary>
    private int _outstanding;

    /// <summary>Prevents admission after shutdown begins.</summary>
    private bool _closed;

    /// <summary>Acquires exclusive access or fails when the gate is closing.</summary>
    /// <param name="cancellationToken">Cancels the caller's queued acquisition.</param>
    /// <returns>A lease that releases the admitted operation exactly once.</returns>
    internal async Task<S7OperationLease> AcquireAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            ThrowIfClosed();
            _outstanding++;
        }

        var acquired = false;
        var transferred = false;
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _closing.Token);
            await _semaphore.WaitAsync(linked.Token).ConfigureAwait(false);
            acquired = true;
            lock (_sync)
            {
                ThrowIfClosed();
            }

            var lease = new S7OperationLease(this);
            transferred = true;
            return lease;
        }
        catch (OperationCanceledException) when (_closing.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new ObjectDisposedException(nameof(S7OperationGate));
        }
        finally
        {
            if (!transferred)
            {
                CompleteEntry(acquired);
            }
        }
    }

    /// <summary>Cancels queued acquisitions and waits for admitted entries to leave.</summary>
    /// <returns>The shared shutdown operation.</returns>
    internal ValueTask DisposeAsync()
    {
        Task shutdown;
        lock (_sync)
        {
            _disposeTask ??= DisposeCoreAsync();
            shutdown = _disposeTask;
        }

        return new(shutdown);
    }

    /// <summary>Returns an active lease to the gate.</summary>
    internal void ReleaseAcquired() => CompleteEntry(true);

    /// <summary>Cancels queued acquisitions and drains active leases.</summary>
    /// <returns>The shared shutdown operation.</returns>
    ValueTask IAsyncDisposable.DisposeAsync() => DisposeAsync();

    /// <summary>Rejects an entry after shutdown admission has closed.</summary>
    private void ThrowIfClosed()
    {
#if NETFRAMEWORK
        if (!_closed)
        {
            return;
        }

        throw new ObjectDisposedException(nameof(S7OperationGate));
#else
        ObjectDisposedException.ThrowIf(_closed, this);
#endif
    }

    /// <summary>Closes admission and releases resources once every admitted entry has drained.</summary>
    /// <returns>A task completing after the gate resources have been disposed.</returns>
    private async Task DisposeCoreAsync()
    {
        _closed = true;
        if (_outstanding == 0)
        {
            _ = _drained.TrySetResult(true);
        }

#if NET8_0_OR_GREATER
        await _closing.CancelAsync().ConfigureAwait(false);
#else
        _closing.Cancel();
#endif
        await _drained.Task.ConfigureAwait(false);
        _semaphore.Dispose();
        _closing.Dispose();
    }

    /// <summary>Releases acquired access and removes the entry from shutdown accounting.</summary>
    /// <param name="acquired">Whether the entry owns a semaphore permit.</param>
    private void CompleteEntry(bool acquired)
    {
        if (acquired)
        {
            _ = _semaphore.Release();
        }

        lock (_sync)
        {
            _outstanding--;
            if (!_closed || _outstanding != 0)
            {
                return;
            }
        }

        _ = _drained.TrySetResult(true);
    }
}
