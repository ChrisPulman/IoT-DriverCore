// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Text;

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;

#else

namespace MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiRx type.</summary>
public sealed partial class MitsubishiRx : IDisposable, IAsyncDisposable
{
    /// <summary>Executes the ObserveTagGroup operation.</summary>
    /// <param name="groupName">The groupName parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="minimumUpdateSpacing">The minimumUpdateSpacing parameter.</param>
    /// <returns>The ObserveTagGroup operation result.</returns>
    public IObservable<Responce<MitsubishiTagGroupSnapshot>> ObserveTagGroup(
        string groupName,
        TimeSpan pollInterval,
        TimeSpan? minimumUpdateSpacing) =>
        BuildPollingTrigger(pollInterval)
            .SelectAsyncSequential(_ =>
                ReadTagGroupSnapshotAsync(groupName, CancellationToken.None))
            .Conflate(minimumUpdateSpacing ?? TimeSpan.FromMilliseconds(MitsubishiNumericConstants.Ten), _scheduler)
            .DoOnSubscribe(() =>
                PublishOperation(
                    $"Observe tag group {groupName} subscribed",
                    true,
                    Array.Empty<byte>(),
                    Array.Empty<byte>()))
            .DoOnDispose(() =>
                PublishOperation(
                    $"Observe tag group {groupName} disposed",
                    true,
                    Array.Empty<byte>(),
                    Array.Empty<byte>()));

    /// <summary>Executes the ObserveTagGroupHeartbeat operation.</summary>
    /// <param name="groupName">The groupName parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="heartbeatAfter">The heartbeatAfter parameter.</param>
    /// <param name="minimumUpdateSpacing">The minimumUpdateSpacing parameter.</param>
    /// <returns>The ObserveTagGroupHeartbeat operation result.</returns>
    public IObservable<Heartbeat<Responce<MitsubishiTagGroupSnapshot>>> ObserveTagGroupHeartbeat(
        string groupName,
        TimeSpan pollInterval,
        TimeSpan heartbeatAfter,
        TimeSpan? minimumUpdateSpacing) =>
        ObserveTagGroup(groupName, pollInterval, minimumUpdateSpacing)
            .Heartbeat(heartbeatAfter, _scheduler);

    /// <summary>Executes the ObserveTagGroupStale operation.</summary>
    /// <param name="groupName">The groupName parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="staleAfter">The staleAfter parameter.</param>
    /// <param name="minimumUpdateSpacing">The minimumUpdateSpacing parameter.</param>
    /// <returns>The ObserveTagGroupStale operation result.</returns>
    public IObservable<Stale<Responce<MitsubishiTagGroupSnapshot>>> ObserveTagGroupStale(
        string groupName,
        TimeSpan pollInterval,
        TimeSpan staleAfter,
        TimeSpan? minimumUpdateSpacing) =>
        ObserveTagGroup(groupName, pollInterval, minimumUpdateSpacing)
            .DetectStale(staleAfter, _scheduler);

    /// <summary>Executes the ObserveTagGroupLatest operation.</summary>
    /// <param name="groupName">The groupName parameter.</param>
    /// <param name="trigger">The trigger parameter.</param>
    /// <returns>The ObserveTagGroupLatest operation result.</returns>
    public IObservable<Responce<MitsubishiTagGroupSnapshot>> ObserveTagGroupLatest(
        string groupName,
        IObservable<Unit> trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        return trigger.SelectLatestAsync(_ =>
            ReadTagGroupSnapshotAsync(groupName, CancellationToken.None));
    }

    /// <summary>Executes the SampleDiagnostics operation.</summary>
    /// <param name="trigger">The trigger parameter.</param>
    /// <returns>The SampleDiagnostics operation result.</returns>
    public IObservable<MitsubishiOperationLog> SampleDiagnostics(IObservable<object> trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        return OperationLogs.SampleLatest(trigger);
    }

    /// <summary>Executes the ObserveConnectionHealth operation.</summary>
    /// <param name="staleAfter">The staleAfter parameter.</param>
    /// <returns>The ObserveConnectionHealth operation result.</returns>
    public IObservable<Stale<MitsubishiConnectionState>> ObserveConnectionHealth(
        TimeSpan staleAfter) => ConnectionStates.DetectStale(staleAfter, _scheduler);

