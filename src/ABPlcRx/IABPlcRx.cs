// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using CP.IoT.Core;

#if REACTIVELIST_REACTIVE
namespace ABPlcRx.Reactive;
#else
namespace ABPlcRx;
#endif

/// <summary>Reactive Allen Bradley PLC facade contract.</summary>
/// <seealso cref="IDisposable" />
public interface IABPlcRx : IDisposable
{
    /// <summary>Gets a value indicating whether the object is disposed.</summary>
    bool IsDisposed { get; }

    /// <summary>Gets the observe all.</summary>
    /// <value>
    /// The observe all.
    /// </value>
    IObservable<IPlcTag?> ObserveAll { get; }

    /// <summary>Gets the asynchronous observe all stream.</summary>
    /// <value>
    /// The asynchronous observe all stream.
    /// </value>
    IObservableAsync<IPlcTag?> ObserveAllAsyncObservable { get; }

    /// <summary>Gets or sets a value indicating whether [scan enabled].</summary>
    /// <value>
    ///   <c>true</c> if [scan enabled]; otherwise, <c>false</c>.
    /// </value>
    bool ScanEnabled { get; set; }

    /// <summary>Gets or sets a value indicating whether [automatic write value].</summary>
    /// <value>
    ///   <c>true</c> if [automatic write value]; otherwise, <c>false</c>.
    /// </value>
    bool AutoWriteValue { get; set; }

    /// <summary>Adds the update tag item.</summary>
    /// <typeparam name="T">The tag type.</typeparam>
    /// <param name="tagName">Name of the PLC tag.</param>
    /// <param name="typeWitness">
    /// Optional type witness for callers that infer <typeparamref name="T"/> from a value.
    /// </param>
    /// <exception cref="System.ArgumentNullException">tagName.</exception>
    void AddUpdateTagItem<T>(string tagName, T? typeWitness);

    /// <summary>Adds the update tag item.</summary>
    /// <typeparam name="T">The tag type.</typeparam>
    /// <param name="variable">The variable, this can be any non null name you wish to use.</param>
    /// <param name="tagName">Name of the plc tag.</param>
    /// <param name="typeWitness">
    /// Optional type witness for callers that infer <typeparamref name="T"/> from a value.
    /// </param>
    /// <exception cref="System.ArgumentNullException">tagName.</exception>
    void AddUpdateTagItem<T>(string variable, string tagName, T? typeWitness);

    /// <summary>Adds the update tag item.</summary>
    /// <typeparam name="T">The tag type.</typeparam>
    /// <param name="variable">The variable, this can be any non null name you wish to use.</param>
    /// <param name="tagName">Name of the plc tag.</param>
    /// <param name="tagGroup">The tag group.</param>
    /// <param name="typeWitness">
    /// Optional type witness for callers that infer <typeparamref name="T"/> from a value.
    /// </param>
    /// <exception cref="System.ArgumentNullException">tagName.</exception>
    void AddUpdateTagItem<T>(string variable, string tagName, string tagGroup, T? typeWitness);

    /// <summary>Removes a registered tag by logical variable name.</summary>
    /// <param name="variable">The logical variable name.</param>
    /// <returns>True when a tag was removed.</returns>
    bool RemoveTagItem(string variable);

    /// <summary>Observes the specified variable.</summary>
    /// <typeparam name="T">The PLC type.</typeparam>
    /// <param name="variable">The variable.</param>
    /// <param name="typeWitness">Type witness for the requested PLC value type.</param>
    /// <param name="bit">The bit.</param>
    /// <returns>
    /// An observable sequence of values of type T.
    /// </returns>
    IObservable<T?> Observe<T>(string? variable, T? typeWitness, int bit);

#if NET8_0_OR_GREATER
    /// <summary>Observes the specified variable using an async-native observable.</summary>
    /// <typeparam name="T">The PLC type.</typeparam>
    /// <param name="variable">The variable.</param>
    /// <param name="typeWitness">Type witness for the requested PLC value type.</param>
    /// <param name="bit">The bit.</param>
    /// <returns>
    /// An async observable sequence of values of type T.
    /// </returns>
    IObservableAsync<T?> ObserveAsyncObservable<T>(string? variable, T? typeWitness, int bit);
#endif

