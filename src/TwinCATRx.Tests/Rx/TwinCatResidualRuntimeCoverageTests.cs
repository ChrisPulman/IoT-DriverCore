// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Subjects;
using System.Reflection;
using System.ServiceProcess;
#if !NETFRAMEWORK
using System.Runtime.Versioning;
#endif
using CP.Collections;
using IoT.DriverCore.Core;
using IoT.DriverCore.TwinCATRx.Core;

namespace IoT.DriverCore.TwinCATRx.Tests.Rx;

/// <summary>Exercises error, completion, cancellation, and persistence paths through public TwinCAT runtime seams.</summary>
public sealed class TwinCatResidualRuntimeCoverageTests
{
    /// <summary>Stores the deterministic direct ADS address.</summary>
    private const string Address = ".Coverage.Value";

    /// <summary>Stores the deterministic logical tag name.</summary>
    private const string TagName = "CoverageValue";

    /// <summary>Stores the deterministic direct write payload.</summary>
    private const int WriteValue = 7;

    /// <summary>Verifies read correlation propagates native errors, completion, and caller cancellation.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Logical_Read_Correlation_Propagates_Error_Completion_And_CancellationAsync()
    {
        using (var native = new ControlledRxClient())
        using (var client = CreateClient(native))
        {
            var pending = client.ReadAsync(TagName);
            native.Data.OnError(new InvalidOperationException("native read error"));
            await TUnitAssert.That(async () => await pending).Throws<InvalidOperationException>();
        }

        using (var native = new ControlledRxClient())
        using (var client = CreateClient(native))
        {
            var pending = client.ReadAsync(TagName);
            native.Data.OnCompleted();
            await TUnitAssert.That(async () => await pending).Throws<InvalidOperationException>();
        }

        using (var native = new ControlledRxClient())
        using (var client = CreateClient(native))
        using (var cancellation = new CancellationTokenSource())
        {
            var pending = client.ReadAsync(TagName, cancellation.Token);
            cancellation.Cancel();
            await TUnitAssert.That(async () => await pending).Throws<OperationCanceledException>();
        }
    }

    /// <summary>Verifies write correlation rejects noise and reports failed native acknowledgements.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Logical_Write_Correlation_Rejects_Noise_And_Null_PayloadsAsync()
    {
        using var native = new ControlledRxClient();
        using var client = CreateClient(native);
        native.WriteHandler = (_, id) =>
        {
            native.WriteResults.OnNext(null);
            native.WriteResults.OnNext(" ");
            native.WriteResults.OnNext("other-correlation");
            native.WriteResults.OnNext($"Failure,{id}");
        };

        var failure = await client.WriteAsync(CreateValue(WriteValue));
        await TUnitAssert.That(failure.Succeeded).IsFalse();
        await TUnitAssert.That(failure.Error).Contains("Failure");

        await TUnitAssert.That(async () => await client.WriteAsync(CreateValue(null!)))
            .Throws<ArgumentNullException>();
    }

    /// <summary>Verifies async observation correctly reports stream completion and stream failures.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Logical_Async_Observation_Reports_Completion_And_FailureAsync()
    {
        using (var native = new ControlledRxClient())
        using (var client = CreateClient(native))
        {
            var enumerator = client.ObserveManyAsync([TagName]).GetAsyncEnumerator();
            try
            {
                var move = enumerator.MoveNextAsync().AsTask();
                native.Data.OnCompleted();
                await TUnitAssert.That(await move).IsFalse();
            }
            finally
            {
                await enumerator.DisposeAsync();
            }
        }

        using (var native = new ControlledRxClient())
        using (var client = CreateClient(native))
        {
            var enumerator = client.ObserveAsync(TagName).GetAsyncEnumerator();
            try
            {
                var move = enumerator.MoveNextAsync().AsTask();
                native.Data.OnError(new InvalidOperationException("native observation error"));
                await TUnitAssert.That(async () => await move).Throws<InvalidOperationException>();
            }
            finally
            {
                await enumerator.DisposeAsync();
            }
        }
    }

