// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Data.Sqlite;

namespace CP.IoT.Core;

/// <summary>Persists logical tag definitions and groups in SQLite.</summary>
public sealed class LogicalTagSqliteStore
{
    /// <summary>DDL statements used to initialise the database schema.</summary>
    private const string CreateSchema = """
        CREATE TABLE IF NOT EXISTS logical_tag_groups (
            name TEXT NOT NULL PRIMARY KEY,
            description TEXT NOT NULL,
            metadata TEXT NOT NULL,
            created_utc TEXT NOT NULL,
            modified_utc TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS logical_tags (
            name TEXT NOT NULL PRIMARY KEY,
            address TEXT NOT NULL,
            data_type TEXT NOT NULL,
            group_name TEXT NOT NULL,
            description TEXT NOT NULL,
            metadata TEXT NOT NULL,
            access_mode TEXT NOT NULL DEFAULT 'ReadWrite',
            scan_interval_ms REAL NULL,
            created_utc TEXT NOT NULL,
            modified_utc TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_logical_tags_group_name ON logical_tags(group_name);
        """;

    /// <summary>Named SQL parameter for the tag or group name.</summary>
    private const string NameParameter = "$name";

    /// <summary>Ordinal of the <c>name</c> column in the logical_tags SELECT result.</summary>
    private const int TagNameColumn = 0;

    /// <summary>Ordinal of the <c>address</c> column in the logical_tags SELECT result.</summary>
    private const int TagAddressColumn = 1;

    /// <summary>Ordinal of the <c>data_type</c> column in the logical_tags SELECT result.</summary>
    private const int TagDataTypeColumn = 2;

    /// <summary>Ordinal of the <c>group_name</c> column in the logical_tags SELECT result.</summary>
    private const int TagGroupNameColumn = 3;

    /// <summary>Ordinal of the <c>description</c> column in the logical_tags SELECT result.</summary>
    private const int TagDescriptionColumn = 4;

    /// <summary>Ordinal of the <c>metadata</c> column in the logical_tags SELECT result.</summary>
    private const int TagMetadataColumn = 5;

    /// <summary>Ordinal of the <c>access_mode</c> column in the logical_tags SELECT result.</summary>
    private const int TagAccessModeColumn = 6;

    /// <summary>Ordinal of the <c>scan_interval_ms</c> column in the logical_tags SELECT result.</summary>
    private const int TagScanIntervalColumn = 7;

    /// <summary>Ordinal of the <c>name</c> column in the logical_tag_groups SELECT result.</summary>
    private const int GroupNameColumn = 0;

    /// <summary>Ordinal of the <c>description</c> column in the logical_tag_groups SELECT result.</summary>
    private const int GroupDescriptionColumn = 1;

    /// <summary>Ordinal of the <c>metadata</c> column in the logical_tag_groups SELECT result.</summary>
    private const int GroupMetadataColumn = 2;

    /// <summary>Ordinal of the <c>name</c> column in the PRAGMA table_info result.</summary>
    private const int PragmaColumnNameColumn = 1;

    /// <summary>SQLite connection string used to open all database connections.</summary>
    private readonly string _connectionString;

    /// <summary>Initializes a new instance of the <see cref="LogicalTagSqliteStore"/> class.</summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    public LogicalTagSqliteStore(string connectionString) =>
        _connectionString = LogicalTag.Required(connectionString, nameof(connectionString));

    /// <summary>Creates the database schema when it is absent.</summary>
    /// <returns>A task that represents the asynchronous initialisation operation.</returns>
    public Task InitializeAsync() => InitializeAsync(CancellationToken.None);

    /// <summary>Creates the database schema when it is absent.</summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that represents the asynchronous initialisation operation.</returns>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = CreateSchema;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await EnsureAccessModeColumnAsync(connection, cancellationToken).ConfigureAwait(false);
        await EnsureScanIntervalColumnAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Gets a tag by name, or <see langword="null"/> when it is absent.</summary>
    /// <param name="name">The tag name to look up.</param>
    /// <returns>The matching tag, or <see langword="null"/>.</returns>
    public Task<LogicalTag?> GetTagAsync(string name) => GetTagAsync(name, CancellationToken.None);

    /// <summary>Gets a tag by name, or <see langword="null"/> when it is absent.</summary>
    /// <param name="name">The tag name to look up.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching tag, or <see langword="null"/>.</returns>
    public async Task<LogicalTag?> GetTagAsync(string name, CancellationToken cancellationToken)
    {
        _ = LogicalTag.Required(name, nameof(name));

        using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT name,address,data_type,group_name,description,metadata,access_mode,scan_interval_ms FROM logical_tags WHERE name = $name;";
        _ = command.Parameters.AddWithValue(NameParameter, name);

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadTag(reader) : null;
    }

