// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.Driver.MitsubishiRx.Reactive;

#else

namespace IoT.Driver.MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiReactiveWritePipeline type.</summary>
/// <typeparam name="TPayload">The TPayload type parameter.</typeparam>
public sealed class MitsubishiReactiveWritePipeline<TPayload> : IDisposable
{
    /// <summary>Stores the gate field.</summary>
#if NET9_0_OR_GREATER
    private readonly System.Threading.Lock _gate = new();
#else
    private readonly object _gate = new();
#endif

    /// <summary>Stores the scheduler field.</summary>
    private readonly IScheduler _scheduler;

    /// <summary>Stores the target field.</summary>
    private readonly string _target;

    /// <summary>Stores the writer field.</summary>
    private readonly Func<TPayload, Task<Responce>> _writer;

    /// <summary>Stores the coalescingWindow field.</summary>
    private readonly TimeSpan _coalescingWindow;

    /// <summary>Stores the queuedWrites field.</summary>
    private readonly Queue<TPayload> _queuedWrites = new();

    /// <summary>Stores the results field.</summary>
    private readonly Signal<MitsubishiReactiveWriteResult> _results = new();

    /// <summary>Stores the scheduledDrain field.</summary>
    private IDisposable? _scheduledDrain;

    /// <summary>Stores the coalescingTimer field.</summary>
    private IDisposable? _coalescingTimer;

    /// <summary>Stores the pendingLatest field.</summary>
    private TPayload? _pendingLatest;

    /// <summary>Stores the hasPendingLatest field.</summary>
    private bool _hasPendingLatest;

