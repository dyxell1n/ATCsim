using Microsoft.Data.Sqlite;

namespace ATCsim.Data.Context;

/// <summary>
/// Provides SQLite database connections and connection string management.
/// Places atcsim.db directly in application root directory for easy inspection.
/// </summary>
public class DatabaseContext
{
    public string DbPath { get; }
    public string ConnectionString { get; }

    public DatabaseContext(string? dbPath = null)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            string baseDir = AppContext.BaseDirectory;
            DbPath = Path.Combine(baseDir, "atcsim.db");
        }
        else
        {
            DbPath = dbPath;
        }

        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true
        }.ToString();
    }

    public SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        return connection;
    }
}
