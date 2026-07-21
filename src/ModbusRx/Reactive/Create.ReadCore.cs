// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ModbusRx.Reactive;
#else
namespace ModbusRx;
#endif

/// <summary>Provides ModbusRx functionality.</summary>
public static partial class Create
{
    /// <summary>Composes a task-based Modbus read into a polling observable.</summary>
    /// <typeparam name="TValue">The returned point type.</typeparam>
    /// <typeparam name="TMaster">The Modbus master type.</typeparam>
    /// <param name="source">The source master stream.</param>
    /// <param name="readAsync">The read operation.</param>
    /// <param name="errorMessage">The communication error message.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <param name="diagnosticName">The optional diagnostic operation name.</param>
    /// <returns>An observable sequence producing data or an error.</returns>
    private static IObservable<(TValue[]? Data, Exception? Error)> ReadMasterCore<TValue, TMaster>(
        IObservable<(bool Connected, Exception? Error, TMaster? Master)> source,
        Func<TMaster, Task<TValue[]>> readAsync,
        string errorMessage,
        double interval,
        string? diagnosticName = null)
        where TMaster : class, IDisposable =>
        Observable.Create<(TValue[]? Data, Exception? Error)>(observer =>
        {
            var isConnected = false;
            var timer = Observable.Interval(TimeSpan.FromMilliseconds(interval))
                .Where(_ => isConnected)
                .StartWith(long.MinValue);
            var subscription = source
                .CombineLatest(timer, (modbus, _) => modbus)
                .Retry(int.MaxValue)
                .SelectMany(modbus => Observable.FromAsync(async () =>
                {
                    var master = modbus.Master;
                    WritePollingDiagnostic(diagnosticName, modbus.Connected, modbus.Error);

                    try
                    {
                        isConnected = modbus.Connected;
                        if (modbus.Connected && modbus.Error is null && master is not null)
                        {
                            var result = await readAsync(master);
                            if (result is not null)
                            {
                                observer.OnNext((result, null));
                            }
                        }
                        else
                        {
                            observer.OnNext((null, modbus.Error));
                        }
                    }
                    catch (Exception ex)
                    {
                        master?.Dispose();
                        isConnected = false;
                        WriteErrorDiagnostic(diagnosticName, ex);
                        observer.OnError(new ModbusCommunicationException(errorMessage, ex));
                    }

                    return RxVoid.Default;
                }))
                .Subscribe(_ => { }, observer.OnError);

            return Disposable.Create(subscription.Dispose);
        }).Retry(int.MaxValue);

    /// <summary>Writes a polling diagnostic when a diagnostic name is configured.</summary>
    /// <param name="diagnosticName">The optional diagnostic operation name.</param>
    /// <param name="connected">The current connection state.</param>
    /// <param name="error">The current source error.</param>
    private static void WritePollingDiagnostic(string? diagnosticName, bool connected, Exception? error)
    {
        if (diagnosticName is null)
        {
            return;
        }

        ModbusDiagnostics.Write(
            $"{diagnosticName} polling: connected={connected}, error={error?.Message}");
    }

    /// <summary>Writes an error diagnostic when a diagnostic name is configured.</summary>
    /// <param name="diagnosticName">The optional diagnostic operation name.</param>
    /// <param name="exception">The read exception.</param>
    private static void WriteErrorDiagnostic(string? diagnosticName, Exception exception)
    {
        if (diagnosticName is null)
        {
            return;
        }

        ModbusDiagnostics.Write($"{diagnosticName} error: {exception.Message}");
    }
}
