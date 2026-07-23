// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiRx type.</summary>
public sealed partial class MitsubishiRx : IDisposable, IAsyncDisposable
{
    /// <summary>Executes the ValidateTagGroupWrite operation.</summary>
    /// <param name="groupName">The groupName parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <returns>The ValidateTagGroupWrite operation result.</returns>
    public Responce ValidateTagGroupWrite(
        string groupName,
        IReadOnlyDictionary<string, object?> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);
        ArgumentNullException.ThrowIfNull(values);
        var result = new Responce();
        var database = TagDatabase;
        if (database is null)
        {
            return result.Fail(
                MitsubishiMessages.TagDatabaseRequiredForGroupedWrites);
        }

        var group = database.GetRequiredGroup(groupName);
        var allowed = new HashSet<string>(group.ResolvedTagNames, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in values)
        {
            if (!allowed.Contains(pair.Key))
            {
                result.IsSucceed = false;
                result.ErrList.Add($"Group '{groupName}' does not contain tag '{pair.Key}'.");
                continue;
            }

            var tag = database.GetRequired(pair.Key);
            if (!CanWriteTagValue(tag, pair.Value, out var error))
            {
                result.IsSucceed = false;
                result.ErrList.Add(error!);
            }
        }

        if (!result.IsSucceed && result.ErrList.Count > 0)
        {
            result.Err = string.Join(Environment.NewLine, result.ErrList);
        }

