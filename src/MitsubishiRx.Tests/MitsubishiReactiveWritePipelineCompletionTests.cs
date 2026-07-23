// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive.Tests;

#else

namespace IoT.DriverCore.MitsubishiRx.Tests;

#endif

/// <summary>Completes reactive write-pipeline guard, error, and disposal coverage.</summary>
internal sealed class MitsubishiReactiveWritePipelineCompletionTests
{
    /// <summary>Stores the coalescing default window in milliseconds.</summary>
    private const int DefaultWindowMilliseconds = 50;

    /// <summary>Stores the deterministic error code.</summary>
    private const int ErrorCode = 0xC051;

    /// <summary>Stores the first test payload.</summary>
    private const int FirstPayload = 1;

    /// <summary>Stores the second test payload.</summary>
    private const int SecondPayload = 2;

    /// <summary>Stores the expected failure-path result count.</summary>
    private const int ExpectedResultCount = 3;

    /// <summary>Exercises response failures, writer exceptions, and the default coalescing window.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task FailureResultsAndDefaultCoalescingWindowArePublishedAsync()
    {
        var scheduler = new TestScheduler();
        using var failed = new MitsubishiReactiveWritePipeline<int>(
            scheduler,
            "Failed",
            MitsubishiReactiveWriteMode.Queued,
            _ => Task.FromResult(CreateFailure()),
            null);
        using var faulted = new MitsubishiReactiveWritePipeline<int>(
            scheduler,
            "Faulted",
            MitsubishiReactiveWriteMode.Queued,
            _ => throw new InvalidOperationException("writer fault"),
            null);
        using var coalesced = new MitsubishiReactiveWritePipeline<int>(
            scheduler,
            "Coalesced",
            MitsubishiReactiveWriteMode.Coalescing,
            _ => Task.FromResult(new Responce()),
            null);
        var results = new List<MitsubishiReactiveWriteResult>();
        using var first = failed.Results.Subscribe(results.Add);
        using var second = faulted.Results.Subscribe(results.Add);
        using var third = coalesced.Results.Subscribe(results.Add);

        failed.Post(FirstPayload);
        faulted.Post(FirstPayload);
        coalesced.Post(FirstPayload);
        coalesced.Post(SecondPayload);
        TestSchedulerDriver.AdvanceBy(
            scheduler,
            TimeSpan.FromMilliseconds(DefaultWindowMilliseconds).Ticks + 1);

        await Assert.That(results).Count().IsEqualTo(ExpectedResultCount);
        await Assert.That(results[0].Success).IsFalse();
        await Assert.That(results[0].ErrorCode).IsEqualTo(ErrorCode);
        await Assert.That(results[1].Exception).IsTypeOf<InvalidOperationException>();
        await Assert.That(results[2].Success).IsTrue();
    }

    /// <summary>Exercises invalid mode, constructor guards, completion, and disposed posting.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task GuardsAndDisposeCompleteThePipelineAsync()
    {
        var scheduler = new TestScheduler();
        _ = Assert.Throws<ArgumentNullException>(
            () => _ = new MitsubishiReactiveWritePipeline<int>(
                null!,
                "Target",
                MitsubishiReactiveWriteMode.Queued,
                _ => Task.FromResult(new Responce()),
                null));
        _ = Assert.Throws<ArgumentNullException>(
            () => _ = new MitsubishiReactiveWritePipeline<int>(
                scheduler,
                null!,
                MitsubishiReactiveWriteMode.Queued,
                _ => Task.FromResult(new Responce()),
                null));
        _ = Assert.Throws<ArgumentNullException>(
            () => _ = new MitsubishiReactiveWritePipeline<int>(
                scheduler,
                "Target",
                MitsubishiReactiveWriteMode.Queued,
                null!,
                null));

        var completed = false;
        using var pipeline = new MitsubishiReactiveWritePipeline<int>(
            scheduler,
            "InvalidMode",
            (MitsubishiReactiveWriteMode)int.MaxValue,
            _ => Task.FromResult(new Responce()),
            null);
        using var subscription = pipeline.Results.Subscribe(_ => { }, () => completed = true);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => pipeline.Post(FirstPayload));
        pipeline.Dispose();
        pipeline.Dispose();

        await Assert.That(completed).IsTrue();
        _ = Assert.Throws<ObjectDisposedException>(() => pipeline.Post(FirstPayload));
    }

    /// <summary>Exercises publication suppression when disposal occurs inside the writer.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task DisposingInsideWriterSuppressesLateResultAsync()
    {
        var scheduler = new TestScheduler();
        var holder = new MitsubishiReactiveWritePipeline<int>[1];
        holder[0] = new(
            scheduler,
            "DisposeInWriter",
            MitsubishiReactiveWriteMode.Queued,
            _ =>
            {
                holder[0].Dispose();
                return Task.FromResult(new Responce());
            },
            null);
        var pipeline = holder[0];
        var results = new List<MitsubishiReactiveWriteResult>();
        using var subscription = pipeline.Results.Subscribe(results.Add);

        pipeline.Post(FirstPayload);
        TestSchedulerDriver.AdvanceBy(scheduler, 1);

        await Assert.That(results).IsEmpty();
    }

    /// <summary>Creates a deterministic failed response.</summary>
    /// <returns>The failed response.</returns>
    private static Responce CreateFailure() =>
        new()
        {
            IsSucceed = false,
            Err = "simulated failure",
            ErrCode = ErrorCode,
            Exception = new InvalidOperationException("protocol failure"),
        };
}