    /// <summary>Verifies the optional persistence service is guarded and an externally owned catalog survives disposal.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Logical_Persistence_Guards_And_External_Catalog_Lifetime_Are_DeterministicAsync()
    {
        using var native = new ControlledRxClient();
        using var catalog = new LogicalTagCatalog();
        using var client = new TwinCatLogicalTagClient(native, catalog);
        client.RegisterTag(CreateTag());

        await TUnitAssert.That(async () => await client.GetTagAsync(TagName)).Throws<InvalidOperationException>();
        await TUnitAssert.That(async () => await client.ListTagsAsync(CancellationToken.None))
            .Throws<InvalidOperationException>();

        client.Dispose();
        client.Dispose();
        await TUnitAssert.That(catalog.TryGet(TagName, out var retained)).IsTrue();
        await TUnitAssert.That(retained!.Name).IsEqualTo(TagName);
    }

    /// <summary>Verifies extension streams reject nonmatching, null, and uncorrelated ADS events.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Extension_Observation_Filters_Are_DeterministicAsync()
    {
        const string correlationId = "coverage-correlation";
        const string otherAddress = ".Coverage.Other";
        const int directValue = 11;
        const int correlatedValue = 12;
        const int expectedDirectObservationCount = 3;
        using var native = new ControlledRxClient();
        var directValues = new List<int>();
        var correlatedValues = new List<int>();
        using var directSubscription = ObservableBridgeExtensions.SubscribeTo(
            TwinCatRxExtensions.Observe(native, Address, static value => (int)value!),
            directValues.Add);
        using var correlatedSubscription = ObservableBridgeExtensions.SubscribeTo(
            TwinCatRxExtensions.Observe(native, Address, correlationId, static value => (int)value!),
            correlatedValues.Add);

        native.Data.OnNext((otherAddress, directValue, correlationId));
        native.Data.OnNext((Address, null, correlationId));
        native.Data.OnNext((Address, directValue, "other-correlation"));
        native.Data.OnNext((Address, directValue, null));
        native.Data.OnNext((Address, correlatedValue, correlationId));

        await TUnitAssert.That(directValues.Count).IsEqualTo(expectedDirectObservationCount);
        await TUnitAssert.That(directValues[0]).IsEqualTo(directValue);
        await TUnitAssert.That(directValues[1]).IsEqualTo(directValue);
        await TUnitAssert.That(directValues[2]).IsEqualTo(correlatedValue);
        await TUnitAssert.That(correlatedValues.Single()).IsEqualTo(correlatedValue);
    }

