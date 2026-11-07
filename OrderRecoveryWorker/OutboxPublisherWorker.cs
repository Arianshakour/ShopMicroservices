using Contracts.Events;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrderRecoveryWorker
{
    //In Worker har chand sanie jadval OutboxMessages baresi mikone va payam haye ersal nashode ra
    //be rabbitMq mifreste
    public class OutboxPublisherWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConnection _connection;
        private readonly ILogger<OutboxPublisherWorker> _logger;

        public OutboxPublisherWorker(
            IServiceScopeFactory scopeFactory,
            IConnection connection,
            ILogger<OutboxPublisherWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _connection = connection;
            _logger = logger;
        }


        //هر ۵ ثانیه جدول Outbox رو بررسی می‌کنه
        //حداکثر ۱۰ پیام ارسال‌نشده رو می‌خونه
        //نوع پیام رو تشخیص می‌ده و Routing Key مناسب رو انتخاب می‌کنه
        //پیام رو به RabbitMQ ارسال می‌کنه
        //اگر ارسال موفق باشه، ProcessedAt رو پر می‌کنه
        //اگر ارسال شکست بخوره، تعداد تلاش و متن خطا رو ذخیره می‌کنه تا بعداً دوباره تلاش بشه
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();

                    var db = scope.ServiceProvider
                        .GetRequiredService<OrderDbContext>();

                    var messages = await db.OutboxMessages
                        .Where(x => x.ProcessedAt == null)
                        .OrderBy(x => x.CreatedAt)
                        .Take(10)
                        .ToListAsync(stoppingToken);

                    if (messages.Any())
                    {
                        await using var channel =
                            await _connection.CreateChannelAsync();

                        await channel.ExchangeDeclareAsync(
                            exchange: "order-events",
                            type: ExchangeType.Direct,
                            durable: true);

                        foreach (var message in messages)
                        {
                            try
                            {
                                string routingKey;

                                switch (message.Type)
                                {
                                    case nameof(OrderCancelledEvent):
                                        routingKey = "order.cancelled";
                                        break;

                                    default:
                                        throw new InvalidOperationException(
                                            $"Unknown event type: {message.Type}");
                                }

                                var body = Encoding.UTF8.GetBytes(
                                    message.Payload);

                                var properties = new BasicProperties
                                {
                                    Persistent = true,
                                    ContentType = "application/json"
                                };

                                await channel.BasicPublishAsync(
                                    exchange: "order-events",
                                    routingKey: routingKey,
                                    mandatory: false,
                                    basicProperties: properties,
                                    body: body);

                                message.ProcessedAt = DateTime.UtcNow;
                                message.Error = null;

                                _logger.LogInformation(
                                    "Outbox message {MessageId} published.",
                                    message.Id);
                            }
                            catch (Exception ex)
                            {
                                message.RetryCount++;
                                message.Error = ex.Message;

                                _logger.LogError(
                                    ex,
                                    "Failed to publish outbox message {MessageId}",
                                    message.Id);
                            }

                            await db.SaveChangesAsync(stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error while processing outbox messages.");
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(5),
                    stoppingToken);
            }
        }
    }
}
