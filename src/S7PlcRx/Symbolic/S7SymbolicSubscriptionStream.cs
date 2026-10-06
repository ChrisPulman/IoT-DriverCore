// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
#if REACTIVE_SHIM
using IoT.Driver.S7PlcRx.Reactive.Symbolic.Protocol;
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
using IoT.Driver.S7PlcRx.Symbolic.Protocol;

namespace IoT.Driver.S7PlcRx.Symbolic;
#endif

/// <summary>Handles the s7symbolic subscription stream.</summary>
/// <typeparam name="T">The streamed value type.</typeparam>
internal sealed class S7SymbolicSubscriptionStream<T> : IAsyncDisposable
{
    /// <summary>The NotificationBufferCapacity protocol constant.</summary>
    private const int NotificationBufferCapacity = 64;

    /// <summary>The MaximumSubscriptionItems protocol constant.</summary>
    private const int MaximumSubscriptionItems = 1_024;

    /// <summary>The CreditLimitAttribute protocol constant.</summary>
    private const int CreditLimitAttribute = 1_053;

    /// <summary>The CreditStep protocol constant.</summary>
    private const int CreditStep = 5;

    /// <summary>The InitialCreditLimit protocol constant.</summary>
    private const int InitialCreditLimit = 10;

    /// <summary>The CreditModulus protocol constant.</summary>
    private const int CreditModulus = 255;

    /// <summary>The session.</summary>
    private readonly Func<uint, CancellationToken, Task> _deleteObject;

    /// <summary>The optional session connection owner.</summary>
    private readonly S7PlusSession? _session;

    /// <summary>The responsive credit update operation.</summary>
    private readonly Func<uint, short, CancellationToken, Task> _setCredit;

    /// <summary>The notification unregistration operation.</summary>
    private readonly Action<EventHandler<S7PlusMessageEventArgs>> _detach;

    /// <summary>The project.</summary>
    private readonly Func<PlusNotification, IEnumerable<T>> _project;

    /// <summary>The notifications.</summary>
    private readonly S7SymbolicSubscriptionQueue<PlusMessage> _notifications = new(NotificationBufferCapacity);

    /// <summary>The output.</summary>
    private readonly S7SymbolicSubscriptionQueue<T> _output = new(MaximumSubscriptionItems);

    /// <summary>The lifetime.</summary>
    private readonly CancellationTokenSource _lifetime;

    /// <summary>The activation.</summary>
    private readonly TaskCompletionSource<uint> _activation = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The pump.</summary>
    private readonly Task _pump;

    /// <summary>The disposal.</summary>
    private readonly TaskCompletionSource<bool> _disposal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The enumerated.</summary>
    private int _enumerated;

    /// <summary>The disposed.</summary>
    private int _disposed;

    /// <summary>The active id.</summary>
    private uint _activeId;

    /// <summary>The terminal PLC or cleanup failure.</summary>
    private Exception? _terminationFailure;

    /// <summary>Initializes a new instance of the <see cref="S7SymbolicSubscriptionStream{T}"/> class.</summary>
    /// <param name="session">The session.</param>
    /// <param name="project">The project.</param>
    /// <param name="ct">The ct.</param>
    internal S7SymbolicSubscriptionStream(S7PlusSession session, Func<PlusNotification, IEnumerable<T>> project, CancellationToken ct)
        : this(
            handler => session.NotificationReceived += handler,
            handler => session.NotificationReceived -= handler,
            session.DeleteObjectAsync,
            (id, credit, token) => session.SetAttributeAsync(id, CreditLimitAttribute, new(S7SymbolicDataType.Int, credit), token),
            project,
            ct)
    {
        _session = session;
        session.ConnectionClosed += OnConnectionClosed;
        if (session.IsConnected)
        {
            return;
        }

        Fail(new IOException("The PLC session is disconnected."));
    }

    /// <summary>Initializes a new instance of the <see cref="S7SymbolicSubscriptionStream{T}"/> class.</summary>
    /// <param name="attach">Registers the notification handler.</param>
    /// <param name="detach">Unregisters the notification handler.</param>
    /// <param name="deleteObject">Deletes the allocated PLC object.</param>
    /// <param name="setCredit">Updates the PLC credit limit.</param>
    /// <param name="project">Maps notifications to public values.</param>
    /// <param name="ct">Cancels the subscription lifetime.</param>
    internal S7SymbolicSubscriptionStream(
        Action<EventHandler<S7PlusMessageEventArgs>> attach,
        Action<EventHandler<S7PlusMessageEventArgs>> detach,
        Func<uint, CancellationToken, Task> deleteObject,
        Func<uint, short, CancellationToken, Task> setCredit,
        Func<PlusNotification, IEnumerable<T>> project,
        CancellationToken ct)
    {
        _detach = detach;
        _deleteObject = deleteObject;
        _setCredit = setCredit;
        _project = project;
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(ct);
        attach(OnNotification);
        _pump = PumpAsync();
    }

