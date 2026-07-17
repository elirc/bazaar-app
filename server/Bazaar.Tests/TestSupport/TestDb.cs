using Bazaar.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Tests.TestSupport;

/// <summary>
/// A disposable, isolated SQLite database held entirely in memory. The connection is kept open
/// for the lifetime of the handle so the schema (created by applying real migrations) survives.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public BazaarDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<BazaarDbContext>()
            .UseSqlite(_connection)
            .Options;
        var context = new BazaarDbContext(options);
        context.Database.Migrate();
        return context;
    }

    public void Dispose() => _connection.Dispose();
}