    /// <summary>Observe values for many variables and emit a latest-value dictionary.</summary>
    /// <param name="variables">One or more variable names to observe.</param>
    /// <returns>Observable sequence of dictionary containing the latest values for each variable.</returns>
    IObservable<IReadOnlyDictionary<string, object?>> ObserveMany(params string[] variables);

#if NET8_0_OR_GREATER
    /// <summary>Observe values for many variables using an async-native observable.</summary>
    /// <param name="variables">One or more variable names to observe.</param>
    /// <returns>Async observable sequence of dictionary containing the latest values for each variable.</returns>
    IObservableAsync<IReadOnlyDictionary<string, object?>> ObserveManyAsyncObservable(params string[] variables);
#endif

    /// <summary>Observe a PLC tag group, emitting the tag whose value changed.</summary>
    /// <param name="groupName">The group name to observe.</param>
    /// <returns>Observable sequence of tags in the group that have changed.</returns>
    IObservable<IPlcTag> ObserveGroup(string groupName);

#if NET8_0_OR_GREATER
    /// <summary>Observe a PLC tag group using an async-native observable.</summary>
    /// <param name="groupName">The group name to observe.</param>
    /// <returns>Async observable sequence of tags in the group that have changed.</returns>
    IObservableAsync<IPlcTag> ObserveGroupAsyncObservable(string groupName);
#endif

    /// <summary>Creates an observer that writes values to a PLC variable when OnNext is called.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="variable">The variable to write to.</param>
    /// <param name="typeWitness">Type witness for the writer value type.</param>
    /// <param name="bit">The bit [ONLY use for bool tags].</param>
    /// <returns>An observer that will write and commit values to the PLC.</returns>
    IObserver<T> CreateWriter<T>(string variable, T? typeWitness, int bit);

    /// <summary>Observe a variable with sampling, reducing event rate while preserving latest value.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="variable">The variable to observe.</param>
    /// <param name="sampleInterval">The sampling interval.</param>
    /// <param name="typeWitness">Type witness for the requested PLC value type.</param>
    /// <param name="bit">The bit [ONLY use for bool tags].</param>
    /// <param name="scheduler">Optional scheduler for sampling.</param>
    /// <returns>Observable sequence of sampled values.</returns>
    IObservable<T?> ObserveSampled<T>(
        string variable,
        TimeSpan sampleInterval,
        T? typeWitness,
        int bit,
        ISequencer? scheduler);

#if NET8_0_OR_GREATER
    /// <summary>Observe a variable with sampling using an async-native observable.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="variable">The variable to observe.</param>
    /// <param name="sampleInterval">The sampling interval.</param>
    /// <param name="typeWitness">Type witness for the requested PLC value type.</param>
    /// <param name="bit">The bit [ONLY use for bool tags].</param>
    /// <param name="scheduler">Optional scheduler for sampling.</param>
    /// <returns>Async observable sequence of sampled values.</returns>
    IObservableAsync<T?> ObserveSampledAsyncObservable<T>(
        string variable,
        TimeSpan sampleInterval,
        T? typeWitness,
        int bit,
        ISequencer? scheduler);
#endif

    /// <summary>Streams only error results across all tags.</summary>
    /// <returns>Observable sequence of error results.</returns>
    IObservable<PlcTagResult> ObserveErrors();

#if NET8_0_OR_GREATER
    /// <summary>Streams only error results across all tags using an async-native observable.</summary>
    /// <returns>Async observable sequence of error results.</returns>
    IObservableAsync<PlcTagResult> ObserveErrorsAsyncObservable();
#endif

    /// <summary>Values the specified variable.</summary>
    /// <typeparam name="T">The PLC type.</typeparam>
    /// <param name="variable">The variable.</param>
    /// <param name="typeWitness">Type witness for the requested PLC value type.</param>
    /// <param name="bit">The bit.</param>
    /// <returns>
    /// A value of T.
    /// </returns>
    T? GetValue<T>(string? variable, T? typeWitness, int bit);

