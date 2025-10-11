using Entities.Model;
using Microsoft.EntityFrameworkCore;

namespace OutboxApi.DBContext
{
    public class OutboxDbContext : DbContext
    {
        public OutboxDbContext(DbContextOptions<OutboxDbContext> options) : base(options) { }

        public DbSet<Outbox> Outboxes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Outbox>(entity =>
            {
                entity.HasKey(e => e.Id);
            });
        }
    }
}
