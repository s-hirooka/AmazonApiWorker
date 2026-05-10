using Microsoft.Data.Sqlite;
using System;
using System.Data.SQLite;

namespace AmazonApiQueueSystem
{
    public sealed class AmazonApiQueueItem
    {
        public long Id { get; set; }
        public string Asin { get; set; } = "";
        public int RetryCount { get; set; }
        public string SourceExe { get; set; } = "";
        public string SourceRowId { get; set; } = "";
    }

    public sealed class AmazonApiQueueRepository
    {
        private readonly string _connectionString;

        public AmazonApiQueueRepository(string sqliteFilePath)
        {
            if (string.IsNullOrWhiteSpace(sqliteFilePath))
                throw new ArgumentException("sqliteFilePath is required.", nameof(sqliteFilePath));

            _connectionString = $"Data Source={sqliteFilePath};Version=3;";
        }

        public AmazonApiQueueItem? TryTakeNextPending()
        {
            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using (var pragmaCmd = con.CreateCommand())
            {
                pragmaCmd.CommandText = @"
PRAGMA journal_mode=WAL;
PRAGMA busy_timeout=5000;";
                pragmaCmd.ExecuteNonQuery();
            }

            using var tx = con.BeginTransaction();

            long? id = null;
            string? asin = null;
            int retryCount = 0;
            string sourceExe = "";
            string sourceRowId = "";

            using (var selectCmd = con.CreateCommand())
            {
                selectCmd.Transaction = tx;
                selectCmd.CommandText = @"
SELECT Id, Asin, RetryCount, IFNULL(SourceExe,''), IFNULL(SourceRowId,'')
FROM AmazonApiQueue
WHERE Status = 0
ORDER BY RequestedAt ASC, Id ASC
LIMIT 1;";
                using var reader = selectCmd.ExecuteReader();
                if (reader.Read())
                {
                    id = reader.GetInt64(0);
                    asin = reader.GetString(1);
                    retryCount = reader.GetInt32(2);
                    sourceExe = reader.GetString(3);
                    sourceRowId = reader.GetString(4);
                }
            }

            if (!id.HasValue)
            {
                tx.Commit();
                return null;
            }

            using (var updateCmd = con.CreateCommand())
            {
                updateCmd.Transaction = tx;
                updateCmd.CommandText = @"
UPDATE AmazonApiQueue
SET Status = 1,
    StartedAt = @StartedAt
WHERE Id = @Id
  AND Status = 0;";
                updateCmd.Parameters.AddWithValue("@StartedAt", DateTime.UtcNow.ToString("o"));
                updateCmd.Parameters.AddWithValue("@Id", id.Value);

                int affected = updateCmd.ExecuteNonQuery();
                if (affected == 0)
                {
                    tx.Commit();
                    return null;
                }
            }

            tx.Commit();

            return new AmazonApiQueueItem
            {
                Id = id.Value,
                Asin = asin!,
                RetryCount = retryCount,
                SourceExe = sourceExe,
                SourceRowId = sourceRowId
            };
        }

        public void MarkSuccess(long id, string title, string affiliateUrl, string rawResponse)
        {
            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using (var pragmaCmd = con.CreateCommand())
            {
                pragmaCmd.CommandText = @"
PRAGMA journal_mode=WAL;
PRAGMA busy_timeout=5000;";
                pragmaCmd.ExecuteNonQuery();
            }

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
UPDATE AmazonApiQueue
SET Status = 2,
    FinishedAt = @FinishedAt,
    Title = @Title,
    AffiliateUrl = @AffiliateUrl,
    RawResponse = @RawResponse,
    LastError = NULL
WHERE Id = @Id;";
            cmd.Parameters.AddWithValue("@FinishedAt", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("@Title", title ?? "");
            cmd.Parameters.AddWithValue("@AffiliateUrl", affiliateUrl ?? "");
            cmd.Parameters.AddWithValue("@RawResponse", rawResponse ?? "");
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }

        public void MarkRetry(long id, int retryCount, string errorText)
        {
            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using (var pragmaCmd = con.CreateCommand())
            {
                pragmaCmd.CommandText = @"
PRAGMA journal_mode=WAL;
PRAGMA busy_timeout=5000;";
                pragmaCmd.ExecuteNonQuery();
            }

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
UPDATE AmazonApiQueue
SET Status = 0,
    RetryCount = @RetryCount,
    LastError = @LastError,
    FinishedAt = @FinishedAt
WHERE Id = @Id;";
            cmd.Parameters.AddWithValue("@RetryCount", retryCount);
            cmd.Parameters.AddWithValue("@LastError", errorText ?? "");
            cmd.Parameters.AddWithValue("@FinishedAt", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }

        public void MarkFailed(long id, string errorText)
        {
            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using (var pragmaCmd = con.CreateCommand())
            {
                pragmaCmd.CommandText = @"
PRAGMA journal_mode=WAL;
PRAGMA busy_timeout=5000;";
                pragmaCmd.ExecuteNonQuery();
            }

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
UPDATE AmazonApiQueue
SET Status = 3,
    LastError = @LastError,
    FinishedAt = @FinishedAt
WHERE Id = @Id;";
            cmd.Parameters.AddWithValue("@LastError", errorText ?? "");
            cmd.Parameters.AddWithValue("@FinishedAt", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }
    }
}