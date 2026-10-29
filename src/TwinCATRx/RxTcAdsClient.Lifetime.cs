// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using IoT.Driver.TwinCATRx.Core.Reactive;
#else
using IoT.Driver.TwinCATRx.Core;
#endif
using TwinCAT.Ads;

#if REACTIVE_SHIM
namespace IoT.Driver.TwinCATRx.Reactive;
#else
namespace IoT.Driver.TwinCATRx;
#endif

/// <summary>Manages a reactive TwinCAT ADS client and its connection lifetime.</summary>
public partial class RxTcAdsClient
{
    /// <summary>Synchronizes connection lifetime changes and publications.</summary>
    private readonly object _lifecycleGate = new();

    /// <summary>Tracks the connection associated with the current execution context.</summary>
    private readonly AsyncLocal<ConnectionScope?> _executingConnection = new();

    /// <summary>The current connection, if any.</summary>
    private ConnectionScope? _connection;

    /// <summary>The last connection state used when no connection is active.</summary>
    private ConnectionScope _lastConnection = new();

    /// <summary>The number of publications in progress.</summary>
    private int _activePublications;

    /// <summary>Indicates whether the client signals have been disposed.</summary>
    private int _signalsDisposed;

    /// <summary>Identifies the latest pause state transition while the lifecycle gate is held.</summary>
    private long _pauseVersion;

    /// <summary>Gets the state associated with the current execution or connection.</summary>
    private ConnectionScope ConnectionState => _executingConnection.Value ?? _connection ?? _lastConnection;

    /// <summary>Checks whether a connection is active and belongs to this client.</summary>
    /// <param name="connection">The connection to check.</param>
    /// <returns>True if the connection is current and open; otherwise, false.</returns>
    private bool IsCurrentConnection(ConnectionScope? connection) =>
        !IsDisposed && connection?.Closed == false && ReferenceEquals(_connection, connection);

    /// <summary>Runs an action while retaining the current connection's resources.</summary>
    /// <param name="connection">The connection to retain.</param>
    /// <param name="action">The action to run if the connection is current.</param>
    private void RunConnection(ConnectionScope connection, Action action)
    {
        lock (_lifecycleGate)
        {
            if (!IsCurrentConnection(connection))
            {
                return;
            }

            connection.ActiveOperations++;
        }

        var previous = _executingConnection.Value;
        _executingConnection.Value = connection;
        try
        {
            action();
        }
        finally
        {
            _executingConnection.Value = previous;
            bool release;
            lock (_lifecycleGate)
            {
                connection.ActiveOperations--;
                release = connection.Closed && connection.ActiveOperations == 0;
            }

            if (release)
            {
                connection.Dispose();
            }
        }
    }

    /// <summary>Subscribes to a source for the current connection lifetime.</summary>
    /// <typeparam name="T">The source value type.</typeparam>
    /// <param name="source">The source to subscribe to.</param>
    /// <param name="onNext">The value handler.</param>
    /// <param name="onError">The error handler, or null to publish errors.</param>
    /// <param name="onCompleted">The optional completion handler.</param>
    /// <returns>The source subscription.</returns>
    private IDisposable SubscribeConnection<T>(
        IObservable<T> source,
        Action<T> onNext,
        Action<Exception>? onError = null,
        Action? onCompleted = null)
    {
        var connection = _executingConnection.Value ?? _connection ??
            throw new InvalidOperationException("The TwinCAT connection lifetime is not initialized.");
        return ObservableBridgeExtensions.SubscribeTo(
            source,
            value =>
            {
                var origin = _executingConnection.Value;
                if (origin is not null && !ReferenceEquals(origin, connection))
                {
                    return;
                }

                RunConnection(connection, () => onNext(value));
            },
            error => RunConnection(connection, () => (onError ?? PublishError)(error)),
            () => RunConnection(connection, () => onCompleted?.Invoke()));
    }

    /// <summary>Publishes a connection error.</summary>
    /// <param name="error">The error to publish.</param>
    private void PublishError(Exception error) => Publish(_errorReceived, error);

    /// <summary>Runs a public request against the active connection regardless of inherited callback context.</summary>
    /// <param name="action">The request to run.</param>
    private void QueueCurrentConnection(Action action) => QueueConnection(action, useCurrentConnection: true);

    /// <summary>Runs an action in the current connection, when one exists.</summary>
    /// <param name="action">The action to run.</param>
    /// <param name="useCurrentConnection">Whether this is a public request for the active connection.</param>
    private void QueueConnection(Action action, bool useCurrentConnection = false)
    {
        ConnectionScope? connection;
        lock (_lifecycleGate)
        {
            connection = useCurrentConnection ? _connection : _executingConnection.Value ?? _connection;
        }

        if (connection is null)
        {
            return;
        }

        RunConnection(connection, action);
    }

    /// <summary>Publishes a value while retaining client signals.</summary>
    /// <typeparam name="T">The published value type.</typeparam>
    /// <param name="observer">The observer receiving the value.</param>
    /// <param name="value">The value to publish.</param>
    /// <param name="isCurrent">An optional state check performed when admitting the publication.</param>
    private void Publish<T>(IObserver<T> observer, T value, Func<bool>? isCurrent = null)
    {
        lock (_lifecycleGate)
        {
            var connection = _executingConnection.Value;
            if (IsDisposed || (connection is not null && !IsCurrentConnection(connection)) || isCurrent?.Invoke() == false)
            {
                return;
            }

            _activePublications++;
        }

        try
        {
            observer.OnNext(value);
        }
        finally
        {
            bool release;
            lock (_lifecycleGate)
            {
                _activePublications--;
                release = IsDisposed && _activePublications == 0;
            }

            if (release)
            {
                DisposeSignals();
            }
        }
    }

