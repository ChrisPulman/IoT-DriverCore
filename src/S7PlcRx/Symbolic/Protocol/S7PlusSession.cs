// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.IO;
#if REACTIVE_SHIM
using IoT.Driver.S7PlcRx.Reactive.Symbolic.Transport;
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic.Protocol;
#else
using IoT.Driver.S7PlcRx.Symbolic.Transport;

namespace IoT.Driver.S7PlcRx.Symbolic.Protocol;
#endif
/// <summary>Coordinates S7 plus session.</summary>
internal sealed partial class S7PlusSession : IAsyncDisposable
{
    /// <summary>The ordinary request protocol version.</summary>
    private const byte OrdinaryVersion = 2;

    /// <summary>The number of allocated session containers.</summary>
    private const int SessionIdentifierCount = 2;

    /// <summary>The third model identifier digit offset.</summary>
    private const int ThirdModelDigitOffset = 2;

    /// <summary>The fixed portion of an item address.</summary>
    private const uint AddressFieldCount = 4;

    /// <summary>The bytes in a fixed uint padding field.</summary>
    private const int PaddingLength = 4;

    /// <summary>The maximum subscription memory limit identifier.</summary>
    private const uint SubscriptionMemoryLimitId = 2;

    /// <summary>The ReadLimitId.</summary>
    private const int ReadLimitId = 1000;

    /// <summary>The WriteLimitId.</summary>
    private const int WriteLimitId = 1001;

    /// <summary>The SystemLimitsId.</summary>
    private const int SystemLimitsId = 1037;

    /// <summary>The QualifierId.</summary>
    private const int QualifierId = 1256;

    /// <summary>The QualifierRidId.</summary>
    private const int QualifierRidId = 1257;

    /// <summary>The QualifierAidId.</summary>
    private const int QualifierAidId = 1258;

    /// <summary>The QualifierValueId.</summary>
    private const int QualifierValueId = 1259;

    /// <summary>The EffectiveProtectionId.</summary>
    private const int EffectiveProtectionId = 1842;

    /// <summary>The ModernAuthenticationId.</summary>
    private const int ModernAuthenticationId = 1846;

    /// <summary>The DefaultBatchLimit.</summary>
    private const int DefaultBatchLimit = 20;

    /// <summary>The RootObjectId.</summary>
    private const int RootObjectId = 201;

    /// <summary>The AllocateObjectId.</summary>
    private const int AllocateObjectId = 211;

    /// <summary>The SubscriptionsClassId.</summary>
    private const int SubscriptionsClassId = 255;

    /// <summary>The SessionParentId.</summary>
    private const int SessionParentId = 285;

    /// <summary>The SessionClassId.</summary>
    private const int SessionClassId = 287;

    /// <summary>The BootstrapSessionId.</summary>
    private const uint BootstrapSessionId = 288U;

    /// <summary>The ClientRidId.</summary>
    private const int ClientRidId = 300;

    /// <summary>The ChallengeId.</summary>
    private const int ChallengeId = 303;

    /// <summary>The LegacyAuthenticationId.</summary>
    private const int LegacyAuthenticationId = 304;

    /// <summary>The SessionVersionId.</summary>
    private const int SessionVersionId = 306;

    /// <summary>The PaomVersionId.</summary>
    private const int PaomVersionId = 319;

    /// <summary>The ExporterLength.</summary>
    private const int ExporterLength = 32;

    /// <summary>The MaximumBatchLimit.</summary>
    private const int MaximumBatchLimit = 65_535;

    /// <summary>The constant session initialization padding.</summary>
    private static readonly byte[] InitializationPadding = new byte[PaddingLength];

    /// <summary>The constant exploration padding.</summary>
    private static readonly byte[] ExplorationPadding = new byte[PaddingLength + 1];

    /// <summary>The streamed attribute request descriptor.</summary>
    private static readonly byte[] AttributeDescriptor = [0x20, 0x04, 0x01];

    /// <summary>The controller resource limit addresses.</summary>
    private static readonly S7SymbolicAddress[] ResourceAddresses = [
        new(RootObjectId, SystemLimitsId, [ReadLimitId]),
        new(RootObjectId, SystemLimitsId, [WriteLimitId]),
        new(RootObjectId, SystemLimitsId, [0]),
        new(RootObjectId, SystemLimitsId, [1]),
        new(RootObjectId, SystemLimitsId, [SubscriptionMemoryLimitId])
    ];

    /// <summary>Stores options.</summary>
    private readonly S7SymbolicConnectionOptions _options;

