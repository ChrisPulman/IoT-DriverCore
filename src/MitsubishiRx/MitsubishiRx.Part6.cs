// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;

#else

namespace MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiRx type.</summary>
public sealed partial class MitsubishiRx : IDisposable, IAsyncDisposable
{
    /// <summary>Executes the ConvertToUInt32 operation.</summary>
    /// <param name="words">The words parameter.</param>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The ConvertToUInt32 operation result.</returns>
    private static uint ConvertToUInt32(ushort[] words, MitsubishiTagDefinition tag)
    {
        EnsureWordCount(words, MitsubishiNumericConstants.Two, tag.Name);
        return tag.ByteOrder == MitsubishiMessages.BigEndian
            ? unchecked((uint)((words[0] << 16) | words[1]))
            : unchecked((uint)(words[0] | (words[1] << 16)));
    }

    /// <summary>Executes the ConvertToInt32 operation.</summary>
    /// <param name="words">The words parameter.</param>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The ConvertToInt32 operation result.</returns>
    private static int ConvertToInt32(ushort[] words, MitsubishiTagDefinition tag) =>
        unchecked((int)ConvertToUInt32(words, tag));

    /// <summary>Executes the ConvertFromInt32 operation.</summary>
    /// <param name="value">The value parameter.</param>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The ConvertFromInt32 operation result.</returns>
    private static ushort[] ConvertFromInt32(int value, MitsubishiTagDefinition tag)
    {
        var raw = unchecked((uint)value);
        return tag.ByteOrder == MitsubishiMessages.BigEndian
            ? [unchecked((ushort)(raw >> 16)), unchecked((ushort)(raw & 0xFFFF))]
            : [unchecked((ushort)(raw & 0xFFFF)), unchecked((ushort)(raw >> 16))];
    }

    /// <summary>Executes the ConvertFromUInt32 operation.</summary>
    /// <param name="value">The value parameter.</param>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The ConvertFromUInt32 operation result.</returns>
    private static ushort[] ConvertFromUInt32(uint value, MitsubishiTagDefinition tag) =>
        tag.ByteOrder == MitsubishiMessages.BigEndian
            ? [unchecked((ushort)(value >> 16)), unchecked((ushort)(value & 0xFFFF))]
            : [unchecked((ushort)(value & 0xFFFF)), unchecked((ushort)(value >> 16))];

    /// <summary>Executes the ConvertFromFloat operation.</summary>
    /// <param name="value">The value parameter.</param>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The ConvertFromFloat operation result.</returns>
    private static ushort[] ConvertFromFloat(float value, MitsubishiTagDefinition tag) =>
        ConvertFromInt32(BitConverter.SingleToInt32Bits(value), tag);

    /// <summary>Executes the EnsureWordCount operation.</summary>
    /// <param name="words">The words parameter.</param>
    /// <param name="requiredCount">The requiredCount parameter.</param>
    /// <param name="tagName">The tagName parameter.</param>
    private static void EnsureWordCount(ushort[] words, int requiredCount, string tagName)
    {
        if (words.Length >= requiredCount)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Tag '{tagName}' requires at least {requiredCount} word(s), but only {words.Length} were read.");
    }

    /// <summary>Executes the DecodeStringFromWords operation.</summary>
    /// <param name="words">The words parameter.</param>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The DecodeStringFromWords operation result.</returns>
    private static string DecodeStringFromWords(ushort[] words, MitsubishiTagDefinition tag)
    {
        var bytes = new byte[words.Length * MitsubishiNumericConstants.Two];
        for (var index = 0; index < words.Length; index++)
        {
            var span = bytes.AsSpan(index * MitsubishiNumericConstants.Two, MitsubishiNumericConstants.Two);
            if (tag.ByteOrder == MitsubishiMessages.BigEndian)
            {
                BinaryPrimitives.WriteUInt16BigEndian(span, words[index]);
            }
            else
            {
                BinaryPrimitives.WriteUInt16LittleEndian(span, words[index]);
            }
        }

        return GetTextEncoding(tag).GetString(bytes).TrimEnd('\0');
    }

