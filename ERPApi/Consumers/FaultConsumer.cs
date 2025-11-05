using ERPApi.Repository.Order;
using MassTransit;

namespace ERPApi.Consumers
{
    public class FaultConsumer<T> : IConsumer<Fault<T>> where T : class
    {
        private readonly ILogger<FaultConsumer<T>> _logger;
        private readonly IOrderRepository _orderRepository;

        public FaultConsumer(ILogger<FaultConsumer<T>> logger, IOrderRepository orderRepository)
        {
            _logger = logger;
            _orderRepository = orderRepository;
        }

        public async Task Consume(ConsumeContext<Fault<T>> context)
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

            // Store the faulted message for manual investigation
            try
            {
                await _orderRepository.StoreFaultedMessageAsync(fault, 999);
                _logger.LogInformation("[ERP-Api] Faulted message stored for manual investigation. FaultId={FaultId}", fault.FaultId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ERP-Api] Failed to store faulted message. FaultId={FaultId}", fault.FaultId);
            }

            // Alert operations team (in production, this would integrate with alerting systems)
            _logger.LogCritical("[ERP-Api] 🚨 MANUAL INTERVENTION REQUIRED: Message {FaultId} requires investigation", fault.FaultId);
        }
    }
}
