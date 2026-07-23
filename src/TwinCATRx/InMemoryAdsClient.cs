// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
#if REACTIVE_SHIM
using IoT.DriverCore.TwinCATRx.Core.Reactive;
#else
using IoT.DriverCore.TwinCATRx.Core;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.TwinCATRx.Reactive;
#else
namespace IoT.DriverCore.TwinCATRx;
#endif

/// <summary>
/// Provides a deterministic, production-usable ADS simulator for applications that need to run without a TwinCAT
/// runtime or physical PLC.
/// </summary>
public sealed class InMemoryAdsClient : IRxTcAdsClient
{
    /// <summary>Publishes generated symbol descriptions.</summary>
    private readonly Signal<string[]> _code = new();

    /// <summary>Publishes connection state changes.</summary>
    private readonly ReplaySignal<InMemoryAdsConnectionState> _connectionStates = new(1);

    /// <summary>Publishes read responses and configured notification values.</summary>
    private readonly Signal<(string Variable, object? Data, string? Id)> _dataReceived = new();

    /// <summary>Publishes deterministic simulator failures.</summary>
    private readonly Signal<Exception> _errors = new();

    /// <summary>Publishes successful initialization events.</summary>
    private readonly ReplaySignal<Unit> _initialized = new(1);

    /// <summary>Publishes pause state changes.</summary>
    private readonly ReplaySignal<bool> _paused = new(1);

    /// <summary>Publishes correlated write results.</summary>
    private readonly Signal<string?> _writes = new();

    /// <summary>Synchronizes symbol, fault, and lifecycle state.</summary>
    private readonly object _gate = new();

    /// <summary>Stores queued deterministic failures by operation.</summary>
    private readonly Dictionary<InMemoryAdsOperation, Queue<Exception>> _faults = [];

    /// <summary>Stores registered symbols by case-insensitive ADS variable name.</summary>
    private readonly Dictionary<string, InMemoryAdsSymbol> _symbols = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Stores the current pause timer.</summary>
    private Timer? _pauseTimer;

    /// <summary>Stores whether this instance has been disposed.</summary>
    private int _disposed;

    /// <summary>Stores the next deterministic ADS handle.</summary>
    private uint _nextHandle = 1;

    /// <summary>Stores the number of native read attempts since the metrics were reset.</summary>
    private long _readOperations;

    /// <summary>Stores the number of native write attempts since the metrics were reset.</summary>
    private long _writeOperations;

    /// <summary>Stores the number of notification publication attempts since the metrics were reset.</summary>
    private long _notificationPublications;

    /// <summary>Initializes a new instance of the <see cref="InMemoryAdsClient"/> class.</summary>
    public InMemoryAdsClient()
    {
        ConnectionState = InMemoryAdsConnectionState.Disconnected;
        _connectionStates.OnNext(ConnectionState);
        _paused.OnNext(false);
    }

    /// <inheritdoc/>
    public IObservable<string[]> Code => _code;

    /// <summary>Gets a value indicating whether the simulator is connected.</summary>
    public bool Connected => ConnectionState == InMemoryAdsConnectionState.Connected;

    /// <summary>Gets the current simulator connection state.</summary>
    public InMemoryAdsConnectionState ConnectionState { get; private set; }

    /// <summary>Gets the observable connection state stream.</summary>
    public IObservable<InMemoryAdsConnectionState> ConnectionStates => _connectionStates;

    /// <inheritdoc/>
    public IObservable<(string Variable, object? Data, string? Id)> DataReceived => _dataReceived;

    /// <inheritdoc/>
    public IObservableAsync<(string Variable, object? Data, string? Id)> DataReceivedAsync =>
        ObservableBridgeExtensions.ToAsyncObservable(DataReceived);

    /// <inheritdoc/>
    public IObservable<Exception> ErrorReceived => _errors;

    /// <inheritdoc/>
    public IObservableAsync<Exception> ErrorReceivedAsync =>
        ObservableBridgeExtensions.ToAsyncObservable(ErrorReceived);

    /// <inheritdoc/>
    public IObservable<Unit> InitializeComplete => _initialized;

