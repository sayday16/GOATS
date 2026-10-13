using GoatStore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoatStore.DataAccess
{
    public class GoatStoreDbContext : DbContext
    {
       
        public DbSet<User> Users { get; set; }
        public DbSet<Product> Products { get; set; }

        public GoatStoreDbContext(DbContextOptions<GoatStoreDbContext> options)
            : base(options)
        {
        }

        public GoatStoreDbContext()
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseNpgsql("Host=aws-1-eu-central-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.thpnzowfynymufgscfst;Password=DubMykolaSrakaHola;SSL Mode=Require;Trust Server Certificate=true");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();
        }
    }
}
