using Microsoft.EntityFrameworkCore;
using OrderRecoveryWorker;
using OrderService.Data;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("OrderDb")));


//InventoryService
builder.Services.AddHttpClient("InventoryService", client =>
{
    client.BaseAddress = new Uri("http://localhost:5091/");
});


var host = builder.Build();
host.Run();
