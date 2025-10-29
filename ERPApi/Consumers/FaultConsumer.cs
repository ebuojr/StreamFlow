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

        public async Task Consume(ConsumeContext<Fault<T>> context)
        {
            var fault = context.Message;
            var messageType = typeof(T).Name;

            _logger.LogError("[ERP-Api] Faulted message received. MessageType={MessageType}, FaultId={FaultId}, ExceptionCount={ExceptionCount}",
                messageType, fault.FaultId, fault.Exceptions?.Length ?? 0);

            if (fault.Exceptions != null && fault.Exceptions.Any())
            {
                foreach (var exception in fault.Exceptions)
                {
                    _logger.LogError("[ERP-Api] Exception: {ExceptionType} - {Message}",
                        exception.ExceptionType, exception.Message);
                }
            }

            await Task.CompletedTask;
        }
    }
}
