// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiRx type.</summary>
public sealed partial class MitsubishiRx : IDisposable, IAsyncDisposable
{
    /// <summary>Executes the RandomWriteWordsOneCAsync operation.</summary>
    /// <param name="values">The values parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The RandomWriteWordsOneCAsync operation result.</returns>
    private async Task<Responce> RandomWriteWordsOneCAsync(
        KeyValuePair<string, ushort>[] values,
        CancellationToken cancellationToken)
    {
        if (values.Length == 0)
        {
            return new Responce().Fail("At least one device value must be supplied.");
        }

        foreach (var pair in values)
        {
            var write = await WriteWordsAsync(pair.Key, [pair.Value], cancellationToken)
                .ConfigureAwait(false);
            if (!write.IsSucceed)
            {
                return write;
            }
        }

        return new Responce().EndTime();
    }

    /// <summary>Executes the RegisterMonitorOneC operation.</summary>
    /// <param name="addresses">The addresses parameter.</param>
    /// <returns>The RegisterMonitorOneC operation result.</returns>
    private Responce RegisterMonitorOneC(string[] addresses)
    {
        if (addresses.Length == 0)
        {
            return new Responce().Fail("At least one device must be supplied.");
        }

        foreach (var address in addresses)
        {
            var parsed = MitsubishiDeviceAddress.Parse(address, Options.XyNotation);
            if (parsed.Descriptor.Kind != DeviceValueKind.Word)
            {
                return new Responce().Fail(
                    $"1C monitor emulation supports word devices only; '{address}' is a bit device.");
            }
        }

        _serialOneCMonitorAddresses = addresses.ToArray();
        PublishOperation(
            "Register monitor 1C emulation",
            true,
            Array.Empty<byte>(),
            Array.Empty<byte>());
        return new Responce().EndTime();
    }

    /// <summary>Executes the ExecuteMonitorOneCAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ExecuteMonitorOneCAsync operation result.</returns>
    private async Task<Responce<byte[]>> ExecuteMonitorOneCAsync(
        CancellationToken cancellationToken)
    {
        if (_serialOneCMonitorAddresses is null || _serialOneCMonitorAddresses.Length == 0)
        {
            return new Responce<byte[]>().Fail(
                "1C monitor execution requires RegisterMonitorAsync to be called first.");
        }

        var read = await RandomReadWordsOneCAsync(_serialOneCMonitorAddresses, cancellationToken)
            .ConfigureAwait(false);
        if (!read.IsSucceed || read.Value is null)
        {
            return new Responce<byte[]>(read);
        }

        var payload = Encoding.ASCII.GetBytes(
            string.Concat(
                read.Value.Select(static value =>
                    value.ToString("X4", System.Globalization.CultureInfo.InvariantCulture))));
        return new Responce<byte[]>(read, payload);
    }

    /// <summary>Executes the ReadBlocksOneCAsync operation.</summary>
    /// <param name="request">The request parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadBlocksOneCAsync operation result.</returns>
    private async Task<Responce<byte[]>> ReadBlocksOneCAsync(
        MitsubishiBlockRequest request,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        foreach (var block in request.ResolvedWordBlocks)
        {
            var read = await ReadWordsAsync(
                    block.Address.Original,
                    block.Values.Length,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!read.IsSucceed || read.Value is null)
            {
                return new Responce<byte[]>(read);
            }

            _ = builder.Append(
                string.Concat(
                    read.Value.Select(static value =>
                        value.ToString("X4", System.Globalization.CultureInfo.InvariantCulture))));
        }

        foreach (var block in request.ResolvedBitBlocks)
        {
            var read = await ReadBitsAsync(
                    block.Address.Original,
                    block.Values.Length,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!read.IsSucceed || read.Value is null)
            {
                return new Responce<byte[]>(read);
            }

            _ = builder.Append(
                string.Concat(read.Value.Select(static value => value ? "10" : "00")));
        }

        return new Responce<byte[]>(Encoding.ASCII.GetBytes(builder.ToString())).EndTime();
    }

    /// <summary>Executes the WriteBlocksOneCAsync operation.</summary>
    /// <param name="request">The request parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteBlocksOneCAsync operation result.</returns>
    private async Task<Responce> WriteBlocksOneCAsync(
        MitsubishiBlockRequest request,
        CancellationToken cancellationToken)
    {
        foreach (var block in request.ResolvedWordBlocks)
        {
            var write = await WriteWordsAsync(
                    block.Address.Original,
                    block.Values.ToArray(),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!write.IsSucceed)
            {
                return write;
            }
        }

        foreach (var block in request.ResolvedBitBlocks)
        {
            var write = await WriteBitsAsync(
                    block.Address.Original,
                    block.Values.ToArray(),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!write.IsSucceed)
            {
                return write;
            }
        }

        return new Responce().EndTime();
    }

    /// <summary>Executes the ExecuteEncodedAsync operation.</summary>
    /// <param name="command">The command parameter.</param>
    /// <param name="expectedLength">The expectedLength parameter.</param>
    /// <param name="description">The description parameter.</param>
    /// <param name="maxRetries">The maxRetries parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ExecuteEncodedAsync operation result.</returns>
    private Task<Responce<byte[]>> ExecuteEncodedAsync(
        byte[] command,
        int? expectedLength,
        string description,
        int maxRetries = 2,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return ExecuteObservableAsync(
            () => command,
            expectedLength,
            description,
            cancellationToken,
            maxRetries);
    }

