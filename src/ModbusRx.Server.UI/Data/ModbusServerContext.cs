// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;

namespace IoT.DriverCore.ModbusRx.Server.UI.Data;

/// <summary>Entity Framework database context for ModbusRx Server configuration.</summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ModbusServerContext"/> class.
/// </remarks>
/// <param name="options">The database context options.</param>
public class ModbusServerContext(DbContextOptions<ModbusServerContext> options) : DbContext(options)
{
    /// <summary>The maximum length of a display name.</summary>
    private const int NameMaximumLength = 100;

    /// <summary>The maximum length of a connection type.</summary>
    private const int ConnectionTypeMaximumLength = 10;

    /// <summary>The maximum length of a client address.</summary>
    private const int AddressMaximumLength = 50;

    /// <summary>The maximum length of a simulation type.</summary>
    private const int SimulationTypeMaximumLength = 20;

    /// <summary>The default Modbus TCP port.</summary>
    private const int DefaultTcpPort = 502;

    /// <summary>The default Modbus UDP port.</summary>
    private const int DefaultUdpPort = 503;

    /// <summary>The default simulation interval in milliseconds.</summary>
    private const int DefaultSimulationIntervalMilliseconds = 1000;

    /// <summary>Gets or sets the Modbus client configurations.</summary>
    public DbSet<ModbusClientConfiguration> ClientConfigurations { get; set; } = null!;

    /// <summary>Gets or sets the server configurations.</summary>
    public DbSet<ServerConfiguration> ServerConfigurations { get; set; } = null!;

    /// <summary>Configures the model that was discovered by convention from the entity types.</summary>
    /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (modelBuilder is null)
        {
            throw new ArgumentNullException(nameof(modelBuilder));
        }

        base.OnModelCreating(modelBuilder);

        // Configure ModbusClientConfiguration
        _ = modelBuilder.Entity<ModbusClientConfiguration>(entity =>
        {
            _ = entity.HasKey(e => e.Id);
            _ = entity.Property(e => e.Name).IsRequired().HasMaxLength(NameMaximumLength);
            _ = entity.Property(e => e.ConnectionType).IsRequired().HasMaxLength(ConnectionTypeMaximumLength);
            _ = entity.Property(e => e.Address).IsRequired().HasMaxLength(AddressMaximumLength);
            _ = entity.HasIndex(e => e.Name);
        });

        // Configure ServerConfiguration
        _ = modelBuilder.Entity<ServerConfiguration>(entity =>
        {
            _ = entity.HasKey(e => e.Id);
            _ = entity.Property(e => e.Name).IsRequired().HasMaxLength(NameMaximumLength);
            _ = entity.Property(e => e.SimulationType).HasMaxLength(SimulationTypeMaximumLength);
        });

        // Seed default server configuration
        _ = modelBuilder.Entity<ServerConfiguration>().HasData(
            new ServerConfiguration
            {
                Id = 1,
                Name = "Default Server",
                TcpPort = DefaultTcpPort,
                UdpPort = DefaultUdpPort,
                UnitId = 1,
                SimulationEnabled = false,
                SimulationType = "Random",
                SimulationIntervalMs = DefaultSimulationIntervalMilliseconds,
                AutoStart = false,
                CreatedAt = DateTimeOffset.UnixEpoch,
                ModifiedAt = DateTimeOffset.UnixEpoch,
            });
    }
}
