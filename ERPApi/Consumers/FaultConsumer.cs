using MassTransit;

namespace ERPApi.Consumers
{
    /// <summary>
    /// Generic Fault consumer for Dead Letter Channel (DLC).
    /// Handles all faulted messages after retry exhaustion.
    /// Logs failures for investigation and alerting.
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

            _logger.LogError("💀 [DEAD LETTER] Message of type {MessageType} faulted after retries. Timestamp: {Timestamp}",
                messageType, fault.Timestamp);

            _logger.LogError("Fault Reason: {Reason}", 
                string.Join(", ", fault.Exceptions.Select(e => e.Message)));
            
            // Log detailed exception information
            foreach (var exception in fault.Exceptions)
            {
                _logger.LogError("Exception Type: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    exception.ExceptionType, exception.Message, exception.StackTrace);
            }

            // Note: Faulted messages are automatically logged and available in Seq
            // MassTransit stores them in RabbitMQ's dead letter queue for manual intervention
            
            // TODO: Send critical alert to operations team
            // TODO: Trigger incident management system
            
            await Task.CompletedTask;
        }
    }
}
