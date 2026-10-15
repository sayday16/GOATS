using GoatStore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoatStore.DataAccess
{
    public class GoatStoreDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Store> Stores { get; set; }

        public GoatStoreDbContext(DbContextOptions<GoatStoreDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .Property(u => u.Id)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<string>();

            modelBuilder.Entity<Product>()
                .Property(p => p.Id)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<Product>()
                .Property(p => p.Status)
                .HasConversion<string>();

            modelBuilder.Entity<Store>(entity =>
            {
                entity.Property(s => s.Id).ValueGeneratedOnAdd();
                entity.Property(s => s.Name).HasMaxLength(100);
                entity.Property(s => s.NormalizedName).HasMaxLength(100);

                entity.HasIndex(s => s.NormalizedName).IsUnique();


                entity.HasMany(s => s.Users)
                    .WithOne(u => u.Store)
                    .HasForeignKey(u => u.StoreId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(s => s.Products)
                    .WithOne(p => p.Store)
                    .HasForeignKey(p => p.StoreId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}