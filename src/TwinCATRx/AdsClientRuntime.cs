// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using TwinCAT;
using TwinCAT.Ads;
using TwinCAT.Ads.TypeSystem;
using TwinCAT.TypeSystem;

#if REACTIVE_SHIM
namespace IoT.Driver.TwinCATRx.Reactive;
#else
namespace IoT.Driver.TwinCATRx;
#endif

/// <summary>Adapts the Beckhoff ADS client to the runtime contract.</summary>
internal sealed class AdsClientRuntime : IAdsClientRuntime
{
    /// <summary>Serializes native subscription creation and disposal.</summary>
#if NET9_0_OR_GREATER || NETFRAMEWORK
    private readonly System.Threading.Lock _notificationGate = new();
#else
    private readonly object _notificationGate = new();
#endif

    /// <summary>Creates the shared loader.</summary>
    private readonly Func<AdsClient, ISymbolLoader> _createSymbolLoader;

    /// <summary>Stores the wrapped ADS client.</summary>
    private readonly AdsClient _client = new();

    /// <summary>Owns all native event subscriptions.</summary>
    private readonly CompositeDisposable _notifications = [];

    /// <summary>Tracks native connection disposal.</summary>
    private bool _disposed;

    /// <summary>Stores the shared symbol loader for this connection.</summary>
    private ISymbolLoader? _symbolLoader;

    /// <summary>Initializes a new instance of the <see cref="AdsClientRuntime"/> class.</summary>
    public AdsClientRuntime()
        : this(static client => SymbolLoaderFactory.Create(client, SymbolLoaderSettings.Default))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AdsClientRuntime"/> class.</summary>
    /// <param name="createSymbolLoader">The native symbol loader factory.</param>
    internal AdsClientRuntime(Func<AdsClient, ISymbolLoader> createSymbolLoader) =>
        _createSymbolLoader = createSymbolLoader;

    /// <inheritdoc/>
    public bool IsConnected => _client.IsConnected;

    /// <inheritdoc/>
    public int? Port => _client.Address?.Port;

    /// <inheritdoc/>
    public void Connect(int port) => _client.Connect(port);

    /// <inheritdoc/>
    public void Connect(string adsAddress, int port) => _client.Connect(adsAddress, port);

    /// <inheritdoc/>
    public uint CreateVariableHandle(string variable) => _client.CreateVariableHandle(variable);

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_notificationGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _notifications.Dispose();
            (_symbolLoader as IDisposable)?.Dispose();
            _symbolLoader = null;
            _client.Dispose();
        }
    }

    /// <inheritdoc/>
    public IObservable<object> ObserveValue(string variable, AdsTransMode adsTransMode, int cycleTime, int maxDelay)
    {
        IValueSymbol symbol;
        lock (_notificationGate)
        {
            ThrowIfDisposed();
            _symbolLoader ??= _createSymbolLoader(_client);
            symbol = (IValueSymbol)_symbolLoader.Symbols[variable];
            symbol.NotificationSettings = new NotificationSettings(adsTransMode, cycleTime, maxDelay);
        }

        return Observable.Create<object>(observer =>
        {
            lock (_notificationGate)
            {
                ThrowIfDisposed();
                var subscription = ObserveSymbolValue(symbol).Subscribe(observer);
                _ = subscription.DisposeWith(_notifications);
                return subscription;
            }
        });
    }

    /// <inheritdoc/>
    public object ReadAny(uint handle, Type type) => _client.ReadAny(handle, type);

    /// <inheritdoc/>
    public object ReadAny(uint handle, Type type, int[] lengths) => _client.ReadAny(handle, type, lengths);

    /// <inheritdoc/>
    public StateInfo ReadState() => _client.ReadState();

    /// <inheritdoc/>
    public void WriteAny(uint handle, object value) => _client.WriteAny(handle, value);

    /// <inheritdoc/>
    public void WriteControl(StateInfo state) => _client.WriteControl(state);

    /// <summary>Converts native value changes to a disposable observable.</summary>
    /// <param name="symbol">The native value symbol.</param>
    /// <returns>The sequence of values supplied by the symbol.</returns>
    internal static IObservable<object> ObserveSymbolValue(IValueSymbol symbol) =>
        Observable.Create<object>(observer =>
        {
            EventHandler<ValueChangedEventArgs> handler = (_, args) => observer.OnNext(args.Value);
            symbol.ValueChanged += handler;
            return ReactiveUI.Primitives.Disposables.Scope.Create(
                (Symbol: symbol, Handler: handler),
                static state => state.Symbol.ValueChanged -= state.Handler);
        });

    /// <summary>Rejects subscriptions after the native connection is disposed.</summary>
    private void ThrowIfDisposed()
    {
#if NET
        ObjectDisposedException.ThrowIf(_disposed, this);
#else
        if (!_disposed)
        {
            return;
        }

        throw new ObjectDisposedException(nameof(AdsClientRuntime));
#endif
    }
}