    /// <summary>Lists all tags in a stable name order.</summary>
    /// <returns>A read-only list of all persisted tags.</returns>
    public Task<IReadOnlyList<LogicalTag>> ListTagsAsync() => ListTagsAsync(CancellationToken.None);

    /// <summary>Lists all tags in a stable name order.</summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A read-only list of all persisted tags.</returns>
    public async Task<IReadOnlyList<LogicalTag>> ListTagsAsync(CancellationToken cancellationToken)
    {
        var tags = new List<LogicalTag>();

        using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT name,address,data_type,group_name,description,metadata,access_mode,scan_interval_ms FROM logical_tags ORDER BY name COLLATE BINARY;";

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            tags.Add(ReadTag(reader));
        }

        return tags;
    }

    /// <summary>Inserts or replaces a tag, retaining the original creation timestamp.</summary>
    /// <param name="tag">The tag to persist.</param>
    /// <returns>A task that represents the asynchronous upsert operation.</returns>
    public Task UpsertTagAsync(LogicalTag tag) => UpsertTagAsync(tag, CancellationToken.None);

    /// <summary>Inserts or replaces a tag, retaining the original creation timestamp.</summary>
    /// <param name="tag">The tag to persist.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that represents the asynchronous upsert operation.</returns>
    public async Task UpsertTagAsync(LogicalTag tag, CancellationToken cancellationToken)
    {
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        var now = DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture);

        using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO logical_tags(name,address,data_type,group_name,description,metadata,access_mode,scan_interval_ms,created_utc,modified_utc)
            VALUES($name,$address,$dataType,$groupName,$description,$metadata,$accessMode,$scanInterval,$now,$now)
            ON CONFLICT(name) DO UPDATE SET
             address = excluded.address, data_type = excluded.data_type, group_name = excluded.group_name,
             description = excluded.description, metadata = excluded.metadata, access_mode = excluded.access_mode,
             scan_interval_ms = excluded.scan_interval_ms, modified_utc = excluded.modified_utc;
            """;
        AddTagParameters(command, tag, now);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Updates an existing tag and returns whether a row was changed.</summary>
    /// <param name="tag">The tag to update.</param>
    /// <returns><see langword="true"/> when the tag was found and updated.</returns>
    public Task<bool> EditTagAsync(LogicalTag tag) => EditTagAsync(tag, CancellationToken.None);

    /// <summary>Updates an existing tag and returns whether a row was changed.</summary>
    /// <param name="tag">The tag to update.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the tag was found and updated.</returns>
    public async Task<bool> EditTagAsync(LogicalTag tag, CancellationToken cancellationToken)
    {
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        var now = DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture);

        using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE logical_tags SET address=$address, data_type=$dataType, group_name=$groupName,
            description=$description, metadata=$metadata, access_mode=$accessMode, scan_interval_ms=$scanInterval,
            modified_utc=$now WHERE name=$name;
            """;
        AddTagParameters(command, tag, now);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    /// <summary>Alias for <see cref="EditTagAsync(LogicalTag)"/>.</summary>
    /// <param name="tag">The tag to update.</param>
    /// <returns><see langword="true"/> when the tag was found and updated.</returns>
    public Task<bool> UpdateTagAsync(LogicalTag tag) => UpdateTagAsync(tag, CancellationToken.None);

    /// <summary>Alias for <see cref="EditTagAsync(LogicalTag, CancellationToken)"/>.</summary>
    /// <param name="tag">The tag to update.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the tag was found and updated.</returns>
    public Task<bool> UpdateTagAsync(LogicalTag tag, CancellationToken cancellationToken) =>
        EditTagAsync(tag, cancellationToken);

    /// <summary>Deletes a tag by name.</summary>
    /// <param name="name">The tag name to delete.</param>
    /// <returns><see langword="true"/> when the tag was found and deleted.</returns>
    public Task<bool> DeleteTagAsync(string name) => DeleteTagAsync(name, CancellationToken.None);

    /// <summary>Deletes a tag by name.</summary>
    /// <param name="name">The tag name to delete.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the tag was found and deleted.</returns>
    public async Task<bool> DeleteTagAsync(string name, CancellationToken cancellationToken)
    {
        _ = LogicalTag.Required(name, nameof(name));

        using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM logical_tags WHERE name = $name;";
        _ = command.Parameters.AddWithValue(NameParameter, name);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    /// <summary>Gets a group by name, or <see langword="null"/> when it is absent.</summary>
    /// <param name="name">The group name to look up.</param>
    /// <returns>The matching group, or <see langword="null"/>.</returns>
    public Task<LogicalTagGroup?> GetGroupAsync(string name) => GetGroupAsync(name, CancellationToken.None);

    /// <summary>Gets a group by name, or <see langword="null"/> when it is absent.</summary>
    /// <param name="name">The group name to look up.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching group, or <see langword="null"/>.</returns>
    public async Task<LogicalTagGroup?> GetGroupAsync(string name, CancellationToken cancellationToken)
    {
        _ = LogicalTag.Required(name, nameof(name));

        using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT name,description,metadata FROM logical_tag_groups WHERE name = $name;";
        _ = command.Parameters.AddWithValue(NameParameter, name);

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadGroup(reader) : null;
    }

    /// <summary>Lists groups in a stable name order.</summary>
    /// <returns>A read-only list of all persisted groups.</returns>
    public Task<IReadOnlyList<LogicalTagGroup>> ListGroupsAsync() => ListGroupsAsync(CancellationToken.None);

    /// <summary>Lists groups in a stable name order.</summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A read-only list of all persisted groups.</returns>
    public async Task<IReadOnlyList<LogicalTagGroup>> ListGroupsAsync(CancellationToken cancellationToken)
    {
        var groups = new List<LogicalTagGroup>();

        using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT name,description,metadata FROM logical_tag_groups ORDER BY name COLLATE BINARY;";

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            groups.Add(ReadGroup(reader));
        }

        return groups;
    }

    /// <summary>Inserts or replaces a group, retaining the original creation timestamp.</summary>
    /// <param name="group">The group to persist.</param>
    /// <returns>A task that represents the asynchronous upsert operation.</returns>
    public Task UpsertGroupAsync(LogicalTagGroup group) => UpsertGroupAsync(group, CancellationToken.None);

    /// <summary>Inserts or replaces a group, retaining the original creation timestamp.</summary>
    /// <param name="group">The group to persist.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that represents the asynchronous upsert operation.</returns>
    public async Task UpsertGroupAsync(LogicalTagGroup group, CancellationToken cancellationToken)
    {
        if (group is null)
        {
            throw new ArgumentNullException(nameof(group));
        }

        var now = DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture);

        using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO logical_tag_groups(name,description,metadata,created_utc,modified_utc)
            VALUES($name,$description,$metadata,$now,$now)
            ON CONFLICT(name) DO UPDATE SET description=excluded.description, metadata=excluded.metadata, modified_utc=excluded.modified_utc;
            """;
        _ = command.Parameters.AddWithValue(NameParameter, group.Name);
        _ = command.Parameters.AddWithValue("$description", group.Description);
        _ = command.Parameters.AddWithValue("$metadata", MetadataCodec.Encode(group.Metadata));
        _ = command.Parameters.AddWithValue("$now", now);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Deletes a group by name.</summary>
    /// <param name="name">The group name to delete.</param>
    /// <returns><see langword="true"/> when the group was found and deleted.</returns>
    public Task<bool> DeleteGroupAsync(string name) => DeleteGroupAsync(name, CancellationToken.None);

    /// <summary>Deletes a group by name.</summary>
    /// <param name="name">The group name to delete.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the group was found and deleted.</returns>
    public async Task<bool> DeleteGroupAsync(string name, CancellationToken cancellationToken)
    {
        _ = LogicalTag.Required(name, nameof(name));

        using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM logical_tag_groups WHERE name = $name;";
        _ = command.Parameters.AddWithValue(NameParameter, name);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    /// <summary>Loads a fresh in-memory catalog from all persisted tags.</summary>
    /// <returns>An in-memory <see cref="LogicalTagCatalog"/> containing all persisted tags.</returns>
    public Task<LogicalTagCatalog> LoadCatalogAsync() => LoadCatalogAsync(CancellationToken.None);

    /// <summary>Loads a fresh in-memory catalog from all persisted tags.</summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>An in-memory <see cref="LogicalTagCatalog"/> containing all persisted tags.</returns>
    public async Task<LogicalTagCatalog> LoadCatalogAsync(CancellationToken cancellationToken)
    {
        var catalog = new LogicalTagCatalog();

        foreach (var tag in await ListTagsAsync(cancellationToken).ConfigureAwait(false))
        {
            catalog.Upsert(tag);
        }

        return catalog;
    }

    /// <summary>Adds the standard tag parameters to <paramref name="command"/>.</summary>
    /// <param name="command">The command to populate.</param>
    /// <param name="tag">The tag providing parameter values.</param>
    /// <param name="now">The ISO 8601 timestamp string used for created/modified columns.</param>
    private static void AddTagParameters(SqliteCommand command, LogicalTag tag, string now)
    {
        _ = command.Parameters.AddWithValue(NameParameter, tag.Name);
        _ = command.Parameters.AddWithValue("$address", tag.Address);
        _ = command.Parameters.AddWithValue("$dataType", tag.DataType);
        _ = command.Parameters.AddWithValue("$groupName", tag.GroupName);
        _ = command.Parameters.AddWithValue("$description", tag.Description);
        _ = command.Parameters.AddWithValue("$metadata", MetadataCodec.Encode(tag.Metadata));
        _ = command.Parameters.AddWithValue("$accessMode", tag.AccessMode.ToString());
        _ = command.Parameters.AddWithValue(
            "$scanInterval",
            tag.ScanInterval.HasValue ? (object)tag.ScanInterval.Value.TotalMilliseconds : DBNull.Value);
        _ = command.Parameters.AddWithValue("$now", now);
    }

    /// <summary>Reads a <see cref="LogicalTag"/> from the current row of <paramref name="reader"/>.</summary>
    /// <param name="reader">The data reader positioned on the row to read.</param>
    /// <returns>The logical tag read from the current row.</returns>
    private static LogicalTag ReadTag(SqliteDataReader reader)
    {
        var accessModeText = reader.GetString(TagAccessModeColumn);

        if (!Enum.TryParse<LogicalTagAccessMode>(accessModeText, true, out var accessMode)
            || !Enum.IsDefined(typeof(LogicalTagAccessMode), accessMode))
        {
            throw new FormatException("The database contains an invalid access mode.");
        }

        var scanInterval = reader.IsDBNull(TagScanIntervalColumn)
            ? (TimeSpan?)null
            : TimeSpan.FromMilliseconds(reader.GetDouble(TagScanIntervalColumn));

        return new LogicalTag(
            reader.GetString(TagNameColumn),
            reader.GetString(TagAddressColumn),
            reader.GetString(TagDataTypeColumn),
            new LogicalTagOptions
            {
                GroupName = reader.GetString(TagGroupNameColumn),
                Description = reader.GetString(TagDescriptionColumn),
                Metadata = MetadataCodec.Decode(reader.GetString(TagMetadataColumn)),
                AccessMode = accessMode,
                ScanInterval = scanInterval,
            });
    }

    /// <summary>Reads a <see cref="LogicalTagGroup"/> from the current row of <paramref name="reader"/>.</summary>
    /// <param name="reader">The data reader positioned on the row to read.</param>
    /// <returns>The logical tag group read from the current row.</returns>
    private static LogicalTagGroup ReadGroup(SqliteDataReader reader) =>
        new(
            reader.GetString(GroupNameColumn),
            reader.GetString(GroupDescriptionColumn),
            MetadataCodec.Decode(reader.GetString(GroupMetadataColumn)));

    /// <summary>Checks whether a named column exists in the <c>logical_tags</c> table.</summary>
    /// <param name="connection">An open SQLite connection.</param>
    /// <param name="columnName">The column name to search for (compared case-insensitively).</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the column exists.</returns>
    private static async Task<bool> ColumnExistsAsync(
        SqliteConnection connection,
        string columnName,
        CancellationToken cancellationToken)
    {
        using var schema = connection.CreateCommand();
        schema.CommandText = "PRAGMA table_info(logical_tags);";

        using var reader = await schema.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (string.Equals(
                    reader.GetString(PragmaColumnNameColumn),
                    columnName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Ensures the <c>access_mode</c> column exists in <c>logical_tags</c>.</summary>
    /// <param name="connection">An open SQLite connection.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that represents the asynchronous schema migration operation.</returns>
    private static async Task EnsureAccessModeColumnAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (await ColumnExistsAsync(connection, "access_mode", cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE logical_tags ADD COLUMN access_mode TEXT NOT NULL DEFAULT 'ReadWrite';";
        await alter.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Ensures the <c>scan_interval_ms</c> column exists in <c>logical_tags</c>.</summary>
    /// <param name="connection">An open SQLite connection.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that represents the asynchronous schema migration operation.</returns>
    private static async Task EnsureScanIntervalColumnAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (await ColumnExistsAsync(connection, "scan_interval_ms", cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE logical_tags ADD COLUMN scan_interval_ms REAL NULL;";
        await alter.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens a new <see cref="SqliteConnection"/> using the stored connection string.</summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>An open <see cref="SqliteConnection"/>.</returns>
    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
