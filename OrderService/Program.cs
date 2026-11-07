using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Messaging;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("OrderDb")));

//ba in ziri mitoonim ba HttpClient az yek service be api yek service dg darkhast http bezanim
builder.Services.AddHttpClient("ProductService", client =>
{
    client.BaseAddress = new Uri("http://localhost:5090/");
})
.AddStandardResilienceHandler(options =>//nasbe package Microsoft.Extensions.Http.Resilience
{
    // Retry
    options.Retry.MaxRetryAttempts = 3;//3 bar talash mojadad mikone
    options.Retry.Delay = TimeSpan.FromSeconds(2);//faselash 2 sanias

    // Circuit Breaker
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.MinimumThroughput = 5;
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
    //tarjome 4 khat bala
    //age 5 ta request bere dar baze 30sanie va hadeaqal nesfesh khata bokhore ta 15 sanie request jadid ra obur nemide

    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);//tanzim timeout
});

//InventoryService
builder.Services.AddHttpClient("InventoryService", client =>
{
    client.BaseAddress = new Uri("http://localhost:5091/");
});


//config vasl shodan be RabbitMQ
builder.Services.AddSingleton<IConnection>(sp =>
{
    var factory = new ConnectionFactory
    {
        HostName = "localhost",
        UserName = "guest",
        Password = "guest"
    };

    return factory.CreateConnectionAsync().GetAwaiter().GetResult();
});


builder.Services.AddSingleton<RabbitMqPublisher>();

builder.Services.AddScoped<OutboxService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
