// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.IO;
using System.Text;

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic.Protocol;
#else
namespace IoT.Driver.S7PlcRx.Symbolic.Protocol;
#endif
/// <summary>Coordinates S7 plus session.</summary>
internal sealed partial class S7PlusSession
{
    /// <summary>Handles Dispose async.</summary>
    /// <returns>A task representing the operation.</returns>
    public ValueTask DisposeAsync()
    {
        TaskCompletionSource<bool>? completion = null;
        Task disposal;
        lock (_pendingGate)
        {
            if (_disposal is null)
            {
                _ = Interlocked.Exchange(ref _disposed, 1);
                completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _disposal = completion.Task;
            }

            disposal = _disposal;
        }

        if (completion is not null)
        {
            _ = DisposeAndReportAsync(completion);
        }

        return new(disposal);
    }

    /// <summary>Handles Connect async.</summary>
    /// <param name = "cancellationToken">The cancellationToken.</param>
    /// <returns>A task representing the operation.</returns>
    internal async Task ConnectAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _operationLifetime.Token);
        using var lifecycleLease = await _lifecycleGate.AcquireAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken = operation.Token;
        if (IsConnected)
        {
            return;
        }

        await StopReceivingAsync().ConfigureAwait(false);
        _sequence = 0;
        _readIntegrity = 0;
        _writeIntegrity = 0;
        SessionId = 0;
        SubscriptionSessionId = 0;
        ReadLimit = DefaultBatchLimit;
        WriteLimit = DefaultBatchLimit;
        await _transport.ConnectAsync(cancellationToken).ConfigureAwait(false);
        _receiving = new();
        _ = Interlocked.Exchange(ref _closedReported, 0);
        _receiver = ReceiveLoopAsync(_receiving.Token);
        try
        {
            await InitializeTlsAsync(cancellationToken).ConfigureAwait(false);
            var serverVersion = await AllocateSessionAsync(cancellationToken).ConfigureAwait(false);
            await SetSessionVersionAsync(serverVersion, cancellationToken).ConfigureAwait(false);
            await ReadResourceLimitsAsync(cancellationToken).ConfigureAwait(false);
            await AuthenticateAsync(serverVersion, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _connected, true);
        }
        catch
        {
            await StopReceivingAsync().ConfigureAwait(false);
            await _transport.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Handles Disconnect async.</summary>
    /// <param name = "cancellationToken">The cancellationToken.</param>
    /// <returns>A task representing the operation.</returns>
    internal async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _operationLifetime.Token);
        using var lifecycleLease = await _lifecycleGate.AcquireAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken = operation.Token;
        try
        {
            if (IsConnected)
            {
                await DeleteObjectAsync(SessionId, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            await StopReceivingAsync().ConfigureAwait(false);
            await _transport.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
            SessionId = 0;
            SubscriptionSessionId = 0;
        }
    }

    /// <summary>Upgrades the naturally paused initialization stream to TLS.</summary>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the initialization.</returns>
    private async Task InitializeTlsAsync(CancellationToken cancellationToken)
    {
        var init = await ExchangeAsync(
            1,
            0x05b3,
            0x30,
            false,
            false,
            static _ => InitializationPadding,
            cancellationToken).ConfigureAwait(false);
        CheckReturn(init.Body);
        if (_receiver is not null)
        {
            await _receiver.ConfigureAwait(false);
        }

        await _transport.UpgradeToTlsAsync(cancellationToken).ConfigureAwait(false);
        _receiving?.Dispose();
        _receiving = new();
        _receiver = ReceiveLoopAsync(_receiving.Token);
    }

    /// <summary>Allocates the controller session and subscription containers.</summary>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The controller session version.</returns>
    private async Task<S7SymbolicValue> AllocateSessionAsync(CancellationToken cancellationToken)
    {
        var sessionObject = new PlusObject(AllocateObjectId, SessionClassId);
        sessionObject.Attributes.Add(ClientRidId, new(S7SymbolicDataType.RID, 0x80c3c901U));
        sessionObject.Children.Add(new(AllocateObjectId, SubscriptionsClassId));
        var created = await ExchangeAsync(
            1,
            0x04ca,
            0x36,
            true,
            false,
            _ => CreateBody(SessionParentId, sessionObject, 0, false),
            cancellationToken).ConfigureAwait(false);
        CheckReturn(created.Body);
        var ids = ReadObjectIds(created.Body);
        if (ids.Length < SessionIdentifierCount || ids[0] == 0 || ids[1] == 0)
        {
            throw new InvalidDataException("Controller did not allocate both session identifiers.");
        }

        SessionId = ids[0];
        SubscriptionSessionId = ids[1];
        var server = PlusObject.ReadFrom(created.Body);
        if (!server.Attributes.TryGetValue(SessionVersionId, out var serverVersion))
        {
            throw new InvalidDataException("Controller omitted its session version.");
        }

        return serverVersion;
    }

    /// <summary>Echoes the controller version into the session setup field.</summary>
    /// <param name = "serverVersion">The controller version value.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the setup request.</returns>
    private async Task SetSessionVersionAsync(S7SymbolicValue serverVersion, CancellationToken cancellationToken)
    {
        var setup = await ExchangeAsync(
            OrdinaryVersion,
            0x0542,
            0x34,
            true,
            false,
            _ =>
        {
            var writer = new PlusWriter();
            writer.UInt32(SessionId);
            writer.VarUInt32(1);
            writer.VarUInt32(1);
            writer.VarUInt32(SessionVersionId);
            writer.VarUInt32(1);
            serverVersion.WriteTo(writer);
            writer.Byte(0);
            Qualifier(writer);
            writer.UInt32(0);
            return writer.ToArray();
        },
            cancellationToken).ConfigureAwait(false);
        CheckReturn(setup.Body);
    }

    /// <summary>Reads the resource limits used to bound request batches.</summary>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the resource discovery.</returns>
    private async Task ReadResourceLimitsAsync(CancellationToken cancellationToken)
    {
        var limits = await ReadAsync(ResourceAddresses, cancellationToken).ConfigureAwait(false);
        if (limits[0].Value is S7SymbolicValue readLimit)
        {
            ReadLimit = PositiveLimit(readLimit);
        }

        if (limits[1].Value is not S7SymbolicValue writeLimit)
        {
            return;
        }

        WriteLimit = PositiveLimit(writeLimit);
    }

    /// <summary>Handles Dispose core async.</summary>
    /// <returns>A task representing the operation.</returns>
    private async Task DisposeCoreAsync()
    {
        var lifecycleClosing = _lifecycleGate.DisposeAsync().AsTask();
        var exchangeClosing = _exchangeGate.DisposeAsync().AsTask();
        try
        {
#if NETFRAMEWORK
            _operationLifetime.Cancel();
#else
            await _operationLifetime.CancelAsync().ConfigureAwait(false);
#endif
            FailConnection(new OperationCanceledException("The symbolic session was disposed."));
            await Task.WhenAll(lifecycleClosing, exchangeClosing).ConfigureAwait(false);
            await StopReceivingAsync().ConfigureAwait(false);
        }
        finally
        {
            _receiving?.Dispose();
            _receiving = null;
            try
            {
                await _transport.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                _operationLifetime.Dispose();
            }
        }
    }

    /// <summary>Reports all cleanup completion or failure through the shared disposal task.</summary>
    /// <param name="completion">The completion shared by disposal callers.</param>
    /// <returns>A task representing the cleanup report.</returns>
    private async Task DisposeAndReportAsync(TaskCompletionSource<bool> completion)
    {
        try
        {
            await DisposeCoreAsync().ConfigureAwait(false);
            _ = completion.TrySetResult(true);
        }
        catch (Exception exception)
        {
            _ = completion.TrySetException(exception);
        }
    }

    /// <summary>Handles Stop receiving async.</summary>
    /// <returns>A task representing the operation.</returns>
    private async Task StopReceivingAsync()
    {
        Volatile.Write(ref _connected, false);
        FailConnection(new OperationCanceledException("The symbolic session was disconnected."));
        if (_receiver is not null)
        {
            await _receiver.ConfigureAwait(false);
            _receiver = null;
        }

        if (Volatile.Read(ref _disposed) != 0)
        {
            await _exchangeGate.DisposeAsync().ConfigureAwait(false);
            DisposeReceivingToken();
            return;
        }

        using var exchangeDrain = await _exchangeGate.AcquireAsync(CancellationToken.None).ConfigureAwait(false);
        DisposeReceivingToken();
    }

    /// <summary>Releases the stopped receive loop cancellation source.</summary>
    private void DisposeReceivingToken()
    {
        _receiving?.Dispose();
        _receiving = null;
    }

    /// <summary>Handles Authenticate async.</summary>
    /// <param name = "serverVersion">The serverVersion.</param>
    /// <param name = "cancellationToken">The cancellationToken.</param>
    /// <returns>A task representing the operation.</returns>
    private async Task AuthenticateAsync(S7SymbolicValue serverVersion, CancellationToken cancellationToken)
    {
        var protection = await GetAttributeCoreAsync(SessionId, EffectiveProtectionId, true, cancellationToken).ConfigureAwait(false);
        if (Convert.ToUInt32(protection.Value, System.Globalization.CultureInfo.InvariantCulture) <= 1 || _options.Password.Length == 0)
        {
            return;
        }

        var challengeValue = await GetAttributeCoreAsync(SessionId, ChallengeId, true, cancellationToken).ConfigureAwait(false);
        var challenge = challengeValue.Value switch
        {
            byte[] bytes => bytes,
            object?[] values => ChallengeBytes(values),
            _ => Array.Empty<byte>(),
        };
        if (challenge.Length != DefaultBatchLimit)
        {
            throw new InvalidDataException("Controller authentication challenge must contain 20 bytes.");
        }

        var modern = _options.AuthenticationMode == S7SymbolicAuthenticationMode.Modern ||
            (_options.AuthenticationMode == S7SymbolicAuthenticationMode.Auto && UsesModernAuthentication(serverVersion));
        var encodedCredential = Encoding.UTF8.GetBytes(_options.Password);
        byte[] response;
        uint attribute;
        S7SymbolicValue responseValue;
        if (modern)
        {
            var exporter = _transport.ExportKeyingMaterial("EXPERIMENTAL_OMS", ExporterLength);
            try
            {
                response = S7PlusAuthentication.Modern(exporter, challenge, Encoding.UTF8.GetBytes(_options.Username), encodedCredential);
            }
            finally
            {
                Array.Clear(exporter, 0, exporter.Length);
                Array.Clear(encodedCredential, 0, encodedCredential.Length);
            }

            attribute = ModernAuthenticationId;
            responseValue = new(S7SymbolicDataType.Blob, new S7SymbolicBlob(0, response));
        }
        else
        {
            response = S7PlusAuthentication.Legacy(encodedCredential, challenge);
            Array.Clear(encodedCredential, 0, encodedCredential.Length);
            attribute = LegacyAuthenticationId;
            responseValue = new(S7SymbolicDataType.USInt, response, 0x10);
        }

        try
        {
            await SetAttributeCoreAsync(SessionId, attribute, responseValue, true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Array.Clear(response, 0, response.Length);
        }
    }
}
