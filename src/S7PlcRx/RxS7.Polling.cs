// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using IoT.DriverCore.S7PlcRx.Reactive.Core;
using IoT.DriverCore.S7PlcRx.Reactive.Enums;
using IoT.DriverCore.S7PlcRx.Reactive.PlcTypes;
#else
using IoT.DriverCore.S7PlcRx.Core;
using IoT.DriverCore.S7PlcRx.Enums;
using IoT.DriverCore.S7PlcRx.PlcTypes;
#endif

using TimeSpan = System.TimeSpan;

#if REACTIVE_SHIM
namespace IoT.DriverCore.S7PlcRx.Reactive;
#else
namespace IoT.DriverCore.S7PlcRx;
#endif

/// <summary>Contains polling members for <see cref="RxS7"/>.</summary>
public partial class RxS7
{
    /// <summary>Reads a data block bit address.</summary>
    /// <param name="tag">The tag to read.</param>
    /// <param name="strings">The parsed address components.</param>
    /// <param name="db">The data block number.</param>
    /// <param name="byteOffset">The byte offset.</param>
    /// <returns>The bit value.</returns>
    private bool ReadDataBlockBitAddress(Tag tag, string[] strings, int db, int byteOffset)
    {
        var bitOffset = int.Parse(strings[BitAddressComponentIndex]);
        RxS7ValueHelpers.EnsureBitOffsetIsValid(bitOffset, tag);
        var value = Read<byte>(tag, DataType.DataBlock, db, byteOffset, VarType.Byte);
        return RxS7ValueHelpers.GetBit(value, bitOffset);
    }

    /// <summary>Reads a timer, counter, or bit address outside data blocks.</summary>
    /// <param name="tag">The tag to read.</param>
    /// <param name="correctVariable">The normalized variable address.</param>
    /// <returns>The value read from the address.</returns>
    private object? ReadSpecialOrBitAddress(Tag tag, string correctVariable)
    {
        return correctVariable[..1] switch
        {
            "E" or "I" => ReadBitAddress(tag, correctVariable, DataType.Input),
            "A" or "O" => ReadBitAddress(tag, correctVariable, DataType.Output),
            "M" => ReadBitAddress(tag, correctVariable, DataType.Memory),
            "T" => ReadTimerAddress(tag, correctVariable),
            "Z" or "C" => ReadCounterAddress(tag, correctVariable),
            _ => throw new ArgumentException($"Unknown variable type {correctVariable[..1]}.", nameof(tag)),
        };
    }

    /// <summary>Reads a bit address from the specified data area.</summary>
    /// <param name="tag">The tag to read.</param>
    /// <param name="correctVariable">The normalized variable address.</param>
    /// <param name="dataType">The data area type.</param>
    /// <returns>The bit value.</returns>
    private bool ReadBitAddress(Tag tag, string correctVariable, DataType dataType)
    {
        var addressLocation = correctVariable[1..];
        var decimalPointIndex = addressLocation.IndexOf('.');
        if (decimalPointIndex == -1)
        {
            throw new ArgumentException(
                string.Concat(
                    $"Cannot parse variable {correctVariable}. ",
                    "Input, Output, Memory Address, Timer, and Counter types ",
                    "require bit-level addressing (e.g. I0.1)."),
                nameof(tag));
        }

        var byteOffset = int.Parse(addressLocation[..decimalPointIndex]);
        var bitOffset = int.Parse(addressLocation[(decimalPointIndex + 1)..]);
        RxS7ValueHelpers.EnsureBitOffsetIsValid(bitOffset, tag);
        var value = Read<byte>(tag, dataType, 0, byteOffset, VarType.Byte);
        return RxS7ValueHelpers.GetBit(value, bitOffset);
    }

