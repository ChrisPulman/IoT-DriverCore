// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive.Tests;
#else

namespace IoT.DriverCore.MitsubishiRx.Tests;
#endif

/// <summary>Provides the FakeTransport type.</summary>
internal sealed class FakeTransport : IMitsubishiTransport
{
    /// <summary>Stores the production simulator used by legacy transport-focused tests.</summary>
    private readonly MitsubishiSimulatorTransport _simulator;

    /// <summary>Initializes a new instance of the <see cref="FakeTransport"/> class.</summary>
    /// <param name="responses">The queued responses.</param>
    public FakeTransport(IEnumerable<byte[]> responses)
    {
        _simulator = new(responses);
    }

    /// <summary>Initializes a new instance of the <see cref="FakeTransport"/> class.</summary>
    /// <param name="responseFactory">The response factory.</param>
    public FakeTransport(Func<MitsubishiTransportRequest, byte[]> responseFactory)
    {
        _simulator = new(responseFactory);
    }

    /// <summary>Gets stores the IsConnected field.</summary>
    public bool IsConnected => _simulator.IsConnected;

    /// <summary>Gets the Requests property.</summary>
    internal IReadOnlyList<MitsubishiTransportRequest> Requests => _simulator.Requests;

    /// <summary>Executes the ConnectAsync operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ConnectAsync operation result.</returns>
    public ValueTask ConnectAsync(MitsubishiClientOptions options, CancellationToken cancellationToken = default)
        => _simulator.ConnectAsync(options, cancellationToken);

    /// <summary>Executes the DisconnectAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The DisconnectAsync operation result.</returns>
    public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
        => _simulator.DisconnectAsync(cancellationToken);

    /// <summary>Executes the ExchangeAsync operation.</summary>
    /// <param name="request">The request parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ExchangeAsync operation result.</returns>
    public ValueTask<byte[]> ExchangeAsync(
        MitsubishiTransportRequest request,
        CancellationToken cancellationToken = default)
        => _simulator.ExchangeAsync(request, cancellationToken);

    /// <summary>Executes the Dispose operation.</summary>
    public void Dispose()
        => _simulator.Dispose();

    /// <summary>Executes the DisposeAsync operation.</summary>
    /// <returns>The DisposeAsync operation result.</returns>
    public ValueTask DisposeAsync() => _simulator.DisposeAsync();
}