    /// <summary>Stores transport.</summary>
    private readonly IS7PlusTransport _transport;

    /// <summary>Stores exchange gate.</summary>
    private readonly S7OperationGate _exchangeGate = new();

    /// <summary>Stores lifecycle gate.</summary>
    private readonly S7OperationGate _lifecycleGate = new();

    /// <summary>Cancels active connection setup when disposal begins.</summary>
    private readonly CancellationTokenSource _operationLifetime = new();

    /// <summary>Stores pending gate.</summary>
    private readonly Lock _pendingGate = new();

    /// <summary>Stores pending.</summary>
    private TaskCompletionSource<PlusResponse>? _pending;

    /// <summary>Stores receiving.</summary>
    private CancellationTokenSource? _receiving;

    /// <summary>Stores receiver.</summary>
    private Task? _receiver;

    /// <summary>Stores sequence.</summary>
    private ushort _sequence;

    /// <summary>Stores read integrity.</summary>
    private uint _readIntegrity;

    /// <summary>Stores write integrity.</summary>
    private uint _writeIntegrity;

    /// <summary>Stores disposed.</summary>
    private int _disposed;

    /// <summary>Stores disposal.</summary>
    private Task? _disposal;

    /// <summary>Stores connected.</summary>
    private bool _connected;

    /// <summary>Tracks whether the current receive lifetime has reported its terminal event.</summary>
    private int _closedReported = 1;

    /// <summary>Initializes a new instance of the <see cref = "S7PlusSession"/> class.</summary>
    /// <param name = "connectionOptions">The connectionOptions.</param>
    /// <param name = "injectedTransport">The injectedTransport.</param>
    internal S7PlusSession(S7SymbolicConnectionOptions connectionOptions, IS7PlusTransport? injectedTransport = null)
    {
        RequireNotNull(connectionOptions, nameof(connectionOptions));
        connectionOptions.Validate();
        _options = connectionOptions;
        _transport = injectedTransport ?? new S7PlusTransport(connectionOptions);
    }

    /// <summary>Handles Notification received.</summary>
    internal event EventHandler<S7PlusMessageEventArgs>? NotificationReceived;

    /// <summary>Reports the receive lifetime terminating.</summary>
    internal event EventHandler<S7PlusSessionClosedEventArgs>? ConnectionClosed;

    /// <summary>Gets Session id.</summary>
    internal uint SessionId { get; private set; }

    /// <summary>Gets Subscription session id.</summary>
    internal uint SubscriptionSessionId { get; private set; }

    /// <summary>Gets Read limit.</summary>
    internal int ReadLimit { get; private set; } = DefaultBatchLimit;

    /// <summary>Gets Write limit.</summary>
    internal int WriteLimit { get; private set; } = DefaultBatchLimit;

    /// <summary>Gets Is connected.</summary>
    internal bool IsConnected => Volatile.Read(ref _connected);