    /// <inheritdoc/>
    public IObservableAsync<Unit> InitializeCompleteAsync =>
        ObservableBridgeExtensions.ToAsyncObservable(InitializeComplete);

    /// <inheritdoc/>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <inheritdoc/>
    public bool IsPaused { get; private set; }

    /// <inheritdoc/>
    public IObservable<bool> IsPausedObservable => _paused;

    /// <inheritdoc/>
    public IObservableAsync<bool> IsPausedObservableAsync =>
        ObservableBridgeExtensions.ToAsyncObservable(IsPausedObservable);

    /// <inheritdoc/>
    public IObservable<string?> OnWrite => _writes;

    /// <inheritdoc/>
    public IObservableAsync<string?> OnWriteAsync => ObservableBridgeExtensions.ToAsyncObservable(OnWrite);

    /// <inheritdoc/>
    public IDictionary<string, uint?> ReadWriteHandleInfo { get; } =
        new Dictionary<string, uint?>(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public ISettings? Settings { get; private set; }

    /// <summary>Gets a deterministic snapshot of native ADS operation counts.</summary>
    public InMemoryAdsOperationMetrics OperationMetrics =>
        new(
            Interlocked.Read(ref _readOperations),
            Interlocked.Read(ref _writeOperations),
            Interlocked.Read(ref _notificationPublications));

    /// <summary>Gets a snapshot of all registered symbols.</summary>
    public IReadOnlyCollection<InMemoryAdsSymbol> Symbols
    {
        get
        {
            lock (_gate)
            {
                return _symbols.Values.ToArray();
            }
        }
    }

    /// <inheritdoc/>
    public IDictionary<string, (uint? Handle, int ArrayLength)> WriteHandleInfo { get; } =
        new Dictionary<string, (uint? Handle, int ArrayLength)>(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    [RequiresUnreferencedCode("Maintains compatibility with IRxTcAdsClient; the simulator itself does not emit code.")]
    [RequiresDynamicCode("Maintains compatibility with IRxTcAdsClient; the simulator itself does not emit code.")]
    public void Connect(ISettings settings)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        ThrowIfDisposed();
        Settings = settings;
        SetConnectionState(InMemoryAdsConnectionState.Connecting);
        if (TryTakeFault(InMemoryAdsOperation.Connect, out var queuedFault))
        {
            FailConnection(queuedFault);
            return;
        }

        if (!TryBuildConfiguredHandles(out var configurationError))
        {
            FailConnection(configurationError);
            return;
        }

        SetConnectionState(InMemoryAdsConnectionState.Connected);
        _code.OnNext(CreateSymbolDescriptions());
        _initialized.OnNext(Unit.Default);
        PublishNotifications();
    }

    /// <inheritdoc/>
    public void Disconnect()
    {
        if (IsDisposed)
        {
            return;
        }

        StopPause();
        SetConnectionState(InMemoryAdsConnectionState.Disconnected);
    }

    /// <summary>Reconnects with the latest settings while preserving registered symbols and queued faults.</summary>
    [RequiresUnreferencedCode("Maintains compatibility with IRxTcAdsClient; the simulator itself does not emit code.")]
    [RequiresDynamicCode("Maintains compatibility with IRxTcAdsClient; the simulator itself does not emit code.")]
    public void Reconnect()
    {
        ThrowIfDisposed();
        var settings = Settings ??
            throw new InvalidOperationException("The in-memory ADS client has not been connected before.");
        Disconnect();
        Connect(settings);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        lock (_gate)
        {
            _pauseTimer?.Dispose();
            _pauseTimer = null;
            IsPaused = false;
            ReadWriteHandleInfo.Clear();
            WriteHandleInfo.Clear();
            _faults.Clear();
            _symbols.Clear();
        }

        SetConnectionState(InMemoryAdsConnectionState.Disposed);
        _paused.Dispose();
        _connectionStates.Dispose();
        _code.Dispose();
        _dataReceived.Dispose();
        _errors.Dispose();
        _initialized.Dispose();
        _writes.Dispose();
    }

    /// <inheritdoc/>
    public void Pause(TimeSpan time)
    {
        ThrowIfDisposed();
        if (time <= TimeSpan.Zero)
        {
            return;
        }

        lock (_gate)
        {
            _pauseTimer?.Dispose();
            IsPaused = true;
            _pauseTimer = new(
                static state => ((InMemoryAdsClient)state!).StopPause(),
                this,
                time,
                Timeout.InfiniteTimeSpan);
        }

        _paused.OnNext(true);
    }

    /// <summary>Publishes every configured notification using the latest in-memory symbol values.</summary>
    public void PublishNotifications()
    {
        ThrowIfDisposed();
        if (!Connected)
        {
            ReportError(
                new InMemoryAdsException(
                    InMemoryAdsOperation.Notification,
                    "Notification publication requires a connected in-memory ADS client."));
            return;
        }

        foreach (var notification in Settings?.Notifications ?? [])
        {
            PublishNotification(notification.Variable);
        }
    }

    /// <summary>Queues a failure for the next matching operation.</summary>
    /// <param name="operation">The operation that will consume the failure.</param>
    /// <param name="error">The error to publish.</param>
    /// <returns>This simulator for fluent setup.</returns>
    public InMemoryAdsClient QueueFault(InMemoryAdsOperation operation, Exception error)
    {
        if (error is null)
        {
            throw new ArgumentNullException(nameof(error));
        }

        ThrowIfDisposed();
        lock (_gate)
        {
            if (!_faults.TryGetValue(operation, out var faults))
            {
                faults = new();
                _faults.Add(operation, faults);
            }

            faults.Enqueue(error);
        }

        return this;
    }

    /// <inheritdoc/>
    public void Read(string variable) => Read(variable, null, null);

    /// <inheritdoc/>
    public void Read(string variable, string? id) => Read(variable, null, id);

    /// <inheritdoc/>
    public void Read(string variable, int? arrayLength) => Read(variable, arrayLength, null);

    /// <inheritdoc/>
    public void Read(string variable, int? arrayLength, string? id)
    {
        ThrowIfDisposed();
        _ = Interlocked.Increment(ref _readOperations);
        var checkedVariable = Required(variable, nameof(variable));
        if (TryTakeFault(InMemoryAdsOperation.Read, out var queuedFault))
        {
            PublishReadFailure(checkedVariable, id, queuedFault);
            return;
        }

        if (!Connected)
        {
            PublishReadFailure(
                checkedVariable,
                id,
                new InMemoryAdsException(
                    InMemoryAdsOperation.Read,
                    "ADS reads require a connected in-memory client.",
                    checkedVariable));
            return;
        }

        InMemoryAdsSymbol? symbol;
        lock (_gate)
        {
            _ = _symbols.TryGetValue(checkedVariable, out symbol);
        }

        if (symbol is null)
        {
            PublishReadFailure(
                checkedVariable,
                id,
                new InMemoryAdsException(
                    InMemoryAdsOperation.Read,
                    $"ADS symbol '{checkedVariable}' is not registered.",
                    checkedVariable));
            return;
        }

        if (!symbol.IsReadable)
        {
            PublishReadFailure(
                checkedVariable,
                id,
                new InMemoryAdsException(
                    InMemoryAdsOperation.Read,
                    $"ADS symbol '{checkedVariable}' is write-only.",
                    checkedVariable));
            return;
        }

        try
        {
            _dataReceived.OnNext((symbol.Name, SliceValue(symbol.Value, arrayLength), id));
        }
        catch (Exception error)
        {
            PublishReadFailure(checkedVariable, id, error);
        }
    }

    /// <summary>Reads several symbols in request order.</summary>
    /// <param name="variables">The variables to read.</param>
    public void ReadMany(IEnumerable<string> variables) => ReadMany(variables, null);

    /// <summary>Reads several symbols in request order.</summary>
    /// <param name="variables">The variables to read.</param>
    /// <param name="correlationPrefix">The correlation prefix.</param>
    public void ReadMany(IEnumerable<string> variables, string? correlationPrefix)
    {
        if (variables is null)
        {
            throw new ArgumentNullException(nameof(variables));
        }

        var index = 0;
        foreach (var variable in variables)
        {
            Read(variable, correlationPrefix is null ? null : $"{correlationPrefix}:{index}");
            index++;
        }
    }

    /// <summary>Registers or replaces an in-memory ADS symbol.</summary>
    /// <param name="name">The symbol name.</param>
    /// <param name="value">The initial value.</param>
    /// <returns>This simulator for fluent setup.</returns>
    public InMemoryAdsClient RegisterSymbol(string name, object? value) =>
        RegisterSymbol(name, value, value?.GetType() ?? typeof(object), -1, isReadable: true, isWritable: true);

    /// <summary>Registers or replaces an in-memory ADS symbol with an explicit declared type.</summary>
    /// <param name="name">The symbol name.</param>
    /// <param name="value">The initial value.</param>
    /// <param name="dataType">The declared type.</param>
    /// <returns>This simulator for fluent setup.</returns>
    public InMemoryAdsClient RegisterSymbol(string name, object? value, Type dataType) =>
        RegisterSymbol(name, value, dataType, -1, isReadable: true, isWritable: true);

    /// <summary>Registers or replaces an in-memory ADS symbol with full access metadata.</summary>
    /// <param name="name">The symbol name.</param>
    /// <param name="value">The initial value.</param>
    /// <param name="dataType">The declared type.</param>
    /// <param name="arrayLength">The array or string length, or -1 for a scalar.</param>
    /// <param name="isReadable">Whether reads are permitted.</param>
    /// <param name="isWritable">Whether writes are permitted.</param>
    /// <returns>This simulator for fluent setup.</returns>
    public InMemoryAdsClient RegisterSymbol(
        string name,
        object? value,
        Type dataType,
        int arrayLength,
        bool isReadable,
        bool isWritable)
    {
        ThrowIfDisposed();
        var symbol = new InMemoryAdsSymbol(
            name,
            value,
            dataType,
            arrayLength,
            isReadable,
            isWritable);
        lock (_gate)
        {
            _symbols[symbol.Name] = symbol;
        }

        if (Connected)
        {
            SynchronizeConfiguredHandle(symbol.Name);
            _code.OnNext(CreateSymbolDescriptions());
        }

        return this;
    }

    /// <summary>Registers or replaces an in-memory structure symbol.</summary>
    /// <typeparam name="T">The structure type.</typeparam>
    /// <param name="name">The root symbol name.</param>
    /// <param name="value">The structure value.</param>
    /// <returns>This simulator for fluent setup.</returns>
    public InMemoryAdsClient RegisterStructure<T>(string name, T value)
        where T : class =>
        RegisterStructure(name, value, isReadable: true, isWritable: true);

    /// <summary>Registers or replaces an in-memory structure symbol with full access metadata.</summary>
    /// <typeparam name="T">The structure type.</typeparam>
    /// <param name="name">The root symbol name.</param>
    /// <param name="value">The structure value.</param>
    /// <param name="isReadable">Whether reads are permitted.</param>
    /// <param name="isWritable">Whether writes are permitted.</param>
    /// <returns>This simulator for fluent setup.</returns>
    public InMemoryAdsClient RegisterStructure<T>(
        string name,
        T value,
        bool isReadable,
        bool isWritable)
        where T : class
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return RegisterSymbol(name, value, typeof(T), -1, isReadable, isWritable);
    }

