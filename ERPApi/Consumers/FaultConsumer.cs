using System.Text.Json;
using Entities.Model;
using ERPApi.DBContext;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace ERPApi.Consumers
{
    /// <summary>
    /// Generic Fault consumer for Dead Letter Channel (DLC).
    /// Handles all faulted messages after retry exhaustion.
    /// Logs failures and stores in outbox for investigation.
    /// </summary>
    public class FaultConsumer<T> : IConsumer<Fault<T>> where T : class
    {
        private readonly OrderDbContext _context;
        private readonly ILogger<FaultConsumer<T>> _logger;

        public FaultConsumer(OrderDbContext context, ILogger<FaultConsumer<T>> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<Fault<T>> context)
        {
            var fault = context.Message;
            var messageType = typeof(T).Name;

            _logger.LogError("💀 [DEAD LETTER] Message of type {MessageType} faulted after retries. Timestamp: {Timestamp}",
                messageType, fault.Timestamp);

            _logger.LogError("Fault Reason: {Reason}", 
                string.Join(", ", fault.Exceptions.Select(e => e.Message)));

            try
            {
                // Store faulted message in outbox for investigation
                var faultRecord = new Outbox
                {
                    Id = Guid.NewGuid(),
                    MessageType = $"Fault<{messageType}>",
                    Payload = JsonSerializer.Serialize(new
                    {
                        OriginalMessage = fault.Message,
                        Exceptions = fault.Exceptions.Select(e => new
                        {
                            e.Message,
                            e.StackTrace,
                            ExceptionType = e.ExceptionType
                        }).ToList(),
                        fault.Timestamp,
                        fault.Host
                    }),
                    CreatedAt = DateTime.UtcNow,
                    ProcessedAt = null, // Mark as unprocessed for manual review
                    RetryCount = 999 // Special marker for DLC messages
                };

                _context.OutboxMessages.Add(faultRecord);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Stored faulted {MessageType} message in outbox for review. Fault ID: {FaultId}",
                    messageType, faultRecord.Id);

                // TODO: Send critical alert to operations team
                // TODO: Trigger incident management system
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store faulted {MessageType} message in outbox", messageType);
                // Don't rethrow - we don't want the fault handler to fault
            }
        }
    }
}
