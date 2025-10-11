using Entities.Model;
using Microsoft.EntityFrameworkCore;

namespace InventoryApi.DBContext
{
    public class InventoryDbContext : DbContext
    {
        public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options)
        {
        }

        public DbSet<Stock> Stocks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Stock>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Sku).IsUnique();
                entity.Property(e => e.Sku)
                      .IsRequired()
                      .HasMaxLength(100);
                entity.Property(e => e.Quantity)
                      .IsRequired();
                entity.Property(e => e.LastUpdated)
                      .IsRequired();
            });
        }
    }
}
