// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using TwinCAT.Ads;

#if REACTIVE_SHIM
namespace IoT.DriverCore.TwinCATRx.Core.Reactive;
#else
namespace IoT.DriverCore.TwinCATRx.Core;
#endif

/// <summary>Observable TwinCAT extensions.</summary>
public static class TwinCatRxExtensions
{
    /// <summary>Stores the default notification cycle time in milliseconds.</summary>
    private const int DefaultNotificationCycleTimeMilliseconds = 100;

    /// <summary>Observes ADS state changed events.</summary>
    /// <param name="client">The ADS client.</param>
    /// <returns>The ADS state changed observable sequence.</returns>
    public static IObservable<AdsStateChangedEventArgs> AdsStateChangedObserver(AdsClient client) =>
        Observable.FromEventPattern<EventHandler<AdsStateChangedEventArgs>, AdsStateChangedEventArgs>(
            handler => client.AdsStateChanged += handler,
            handler => client.AdsStateChanged -= handler).Select(pattern => pattern.EventArgs);

    /// <summary>Polls ADS state from the client.</summary>
    /// <param name="client">The ADS client.</param>
    /// <returns>The ADS state observable sequence.</returns>
    public static IObservable<StateInfo> AdsStateObserver(AdsClient client) =>
        Observable.Create<StateInfo>(observer =>
        {
            var timer = new Timer(
                _ =>
                {
                    try
                    {
                        observer.OnNext(
                            client.IsConnected ? client.ReadState() : new StateInfo { AdsState = AdsState.Invalid });
                    }
                    catch (Exception ex)
                    {
                        observer.OnError(ex);
                    }
                },
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(1));

            return ReactiveUI.Primitives.Disposables.Scope.Create(timer.Dispose);
        });

    /// <typeparam name="TSource">The observable value type.</typeparam>
    /// <param name="source">The observable sequence.</param>
    /// <summary>Repeats the observable sequence until it completes successfully.</summary>
    /// <returns>The retrying observable sequence.</returns>
    public static IObservable<TSource?> OnErrorRetry<TSource>(IObservable<TSource?> source)
    {
        var checkedSource = Require(source, nameof(source));
        return checkedSource.Retry(int.MaxValue);
    }

    /// <typeparam name="TSource">The observable value type.</typeparam>
    /// <typeparam name="TException">The handled exception type.</typeparam>
    /// <param name="source">The observable sequence.</param>
    /// <summary>Runs the error handler and repeats the observable sequence.</summary>
    /// <param name="onError">The error handler.</param>
    /// <returns>The retrying observable sequence.</returns>
    public static IObservable<TSource?> OnErrorRetry<TSource, TException>(
        IObservable<TSource?> source,
        Action<TException> onError)
        where TException : Exception
    {
        var checkedSource = Require(source, nameof(source));
        var checkedOnError = Require(onError, nameof(onError));
        return OnErrorRetry(checkedSource, checkedOnError, TimeSpan.Zero);
    }

    /// <summary>Runs the error handler and repeats the observable sequence after a delay.</summary>
    /// <typeparam name="TSource">The observable value type.</typeparam>
    /// <typeparam name="TException">The handled exception type.</typeparam>
    /// <param name="source">The observable sequence.</param>
    /// <param name="onError">The error handler.</param>
    /// <param name="delay">The retry delay.</param>
    /// <returns>The retrying observable sequence.</returns>
    public static IObservable<TSource?> OnErrorRetry<TSource, TException>(
        IObservable<TSource?> source,
        Action<TException> onError,
        TimeSpan delay)
        where TException : Exception
    {
        var checkedSource = Require(source, nameof(source));
        var checkedOnError = Require(onError, nameof(onError));
        return OnErrorRetry(checkedSource, checkedOnError, int.MaxValue, delay);
    }

    /// <summary>Runs the error handler and repeats the observable sequence for the retry count.</summary>
    /// <typeparam name="TSource">The observable value type.</typeparam>
    /// <typeparam name="TException">The handled exception type.</typeparam>
    /// <param name="source">The observable sequence.</param>
    /// <param name="onError">The error handler.</param>
    /// <param name="retryCount">The retry count.</param>
    /// <returns>The retrying observable sequence.</returns>
    public static IObservable<TSource?> OnErrorRetry<TSource, TException>(
        IObservable<TSource?> source,
        Action<TException> onError,
        int retryCount)
        where TException : Exception
    {
        var checkedSource = Require(source, nameof(source));
        var checkedOnError = Require(onError, nameof(onError));
        return OnErrorRetry(checkedSource, checkedOnError, retryCount, TimeSpan.Zero);
    }

    /// <summary>Runs the error handler and repeats the observable sequence after a delay for the retry count.</summary>
    /// <typeparam name="TSource">The observable value type.</typeparam>
    /// <typeparam name="TException">The handled exception type.</typeparam>
    /// <param name="source">The observable sequence.</param>
    /// <param name="onError">The error handler.</param>
    /// <param name="retryCount">The retry count.</param>
    /// <param name="delay">The retry delay.</param>
    /// <returns>The retrying observable sequence.</returns>
    public static IObservable<TSource?> OnErrorRetry<TSource, TException>(
        IObservable<TSource?> source,
        Action<TException> onError,
        int retryCount,
        TimeSpan delay)
        where TException : Exception
    {
        var checkedSource = Require(source, nameof(source));
        var checkedOnError = Require(onError, nameof(onError));
        return OnErrorRetry(checkedSource, checkedOnError, retryCount, delay, TaskPoolSequencer.Default);
    }

