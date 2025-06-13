using System.Data;
using Microsoft.Data.Sqlite;
using TelegramWordBot;

public class TestDbConnectionFactory : IConnectionFactory, IDisposable
{
    private readonly string _connectionString;
    private readonly string _dbPath;

    public TestDbConnectionFactory()
    {
        _dbPath = Path.GetTempFileName();
        _connectionString = $"Data Source={_dbPath}";
    }

    public TestDbConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
        var builder = new SqliteConnectionStringBuilder(connectionString);
        _dbPath = builder.DataSource ?? string.Empty;
    }

    public IDbConnection CreateConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    public void Dispose()
    {
        if (!string.IsNullOrEmpty(_dbPath) && File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }
}