    /// <summary>Removes a registered symbol and any configured handles for it.</summary>
    /// <param name="name">The symbol name.</param>
    /// <returns>Whether a symbol was removed.</returns>
    public bool RemoveSymbol(string name)
    {
        ThrowIfDisposed();
        var checkedName = Required(name, nameof(name));
        lock (_gate)
        {
            _ = ReadWriteHandleInfo.Remove(checkedName);
            _ = WriteHandleInfo.Remove(checkedName);
            return _symbols.Remove(checkedName);
        }
    }

    /// <summary>Updates a symbol as if its value changed in the simulated PLC.</summary>
    /// <param name="variable">The variable to update.</param>
    /// <param name="value">The new value.</param>
    public void SetValue(string variable, object? value)
    {
        ThrowIfDisposed();
        var checkedVariable = Required(variable, nameof(variable));
        InMemoryAdsSymbol symbol;
        lock (_gate)
        {
            if (!_symbols.TryGetValue(checkedVariable, out symbol!))
            {
                throw new KeyNotFoundException($"ADS symbol '{checkedVariable}' is not registered.");
            }

            symbol.Value = ConvertValue(symbol, value);
        }

        if (!Connected || !IsConfiguredNotification(symbol.Name))
        {
            return;
        }

        PublishNotification(symbol.Name);
    }

