// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace CP.IO.Ports.Reactive;
#else
namespace CP.IO.Ports;
#endif

/// <summary>Provides serial port reactive extension methods.</summary>
public static class SerialPortRxMixins
{
    /// <summary>Buffers characters between the start and end markers.</summary>
    /// <param name="source">The source observable.</param>
        /// <param name="startsWith">The starts with.</param>
        /// <param name="endsWith">The ends with.</param>
        /// <param name="timeOut">The time out.</param>
        /// <returns>
        /// A string made up from the char values between the start and end chars.
        /// </returns>
    public static IObservable<string> BufferUntil(
            IObservable<char> source,
            IObservable<char> startsWith,
            IObservable<char> endsWith,
            int timeOut) => BufferUntil(source, startsWith, endsWith, timeOut, null);

    /// <summary>Buffers characters between the start and end markers.</summary>
    /// <param name="source">The source observable.</param>
        /// <param name="startsWith">The starts with.</param>
        /// <param name="endsWith">The ends with.</param>
        /// <param name="timeOut">The time out.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>A string made up from the char values between the start and end chars.</returns>
    public static IObservable<string> BufferUntil(
            IObservable<char> source,
            IObservable<char> startsWith,
            IObservable<char> endsWith,
            int timeOut,
            IScheduler? scheduler) => Observable.Create<string>(o =>
        {
            var dis = new CompositeDisposable();
            var str = string.Empty;

            var startFound = false;
            var elapsedTime = 0;
            var startsWithL = ' ';
            dis.Add(startsWith.Subscribe(sw =>
            {
                startsWithL = sw;
                elapsedTime = 0;
            }));
            var endsWithL = ' ';
            dis.Add(endsWith.Subscribe(ew => endsWithL = ew));
            dis.Add(source.Subscribe(s =>
            {
                elapsedTime = 0;
                if (!startFound && s != startsWithL)
                {
                    return;
                }

                startFound = true;
                str += s;
                if (s != endsWithL)
                {
                    return;
                }

                o.OnNext(str);
                startFound = false;
                str = string.Empty;
            }));

            scheduler ??= DefaultScheduler.Instance;

            dis.Add(Observable.Interval(TimeSpan.FromMilliseconds(1), scheduler).Subscribe(_ =>
            {
                elapsedTime++;
                if (elapsedTime <= timeOut)
                {
                    return;
                }

                startFound = false;
                str = string.Empty;
                elapsedTime = 0;
            }));

            return dis;
        });

    /// <summary>
    /// Buffers the Char values until the start and end chars have been found within the timeout
    /// period otherwise returns the default value.
    /// </summary>
    /// <param name="source">The source observable.</param>
        /// <param name="startsWith">The starts with.</param>
        /// <param name="endsWith">The ends with.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <param name="timeOut">The time out.</param>
        /// <returns>
        /// A string made up from the char values between the start and end chars.
        /// </returns>
    public static IObservable<string> BufferUntil(
            IObservable<char> source,
            IObservable<char> startsWith,
            IObservable<char> endsWith,
            IObservable<string> defaultValue,
            int timeOut) => BufferUntil(source, startsWith, endsWith, defaultValue, timeOut, null);

    /// <summary>Buffers the Char values until the start and end chars have been found within the timeout
    /// period otherwise returns the default value.</summary>
    /// <param name="source">The source observable.</param>
        /// <param name="startsWith">The starts with.</param>
        /// <param name="endsWith">The ends with.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <param name="timeOut">The time out.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>A string made up from the char values between the start and end chars.</returns>
    public static IObservable<string> BufferUntil(
            IObservable<char> source,
            IObservable<char> startsWith,
            IObservable<char> endsWith,
            IObservable<string> defaultValue,
            int timeOut,
            IScheduler? scheduler) =>
            Observable.Create<string>(o =>
            {
                var dis = new CompositeDisposable();
                var str = string.Empty;

                var startFound = false;
                var elapsedTime = 0;
                var startsWithL = ' ';
                dis.Add(startsWith.Subscribe(sw =>
                {
                    startsWithL = sw;
                    elapsedTime = 0;
                }));
                var endsWithL = ' ';
                dis.Add(endsWith.Subscribe(ew => endsWithL = ew));
                var defaultValueL = string.Empty;
                dis.Add(defaultValue.Subscribe(dv => defaultValueL = dv));
                dis.Add(source.Subscribe(s =>
                {
                    elapsedTime = 0;
                    if (!startFound && s != startsWithL)
                    {
                        return;
                    }

                    startFound = true;
                    str += s;
                    if (s != endsWithL)
                    {
                        return;
                    }

                    o.OnNext(str);
                    startFound = false;
                    str = string.Empty;
                }));

                scheduler ??= DefaultScheduler.Instance;

                dis.Add(Observable.Interval(TimeSpan.FromMilliseconds(1), scheduler).Subscribe(_ =>
                {
                    elapsedTime++;
                    if (elapsedTime <= timeOut)
                    {
                        return;
                    }

                    o.OnNext(defaultValueL);
                    startFound = false;
                    str = string.Empty;
                    elapsedTime = 0;
                }));

                return dis;
            });

