using Microsoft.Data.Sqlite;

namespace ATCsim.Data.Context;

/// <summary>
/// Provides SQLite database connections and connection string management.
/// Keeps new local databases in the user's application data directory.
/// </summary>
public class DatabaseContext
{
    public static string LocalDataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ATCsim");

    public string DbPath { get; }
    public string ConnectionString { get; }

    public DatabaseContext(string? dbPath = null)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            string previousDatabase = Path.Combine(AppContext.BaseDirectory, "atcsim.db");
            if (File.Exists(previousDatabase))
            {
                DbPath = previousDatabase;
            }
            else
            {
                string databaseDirectory = Path.Combine(LocalDataDirectory, "Data");
                Directory.CreateDirectory(databaseDirectory);
                DbPath = Path.Combine(databaseDirectory, "atcsim.db");
            }
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
