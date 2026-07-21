// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CP.IoT.Core;
using ModbusRx.Device;
using ModbusRx.IO;
using ModbusRx.LogicalTags;

namespace ModbusRx.UnitTests;

/// <summary>Direct TUnit tests for the logical-tag Modbus adapter.</summary>
public sealed class ModbusLogicalTagClientTests
{
    /// <summary>The logical name used for metadata conversion.</summary>
    private const string LogicalName = "Line.Speed";

    /// <summary>The logical name used for persistence.</summary>
    private const string StoredTagName = "Stored";

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

    /// <summary>The write target address.</summary>
    private const ushort WriteAddress = 20;

    /// <summary>The value written by the typed API test.</summary>
    private const ushort WriteValue = 999;

    /// <summary>The persisted coil address.</summary>
    private const ushort StoredAddress = 5;

    /// <summary>The scan interval used for metadata conversion.</summary>
    private const int ScanIntervalMilliseconds = 250;

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
            new ModbusTagConfiguration("First", 1, ModbusDataArea.HoldingRegister, FirstAddress, 1, typeof(ushort)));
        _ = client.CreateTag(
            new ModbusTagConfiguration("Second", 1, ModbusDataArea.HoldingRegister, SecondAddress, 1, typeof(ushort)));

        var results = await client.ReadManyAsync(["First", "Second"]);

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
        public Task WriteSingleCoilAsync(byte slaveAddress, ushort coilAddress, bool value) => Task.CompletedTask;

        /// <inheritdoc/>
        public Task WriteSingleRegisterAsync(byte slaveAddress, ushort registerAddress, ushort value)
        {
            LastSingleRegisterWrite = (slaveAddress, registerAddress, value);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task WriteMultipleRegistersAsync(
            byte slaveAddress,
            ushort startAddress,
            ushort[] data) => Task.CompletedTask;

        /// <inheritdoc/>
        public Task WriteMultipleCoilsAsync(byte slaveAddress, ushort startAddress, bool[] data) => Task.CompletedTask;

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
