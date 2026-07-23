// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using IoT.DriverCore.ModbusRx.Reactive.Data;
#else
using IoT.DriverCore.ModbusRx.Data;
#endif
#if REACTIVE_SHIM
using IoT.DriverCore.ModbusRx.Reactive.Device;
#else
using IoT.DriverCore.ModbusRx.Device;
#endif
#if REACTIVE_SHIM
using IoT.DriverCore.ModbusRx.Reactive.Utility;
#else
using IoT.DriverCore.ModbusRx.Utility;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive;
#else
namespace IoT.DriverCore.ModbusRx;
#endif
    /// <summary>Provides ModbusRx functionality.</summary>
    public static partial class Create
    {
        /// <summary>Convert ushort span to float with high-performance operations.</summary>
        /// <param name="inputs">The inputs span.</param>
        /// <param name="start">The start index.</param>
        /// <param name="swapWords">if set to <c>true</c> [swap words].</param>
        /// <returns>A float value or null if insufficient data.</returns>
        internal static float? ToFloatCore(ReadOnlySpan<ushort> inputs, int start, bool swapWords = true)
        {
            return inputs.Length < start + Two ? null : ModbusUtility.ReadSingle(inputs.Slice(start), swapWords);
        }

        /// <summary>Convert ushort array to float.</summary>
        /// <param name="inputs">The inputs.</param>
        /// <param name="start">The start.</param>
        /// <param name="swapWords">if set to <c>true</c> [swap words].</param>
        /// <returns>
        /// A float.
        /// </returns>
        internal static float? ToFloatCore(ushort[]? inputs, int start, bool swapWords = true)
        {
            return inputs is null || inputs.Length < start + Two
                ? null
                : CreateExtensions.ToFloat(inputs.AsSpan(), start, swapWords);
        }

        /// <summary>Convert ushort span to double with high-performance operations.</summary>
        /// <param name="inputs">The inputs span.</param>
        /// <param name="start">The start index.</param>
        /// <param name="swapWords">if set to <c>true</c> [swap words].</param>
        /// <returns>A double value or null if insufficient data.</returns>
        internal static double? ToDoubleCore(ReadOnlySpan<ushort> inputs, int start, bool swapWords = true)
        {
            return inputs.Length < start + Four ? null : ModbusUtility.ReadDouble(inputs.Slice(start), swapWords);
        }

        /// <summary>Converts to double.</summary>
        /// <param name="inputs">The inputs.</param>
        /// <param name="start">The start.</param>
        /// <param name="swapWords">if set to <c>true</c> [swap words].</param>
        /// <returns>A double.</returns>
        internal static double? ToDoubleCore(ushort[]? inputs, int start, bool swapWords = true)
        {
            return inputs is null || inputs.Length < start + Four
                ? null
                : CreateExtensions.ToDouble(inputs.AsSpan(), start, swapWords);
        }

        /// <summary>Write float to ushort span with high-performance operations.</summary>
        /// <param name="input">The input value.</param>
        /// <param name="output">The output span.</param>
        /// <param name="start">The start index.</param>
        /// <param name="swapWords">if set to <c>true</c> [swap words].</param>
        /// <exception cref="ArgumentException">Thrown when output span is too small.</exception>
        internal static void FromFloatCore(float input, Span<ushort> output, int start, bool swapWords = true)
        {
            if (output.Length < start + Two)
            {
                throw new ArgumentException("Output span is too small.", nameof(output));
            }

            ModbusUtility.WriteSingle(input, output.Slice(start), swapWords);
        }

        /// <summary>Froms the float.</summary>
        /// <param name="input">The input.</param>
        /// <param name="output">The output.</param>
        /// <param name="start">The start.</param>
        /// <param name="swapWords">if set to <c>true</c> [swap words].</param>
        internal static void FromFloatCore(float input, ushort[] output, int start, bool swapWords = true)
        {
            if (output is null || output.Length < start + Two)
            {
                return;
            }

            CreateExtensions.FromFloat(input, output.AsSpan(), start, swapWords);
        }

        /// <summary>Write double to ushort span with high-performance operations.</summary>
        /// <param name="input">The input value.</param>
        /// <param name="output">The output span.</param>
        /// <param name="start">The start index.</param>
        /// <param name="swapWords">if set to <c>true</c> [swap words].</param>
        /// <exception cref="ArgumentException">Thrown when output span is too small.</exception>
        internal static void FromDoubleCore(double input, Span<ushort> output, int start, bool swapWords = true)
        {
            if (output.Length < start + Four)
            {
                throw new ArgumentException("Output span is too small.", nameof(output));
            }

            ModbusUtility.WriteDouble(input, output.Slice(start), swapWords);
        }

        /// <summary>Froms the double.</summary>
        /// <param name="input">The input.</param>
        /// <param name="output">The output.</param>
        /// <param name="start">The start.</param>
        /// <param name="swapWords">if set to <c>true</c> [swap words].</param>
        internal static void FromDoubleCore(double input, ushort[] output, int start, bool swapWords = true)
        {
            if (output is null || output.Length < start + Four)
            {
                return;
            }

            CreateExtensions.FromDouble(input, output.AsSpan(), start, swapWords);
        }

        /// <summary>Observes the data store written to.</summary>
        /// <param name="slave">The slave.</param>
        /// <returns>An Observable of DataStoreEventArgs.</returns>
        internal static IObservable<DataStoreEventArgs> ObserveDataStoreReadFromCore(ModbusSlave slave) =>
            Observable.FromEventPattern<DataStoreEventArgs>(
                handler => slave.DataStore.DataStoreReadFrom += handler,
                handler => slave.DataStore.DataStoreReadFrom -= handler)
                .Select(pattern => pattern.EventArgs);

        /// <summary>Observes the data store written to.</summary>
        /// <param name="slave">The slave.</param>
        /// <returns>An Observable of DataStoreEventArgs.</returns>
        internal static IObservable<DataStoreEventArgs> ObserveDataStoreWrittenToCore(ModbusSlave slave) =>
            Observable.FromEventPattern<DataStoreEventArgs>(
                handler => slave.DataStore.DataStoreWrittenTo += handler,
                handler => slave.DataStore.DataStoreWrittenTo -= handler)
                .Select(pattern => pattern.EventArgs);

        /// <summary>Observes the request.</summary>
        /// <param name="slave">The slave.</param>
        /// <returns>An Observable of ModbusSlaveRequestEventArgs.</returns>
        internal static IObservable<ModbusSlaveRequestEventArgs> ObserveRequestCore(ModbusSlave slave) =>
            Observable.FromEventPattern<ModbusSlaveRequestEventArgs>(
                handler => slave.ModbusSlaveRequestReceived += handler,
                handler => slave.ModbusSlaveRequestReceived -= handler)
                .Select(pattern => pattern.EventArgs);

        /// <summary>Observes the write complete.</summary>
        /// <param name="slave">The slave.</param>
        /// <returns>An Observable of ModbusSlaveRequestEventArgs.</returns>
        internal static IObservable<ModbusSlaveRequestEventArgs> ObserveWriteCompleteCore(ModbusSlave slave) =>
            Observable.FromEventPattern<ModbusSlaveRequestEventArgs>(
                handler => slave.WriteComplete += handler,
                handler => slave.WriteComplete -= handler)
                .Select(pattern => pattern.EventArgs);
}
