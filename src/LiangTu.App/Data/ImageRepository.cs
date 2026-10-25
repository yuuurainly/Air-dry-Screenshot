using LiangTu.Core;
using Microsoft.Data.Sqlite;

namespace LiangTu.App.Data;

public sealed class ImageRepository
{
    private readonly string _databasePath;
    private readonly string _connectionString;

    public ImageRepository(string databasePath)
    {
        _databasePath = databasePath;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
        }.ToString();
    }

    public void Initialize()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        using var connection = CreateConnection();
        var version = GetSchemaVersion(connection);
        if (version > 2)
        {
            throw new InvalidOperationException($"数据库版本 {version} 高于当前应用支持的版本。");
        }

        using var transaction = connection.BeginTransaction();
        if (version < 1)
        {
            Execute(connection, transaction, CreateSchemaSql);
        }
        else if (version == 1)
        {
            Execute(connection, transaction, MigrateVersionOneSql);
        }

        transaction.Commit();
        Execute(connection, null, "PRAGMA foreign_keys = ON;");
    }

    private const string CreateSchemaSql =
        """
            CREATE TABLE IF NOT EXISTS image_assets (
                id TEXT PRIMARY KEY,
                file_path TEXT NOT NULL,
                content_hash TEXT NOT NULL,
                created_at INTEGER NOT NULL,
                modified_at INTEGER NOT NULL,
                pixel_width INTEGER NOT NULL,
                pixel_height INTEGER NOT NULL,
                source_type INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_image_assets_hash ON image_assets(content_hash);

            CREATE TABLE IF NOT EXISTS rope_items (
                id TEXT PRIMARY KEY,
                image_id TEXT NOT NULL UNIQUE REFERENCES image_assets(id) ON DELETE CASCADE,
                sort_position INTEGER NOT NULL,
                added_at INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_rope_items_sort ON rope_items(sort_position);

            CREATE TABLE IF NOT EXISTS pin_instances (
                id TEXT PRIMARY KEY,
                image_id TEXT NULL REFERENCES image_assets(id) ON DELETE SET NULL,
                x REAL NOT NULL,
                y REAL NOT NULL,
                width REAL NOT NULL,
                height REAL NOT NULL,
                opacity REAL NOT NULL,
                is_topmost INTEGER NOT NULL,
                created_at INTEGER NOT NULL
            );

            PRAGMA user_version = 2;
            """;

    private const string MigrateVersionOneSql =
        """
            DROP INDEX IF EXISTS ix_rope_items_sort;
            ALTER TABLE image_assets RENAME TO image_assets_v1;
            ALTER TABLE rope_items RENAME TO rope_items_v1;
            ALTER TABLE pin_instances RENAME TO pin_instances_v1;

            CREATE TABLE image_assets (
                id TEXT PRIMARY KEY,
                file_path TEXT NOT NULL,
                content_hash TEXT NOT NULL,
                created_at INTEGER NOT NULL,
                modified_at INTEGER NOT NULL,
                pixel_width INTEGER NOT NULL,
                pixel_height INTEGER NOT NULL,
                source_type INTEGER NOT NULL
            );

            CREATE INDEX ix_image_assets_hash ON image_assets(content_hash);

            CREATE TABLE rope_items (
                id TEXT PRIMARY KEY,
                image_id TEXT NOT NULL UNIQUE REFERENCES image_assets(id) ON DELETE CASCADE,
                sort_position INTEGER NOT NULL,
                added_at INTEGER NOT NULL
            );

            CREATE INDEX ix_rope_items_sort ON rope_items(sort_position);

            CREATE TABLE pin_instances (
                id TEXT PRIMARY KEY,
                image_id TEXT NULL REFERENCES image_assets(id) ON DELETE SET NULL,
                x REAL NOT NULL,
                y REAL NOT NULL,
                width REAL NOT NULL,
                height REAL NOT NULL,
                opacity REAL NOT NULL,
                is_topmost INTEGER NOT NULL,
                created_at INTEGER NOT NULL
            );

            INSERT INTO image_assets
            SELECT id, file_path, content_hash, created_at, modified_at,
                   pixel_width, pixel_height, source_type
            FROM image_assets_v1;

            INSERT INTO rope_items
            SELECT id, image_id, sort_position, added_at
            FROM rope_items_v1;

            INSERT INTO pin_instances
            SELECT id, image_id, x, y, width, height, opacity, is_topmost, created_at
            FROM pin_instances_v1;

            DROP TABLE rope_items_v1;
            DROP TABLE pin_instances_v1;
            DROP TABLE image_assets_v1;

            PRAGMA user_version = 2;
            """;

    public ImageAsset? FindByHash(string contentHash)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, file_path, content_hash, created_at, modified_at,
                   pixel_width, pixel_height, source_type
            FROM image_assets
            WHERE content_hash = $hash
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$hash", contentHash);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadAsset(reader) : null;
    }

    public ImageAsset? GetAsset(Guid id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, file_path, content_hash, created_at, modified_at,
                   pixel_width, pixel_height, source_type
            FROM image_assets
            WHERE id = $id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadAsset(reader) : null;
    }

    public IReadOnlyList<ImageAsset> GetAssets()
    {
        var result = new List<ImageAsset>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, file_path, content_hash, created_at, modified_at,
                   pixel_width, pixel_height, source_type
            FROM image_assets
            ORDER BY modified_at DESC;
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(ReadAsset(reader));
        }

        return result;
    }

    public IReadOnlyList<RopeItem> GetRopeItems()
    {
        var result = new List<RopeItem>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT r.id, r.sort_position, r.added_at,
                   a.id, a.file_path, a.content_hash, a.created_at, a.modified_at,
                   a.pixel_width, a.pixel_height, a.source_type
            FROM rope_items r
            JOIN image_assets a ON a.id = r.image_id
            ORDER BY r.sort_position ASC;
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var asset = ReadAsset(reader, 3);
            result.Add(new RopeItem(
                Guid.Parse(reader.GetString(0)),
                asset,
                reader.GetInt32(1),
                DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(2))));
        }

        return result;
    }

    public void InsertAsset(ImageAsset asset)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO image_assets (
                id, file_path, content_hash, created_at, modified_at,
                pixel_width, pixel_height, source_type
            )
            VALUES (
                $id, $filePath, $hash, $createdAt, $modifiedAt,
                $width, $height, $sourceType
            );
            """;
        command.Parameters.AddWithValue("$id", asset.Id.ToString("D"));
        command.Parameters.AddWithValue("$filePath", asset.FilePath);
        command.Parameters.AddWithValue("$hash", asset.ContentHash);
        command.Parameters.AddWithValue("$createdAt", asset.CreatedAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$modifiedAt", asset.ModifiedAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$width", asset.PixelWidth);
        command.Parameters.AddWithValue("$height", asset.PixelHeight);
        command.Parameters.AddWithValue("$sourceType", (int)asset.SourceType);
        command.ExecuteNonQuery();
    }

    public void PromoteToRope(Guid assetId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var nextCommand = connection.CreateCommand();
        nextCommand.Transaction = transaction;
        nextCommand.CommandText = "SELECT COALESCE(MAX(sort_position), -1) + 1 FROM rope_items;";
        var nextPosition = Convert.ToInt32(nextCommand.ExecuteScalar());

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO rope_items (id, image_id, sort_position, added_at)
            VALUES ($id, $imageId, $position, $addedAt)
            ON CONFLICT(image_id) DO UPDATE SET
                sort_position = excluded.sort_position,
                added_at = excluded.added_at;
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$imageId", assetId.ToString("D"));
        command.Parameters.AddWithValue("$position", nextPosition);
        command.Parameters.AddWithValue("$addedAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public void SetRopeOrder(IReadOnlyList<Guid> ropeItemIds)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        for (var index = 0; index < ropeItemIds.Count; index++)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE rope_items SET sort_position = $position WHERE id = $id;";
            command.Parameters.AddWithValue("$position", index);
            command.Parameters.AddWithValue("$id", ropeItemIds[index].ToString("D"));
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void RemoveFromRope(Guid ropeItemId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM rope_items WHERE id = $id;";
        command.Parameters.AddWithValue("$id", ropeItemId.ToString("D"));
        command.ExecuteNonQuery();
    }

    public void RemoveAssetFromRope(Guid imageId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM rope_items WHERE image_id = $imageId;";
        command.Parameters.AddWithValue("$imageId", imageId.ToString("D"));
        command.ExecuteNonQuery();
    }

    public void DeleteAsset(Guid imageId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM image_assets WHERE id = $id;";
        command.Parameters.AddWithValue("$id", imageId.ToString("D"));
        command.ExecuteNonQuery();
    }

    public void UpdateAssetContent(
        Guid assetId,
        string contentHash,
        DateTimeOffset modifiedAt,
        int pixelWidth,
        int pixelHeight)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE image_assets
            SET content_hash = $hash,
                modified_at = $modifiedAt,
                pixel_width = $width,
                pixel_height = $height
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$hash", contentHash);
        command.Parameters.AddWithValue("$modifiedAt", modifiedAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$width", pixelWidth);
        command.Parameters.AddWithValue("$height", pixelHeight);
        command.Parameters.AddWithValue("$id", assetId.ToString("D"));
        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException("图片记录不存在，无法完成编辑保存。");
        }
    }

    public IReadOnlyList<PinInstanceState> GetPinInstances()
    {
        var result = new List<PinInstanceState>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, image_id, x, y, width, height, opacity, is_topmost, created_at
            FROM pin_instances
            ORDER BY created_at ASC;
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new PinInstanceState(
                Guid.Parse(reader.GetString(0)),
                reader.IsDBNull(1) ? null : Guid.Parse(reader.GetString(1)),
                reader.GetDouble(2),
                reader.GetDouble(3),
                reader.GetDouble(4),
                reader.GetDouble(5),
                reader.GetDouble(6),
                reader.GetInt64(7) != 0,
                DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(8))));
        }

        return result;
    }

    public void UpsertPin(PinInstanceState state)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO pin_instances (
                id, image_id, x, y, width, height, opacity, is_topmost, created_at
            )
            VALUES (
                $id, $imageId, $x, $y, $width, $height, $opacity, $isTopmost, $createdAt
            )
            ON CONFLICT(id) DO UPDATE SET
                image_id = excluded.image_id,
                x = excluded.x,
                y = excluded.y,
                width = excluded.width,
                height = excluded.height,
                opacity = excluded.opacity,
                is_topmost = excluded.is_topmost;
            """;
        command.Parameters.AddWithValue("$id", state.Id.ToString("D"));
        command.Parameters.AddWithValue("$imageId", state.ImageId is null ? DBNull.Value : state.ImageId.Value.ToString("D"));
        command.Parameters.AddWithValue("$x", state.X);
        command.Parameters.AddWithValue("$y", state.Y);
        command.Parameters.AddWithValue("$width", state.Width);
        command.Parameters.AddWithValue("$height", state.Height);
        command.Parameters.AddWithValue("$opacity", state.Opacity);
        command.Parameters.AddWithValue("$isTopmost", state.IsTopmost ? 1 : 0);
        command.Parameters.AddWithValue("$createdAt", state.CreatedAt.ToUnixTimeMilliseconds());
        command.ExecuteNonQuery();
    }

    public void DeletePin(Guid id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM pin_instances WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;
            PRAGMA busy_timeout = 5000;
            PRAGMA journal_mode = WAL;
            """;
        command.ExecuteNonQuery();
        return connection;
    }

    private SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static int GetSchemaVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static ImageAsset ReadAsset(SqliteDataReader reader, int offset = 0)
    {
        return new ImageAsset(
            Guid.Parse(reader.GetString(offset)),
            reader.GetString(offset + 1),
            reader.GetString(offset + 2),
            DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(offset + 3)),
            DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(offset + 4)),
            reader.GetInt32(offset + 5),
            reader.GetInt32(offset + 6),
            (ImageSourceType)reader.GetInt32(offset + 7));
    }
}