    /// <summary>Stores the disposed field.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the MitsubishiReactiveWritePipeline class.</summary>
    /// <param name="scheduler">The scheduler parameter.</param>
    /// <param name="target">The target parameter.</param>
    /// <param name="mode">The mode parameter.</param>
    /// <param name="writer">The writer parameter.</param>
    /// <param name="coalescingWindow">The coalescingWindow parameter.</param>
    internal MitsubishiReactiveWritePipeline(
        IScheduler scheduler,
        string target,
        MitsubishiReactiveWriteMode mode,
        Func<TPayload, Task<Responce>> writer,
        TimeSpan? coalescingWindow)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _target = target ?? throw new ArgumentNullException(nameof(target));
        Mode = mode;
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _coalescingWindow = coalescingWindow ?? TimeSpan.FromMilliseconds(MitsubishiNumericConstants.Fifty);
    }

    /// <summary>Gets or sets the Mode property.</summary>
    public MitsubishiReactiveWriteMode Mode { get; }

    /// <summary>Gets or sets the Results property.</summary>
    public IObservable<MitsubishiReactiveWriteResult> Results => _results.AsObservable();

    /// <summary>Executes the Post operation.</summary>
    /// <param name="payload">The payload parameter.</param>
    public void Post(TPayload payload)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        switch (Mode)
        {
            case MitsubishiReactiveWriteMode.Queued:
            {
                lock (_gate)
                {
                    _queuedWrites.Enqueue(payload);
                    _scheduledDrain ??= ScheduleImmediate(DrainQueuedAsync);
                }

                break;
            }

            case MitsubishiReactiveWriteMode.LatestWins:
            {
                lock (_gate)
                {
                    _pendingLatest = payload;
                    _hasPendingLatest = true;
                    _scheduledDrain ??= ScheduleImmediate(DrainLatestWinsAsync);
                }

                break;
            }

            case MitsubishiReactiveWriteMode.Coalescing:
            {
                lock (_gate)
                {
                    _pendingLatest = payload;
                    _hasPendingLatest = true;
                    _coalescingTimer?.Dispose();
                    _coalescingTimer = Observable
                        .Timer(_coalescingWindow, _scheduler)
                        .SelectAsyncSequential(_ => FlushCoalescedAsync())
                        .Subscribe(static _ => { });
                }

                break;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(Mode));
        }
    }

    /// <summary>Executes the Dispose operation.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (_gate)
        {
            _scheduledDrain?.Dispose();
            _scheduledDrain = null;
            _coalescingTimer?.Dispose();
            _coalescingTimer = null;
            _queuedWrites.Clear();
            _pendingLatest = default;
            _hasPendingLatest = false;
        }

        _results.OnCompleted();
        _results.Dispose();
    }

    /// <summary>Executes the ScheduleImmediate operation.</summary>
    /// <param name="action">The action parameter.</param>
    /// <returns>The ScheduleImmediate operation result.</returns>
    private IDisposable ScheduleImmediate(Func<Task<Unit>> action) =>
        Observable.Return(Unit.Default, _scheduler)
            .SelectAsyncSequential(_ => action())
            .Subscribe(static _ => { });

    /// <summary>Executes the DrainQueuedAsync operation.</summary>
    /// <returns>A completion value after all queued writes have been drained.</returns>
    private async Task<Unit> DrainQueuedAsync()
    {
        while (true)
        {
            TPayload payload;
            lock (_gate)
            {
                if (_queuedWrites.Count == 0)
                {
                    _scheduledDrain?.Dispose();
                    _scheduledDrain = null;
                    return Unit.Default;
                }

                payload = _queuedWrites.Dequeue();
            }

            PublishResult(await WriteAsync(payload).ConfigureAwait(false));
        }
    }

    /// <summary>Executes the DrainLatestWinsAsync operation.</summary>
    /// <returns>A completion value after the latest pending write has been processed.</returns>
    private async Task<Unit> DrainLatestWinsAsync()
    {
        TPayload payload;
        lock (_gate)
        {
            if (!_hasPendingLatest)
            {
                _scheduledDrain?.Dispose();
                _scheduledDrain = null;
                return Unit.Default;
            }

            payload = _pendingLatest!;
            _pendingLatest = default;
            _hasPendingLatest = false;
            _scheduledDrain?.Dispose();
            _scheduledDrain = null;
        }

        PublishResult(await WriteAsync(payload).ConfigureAwait(false));
        lock (_gate)
        {
            if (_hasPendingLatest && _scheduledDrain is null)
            {
                _scheduledDrain = ScheduleImmediate(DrainLatestWinsAsync);
            }
        }

        return Unit.Default;
    }

    /// <summary>Executes the FlushCoalescedAsync operation.</summary>
    /// <returns>A completion value after the coalesced write has been processed.</returns>
    private async Task<Unit> FlushCoalescedAsync()
    {
        TPayload payload;
        lock (_gate)
        {
            _coalescingTimer?.Dispose();
            _coalescingTimer = null;
            if (!_hasPendingLatest)
            {
                return Unit.Default;
            }

            payload = _pendingLatest!;
            _pendingLatest = default;
            _hasPendingLatest = false;
        }

        PublishResult(await WriteAsync(payload).ConfigureAwait(false));
        return Unit.Default;
    }

    /// <summary>Executes the WriteAsync operation.</summary>
    /// <param name="payload">The payload parameter.</param>
    /// <returns>The WriteAsync operation result.</returns>
    private async Task<MitsubishiReactiveWriteResult> WriteAsync(TPayload payload)
    {
        try
        {
            var response = await _writer(payload).ConfigureAwait(false);
            return new(
                _target,
                _scheduler.Now,
                Mode,
                response.IsSucceed,
                response.Err,
                response.ErrCode,
                response.Exception);
        }
        catch (Exception ex)
        {
            return new(
                _target,
                _scheduler.Now,
                Mode,
                false,
                ex.Message,
                Exception: ex);
        }
    }

    /// <summary>Executes the PublishResult operation.</summary>
    /// <param name="result">The result parameter.</param>
    private void PublishResult(MitsubishiReactiveWriteResult result)
    {
        if (_disposed)
        {
            return;
        }

        _results.OnNext(result);
    }
}