    /// <summary>Tries to retrieve and convert a registered symbol value.</summary>
    /// <typeparam name="T">The requested value type.</typeparam>
    /// <param name="variable">The symbol name.</param>
    /// <param name="value">The converted value when successful.</param>
    /// <returns>Whether the symbol exists and its value is compatible.</returns>
    public bool TryGetValue<T>(string variable, [MaybeNullWhen(false)] out T value)
    {
        ThrowIfDisposed();
        var checkedVariable = Required(variable, nameof(variable));
        lock (_gate)
        {
            if (_symbols.TryGetValue(checkedVariable, out var symbol) && symbol.Value is T typed)
            {
                value = typed;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <inheritdoc/>
    public void Write(string variable, object value) => Write(variable, value, null);

    /// <inheritdoc/>
    public void Write(string variable, object value, string? id)
    {
        ThrowIfDisposed();
        _ = Interlocked.Increment(ref _writeOperations);
        var checkedVariable = Required(variable, nameof(variable));
        if (TryTakeFault(InMemoryAdsOperation.Write, out var queuedFault))
        {
            PublishWriteFailure(checkedVariable, id, queuedFault);
            return;
        }

        if (!Connected)
        {
            PublishWriteFailure(
                checkedVariable,
                id,
                new InMemoryAdsException(
                    InMemoryAdsOperation.Write,
                    "ADS writes require a connected in-memory client.",
                    checkedVariable));
            return;
        }

        if (!TryGetWritableSymbol(checkedVariable, id, out var symbol))
        {
            return;
        }

        try
        {
            lock (_gate)
            {
                symbol.Value = ConvertValue(symbol, value);
            }

            _writes.OnNext(SuccessResult(id));
            if (IsConfiguredNotification(symbol.Name))
            {
                PublishNotification(symbol.Name);
            }
        }
        catch (Exception error)
        {
            PublishWriteFailure(checkedVariable, id, error);
        }
    }

    /// <summary>Writes several symbols in enumeration order.</summary>
    /// <param name="values">The variable/value pairs to write.</param>
    public void WriteMany(IEnumerable<KeyValuePair<string, object>> values) => WriteMany(values, null);

    /// <summary>Writes several symbols in enumeration order.</summary>
    /// <param name="values">The variable/value pairs to write.</param>
    /// <param name="correlationPrefix">The correlation prefix.</param>
    public void WriteMany(IEnumerable<KeyValuePair<string, object>> values, string? correlationPrefix)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        var index = 0;
        foreach (var value in values)
        {
            Write(value.Key, value.Value, correlationPrefix is null ? null : $"{correlationPrefix}:{index}");
            index++;
        }
    }

    /// <summary>Resets deterministic native ADS operation counts without changing simulator state.</summary>
    public void ResetOperationMetrics()
    {
        ThrowIfDisposed();
        _ = Interlocked.Exchange(ref _readOperations, 0);
        _ = Interlocked.Exchange(ref _writeOperations, 0);
        _ = Interlocked.Exchange(ref _notificationPublications, 0);
    }

    /// <summary>Creates deterministic symbol descriptions for the existing code stream.</summary>
    /// <returns>The symbol descriptions.</returns>
    private string[] CreateSymbolDescriptions()
    {
        lock (_gate)
        {
            return _symbols.Values
                .OrderBy(static symbol => symbol.Name, StringComparer.OrdinalIgnoreCase)
                .Select(static symbol =>
                    $"{symbol.Name}:{symbol.DataType.FullName ?? symbol.DataType.Name}:{symbol.ArrayLength}")
                .ToArray();
        }
    }

    /// <summary>Converts one assigned value to the symbol's declared type.</summary>
    /// <param name="symbol">The destination symbol.</param>
    /// <param name="value">The assigned value.</param>
    /// <returns>The compatible value.</returns>
    private object? ConvertValue(InMemoryAdsSymbol symbol, object? value)
    {
        if (value is null)
        {
            if (!symbol.DataType.IsValueType || Nullable.GetUnderlyingType(symbol.DataType) is not null)
            {
                return null;
            }

            throw new InvalidCastException($"ADS symbol '{symbol.Name}' does not accept null values.");
        }

        if (symbol.DataType.IsInstanceOfType(value))
        {
            return CloneValue(value);
        }

        var destination = Nullable.GetUnderlyingType(symbol.DataType) ?? symbol.DataType;
        if (destination.IsEnum)
        {
            return value is string text
                ? Enum.Parse(destination, text, ignoreCase: true)
                : Enum.ToObject(destination, value);
        }

        try
        {
            return Convert.ChangeType(value, destination, CultureInfo.InvariantCulture);
        }
        catch (Exception error) when (error is InvalidCastException or FormatException or OverflowException)
        {
            throw new InvalidCastException(
                $"Value for ADS symbol '{symbol.Name}' cannot be converted to {symbol.DataType.FullName}.",
                error);
        }
    }

    /// <summary>Clones array values to keep simulator storage isolated from callers.</summary>
    /// <param name="value">The value to clone when needed.</param>
    /// <returns>The stored value.</returns>
    private object CloneValue(object value) => value is Array array ? array.Clone() : value;

    /// <summary>Formats a correlated error result.</summary>
    /// <param name="error">The failure.</param>
    /// <param name="id">The optional correlation identifier.</param>
    /// <returns>The write result.</returns>
    private string ErrorResult(Exception error, string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? $"Error:{error.Message}"
            : $"Error:{error.Message},{id}";

    /// <summary>Formats a correlated success result.</summary>
    /// <param name="id">The optional correlation identifier.</param>
    /// <returns>The write result.</returns>
    private string SuccessResult(string? id) =>
        string.IsNullOrWhiteSpace(id) ? "Success" : $"Success,{id}";

    /// <summary>Validates and normalizes required text.</summary>
    /// <param name="value">The text.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <returns>The normalized text.</returns>
    private string Required(string value, string parameterName)
    {
#if NET
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
#else
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The value cannot be null or whitespace.", parameterName);
        }
#endif

        return value.Trim();
    }

    /// <summary>Creates configured handles after validating all configured symbol names.</summary>
    /// <param name="error">The first configuration error.</param>
    /// <returns>Whether all handles were created.</returns>
    private bool TryBuildConfiguredHandles([NotNullWhen(false)] out Exception? error)
    {
        lock (_gate)
        {
            ReadWriteHandleInfo.Clear();
            WriteHandleInfo.Clear();
            _nextHandle = 1;
            foreach (var notification in Settings?.Notifications ?? [])
            {
                if (!TryGetConfiguredSymbol(notification.Variable, InMemoryAdsOperation.Notification, out var symbol, out error))
                {
                    return false;
                }

                ReadWriteHandleInfo[symbol.Name] = GetNextHandle();
            }

            foreach (var writeVariable in Settings?.WriteVariables ?? [])
            {
                if (!TryGetConfiguredSymbol(writeVariable.Variable, InMemoryAdsOperation.Write, out var symbol, out error))
                {
                    return false;
                }

                var handle = ReadWriteHandleInfo.TryGetValue(symbol.Name, out var existing)
                    ? existing
                    : GetNextHandle();
                WriteHandleInfo[symbol.Name] = (handle, writeVariable.ArraySize);
            }
        }

        error = null;
        return true;
    }

    /// <summary>Finds one configured symbol and creates a descriptive error when absent.</summary>
    /// <param name="variable">The configured variable.</param>
    /// <param name="operation">The configured operation.</param>
    /// <param name="symbol">The resolved symbol.</param>
    /// <param name="error">The configuration error.</param>
    /// <returns>Whether the symbol was found.</returns>
    private bool TryGetConfiguredSymbol(
        string? variable,
        InMemoryAdsOperation operation,
        [NotNullWhen(true)] out InMemoryAdsSymbol? symbol,
        [NotNullWhen(false)] out Exception? error)
    {
        symbol = null;
        if (string.IsNullOrWhiteSpace(variable) || !_symbols.TryGetValue(variable!.Trim(), out symbol))
        {
            error = new InMemoryAdsException(
                operation,
                $"Configured ADS symbol '{variable ?? string.Empty}' is not registered.",
                variable);
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>Adds handles for a symbol registered after connection.</summary>
    /// <param name="variable">The registered variable.</param>
    private void SynchronizeConfiguredHandle(string variable)
    {
        lock (_gate)
        {
            if ((Settings?.Notifications ?? []).Any(
                item => string.Equals(item.Variable, variable, StringComparison.OrdinalIgnoreCase)) &&
                !ReadWriteHandleInfo.ContainsKey(variable))
            {
                ReadWriteHandleInfo[variable] = GetNextHandle();
            }

            var writeVariable = (Settings?.WriteVariables ?? []).FirstOrDefault(
                item => string.Equals(item.Variable, variable, StringComparison.OrdinalIgnoreCase));
            if (writeVariable is not null && !WriteHandleInfo.ContainsKey(variable))
            {
                var handle = ReadWriteHandleInfo.TryGetValue(variable, out var existing)
                    ? existing
                    : GetNextHandle();
                WriteHandleInfo[variable] = (handle, writeVariable.ArraySize);
            }
        }
    }

    /// <summary>Gets the next nonzero deterministic handle.</summary>
    /// <returns>The handle.</returns>
    private uint GetNextHandle() => _nextHandle++;

    /// <summary>Publishes and records a connection failure.</summary>
    /// <param name="error">The connection failure.</param>
    private void FailConnection(Exception error)
    {
        SetConnectionState(InMemoryAdsConnectionState.Faulted);
        ReportError(error);
    }

    /// <summary>Gets whether a variable is a configured notification.</summary>
    /// <param name="variable">The variable.</param>
    /// <returns>Whether notifications are configured.</returns>
    private bool IsConfiguredNotification(string variable) =>
        (Settings?.Notifications ?? []).Any(
            notification => string.Equals(notification.Variable, variable, StringComparison.OrdinalIgnoreCase));

    /// <summary>Publishes a configured notification.</summary>
    /// <param name="variable">The configured variable.</param>
    private void PublishNotification(string? variable)
    {
        _ = Interlocked.Increment(ref _notificationPublications);
        if (TryTakeFault(InMemoryAdsOperation.Notification, out var queuedFault))
        {
            ReportError(queuedFault);
            return;
        }

        if (string.IsNullOrWhiteSpace(variable))
        {
            ReportError(
                new InMemoryAdsException(
                    InMemoryAdsOperation.Notification,
                    "A configured ADS notification has no variable name."));
            return;
        }

        InMemoryAdsSymbol? symbol;
        lock (_gate)
        {
            _ = _symbols.TryGetValue(variable!.Trim(), out symbol);
        }

        if (symbol is null)
        {
            ReportError(
                new InMemoryAdsException(
                    InMemoryAdsOperation.Notification,
                    $"Configured ADS symbol '{variable}' is not registered.",
                    variable));
            return;
        }

        _dataReceived.OnNext((symbol.Name, CloneValueOrNull(symbol.Value), null));
    }

    /// <summary>Clones nullable array values before publishing them.</summary>
    /// <param name="value">The symbol value.</param>
    /// <returns>The published value.</returns>
    private object? CloneValueOrNull(object? value) => value is null ? null : CloneValue(value);

    /// <summary>Publishes a read failure without terminating the shared observable stream.</summary>
    /// <param name="variable">The requested variable.</param>
    /// <param name="id">The correlation identifier.</param>
    /// <param name="error">The failure.</param>
    private void PublishReadFailure(string variable, string? id, Exception error)
    {
        ReportError(error);
        _dataReceived.OnNext((variable, null, id));
    }

    /// <summary>Publishes a write failure without terminating the shared observable stream.</summary>
    /// <param name="variable">The requested variable.</param>
    /// <param name="id">The correlation identifier.</param>
    /// <param name="error">The failure.</param>
    private void PublishWriteFailure(string variable, string? id, Exception error)
    {
        var reported = error is InMemoryAdsException
            ? error
            : new InMemoryAdsException(InMemoryAdsOperation.Write, error.Message, variable);
        ReportError(reported);
        _writes.OnNext(ErrorResult(reported, id));
    }

    /// <summary>Publishes one simulator error.</summary>
    /// <param name="error">The error.</param>
    private void ReportError(Exception error)
    {
        if (IsDisposed)
        {
            return;
        }

        _errors.OnNext(error);
    }

    /// <summary>Changes and publishes the current connection state.</summary>
    /// <param name="state">The new state.</param>
    private void SetConnectionState(InMemoryAdsConnectionState state)
    {
        ConnectionState = state;
        _connectionStates.OnNext(state);
    }

    /// <summary>Ends the current pause.</summary>
    private void StopPause()
    {
        bool changed;
        lock (_gate)
        {
            changed = IsPaused;
            IsPaused = false;
            _pauseTimer?.Dispose();
            _pauseTimer = null;
        }

        if (!changed || IsDisposed)
        {
            return;
        }

        _paused.OnNext(false);
    }

    /// <summary>Consumes the next queued failure for an operation.</summary>
    /// <param name="operation">The operation.</param>
    /// <param name="error">The queued failure.</param>
    /// <returns>Whether a failure was queued.</returns>
    private bool TryTakeFault(InMemoryAdsOperation operation, [NotNullWhen(true)] out Exception? error)
    {
        lock (_gate)
        {
            if (_faults.TryGetValue(operation, out var faults) && faults.Count > 0)
            {
                error = faults.Dequeue();
                return true;
            }
        }

        error = null;
        return false;
    }

    /// <summary>Slices array and string reads when an explicit ADS length is supplied.</summary>
    /// <param name="value">The symbol value.</param>
    /// <param name="requestedLength">The requested length.</param>
    /// <returns>The published value.</returns>
    private object? SliceValue(object? value, int? requestedLength)
    {
        if (requestedLength is null)
        {
            return CloneValueOrNull(value);
        }

        if (requestedLength <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedLength),
                requestedLength,
                "An explicit ADS array or string length must be positive.");
        }

        if (value is string text)
        {
            return text.Substring(0, Math.Min(text.Length, requestedLength.Value));
        }

        if (value is not Array array)
        {
            return CloneValueOrNull(value);
        }

        var length = Math.Min(array.Length, requestedLength.Value);
        return SliceArray(array, length);
    }

    /// <summary>Throws when the simulator has been disposed.</summary>
    private void ThrowIfDisposed()
    {
        if (!IsDisposed)
        {
            return;
        }

        throw new ObjectDisposedException(nameof(InMemoryAdsClient));
    }

    /// <summary>Gets a writable symbol or publishes a correlated failure.</summary>
    /// <param name="variable">The variable.</param>
    /// <param name="id">The correlation identifier.</param>
    /// <param name="symbol">The writable symbol.</param>
    /// <returns>Whether the symbol is writable.</returns>
    private bool TryGetWritableSymbol(
        string variable,
        string? id,
        [NotNullWhen(true)] out InMemoryAdsSymbol? symbol)
    {
        lock (_gate)
        {
            _ = _symbols.TryGetValue(variable, out symbol);
        }

        if (symbol is null)
        {
            PublishWriteFailure(
                variable,
                id,
                new InMemoryAdsException(
                    InMemoryAdsOperation.Write,
                    $"ADS symbol '{variable}' is not registered.",
                    variable));
            return false;
        }

        if (symbol.IsWritable)
        {
            return true;
        }

        PublishWriteFailure(
            variable,
            id,
            new InMemoryAdsException(
                InMemoryAdsOperation.Write,
                $"ADS symbol '{variable}' is read-only.",
                variable));
        return false;
    }

    /// <summary>Slices known ADS primitive arrays without runtime code generation.</summary>
    /// <param name="array">The source array.</param>
    /// <param name="length">The requested length.</param>
    /// <returns>The sliced values.</returns>
    private object?[] SliceArray(Array array, int length) => array.Cast<object?>().Take(length).ToArray();
}