    /// <summary>Executes the Dispose operation.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connectionStates.OnCompleted();
        _operationLogs.OnCompleted();
        _connectionStates.Dispose();
        _operationLogs.Dispose();
        DisposeReactiveStreams();
        _transport.Dispose();
        _requestGate.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Executes the DisposeAsync operation.</summary>
    /// <returns>The DisposeAsync operation result.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connectionStates.OnCompleted();
        _operationLogs.OnCompleted();
        _connectionStates.Dispose();
        _operationLogs.Dispose();
        DisposeReactiveStreams();
        await _transport.DisposeAsync().ConfigureAwait(false);
        _requestGate.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Executes the BuildEndPoint operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <returns>The BuildEndPoint operation result.</returns>
    private static IPEndPoint BuildEndPoint(MitsubishiClientOptions options)
    {
        return IPAddress.TryParse(options.Host, out var ipAddress)
            ? new IPEndPoint(ipAddress, options.Port)
            : new IPEndPoint(IPAddress.Any, options.Port);
    }

    /// <summary>Executes the CreateDefaultTransport operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <returns>The CreateDefaultTransport operation result.</returns>
    private static IMitsubishiTransport CreateDefaultTransport(MitsubishiClientOptions options) =>
        options.TransportKind == MitsubishiTransportKind.Serial
            ? new ReactiveSerialMitsubishiTransport()
            : new SocketMitsubishiTransport();

    /// <summary>Executes the ParseWordPayload operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="payload">The payload parameter.</param>
    /// <param name="expectedWordCount">The expectedWordCount parameter.</param>
    /// <returns>The ParseWordPayload operation result.</returns>
    private static ushort[] ParseWordPayload(
        MitsubishiClientOptions options,
        byte[] payload,
        int? expectedWordCount = null)
    {
        if (options.DataCode == CommunicationDataCode.Binary)
        {
            var count = expectedWordCount ?? (payload.Length / MitsubishiNumericConstants.Two);
            var values = new ushort[count];
            for (var index = 0; index < count; index++)
            {
                values[index] = BitConverter.ToUInt16(payload, index * MitsubishiNumericConstants.Two);
            }

            return values;
        }

        var text = System.Text.Encoding.ASCII.GetString(payload);
        var countFromAscii = expectedWordCount ?? (text.Length / MitsubishiNumericConstants.Four);
        var valuesFromAscii = new ushort[countFromAscii];
        for (var index = 0; index < countFromAscii; index++)
        {
            valuesFromAscii[index] = Convert.ToUInt16(
                text.Substring(index * MitsubishiNumericConstants.Four, MitsubishiNumericConstants.Four),
                MitsubishiNumericConstants.Sixteen);
        }

        return valuesFromAscii;
    }

    /// <summary>Executes the GetTextEncoding operation.</summary>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The GetTextEncoding operation result.</returns>
    private static Encoding GetTextEncoding(MitsubishiTagDefinition tag) =>
        tag.Encoding switch
        {
            "Utf8" => Encoding.UTF8,
            "Utf16" => Encoding.Unicode,
            _ => Encoding.ASCII,
        };

