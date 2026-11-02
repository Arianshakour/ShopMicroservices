using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Dtos;
using OrderService.Entities;
using Polly.CircuitBreaker;
using Polly.Timeout;
using System.Net;

namespace OrderService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly OrderDbContext _db;
        public OrdersController(IHttpClientFactory httpClientFactory, OrderDbContext db)
        {
            _httpClientFactory = httpClientFactory;
            _db = db;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            var orders = _db.Orders
                            .Include(x => x.Items)
                            .Select(x => new OrderDto
                            {
                                Id = x.Id,
                                CreatedAt = x.CreatedAt,
                                TotalPrice = x.TotalPrice,

                                Items = x.Items.Select(i => new OrderItemDto
                                {
                                    ProductId = i.ProductId,
                                    ProductName = i.ProductName,
                                    UnitPrice = i.UnitPrice,
                                    Quantity = i.Quantity,
                                    TotalPrice = i.TotalPrice
                                }).ToList()
                            })
                            .ToList();

            return Ok(orders);
        }

        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            var order = _db.Orders
                            .Where(x => x.Id == id)
                            .Select(x => new OrderDto
                            {
                                Id = x.Id,
                                CreatedAt = x.CreatedAt,
                                TotalPrice = x.TotalPrice,

                                Items = x.Items.Select(i => new OrderItemDto
                                {
                                    ProductId = i.ProductId,
                                    ProductName = i.ProductName,
                                    UnitPrice = i.UnitPrice,
                                    Quantity = i.Quantity,
                                    TotalPrice = i.TotalPrice
                                }).ToList()
                            })
                            .FirstOrDefault();

            if (order == null)
                return NotFound();

            return Ok(order);
        }
        [HttpGet("product/{productId}")]
        public async Task<IActionResult> GetProduct(int productId)
        {
            var client = _httpClientFactory.CreateClient("ProductService");

            var product = await client.GetFromJsonAsync<ProductDto>(
                $"api/products/{productId}");

            if (product == null)
                return NotFound();

            return Ok(product);
        }
        [HttpPost]
        public async Task<IActionResult> Create(CreateOrderRequest request,
    CancellationToken cancellationToken)
        {
            //vorodie cancellationToken baraye ineke age karbar darkhastesho laqv kard ya ertebat qat shod
            //darkhast http ham betone laqv beshe
            //khodesh ba asp.net core automat por mishe
            if (request.Items == null || !request.Items.Any())
                return BadRequest("Order must have at least one item.");

            var client = _httpClientFactory.CreateClient("ProductService");

            var order = new Order
            {
                CreatedAt = DateTime.UtcNow
            };

            foreach (var item in request.Items)
            {
                ProductDto? product;
                HttpResponseMessage response;

                if (item.Quantity <= 0)
                    return BadRequest("Quantity must be greater than zero.");

                //Modiriat khata va modiriat qat boodan shabake Product
                try
                {
                    response = await client.GetAsync(
                        $"api/products/{item.ProductId}", cancellationToken);
                }
                catch (BrokenCircuitException)
                {
                    return StatusCode(503, "ProductService circuit is open.");
                }
                catch (TimeoutRejectedException)
                {
                    return StatusCode(504, "ProductService request timed out.");
                }
                catch (HttpRequestException)
                {
                    return StatusCode(503, "ProductService is unavailable.");
                }
                catch (TaskCanceledException)
                {
                    return StatusCode(504, "ProductService request timed out.");
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                    return NotFound($"Product {item.ProductId} not found.");

                if (!response.IsSuccessStatusCode)
                    return StatusCode(502, "Error while communicating with ProductService.");

                product = await response.Content.ReadFromJsonAsync<ProductDto>();

                if (product == null)
                    return StatusCode(502, "Invalid response from ProductService.");

                var orderItem = new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = product.Price,
                    Quantity = item.Quantity,
                    TotalPrice = product.Price * item.Quantity
                };

                order.Items.Add(orderItem);
                order.TotalPrice += orderItem.TotalPrice;
            }

            _db.Orders.Add(order);

            await _db.SaveChangesAsync();

            var result = new OrderDto
            {
                Id = order.Id,
                CreatedAt = order.CreatedAt,
                TotalPrice = order.TotalPrice,

                Items = order.Items.Select(x => new OrderItemDto
                {
                    ProductId = x.ProductId,
                    ProductName = x.ProductName,
                    UnitPrice = x.UnitPrice,
                    Quantity = x.Quantity,
                    TotalPrice = x.TotalPrice
                }).ToList()
            };

            return Ok(result);
        }
    }
}