    /// <summary>Handles the dispose async.</summary>
    /// <returns>The operation result.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            try
            {
#if NET8_0_OR_GREATER
                await _lifetime.CancelAsync().ConfigureAwait(false);
#else
                _lifetime.Cancel();
#endif
                _ = _activation.TrySetResult(0);
                await _pump.ConfigureAwait(false);
                _lifetime.Dispose();
                if (_terminationFailure is not null)
                {
                    _ = _disposal.TrySetException(_terminationFailure);
                }
                else
                {
                    _ = _disposal.TrySetResult(true);
                }
            }
            catch (Exception ex)
            {
                _ = _disposal.TrySetException(ex);
            }
        }

        await _disposal.Task.ConfigureAwait(false);
    }

    /// <summary>Handles the activate.</summary>
    /// <param name="id">The id.</param>
    internal void Activate(uint id)
    {
        Volatile.Write(ref _activeId, id);
        _ = _activation.TrySetResult(id);
    }

    /// <summary>Handles the read all.</summary>
    /// <param name="ct">The ct.</param>
    /// <returns>The operation result.</returns>
    internal async IAsyncEnumerable<T> ReadAll([EnumeratorCancellation] CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref _enumerated, 1) != 0)
        {
            throw new InvalidOperationException("A subscription supports one consumer.");
        }

        try
        {
            while (true)
            {
                T item;
                try
                {
                    item = await _output.TakeAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    yield break;
                }

                yield return item;
            }
        }
        finally
        {
            await DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Terminates queued consumers when the PLC connection fails.</summary>
    /// <param name="error">The connection failure.</param>
    internal void Fail(Exception error)
    {
        _notifications.Complete(error);
        _output.Complete(error);
    }

    /// <summary>Observes the session terminal receive error.</summary>
    /// <param name="sender">The failed session.</param>
    /// <param name="args">The terminal connection error.</param>
    private void OnConnectionClosed(object? sender, S7PlusSessionClosedEventArgs args) => Fail(args.Exception);

    /// <summary>Handles the on notification.</summary>
    /// <param name="sender">The session publishing the notification.</param>
    /// <param name="args">The received message.</param>
    private void OnNotification(object? sender, S7PlusMessageEventArgs args)
    {
        var message = args.Message;
        var current = Volatile.Read(ref _activeId);
        if (current != 0 && message.Payload.Length >= CreditStep)
        {
            var reader = new PlusReader(message.Payload);
            _ = reader.Byte();
            if (reader.UInt32() != current)
            {
                return;
            }
        }

        _ = _notifications.Publish(message);
    }

    /// <summary>Handles the pump async.</summary>
    /// <returns>The operation result.</returns>
    private async Task PumpAsync()
    {
        uint id = 0;
        Exception? failure = null;
        try
        {
            id = await _activation.Task.ConfigureAwait(false);
            _lifetime.Token.ThrowIfCancellationRequested();
            var nextCredit = InitialCreditLimit;
            while (true)
            {
                var message = await _notifications.TakeAsync(_lifetime.Token).ConfigureAwait(false);
                var notification = PlusNotification.Parse(message);
                if (notification.ObjectId != id)
                {
                    continue;
                }

                foreach (var item in _project(notification))
                {
                    if (!_output.Publish(item))
                    {
                        throw new InvalidDataException("The subscription consumer exceeded the bounded value buffer.");
                    }
                }

                if ((nextCredit - notification.CreditTick + CreditModulus) % CreditModulus <= 1)
                {
                    nextCredit = (nextCredit + CreditStep) % CreditModulus;

                    // Disposal drains this bounded session exchange before deleting the subscription.
                    await _setCredit(id, (short)nextCredit, CancellationToken.None).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested || _session is { IsConnected: false })
        {
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            _detach(OnNotification);
            _session?.ConnectionClosed -= OnConnectionClosed;

            failure = await DeleteSubscriptionAsync(id, failure).ConfigureAwait(false);
            _notifications.Complete();
            _terminationFailure = failure;
            _output.Complete(failure);
        }
    }

    /// <summary>Releases the PLC object while preserving the original terminal error.</summary>
    /// <param name="id">The allocated subscription identifier.</param>
    /// <param name="failure">The original terminal error.</param>
    /// <returns>The original or cleanup failure.</returns>
    private async Task<Exception?> DeleteSubscriptionAsync(uint id, Exception? failure)
    {
        if (id == 0 || _session?.IsConnected is false)
        {
            return failure;
        }

        try
        {
            await _deleteObject(id, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            failure ??= ex;
        }

        return failure;
    }
}
