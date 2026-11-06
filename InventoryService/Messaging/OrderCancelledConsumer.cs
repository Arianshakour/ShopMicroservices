using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using InventoryService.Data;
using Microsoft.EntityFrameworkCore;
using Contracts.Events;
using System.Text.Json;
using InventoryService.Entities;

namespace InventoryService.Messaging
{
    //In Event miad OrderInventoryState onaei ke cancelling mikone cancelled ke Inventory ham bedone cancelled shodan
    public class OrderCancelledConsumer : BackgroundService
    {
        private readonly IConnection _connection;
        private readonly IServiceScopeFactory _scopeFactory;

        public OrderCancelledConsumer(
            IConnection connection,
            IServiceScopeFactory scopeFactory)
        {
            _connection = connection;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            await using var channel =
                await _connection.CreateChannelAsync();

            await channel.ExchangeDeclareAsync(
                exchange: "order-events",
                type: ExchangeType.Direct,
                durable: true);

            await channel.QueueDeclareAsync(
                queue: "inventory-order-cancelled",
                durable: true,
                exclusive: false,
                autoDelete: false);

            await channel.QueueBindAsync(
                queue: "inventory-order-cancelled",
                exchange: "order-events",
                routingKey: "order.cancelled");

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (sender, ea) =>
            {
                var message =
                    JsonSerializer.Deserialize<OrderCancelledEvent>(
                        ea.Body.ToArray());

                if (message == null)
                {
                    await channel.BasicNackAsync(
                        ea.DeliveryTag,
                        multiple: false,
                        requeue: false);

                    return;
                }

                using var scope = _scopeFactory.CreateScope();

                var db = scope.ServiceProvider
                    .GetRequiredService<InventoryDbContext>();

                //var orderState = await db.OrderInventoryStates
                //    .FirstOrDefaultAsync(
                //        x => x.OrderId == message.OrderId);

                //if (orderState == null)
                //{
                //    orderState = new Entities.OrderInventoryState
                //    {
                //        OrderId = message.OrderId,
                //        Status = "Cancelled"
                //    };

                //    db.OrderInventoryStates.Add(orderState);
                //}
                //else
                //{
                //    orderState.Status = "Cancelled";
                //}

                //await db.SaveChangesAsync();

                //comment kardam chon bayad injoori benevisam ke lock beshe ta ba OrderCancellingConsumer hamzaman nashe

                using var transaction = await db.Database.BeginTransactionAsync(stoppingToken);

                var orderState = await db.OrderInventoryStates
                    .FromSqlInterpolated($@"
                        SELECT *
                        FROM OrderInventoryStates WITH (UPDLOCK, HOLDLOCK)
                        WHERE OrderId = {message.OrderId}
                        ")
                    .FirstOrDefaultAsync();

                if (orderState == null)
                {
                    orderState = new OrderInventoryState
                    {
                        OrderId = message.OrderId,
                        Status = "Cancelled"
                    };

                    db.OrderInventoryStates.Add(orderState);
                }
                else
                {
                    orderState.Status = "Cancelled";
                }

                await db.SaveChangesAsync();

                await transaction.CommitAsync();

                Console.WriteLine(
                    $"Order {message.OrderId} is cancelled.");

                await channel.BasicAckAsync(
                    ea.DeliveryTag,
                    multiple: false);
            };

            await channel.BasicConsumeAsync(
                queue: "inventory-order-cancelled",
                autoAck: false,
                consumer: consumer);

            await Task.Delay(
                Timeout.Infinite,
                stoppingToken);
        }
    }
}