    /// <summary>Executes the EncodeStringWords operation.</summary>
    /// <param name="value">The value parameter.</param>
    /// <param name="wordLength">The wordLength parameter.</param>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The EncodeStringWords operation result.</returns>
    private static ushort[] EncodeStringWords(
        string value,
        int wordLength,
        MitsubishiTagDefinition tag)
    {
        var bytes = GetTextEncoding(tag).GetBytes(value);
        var maxBytes = checked(wordLength * MitsubishiNumericConstants.Two);
        if (bytes.Length > maxBytes)
        {
            throw new ArgumentException(
                $"String length {bytes.Length} exceeds the requested PLC word capacity of {maxBytes} bytes.",
                nameof(value));
        }

        var padded = new byte[maxBytes];
        bytes.CopyTo(padded, 0);
        var words = new ushort[wordLength];
        for (var index = 0; index < wordLength; index++)
        {
            var span = padded.AsSpan(index * MitsubishiNumericConstants.Two, MitsubishiNumericConstants.Two);
            words[index] =
                tag.ByteOrder == MitsubishiMessages.BigEndian
                    ? BinaryPrimitives.ReadUInt16BigEndian(span)
                    : BinaryPrimitives.ReadUInt16LittleEndian(span);
        }

        return words;
    }

    /// <summary>Executes the CanWriteTagValue operation.</summary>
    /// <param name="tag">The tag parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="error">The error parameter.</param>
    /// <returns>The CanWriteTagValue operation result.</returns>
    private static bool CanWriteTagValue(
        MitsubishiTagDefinition tag,
        object? value,
        out string? error)
    {
        if (value is null)
        {
            error = $"Tag '{tag.Name}' cannot be written with a null value.";
            return false;
        }

        var ok = tag.DataType switch
        {
            "Bit" => value is bool,
            "String" => value is string,
            "Float" => value is float,
            "DWord" or "UInt32" => value is uint,
            "Int32" => value is int,
            "Int16" => value is short,
            "UInt16" => value is ushort,
            _ when HasEngineeringMetadata(tag) => value is double,
            _ => value is ushort,
        };
        error = ok
            ? null
            : $"Tag '{tag.Name}' expects a value compatible with DataType " +
              $"'{tag.DataType ?? "Word"}', but received '{value.GetType().Name}'.";
        return ok;
    }

