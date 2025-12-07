using MassTransit;

namespace ERPApi.Consumers
{
    public class FaultConsumer<T> : IConsumer<Fault<T>> where T : class
    {
        private readonly ILogger<FaultConsumer<T>> _logger;

        public FaultConsumer(ILogger<FaultConsumer<T>> logger)
        {
            _logger = logger;
        }

        public Task Consume(ConsumeContext<Fault<T>> context)
        {
            var fault = context.Message;
            var messageType = typeof(T).Name;

            _logger.LogCritical(
                "[ERP-Api] Faulted message moved to dead letter queue. MessageType={MessageType}, FaultId={FaultId}, ExceptionCount={ExceptionCount}",
                messageType, fault.FaultId, fault.Exceptions?.Length ?? 0);

            if (fault.Exceptions != null && fault.Exceptions.Any())
            {
                foreach (var exception in fault.Exceptions)
                {
                    _logger.LogCritical("[ERP-Api] Exception: {ExceptionType} - {Message}",
                        exception.ExceptionType, exception.Message);
                }
            }

            // Alert operations team (in production, this would integrate with alerting systems)
            // Faults are logged to Seq for investigation - no database storage needed
            _logger.LogCritical("[ERP-Api] 🚨 MANUAL INTERVENTION REQUIRED: Message {FaultId} requires investigation", fault.FaultId);

            return Task.CompletedTask;
        }
    }
}
