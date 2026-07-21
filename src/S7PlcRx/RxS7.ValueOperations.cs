// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
#if REACTIVE_SHIM
using S7PlcRx.Reactive.Core;
using S7PlcRx.Reactive.Enums;
using S7PlcRx.Reactive.PlcTypes;
#else
using S7PlcRx.Core;
using S7PlcRx.Enums;
using S7PlcRx.PlcTypes;
#endif

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive;
#else
namespace S7PlcRx;
#endif

/// <summary>Contains value and lifecycle members for <see cref="RxS7"/>.</summary>
public partial class RxS7
{
    /// <summary>Asynchronously retrieves a variable value while pausing polling operations.</summary>
    /// <remarks>Polling is temporarily paused while the value is being read to ensure consistency. If no
    /// polling is active, the method may wait briefly before proceeding. The method is cancellation-friendly and will
    /// respond promptly to cancellation requests.</remarks>
    /// <typeparam name="T">The expected type of the variable's value to retrieve.</typeparam>
    /// <param name="tag">The typed logical tag key to read.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. Its result contains the specified variable
    /// cast to type T, or the default value of T if the variable is not found or cannot be cast.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="tag"/> is null.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown if the operation is canceled via the cancellation token.
    /// </exception>
    public async Task<T?> ReadAsync<T>(LogicalTagKey<T> tag, CancellationToken cancellationToken)
    {
        Guard.NotNull(tag, nameof(tag));
        var variable = tag.Name;

        _pause = true;
        try
        {
            // Wait until the poll loop observes the paused state.
            // If nothing is polling, the observable might never emit; in that case, proceed after a short delay.
            // This keeps behavior compatible while still being cancellation-friendly.
            try
            {
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
#if NET8_0_OR_GREATER
                await using var reg = cancellationToken.Register(() => tcs.TrySetCanceled());
#else
                using var reg = cancellationToken.Register(() => tcs.TrySetCanceled());
#endif
                void SetResult(bool result) => tcs.TrySetResult(result);
                void SetException(Exception exception) => tcs.TrySetException(exception);

                using var sub = _paused.Where(x => x).Take(1).Subscribe(SetResult, SetException);
                await tcs.Task.ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (TaskCanceledException)
            {
                throw new OperationCanceledException(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Pause wait failed; direct read fallback will be used. {ex}");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var storedTag = TagList[variable];
            if (storedTag?.Type == typeof(object))
            {
                storedTag.Type = typeof(T);
            }

            for (var attempt = 0; attempt < ValueReadMaxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                GetTagValue(storedTag);
                if (TagValueIsValid<T>(storedTag))
                {
                    return (T?)storedTag?.Value;
                }

                await Task.Delay(ValueReadRetryDelayMilliseconds, cancellationToken).ConfigureAwait(false);
            }

            return default;
        }
        finally
        {
            _pause = false;
        }
    }

    /// <summary>Releases resources used by this instance.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Retrieves detailed information about the connected CPU as an observable sequence.</summary>
    /// <remarks>The method waits until a connection is established before retrieving CPU information. If the
    /// required data is not immediately available, the method will retry until successful or until the subscription is
    /// disposed. The order and content of the returned string array correspond to specific CPU information fields. This
    /// method is intended for use in reactive programming scenarios where CPU information is needed
    /// asynchronously.</remarks>
    /// <returns>An observable sequence that emits CPU information fields, such as the AS name, module
    /// name, copyright, serial number, module type name, order code, and version numbers. The sequence completes after
    /// emitting the data.</returns>
    public IObservable<string[]> GetCpuInfo() =>
        Observable.Create<string[]>(obs =>
        {
            var cancellation = new CancellationTokenSource();
            var subscription = IsConnected.Where(isConnected => isConnected).Take(1).Subscribe(
                isConnected => _ = PublishCpuInfoAsync(obs, cancellation.Token));
            return new CompositeDisposable(subscription, Disposable.Create(cancellation.Cancel));
        });

    /// <summary>Writes a class object's serialized representation to the specified data block address.</summary>
    /// <param name="tag">The tag that identifies the target location for the write operation.</param>
    /// <param name="classValue">The class object to serialize and write. Cannot be null.</param>
    /// <param name="db">The number of the data block to which the class data will be written.</param>
    /// <param name="startByteAdr">
    /// The starting byte address within the data block at which to begin writing. The default is 0.
    /// </param>
    /// <returns>true if the class data was successfully written; otherwise, false.</returns>
    internal bool WriteClass(Tag tag, object classValue, int db, int startByteAdr = 0)
    {
        if (classValue is null)
        {
            return false;
        }

        var bytes = new byte[(int)Class.GetClassSize(classValue)];
        _ = Class.ToBytes(classValue, bytes);
        return WriteMultipleBytes(tag, [.. bytes], db, startByteAdr);
    }

    /// <summary>Adds a new tag to the collection or updates an existing tag with the same name.</summary>
    /// <param name="tag">The tag to add or update. Its Address property must not be null, empty, or whitespace.
    /// characters.</param>
    /// <exception cref="TagAddressOutOfRangeException">
    /// Thrown if the tag's Address property is null, empty, or consists only of white-space characters.
    /// </exception>
    internal void AddUpdateTagItemInternal(Tag tag)
    {
        if (tag is null || string.IsNullOrWhiteSpace(tag.Name))
        {
            throw new ArgumentNullException(nameof(tag));
        }

        if (string.IsNullOrWhiteSpace(tag.Address))
        {
            throw new TagAddressOutOfRangeException(tag);
        }

        var tagName = tag.Name!;
        _lockTagList.Wait();
        try
        {
            if (TagList[tagName] is Tag tagExists)
            {
                tagExists.Name = tagName;
                tagExists.Value = tag.Value;
                tagExists.Address = tag.Address;
                tagExists.Type = tag.Type;
                tagExists.ArrayLength = tag.ArrayLength;
            }
            else
            {
                TagList.Add(tag);
            }
        }
        finally
        {
            _ = _lockTagList.Release();
        }
    }

    /// <summary>Removes the tag item with the specified name from the collection, if it exists.</summary>
    /// <param name="tagName">The name of the tag item to remove. It cannot be null, empty, or whitespace.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="tagName"/> is null, empty, or consists only of white-space characters.
    /// </exception>
    internal void RemoveTagItemInternal(string tagName)
    {
        if (HasNoText(tagName))
        {
            throw new ArgumentNullException(nameof(tagName));
        }

        _lockTagList.Wait();
        try
        {
            if (TagList.ContainsKey(tagName))
            {
                TagList.Remove(tagName);
            }
        }
        finally
        {
            _ = _lockTagList.Release();
        }
    }

    /// <summary>Reads multiple PLC variables in one operation and returns their values as a dictionary.</summary>
    /// <remarks>The returned dictionary uses case-insensitive keys based on the tag names. If a variable
    /// cannot be read, its value in the dictionary will be <see langword="null"/>. The method returns <see
    /// langword="null"/> if the input list is null, empty, or contains invalid tags. This method is not thread-safe and
    /// should be called with appropriate synchronization if used concurrently.</remarks>
    /// <param name="tags">Tags specifying the variables to read. Each must have a valid name, address,
    /// and array length.</param>
    /// <returns>A dictionary mapping tag names to corresponding values, or <see langword="null"/> if the read operation
    /// fails or if the input is invalid.</returns>
    internal Dictionary<string, object?>? ReadMultiVar(IReadOnlyList<Tag> tags)
    {
        if (tags is null || tags.Count == 0)
        {
            return null;
        }

        lock (_socketLock)
        {
            var pool = ArrayPool<byte>.Shared;
            var receiveBuffer = pool.Rent(_socketRx.DataReadLength + ProtocolReceivePadding);
            var parsed = new List<S7MultiVar.ReadResult>();
            try
            {
                if (!TryBuildMultiVarReadItems(tags, out var items, out var varTypes, out var arrayLengths))
                {
                    return null;
                }

                var request = S7MultiVar.BuildReadVarRequest(items);
                if (!TrySendMultiVarRequest(_socketRx, tags[0], request, receiveBuffer))
                {
                    return null;
                }

                parsed.AddRange(S7MultiVar.ParseReadVarResponse(receiveBuffer, items, pool));
                return parsed.Count == 0
                    ? null
                    : CreateMultiVarReadResult(tags, parsed, varTypes, arrayLengths, ParseBytes);
            }
            catch (Exception ex)
            {
                _lastErrorCode.OnNext(ErrorCode.ReadData);
                _lastError.OnNext(ex.Message);
                return null;
            }
            finally
            {
                foreach (var r in parsed)
                {
                    if (r.RentedBuffer is not null)
                    {
                        pool.Return(r.RentedBuffer);
                    }
                }

                pool.Return(receiveBuffer);
            }
        }
    }

    /// <summary>Writes the specified tags to the connected device in a multi-variable operation.</summary>
    /// <remarks>This method performs a batch write operation, sending all tag values in a single request. If
    /// any tag is invalid or the write operation fails for any tag, the method returns false. The operation is not
    /// atomic; partial writes may occur if an error is encountered during the process.</remarks>
    /// <param name="tags">A read-only list of <see cref="Tag"/> objects representing the variables to write.
    /// Each tag must have a valid
    /// name, address, and new value. The list cannot be null or empty.</param>
    /// <returns>true if all tags are written successfully; otherwise, false.</returns>
    internal bool WriteMultiVar(IReadOnlyList<Tag> tags)
    {
        if (tags is null || tags.Count == 0)
        {
            return false;
        }

        lock (_socketLock)
        {
            var receiveBuffer = new byte[1024];
            try
            {
                if (!TryBuildMultiVarWriteItems(tags, out var items))
                {
                    return false;
                }

                var request = S7MultiVar.BuildWriteVarRequest(items);
                return TrySendMultiVarRequest(_socketRx, tags[0], request, receiveBuffer) &&
                    AreMultiVarWriteResultsSuccessful(receiveBuffer, items.Count);
            }
            catch (Exception ex)
            {
                _lastErrorCode.OnNext(ErrorCode.WriteData);
                _lastError.OnNext(ex.Message);
                return false;
            }
        }
    }

    /// <summary>Releases unmanaged and - optionally - managed resources.</summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources;
    /// <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        if (!disposing)
        {
            return;
        }

        _disposables.Dispose();
        DisposeSemaphore(_lock, LockName);
        DisposeSemaphore(_lockTagList, "Tag list lock");
        _dataRead.Dispose();
        _lastError.Dispose();
        ((IDisposable)_socketRx).Dispose();
        _lastErrorCode.Dispose();
        _paused.Dispose();
        _plcRequestSubject.Dispose();
        _status.Dispose();
        _readTime.Dispose();
    }
}
