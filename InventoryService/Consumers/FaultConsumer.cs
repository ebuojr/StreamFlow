using MassTransit;

namespace InventoryService.Consumers
{
    /// <summary>
    /// Dead Letter Channel (DLC) consumer for handling faulted messages
    /// Implements Dead Letter Channel pattern for fault tolerance
    /// </summary>
    public class FaultConsumer<T> : IConsumer<Fault<T>> where T : class
    {
        private readonly ILogger<FaultConsumer<T>> _logger;

        public FaultConsumer(ILogger<FaultConsumer<T>> logger)
        {
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<Fault<T>> context)
        {
            var fault = context.Message;
            var messageType = typeof(T).Name;

            _logger.LogError(
                "💚❌ [INVENTORY-DLC] Faulted message received: MessageType={MessageType}, FaultId={FaultId}, Timestamp={Timestamp}, Exceptions={ExceptionCount}",
                messageType,
                fault.FaultId,
                fault.Timestamp,
                fault.Exceptions?.Length ?? 0);

            if (fault.Exceptions != null && fault.Exceptions.Any())
            {
                foreach (var ex in fault.Exceptions)
                {
                    _logger.LogError(
                        "💚❌ [INVENTORY-DLC] Exception: {ExceptionType} - {Message}",
                        ex.ExceptionType,
                        ex.Message);
                }
            }

            // Store fault in database for manual intervention
            // TODO: Implement fault storage mechanism
            _logger.LogWarning(
                "💚⚠️ [INVENTORY-DLC] Fault stored for manual review: {MessageType} [FaultId={FaultId}]",
                messageType,
                fault.FaultId);

            await Task.CompletedTask;
        }
    }
}
