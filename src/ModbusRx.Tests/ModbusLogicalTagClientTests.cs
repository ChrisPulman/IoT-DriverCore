// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IoT.DriverCore.Core;
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.ModbusRx.IO;
using IoT.DriverCore.ModbusRx.LogicalTags;

namespace IoT.DriverCore.ModbusRx.UnitTests;

/// <summary>Direct TUnit tests for the logical-tag Modbus adapter.</summary>
public sealed class ModbusLogicalTagClientTests
{
    /// <summary>The logical name used for metadata conversion.</summary>
    private const string LogicalName = "Line.Speed";

    /// <summary>The logical name used for persistence.</summary>
    private const string StoredTagName = "Stored";

    /// <summary>The first adjacent logical tag name.</summary>
    private const string FirstTagName = "First";

    /// <summary>The second adjacent logical tag name.</summary>
    private const string SecondTagName = "Second";

    /// <summary>The separated logical tag name.</summary>
    private const string SeparatedTagName = "Separated";

    /// <summary>The read-only logical tag name.</summary>
    private const string ReadOnlyTagName = "ReadOnly";

    /// <summary>The missing logical tag name.</summary>
    private const string MissingTagName = "Missing";

    /// <summary>The unit identifier used for metadata conversion.</summary>
    private const byte MetadataUnitId = 7;

    /// <summary>The unit identifier used for writes.</summary>
    private const byte WriteUnitId = 3;

    /// <summary>The unit identifier used for persistence.</summary>
    private const byte StoredUnitId = 2;

    /// <summary>The address used for metadata conversion.</summary>
    private const ushort MetadataAddress = 42;

    /// <summary>The point count used for metadata conversion.</summary>
    private const ushort MetadataCount = 2;

    /// <summary>The first adjacent register address.</summary>
    private const ushort FirstAddress = 10;

    /// <summary>The second adjacent register address.</summary>
    private const ushort SecondAddress = 11;

    /// <summary>The first adjacent register value.</summary>
    private const ushort FirstValue = 123;

    /// <summary>The second adjacent register value.</summary>
    private const ushort SecondValue = 456;

    /// <summary>The value written by the later duplicate request.</summary>
    private const ushort DuplicateValue = 789;

    /// <summary>The write target address.</summary>
    private const ushort WriteAddress = 20;

    /// <summary>The address used for a separated successful write.</summary>
    private const ushort SeparatedWriteAddress = 30;

    /// <summary>The first adjacent coil address.</summary>
    private const ushort FirstCoilAddress = 40;

    /// <summary>The second adjacent coil address.</summary>
    private const ushort SecondCoilAddress = 41;

    /// <summary>The value written by the typed API test.</summary>
    private const ushort WriteValue = 999;

    /// <summary>The persisted coil address.</summary>
    private const ushort StoredAddress = 5;

    /// <summary>The scan interval used for metadata conversion.</summary>
    private const int ScanIntervalMilliseconds = 250;

    /// <summary>The number of results expected when a duplicate write is retained.</summary>
    private const int DuplicateResultCount = 3;

    /// <summary>The number of independent valid writes surrounding an invalid value.</summary>
    private const int IndependentWriteCount = 2;

    /// <summary>Verifies common catalog conversion preserves all Modbus metadata.</summary>
    /// <returns>A task representing the test.</returns>
    [TUnit.Core.Test]
    public async Task CatalogRoundTripPreservesProtocolMetadataAsync()
    {
        var source = new ModbusLogicalTag(new ModbusTagConfiguration(
            LogicalName,
            MetadataUnitId,
            ModbusDataArea.HoldingRegister,
            MetadataAddress,
            MetadataCount,
            typeof(float))
        {
            ByteOrder = ModbusByteOrder.BigEndianWordSwap,
            AccessMode = LogicalTagAccessMode.ReadWrite,
            ScanInterval = TimeSpan.FromMilliseconds(ScanIntervalMilliseconds),
        });

        var roundTrip = ModbusLogicalTag.FromLogicalTag(source.ToLogicalTag());

        await TUnit.Assertions.Assert.That(roundTrip.Name).IsEqualTo(LogicalName);
        await TUnit.Assertions.Assert.That(roundTrip.UnitId).IsEqualTo(MetadataUnitId);
        await TUnit.Assertions.Assert.That(roundTrip.Address).IsEqualTo(MetadataAddress);
        await TUnit.Assertions.Assert.That(roundTrip.Count).IsEqualTo(MetadataCount);
        await TUnit.Assertions.Assert.That(roundTrip.ClrDataType).IsEqualTo(typeof(float));
        await TUnit.Assertions.Assert.That(roundTrip.ByteOrder).IsEqualTo(ModbusByteOrder.BigEndianWordSwap);
    }

