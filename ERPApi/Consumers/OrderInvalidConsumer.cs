using Contracts.Events;
using MassTransit;

namespace ERPApi.Consumers
{
    public class OrderInvalidConsumer : IConsumer<OrderInvalid>
    {
        private readonly ILogger<OrderInvalidConsumer> _logger;

        public OrderInvalidConsumer(ILogger<OrderInvalidConsumer> logger)
        {
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<OrderInvalid> context)
        {
            var message = context.Message;
            
            _logger.LogWarning(
                "[ERP-Api] Invalid order received. OrderId={OrderId}, Reason={Reason}, Errors={Errors}",
                message.OrderId,
                message.Reason,
                string.Join(", ", message.ValidationErrors));

            await Task.CompletedTask;
        }
    }
}