    /// <summary>Executes the ExecuteObservableAsync operation.</summary>
    /// <param name="payloadFactory">The payloadFactory parameter.</param>
    /// <param name="expectedLength">The expectedLength parameter.</param>
    /// <param name="description">The description parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <param name="maxRetries">The maxRetries parameter.</param>
    /// <returns>The ExecuteObservableAsync operation result.</returns>
    private Task<Responce<byte[]>> ExecuteObservableAsync(
        Func<byte[]> payloadFactory,
        int? expectedLength,
        string description,
        CancellationToken cancellationToken,
        int maxRetries = 2)
    {
        ArgumentNullException.ThrowIfNull(payloadFactory);
        var observable = Observable
            .Defer(() =>
                Observable.FromAsync(ct =>
                    ExecuteOnceAsync(payloadFactory, expectedLength, description, ct)))
            .RetryWithBackoff(
                maxRetries,
                TimeSpan.FromMilliseconds(MitsubishiNumericConstants.OneHundred),
                backoffFactor: 2.0,
                maxDelay: null,
                scheduler: _scheduler)
            .Catch<Responce<byte[]>, Exception>(ex =>
                Observable.Return(new Responce<byte[]>().Fail(ex.Message, exception: ex)));
        return observable.FirstAsync().ToTask(cancellationToken);
    }

    /// <summary>Executes the ExecuteOnceAsync operation.</summary>
    /// <param name="payloadFactory">The payloadFactory parameter.</param>
    /// <param name="expectedLength">The expectedLength parameter.</param>
    /// <param name="description">The description parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ExecuteOnceAsync operation result.</returns>
    private async Task<Responce<byte[]>> ExecuteOnceAsync(
        Func<byte[]> payloadFactory,
        int? expectedLength,
        string description,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
            var payload = payloadFactory();
            var request = new MitsubishiTransportRequest(payload, expectedLength, description);
            var response = await _transport
                .ExchangeAsync(request, cancellationToken)
                .ConfigureAwait(false);
            var decoded =
                Options.TransportKind == MitsubishiTransportKind.Serial
                    ? MitsubishiSerialProtocolEncoding.Decode(Options, response)
                    : MitsubishiProtocolEncoding.Decode(Options, request, response);
            decoded.Request = Convert.ToHexString(payload);
            decoded.Response = Convert.ToHexString(response);
            PublishOperation(description, decoded.IsSucceed, payload, response, decoded.Exception);
            return decoded;
        }
        catch (Exception ex)
        {
            PublishFault(description, Array.Empty<byte>(), Array.Empty<byte>(), ex);
            await _transport.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        finally
        {
            _ = _requestGate.Release();
        }
    }

    /// <summary>Executes the EnsureConnectedAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The EnsureConnectedAsync operation result.</returns>
    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_transport.IsConnected)
        {
            PublishState(MitsubishiConnectionState.Connected);
            return;
        }

        PublishState(
            _connectionStates.Value == MitsubishiConnectionState.Disconnected
                ? MitsubishiConnectionState.Connecting
                : MitsubishiConnectionState.Reconnecting);
        await _transport.ConnectAsync(Options, cancellationToken).ConfigureAwait(false);
        PublishState(MitsubishiConnectionState.Connected);
    }

    /// <summary>Executes the ParseBits operation.</summary>
    /// <param name="raw">The raw parameter.</param>
    /// <param name="expectedBitCount">The expectedBitCount parameter.</param>
    /// <returns>The ParseBits operation result.</returns>
    private Responce<bool[]> ParseBits(Responce<byte[]> raw, int expectedBitCount)
    {
        if (!raw.IsSucceed || raw.Value is null)
        {
            return new Responce<bool[]>(raw);
        }

        try
        {
            var values =
                Options.DataCode == CommunicationDataCode.Binary
                    ? ParseBinaryBits(raw.Value, expectedBitCount)
                    : ParseAsciiBits(raw.Value, expectedBitCount);
            return new Responce<bool[]>(raw, values);
        }
        catch (Exception ex)
        {
            return new Responce<bool[]>(raw).Fail(ex.Message, exception: ex);
        }
    }

    /// <summary>Executes the ParseTypeName operation.</summary>
    /// <param name="raw">The raw parameter.</param>
    /// <returns>The ParseTypeName operation result.</returns>
    private Responce<MitsubishiTypeName> ParseTypeName(Responce<byte[]> raw)
    {
        if (!raw.IsSucceed || raw.Value is null)
        {
            return new Responce<MitsubishiTypeName>(raw);
        }

        try
        {
            if (Options.DataCode == CommunicationDataCode.Binary)
            {
                var code =
                    raw.Value.Length >= 2
                        ? BitConverter.ToUInt16(raw.Value, raw.Value.Length - MitsubishiNumericConstants.Two)
                        : (ushort)0;
                var nameLength = Math.Max(0, raw.Value.Length - MitsubishiNumericConstants.Two);
                var name = System
                    .Text.Encoding.ASCII.GetString(raw.Value, 0, nameLength)
                    .TrimEnd('\0', ' ');
                return new Responce<MitsubishiTypeName>(raw, new MitsubishiTypeName(name, code));
            }

            var ascii = System.Text.Encoding.ASCII.GetString(raw.Value).Trim();
            var modelCode =
                ascii.Length >= 4
                && ushort.TryParse(
                    ascii[^MitsubishiNumericConstants.Four..],
                    System.Globalization.NumberStyles.HexNumber,
                    null,
                    out var parsed)
                    ? parsed
                    : (ushort)0;
            var modelName = ascii.Length > MitsubishiNumericConstants.Four
                ? ascii[..^MitsubishiNumericConstants.Four].Trim()
                : ascii;
            return new Responce<MitsubishiTypeName>(
                raw,
                new MitsubishiTypeName(modelName, modelCode));
        }
        catch (Exception ex)
        {
            return new Responce<MitsubishiTypeName>(raw).Fail(ex.Message, exception: ex);
        }
    }
}