    /// <summary>Verifies adjacent logical reads share one raw Modbus request.</summary>
    /// <returns>A task representing the test.</returns>
    [TUnit.Core.Test]
    public async Task ReadManyCoalescesAdjacentRegistersAsync()
    {
        using var master = new RecordingMaster();
        master.HoldingRegisters[FirstAddress] = FirstValue;
        master.HoldingRegisters[SecondAddress] = SecondValue;
        using var catalog = new ModbusTagCatalog();
        using var client = new ModbusLogicalTagClient(master, catalog, TimeSpan.FromSeconds(1));
        _ = client.CreateTag(
            new ModbusTagConfiguration(
                FirstTagName,
                1,
                ModbusDataArea.HoldingRegister,
                FirstAddress,
                1,
                typeof(ushort)));
        _ = client.CreateTag(
            new ModbusTagConfiguration(
                SecondTagName,
                1,
                ModbusDataArea.HoldingRegister,
                SecondAddress,
                1,
                typeof(ushort)));

        var results = await client.ReadManyAsync([FirstTagName, SecondTagName]);

        await TUnit.Assertions.Assert.That(master.HoldingReadCount).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(master.LastHoldingRead).IsEqualTo(
            (UnitId: (byte)1, Address: FirstAddress, Count: MetadataCount));
        await TUnit.Assertions.Assert.That((ushort)results[0].Value!.Value!).IsEqualTo(FirstValue);
        await TUnit.Assertions.Assert.That((ushort)results[1].Value!.Value!).IsEqualTo(SecondValue);
    }

    /// <summary>Verifies typed logical writes preserve the raw master API.</summary>
    /// <returns>A task representing the test.</returns>
    [TUnit.Core.Test]
    public async Task WriteByNameUsesRawRegisterWriteAsync()
    {
        using var master = new RecordingMaster();
        using var catalog = new ModbusTagCatalog();
        using var client = new ModbusLogicalTagClient(master, catalog, TimeSpan.FromSeconds(1));
        _ = client.CreateTag(
            new ModbusTagConfiguration(
                "Setpoint",
                WriteUnitId,
                ModbusDataArea.HoldingRegister,
                WriteAddress,
                1,
                typeof(ushort)));

        var result = await client.WriteAsync(
            new LogicalTagValue("Setpoint", WriteValue, new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)));