    /// <summary>Reads a timer address.</summary>
    /// <param name="tag">The tag to read.</param>
    /// <param name="correctVariable">The normalized variable address.</param>
    /// <returns>The timer value or array.</returns>
    private object? ReadTimerAddress(Tag tag, string correctVariable) =>
        tag.Type == typeof(double[])
            ? Read<double[]>(tag, DataType.Timer, 0, int.Parse(correctVariable[AreaAddressCodeLength..]), VarType.Timer)
            : Read<double>(tag, DataType.Timer, 0, int.Parse(correctVariable[1..]), VarType.Timer);

    /// <summary>Reads a counter address.</summary>
    /// <param name="tag">The tag to read.</param>
    /// <param name="correctVariable">The normalized variable address.</param>
    /// <returns>The counter value or array.</returns>
    private object? ReadCounterAddress(Tag tag, string correctVariable)
    {
        if (tag.Type == typeof(ushort[]))
        {
            return Read<ushort[]>(
                tag,
                DataType.Counter,
                0,
                int.Parse(correctVariable[AreaAddressCodeLength..]),
                VarType.Counter);
        }

        if (tag.Type == typeof(short[]))
        {
            return Read<short[]>(
                tag,
                DataType.Counter,
                0,
                int.Parse(correctVariable[AreaAddressCodeLength..]),
                VarType.Counter);
        }

        return tag.Type == typeof(short)
            ? Read<short>(
                tag,
                DataType.Counter,
                0,
                int.Parse(correctVariable[AreaAddressCodeLength..]),
                VarType.Counter)
            : Read<ushort>(tag, DataType.Counter, 0, int.Parse(correctVariable[1..]), VarType.Counter);
    }

    /// <summary>Reads bytes from the specified data block and address.</summary>
    /// <remarks>If the read operation fails or an error occurs, the method returns null and updates the error
    /// state. The method is thread-safe.</remarks>
    /// <param name="tag">The tag that identifies the target device or connection for the read operation.</param>
    /// <param name="dataType">The data type to use when reading from the data block. Determines how the data is
    /// interpreted.</param>
    /// <param name="db">The number of the data block to read from.</param>
    /// <param name="startByteAdr">The zero-based starting byte address within the data block from which to begin
    /// reading.</param>
    /// <param name="count">The number of bytes to read from the specified address. Must be greater than zero.</param>
    /// <returns>A byte array containing the data read from the specified location, or null if the read operation
    /// fails.</returns>
    private byte[]? ReadBytes(Tag tag, DataType dataType, int db, int startByteAdr, int count)
    {
        lock (_socketLock)
        {
            try
            {
                var bytes = new byte[count];
                using var package = new ByteArray(SingleReadPackageSize);
                package.Add(ReadHeaderPackage());
                package.Add(CreateReadDataRequestPackage(dataType, db, startByteAdr, count));

                var sent = _socketRx.Send(tag, package.Array, package.Array.Length);
                if (package.Array.Length != sent)
                {
                    return default;
                }

                var receiveSize = Math.Max(
                    _socketRx.DataReadLength + ProtocolReceivePadding,
                    count + ProtocolReceivePadding);
                var receiveBuffer = new byte[receiveSize];
                var result = _socketRx.ReceiveIsoData(tag, ref receiveBuffer);
                if (result < ReadResponseMinimumSize)
                {
                    return default;
                }

                if (receiveBuffer[ResponseReturnCodeOffset] != 0xFF)
                {
                    return default;
                }

                var availableDataLength = RxS7ValueHelpers.GetReadResponseDataLengthBytes(receiveBuffer, result);
                if (availableDataLength <= 0)
                {
                    return default;
                }

                var bytesToCopy = Math.Min(count, availableDataLength);
                Array.Copy(receiveBuffer, ReadResponseDataOffset, bytes, 0, bytesToCopy);
                if (bytesToCopy == count)
                {
                    return bytes;
                }

                var trimmed = new byte[bytesToCopy];
                Array.Copy(bytes, 0, trimmed, 0, bytesToCopy);
                return trimmed;
            }
            catch (Exception exc)
            {
                _lastErrorCode.OnNext(ErrorCode.WriteData);
                _lastError.OnNext(exc.Message);
                return default;
            }
        }
    }