    /// <summary>Executes the ConvertTagValueAsync operation.</summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="task">The task parameter.</param>
    /// <param name="projector">The projector parameter.</param>
    /// <returns>The ConvertTagValueAsync operation result.</returns>
    private static async Task<Responce<object?>> ConvertTagValueAsync<T>(
        Task<Responce<T>> task,
        Func<T, object?> projector)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(projector);
        var raw = await task.ConfigureAwait(false);
        return !raw.IsSucceed || raw.Value is null
            ? new Responce<object?>(raw)
            : new Responce<object?>(raw, projector(raw.Value));
    }

    /// <summary>Executes the HasEngineeringMetadata operation.</summary>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The HasEngineeringMetadata operation result.</returns>
    private static bool HasEngineeringMetadata(MitsubishiTagDefinition tag) =>
        Math.Abs(tag.Scale - 1.0) > double.Epsilon || Math.Abs(tag.Offset) > double.Epsilon;

    /// <summary>Executes the ValidateRolloutPolicy operation.</summary>
    /// <param name="diff">The diff parameter.</param>
    /// <param name="policy">The policy parameter.</param>
    /// <returns>The ValidateRolloutPolicy operation result.</returns>
    private static Responce ValidateRolloutPolicy(
        MitsubishiTagDatabaseDiff diff,
        MitsubishiTagRolloutPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(diff);
        if (policy == MitsubishiTagRolloutPolicy.AllowAll)
        {
            return new Responce().EndTime();
        }

        if (policy == MitsubishiTagRolloutPolicy.SafeMetadataAndGroups)
        {
            var disallowed =
                diff.ChangeKinds
                & (
                    MitsubishiSchemaChangeKind.AddressChange
                    | MitsubishiSchemaChangeKind.DataTypeChange
                    | MitsubishiSchemaChangeKind.StructureChange);
            return disallowed == MitsubishiSchemaChangeKind.None
                ? new Responce().EndTime()
                : new Responce().Fail(
                    $"Rollout policy '{policy}' rejected schema changes: {disallowed}.");
        }

        return new Responce().Fail($"Unsupported rollout policy '{policy}'.");
    }

    /// <summary>Executes the GetSchemaFingerprint operation.</summary>
    /// <param name="path">The path parameter.</param>
    /// <returns>The GetSchemaFingerprint operation result.</returns>
    private static string GetSchemaFingerprint(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            return $"{info.FullName}|missing";
        }

        var content = File.ReadAllBytes(path);
        var hash = Convert.ToHexString(SHA256.HashData(content));
        return $"{info.FullName}|{content.Length}|{hash}";
    }

    /// <summary>Executes the ConvertToFloat operation.</summary>
    /// <param name="words">The words parameter.</param>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The ConvertToFloat operation result.</returns>
    private static float ConvertToFloat(ushort[] words, MitsubishiTagDefinition tag) =>
        BitConverter.Int32BitsToSingle(ConvertToInt32(words, tag));

    /// <summary>Executes the ReadNumericTagValue operation.</summary>
    /// <param name="tag">The tag parameter.</param>
    /// <param name="words">The words parameter.</param>
    /// <returns>The ReadNumericTagValue operation result.</returns>
    private static double ReadNumericTagValue(MitsubishiTagDefinition tag, ushort[] words) =>
        tag.DataType switch
        {
            null or "Word" => tag.Signed ? unchecked((short)words[0]) : words[0],
            "Int16" => unchecked((short)words[0]),
            "UInt16" => words[0],
            "DWord" => unchecked((uint)(words[0] | (words[1] << 16))),
            "Int32" => ConvertToInt32(words, tag),
            "UInt32" => ConvertToUInt32(words, tag),
            "Float" => ConvertToFloat(words, tag),
            _ => throw new InvalidOperationException(
                $"Numeric conversion is not supported for DataType '{tag.DataType}'."),
        };

    /// <summary>Executes the BuildPollingTrigger operation.</summary>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="emitInitial">The emitInitial parameter.</param>
    /// <returns>The BuildPollingTrigger operation result.</returns>
    private IObservable<long> BuildPollingTrigger(TimeSpan pollInterval, bool emitInitial = true) =>
        emitInitial
            ? Observable.Interval(pollInterval, _scheduler).StartWith(0L)
            : Observable.Interval(pollInterval, _scheduler);

    /// <summary>Executes the ExecuteControlAsync operation.</summary>
    /// <param name="command">The command parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <param name="force">The force parameter.</param>
    /// <param name="clearMode">The clearMode parameter.</param>
    /// <returns>The ExecuteControlAsync operation result.</returns>
    private async Task<Responce> ExecuteControlAsync(
        ushort command,
        CancellationToken cancellationToken,
        bool force = true,
        bool clearMode = false)
    {
        var raw = await ExecuteObservableAsync(
                () =>
                    Options.TransportKind == MitsubishiTransportKind.Serial
                        ? MitsubishiSerialProtocolEncoding.EncodeRemoteOperationRequest(
                            Options,
                            command,
                            force,
                            clearMode)
                        : MitsubishiProtocolEncoding.EncodeRemoteOperation(
                            Options,
                            command,
                            force,
                            clearMode),
                null,
                $"Remote operation {command:X4}",
                cancellationToken)
            .ConfigureAwait(false);
        return raw.ToBaseResponse();
    }

    /// <summary>Executes the ExecutePasswordAsync operation.</summary>
    /// <param name="command">The command parameter.</param>
    /// <param name="password">The password parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ExecutePasswordAsync operation result.</returns>
    private async Task<Responce> ExecutePasswordAsync(
        ushort command,
        string password,
        CancellationToken cancellationToken)
    {
        var raw = await ExecuteObservableAsync(
                () => MitsubishiProtocolEncoding.EncodeRemotePassword(Options, command, password),
                null,
                command == MitsubishiCommandCodes.Unlock ? "Unlock" : "Lock",
                cancellationToken)
            .ConfigureAwait(false);
        return raw.ToBaseResponse();
    }

    /// <summary>Executes the IsSerialOneC operation.</summary>
    /// <returns>The IsSerialOneC operation result.</returns>
    private bool IsSerialOneC() =>
        Options.TransportKind == MitsubishiTransportKind.Serial
        && Options.FrameType == MitsubishiFrameType.OneC;

    /// <summary>Executes the RandomReadWordsOneCAsync operation.</summary>
    /// <param name="addresses">The addresses parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The RandomReadWordsOneCAsync operation result.</returns>
    private async Task<Responce<ushort[]>> RandomReadWordsOneCAsync(
        string[] addresses,
        CancellationToken cancellationToken)
    {
        if (addresses.Length == 0)
        {
            return new Responce<ushort[]>().Fail("At least one device must be supplied.");
        }

        var values = new List<ushort>(addresses.Length);
        foreach (var address in addresses)
        {
            var read = await ReadWordsAsync(address, 1, cancellationToken).ConfigureAwait(false);
            if (!read.IsSucceed || read.Value is null)
            {
                return new Responce<ushort[]>(read);
            }

            values.Add(read.Value[0]);
        }

        return new Responce<ushort[]>(values.ToArray()).EndTime();
    }
}
