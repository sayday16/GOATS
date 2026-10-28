using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using GoatStore.DataAccess;

namespace GoatStore.DataAccess.Tests;

public static class TestDb
{
    public static GoatStoreDbContext CreateInMemoryContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<GoatStoreDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new GoatStoreDbContext(options);
        context.Database.EnsureCreated();

        return context;
    }
}