    /// <summary>Reads a specified byte count from the given data block and starting address.</summary>
    /// <remarks>The method attempts to read the requested bytes in chunks, retrying up to three times per
    /// chunk if necessary. If any chunk cannot be read after retries, the method returns an empty array. The returned
    /// array may be empty if the operation fails.</remarks>
    /// <param name="tag">The tag that identifies the target device or memory area to read from.</param>
    /// <param name="dataType">The data type that determines how the bytes are interpreted during the read
    /// operation.</param>
    /// <param name="db">The number of the data block from which to read.</param>
    /// <param name="startByteAdr">The zero-based starting byte address within the data block.</param>
    /// <param name="numBytes">The total number of bytes to read from the specified starting address.</param>
    /// <returns>A byte array containing the data read from the specified location. Returns an empty array if the read
    /// operation
    /// fails.</returns>
    private byte[] ReadMultipleBytes(Tag tag, DataType dataType, int db, int startByteAdr, int numBytes)
    {
        try
        {
            var resultBytes = new List<byte>();
            var index = startByteAdr;
            var maxChunkSize = Math.Max(1, _socketRx.DataReadLength - ReadChunkProtocolOverhead);
            var chunkSize = tag.Type == typeof(byte[])
                ? Math.Min(MaximumByteArrayReadChunk, maxChunkSize)
                : maxChunkSize;
            while (numBytes > 0)
            {
                var maxToRead = Math.Min(numBytes, chunkSize);
                var bytes = ReadBytesWithRetries(tag, dataType, db, index, maxToRead);

                if (bytes is null || bytes.Length == 0)
                {
                    if (maxToRead > 1)
                    {
                        chunkSize = Math.Max(1, maxToRead / ReadChunkReductionDivisor);
                        continue;
                    }

                    _lastErrorCode.OnNext(ErrorCode.ReadData);
                    _lastError.OnNext($"Tag {tag.Name} failed to read - unable to read chunk at DB{db}.DBB{index}.");
                    return [];
                }

                resultBytes.AddRange(bytes);
                numBytes -= bytes.Length;
                index += bytes.Length;

                if (bytes.Length < maxToRead && maxToRead > 1)
                {
                    chunkSize = Math.Max(1, bytes.Length);
                }
            }

            return [.. resultBytes];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>Reads a byte chunk with a small retry window.</summary>
    /// <param name="tag">The tag to read.</param>
    /// <param name="dataType">The data type.</param>
    /// <param name="db">The data block number.</param>
    /// <param name="index">The byte index.</param>
    /// <param name="maxToRead">The maximum bytes to read.</param>
    /// <returns>The read bytes, or null when all attempts fail.</returns>
    private byte[]? ReadBytesWithRetries(Tag tag, DataType dataType, int db, int index, int maxToRead)
    {
        for (var i = 0; i < ReadChunkMaxAttempts; i++)
        {
            var bytes = ReadBytes(tag, dataType, db, index, maxToRead);
            if (bytes?.Length > 0)
            {
                return bytes;
            }
        }

        return null;
    }

    /// <summary>
    /// Creates an observable sequence that periodically reads tags at the specified interval, emitting a notification
    /// each time the read operation is performed.
    /// </summary>
    /// <remarks>The observable automatically manages connection state and pauses polling if no tags are
    /// available or polling is paused. Errors encountered during tag reading are handled internally and do not
    /// terminate the observable sequence. The returned observable is shared among all subscribers and begins polling
    /// when the first subscription is made.</remarks>
    /// <param name="interval">The polling interval, in milliseconds, between consecutive tag read operations. Must be
    /// greater than zero.</param>
    /// <returns>An observable sequence that emits a value each time the tag reading process completes. The sequence
    /// completes
    /// when unsubscribed.</returns>
    private IObservable<Unit> TagReaderObservable(double interval) =>
        Observable.Create<Unit>(__ =>
            {
                var tim = Observable.Interval(TimeSpan.FromMilliseconds(interval))
                    .Subscribe(_ => StartTagPolling());

                return new SingleAssignmentDisposable { Disposable = tim };
            }).OnErrorRetry().Publish().RefCount();

    /// <summary>Starts a scheduled tag-polling cycle.</summary>
    private void StartTagPolling() => _ = ProcessTagPollingAsync();

    /// <summary>Processes one scheduled tag-polling cycle.</summary>
    /// <returns>A task that represents the asynchronous polling cycle.</returns>
    private async Task ProcessTagPollingAsync()
    {
        if (!IsConnectedValue ||
            _timeProvider.GetUtcNow().UtcDateTime - _lastConnectedAtUtc <
            TimeSpan.FromMilliseconds(ConnectionPollingGraceMilliseconds))
        {
            NotifyPaused(true);
            return;
        }

        var tagList = TagList.ToList().Where(tag => !tag.DoNotPoll).ToList();
        if (tagList.Count == 0 || _pause)
        {
            NotifyPaused(true);
            return;
        }

        _stopwatch.Restart();
        NotifyPaused(false);
        try
        {
            foreach (var tag in tagList)
            {
                try
                {
                    while (!IsConnectedValue)
                    {
                        await Task.Delay(ConnectionWaitDelayMilliseconds).ConfigureAwait(false);
                    }

                    _plcRequestSubject.OnNext(new PLCRequest(PlcRequestType.Read, tag));
                }
                catch (Exception ex)
                {
                    _lastError.OnNext(ex.Message);
                    _status.OnNext($"{tag.Name} could not be read from {tag.Address}. Error: " + ex);
                }
            }
        }
        finally
        {
            _stopwatch.Stop();
            _readTime.OnNext(_stopwatch.ElapsedTicks);
        }
    }

    /// <summary>Notifies pause observers while tolerating timer callbacks racing with disposal.</summary>
    /// <param name="isPaused">The pause state to publish.</param>
    private void NotifyPaused(bool isPaused)
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            _paused.OnNext(isPaused);
        }
        catch (ObjectDisposedException)
        {
            // A timer callback can overlap disposal; in that case the pause notification is no longer observable.
        }
    }