    /// <summary>Closes a connection and releases its resources after active operations finish.</summary>
    /// <param name="expected">The connection to close, or null for the current connection.</param>
    private void CloseConnection(ConnectionScope? expected = null)
    {
        ConnectionScope? resources;
        lock (_lifecycleGate)
        {
            resources = CloseConnectionLocked(expected ?? _connection);
        }

        resources?.Dispose();
    }

    /// <summary>Marks a connection closed while the lifecycle gate is held.</summary>
    /// <param name="connection">The connection to close.</param>
    /// <returns>Resources ready for disposal, or null if operations remain.</returns>
    private ConnectionScope? CloseConnectionLocked(ConnectionScope? connection)
    {
        if (ReferenceEquals(_connection, connection))
        {
            _connection = null;
            Connected = false;
        }

        if (connection is not null)
        {
            connection.Closed = true;
        }

        return connection?.ActiveOperations == 0 ? connection : null;
    }

    /// <summary>Holds state and resources for one connection lifetime.</summary>
    private sealed class ConnectionScope : IDisposable
    {
        /// <summary>Tracks whether this scope's resources have been released.</summary>
        private int _disposed;

        /// <summary>The current notification lifetime.</summary>
        private CompositeDisposable? _notifications;

        /// <summary>Initializes a new instance of the <see cref="ConnectionScope"/> class.</summary>
        internal ConnectionScope()
        {
            _ = ClientState.DisposeWith(Resources);
            _ = ServiceStatus.DisposeWith(Resources);
            _ = Reads.DisposeWith(Resources);
            _ = Writes.DisposeWith(Resources);
        }

        /// <summary>Gets the connection state signal.</summary>
        internal Signal<AdsState> ClientState { get; } = new();

        /// <summary>Gets the latest service status signal.</summary>
        internal ReplaySignal<ServiceStatus> ServiceStatus { get; } = new(1);

        /// <summary>Gets the read request signal.</summary>
        internal Signal<(uint? Handle, Type Type, int Length, string? Id)> Reads { get; } = new();

        /// <summary>Gets the write request signal.</summary>
        internal Signal<(uint? Handle, object Value, int Length, string? Id)> Writes { get; } = new();

        /// <summary>Gets resources retained for this connection.</summary>
        internal CompositeDisposable Resources { get; } = [];

        /// <summary>Gets or sets the number of active operations.</summary>
        internal int ActiveOperations { get; set; }

        /// <summary>Gets or sets a value indicating whether the connection is closed.</summary>
        internal bool Closed { get; set; }

        /// <summary>Gets generated code for the connection.</summary>
        internal List<string> Code { get; } = [];

        /// <summary>Gets the type associated with each variable name.</summary>
        internal Dictionary<string, Type> TypeInfo { get; } = [with(StringComparer.OrdinalIgnoreCase)];

        /// <summary>Gets names indexed by read/write handle.</summary>
        internal Dictionary<uint, string> ReadWriteVariables { get; } = [];

        /// <summary>Gets names indexed by write handle.</summary>
        internal Dictionary<uint, string> WriteVariables { get; } = [];

        /// <summary>Gets read/write handles indexed by variable name.</summary>
        internal Dictionary<string, uint?> ReadWriteHandles { get; } = [with(StringComparer.OrdinalIgnoreCase)];

        /// <summary>Gets write handles and array lengths indexed by variable name.</summary>
        internal Dictionary<string, (uint? Handle, int ArrayLength)> WriteHandles { get; } = [with(StringComparer.OrdinalIgnoreCase)];

        /// <summary>Gets or sets a value indicating whether initialization completed.</summary>
        internal bool Initialized { get; set; }

        /// <summary>Gets or sets the connection's code generator.</summary>
        internal ICodeGenerator? CodeGenerator { get; set; }

        /// <summary>Gets the current notification lifetime.</summary>
        internal CompositeDisposable? Notifications => _notifications;

        /// <summary>Gets or sets the connection settings.</summary>
        internal ISettings? Settings { get; set; }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            try
            {
                Resources.Dispose();
            }
            finally
            {
                Code.Clear();
                TypeInfo.Clear();
                ReadWriteVariables.Clear();
                WriteVariables.Clear();
                ReadWriteHandles.Clear();
                WriteHandles.Clear();
                CodeGenerator = null;
                Settings = null;
                _notifications = null;
                Initialized = false;
            }
        }

        /// <summary>Replaces the current notification lifetime.</summary>
        /// <param name="value">The notification lifetime, or null.</param>
        internal void SetNotifications(CompositeDisposable? value) => _notifications = value;
    }

    /// <summary>Closes a particular connection when disposed.</summary>
    /// <param name="owner">The client that owns the connection.</param>
    /// <param name="connection">The connection to close.</param>
    private sealed class ConnectionSubscription(RxTcAdsClient owner, ConnectionScope connection) : IDisposable
    {
        /// <inheritdoc/>
        public void Dispose() => owner.CloseConnection(connection);
    }
}
