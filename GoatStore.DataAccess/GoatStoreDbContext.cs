using GoatStore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoatStore.DataAccess
{
    public class GoatStoreDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Store> Stores { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleItem> SaleItems { get; set; }

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

            modelBuilder.Entity<Product>(entity =>
            {
                entity.Property(p => p.Id).ValueGeneratedOnAdd();
                entity.Property(p => p.Status).HasConversion<string>();
                entity.Property(p => p.Condition).HasConversion<string>().HasMaxLength(20);

                entity.Property(p => p.Title).HasMaxLength(200);
                entity.Property(p => p.Brand).HasMaxLength(100);
                entity.Property(p => p.Size).HasMaxLength(30);
                entity.Property(p => p.Category).HasMaxLength(100);
                entity.Property(p => p.Sku).HasMaxLength(50);

                entity.HasIndex(p => new { p.StoreId, p.Sku })
                    .IsUnique()
                    .HasFilter("\"Sku\" IS NOT NULL");

                entity.HasIndex(p => new { p.StoreId, p.Status });
                entity.HasIndex(p => new { p.StoreId, p.Quantity });
            });

            modelBuilder.Entity<Sale>(entity =>
            {
                entity.Property(s => s.Id).ValueGeneratedOnAdd();
                entity.Property(s => s.PaymentMethod).HasConversion<string>().HasMaxLength(20);
                entity.Property(s => s.Channel).HasMaxLength(50);
                entity.Property(s => s.Discount).HasPrecision(18, 2);

                entity.HasIndex(s => new { s.StoreId, s.Number }).IsUnique();
                entity.HasIndex(s => new { s.StoreId, s.CreatedAt });

                entity.HasOne(s => s.Store)
                    .WithMany()
                    .HasForeignKey(s => s.StoreId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.SoldBy)
                    .WithMany()
                    .HasForeignKey(s => s.SoldByUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(s => s.Items)
                    .WithOne(i => i.Sale)
                    .HasForeignKey(i => i.SaleId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SaleItem>(entity =>
            {
                entity.Property(i => i.Id).ValueGeneratedOnAdd();
                entity.Property(i => i.UnitPrice).HasPrecision(18, 2);
                entity.Property(i => i.PurchasePrice).HasPrecision(18, 2);

                entity.HasOne(i => i.Product)
                    .WithMany()
                    .HasForeignKey(i => i.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Store>(entity =>
            {
                entity.Property(s => s.Id).ValueGeneratedOnAdd();
                entity.Property(s => s.Name).HasMaxLength(100);
                entity.Property(s => s.NormalizedName).HasMaxLength(100);
                entity.Property(s => s.AnnualTurnoverLimit).HasPrecision(18, 2);

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