    /// <summary>Handles Exchange async.</summary>
    /// <param name = "version">The version.</param>
    /// <param name = "function">The function.</param>
    /// <param name = "flags">The flags.</param>
    /// <param name = "mutation">The mutation.</param>
    /// <param name = "withIntegrity">The withIntegrity.</param>
    /// <param name = "createBody">The createBody.</param>
    /// <param name = "cancellationToken">The cancellationToken.</param>
    /// <returns>A task representing the operation.</returns>
    internal async Task<PlusResponse> ExchangeAsync(
        byte version,
        ushort function,
        byte flags,
        bool mutation,
        bool withIntegrity,
        Func<uint, byte[]> createBody,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        using var exchangeLease = await _exchangeGate.AcquireAsync(cancellationToken).ConfigureAwait(false);
        if (_receiving?.IsCancellationRequested != false)
        {
            throw new IOException("The symbolic transport is disconnected.");
        }

        _sequence = _sequence == ushort.MaxValue ? (ushort)1 : (ushort)(_sequence + 1);
        var integrity = withIntegrity ? NextIntegrity(mutation) : 0;
        var writer = new PlusWriter();
        writer.Byte(0x31);
        writer.UInt16(0);
        writer.UInt16(function);
        writer.UInt16(0);
        writer.UInt16(_sequence);
        writer.UInt32(SessionId == 0 ? BootstrapSessionId : SessionId);
        writer.Byte(flags);
        writer.Bytes(createBody(integrity));
        uint? expectedIntegrity = withIntegrity ? unchecked((uint)_sequence + integrity) : null;
        return await ExchangeCoreAsync(new(version, writer.ToArray()), function, expectedIntegrity, _receiving.Token, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Advances the independent integrity counter for an operation.</summary>
    /// <param name = "mutation">Whether the request mutates controller state.</param>
    /// <returns>The request integrity identifier.</returns>
    private uint NextIntegrity(bool mutation)
    {
        if (mutation)
        {
            _writeIntegrity = unchecked(_writeIntegrity + 1);
            return _writeIntegrity;
        }

        _readIntegrity = unchecked(_readIntegrity + 1);
        return _readIntegrity;
    }

    /// <summary>Sends and correlates one request while the exchange gate is held.</summary>
    /// <param name = "message">The request message.</param>
    /// <param name = "function">The request function.</param>
    /// <param name = "expectedIntegrity">The response integrity, if present.</param>
    /// <param name = "lifetime">The receive loop cancellation token.</param>
    /// <param name = "cancellationToken">The caller cancellation token.</param>
    /// <returns>The correlated response.</returns>
    private async Task<PlusResponse> ExchangeCoreAsync(PlusMessage message, ushort function, uint? expectedIntegrity, CancellationToken lifetime, CancellationToken cancellationToken)
    {
        TaskCompletionSource<PlusResponse> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_pendingGate)
        {
            _pending = completion;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime);
        timeout.CancelAfter(_options.Timeout);
#if NETFRAMEWORK
        using var registration = timeout.Token.Register(static state => ((TaskCompletionSource<PlusResponse>)state!).TrySetCanceled(), completion);
#else
        await using var registration = timeout.Token.Register(
            static state =>
        {
            var request = ((TaskCompletionSource<PlusResponse> Completion, CancellationToken Token))state!;
            _ = request.Completion.TrySetCanceled(request.Token);
        },
            (completion, timeout.Token));
#endif
        try
        {
            await _transport.SendAsync(message, timeout.Token).ConfigureAwait(false);
            var response = await completion.Task.ConfigureAwait(false);
            if (response.Function != function || response.Sequence != _sequence)
            {
                throw new InvalidDataException("Response function or sequence does not match the request.");
            }

            response.ExpectedIntegrity = expectedIntegrity;
            return response;
        }
        catch
        {
            FailConnection(new IOException("The symbolic request failed; the session cannot be reused."));
            await _transport.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        finally
        {
            lock (_pendingGate)
            {
                _pending = null;
            }
        }
    }

    /// <summary>Handles Receive loop async.</summary>
    /// <param name = "cancellationToken">The cancellationToken.</param>
    /// <returns>A task representing the operation.</returns>
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                var message = await _transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                if (message.Version == 0xfe)
                {
                    throw new IOException("The controller terminated the symbolic session.");
                }

                if (message.Payload.Length > 0 && message.Payload[0] == 0x33)
                {
                    NotificationReceived?.Invoke(this, new S7PlusMessageEventArgs(message));
                    continue;
                }

                var response = new PlusResponse(message);
                lock (_pendingGate)
                {
                    if (_pending?.TrySetResult(response) != true)
                    {
                        throw new InvalidDataException("An unsolicited or duplicate response was received.");
                    }
                }

                if (response.Function == 0x05b3)
                {
                    return;
                }
            }
        }
        catch (Exception exception)
        {
            FailConnection(exception);
            await _transport.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>Handles Fail connection.</summary>
    /// <param name = "exception">The exception.</param>
    private void FailConnection(Exception exception)
    {
        Volatile.Write(ref _connected, false);
        _receiving?.Cancel();
        lock (_pendingGate)
        {
            _pending?.TrySetException(exception);
        }

        if (Interlocked.Exchange(ref _closedReported, 1) != 0)
        {
            return;
        }

        ConnectionClosed?.Invoke(this, new S7PlusSessionClosedEventArgs(exception));
    }

    /// <summary>Handles Check integrity.</summary>
    /// <param name = "response">The response.</param>
    private void CheckIntegrity(PlusResponse response)
    {
        if (response.ExpectedIntegrity is not uint expected || response.Body.VarUInt32() == expected)
        {
            return;
        }

        FailConnection(new InvalidDataException("Response integrity does not match the request."));
        throw new InvalidDataException("Response integrity does not match the request.");
    }

    /// <summary>Rejects operations after disposal has begun.</summary>
    private void ThrowIfDisposed()
    {
#if NETFRAMEWORK
        if (Volatile.Read(ref _disposed) == 0)
        {
            return;
        }

        throw new ObjectDisposedException(nameof(S7PlusSession));
#else
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
#endif
    }
}
