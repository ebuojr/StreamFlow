using ERPApi.DBContext;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ERPApi.Repository.Order
{
    public class OrderRepositroy : IOrderRepository
    {
        private readonly OrderDbContext _context;
        public OrderRepositroy(OrderDbContext context)
        {
            _context = context;
        }

    public async Task<int> CreateOrderAsync(Entities.Model.Order order)
    {
        // NOTE: Transaction is managed by the service layer (for transactional outbox pattern)
        // Generate incremental OrderNo (max + 1)
        var maxOrderNo = await _context.Orders.MaxAsync(o => (int?)o.OrderNo) ?? 0;
        int newOrderNo = maxOrderNo >= 1000 ? (maxOrderNo + 1) : 1000;

        order.OrderNo = newOrderNo;
        _context.Orders.Add(order);
        // NOTE: SaveChanges is called by service layer after adding outbox message

        return newOrderNo;
    }        public async Task<IEnumerable<Entities.Model.Order>> GetAllOrders()
        {
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .ToListAsync();
            return orders;
        }

        public async Task<Entities.Model.Order> GetOrderById(Guid id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                throw new System.Collections.Generic.KeyNotFoundException($"Order with id '{id}' was not found.");

            return order;
        }

        public async Task<IEnumerable<Entities.Model.Order>> GetOrderByState(string state)
        {
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .Where(o => o.OrderState == state)
                .ToListAsync();
            return orders;
        }

        public async Task<bool> UpdateOrderState(Guid id, string state)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null)
                return false;

            order.OrderState = state;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task StoreFaultedMessageAsync<T>(Fault<T> fault, int retryCount) where T : class
        {
            // Store the faulted message in the Outbox table for manual investigation
            var faultMessage = new
            {
                FaultId = fault.FaultId,
                MessageType = typeof(T).Name,
                Message = JsonSerializer.Serialize(fault.Message),
                Exceptions = fault.Exceptions?.Select(e => new
                {
                    ExceptionType = e.ExceptionType,
                    Message = e.Message,
                    StackTrace = e.StackTrace
                }).ToList(),
                Timestamp = fault.Timestamp,
                Host = fault.Host,
                RetryCount = retryCount,
                ProcessedAt = (DateTime?)null
            };

            // Store as JSON in the Outbox table (using a custom message type for faults)
            var outboxMessage = new MassTransit.EntityFrameworkCoreIntegration.OutboxMessage
            {
                SequenceNumber = 0, // Will be auto-generated
                Body = JsonSerializer.Serialize(faultMessage),
                ContentType = "application/vnd.masstransit+json",
                ConversationId = null,
                CorrelationId = null,
                DestinationAddress = new Uri("urn:fault"),
                EnqueueTime = DateTime.UtcNow,
                ExpirationTime = null,
                FaultAddress = null,
                Headers = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["MT-Fault-RetryCount"] = retryCount,
                    ["MT-Fault-MessageType"] = typeof(T).Name
                }),
                InboxConsumerId = null,
                InboxMessageId = null,
                InitiatorId = null,
                MessageId = fault.FaultId,
                MessageType = $"urn:message:Contracts.Fault`1[[{typeof(T).AssemblyQualifiedName}]]",
                OutboxId = null,
                Properties = null,
                RequestId = null,
                ResponseAddress = null,
                SentTime = DateTime.UtcNow,
                SourceAddress = null
            };

            _context.Set<MassTransit.EntityFrameworkCoreIntegration.OutboxMessage>().Add(outboxMessage);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<MassTransit.EntityFrameworkCoreIntegration.OutboxMessage>> GetFaultedMessagesAsync()
        {
            // Get faulted messages from Outbox table (those with destination "urn:fault")
            // Use client-side evaluation since ToString() cannot be translated to SQL
            return _context.Set<MassTransit.EntityFrameworkCoreIntegration.OutboxMessage>()
                .AsEnumerable()
                .Where(m => m.DestinationAddress?.ToString() == "urn:fault")
                .OrderByDescending(m => m.SentTime)
                .ToList();
        }
    }
}
