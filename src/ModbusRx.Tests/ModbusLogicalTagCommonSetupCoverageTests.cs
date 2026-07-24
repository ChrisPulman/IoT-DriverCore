// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.ModbusRx.IO;
using IoT.DriverCore.ModbusRx.LogicalTags;
using NativeAssert = TUnit.Assertions.Assert;

namespace IoT.DriverCore.ModbusRx.UnitTests;

/// <summary>Exercises the common logical-tag setup contracts through the Modbus adapter.</summary>
public sealed class ModbusLogicalTagCommonSetupCoverageTests
{
    /// <summary>Exercises catalog, CSV, and SQLite-backed common setup contracts without a device.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task CommonSetupContracts_PersistCatalogAndDefinitionsAsync()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"modbusrx-common-{Guid.NewGuid():N}.db");
        try
        {
            using var master = new NoopMaster();
            using var client = new ModbusLogicalTagClient(master, null, TimeSpan.FromMilliseconds(1));
            var registry = (ILogicalTagRegistry)client;
            var exchange = (ILogicalTagDefinitionExchange)client;
            var persistence = (ILogicalTagPersistence)client;
            var groupPersistence = (ILogicalTagGroupPersistence)client;
            var tag = new ModbusLogicalTag(new ModbusTagConfiguration(
                "Common.Tag",
                1,
                ModbusDataArea.HoldingRegister,
                0,
                1,
                typeof(ushort))).ToLogicalTag();

            registry.RegisterTag(tag);
            await NativeAssert.That(registry.Catalog.List().Single().Name).IsEqualTo(tag.Name);

            using var csv = new StringWriter();
            await exchange.ExportCsvAsync(csv, ',', CancellationToken.None);
            await exchange.ImportCsvAsync(new StringReader(csv.ToString()), ',', CancellationToken.None);

            await client.InitializeStoreAsync($"Data Source={databasePath};Pooling=False", CancellationToken.None);
            await persistence.InitializeStoreAsync(CancellationToken.None);
            await persistence.UpsertTagAsync(tag, CancellationToken.None);
            await NativeAssert.That(await persistence.GetTagAsync(tag.Name, CancellationToken.None)).IsNotNull();
            await NativeAssert.That((await persistence.ListTagsAsync(CancellationToken.None)).Count).IsEqualTo(1);
            await NativeAssert.That(await persistence.EditTagAsync(tag, CancellationToken.None)).IsTrue();
            await NativeAssert.That((await persistence.LoadTagsAsync(CancellationToken.None)).Count).IsEqualTo(1);
            await NativeAssert.That(await persistence.DeleteTagAsync(tag.Name, CancellationToken.None)).IsTrue();

            var group = new LogicalTagGroup("Common", "Shared setup coverage");
            await groupPersistence.UpsertGroupAsync(group, CancellationToken.None);
            await NativeAssert.That(await groupPersistence.GetGroupAsync(group.Name, CancellationToken.None)).IsNotNull();
            await NativeAssert.That((await groupPersistence.ListGroupsAsync(CancellationToken.None)).Count).IsEqualTo(1);
            await NativeAssert.That(await groupPersistence.DeleteGroupAsync(group.Name, CancellationToken.None)).IsTrue();
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    /// <summary>Supplies the inactive transport required by setup-only logical-tag tests.</summary>
    private sealed class NoopMaster : IModbusMaster
    {
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
        public Task<ushort[]> ReadHoldingRegistersAsync(byte slaveAddress, ushort startAddress, ushort numberOfPoints) =>
            Task.FromResult(new ushort[numberOfPoints]);

        /// <inheritdoc/>
        public Task<ushort[]> ReadInputRegistersAsync(byte slaveAddress, ushort startAddress, ushort numberOfPoints) =>
            ReadHoldingRegistersAsync(slaveAddress, startAddress, numberOfPoints);

        /// <inheritdoc/>
        public Task WriteSingleCoilAsync(byte slaveAddress, ushort coilAddress, bool value) => Task.CompletedTask;

        /// <inheritdoc/>
        public Task WriteSingleRegisterAsync(byte slaveAddress, ushort registerAddress, ushort value) => Task.CompletedTask;

        /// <inheritdoc/>
        public Task WriteMultipleRegistersAsync(byte slaveAddress, ushort startAddress, ushort[] data) => Task.CompletedTask;

        /// <inheritdoc/>
        public Task WriteMultipleCoilsAsync(byte slaveAddress, ushort startAddress, bool[] data) => Task.CompletedTask;

        /// <inheritdoc/>
        public Task<ushort[]> ReadWriteMultipleRegistersAsync(
            byte slaveAddress,
            ushort startReadAddress,
            ushort numberOfPointsToRead,
            ushort startWriteAddress,
            ushort[] writeData) => Task.FromResult(new ushort[numberOfPointsToRead]);

        /// <inheritdoc/>
        public void Dispose() => IsDisposed = true;
    }
}