    /// <summary>Buffers async characters between the start and end markers.</summary>
    /// <param name="source">The source async observable.</param>
        /// <param name="startsWith">The starts with.</param>
        /// <param name="endsWith">The ends with.</param>
        /// <param name="timeOut">The time out.</param>
        /// <returns>
        /// An async observable string made up from the char values between the start and end chars.
        /// </returns>
    public static IObservableAsync<string> BufferUntil(
            IObservableAsync<char> source,
            IObservableAsync<char> startsWith,
            IObservableAsync<char> endsWith,
            int timeOut) =>
            ObservableAsyncBridgeExtensions.ToAsyncObservable(
                BufferUntil(
                    ObservableAsyncBridgeExtensions.ToObservable(source),
                    ObservableAsyncBridgeExtensions.ToObservable(startsWith),
                    ObservableAsyncBridgeExtensions.ToObservable(endsWith),
                    timeOut,
                    null));

    /// <summary>Buffers async characters between the start and end markers.</summary>
    /// <param name="source">The source async observable.</param>
        /// <param name="startsWith">The starts with.</param>
        /// <param name="endsWith">The ends with.</param>
        /// <param name="timeOut">The time out.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>An async observable string made up from the char values between the start and end chars.</returns>
    public static IObservableAsync<string> BufferUntil(
            IObservableAsync<char> source,
            IObservableAsync<char> startsWith,
            IObservableAsync<char> endsWith,
            int timeOut,
            IScheduler? scheduler) =>
            ObservableAsyncBridgeExtensions.ToAsyncObservable(
                BufferUntil(
                    ObservableAsyncBridgeExtensions.ToObservable(source),
                    ObservableAsyncBridgeExtensions.ToObservable(startsWith),
                    ObservableAsyncBridgeExtensions.ToObservable(endsWith),
                    timeOut,
                    scheduler));

    /// <summary>
    /// Buffers async Char values until the start and end chars have been found within the timeout
    /// period otherwise returns the default value.
    /// </summary>
    /// <param name="source">The source async observable.</param>
        /// <param name="startsWith">The starts with.</param>
        /// <param name="endsWith">The ends with.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <param name="timeOut">The time out.</param>
        /// <returns>
        /// An async observable string made up from the char values between the start and end chars.
        /// </returns>
    public static IObservableAsync<string> BufferUntil(
            IObservableAsync<char> source,
            IObservableAsync<char> startsWith,
            IObservableAsync<char> endsWith,
            IObservableAsync<string> defaultValue,
            int timeOut) =>
            ObservableAsyncBridgeExtensions.ToAsyncObservable(
                BufferUntil(
                    ObservableAsyncBridgeExtensions.ToObservable(source),
                    ObservableAsyncBridgeExtensions.ToObservable(startsWith),
                    ObservableAsyncBridgeExtensions.ToObservable(endsWith),
                    ObservableAsyncBridgeExtensions.ToObservable(defaultValue),
                    timeOut,
                    null));

    /// <summary>Buffers async Char values until the start and end chars have been found within the timeout
    /// period otherwise returns the default value.</summary>
    /// <param name="source">The source async observable.</param>
        /// <param name="startsWith">The starts with.</param>
        /// <param name="endsWith">The ends with.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <param name="timeOut">The time out.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>An async observable string made up from the char values between the start and end chars.</returns>
    public static IObservableAsync<string> BufferUntil(
            IObservableAsync<char> source,
            IObservableAsync<char> startsWith,
            IObservableAsync<char> endsWith,
            IObservableAsync<string> defaultValue,
            int timeOut,
            IScheduler? scheduler) =>
            ObservableAsyncBridgeExtensions.ToAsyncObservable(
                BufferUntil(
                    ObservableAsyncBridgeExtensions.ToObservable(source),
                    ObservableAsyncBridgeExtensions.ToObservable(startsWith),
                    ObservableAsyncBridgeExtensions.ToObservable(endsWith),
                    ObservableAsyncBridgeExtensions.ToObservable(defaultValue),
                    timeOut,
                    scheduler));