        return result.EndTime();
    }

    /// <summary>Executes the WriteTagGroupValuesAsync operation.</summary>
    /// <param name="groupName">The groupName parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteTagGroupValuesAsync operation result.</returns>
    public async Task<Responce> WriteTagGroupValuesAsync(
        string groupName,
        IReadOnlyDictionary<string, object?> values,
        CancellationToken cancellationToken)
    {
        var validation = ValidateTagGroupWrite(groupName, values);
        if (!validation.IsSucceed)
        {
            return validation;
        }

        var group = GetRequiredTagDatabase().GetRequiredGroup(groupName);
        foreach (var tagName in group.ResolvedTagNames)
        {
            if (!values.TryGetValue(tagName, out var value))
            {
                continue;
            }

            var write = await WriteTagValueAsync(tagName, value, cancellationToken)
                .ConfigureAwait(false);
            if (!write.IsSucceed)
            {
                return write;
            }
        }

        return new Responce().EndTime();
    }

    /// <summary>Executes the WriteTagGroupSnapshotAsync operation.</summary>
    /// <param name="snapshot">The snapshot parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteTagGroupSnapshotAsync operation result.</returns>
    public Task<Responce> WriteTagGroupSnapshotAsync(
        MitsubishiTagGroupSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return WriteTagGroupValuesAsync(
            snapshot.GroupName,
            new Dictionary<string, object?>(snapshot.Values, StringComparer.OrdinalIgnoreCase),
            cancellationToken);
    }

    /// <summary>Executes the Open operation.</summary>
    /// <returns>The Open operation result.</returns>
    public Responce Open() => OpenAsync(CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>Executes the OpenAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The OpenAsync operation result.</returns>
    public async Task<Responce> OpenAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var result = new Responce();
        try
        {
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
            return result.EndTime();
        }
        catch (Exception ex)
        {
            PublishFault("Open transport", Array.Empty<byte>(), Array.Empty<byte>(), ex);
            return result.Fail(ex.Message, exception: ex);
        }
    }

    /// <summary>Executes the Close operation.</summary>
    /// <returns>The Close operation result.</returns>
    public Responce Close() => CloseAsync(CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>Executes the CloseAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The CloseAsync operation result.</returns>
    public async Task<Responce> CloseAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var result = new Responce();
        try
        {
            await _transport.DisconnectAsync(cancellationToken).ConfigureAwait(false);
            PublishState(MitsubishiConnectionState.Disconnected);
            PublishOperation("Close transport", true, Array.Empty<byte>(), Array.Empty<byte>());
            return result.EndTime();
        }
        catch (Exception ex)
        {
            PublishFault("Close transport", Array.Empty<byte>(), Array.Empty<byte>(), ex);
            return result.Fail(ex.Message, exception: ex);
        }
    }

    /// <summary>Executes the SendPackage operation.</summary>
    /// <param name="command">The command parameter.</param>
    /// <param name="receiveCount">The receiveCount parameter.</param>
    /// <returns>The SendPackage operation result.</returns>
    public Responce<byte[]> SendPackage(byte[] command, int receiveCount) =>
        ExecuteEncodedAsync(command, receiveCount, "Legacy raw package").GetAwaiter().GetResult();

    /// <summary>Executes the SendPackageSingle operation.</summary>
    /// <param name="command">The command parameter.</param>
    /// <returns>The SendPackageSingle operation result.</returns>
    public Responce<byte[]> SendPackageSingle(byte[] command) =>
        ExecuteEncodedAsync(command, null, "Legacy raw package single").GetAwaiter().GetResult();

    /// <summary>Executes the SendPackageReliable operation.</summary>
    /// <param name="command">The command parameter.</param>
    /// <returns>The SendPackageReliable operation result.</returns>
    public Responce<byte[]> SendPackageReliable(byte[] command) =>
        ExecuteEncodedAsync(command, null, "Legacy raw package reliable").GetAwaiter().GetResult();

    /// <summary>Executes the ExecuteRawAsync operation.</summary>
    /// <param name="request">The request parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ExecuteRawAsync operation result.</returns>
    public Task<Responce<byte[]>> ExecuteRawAsync(
        MitsubishiRawCommandRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteObservableAsync(
            () => EncodeRawRequest(request),
            GetFixedResponseLength(request),
            request.Description ?? $"Command {request.Command:X4}",
            cancellationToken);
    }

    /// <summary>Executes the ReadWordsAsync operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadWordsAsync operation result.</returns>
    public async Task<Responce<ushort[]>> ReadWordsAsync(
        string address,
        int points,
        CancellationToken cancellationToken)
    {
        var parsed = MitsubishiDeviceAddress.Parse(address, Options.XyNotation);
        var raw = await ExecuteObservableAsync(
                () => EncodeWordReadRequest(parsed, points),
                GetOneEExpectedLength(MitsubishiNumericConstants.Two + (points * MitsubishiNumericConstants.Two)),
                $"Read words {address}",
                cancellationToken)
            .ConfigureAwait(false);
        return ParseWords(raw, GetSerialExpectedWordCount(points));
    }

    /// <summary>Executes the WriteWordsAsync operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteWordsAsync operation result.</returns>
    public async Task<Responce> WriteWordsAsync(
        string address,
        IReadOnlyList<ushort> values,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        var parsed = MitsubishiDeviceAddress.Parse(address, Options.XyNotation);
        var raw = await ExecuteObservableAsync(
                () => EncodeWordWriteRequest(parsed, values),
                GetOneEExpectedLength(MitsubishiNumericConstants.Two),
                $"Write words {address}",
                cancellationToken)
            .ConfigureAwait(false);
        return raw.ToBaseResponse();
    }

    /// <summary>Executes the ReadBitsAsync operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadBitsAsync operation result.</returns>
    public async Task<Responce<bool[]>> ReadBitsAsync(
        string address,
        int points,
        CancellationToken cancellationToken)
    {
        var parsed = MitsubishiDeviceAddress.Parse(address, Options.XyNotation);
        var expected = GetOneEExpectedLength(
            MitsubishiNumericConstants.Two + ((points + 1) / MitsubishiNumericConstants.Two));
        var raw = await ExecuteObservableAsync(
                () => EncodeBitReadRequest(parsed, points),
                expected,
                $"Read bits {address}",
                cancellationToken)
            .ConfigureAwait(false);
        return ParseBits(raw, points);
    }

    /// <summary>Executes the WriteBitsAsync operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteBitsAsync operation result.</returns>
    public async Task<Responce> WriteBitsAsync(
        string address,
        IReadOnlyList<bool> values,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        var parsed = MitsubishiDeviceAddress.Parse(address, Options.XyNotation);
        var raw = await ExecuteObservableAsync(
                () => EncodeBitWriteRequest(parsed, values),
                GetOneEExpectedLength(MitsubishiNumericConstants.Two),
                $"Write bits {address}",
                cancellationToken)
            .ConfigureAwait(false);
        return raw.ToBaseResponse();
    }

    /// <summary>Executes the RandomReadWordsAsync operation.</summary>
    /// <param name="addresses">The addresses parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The RandomReadWordsAsync operation result.</returns>
    public async Task<Responce<ushort[]>> RandomReadWordsAsync(
        IEnumerable<string> addresses,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        var addressArray = addresses.ToArray();
        if (IsSerialOneC())
        {
            return await RandomReadWordsOneCAsync(addressArray, cancellationToken)
                .ConfigureAwait(false);
        }

        var parsed = addressArray
            .Select(address => MitsubishiDeviceAddress.Parse(address, Options.XyNotation))
            .ToArray();
        var raw = await ExecuteObservableAsync(
                () =>
                    Options.TransportKind == MitsubishiTransportKind.Serial
                        ? MitsubishiSerialProtocolEncoding.EncodeRandomReadRequest(Options, parsed)
                        : MitsubishiProtocolEncoding.EncodeRandomRead(Options, parsed),
                null,
                "Random read words",
                cancellationToken)
            .ConfigureAwait(false);
        return ParseWords(raw);
    }

    /// <summary>Executes the RandomWriteWordsAsync operation.</summary>
    /// <param name="values">The values parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The RandomWriteWordsAsync operation result.</returns>
    public async Task<Responce> RandomWriteWordsAsync(
        IEnumerable<KeyValuePair<string, ushort>> values,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        var valueArray = values.ToArray();
        if (IsSerialOneC())
        {
            return await RandomWriteWordsOneCAsync(valueArray, cancellationToken)
                .ConfigureAwait(false);
        }

        var payload = valueArray
            .Select(pair => new MitsubishiDeviceValue(
                MitsubishiDeviceAddress.Parse(pair.Key, Options.XyNotation),
                pair.Value))
            .ToArray();
        var raw = await ExecuteObservableAsync(
                () =>
                    Options.TransportKind == MitsubishiTransportKind.Serial
                        ? MitsubishiSerialProtocolEncoding.EncodeRandomWriteRequest(
                            Options,
                            payload)
                        : MitsubishiProtocolEncoding.EncodeRandomWrite(Options, payload),
                null,
                "Random write words",
                cancellationToken)
            .ConfigureAwait(false);
        return raw.ToBaseResponse();
    }

    /// <summary>Executes the RegisterMonitorAsync operation.</summary>
    /// <param name="addresses">The addresses parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The RegisterMonitorAsync operation result.</returns>
    public async Task<Responce> RegisterMonitorAsync(
        IEnumerable<string> addresses,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        var addressArray = addresses.ToArray();
        if (IsSerialOneC())
        {
            return RegisterMonitorOneC(addressArray);
        }

        var payload = addressArray
            .Select(address => MitsubishiDeviceAddress.Parse(address, Options.XyNotation))
            .ToArray();
        var raw = await ExecuteObservableAsync(
                () =>
                    Options.TransportKind == MitsubishiTransportKind.Serial
                        ? MitsubishiSerialProtocolEncoding.EncodeMonitorRegistrationRequest(
                            Options,
                            payload)
                        : MitsubishiProtocolEncoding.EncodeMonitorRegistration(Options, payload),
                null,
                "Register monitor",
                cancellationToken)
            .ConfigureAwait(false);
        return raw.ToBaseResponse();
    }
}
