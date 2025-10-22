using MassTransit;

namespace PickingService.Consumers
{
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
                "🎯❌ [PICKING-DLC] Faulted message: Type={MessageType}, FaultId={FaultId}, Timestamp={Timestamp}",
                messageType,
                fault.FaultId,
                fault.Timestamp);

            if (fault.Exceptions != null)
            {
                foreach (var ex in fault.Exceptions)
                {
                    _logger.LogError(
                        "🎯❌ [PICKING-DLC] Exception: {ExceptionType} - {Message}",
                        ex.ExceptionType,
                        ex.Message);
                }
            }

            _logger.LogWarning(
                "🎯⚠️ [PICKING-DLC] Fault stored for manual review: {MessageType} [FaultId={FaultId}]",
                messageType,
                fault.FaultId);

            await Task.CompletedTask;
        }
    }
}