    /// <summary>Gets the data received after opening a receive port as an async observable.</summary>
    /// <param name="port">The source port.</param>
    /// <returns>An async observable of received byte values.</returns>
    public static IObservableAsync<int> BytesReceivedAsyncObservable(IPortRx? port)
    {
        ArgumentGuard.ThrowIfNull(port, nameof(port));

        return ObservableAsyncBridgeExtensions.ToAsyncObservable(port.BytesReceived);
    }

    /// <summary>Gets serial characters as an async observable.</summary>
    /// <param name="serialPort">The source serial port.</param>
    /// <returns>An async observable of received characters.</returns>
    public static IObservableAsync<char> DataReceivedAsyncObservable(ISerialPortRx? serialPort)
    {
        ArgumentGuard.ThrowIfNull(serialPort, nameof(serialPort));

        return ObservableAsyncBridgeExtensions.ToAsyncObservable(serialPort.DataReceived);
    }

    /// <summary>Gets serial bytes as an async observable.</summary>
    /// <param name="serialPort">The source serial port.</param>
    /// <returns>An async observable of received bytes.</returns>
    public static IObservableAsync<byte> DataReceivedBytesAsyncObservable(ISerialPortRx? serialPort)
    {
        ArgumentGuard.ThrowIfNull(serialPort, nameof(serialPort));

        return ObservableAsyncBridgeExtensions.ToAsyncObservable(serialPort.DataReceivedBytes);
    }

    /// <summary>Gets serial lines as an async observable.</summary>
    /// <param name="serialPort">The source serial port.</param>
    /// <returns>An async observable of received lines.</returns>
    public static IObservableAsync<string> LinesAsyncObservable(ISerialPortRx? serialPort)
    {
        ArgumentGuard.ThrowIfNull(serialPort, nameof(serialPort));

        return ObservableAsyncBridgeExtensions.ToAsyncObservable(serialPort.Lines);
    }

    /// <summary>Gets serial errors as an async observable.</summary>
    /// <param name="serialPort">The source serial port.</param>
    /// <returns>An async observable of errors.</returns>
    public static IObservableAsync<Exception> ErrorReceivedAsyncObservable(ISerialPortRx? serialPort)
    {
        ArgumentGuard.ThrowIfNull(serialPort, nameof(serialPort));

        return ObservableAsyncBridgeExtensions.ToAsyncObservable(serialPort.ErrorReceived);
    }

    /// <summary>Gets serial open-state changes as an async observable.</summary>
    /// <param name="serialPort">The source serial port.</param>
    /// <returns>An async observable of open-state changes.</returns>
    public static IObservableAsync<bool> IsOpenAsyncObservable(ISerialPortRx? serialPort)
    {
        ArgumentGuard.ThrowIfNull(serialPort, nameof(serialPort));

        return ObservableAsyncBridgeExtensions.ToAsyncObservable(serialPort.IsOpenObservable);
    }

#if HasWindows
    /// <summary>Gets serial pin changes as an async observable.</summary>
    /// <param name="serialPort">The source serial port.</param>
    /// <returns>An async observable of pin change events.</returns>
    public static IObservableAsync<SerialPinChangedEventArgs> PinChangedAsyncObservable(ISerialPortRx? serialPort)
    {
        ArgumentGuard.ThrowIfNull(serialPort, nameof(serialPort));

        return ObservableAsyncBridgeExtensions.ToAsyncObservable(serialPort.PinChanged);
    }
#endif

    /// <summary>Monitors the received observer.</summary>
    /// <param name="serialPort">The source serial port.</param>
    /// <returns>Observable value.</returns>
    public static IObservable<EventPattern<SerialDataReceivedEventArgs>> DataReceivedObserver(
        SerialPort serialPort) =>
        Observable.FromEventPattern<SerialDataReceivedEventHandler, SerialDataReceivedEventArgs>(
            h => serialPort.DataReceived += h,
            h => serialPort.DataReceived -= h);

