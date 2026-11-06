using Contracts.Events;
using Microsoft.AspNetCore.Mvc;
using OrderService.Messaging;

namespace OrderService.Controllers
{
    [ApiController]
    [Route("api/messaging-test")]
    public class MessagingTestController : ControllerBase
    {
        private readonly RabbitMqPublisher _publisher;

        public MessagingTestController(RabbitMqPublisher publisher)
        {
            _publisher = publisher;
        }

        [HttpPost]
        public async Task<IActionResult> Send()
        {
            var message = new OrderCancellingEvent
            {
                OrderId = 201,
                OccurredAt = DateTime.UtcNow
            };

            await _publisher.PublishOrderCancellingAsync(message);

            return Ok("Event published.");
        }
        [HttpPost("test-cancellation-events")]
        public async Task<IActionResult> TestCancellationEvents()
        {
            var orderId = 9998;

            await _publisher.PublishOrderCancellingAsync(
                new OrderCancellingEvent
                {
                    OrderId = orderId,
                    OccurredAt = DateTime.UtcNow
                });

            await _publisher.PublishOrderCancelledAsync(
                new OrderCancelledEvent
                {
                    OrderId = orderId,
                    OccurredAt = DateTime.UtcNow
                });

            return Ok("Both events published.");
        }
    }
}
