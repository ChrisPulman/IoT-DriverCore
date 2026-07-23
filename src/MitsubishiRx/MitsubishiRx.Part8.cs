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
    /// <summary>Executes the ParseLoopback operation.</summary>
    /// <param name="raw">The raw parameter.</param>
    /// <returns>The ParseLoopback operation result.</returns>
    private Responce<byte[]> ParseLoopback(Responce<byte[]> raw)
    {
        if (!raw.IsSucceed || raw.Value is null)
        {
            return new Responce<byte[]>(raw);
        }

        try
        {
            if (Options.TransportKind != MitsubishiTransportKind.Serial)
            {
                return raw;
            }

            if (Options.DataCode == CommunicationDataCode.Binary)
            {
                if (raw.Value.Length < 2)
                {
                    throw new InvalidOperationException(
                        "Loopback payload is missing the returned length field.");
                }

                var length = BitConverter.ToUInt16(raw.Value, 0);
                var available = Math.Max(0, raw.Value.Length - MitsubishiNumericConstants.Two);
                var count = Math.Min(length, available);
                return new Responce<byte[]>(raw, raw.Value.Skip(MitsubishiNumericConstants.Two).Take(count).ToArray());
            }

            var ascii = System.Text.Encoding.ASCII.GetString(raw.Value);
            if (ascii.Length < 4)
            {
                throw new InvalidOperationException(
                    "Loopback payload is missing the returned ASCII length field.");
            }

            var lengthValue = ushort.TryParse(
                ascii[..MitsubishiNumericConstants.Four],
                System.Globalization.NumberStyles.HexNumber,
                null,
                out var parsedLength)
                ? parsedLength
                : (ushort)0;
            var echoed =
                ascii.Length > 4
                    ? ascii.Substring(
                        MitsubishiNumericConstants.Four,
                        Math.Min(lengthValue, ascii.Length - MitsubishiNumericConstants.Four))
                    : string.Empty;
            return new Responce<byte[]>(raw, System.Text.Encoding.ASCII.GetBytes(echoed));
        }
        catch (Exception ex)
        {
            return new Responce<byte[]>(raw).Fail(ex.Message, exception: ex);
        }
    }

    /// <summary>Executes the ParseWords operation.</summary>
    /// <param name="raw">The raw parameter.</param>
    /// <param name="expectedWordCount">The expectedWordCount parameter.</param>
    /// <returns>The ParseWords operation result.</returns>
    private Responce<ushort[]> ParseWords(Responce<byte[]> raw, int? expectedWordCount = null)
    {
        if (!raw.IsSucceed || raw.Value is null)
        {
            return new Responce<ushort[]>(raw);
        }

        try
        {
            return new Responce<ushort[]>(
                raw,
                ParseWordPayload(Options, raw.Value, expectedWordCount));
        }
        catch (Exception ex)
        {
            return new Responce<ushort[]>(raw).Fail(ex.Message, exception: ex);
        }
    }

    /// <summary>Executes the GetOneEExpectedLength operation.</summary>
    /// <param name="length">The length parameter.</param>
    /// <returns>The GetOneEExpectedLength operation result.</returns>
    private int? GetOneEExpectedLength(int length)
    {
        if (Options.TransportKind == MitsubishiTransportKind.Serial)
        {
            return null;
        }

        return Options.FrameType == MitsubishiFrameType.OneE ? length : null;
    }

    /// <summary>Executes the GetFixedResponseLength operation.</summary>
    /// <param name="request">The request parameter.</param>
    /// <returns>The GetFixedResponseLength operation result.</returns>
    private int? GetFixedResponseLength(MitsubishiRawCommandRequest request)
    {
        return Options.TransportKind == MitsubishiTransportKind.Serial
            ? null
            : MitsubishiProtocolEncoding.GetFixedResponseLength(
                Options.FrameType,
                Options.DataCode,
                request.Command,
                request.Subcommand,
                request.ResolvedBody.Count);
    }

    /// <summary>Executes the EncodeRawRequest operation.</summary>
    /// <param name="request">The request parameter.</param>
    /// <returns>The EncodeRawRequest operation result.</returns>
    private byte[] EncodeRawRequest(MitsubishiRawCommandRequest request)
    {
        return Options.TransportKind == MitsubishiTransportKind.Serial
            ? MitsubishiSerialProtocolEncoding.EncodeRawRequest(Options, request)
            : MitsubishiProtocolEncoding.Encode(Options, request);
    }

    /// <summary>Executes the EncodeWordReadRequest operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <returns>The EncodeWordReadRequest operation result.</returns>
    private byte[] EncodeWordReadRequest(MitsubishiDeviceAddress address, int points)
    {
        return Options.TransportKind == MitsubishiTransportKind.Serial
            ? MitsubishiSerialProtocolEncoding.EncodeWordReadRequest(Options, address, points)
            : MitsubishiProtocolEncoding.EncodeDeviceBatchRead(
                Options,
                address,
                points,
                bitUnits: false);
    }

    /// <summary>Executes the EncodeWordWriteRequest operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <returns>The EncodeWordWriteRequest operation result.</returns>
    private byte[] EncodeWordWriteRequest(
        MitsubishiDeviceAddress address,
        IReadOnlyList<ushort> values)
    {
        return Options.TransportKind == MitsubishiTransportKind.Serial
            ? MitsubishiSerialProtocolEncoding.EncodeWordWriteRequest(Options, address, values)
            : MitsubishiProtocolEncoding.EncodeDeviceBatchWrite(
                Options,
                address,
                values.ToArray(),
                bitUnits: false);
    }

    /// <summary>Executes the EncodeBitReadRequest operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <returns>The EncodeBitReadRequest operation result.</returns>
    private byte[] EncodeBitReadRequest(MitsubishiDeviceAddress address, int points)
    {
        return Options.TransportKind == MitsubishiTransportKind.Serial
            ? MitsubishiSerialProtocolEncoding.EncodeBitReadRequest(Options, address, points)
            : MitsubishiProtocolEncoding.EncodeDeviceBatchRead(
                Options,
                address,
                points,
                bitUnits: true);
    }

    /// <summary>Executes the EncodeBitWriteRequest operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <returns>The EncodeBitWriteRequest operation result.</returns>
    private byte[] EncodeBitWriteRequest(
        MitsubishiDeviceAddress address,
        IReadOnlyList<bool> values)
    {
        if (Options.TransportKind == MitsubishiTransportKind.Serial)
        {
            return MitsubishiSerialProtocolEncoding.EncodeBitWriteRequest(Options, address, values);
        }

        return MitsubishiProtocolEncoding.EncodeDeviceBatchWrite(
            Options,
            address,
            values.Select(static value => value ? (ushort)1 : (ushort)0).ToArray(),
            bitUnits: true);
    }

    /// <summary>Executes the EncodeLoopbackRequest operation.</summary>
    /// <param name="data">The data parameter.</param>
    /// <returns>The EncodeLoopbackRequest operation result.</returns>
    private byte[] EncodeLoopbackRequest(byte[] data)
    {
        return Options.TransportKind == MitsubishiTransportKind.Serial
            ? MitsubishiSerialProtocolEncoding.EncodeLoopbackRequest(Options, data)
            : MitsubishiProtocolEncoding.EncodeLoopback(Options, data);
    }

    /// <summary>Executes the GetSerialExpectedWordCount operation.</summary>
    /// <param name="wordCount">The wordCount parameter.</param>
    /// <returns>The GetSerialExpectedWordCount operation result.</returns>
    private int? GetSerialExpectedWordCount(int wordCount) =>
        Options.TransportKind == MitsubishiTransportKind.Serial ? wordCount : null;

    /// <summary>Executes the ValidateTagDatabase operation.</summary>
    /// <param name="database">The database parameter.</param>
    /// <returns>The ValidateTagDatabase operation result.</returns>
    private Responce ValidateTagDatabase(MitsubishiTagDatabase? database)
    {
        var result = new Responce();
        if (database is null)
        {
            return result.Fail(MitsubishiMessages.TagDatabaseRequiredForValidation);
        }

        ValidateTags(database.Tags, result);
        ValidateGroups(database, result);
        ApplyValidationSummary(result);
        return result.EndTime();
    }

    /// <summary>Executes the ValidateTags operation.</summary>
    /// <param name="tags">The tags parameter.</param>
    /// <param name="result">The result parameter.</param>
    private void ValidateTags(IEnumerable<MitsubishiTagDefinition> tags, Responce result)
    {
        foreach (var tag in tags)
        {
            ValidateTagAddress(tag, result);
            ValidateStringTagLength(tag, result);
        }
    }

    /// <summary>Executes the ValidateTagAddress operation.</summary>
    /// <param name="tag">The tag parameter.</param>
    /// <param name="result">The result parameter.</param>
    private void ValidateTagAddress(MitsubishiTagDefinition tag, Responce result)
    {
        try
        {
            _ = MitsubishiDeviceAddress.Parse(tag.Address, Options.XyNotation);
        }
        catch (Exception ex)
        {
            result.IsSucceed = false;
            result.ErrList.Add(
                $"Tag '{tag.Name}' has invalid Address '{tag.Address}': {ex.Message}");
        }
    }

    /// <summary>Executes the GetRequiredTag operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <returns>The GetRequiredTag operation result.</returns>
    private MitsubishiTagDefinition GetRequiredTag(string tagName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        var database =
            TagDatabase
            ?? throw new InvalidOperationException(
                MitsubishiMessages.TagDatabaseRequiredForTagApis);
        return database.GetRequired(tagName);
    }

    /// <summary>Executes the GetRequiredTagDatabase operation.</summary>
    /// <returns>The GetRequiredTagDatabase operation result.</returns>
    private MitsubishiTagDatabase GetRequiredTagDatabase() =>
        TagDatabase
        ?? throw new InvalidOperationException(
            MitsubishiMessages.TagDatabaseRequiredForTagApis);

    /// <summary>Executes the ReadTagValueAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadTagValueAsync operation result.</returns>
    private async Task<Responce<object?>> ReadTagValueAsync(
        string tagName,
        CancellationToken cancellationToken)
    {
        var tag = GetRequiredTag(tagName);
        return tag.DataType switch
        {
            "Bit" => await ConvertTagValueAsync(
                    ReadBitsByTagAsync(tagName, 1, cancellationToken),
                    static values => values[0])
                .ConfigureAwait(false),
            "String" => await ConvertTagValueAsync(
                    ReadStringByTagAsync(tagName, cancellationToken),
                    static value => value)
                .ConfigureAwait(false),
            "Float" => await ConvertTagValueAsync(
                    ReadFloatByTagAsync(tagName, cancellationToken),
                    static value => (object?)value)
                .ConfigureAwait(false),
            "DWord" or "UInt32" => await ConvertTagValueAsync(
                    ReadDWordByTagAsync(tagName, cancellationToken),
                    static value => (object?)value)
                .ConfigureAwait(false),
            "Int32" => await ConvertTagValueAsync(
                    ReadInt32ByTagAsync(tagName, cancellationToken),
                    static value => (object?)value)
                .ConfigureAwait(false),
            "Int16" => await ConvertTagValueAsync(
                    ReadInt16ByTagAsync(tagName, cancellationToken),
                    static value => (object?)value)
                .ConfigureAwait(false),
            "UInt16" => await ConvertTagValueAsync(
                    ReadUInt16ByTagAsync(tagName, cancellationToken),
                    static value => (object?)value)
                .ConfigureAwait(false),
            _ when HasEngineeringMetadata(tag) => await ConvertTagValueAsync(
                    ReadScaledDoubleByTagAsync(tagName, cancellationToken),
                    static value => (object?)value)
                .ConfigureAwait(false),
            _ => await ConvertTagValueAsync(
                    ReadWordsByTagAsync(tagName, 1, cancellationToken),
                    static values => (object?)values[0])
                .ConfigureAwait(false),
        };
    }

    /// <summary>Executes the WriteTagValueAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteTagValueAsync operation result.</returns>
    private async Task<Responce> WriteTagValueAsync(
        string tagName,
        object? value,
        CancellationToken cancellationToken)
    {
        var tag = GetRequiredTag(tagName);
        try
        {
            var writeTask = CreateWriteTagValueTask(tagName, tag, value, cancellationToken);
            if (writeTask is not null)
            {
                return await writeTask.ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            return new Responce().Fail(ex.Message, exception: ex);
        }

        return new Responce().Fail(
            $"Value for tag '{tagName}' is not compatible with DataType '{tag.DataType ?? "Word"}'.");
    }
}
