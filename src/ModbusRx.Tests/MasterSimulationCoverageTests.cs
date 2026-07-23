// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
using IoT.DriverCore.ModbusRx.Data;
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.ModbusRx.Extensions.Enron;
using IoT.DriverCore.ModbusRx.IO;
using IoT.DriverCore.ModbusRx.LogicalTags;
using IoT.DriverCore.ModbusRx.Message;
using NativeAssert = TUnit.Assertions.Assert;

namespace IoT.DriverCore.ModbusRx.UnitTests;

/// <summary>Deterministic master tests backed by an in-memory response transport.</summary>
public sealed class MasterSimulationCoverageTests
{
    /// <summary>The unit identifier used by requests.</summary>
    private const byte UnitId = 1;

    /// <summary>The point count used by read operations.</summary>
    private const ushort PointCount = 2;

    /// <summary>The low word of the representative Enron value.</summary>
    private const ushort LowWord = 0x5678;

    /// <summary>The high word of the representative Enron value.</summary>
    private const ushort HighWord = 0x1234;

    /// <summary>The representative Enron 32-bit value.</summary>
    private const uint EnronValue = 0x12345678;

    /// <summary>One more than the maximum Enron read count.</summary>
    private const ushort InvalidEnronReadCount = 63;

    /// <summary>One more than the maximum Enron write count.</summary>
    private const int InvalidEnronWriteCount = 62;

    /// <summary>The logical holding-register tag name.</summary>
    private const string HoldingTagName = "Holding";

    /// <summary>The logical coil tag name.</summary>
    private const string CoilTagName = "Coil";

    /// <summary>The logical input-register tag name.</summary>
    private const string InputTagName = "Input";

    /// <summary>The logical discrete-input tag name.</summary>
    private const string DiscreteTagName = "Discrete";

    /// <summary>An unregistered logical tag name.</summary>
    private const string MissingTagName = "Missing";

    /// <summary>The result count for the mixed-area read.</summary>
    private const int MixedReadResultCount = 5;

    /// <summary>The logical name used by persisted-tag tests.</summary>
    private const string StoredTagName = "Stored";

    /// <summary>A logical name used to provoke a coalesced decode failure.</summary>
    private const string WideTagName = "Wide";

    /// <summary>A logical name with a shorter observation interval.</summary>
    private const string FastTagName = "Fast";

    /// <summary>The logical write-only tag name.</summary>
    private const string WriteOnlyTagName = "WriteOnly";

    /// <summary>The updated address used by persisted-tag tests.</summary>
    private const ushort UpdatedStoredAddress = 7;

    /// <summary>Executes every standard master operation against an in-memory transport.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusMaster_ExecutesEveryOperationAgainstRecordingTransportAsync()
    {
        var transport = new RecordingTransport();
        using var master = new ModbusMaster(transport);

        await NativeAssert.That(await master.ReadCoilsAsync(UnitId, 0, PointCount))
            .IsEquivalentTo([true, false]);
        await NativeAssert.That(await master.ReadInputsAsync(UnitId, 0, PointCount))
            .IsEquivalentTo([true, false]);
        await NativeAssert.That(await master.ReadHoldingRegistersAsync(UnitId, 0, PointCount))
            .IsEquivalentTo([LowWord, HighWord]);
        await NativeAssert.That(await master.ReadInputRegistersAsync(UnitId, 0, PointCount))
            .IsEquivalentTo([LowWord, HighWord]);
        await NativeAssert.That(
                await master.ReadWriteMultipleRegistersAsync(
                    UnitId,
                    0,
                    PointCount,
                    0,
                    [HighWord]))
            .IsEquivalentTo([LowWord, HighWord]);

        await master.WriteSingleCoilAsync(UnitId, 0, true);
        await master.WriteSingleRegisterAsync(UnitId, 0, HighWord);
        await master.WriteMultipleCoilsAsync(UnitId, 0, [true, false]);
        await master.WriteMultipleRegistersAsync(UnitId, 0, [LowWord, HighWord]);
        var custom = master.ExecuteCustomMessage(
            new DiagnosticsRequestResponse(0, UnitId, new RegisterCollection(LowWord)),
            static () => new DiagnosticsRequestResponse());

        await NativeAssert.That(custom).IsNotNull();
        await NativeAssert.That(transport.LastRequest).IsNotNull();
    }

