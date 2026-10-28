using Microsoft.EntityFrameworkCore;
using GoatStore.DataAccess.Entities;
using Xunit;

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
}
