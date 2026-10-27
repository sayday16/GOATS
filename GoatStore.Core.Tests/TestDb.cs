using GoatStore.DataAccess;
using GoatStore.DataAccess.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GoatStore.Core.Tests
{
    /// <summary>
    /// Окрема SQLite-база в пам'яті для кожного тесту. Живе, поки відкрите з'єднання.
    /// </summary>
    public sealed class TestDb : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<GoatStoreDbContext> _options;

        public TestDb()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<GoatStoreDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var context = CreateContext();
            context.Database.EnsureCreated();
        }

        public GoatStoreDbContext CreateContext() => new(_options);

        public Store AddStore(string name = "Test Store")
        {
            using var context = CreateContext();
            var store = new Store
            {
                Name = name,
                NormalizedName = name.ToLowerInvariant(),
                StorePasswordHash = "not-a-real-hash"
            };
            context.Stores.Add(store);
            context.SaveChanges();
            return store;
        }

        public Product AddProduct(Guid storeId, string title = "Test Product", ProductStatus status = ProductStatus.InStock)
        {
            using var context = CreateContext();
            var product = new Product
            {
                Title = title,
                PurchasePrice = 100m,
                SalePrice = 250m,
                Status = status,
                SoldDate = status == ProductStatus.Sold ? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) : null,
                StoreId = storeId
            };
            context.Products.Add(product);
            context.SaveChanges();
            return product;
        }

        public void Dispose() => _connection.Dispose();
    }

    public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
