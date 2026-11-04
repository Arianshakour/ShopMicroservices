using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Entities;

namespace OrderRecoveryWorker;

public class Worker : BackgroundService
{
    //har 30 sanie ejra mishe
    //order haye ke status anha pending hast va 1min az create anha migzare ra peida mikone
    //az InventoryService darkhast Release mikonad
    //agar release ok bood status mishe cancelled
    //agar ertebat qat shod vaziat hamoon pending mimoone ta dar ejraye badi dobare baresi beshe
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<Worker> _logger;

    public Worker(IServiceScopeFactory scopeFactory,IHttpClientFactory httpClientFactory,ILogger<Worker> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(30));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RecoverPendingOrders(stoppingToken);
        }
    }

    private async Task<List<Order>> LockPendingOrders(OrderDbContext db,CancellationToken cancellationToken)
    {
        //injoori atomic update mikonim vaqti bishtar az yek worker dashtim
        var now = DateTime.UtcNow;

        return await db.Orders
            .FromSqlInterpolated($@"
            UPDATE TOP (10) Orders
            SET 
                IsRecoveryLocked = 1,
                RecoveryLockedAt = {now}
            OUTPUT INSERTED.*
            WHERE Status = {(int)OrderStatus.Pending}
              AND LastSagaActivityAt < {now.AddMinutes(-1)}
              AND
              (
                  IsRecoveryLocked = 0
                  OR RecoveryLockedAt < {now.AddMinutes(-2)}
              )
        ")
            .ToListAsync(cancellationToken);
    }

    private async Task RecoverPendingOrders(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<OrderDbContext>();

        var inventoryClient = _httpClientFactory
            .CreateClient("InventoryService");

        //var pendingOrders = await db.Orders
        //    .Where(x =>
        //        x.Status == OrderStatus.Pending &&
        //        x.LastSagaActivityAt < DateTime.UtcNow.AddMinutes(-1))
        //    .ToListAsync(cancellationToken);

        //balaei baraye 1 worker javab midad vali ma farz kardim bishtar az 1 worker darim
        var pendingOrders = await LockPendingOrders(db, cancellationToken);


        foreach (var order in pendingOrders)
        {
            try
            {
                _logger.LogInformation(
                    "Recovering Order {OrderId}",
                    order.Id);

                var response = await inventoryClient.PostAsJsonAsync(
                    "api/inventory/release",
                    new { OrderId = order.Id },
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Could not release stock for Order {OrderId}",
                        order.Id);

                    continue;
                }

                order.Status = OrderStatus.Cancelled;

                _logger.LogInformation(
                    "Order {OrderId} recovered successfully.",
                    order.Id);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(
                    ex,
                    "InventoryService connection failed for Order {OrderId}",
                    order.Id);
            }
            finally
            {
                order.IsRecoveryLocked = false;
                order.RecoveryLockedAt = null;

                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }
}