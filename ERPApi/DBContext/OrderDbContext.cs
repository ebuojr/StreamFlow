using Entities.Model;
using Microsoft.EntityFrameworkCore;

namespace ERPApi.DBContext
{
    public class OrderDbContext : DbContext
    {
        public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
        {
        }

        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<OrderSentToPicking> OrderSentToPickings { get; set; }
        public DbSet<Outbox> OutboxMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Unique index for OrderNo
                entity.HasIndex(e => e.OrderNo).IsUnique();

                // Own simple single-value objects as owned types (flattened columns)
                entity.OwnsOne(o => o.Customer, cb =>
                {
                    cb.Property(c => c.Id).HasColumnName("CustomerId");
                    cb.Property(c => c.FirstName).HasColumnName("CustomerFirstName");
                    cb.Property(c => c.LastName).HasColumnName("CustomerLastName");
                    cb.Property(c => c.Email).HasColumnName("CustomerEmail");
                    cb.Property(c => c.Phone).HasColumnName("CustomerPhone");
                });

                entity.OwnsOne(o => o.Payment, pb =>
                {
                    pb.Property(p => p.PaymentMethod).HasColumnName("PaymentMethod");
                    pb.Property(p => p.PaymentStatus).HasColumnName("PaymentStatus");
                    pb.Property(p => p.PaidAt).HasColumnName("PaidAt");
                    pb.Property(p => p.Currency).HasColumnName("Currency");
                    pb.Property(p => p.Amount).HasColumnName("PaymentAmount");
                });

                entity.OwnsOne(o => o.ShippingAddress, ab =>
                {
                    ab.Property(a => a.Street).HasColumnName("Ship_Street");
                    ab.Property(a => a.City).HasColumnName("Ship_City");
                    ab.Property(a => a.State).HasColumnName("Ship_State");
                    ab.Property(a => a.PostalCode).HasColumnName("Ship_PostalCode");
                    ab.Property(a => a.Country).HasColumnName("Ship_Country");
                });

                // Configure relationship to OrderItems
                entity.HasMany(o => o.OrderItems)
                      .WithOne()
                      .HasForeignKey(oi => oi.OrderId)
                      .IsRequired();
            });

            modelBuilder.Entity<OrderItem>(item =>
            {
                item.HasKey(i => i.Id);
                item.Property(i => i.Sku).IsRequired();
                item.Property(i => i.Name).IsRequired();
                item.Property<Guid>("OrderId");
                
                // Configure Status property with default value
                item.Property(i => i.Status)
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasDefaultValue("Pending");
            });

            modelBuilder.Entity<OrderSentToPicking>(osp =>
            {
                osp.HasKey(o => o.Id);
                osp.Property(o => o.OrderNo).IsRequired();
                osp.Property(o => o.SentTime).IsRequired();
            });

            modelBuilder.Entity<Outbox>(outbox =>
            {
                outbox.HasKey(o => o.Id);
                outbox.Property(o => o.MessageType).IsRequired().HasMaxLength(100);
                outbox.Property(o => o.Payload).IsRequired();
                outbox.Property(o => o.CreatedAt).IsRequired();
                outbox.HasIndex(o => o.ProcessedAt); // For querying unprocessed messages
                outbox.HasIndex(o => o.CreatedAt); // For ordering
            });
        }
    }
}
