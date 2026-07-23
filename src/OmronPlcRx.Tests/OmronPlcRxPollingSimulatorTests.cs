// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using IoT.DriverCore.Core;
using IoT.DriverCore.OmronPlcRx.Core;
using IoT.DriverCore.OmronPlcRx.Enums;
using IoT.DriverCore.OmronPlcRx.Tags;
using ReactiveUI.Primitives;
using TUnit.Core;

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>Exercises facade polling, asynchronous SetValue, and connection delegate paths.</summary>
public sealed class OmronPlcRxPollingSimulatorTests
{
    /// <summary>Gets the local FINS node.</summary>
    private const byte LocalNode = 1;

    /// <summary>Gets the remote FINS node.</summary>
    private const byte RemoteNode = 2;

    /// <summary>Gets the deterministic request timeout.</summary>
    private const int TimeoutMilliseconds = 100;

    /// <summary>Gets the deterministic poll interval.</summary>
    private const int PollIntervalMilliseconds = 10;

    /// <summary>Gets the deterministic short value.</summary>
    private const short ShortValue = 42;

    /// <summary>Gets the expected day of week.</summary>
    private const int ExpectedDayOfWeek = 2;

    /// <summary>Verifies background polling publishes a deterministic changed tag and stops on dispose.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Driver_PollsInjectedChannelAndPublishesChangesAsync()
    {
        var channel = new CoreProtocolCoverageTests.TestChannel();
        channel.SetResponseData([0, (byte)ShortValue]);
        using var connection = CreateConnection(channel);
        var driver = new OmronPlcRx(
            connection,
            TimeSpan.FromMilliseconds(PollIntervalMilliseconds));
        var changed = new TaskCompletionSource<IPlcTag?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = driver.ObserveAll.SubscribeSafe(
            value => changed.TrySetResult(value),
            changed.SetException);
        driver.AddUpdateTagItem(new PlcTag<short>("Poll", "D100"));

        var published = await changed.Task.WaitAsync(
            TimeSpan.FromMilliseconds(TimeoutMilliseconds));
        await Task.Delay(
            TimeSpan.FromMilliseconds(PollIntervalMilliseconds * ExpectedDayOfWeek));
        driver.Dispose();
        driver.Dispose();

        await Assert.That(published?.TagName).IsEqualTo("Poll");
        await Assert.That(published?.Value).IsEqualTo(ShortValue);
        await Assert.That(driver.IsDisposed).IsTrue();
    }

    /// <summary>Verifies facade clock and cycle delegates traverse the injected FINS connection.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Driver_DelegatesClockAndCycleOperationsAsync()
    {
        var channel = new CoreProtocolCoverageTests.TestChannel();
        using var connection = CreateConnection(channel);
        using var driver = new OmronPlcRx(
            connection,
            TimeSpan.FromMilliseconds(PollIntervalMilliseconds),
            false);
        channel.SetResponseData([0x26, 0x06, 0x30, 0x14, 0x25, 0x59, ExpectedDayOfWeek]);
        var clock = await driver.ReadClockAsync(CancellationToken.None);
        channel.SetResponseData([]);
        var writeInferred = await driver.WriteClockAsync(clock.Clock, CancellationToken.None);
        var writeExplicit = await driver.WriteClockAsync(
            clock.Clock,
            ExpectedDayOfWeek,
            CancellationToken.None);
        channel.SetResponseData(
            [0x00, 0x00, 0x01, 0x23, 0x00, 0x00, 0x04, 0x56, 0x00, 0x00, 0x00, 0x00]);
        var cycle = await driver.ReadCycleTimeAsync(CancellationToken.None);

        await Assert.That(clock.DayOfWeek).IsEqualTo(ExpectedDayOfWeek);
        await Assert.That(writeInferred.PacketsSent).IsEqualTo(1);
        await Assert.That(writeExplicit.PacketsSent).IsEqualTo(1);
        await Assert.That(cycle.AverageCycleTime > 0).IsTrue();
    }

