using Microsoft.Data.Sqlite;
using System;
using System.Data.SQLite;

namespace PaApiWorker
{
    public sealed class SourceDbUpdater
    {
        private readonly string _connectionString;
        private readonly AppLogger _logger;
        private readonly string _sourceDbPath;

        public SourceDbUpdater(string sourceDbPath, AppLogger logger)
        {
            if (string.IsNullOrWhiteSpace(sourceDbPath))
                throw new ArgumentException("sourceDbPath is required.", nameof(sourceDbPath));

            _connectionString = $"Data Source={sourceDbPath};Version=3;";
            _sourceDbPath = sourceDbPath;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 元DBの Products テーブルに AffiliateUrl / Title を反映
        /// 主キー列は Id を想定
        /// </summary>
        public void UpdateAffiliateUrl(string sourceRowId, string affiliateUrl, string title)
        {
            if (string.IsNullOrWhiteSpace(sourceRowId))
                throw new ArgumentException("sourceRowId is required.", nameof(sourceRowId));



/*
            var con = new SqliteConnectionStringBuilder(_connectionString);
            con.Open();

            using (var pragmaCmd = con.CreateCommand())
            {
                pragmaCmd.CommandText = @"
PRAGMA journal_mode=WAL;
PRAGMA busy_timeout=5000;";
                pragmaCmd.ExecuteNonQuery();
            }


*/

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = _sourceDbPath
            };

            using var con = new SqliteConnection(builder.ConnectionString);
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT * FROM SampleTable";
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                Console.WriteLine(reader[0]?.ToString());
            }


/*

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
UPDATE Products
SET AffiliateUrl = @AffiliateUrl,
    Title = CASE
                WHEN @Title <> '' THEN @Title
                ELSE Title
            END
WHERE Id = @Id;";
            
            
            cmd.Parameters.AddWithValue("@AffiliateUrl", affiliateUrl ?? "");
            cmd.Parameters.AddWithValue("@Title", title ?? "");
            cmd.Parameters.AddWithValue("@Id", sourceRowId);

*/
            var csb = new SqliteConnectionStringBuilder();
            csb.DataSource = _sourceDbPath;

            using var con1 = new SqliteConnection(csb.ConnectionString);
            con1.Open();


            using var cmd1 = con1.CreateCommand();
            cmd1.CommandText = @"
UPDATE Products
SET AffiliateUrl = @AffiliateUrl,
    Title = CASE
                WHEN @Title <> '' THEN @Title
                ELSE Title
            END
WHERE Id = @Id;";

            cmd1.Parameters.AddWithValue("@AffiliateUrl", affiliateUrl ?? "");
            cmd1.Parameters.AddWithValue("@Title", title ?? "");
            cmd1.Parameters.AddWithValue("@Id", sourceRowId);

            int affected = cmd1.ExecuteNonQuery();


            if (affected > 0)
            {
                _logger.Info($"Source DB updated. Id={sourceRowId}, AffiliateUrl={affiliateUrl}");
            }
            else
            {
                _logger.Warn($"Source DB update target not found. Id={sourceRowId}");
            }
        }
    }
}
