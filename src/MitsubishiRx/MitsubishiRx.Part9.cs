// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;

#else

namespace MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiRx type.</summary>
public sealed partial class MitsubishiRx : IDisposable, IAsyncDisposable
{
    /// <summary>Executes the CreateWriteTagValueTask operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="tag">The tag parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The CreateWriteTagValueTask operation result.</returns>
    private Task<Responce>? CreateWriteTagValueTask(
        string tagName,
        MitsubishiTagDefinition tag,
        object? value,
        CancellationToken cancellationToken) =>
        tag.DataType switch
        {
            "Bit" => CreateBitWriteTask(tagName, value, cancellationToken),
            "String" => CreateStringWriteTask(tagName, value, cancellationToken),
            "Float" => CreateFloatWriteTask(tagName, value, cancellationToken),
            "DWord" or "UInt32" => CreateDWordWriteTask(tagName, value, cancellationToken),
            "Int32" => CreateInt32WriteTask(tagName, value, cancellationToken),
            "Int16" => CreateInt16WriteTask(tagName, value, cancellationToken),
            "UInt16" => CreateUInt16WriteTask(tagName, value, cancellationToken),
            _ => CreateDefaultWriteTask(tagName, tag, value, cancellationToken),
        };

    /// <summary>Executes the CreateBitWriteTask operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The CreateBitWriteTask operation result.</returns>
    private Task<Responce>? CreateBitWriteTask(
        string tagName,
        object? value,
        CancellationToken cancellationToken) => value is bool bit
            ? WriteBitsByTagAsync(tagName, [bit], cancellationToken)
            : null;

    /// <summary>Executes the CreateStringWriteTask operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The CreateStringWriteTask operation result.</returns>
    private Task<Responce>? CreateStringWriteTask(
        string tagName,
        object? value,
        CancellationToken cancellationToken) => value is string text
            ? WriteStringByTagAsync(tagName, text, cancellationToken)
            : null;

    /// <summary>Executes the CreateFloatWriteTask operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The CreateFloatWriteTask operation result.</returns>
    private Task<Responce>? CreateFloatWriteTask(
        string tagName,
        object? value,
        CancellationToken cancellationToken) => value is float single
            ? WriteFloatByTagAsync(tagName, single, cancellationToken)
            : null;

    /// <summary>Executes the CreateDWordWriteTask operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The CreateDWordWriteTask operation result.</returns>
    private Task<Responce>? CreateDWordWriteTask(
        string tagName,
        object? value,
        CancellationToken cancellationToken) => value is uint uint32
            ? WriteDWordByTagAsync(tagName, uint32, cancellationToken)
            : null;

    /// <summary>Executes the CreateInt32WriteTask operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The CreateInt32WriteTask operation result.</returns>
    private Task<Responce>? CreateInt32WriteTask(
        string tagName,
        object? value,
        CancellationToken cancellationToken) => value is int int32
            ? WriteInt32ByTagAsync(tagName, int32, cancellationToken)
            : null;

    /// <summary>Executes the CreateInt16WriteTask operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The CreateInt16WriteTask operation result.</returns>
    private Task<Responce>? CreateInt16WriteTask(
        string tagName,
        object? value,
        CancellationToken cancellationToken) => value is short int16
            ? WriteInt16ByTagAsync(tagName, int16, cancellationToken)
            : null;

    /// <summary>Executes the CreateUInt16WriteTask operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The CreateUInt16WriteTask operation result.</returns>
    private Task<Responce>? CreateUInt16WriteTask(
        string tagName,
        object? value,
        CancellationToken cancellationToken) => value is ushort uint16
            ? WriteUInt16ByTagAsync(tagName, uint16, cancellationToken)
            : null;

    /// <summary>Executes the CreateDefaultWriteTask operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="tag">The tag parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The CreateDefaultWriteTask operation result.</returns>
    private Task<Responce>? CreateDefaultWriteTask(
        string tagName,
        MitsubishiTagDefinition tag,
        object? value,
        CancellationToken cancellationToken)
    {
        if (HasEngineeringMetadata(tag) && value is double engineering)
        {
            return WriteScaledDoubleByTagAsync(tagName, engineering, cancellationToken);
        }

        return value is ushort rawWord
            ? WriteWordsByTagAsync(tagName, [rawWord], cancellationToken)
            : null;
    }

    /// <summary>Executes the ResolveTagAddress operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <returns>The ResolveTagAddress operation result.</returns>
    private string ResolveTagAddress(string tagName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        var database =
            TagDatabase
            ?? throw new InvalidOperationException(
                MitsubishiMessages.TagDatabaseRequiredForTagApis);
        return database.GetRequired(tagName).Address;
    }

    /// <summary>Executes the ResolveTagAddresses operation.</summary>
    /// <param name="tagNames">The tagNames parameter.</param>
    /// <returns>The ResolveTagAddresses operation result.</returns>
    private string[] ResolveTagAddresses(IEnumerable<string> tagNames)
    {
        ArgumentNullException.ThrowIfNull(tagNames);
        return tagNames.Select(ResolveTagAddress).ToArray();
    }

    /// <summary>Executes the PublishFault operation.</summary>
    /// <param name="description">The description parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <param name="response">The response parameter.</param>
    /// <param name="exception">The exception parameter.</param>
    private void PublishFault(
        string description,
        ReadOnlyMemory<byte> request,
        ReadOnlyMemory<byte> response,
        Exception exception)
    {
        PublishState(MitsubishiConnectionState.Faulted);
        PublishOperation(description, false, request, response, exception);
    }

    /// <summary>Executes the PublishOperation operation.</summary>
    /// <param name="description">The description parameter.</param>
    /// <param name="success">The success parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <param name="response">The response parameter.</param>
    /// <param name="exception">The exception parameter.</param>
    private void PublishOperation(
        string description,
        bool success,
        ReadOnlyMemory<byte> request,
        ReadOnlyMemory<byte> response,
        Exception? exception = null)
    {
        _operationLogs.OnNext(
            new MitsubishiOperationLog(
                _timeProvider.GetUtcNow(),
                _connectionStates.Value,
                description,
                success,
                request,
                response,
                exception));
    }

    /// <summary>Executes the PublishState operation.</summary>
    /// <param name="state">The state parameter.</param>
    private void PublishState(MitsubishiConnectionState state)
    {
        if (_connectionStates.Value == state)
        {
            return;
        }

        _connectionStates.OnNext(state);
    }

    /// <summary>Executes the ThrowIfDisposed operation.</summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
