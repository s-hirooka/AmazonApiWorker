using Microsoft.Data.Sqlite;

namespace AmazonApiWorker;

public sealed class AmazonAffiliateQueueItem
{
    public long Id { get; set; }
    public string RequestId { get; set; } = "";
    public string Mode { get; set; } = "";
    public string ProductUrl { get; set; } = "";
    public string Asin { get; set; } = "";
    public string AssociateId { get; set; } = "";
    public string TrackingId { get; set; } = "";
    public string CredentialId { get; set; } = "";
    public string CredentialSecret { get; set; } = "";
    public string Version { get; set; } = "";
    public string Marketplace { get; set; } = "www.amazon.co.jp";
}

public sealed class AmazonAffiliateQueueRepository
{
    private readonly string _connectionString;

    public AmazonAffiliateQueueRepository(string dbPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath) ?? AppContext.BaseDirectory);
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };
        _connectionString = builder.ToString();
        EnsureTables();
    }

    public static string GetDefaultDbPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "database", "amazon_affiliate_worker.db");
    }

    public void EnsureTables()
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS AmazonAffiliateQueue (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    RequestId TEXT NOT NULL UNIQUE,
    Mode TEXT NOT NULL,
    ProductUrl TEXT NOT NULL,
    Asin TEXT,
    AssociateId TEXT,
    TrackingId TEXT,
    CredentialId TEXT,
    CredentialSecret TEXT,
    Version TEXT,
    Marketplace TEXT,
    Status INTEGER NOT NULL DEFAULT 0,
    ResultUrl TEXT,
    ErrorMessage TEXT,
    RequestedAt TEXT NOT NULL,
    StartedAt TEXT,
    FinishedAt TEXT
);

CREATE INDEX IF NOT EXISTS IX_AmazonAffiliateQueue_Status_RequestedAt
ON AmazonAffiliateQueue(Status, RequestedAt);
";
        cmd.ExecuteNonQuery();
    }

    public AmazonAffiliateQueueItem? TryTakeNextPending()
    {
        using var con = Open();
        using var tx = con.BeginTransaction();

        AmazonAffiliateQueueItem? item = null;
        using (var select = con.CreateCommand())
        {
            select.Transaction = tx;
            select.CommandText = @"
SELECT Id, RequestId, Mode, ProductUrl, IFNULL(Asin,''), IFNULL(AssociateId,''),
       IFNULL(TrackingId,''), IFNULL(CredentialId,''), IFNULL(CredentialSecret,''),
       IFNULL(Version,''), IFNULL(Marketplace,'www.amazon.co.jp')
FROM AmazonAffiliateQueue
WHERE Status = 0
ORDER BY RequestedAt ASC, Id ASC
LIMIT 1;";

            using var reader = select.ExecuteReader();
            if (reader.Read())
            {
                item = new AmazonAffiliateQueueItem
                {
                    Id = reader.GetInt64(0),
                    RequestId = reader.GetString(1),
                    Mode = reader.GetString(2),
                    ProductUrl = reader.GetString(3),
                    Asin = reader.GetString(4),
                    AssociateId = reader.GetString(5),
                    TrackingId = reader.GetString(6),
                    CredentialId = reader.GetString(7),
                    CredentialSecret = reader.GetString(8),
                    Version = reader.GetString(9),
                    Marketplace = reader.GetString(10)
                };
            }
        }

        if (item == null)
        {
            tx.Commit();
            return null;
        }

        using var update = con.CreateCommand();
        update.Transaction = tx;
        update.CommandText = @"
UPDATE AmazonAffiliateQueue
SET Status = 1,
    StartedAt = @StartedAt
WHERE Id = @Id
  AND Status = 0;";
        update.Parameters.AddWithValue("@StartedAt", DateTime.UtcNow.ToString("o"));
        update.Parameters.AddWithValue("@Id", item.Id);

        if (update.ExecuteNonQuery() == 0)
        {
            tx.Commit();
            return null;
        }

        tx.Commit();
        return item;
    }

    public void MarkSuccess(long id, string resultUrl)
    {
        UpdateFinished(id, 2, resultUrl, "");
    }

    public void MarkFailed(long id, string error)
    {
        UpdateFinished(id, 3, "", error);
    }

    private void UpdateFinished(long id, int status, string resultUrl, string error)
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
UPDATE AmazonAffiliateQueue
SET Status = @Status,
    ResultUrl = @ResultUrl,
    ErrorMessage = @ErrorMessage,
    FinishedAt = @FinishedAt
WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Status", status);
        cmd.Parameters.AddWithValue("@ResultUrl", resultUrl ?? "");
        cmd.Parameters.AddWithValue("@ErrorMessage", error ?? "");
        cmd.Parameters.AddWithValue("@FinishedAt", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var con = new SqliteConnection(_connectionString);
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=10000;";
        cmd.ExecuteNonQuery();
        return con;
    }
}
