using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Text.Json;
using Contracts.Events;
using InventoryService.Data;
using InventoryService.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Messaging
{
    public class OrderCancellingConsumer : BackgroundService
    {
        private readonly IConnection _connection;
        private readonly IServiceScopeFactory _scopeFactory;//baraye zakhire payam dar database

        public OrderCancellingConsumer(
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
                queue: "inventory-order-cancelling",
                durable: true,
                exclusive: false,
                autoDelete: false);

            await channel.QueueBindAsync(
                queue: "inventory-order-cancelling",
                exchange: "order-events",
                routingKey: "order.cancelling");

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (sender, ea) =>
            {
                var message = JsonSerializer.Deserialize<OrderCancellingEvent>(
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
                //    .FirstOrDefaultAsync(x => x.OrderId == message.OrderId);

                //if (orderState == null)
                //{
                //    orderState = new OrderInventoryState
                //    {
                //        OrderId = message.OrderId,
                //        Status = "Cancelling"
                //    };

                //    db.OrderInventoryStates.Add(orderState);
                //}
                //else if (orderState.Status != "Cancelled")
                //{
                //    orderState.Status = "Cancelling";
                //}

                //await db.SaveChangesAsync();


                //comment kardam chon bayad injoori benevisam ke lock beshe ta ba OrderCancelledConsumer hamzaman nashe
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
                        Status = "Cancelling"
                    };

                    db.OrderInventoryStates.Add(orderState);
                }
                else if (orderState.Status != "Cancelled")
                {
                    orderState.Status = "Cancelling";
                }

                await db.SaveChangesAsync();

                await transaction.CommitAsync();

                await channel.BasicAckAsync(
                    ea.DeliveryTag,
                    multiple: false);
            };

            await channel.BasicConsumeAsync(
                queue: "inventory-order-cancelling",
                autoAck: false,
                consumer: consumer);

            await Task.Delay(
                Timeout.Infinite,
                stoppingToken);
        }
    }
}