    /// <summary>Verifies logical-tag persistence overloads synchronize the catalog without a physical controller.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Logical_Persistence_Overloads_Synchronize_Store_And_CatalogAsync()
    {
        const string missingTagName = nameof(missingTagName);
        var databasePath = Path.Combine(Path.GetTempPath(), $"TwinCATRx_Coverage_{Guid.NewGuid()}.db");
        try
        {
            using var native = new ControlledRxClient();
            var store = new LogicalTagSqliteStore($"Data Source={databasePath};Pooling=False");
            using var client = new TwinCatLogicalTagClient(native, store);
            await client.InitializeStoreAsync(CancellationToken.None);
            await client.UpsertTagAsync(CreateTag(), CancellationToken.None);

            var duplicateCsv = string.Concat(
                "Name;Address;DataType;GroupName;Description;Metadata;AccessMode;ScanIntervalMilliseconds\r\n",
                TagName,
                ";.Coverage.Replaced;DINT;;;;ReadWrite;\r\n");
            using var reader = new StringReader(duplicateCsv);
            _ = await client.ImportCsvAsync(reader, ';', replaceExisting: false, CancellationToken.None);
            await TUnitAssert.That(client.Catalog.TryGet(TagName, out var retained)).IsTrue();
            await TUnitAssert.That(retained!.Address).IsEqualTo(Address);

            await client.UpsertTagAsync(CreateTag(), CancellationToken.None);
            await TUnitAssert.That(await client.EditTagAsync(CreateTag(), CancellationToken.None)).IsTrue();
            await TUnitAssert.That(await client.DeleteTagAsync(missingTagName, CancellationToken.None)).IsFalse();
            await TUnitAssert.That(await client.LoadTagsAsync(replaceExisting: false, CancellationToken.None)).Count()
                .IsEqualTo(1);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>Verifies composed service runtime command, refresh, error, and enumeration seams without opening a service.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if !NETFRAMEWORK
    [SupportedOSPlatform("windows")]
#endif
    public async Task Service_Controller_Runtime_Seams_Cover_Status_Error_And_EnumerationAsync()
    {
        const int firstTick = 0;
        const int secondTick = 1;
        using var ticks = new Subject<long>();
        var runtime = new CoverageServiceRuntime
        {
            CanStop = true,
            Status = ServiceControllerStatus.Running,
            RefreshedStatus = ServiceControllerStatus.Paused,
        };
        using var controller = new ObservableServiceController(runtime, ticks);
        var statuses = new List<ServiceControllerStatus>();
        var errors = new List<Exception>();
        using var statusSubscription = ObservableBridgeExtensions.SubscribeTo(
            controller.StatusObserver,
            statuses.Add,
            errors.Add,
            static () => { });

        controller.Restart();
        ticks.OnNext(firstTick);
        runtime.StatusError = new InvalidOperationException("coverage service error");
        ticks.OnNext(secondTick);

        var services = new CoverageObserver<ObservableServiceController>();
        using var serviceSubscription = ObservableServiceController
            .GetServices(new CoverageServiceSource(new CoverageServiceRuntime()), TimeSpan.FromHours(1))
            .Subscribe(services);
        var unsupported = new CoverageObserver<ObservableServiceController>();
        using var unsupportedSubscription = ObservableServiceController
            .GetServices(new UnsupportedCoverageServiceSource(), TimeSpan.FromHours(1))
            .Subscribe(unsupported);

        await TUnitAssert.That(runtime.StopCount).IsEqualTo(1);
        await TUnitAssert.That(runtime.StartCount).IsEqualTo(1);
        await TUnitAssert.That(statuses).Contains(ServiceControllerStatus.Paused);
        await TUnitAssert.That(errors.Count).IsEqualTo(1);
        await TUnitAssert.That(services.Values.Count).IsEqualTo(1);
        await TUnitAssert.That(unsupported.Completed).IsTrue();
    }

    /// <summary>Verifies extension null, clone, and metadata branches through a local ADS seam.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("HashTableRx structure APIs reflect over test data.")]
#endif
    public async Task Extension_Structure_Guards_And_Clone_Branches_Are_DeterministicAsync()
    {
        using var native = new ControlledRxClient { Settings = null };
        using var table = TwinCatRxExtensions.CreateStruct(native, Address)!;
        using var clone = TwinCatRxExtensions.CreateClone(table);

        await TUnitAssert.That(TwinCatRxExtensions.WriteValues(table, static _ => { })).IsFalse();
        await TUnitAssert.That(TwinCatRxExtensions.CreateStruct(null!, Address)).IsNull();
        await TUnitAssert.That(clone.Structure).IsNull();
        await TUnitAssert.That(() => TwinCatRxExtensions.CreateClone(null!)).Throws<ArgumentNullException>();
        await TUnitAssert.That(() => TwinCatRxExtensions.StructureReady(null!)).Throws<ArgumentNullException>();
    }

    /// <summary>Verifies persistence APIs reject null tag and group definitions before reaching SQLite.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Logical_Persistence_Rejects_Null_DefinitionsAsync()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"TwinCATRx_Nulls_{Guid.NewGuid()}.db");
        try
        {
            using var native = new ControlledRxClient();
            var store = new LogicalTagSqliteStore($"Data Source={databasePath};Pooling=False");
            using var client = new TwinCatLogicalTagClient(native, store);
            await client.InitializeStoreAsync();

            await TUnitAssert.That(async () => await client.UpsertTagAsync(null!))
                .Throws<ArgumentNullException>();
            await TUnitAssert.That(async () => await client.EditTagAsync(null!))
                .Throws<ArgumentNullException>();
            await TUnitAssert.That(async () => await client.UpsertGroupAsync(null!, CancellationToken.None))
                .Throws<ArgumentNullException>();
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>Verifies extension metadata checks distinguish a missing variable from a missing client.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("HashTableRx structure APIs reflect over test data.")]
#endif
    public async Task Extension_Write_Guards_Distinguish_Metadata_And_Client_BranchesAsync()
    {
        using var native = new ControlledRxClient();
        using var table = new HashTableRx(false);
        table.Tag[nameof(IRxTcAdsClient)] = native;

        await TUnitAssert.That(TwinCatRxExtensions.WriteValues(table, static _ => { })).IsFalse();
        await TUnitAssert.That(await TwinCatRxExtensions.WriteValuesAsync(table, static _ => { }, TimeSpan.Zero))
            .IsFalse();
        await TUnitAssert.That(TwinCatRxExtensions.StructureReady(table)).IsNotNull();
    }

    /// <summary>Verifies null service runtime and non-stoppable service commands remain deterministic.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if !NETFRAMEWORK
    [SupportedOSPlatform("windows")]
#endif
    public async Task Service_Controller_Null_And_NonStoppable_Branches_Are_DeterministicAsync()
    {
        using var nullRuntimeTicks = new Subject<long>();
        var nullRuntime = new CoverageServiceRuntime();
        using var nullRuntimeController = new ObservableServiceController(nullRuntime, nullRuntimeTicks);
        SetServiceRuntime(nullRuntimeController, null);

        await TUnitAssert.That(nullRuntimeController.Status).IsEqualTo(ServiceControllerStatus.Stopped);
        nullRuntimeController.Start();
        nullRuntimeController.Stop();
        nullRuntimeController.Restart();
        nullRuntimeTicks.OnNext(0);

        using var ticks = new Subject<long>();
        var nonStoppable = new CoverageServiceRuntime { Status = ServiceControllerStatus.Paused };
        using var controller = new ObservableServiceController(nonStoppable, ticks);
        controller.Restart();

        await TUnitAssert.That(controller.Status).IsEqualTo(ServiceControllerStatus.Paused);
        await TUnitAssert.That(nonStoppable.StartCount).IsEqualTo(0);
        await TUnitAssert.That(nonStoppable.StopCount).IsEqualTo(0);
    }

    /// <summary>Creates a configured logical client over the supplied controllable seam.</summary>
    /// <param name="native">The native seam.</param>
    /// <returns>The configured logical client.</returns>
    private static TwinCatLogicalTagClient CreateClient(ControlledRxClient native)
    {
        var client = new TwinCatLogicalTagClient(native);
        client.RegisterTag(CreateTag());
        return client;
    }

    /// <summary>Creates the direct tag used by the test cases.</summary>
    /// <returns>The configured logical tag.</returns>
    private static LogicalTag CreateTag() => new(TagName, Address, "DINT");

    /// <summary>Creates a timestamped logical tag value.</summary>
    /// <param name="value">The native payload.</param>
    /// <returns>The logical tag value.</returns>
    private static LogicalTagValue CreateValue(object value) =>
        new(TagName, value, TimeProvider.System.GetUtcNow(), "Good");

    /// <summary>Replaces the composed service runtime for null-state branch coverage.</summary>
    /// <param name="controller">The observable controller.</param>
    /// <param name="runtime">The replacement runtime.</param>
#if !NETFRAMEWORK
    [SupportedOSPlatform("windows")]
#endif
    private static void SetServiceRuntime(
        ObservableServiceController controller,
        IServiceControllerRuntime? runtime) =>
        typeof(ObservableServiceController)
            .GetField("_serviceController", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(controller, runtime);

    /// <summary>Deterministic service runtime for exercising the composed service wrapper.</summary>
#if !NETFRAMEWORK
    [SupportedOSPlatform("windows")]
#endif
    private sealed class CoverageServiceRuntime : IServiceControllerRuntime
    {
        /// <summary>Stores the current service status.</summary>
        private ServiceControllerStatus _status = ServiceControllerStatus.Stopped;

        /// <inheritdoc/>
        public event EventHandler? Disposed;

        /// <inheritdoc/>
        public bool CanStop { get; set; }

        /// <inheritdoc/>
        public string DisplayName => "Coverage Service";

        /// <inheritdoc/>
        public string ServiceName => "Coverage";

        /// <inheritdoc/>
        public ServiceControllerStatus Status
        {
            get
            {
                if (StatusError is not null)
                {
                    throw StatusError;
                }

                return _status;
            }

            set => _status = value;
        }

        /// <summary>Gets or sets the status assigned by the next refresh.</summary>
        public ServiceControllerStatus? RefreshedStatus { get; set; }

        /// <summary>Gets or sets the error raised by status reads.</summary>
        public Exception? StatusError { get; set; }

        /// <summary>Gets the number of start operations.</summary>
        public int StartCount { get; private set; }

        /// <summary>Gets the number of stop operations.</summary>
        public int StopCount { get; private set; }

        /// <inheritdoc/>
        public void Dispose() => Disposed?.Invoke(this, EventArgs.Empty);

        /// <inheritdoc/>
        public void Refresh()
        {
            if (!RefreshedStatus.HasValue)
            {
                return;
            }

            _status = RefreshedStatus.Value;
        }

        /// <inheritdoc/>
        public void Start()
        {
            StartCount++;
            _status = ServiceControllerStatus.Running;
        }

        /// <inheritdoc/>
        public void Stop()
        {
            StopCount++;
            _status = ServiceControllerStatus.Stopped;
        }

        /// <inheritdoc/>
        public void WaitForStatus(ServiceControllerStatus status)
        {
        }
    }

    /// <summary>Deterministic source that exposes one service runtime.</summary>
    /// <param name="service">The service runtime to expose.</param>
#if !NETFRAMEWORK
    [SupportedOSPlatform("windows")]
#endif
    private sealed class CoverageServiceSource(IServiceControllerRuntime service) : IServiceControllerSource
    {
        /// <inheritdoc/>
        public IEnumerable<IServiceControllerRuntime> GetServices() => [service];
    }

    /// <summary>Deterministic source that models an unavailable service manager.</summary>
#if !NETFRAMEWORK
    [SupportedOSPlatform("windows")]
#endif
    private sealed class UnsupportedCoverageServiceSource : IServiceControllerSource
    {
        /// <inheritdoc/>
        public IEnumerable<IServiceControllerRuntime> GetServices() =>
            throw new PlatformNotSupportedException();
    }

    /// <summary>Records observable notifications used by service enumeration assertions.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    private sealed class CoverageObserver<T> : IObserver<T>
    {
        /// <summary>Gets the observed values.</summary>
        public List<T> Values { get; } = [];

        /// <summary>Gets a value indicating whether the sequence completed.</summary>
        public bool Completed { get; private set; }

        /// <inheritdoc/>
        public void OnCompleted() => Completed = true;

        /// <inheritdoc/>
        public void OnError(Exception error)
        {
        }

        /// <inheritdoc/>
        public void OnNext(T value) => Values.Add(value);
    }

    /// <summary>Small, controllable implementation of the public ADS seam; it never opens an ADS connection.</summary>
    private sealed class ControlledRxClient : IRxTcAdsClient
    {
        /// <summary>Stores the TwinCAT 3 ADS port used by the seam settings.</summary>
        private const int TwinCat3Port = 851;

        /// <summary>Publishes paused-state changes.</summary>
        private readonly Subject<bool> _paused = new();

        /// <summary>Gets the controlled native data stream.</summary>
        public Subject<(string Variable, object? Data, string? Id)> Data { get; } = new();

        /// <summary>Gets the controlled native write-result stream.</summary>
        public Subject<string?> WriteResults { get; } = new();

        /// <summary>Gets or sets the action raised when a logical write is issued.</summary>
        public Action<string, string?>? WriteHandler { get; set; }

        /// <inheritdoc/>
        public IObservable<string[]> Code => Observable.Empty<string[]>();

        /// <inheritdoc/>
        public IObservable<Unit> InitializeComplete => Observable.Return(Unit.Default);

        /// <inheritdoc/>
        public IObservableAsync<Unit> InitializeCompleteAsync =>
            ObservableBridgeExtensions.ToAsyncObservable(InitializeComplete);

        /// <inheritdoc/>
        public IObservable<(string Variable, object? Data, string? Id)> DataReceived => Data;

        /// <inheritdoc/>
        public IObservableAsync<(string Variable, object? Data, string? Id)> DataReceivedAsync =>
            ObservableBridgeExtensions.ToAsyncObservable(DataReceived);

        /// <inheritdoc/>
        public IObservable<Exception> ErrorReceived => Observable.Empty<Exception>();

        /// <inheritdoc/>
        public IObservableAsync<Exception> ErrorReceivedAsync =>
            ObservableBridgeExtensions.ToAsyncObservable(ErrorReceived);

        /// <inheritdoc/>
        public IObservable<string?> OnWrite => WriteResults;

        /// <inheritdoc/>
        public IObservableAsync<string?> OnWriteAsync => ObservableBridgeExtensions.ToAsyncObservable(OnWrite);

        /// <inheritdoc/>
        public IDictionary<string, uint?> ReadWriteHandleInfo { get; } = new Dictionary<string, uint?>();

        /// <inheritdoc/>
        public ISettings? Settings { get; set; } = new Settings
        {
            Port = TwinCat3Port,
            AdsAddress = string.Empty,
            SettingsId = "Coverage",
        };

        /// <inheritdoc/>
        public IDictionary<string, (uint? Handle, int ArrayLength)> WriteHandleInfo { get; } =
            new Dictionary<string, (uint? Handle, int ArrayLength)>();

        /// <inheritdoc/>
        public bool IsPaused { get; private set; }

        /// <inheritdoc/>
        public IObservable<bool> IsPausedObservable => _paused;

        /// <inheritdoc/>
        public IObservableAsync<bool> IsPausedObservableAsync =>
            ObservableBridgeExtensions.ToAsyncObservable(IsPausedObservable);

        /// <inheritdoc/>
        public bool IsDisposed { get; private set; }

        /// <inheritdoc/>
        public void Pause(TimeSpan time)
        {
            IsPaused = true;
            _paused.OnNext(true);
        }

        /// <inheritdoc/>
        public void Connect(ISettings settings)
        {
        }

        /// <inheritdoc/>
        public void Disconnect()
        {
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
        }

        /// <inheritdoc/>
        public void Write(string variable, object value) => Write(variable, value, null);

        /// <inheritdoc/>
        public void Write(string variable, object value, string? id) => WriteHandler?.Invoke(variable, id);

        /// <inheritdoc/>
        public void Dispose()
        {
            IsDisposed = true;
            Data.Dispose();
            WriteResults.Dispose();
            _paused.Dispose();
        }
    }
}