    /// <summary>Monitors the Errors observer.</summary>
    /// <param name="serialPort">The source serial port.</param>
    /// <returns>Observable value.</returns>
    public static IObservable<EventPattern<SerialErrorReceivedEventArgs>> ErrorReceivedObserver(
        SerialPort serialPort) =>
        Observable.FromEventPattern<SerialErrorReceivedEventHandler, SerialErrorReceivedEventArgs>(
            h => serialPort.ErrorReceived += h,
            h => serialPort.ErrorReceived -= h);

#if HasWindows
    /// <summary>Monitors the PinChanged observer.</summary>
    /// <param name="serialPort">The source serial port.</param>
    /// <returns>Observable value.</returns>
    public static IObservable<SerialPinChangedEventArgs> PinChangedObserver(SerialPort serialPort) =>
        Observable.Create<SerialPinChangedEventArgs>(observer =>
            {
                SerialPinChangedEventHandler handler = (_, args) => observer.OnNext(args);
                serialPort.PinChanged += handler;
                return Disposable.Create(() => serialPort.PinChanged -= handler);
            });
#endif

    /// <summary>Executes while port is open at the given TimeSpan.</summary>
    /// <param name="serialPort">The source serial port.</param>
    /// <param name="timespan">The timespan at which to notify.</param>
    /// <returns>Observable value.</returns>
    public static IObservable<bool> WhileIsOpen(SerialPortRx serialPort, TimeSpan timespan) =>
        Observable.Defer(() => Observable.Create<bool>(obs =>
        {
            var isOpen = Observable.Interval(timespan)
                .CombineLatest(serialPort.IsOpenObservable, (_, b) => b)
                .Where(x => x);
            return isOpen.Subscribe(obs);
        }));

    /// <summary>Executes while port is open at the given TimeSpan via an async observable.</summary>
    /// <param name="serialPort">The source serial port.</param>
    /// <param name="timespan">The timespan at which to notify.</param>
    /// <returns>Async observable value.</returns>
    public static IObservableAsync<bool> WhileIsOpenAsyncObservable(
        SerialPortRx serialPort,
        TimeSpan timespan) =>
        ObservableAsyncBridgeExtensions.ToAsyncObservable(WhileIsOpen(serialPort, timespan));

    /// <summary>Transforms a byte into a single value observable.</summary>
    /// <param name="value">The source byte.</param>
    /// <returns>An observable char.</returns>
    public static IObservable<char> AsObservable(byte value) => AsObservable((short)value);

    /// <summary>Transforms an int into a single value observable.</summary>
    /// <param name="value">The source integer.</param>
    /// <returns>An observable char.</returns>
    public static IObservable<char> AsObservable(int value) => Observable.Return(Convert.ToChar(value));

    /// <summary>Transforms a short into a single value observable.</summary>
    /// <param name="value">The source short.</param>
    /// <returns>An observable char.</returns>
    public static IObservable<char> AsObservable(short value) => AsObservable((int)value);

    /// <summary>Transforms a byte into a single value async observable.</summary>
    /// <param name="value">The source byte.</param>
    /// <returns>An async observable char.</returns>
    public static IObservableAsync<char> AsAsyncObservable(byte value) =>
        AsAsyncObservable((short)value);

    /// <summary>Transforms an int into a single value async observable.</summary>
    /// <param name="value">The source integer.</param>
    /// <returns>An async observable char.</returns>
    public static IObservableAsync<char> AsAsyncObservable(int value) =>
        ObservableAsync.Return(Convert.ToChar(value));

    /// <summary>Transforms a short into a single value async observable.</summary>
    /// <param name="value">The source short.</param>
    /// <returns>An async observable char.</returns>
    public static IObservableAsync<char> AsAsyncObservable(short value) =>
        AsAsyncObservable((int)value);

    /// <summary>Emits the list of available port names whenever it changes as an async observable.</summary>
    /// <returns>An async observable of port name arrays.</returns>
    public static IObservableAsync<string[]> PortNamesAsyncObservable() =>
        ObservableAsyncBridgeExtensions.ToAsyncObservable(SerialPortRx.PortNames());

    /// <summary>Emits the list of available port names whenever it changes as an async observable.</summary>
    /// <param name="pollInterval">The poll interval.</param>
    /// <returns>An async observable of port name arrays.</returns>
    public static IObservableAsync<string[]> PortNamesAsyncObservable(int pollInterval) =>
        PortNamesAsyncObservable(pollInterval, 0);

    /// <summary>Emits the list of available port names whenever it changes as an async observable.</summary>
    /// <param name="pollInterval">The poll interval.</param>
    /// <param name="pollLimit">The poll limit.</param>
    /// <returns>An async observable of port name arrays.</returns>
    public static IObservableAsync<string[]> PortNamesAsyncObservable(int pollInterval, int pollLimit) =>
        ObservableAsyncBridgeExtensions.ToAsyncObservable(SerialPortRx.PortNames(pollInterval, pollLimit));
}
