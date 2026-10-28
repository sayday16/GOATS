using Microsoft.EntityFrameworkCore;
using GoatStore.DataAccess.Entities;

namespace GoatStore.DataAccess.Tests;

public class GoatStoreDbContextTests
{
    [Fact]
    public async Task AddUser_DuplicateEmail_ThrowsDbUpdateException()
    {
        using var context = TestDb.CreateInMemoryContext();

        var store = new Store
        {
            Name = "Shop 1",
            NormalizedName = "shop 1"
        };
        context.Stores.Add(store);
        await context.SaveChangesAsync();

        context.Users.Add(new User
        {
            Email = "test@gmail.com",
            PasswordHash = "hash1",
            Role = UserRole.Admin,
            StoreId = store.Id
        });
        await context.SaveChangesAsync();

        context.Users.Add(new User
        {
            Email = "test@gmail.com",
            PasswordHash = "hash2",
            Role = UserRole.Seller,
            StoreId = store.Id
        });

        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            await context.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task AddStore_DuplicateNormalizedName_ThrowsDbUpdateException()
    {
        using var context = TestDb.CreateInMemoryContext();

        context.Stores.Add(new Store
        {
            Name = "Lviv Shop",
            NormalizedName = "lviv shop"
        });
        await context.SaveChangesAsync();

        context.Stores.Add(new Store
        {
            Name = "Lviv SHOP",
            NormalizedName = "lviv shop"
        });

        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            await context.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task DeleteStore_WithUsers_ThrowsDbUpdateException()
    {
        // Arrange
        using var context = TestDb.CreateInMemoryContext();

        var store = new Store
        {
            Name = "Shop Restrict",
            NormalizedName = "shop restrict"
        };
        context.Stores.Add(store);
        await context.SaveChangesAsync();

        var user = new User
        {
            Email = "user@gmail.com",
            PasswordHash = "hash",
            Role = UserRole.Seller,
            StoreId = store.Id
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Очищаємо трекер контексту, щоб EF Core забув про зв'язки в пам'яті
        // і перевірив реальне обмеження бази даних (Restrict)
        context.ChangeTracker.Clear();

        // Наново знаходимо магазин або видаляємо за ID
        var storeToDelete = await context.Stores.FirstAsync(s => s.Id == store.Id);
        context.Stores.Remove(storeToDelete);

        // Act & Assert - база повинна викинути DbUpdateException через зовнішній ключ
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            await context.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task DeleteStore_WithProducts_ThrowsDbUpdateException()
    {
        using var context = TestDb.CreateInMemoryContext();

        var store = new Store
        {
            Name = "Shop With Products",
            NormalizedName = "shop with products"
        };
        context.Stores.Add(store);
        await context.SaveChangesAsync();

        context.Products.Add(new Product
        {
            Title = "Levi's 501",
            PurchasePrice = 400m,
            SalePrice = 1200m,
            StoreId = store.Id
        });
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();

        var storeToDelete = await context.Stores.FirstAsync(s => s.Id == store.Id);
        context.Stores.Remove(storeToDelete);

        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            await context.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task UserRole_IsStoredAsString()
    {
        using var context = TestDb.CreateInMemoryContext();

        var store = new Store
        {
            Name = "Role Shop",
            NormalizedName = "role shop"
        };
        context.Stores.Add(store);
        context.Users.Add(new User
        {
            Email = "admin@gmail.com",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            Store = store
        });
        await context.SaveChangesAsync();

        var storedRole = await context.Database
            .SqlQueryRaw<string>("SELECT \"Role\" AS \"Value\" FROM \"Users\"")
            .SingleAsync();

        Assert.Equal("Admin", storedRole);
    }

    [Fact]
    public async Task ProductStatus_IsStoredAsString()
    {
        using var context = TestDb.CreateInMemoryContext();

        var store = new Store
        {
            Name = "Status Shop",
            NormalizedName = "status shop"
        };
        context.Stores.Add(store);
        context.Products.Add(new Product
        {
            Title = "Nike Hoodie",
            PurchasePrice = 300m,
            SalePrice = 900m,
            Status = ProductStatus.Sold,
            Store = store
        });
        await context.SaveChangesAsync();

        var storedStatus = await context.Database
            .SqlQueryRaw<string>("SELECT \"Status\" AS \"Value\" FROM \"Products\"")
            .SingleAsync();

        Assert.Equal("Sold", storedStatus);
    }
}
