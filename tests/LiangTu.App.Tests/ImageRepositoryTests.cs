using System.IO;
using LiangTu.App.Data;
using LiangTu.Core;
using Microsoft.Data.Sqlite;

namespace LiangTu.App.Tests;

[TestClass]
public sealed class ImageRepositoryTests
{
    [TestMethod]
    public void Initialize_MigratesVersionOneAndPreservesRows()
    {
        var directory = CreateTemporaryDirectory();
        var databasePath = Path.Combine(directory, "liangtu.db");
        try
        {
            CreateVersionOneDatabase(databasePath);
            var repository = new ImageRepository(databasePath);

            repository.Initialize();

            Assert.AreEqual(2, ReadSchemaVersion(databasePath));
            Assert.HasCount(1, repository.GetAssets());
            Assert.HasCount(1, repository.GetRopeItems());

            var duplicateHashAsset = CreateAsset(Guid.NewGuid(), "duplicate.png", "same-hash");
            repository.InsertAsset(duplicateHashAsset);
            Assert.HasCount(2, repository.GetAssets());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void RopeOrder_AndPins_ArePersisted()
    {
        var directory = CreateTemporaryDirectory();
        var databasePath = Path.Combine(directory, "liangtu.db");
        try
        {
            var repository = new ImageRepository(databasePath);
            repository.Initialize();
            var first = CreateAsset(Guid.NewGuid(), "first.png", "hash-1");
            var second = CreateAsset(Guid.NewGuid(), "second.png", "hash-2");
            repository.InsertAsset(first);
            repository.InsertAsset(second);
            repository.PromoteToRope(first.Id);
            repository.PromoteToRope(second.Id);

            var rope = repository.GetRopeItems();
            repository.SetRopeOrder([rope[1].Id, rope[0].Id]);
            var reordered = repository.GetRopeItems();
            Assert.AreEqual(second.Id, reordered[0].Image.Id);

            var pin = new PinInstanceState(
                Guid.NewGuid(),
                first.Id,
                120,
                80,
                400,
                260,
                0.8,
                true,
                DateTimeOffset.UtcNow);
            repository.UpsertPin(pin);
            Assert.HasCount(1, repository.GetPinInstances());

            repository.DeletePin(pin.Id);
            Assert.IsEmpty(repository.GetPinInstances());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void DeletingAsset_CascadesRopeAndDetachesPins()
    {
        var directory = CreateTemporaryDirectory();
        var databasePath = Path.Combine(directory, "liangtu.db");
        try
        {
            var repository = new ImageRepository(databasePath);
            repository.Initialize();
            var asset = CreateAsset(Guid.NewGuid(), "delete.png", "delete-hash");
            repository.InsertAsset(asset);
            repository.PromoteToRope(asset.Id);
            var pin = new PinInstanceState(
                Guid.NewGuid(),
                asset.Id,
                80,
                60,
                320,
                220,
                1,
                true,
                DateTimeOffset.UtcNow);
            repository.UpsertPin(pin);

            repository.DeleteAsset(asset.Id);

            Assert.IsEmpty(repository.GetAssets());
            Assert.IsEmpty(repository.GetRopeItems());
            var remainingPin = repository.GetPinInstances().Single();
            Assert.AreEqual(pin.Id, remainingPin.Id);
            Assert.IsNull(remainingPin.ImageId);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void CreateVersionOneDatabase(string databasePath)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE image_assets (
                id TEXT PRIMARY KEY,
                file_path TEXT NOT NULL,
                content_hash TEXT NOT NULL UNIQUE,
                created_at INTEGER NOT NULL,
                modified_at INTEGER NOT NULL,
                pixel_width INTEGER NOT NULL,
                pixel_height INTEGER NOT NULL,
                source_type INTEGER NOT NULL
            );
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
            PRAGMA user_version = 1;
            """;
        command.ExecuteNonQuery();

        var assetId = Guid.NewGuid();
        using var insertAsset = connection.CreateCommand();
        insertAsset.CommandText = """
            INSERT INTO image_assets
            VALUES ($id, 'legacy.png', 'legacy-hash', 1, 1, 100, 80, 1);
            """;
        insertAsset.Parameters.AddWithValue("$id", assetId.ToString("D"));
        insertAsset.ExecuteNonQuery();

        using var insertRope = connection.CreateCommand();
        insertRope.CommandText = """
            INSERT INTO rope_items
            VALUES ($id, $imageId, 0, 1);
            """;
        insertRope.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
        insertRope.Parameters.AddWithValue("$imageId", assetId.ToString("D"));
        insertRope.ExecuteNonQuery();
    }

    private static int ReadSchemaVersion(string databasePath)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static ImageAsset CreateAsset(Guid id, string fileName, string hash)
    {
        var now = DateTimeOffset.UtcNow;
        return new ImageAsset(
            id,
            fileName,
            hash,
            now,
            now,
            100,
            80,
            ImageSourceType.FileImport);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "LiangTu.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