        await TUnit.Assertions.Assert.That(result.Succeeded).IsTrue();
        await TUnit.Assertions.Assert.That(master.LastSingleRegisterWrite).IsEqualTo(
            (UnitId: WriteUnitId, Address: WriteAddress, Value: WriteValue));
    }

    /// <summary>
    /// Verifies adjacent register writes use one native multiple-write request while results and
    /// overlapping duplicates retain caller order.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [TUnit.Core.Test]
    public async Task WriteManyCoalescesRegistersAndPreservesDuplicatesAsync()
    {
        using var master = new RecordingMaster();
        using var catalog = new ModbusTagCatalog();
        using var client = new ModbusLogicalTagClient(master, catalog, TimeSpan.FromSeconds(1));
        _ = client.CreateTag(
            new ModbusTagConfiguration(
                FirstTagName,
                WriteUnitId,
                ModbusDataArea.HoldingRegister,
                FirstAddress,
                1,
                typeof(ushort)));
        _ = client.CreateTag(
            new ModbusTagConfiguration(
                SecondTagName,
                WriteUnitId,
                ModbusDataArea.HoldingRegister,
                SecondAddress,
                1,
                typeof(ushort)));
        var requestedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var results = await client.WriteManyAsync(
        [
            new LogicalTagValue(SecondTagName, SecondValue, requestedAt),
            new LogicalTagValue(FirstTagName, FirstValue, requestedAt),
            new LogicalTagValue(FirstTagName, DuplicateValue, requestedAt),
        ]);

        await TUnit.Assertions.Assert.That(results.Count).IsEqualTo(DuplicateResultCount);
        await TUnit.Assertions.Assert.That(results.All(static result => result.Succeeded)).IsTrue();
        await TUnit.Assertions.Assert.That(results[0].Value!.TagName).IsEqualTo(SecondTagName);
        await TUnit.Assertions.Assert.That(results[1].Value!.TagName).IsEqualTo(FirstTagName);
        await TUnit.Assertions.Assert.That(results[2].Value!.TagName).IsEqualTo(FirstTagName);
        await TUnit.Assertions.Assert.That((ushort)results[0].Value!.Value!).IsEqualTo(SecondValue);
        await TUnit.Assertions.Assert.That((ushort)results[1].Value!.Value!).IsEqualTo(FirstValue);
        await TUnit.Assertions.Assert.That((ushort)results[2].Value!.Value!).IsEqualTo(DuplicateValue);
        await TUnit.Assertions.Assert.That(master.MultipleRegisterWrites.Count).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(master.MultipleRegisterWrites[0].Address).IsEqualTo(FirstAddress);
        await TUnit.Assertions.Assert.That(master.MultipleRegisterWrites[0].Data[0]).IsEqualTo(FirstValue);
        await TUnit.Assertions.Assert.That(master.MultipleRegisterWrites[0].Data[1]).IsEqualTo(SecondValue);
        await TUnit.Assertions.Assert.That(master.SingleRegisterWrites.Count).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(master.SingleRegisterWrites[0])
            .IsEqualTo((UnitId: WriteUnitId, Address: FirstAddress, Value: DuplicateValue));
        await TUnit.Assertions.Assert.That(master.HoldingRegisters[FirstAddress]).IsEqualTo(DuplicateValue);
        await TUnit.Assertions.Assert.That(master.HoldingRegisters[SecondAddress]).IsEqualTo(SecondValue);
    }

    /// <summary>Verifies adjacent coils use the native multiple-coil operation.</summary>
    /// <returns>A task representing the test.</returns>
    [TUnit.Core.Test]
    public async Task WriteManyCoalescesAdjacentCoilsAsync()
    {
        using var master = new RecordingMaster();
        using var catalog = new ModbusTagCatalog();
        using var client = new ModbusLogicalTagClient(master, catalog, TimeSpan.FromSeconds(1));
        _ = client.CreateTag(
            new ModbusTagConfiguration(
                "FirstCoil",
                WriteUnitId,
                ModbusDataArea.Coil,
                FirstCoilAddress,
                1,
                typeof(bool)));
        _ = client.CreateTag(
            new ModbusTagConfiguration(
                "SecondCoil",
                WriteUnitId,
                ModbusDataArea.Coil,
                SecondCoilAddress,
                1,
                typeof(bool)));
        var requestedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var results = await client.WriteManyAsync(
        [
            new LogicalTagValue("FirstCoil", true, requestedAt),
            new LogicalTagValue("SecondCoil", false, requestedAt),
        ]);

        await TUnit.Assertions.Assert.That(results.All(static result => result.Succeeded)).IsTrue();
        await TUnit.Assertions.Assert.That(master.MultipleCoilWrites.Count).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(master.MultipleCoilWrites[0].Address).IsEqualTo(FirstCoilAddress);
        await TUnit.Assertions.Assert.That(master.MultipleCoilWrites[0].Data[0]).IsTrue();
        await TUnit.Assertions.Assert.That(master.MultipleCoilWrites[0].Data[1]).IsFalse();
        await TUnit.Assertions.Assert.That(master.SingleCoilWriteCount).IsEqualTo(0);
    }

    /// <summary>Verifies one invalid value does not prevent independent valid writes.</summary>
    /// <returns>A task representing the test.</returns>
    [TUnit.Core.Test]
    public async Task WriteManyReportsEncodingFailurePerItemAsync()
    {
        using var master = new RecordingMaster();
        using var catalog = new ModbusTagCatalog();
        using var client = new ModbusLogicalTagClient(master, catalog, TimeSpan.FromSeconds(1));
        _ = client.CreateTag(
            new ModbusTagConfiguration(
                FirstTagName,
                WriteUnitId,
                ModbusDataArea.HoldingRegister,
                FirstAddress,
                1,
                typeof(ushort)));
        _ = client.CreateTag(
            new ModbusTagConfiguration(
                "Invalid",
                WriteUnitId,
                ModbusDataArea.HoldingRegister,
                SecondAddress,
                1,
                typeof(ushort)));
        _ = client.CreateTag(
            new ModbusTagConfiguration(
                SeparatedTagName,
                WriteUnitId,
                ModbusDataArea.HoldingRegister,
                SeparatedWriteAddress,
                1,
                typeof(ushort)));
        var requestedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var results = await client.WriteManyAsync(
        [
            new LogicalTagValue(FirstTagName, FirstValue, requestedAt),
            new LogicalTagValue("Invalid", "not-a-register", requestedAt),
            new LogicalTagValue(SeparatedTagName, SecondValue, requestedAt),
        ]);

        await TUnit.Assertions.Assert.That(results[0].Succeeded).IsTrue();
        await TUnit.Assertions.Assert.That(results[1].Succeeded).IsFalse();
        await TUnit.Assertions.Assert.That(results[1].Error).Contains("requires a value of CLR type");
        await TUnit.Assertions.Assert.That(results[2].Succeeded).IsTrue();
        await TUnit.Assertions.Assert.That(master.SingleRegisterWrites.Count).IsEqualTo(IndependentWriteCount);
        await TUnit.Assertions.Assert.That(master.HoldingRegisters[FirstAddress]).IsEqualTo(FirstValue);
        await TUnit.Assertions.Assert.That(master.HoldingRegisters[SeparatedWriteAddress]).IsEqualTo(SecondValue);
    }

    /// <summary>Verifies missing and read-only tags each produce an indexed failure without transport access.</summary>
    /// <returns>A task representing the test.</returns>
    [TUnit.Core.Test]
    public async Task WriteManyReportsLookupAndAccessFailuresPerItemAsync()
    {
        using var master = new RecordingMaster();
        using var catalog = new ModbusTagCatalog();
        using var client = new ModbusLogicalTagClient(master, catalog, TimeSpan.FromSeconds(1));
        _ = client.CreateTag(
            new ModbusTagConfiguration(
                ReadOnlyTagName,
                WriteUnitId,
                ModbusDataArea.HoldingRegister,
                FirstAddress,
                1,
                typeof(ushort))
            {
                AccessMode = LogicalTagAccessMode.Read,
            });
        var requestedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var results = await client.WriteManyAsync(
        [
            new LogicalTagValue(MissingTagName, FirstValue, requestedAt),
            new LogicalTagValue(ReadOnlyTagName, SecondValue, requestedAt),
        ]);

        await TUnit.Assertions.Assert.That(results[0].Succeeded).IsFalse();
        await TUnit.Assertions.Assert.That(results[0].Error).Contains("is not registered");
        await TUnit.Assertions.Assert.That(results[1].Succeeded).IsFalse();
        await TUnit.Assertions.Assert.That(results[1].Error).Contains("is read-only");
        await TUnit.Assertions.Assert.That(master.SingleRegisterWrites.Count).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(master.MultipleRegisterWrites.Count).IsEqualTo(0);
    }

    /// <summary>Verifies a failed coalesced range fails only its members and later ranges continue.</summary>
    /// <returns>A task representing the test.</returns>
    [TUnit.Core.Test]
    public async Task WriteManyReportsTransportFailurePerRangeAsync()
    {
        using var master = new RecordingMaster
        {
            FailingMultipleRegisterAddress = FirstAddress,
        };
        using var catalog = new ModbusTagCatalog();
        using var client = new ModbusLogicalTagClient(master, catalog, TimeSpan.FromSeconds(1));
        _ = client.CreateTag(
            new ModbusTagConfiguration(
                FirstTagName,
                WriteUnitId,
                ModbusDataArea.HoldingRegister,
                FirstAddress,
                1,
                typeof(ushort)));
        _ = client.CreateTag(
            new ModbusTagConfiguration(
                SecondTagName,
                WriteUnitId,
                ModbusDataArea.HoldingRegister,
                SecondAddress,
                1,
                typeof(ushort)));
        _ = client.CreateTag(
            new ModbusTagConfiguration(
                SeparatedTagName,
                WriteUnitId,
                ModbusDataArea.HoldingRegister,
                SeparatedWriteAddress,
                1,
                typeof(ushort)));
        var requestedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var results = await client.WriteManyAsync(
        [
            new LogicalTagValue(FirstTagName, FirstValue, requestedAt),
            new LogicalTagValue(SecondTagName, SecondValue, requestedAt),
            new LogicalTagValue(SeparatedTagName, WriteValue, requestedAt),
        ]);

        await TUnit.Assertions.Assert.That(results[0].Succeeded).IsFalse();
        await TUnit.Assertions.Assert.That(results[1].Succeeded).IsFalse();
        await TUnit.Assertions.Assert.That(results[0].Error).Contains("Simulated multiple-register failure");
        await TUnit.Assertions.Assert.That(results[2].Succeeded).IsTrue();
        await TUnit.Assertions.Assert.That(master.SingleRegisterWrites.Count).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(master.HoldingRegisters[SeparatedWriteAddress]).IsEqualTo(WriteValue);
    }

    /// <summary>Verifies SQLite CRUD updates dynamically loaded catalogs.</summary>
    /// <returns>A task representing the test.</returns>
    [TUnit.Core.Test]
    public async Task SqliteCrudLoadsLiveCatalogAsync()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"modbusrx-{Guid.NewGuid():N}.db");
        try
        {
            using var master = new RecordingMaster();
            using var catalog = new ModbusTagCatalog();
            using var client = new ModbusLogicalTagClient(master, catalog, TimeSpan.FromSeconds(1));
            await client.InitializeStoreAsync(
                $"Data Source={databasePath};Pooling=False",
                CancellationToken.None);
            var tag = new ModbusLogicalTag(new ModbusTagConfiguration(
                StoredTagName,
                StoredUnitId,
                ModbusDataArea.Coil,
                StoredAddress,
                1,
                typeof(bool)));

            await client.UpsertStoredTagAsync(tag, CancellationToken.None);
            _ = client.RemoveTag(StoredTagName);
            var loaded = await client.LoadTagsAsync(CancellationToken.None);
            var deleted = await client.DeleteStoredTagAsync(StoredTagName, CancellationToken.None);

            await TUnit.Assertions.Assert.That(loaded).IsEqualTo(1);
            await TUnit.Assertions.Assert.That(deleted).IsTrue();
            await TUnit.Assertions.Assert.That(client.Catalog.TryGet(StoredTagName, out _)).IsFalse();
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>Records raw Modbus requests without changing the production master contract.</summary>
    private sealed class RecordingMaster : IModbusMaster
    {
        /// <summary>Gets the mutable holding-register image.</summary>
        public Dictionary<ushort, ushort> HoldingRegisters { get; } = [];

        /// <summary>Gets the native multiple-register writes.</summary>
        public List<(byte UnitId, ushort Address, ushort[] Data)> MultipleRegisterWrites { get; } = [];

        /// <summary>Gets the native multiple-coil writes.</summary>
        public List<(byte UnitId, ushort Address, bool[] Data)> MultipleCoilWrites { get; } = [];

        /// <summary>Gets the single-register writes.</summary>
        public List<(byte UnitId, ushort Address, ushort Value)> SingleRegisterWrites { get; } = [];

        /// <summary>Gets the number of single-coil writes.</summary>
        public int SingleCoilWriteCount { get; private set; }

        /// <summary>Gets or sets the address at which multiple-register writes fail.</summary>
        public ushort? FailingMultipleRegisterAddress { get; init; }

        /// <summary>Gets the number of holding-register reads.</summary>
        public int HoldingReadCount { get; private set; }

        /// <summary>Gets the last holding-register read.</summary>
        public (byte UnitId, ushort Address, ushort Count) LastHoldingRead { get; private set; }

        /// <summary>Gets the last single-register write.</summary>
        public (byte UnitId, ushort Address, ushort Value) LastSingleRegisterWrite { get; private set; }

        /// <inheritdoc/>
        public ModbusTransport? Transport => null;

        /// <inheritdoc/>
        public bool IsDisposed { get; private set; }

        /// <inheritdoc/>
        public Task<bool[]> ReadCoilsAsync(byte slaveAddress, ushort startAddress, ushort numberOfPoints) =>
            Task.FromResult(new bool[numberOfPoints]);

        /// <inheritdoc/>
        public Task<bool[]> ReadInputsAsync(byte slaveAddress, ushort startAddress, ushort numberOfPoints) =>
            ReadCoilsAsync(slaveAddress, startAddress, numberOfPoints);

        /// <inheritdoc/>
        public Task<ushort[]> ReadHoldingRegistersAsync(byte slaveAddress, ushort startAddress, ushort numberOfPoints)
        {
            HoldingReadCount++;
            LastHoldingRead = (slaveAddress, startAddress, numberOfPoints);
            return Task.FromResult(
                Enumerable.Range(startAddress, numberOfPoints)
                    .Select(address =>
                        HoldingRegisters.TryGetValue((ushort)address, out var value) ? value : (ushort)0)
                    .ToArray());
        }

        /// <inheritdoc/>
        public Task<ushort[]> ReadInputRegistersAsync(byte slaveAddress, ushort startAddress, ushort numberOfPoints) =>
            Task.FromResult(new ushort[numberOfPoints]);

        /// <inheritdoc/>
        public Task WriteSingleCoilAsync(byte slaveAddress, ushort coilAddress, bool value)
        {
            SingleCoilWriteCount++;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task WriteSingleRegisterAsync(byte slaveAddress, ushort registerAddress, ushort value)
        {
            LastSingleRegisterWrite = (slaveAddress, registerAddress, value);
            SingleRegisterWrites.Add(LastSingleRegisterWrite);
            HoldingRegisters[registerAddress] = value;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task WriteMultipleRegistersAsync(
            byte slaveAddress,
            ushort startAddress,
            ushort[] data)
        {
            MultipleRegisterWrites.Add((slaveAddress, startAddress, data.ToArray()));
            if (FailingMultipleRegisterAddress == startAddress)
            {
                throw new InvalidOperationException("Simulated multiple-register failure.");
            }

            for (var index = 0; index < data.Length; index++)
            {
                HoldingRegisters[checked((ushort)(startAddress + index))] = data[index];
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task WriteMultipleCoilsAsync(byte slaveAddress, ushort startAddress, bool[] data)
        {
            MultipleCoilWrites.Add((slaveAddress, startAddress, data.ToArray()));
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<ushort[]> ReadWriteMultipleRegistersAsync(
            byte slaveAddress,
            ushort startReadAddress,
            ushort numberOfPointsToRead,
            ushort startWriteAddress,
            ushort[] writeData) =>
            Task.FromResult(new ushort[numberOfPointsToRead]);

        /// <inheritdoc/>
        public void Dispose() => IsDisposed = true;
    }
}
