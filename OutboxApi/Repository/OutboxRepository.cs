using Entities.Model;
using Microsoft.EntityFrameworkCore;
using OutboxApi.DBContext;

namespace OutboxApi.Repository
{
    public class OutboxRepository : IOutboxRepository
    {
        private readonly OutboxDbContext _context;

        public OutboxRepository(OutboxDbContext context)
        {
            _context = context;
        }

        public async Task AddNewOutboxAsync(Outbox outbox)
        {
            using var tx = await _context.Database.BeginTransactionAsync();
            outbox.RetryCount = 0;

            _context.Outboxes.Add(outbox);
            await _context.SaveChangesAsync();

            await tx.CommitAsync();
        }

        public async Task<List<Outbox>> GetUnprocessedOutboxesAsync()
        {
            var outboxes = await _context.Outboxes
                .Where(o => o.ProcessedAt == null)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();

            return outboxes;
        }

        public async Task MarkOutboxAsProcessedAsync(Guid outboxId)
        {
            using var tx = await _context.Database.BeginTransactionAsync();

            var outbox = await _context.Outboxes.FirstOrDefaultAsync(o => o.Id == outboxId);
            if (outbox == null)
                throw new KeyNotFoundException($"Outbox with id '{outboxId}' was not found.");

            outbox.ProcessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await tx.CommitAsync();
        }

        public async Task MarkOutboxAsFailedAsync(Guid outboxId)
        {
            using var tx = await _context.Database.BeginTransactionAsync();

            var outbox = await _context.Outboxes.FirstOrDefaultAsync(o => o.Id == outboxId);
            if (outbox == null)
                throw new KeyNotFoundException($"Outbox with id '{outboxId}' was not found.");

            outbox.RetryCount++;
            await _context.SaveChangesAsync();

            await tx.CommitAsync();
        }
    }
}