    /// <summary>Verifies fire-and-forget writes, null writes, unsupported values, and error publication.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Driver_SetValuePublishesWritesAndErrorsAsync()
    {
        var channel = new CoreProtocolCoverageTests.TestChannel();
        using var connection = CreateConnection(channel);
        using var driver = new OmronPlcRx(
            connection,
            TimeSpan.FromMilliseconds(PollIntervalMilliseconds),
            false);
        driver.AddUpdateTagItem(new PlcTag<short>("Set", "D1"));
        driver.AddUpdateTagItem(new PlcTag<string>("Null", "D2[2]"));
        driver.AddUpdateTagItem(new PlcTag<decimal>("Unsupported", "D3"));
        var errors = new TaskCompletionSource<OmronPLCException?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = driver.Errors.SubscribeSafe(
            value => errors.TrySetResult(value),
            errors.SetException);

        driver.SetValue(new LogicalTagKey<short>("Set"), ShortValue);
        await WaitUntilAsync(() => channel.SendCount > 0);
        await driver.WriteValueAsync(
            new LogicalTagKey<string>("Null"),
            null,
            CancellationToken.None);
        await driver.WriteValueAsync(
            new LogicalTagKey<decimal>("Unsupported"),
            decimal.One,
            CancellationToken.None);
        driver.AddUpdateTagItem(new PlcTag<short>("Bad", "INVALID"));
        driver.SetValue(new LogicalTagKey<short>("Bad"), ShortValue);
        var error = await errors.Task.WaitAsync(
            TimeSpan.FromMilliseconds(TimeoutMilliseconds));

        await Assert.That(channel.SendCount > 0).IsTrue();
        await Assert.That(error?.Message).Contains("Failed to write");
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(() => driver.SetValue(null!, ShortValue)));
        await AssertThrowsAsync<KeyNotFoundException>(
            () => Task.Run(() => driver.SetValue(new LogicalTagKey<short>("Missing"), ShortValue)));
    }

    /// <summary>Verifies the serial facade constructor rejects a null options object.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Driver_SerialConstructorRejectsNullOptionsAsync()
    {
        await AssertThrowsAsync<ArgumentNullException>(
            () => Task.Run(
                () => new OmronPlcRx(
                    LocalNode,
                    RemoteNode,
                    null!,
                    TimeoutMilliseconds,
                    0,
                    TimeSpan.FromMilliseconds(PollIntervalMilliseconds))));
    }

    /// <summary>Verifies the public options constructor owns its transport and polling lifecycle.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Driver_PublicOptionsConstructorRunsAndDisposesAsync()
    {
        var options = new OmronConnectionOptions(
            LocalNode,
            RemoteNode,
            ConnectionMethod.TCP,
            IPAddress.Loopback.ToString())
        {
            Port = 1,
            Timeout = PollIntervalMilliseconds,
            Retries = 0,
        };
        var driver = new OmronPlcRx(
            options,
            TimeSpan.FromMilliseconds(PollIntervalMilliseconds));
        await Task.Delay(TimeSpan.FromMilliseconds(TimeoutMilliseconds));
        var wasDisposed = driver.IsDisposed;
        driver.Dispose();

        await Assert.That(wasDisposed).IsFalse();
        await Assert.That(driver.IsDisposed).IsTrue();
    }

    /// <summary>Creates an initialized injected FINS connection.</summary>
    /// <param name="channel">Deterministic channel.</param>
    /// <returns>The injected connection.</returns>
    private static OmronPLCConnection CreateConnection(
        CoreProtocolCoverageTests.TestChannel channel) =>
        new(
            new OmronConnectionOptions(
                LocalNode,
                RemoteNode,
                ConnectionMethod.UDP,
                IPAddress.Loopback.ToString())
            {
                Timeout = TimeoutMilliseconds,
                Retries = 0,
            },
            channel,
            PlcType.CJ2,
            "CJ2M",
            "1.0",
            true);

    /// <summary>Waits until a deterministic asynchronous write completes.</summary>
    /// <param name="condition">Completion condition.</param>
    /// <returns>A task that represents the wait.</returns>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(TimeoutMilliseconds));
        while (!condition())
        {
            await Task.Delay(1, timeout.Token);
        }
    }

    /// <summary>Captures and verifies an asynchronous exception.</summary>
    /// <typeparam name="TException">Expected exception type.</typeparam>
    /// <param name="action">Action to invoke.</param>
    /// <returns>A task that represents the assertion.</returns>
    private static async Task AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException exception)
        {
            await Assert.That(exception).IsNotNull();
            return;
        }

        throw new InvalidOperationException($"Expected {nameof(TException)}.");
    }
}
