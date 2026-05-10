using Microsoft.Data.Sqlite;
using System;
using System.Data.SQLite;

namespace AmazonApiQueueSystem
{
    public sealed class AmazonApiQueueWriter
    {
        private readonly string _connectionString;

        public AmazonApiQueueWriter(string sqliteFilePath)
        {
            if (string.IsNullOrWhiteSpace(sqliteFilePath))
                throw new ArgumentException("sqliteFilePath is required.", nameof(sqliteFilePath));

            _connectionString = $"Data Source={sqliteFilePath};Version=3;";
        }

        public void EnsureTables()
        {
            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS AmazonApiQueue (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Asin TEXT NOT NULL,
    Status INTEGER NOT NULL DEFAULT 0,
    RetryCount INTEGER NOT NULL DEFAULT 0,
    RequestedAt TEXT NOT NULL,
    StartedAt TEXT,
    FinishedAt TEXT,
    LastError TEXT,
    AffiliateUrl TEXT,
    Title TEXT,
    RawResponse TEXT
);

CREATE INDEX IF NOT EXISTS IX_AmazonApiQueue_Status_RequestedAt
ON AmazonApiQueue(Status, RequestedAt);

CREATE UNIQUE INDEX IF NOT EXISTS UX_AmazonApiQueue_Asin_Pending
ON AmazonApiQueue(Asin, Status)
WHERE Status IN (0,1);
";
            cmd.ExecuteNonQuery();
        }

        public bool EnqueueAsin(string asin)
        {
            if (string.IsNullOrWhiteSpace(asin))
                return false;

            asin = asin.Trim().ToUpperInvariant();

            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
INSERT OR IGNORE INTO AmazonApiQueue
(
    Asin,
    Status,
    RetryCount,
    RequestedAt
)
VALUES
(
    @Asin,
    0,
    0,
    @RequestedAt
);";
            cmd.Parameters.AddWithValue("@Asin", asin);
            cmd.Parameters.AddWithValue("@RequestedAt", DateTime.UtcNow.ToString("o"));

            int affected = cmd.ExecuteNonQuery();
            return affected > 0;
        }
    }
}