    /// <summary>Values the specified variable.</summary>
    /// <typeparam name="T">The PLC type.</typeparam>
    /// <param name="variable">The variable.</param>
    /// <param name="value">The value.</param>
    /// <param name="bit">The bit.</param>
    void Value<T>(string? variable, T? value, int bit);

    /// <summary>Writes all tags in this instance.</summary>
    /// <returns>A sequence of PlcTagResult.</returns>
    IEnumerable<PlcTagResult> Write();

    /// <summary>Writes the specified variable.</summary>
    /// <param name="variable">The variable.</param>
    /// <returns>A PlcTagResult.</returns>
    PlcTagResult? Write(string? variable);

    /// <summary>Reads all tags in this instance.</summary>
    /// <returns>A sequence of PlcTagResult.</returns>
    IEnumerable<PlcTagResult> Read();

    /// <summary>Reads the specified variable.</summary>
    /// <param name="variable">The variable.</param>
    /// <returns>A PlcTagResult.</returns>
    PlcTagResult? Read(string? variable);

    /// <summary>Reads selected logical variables asynchronously.</summary>
    /// <param name="variables">The variables to read.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The PLC read results.</returns>
    Task<IReadOnlyList<PlcTagResult>> ReadManyAsync(
        IReadOnlyCollection<string> variables,
        CancellationToken cancellationToken);

    /// <summary>Writes selected logical variable values asynchronously.</summary>
    /// <param name="values">Values keyed by logical variable name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The PLC write results.</returns>
    Task<IReadOnlyList<PlcTagResult>> WriteManyAsync(
        IReadOnlyDictionary<string, object?> values,
        CancellationToken cancellationToken);

    /// <summary>Reads and converts one logical variable asynchronously.</summary>
    /// <typeparam name="T">The requested value type.</typeparam>
    /// <param name="variable">The logical variable name.</param>
    /// <param name="typeWitness">Type witness for the requested PLC value type.</param>
    /// <param name="bit">The optional integral bit index.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The typed operation result.</returns>
    Task<TagOperationResult<T>> ReadValueAsync<T>(
        string variable,
        T? typeWitness,
        int bit,
        CancellationToken cancellationToken);

    /// <summary>Writes one logical variable asynchronously.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="variable">The logical variable name.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="bit">The optional integral bit index.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The typed operation result.</returns>
    Task<TagOperationResult<T>> WriteValueAsync<T>(
        string variable,
        T value,
        int bit,
        CancellationToken cancellationToken);

    /// <summary>Ping the PLC.</summary>
    /// <param name="echo">True echo result to standard output.</param>
    /// <returns>True when ping succeeds; otherwise, false.</returns>
    bool Ping(bool echo);

    /// <summary>Ping the PLC asynchronously.</summary>
    /// <param name="echo">True echo result to standard output.</param>
    /// <param name="cancellationToken">A token to cancel the ping operation.</param>
    /// <returns>A task producing true when ping succeeds; otherwise, false.</returns>
    Task<bool> PingAsync(bool echo, CancellationToken cancellationToken);

    /// <summary>Observe ping results on a schedule.</summary>
    /// <param name="interval">The interval between pings.</param>
    /// <param name="echo">True echo result to standard output.</param>
    /// <param name="scheduler">Optional scheduler for the ping cadence.</param>
    /// <returns>Observable sequence of ping result states, deduplicated.</returns>
    IObservable<bool> ObservePing(TimeSpan interval, bool echo, ISequencer? scheduler);

#if NET8_0_OR_GREATER
    /// <summary>Observe ping results on a schedule using an async-native observable.</summary>
    /// <param name="interval">The interval between pings.</param>
    /// <param name="echo">True echo result to standard output.</param>
    /// <param name="scheduler">Optional scheduler for the ping cadence.</param>
    /// <returns>Async observable sequence of ping result states, deduplicated.</returns>
    IObservableAsync<bool> ObservePingAsyncObservable(
        TimeSpan interval,
        bool echo,
        ISequencer? scheduler);
#endif
}