    /// <summary>
    /// Creates an observable sequence that periodically writes a watchdog value to the configured address, enabling
    /// external monitoring of connection health.
    /// </summary>
    /// <remarks>The observable will not emit any values if the watchdog address is null or whitespace. The
    /// sequence automatically retries on errors and is shared among all subscribers. The observable is considered
    /// active as long as there is at least one subscription.</remarks>
    /// <returns>An observable sequence that completes if the watchdog address is not defined, or emits a value
    /// each time
    /// the
    /// watchdog is written.</returns>
    private IObservable<Unit> WatchDogObservable() =>
        Observable.Create<Unit>(obs =>
        {
            if (string.IsNullOrWhiteSpace(WatchDogAddress))
            {
                // disable watchdog if not defined
                obs.OnCompleted();
                return Disposable.Empty;
            }

            // Setup the watchdog
            _ = TagOperations.AddUpdateTagItem(this, typeof(ushort), "WatchDog", WatchDogAddress!).SetPolling(false);

            var tim = Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(WatchDogWritingTime))
                .OnErrorRetry()
                .Subscribe(_ =>
            {
                lock (_lifecycleLock)
                {
                    if (IsDisposed || !IsConnectedValue)
                    {
                        return;
                    }

                    Value("WatchDog", WatchDogValueToWrite);
                    if (!ShowWatchDogWriting)
                    {
                        return;
                    }

                    _status.OnNext($"{_timeProvider.GetUtcNow().LocalDateTime} - WatchDog writing {WatchDogValueToWrite} to {WatchDogAddress}");
                }
            });

            return new SingleAssignmentDisposable { Disposable = tim };
        }).OnErrorRetry().Publish().RefCount();
}