    /// <summary>Retries an observable sequence using the supplied delay sequencer.</summary>
    /// <typeparam name="TSource">The observable value type.</typeparam>
    /// <typeparam name="TException">The handled exception type.</typeparam>
    /// <param name="source">The observable sequence.</param>
    /// <param name="onError">The error handler.</param>
    /// <param name="retryCount">The retry count.</param>
    /// <param name="delay">The retry delay.</param>
    /// <param name="delaySequencer">The delay sequencer.</param>
    /// <returns>The retrying observable sequence.</returns>
    public static IObservable<TSource?> OnErrorRetry<TSource, TException>(
        IObservable<TSource?> source,
        Action<TException> onError,
        int retryCount,
        TimeSpan delay,
        ISequencer delaySequencer)
        where TException : Exception
    {
        var checkedSource = Require(source, nameof(source));
        var checkedOnError = Require(onError, nameof(onError));
        var checkedDelaySequencer = Require(delaySequencer, nameof(delaySequencer));

        return Observable.Defer(() =>
        {
            var dueTime = delay.Ticks < 0 ? TimeSpan.Zero : delay;
            var empty = Observable.Empty<TSource?>();
            var count = 0;
            IObservable<TSource?>? self = null;
            self = checkedSource.Catch((TException ex) =>
            {
                checkedOnError(ex);

                count++;
                if (count >= retryCount)
                {
                    return Observable.Throw<TSource?>(ex);
                }

                return dueTime == TimeSpan.Zero
                    ? self!
                    : empty.Delay(dueTime, checkedDelaySequencer).Concat(self!);
            });
            return self;
        });
    }

    /// <summary>Adds a notification variable to the settings.</summary>
    /// <param name="settings">The TwinCAT settings.</param>
    /// <param name="variableName">The PLC variable name.</param>
    public static void AddNotification(ISettings? settings, string variableName) =>
        AddNotification(settings, variableName, DefaultNotificationCycleTimeMilliseconds, -1);

    /// <summary>Adds a notification variable to the settings.</summary>
    /// <param name="settings">The TwinCAT settings.</param>
    /// <param name="variableName">The PLC variable name.</param>
    /// <param name="cycleTime">The polling cycle time.</param>
    public static void AddNotification(ISettings? settings, string variableName, int cycleTime) =>
        AddNotification(settings, variableName, cycleTime, -1);

    /// <summary>Adds a notification variable to the settings.</summary>
    /// <param name="settings">The TwinCAT settings.</param>
    /// <param name="variableName">The PLC variable name.</param>
    /// <param name="cycleTime">The polling cycle time.</param>
    /// <param name="arraySize">The array size.</param>
    public static void AddNotification(ISettings? settings, string variableName, int cycleTime, int arraySize)
    {
        if (settings is null)
        {
            return;
        }

        settings.Notifications.Add(new Notification(cycleTime, variableName, arraySize));
    }

    /// <summary>Adds a write variable to the settings.</summary>
    /// <param name="settings">The TwinCAT settings.</param>
    /// <param name="variableName">The PLC variable name.</param>
    public static void AddWriteVariable(ISettings? settings, string variableName) =>
        AddWriteVariable(settings, variableName, -1);

    /// <summary>Adds a write variable to the settings.</summary>
    /// <param name="settings">The TwinCAT settings.</param>
    /// <param name="variableName">The PLC variable name.</param>
    /// <param name="arraySize">The array size.</param>
    public static void AddWriteVariable(ISettings? settings, string variableName, int arraySize)
    {
        if (settings is null)
        {
            return;
        }

        settings.WriteVariables.Add(new WriteVariable(variableName, arraySize));
    }

    /// <summary>Loads an assembly from a DLL file path.</summary>
    /// <param name="dllFullName">The full DLL path.</param>
    /// <returns>The loaded assembly.</returns>
    [RequiresDynamicCode("Loads an assembly at runtime via Assembly.Load which requires dynamic code.")]
    [RequiresUnreferencedCode("Uses reflection-based assembly loading which may be trimmed.")]
    public static Assembly? AssemblyLoad(string dllFullName)
    {
        Assembly? assembly = null;
        if (File.Exists(dllFullName))
        {
            using var fs = File.Open(dllFullName, FileMode.Open, FileAccess.Read);
            using var ms = new MemoryStream();
            var buffer = new byte[1024];
            int read;
            while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
            {
                ms.Write(buffer, 0, read);
            }

            assembly = Assembly.Load(ms.ToArray());
        }

        return assembly;
    }

    /// <summary>Gets a type from an assembly file.</summary>
    /// <param name="dllFullName">The full DLL path.</param>
    /// <param name="engineType">The type name.</param>
    /// <returns>The resolved type.</returns>
    [RequiresDynamicCode("Accesses type by name using reflection which may require dynamic code.")]
    [RequiresUnreferencedCode("Uses reflection to access type by name which may be trimmed in AOT.")]
    public static Type? GetType(string dllFullName, string engineType) =>
        AssemblyLoad(dllFullName)?.GetType(engineType);

    /// <summary>Returns a value or throws when it is null.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <returns>The non-null value.</returns>
    private static T Require<T>(T? value, string parameterName)
        where T : class =>
        value ?? throw new ArgumentNullException(parameterName);
}