    /// <summary>Round-trips Enron 32-bit values through standard register operations.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task EnronExtensions_ConvertAndForwardValuesAsync()
    {
        var transport = new RecordingTransport();
        using var master = new ModbusMaster(transport);

        await NativeAssert.That(
                await EnronModbusExtensions.ReadHoldingRegisters32Async(master, UnitId, 0, 1))
            .IsEquivalentTo([EnronValue]);
        await NativeAssert.That(
                await EnronModbusExtensions.ReadInputRegisters32Async(master, UnitId, 0, 1))
            .IsEquivalentTo([EnronValue]);
        await EnronModbusExtensions.WriteSingleRegister32Async(master, UnitId, 0, EnronValue);
        await NativeAssert.That(((WriteMultipleRegistersRequest)transport.LastRequest!).Data)
            .IsEquivalentTo([LowWord, HighWord]);
        await EnronModbusExtensions.WriteMultipleRegisters32Async(
            master,
            UnitId,
            0,
            [EnronValue, EnronValue]);
        await NativeAssert.That(((WriteMultipleRegistersRequest)transport.LastRequest!).Data)
            .IsEquivalentTo([LowWord, HighWord, LowWord, HighWord]);
    }

    /// <summary>Verifies Enron extension input guards.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task EnronExtensions_RejectInvalidArgumentsAsync()
    {
        using var master = new ModbusMaster(new RecordingTransport());

        await NativeAssert.That(
                async () => await EnronModbusExtensions.ReadHoldingRegisters32Async(null!, UnitId, 0, 1))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                async () => await EnronModbusExtensions.ReadInputRegisters32Async(null!, UnitId, 0, 1))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                () => EnronModbusExtensions.WriteSingleRegister32Async(null!, UnitId, 0, EnronValue))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                async () => await EnronModbusExtensions.ReadHoldingRegisters32Async(master, UnitId, 0, 0))
            .Throws<ArgumentException>();
        await NativeAssert.That(
                async () => await EnronModbusExtensions.ReadInputRegisters32Async(
                    master,
                    UnitId,
                    0,
                    InvalidEnronReadCount))
            .Throws<ArgumentException>();
        await NativeAssert.That(
                async () => await EnronModbusExtensions.WriteMultipleRegisters32Async(
                    master,
                    UnitId,
                    0,
                    null!))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                async () => await EnronModbusExtensions.WriteMultipleRegisters32Async(master, UnitId, 0, []))
            .Throws<ArgumentException>();
        await NativeAssert.That(
                async () => await EnronModbusExtensions.WriteMultipleRegisters32Async(
                    master,
                    UnitId,
                    0,
                    new uint[InvalidEnronWriteCount]))
            .Throws<ArgumentException>();
    }

    /// <summary>Reads every logical data area and reports lookup, access, and transport failures.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task LogicalClient_ReadsAllAreasAndReportsFailuresAsync()
    {
        var transport = new RecordingTransport();
        using var master = new ModbusMaster(transport);
        using var client = CreateLogicalClient(master);

        var holding = await client.ReadAsync(HoldingTagName);
        var coil = await client.ReadAsync(CoilTagName);
        var input = await client.ReadAsync(InputTagName);
        var discrete = await client.ReadAsync(DiscreteTagName);
        var missing = await client.ReadAsync(MissingTagName);
        var writeOnly = await client.ReadAsync(WriteOnlyTagName);
        var many = await client.ReadManyAsync(
            [HoldingTagName, CoilTagName, InputTagName, DiscreteTagName, MissingTagName]);
        transport.ThrowOnRead = true;
        var failure = await client.ReadAsync(HoldingTagName);

        await NativeAssert.That(holding.Succeeded).IsTrue();
        await NativeAssert.That(coil.Succeeded).IsTrue();
        await NativeAssert.That(input.Succeeded).IsTrue();
        await NativeAssert.That(discrete.Succeeded).IsTrue();
        await NativeAssert.That(missing.Succeeded).IsFalse();
        await NativeAssert.That(writeOnly.Succeeded).IsFalse();
        await NativeAssert.That(many.Count).IsEqualTo(MixedReadResultCount);
        await NativeAssert.That(failure.Succeeded).IsFalse();
    }

    /// <summary>Writes scalar and array logical values and exercises failure results.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task LogicalClient_WritesValuesAndObservesChangesAsync()
    {
        using var master = new ModbusMaster(new RecordingTransport());
        using var client = CreateLogicalClient(master);
        var timestamp = TestFrameworkCompatibilityExtensions.UnixEpoch;

        var coil = await client.WriteAsync(new LogicalTagValue(CoilTagName, true, timestamp));
        var holding = await client.WriteAsync(new LogicalTagValue(HoldingTagName, LowWord, timestamp));
        var array = await client.WriteAsync(
            new LogicalTagValue("HoldingArray", new[] { LowWord, HighWord }, timestamp));
        var missing = await client.WriteAsync(new LogicalTagValue(MissingTagName, LowWord, timestamp));
        var readOnly = await client.WriteAsync(new LogicalTagValue(InputTagName, LowWord, timestamp));
        var invalid = await client.WriteAsync(new LogicalTagValue(HoldingTagName, true, timestamp));
        var many = await client.WriteManyAsync(
            [
                new LogicalTagValue(CoilTagName, false, timestamp),
                new LogicalTagValue(HoldingTagName, HighWord, timestamp),
            ]);
        var observed = await client.Observe(HoldingTagName).FirstAsync();
        var observedMany = await client.ObserveMany([HoldingTagName, CoilTagName]).FirstAsync();

        await NativeAssert.That(coil.Succeeded).IsTrue();
        await NativeAssert.That(holding.Succeeded).IsTrue();
        await NativeAssert.That(array.Succeeded).IsTrue();
        await NativeAssert.That(missing.Succeeded).IsFalse();
        await NativeAssert.That(readOnly.Succeeded).IsFalse();
        await NativeAssert.That(invalid.Succeeded).IsFalse();
        await NativeAssert.That(many.All(static result => result.Succeeded)).IsTrue();
        await NativeAssert.That(observed.TagName).IsEqualTo(HoldingTagName);
        await NativeAssert.That(observedMany.TagName).IsEqualTo(HoldingTagName);
    }

    /// <summary>Exercises the complete persisted-tag facade and its lifecycle guards.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task LogicalClient_PersistedTagFacadeRoundTripsAndValidatesLifecycleAsync()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"modbusrx-simulation-{Guid.NewGuid():N}.db");
        try
        {
            using var master = new ModbusMaster(new RecordingTransport());
            var client = new ModbusLogicalTagClient(master, null, TimeSpan.FromMilliseconds(1));
            await NativeAssert.That(
                    async () => await client.ListStoredTagsAsync(CancellationToken.None))
                .Throws<InvalidOperationException>();

            await client.InitializeStoreAsync($"Data Source={databasePath};Pooling=False", CancellationToken.None);
            var original = new ModbusLogicalTag(new ModbusTagConfiguration(
                StoredTagName,
                UnitId,
                ModbusDataArea.HoldingRegister,
                0,
                1,
                typeof(ushort)));
            await client.UpsertStoredTagAsync(original, CancellationToken.None);

            var stored = await client.GetStoredTagAsync(StoredTagName, CancellationToken.None);
            var listed = await client.ListStoredTagsAsync(CancellationToken.None);
            var updatedTag = new ModbusLogicalTag(new ModbusTagConfiguration(
                StoredTagName,
                UnitId,
                ModbusDataArea.HoldingRegister,
                UpdatedStoredAddress,
                1,
                typeof(ushort)));
            var updated = await client.UpdateStoredTagAsync(updatedTag, CancellationToken.None);
            var absent = await client.UpdateStoredTagAsync(
                new ModbusLogicalTag(new ModbusTagConfiguration(
                    MissingTagName,
                    UnitId,
                    ModbusDataArea.HoldingRegister,
                    0,
                    1,
                    typeof(ushort))),
                CancellationToken.None);
            _ = client.Catalog.TryGet(StoredTagName, out var catalogTag);

            await NativeAssert.That(stored?.Name).IsEqualTo(StoredTagName);
            await NativeAssert.That(listed.Select(static tag => tag.Name)).Contains(StoredTagName);
            await NativeAssert.That(updated).IsTrue();
            await NativeAssert.That(absent).IsFalse();
            await NativeAssert.That(catalogTag?.Address).IsEqualTo(UpdatedStoredAddress);

            client.Dispose();
            client.Dispose();
            await NativeAssert.That(
                    async () => await client.GetStoredTagAsync(StoredTagName, CancellationToken.None))
                .Throws<ObjectDisposedException>();
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    /// <summary>Exercises logical-client CSV forwarding, null guards, cancellation, and access failures.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task LogicalClient_ValidatesFacadeBulkAndCancellationInputsAsync()
    {
        using var master = new ModbusMaster(new RecordingTransport());
        using var client = CreateLogicalClient(master);
        using var writer = new StringWriter();
        await client.ExportCsvAsync(writer, CancellationToken.None);
        using var importedMaster = new ModbusMaster(new RecordingTransport());
        using var imported = new ModbusLogicalTagClient(importedMaster, null, TimeSpan.FromMilliseconds(1));
        var importedCount = await imported.ImportCsvAsync(
            new StringReader(writer.ToString()),
            CancellationToken.None);

        await NativeAssert.That(importedCount).IsEqualTo(client.Catalog.List().Count);
        await NativeAssert.That(
                async () => await client.ReadManyAsync(null!, CancellationToken.None))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                async () => await client.WriteAsync(null!, CancellationToken.None))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                async () => await client.WriteManyAsync(null!, CancellationToken.None))
            .Throws<ArgumentNullException>();

        var writeOnly = await client.ReadManyAsync([WriteOnlyTagName], CancellationToken.None);
        await NativeAssert.That(writeOnly.Single().Succeeded).IsFalse();

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await NativeAssert.That(
                async () => await client.ReadAsync(HoldingTagName, cancellation.Token))
            .Throws<OperationCanceledException>();
        await NativeAssert.That(
                async () => await client.WriteAsync(
                    new LogicalTagValue(HoldingTagName, LowWord, TestFrameworkCompatibilityExtensions.UnixEpoch),
                    cancellation.Token))
            .Throws<OperationCanceledException>();
    }

    /// <summary>Exercises coalesced-read decode and transport failure result paths.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task LogicalClient_ReadManyReportsDecodeAndTransportFailuresAsync()
    {
        var transport = new RecordingTransport();
        using var master = new ModbusMaster(transport);
        using var client = CreateLogicalClient(master);
        _ = client.CreateTag(new ModbusTagConfiguration(
            WideTagName,
            UnitId,
            ModbusDataArea.HoldingRegister,
            1,
            PointCount,
            typeof(uint)));

        var decoded = await client.ReadManyAsync([HoldingTagName, WideTagName], CancellationToken.None);
        transport.ThrowOnRead = true;
        var transportFailures = await client.ReadManyAsync(
            [HoldingTagName, CoilTagName],
            CancellationToken.None);

        await NativeAssert.That(decoded.All(static result => !result.Succeeded)).IsTrue();
        await NativeAssert.That(transportFailures.All(static result => !result.Succeeded)).IsTrue();
    }

    /// <summary>Exercises async-enumerable and observable failure, cancellation, and disposal paths.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task LogicalClient_ObservationAdaptersReportErrorsAndCancelAsync()
    {
        using var master = new ModbusMaster(new RecordingTransport());
        using var client = CreateLogicalClient(master);
        _ = client.CreateTag(new ModbusTagConfiguration(
            FastTagName,
            UnitId,
            ModbusDataArea.HoldingRegister,
            0,
            1,
            typeof(ushort))
        {
            ScanInterval = TimeSpan.FromTicks(1),
        });

        await using var missing = client.ObserveAsync(MissingTagName).GetAsyncEnumerator();
        await NativeAssert.That(async () => await missing.MoveNextAsync())
            .Throws<InvalidOperationException>();
        await using var missingMany = client.ObserveManyAsync([MissingTagName]).GetAsyncEnumerator();
        await NativeAssert.That(async () => await missingMany.MoveNextAsync())
            .Throws<InvalidOperationException>();
        await NativeAssert.That(() => client.Observe(HoldingTagName).Subscribe(null!))
            .Throws<ArgumentNullException>();

        var error = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var failing = client.Observe(MissingTagName)
            .Subscribe(_ => { }, exception => error.TrySetResult(exception));
        await NativeAssert.That(await error.Task.WaitAsync(TimeSpan.FromSeconds(1)))
            .IsTypeOf<InvalidOperationException>();

        var subscription = client.Observe(HoldingTagName).Subscribe(_ => { });
        subscription.Dispose();
        subscription.Dispose();

        await using var values = client.ObserveManyAsync([HoldingTagName, FastTagName]).GetAsyncEnumerator();
        await NativeAssert.That(await values.MoveNextAsync()).IsTrue();
        await NativeAssert.That(await values.MoveNextAsync()).IsTrue();
        await NativeAssert.That(await values.MoveNextAsync()).IsTrue();
    }

    /// <summary>Creates a logical client with tags spanning every supported raw data area.</summary>
    /// <param name="master">The in-memory master.</param>
    /// <returns>The configured logical client.</returns>
    private static ModbusLogicalTagClient CreateLogicalClient(IModbusMaster master)
    {
        var client = new ModbusLogicalTagClient(master, null, TimeSpan.FromMilliseconds(1), new FixedTimeProvider());
        _ = client.CreateTag(
            new ModbusTagConfiguration(HoldingTagName, UnitId, ModbusDataArea.HoldingRegister, 0, 1, typeof(ushort)));
        _ = client.CreateTag(
            new ModbusTagConfiguration(CoilTagName, UnitId, ModbusDataArea.Coil, 0, 1, typeof(bool)));
        _ = client.CreateTag(
            new ModbusTagConfiguration(
                "HoldingArray",
                UnitId,
                ModbusDataArea.HoldingRegister,
                0,
                PointCount,
                typeof(ushort[])));
        _ = client.CreateTag(
            new ModbusTagConfiguration(InputTagName, UnitId, ModbusDataArea.InputRegister, 0, 1, typeof(ushort))
            {
                AccessMode = LogicalTagAccessMode.Read,
            });
        _ = client.CreateTag(
            new ModbusTagConfiguration(DiscreteTagName, UnitId, ModbusDataArea.DiscreteInput, 0, 1, typeof(bool))
            {
                AccessMode = LogicalTagAccessMode.Read,
            });
        _ = client.CreateTag(
            new ModbusTagConfiguration(
                WriteOnlyTagName,
                UnitId,
                ModbusDataArea.HoldingRegister,
                0,
                1,
                typeof(ushort))
            {
                AccessMode = LogicalTagAccessMode.Write,
            });
        return client;
    }

    /// <summary>Returns deterministic protocol responses without I/O.</summary>
    private sealed class RecordingTransport : ModbusTransport
    {
        /// <summary>Gets the most recent request.</summary>
        internal IModbusMessage? LastRequest { get; private set; }

        /// <summary>Gets or sets a value indicating whether reads fail.</summary>
        internal bool ThrowOnRead { get; set; }

        /// <inheritdoc />
        internal override T UnicastMessage<T>(IModbusMessage message)
        {
            LastRequest = message;
            if (ThrowOnRead)
            {
                throw new IOException("simulated");
            }

            if (typeof(T) == typeof(ReadCoilsInputsResponse))
            {
                var request = (ReadCoilsInputsRequest)message;
                return (T)(IModbusMessage)new ReadCoilsInputsResponse(
                    request.FunctionCode,
                    request.SlaveAddress,
                    1,
                    new DiscreteCollection(true, false));
            }

            return typeof(T) == typeof(ReadHoldingInputRegistersResponse)
                ? (T)(IModbusMessage)new ReadHoldingInputRegistersResponse(
                    message.FunctionCode,
                    message.SlaveAddress,
                    new RegisterCollection(LowWord, HighWord))
                : CreateWriteResponse<T>(message);
        }

        /// <inheritdoc />
        internal override T UnicastMessage<T>(IModbusMessage message, Func<T> responseFactory)
        {
            LastRequest = message;
            return responseFactory();
        }

        /// <inheritdoc />
        internal override void OnValidateResponse(IModbusMessage request, IModbusMessage response)
        {
        }

        /// <inheritdoc />
        internal override Task<byte[]> ReadRequestAsync() => Task.FromResult<byte[]>([]);

        /// <inheritdoc />
        internal override Task<IModbusMessage> ReadResponseAsync<T>(Func<T> responseFactory) =>
            Task.FromResult<IModbusMessage>(responseFactory());

        /// <inheritdoc />
        internal override byte[] BuildMessageFrame(IModbusMessage message) => message.MessageFrame;

        /// <inheritdoc />
        internal override void Write(IModbusMessage message) => LastRequest = message;

        /// <summary>Creates the acknowledgement for a write request.</summary>
        /// <typeparam name="T">The response type.</typeparam>
        /// <param name="message">The request message.</param>
        /// <returns>The response.</returns>
        private static T CreateWriteResponse<T>(IModbusMessage message)
            where T : IModbusMessage, new()
        {
            if (message is WriteSingleCoilRequestResponse or WriteSingleRegisterRequestResponse)
            {
                return (T)message;
            }

            if (message is WriteMultipleCoilsRequest coils)
            {
                return (T)(IModbusMessage)new WriteMultipleCoilsResponse(
                    coils.SlaveAddress,
                    coils.StartAddress,
                    coils.NumberOfPoints);
            }

            var registers = (WriteMultipleRegistersRequest)message;
            return (T)(IModbusMessage)new WriteMultipleRegistersResponse(
                registers.SlaveAddress,
                registers.StartAddress,
                registers.NumberOfPoints);
        }
    }

    /// <summary>Provides stable logical-value timestamps.</summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => TestFrameworkCompatibilityExtensions.UnixEpoch;
    }
}
