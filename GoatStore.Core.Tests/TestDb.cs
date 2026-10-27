using GoatStore.DataAccess;
using GoatStore.DataAccess.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GoatStore.Core.Tests
{
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

        public Store AddStore(string name = "Test Store", string? storePassword = null)
        {
            using var context = CreateContext();
            var store = new Store
            {
                Name = name,
                NormalizedName = name.ToLowerInvariant(),
                StorePasswordHash = storePassword is null
                    ? "not-a-real-hash"
                    : BCrypt.Net.BCrypt.HashPassword(storePassword)
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

        public User AddUser(Guid storeId, string email = "user@test.com", string password = "password123", UserRole role = UserRole.Seller)
        {
            using var context = CreateContext();
            var user = new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = role,
                StoreId = storeId
            };
            context.Users.Add(user);
            context.SaveChanges();
            return user;
        }

        public void Dispose() => _connection.Dispose();
    }

    public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
