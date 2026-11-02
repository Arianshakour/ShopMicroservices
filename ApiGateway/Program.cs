var builder = WebApplication.CreateBuilder(args);

// Register YARP
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Configure YARP
app.MapReverseProxy();

app.Run();

//Test 
//khode ApiGateway ipish shod localhost:5035 pas ba test 2ta zir didim doroste
//http://localhost:5035/api/products
//http://localhost:5035/api/orders

//Dastorate Migration dar Terminal
//nemikhad beri to root hamoon to aslie bezan ShopMicroservice
//dotnet ef migrations add InitialCreate --project ProductService --startup-project ProductService
//dotnet ef database update --project ProductService --startup-project ProductService
//dotnet ef migrations add InitialCreate --project OrderService --startup-project OrderService
//dotnet ef database update --project OrderService --startup-project OrderService