    /// <summary>Executes the ValidateStringTagLength operation.</summary>
    /// <param name="tag">The tag parameter.</param>
    /// <param name="result">The result parameter.</param>
    private static void ValidateStringTagLength(MitsubishiTagDefinition tag, Responce result)
    {
        if (!string.Equals(tag.DataType, "String", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (tag.Length > 0)
        {
            return;
        }

        result.IsSucceed = false;
        result.ErrList.Add(
            $"Tag '{tag.Name}' uses DataType 'String' and must define a positive Length.");
    }

    /// <summary>Executes the GetWordCountForScaledRead operation.</summary>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The GetWordCountForScaledRead operation result.</returns>
    private static int GetWordCountForScaledRead(MitsubishiTagDefinition tag) =>
        tag.DataType switch
        {
            null or "Word" or "Int16" or "UInt16" => 1,
            "DWord" or "Int32" or "UInt32" or "Float" => MitsubishiNumericConstants.Two,
            _ => throw new InvalidOperationException(
                $"Scaled access is not supported for DataType '{tag.DataType}'."),
        };

    /// <summary>Executes the ParseAsciiBits operation.</summary>
    /// <param name="payload">The payload parameter.</param>
    /// <param name="expectedBitCount">The expectedBitCount parameter.</param>
    /// <returns>The ParseAsciiBits operation result.</returns>
    private static bool[] ParseAsciiBits(byte[] payload, int expectedBitCount)
    {
        var text = System.Text.Encoding.ASCII.GetString(payload);
        var bits = new List<bool>(expectedBitCount);
        foreach (var ch in text)
        {
            if (ch is '0' or '1')
            {
                bits.Add(ch == '1');
            }

            if (bits.Count == expectedBitCount)
            {
                break;
            }
        }

        return bits.ToArray();
    }

    /// <summary>Executes the ParseBinaryBits operation.</summary>
    /// <param name="payload">The payload parameter.</param>
    /// <param name="expectedBitCount">The expectedBitCount parameter.</param>
    /// <returns>The ParseBinaryBits operation result.</returns>
    private static bool[] ParseBinaryBits(byte[] payload, int expectedBitCount)
    {
        var bits = new List<bool>(expectedBitCount);
        foreach (var value in payload)
        {
            bits.Add((value & 0x0F) != 0);
            if (bits.Count == expectedBitCount)
            {
                break;
            }

            bits.Add((value & 0xF0) != 0);
            if (bits.Count == expectedBitCount)
            {
                break;
            }
        }

        return bits.ToArray();
    }

    /// <summary>Executes the ConvertWords operation.</summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="raw">The raw parameter.</param>
    /// <param name="converter">The converter parameter.</param>
    /// <returns>The ConvertWords operation result.</returns>
    private static Responce<T> ConvertWords<T>(Responce<ushort[]> raw, Func<ushort[], T> converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        if (!raw.IsSucceed || raw.Value is null)
        {
            return new Responce<T>(raw);
        }

        try
        {
            return new Responce<T>(raw, converter(raw.Value));
        }
        catch (Exception ex)
        {
            return new Responce<T>(raw).Fail(ex.Message, exception: ex);
        }
    }

    /// <summary>Executes the ValidateGroups operation.</summary>
    /// <param name="database">The database parameter.</param>
    /// <param name="result">The result parameter.</param>
    private static void ValidateGroups(MitsubishiTagDatabase database, Responce result)
    {
        foreach (var group in database.Groups)
        {
            ValidateGroupTags(database, group, result);
        }
    }

    /// <summary>Executes the ValidateGroupTags operation.</summary>
    /// <param name="database">The database parameter.</param>
    /// <param name="group">The group parameter.</param>
    /// <param name="result">The result parameter.</param>
    private static void ValidateGroupTags(
        MitsubishiTagDatabase database,
        MitsubishiTagGroupDefinition group,
        Responce result)
    {
        foreach (var tagName in group.ResolvedTagNames)
        {
            if (database.TryGet(tagName, out _))
            {
                continue;
            }

            result.IsSucceed = false;
            result.ErrList.Add($"Group '{group.Name}' references unknown tag '{tagName}'.");
        }
    }

    /// <summary>Executes the ApplyValidationSummary operation.</summary>
    /// <param name="result">The result parameter.</param>
    private static void ApplyValidationSummary(Responce result)
    {
        if (result.IsSucceed || result.ErrList.Count == 0)
        {
            return;
        }

        result.Err = string.Join(Environment.NewLine, result.ErrList);
    }

    /// <summary>Executes the ApplyScaleAndOffset operation.</summary>
    /// <param name="rawValue">The rawValue parameter.</param>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The ApplyScaleAndOffset operation result.</returns>
    private static double ApplyScaleAndOffset(double rawValue, MitsubishiTagDefinition tag) =>
        (rawValue * tag.Scale) + tag.Offset;

    /// <summary>Executes the RemoveScaleAndOffset operation.</summary>
    /// <param name="engineeringValue">The engineeringValue parameter.</param>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The RemoveScaleAndOffset operation result.</returns>
    private static double RemoveScaleAndOffset(double engineeringValue, MitsubishiTagDefinition tag)
    {
        if (tag.Scale == 0)
        {
            throw new InvalidOperationException(
                $"Tag '{tag.Name}' has Scale=0 and cannot be used for scaled writes.");
        }

        return (engineeringValue - tag.Offset) / tag.Scale;
    }
}
