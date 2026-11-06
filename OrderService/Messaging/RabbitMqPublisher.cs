using Contracts.Events;
using RabbitMQ.Client;
using System.Text.Json;

namespace OrderService.Messaging
{
    public class RabbitMqPublisher
    {
        private readonly IConnection _connection;

        public RabbitMqPublisher(IConnection connection)
        {
            _connection = connection;
        }

        public async Task PublishOrderCancellingAsync(
            OrderCancellingEvent message)
        {
            await using var channel =
                await _connection.CreateChannelAsync();

            await channel.ExchangeDeclareAsync(
                exchange: "order-events",
                type: ExchangeType.Direct,
                durable: true);

            var body = JsonSerializer.SerializeToUtf8Bytes(message);

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json"
            };

            await channel.BasicPublishAsync(
                exchange: "order-events",
                routingKey: "order.cancelling",
                mandatory: false,
                basicProperties: properties,
                body: body);
        }


        //In Event miad OrderInventoryState onaei ke cancelling mikone cancelled ke Inventory ham bedone cancelled shodan
        public async Task PublishOrderCancelledAsync(OrderCancelledEvent message)
        {
            await using var channel =
                await _connection.CreateChannelAsync();

            await channel.ExchangeDeclareAsync(
                exchange: "order-events",
                type: ExchangeType.Direct,
                durable: true);

            var body = JsonSerializer.SerializeToUtf8Bytes(message);

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json"
            };

            await channel.BasicPublishAsync(
                exchange: "order-events",
                routingKey: "order.cancelled",
                mandatory: false,
                basicProperties: properties,
                body: body);
        }
    }
}
