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
                                Status = x.Status,
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
                                Status = x.Status,
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

            if (request.Items.Any(x => x.ProductId <= 0 || x.Quantity <= 0))
                return BadRequest("Invalid order items.");

            var productClient = _httpClientFactory.CreateClient("ProductService");
            var inventoryClient = _httpClientFactory.CreateClient("InventoryService");

            //Step1 =>getproduct
            var order = new Order
            {
                CreatedAt = DateTime.UtcNow,
                LastSagaActivityAt = DateTime.UtcNow,
                Status = OrderStatus.Pending
            };

            try
            {
                foreach (var item in request.Items)
                {
                    var response = await productClient.GetAsync(
                        $"api/products/{item.ProductId}");

                    if (!response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == HttpStatusCode.NotFound)
                            return NotFound($"Product {item.ProductId} not found.");

                        return StatusCode(502, "ProductService returned an error.");
                    }

                    var product = await response.Content.ReadFromJsonAsync<ProductDto>();

                    if (product == null)
                        return StatusCode(502, "Invalid response from ProductService.");

                    order.Items.Add(new OrderItem
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        UnitPrice = product.Price,
                        Quantity = item.Quantity,
                        TotalPrice = product.Price * item.Quantity
                    });

                    order.TotalPrice += product.Price * item.Quantity;
                }
            }
            catch (HttpRequestException)
            {
                return StatusCode(503, "ProductService is unavailable.");
            }
            catch (TaskCanceledException)
            {
                return StatusCode(503, "ProductService request timed out.");
            }

            //step2 => create order with pending
            _db.Orders.Add(order);

            await _db.SaveChangesAsync(cancellationToken);

            //baraye test dashtan status pending
            //return Ok();

            // step3 => reserve
            bool reservationFailed = false;

            try
            {
                foreach (var item in order.Items)
                {
                    var response = await inventoryClient.PostAsJsonAsync(
                        "api/inventory/reserve",
                        new
                        {
                            OrderId = order.Id,
                            item.ProductId,
                            item.Quantity
                        });

                    if (!response.IsSuccessStatusCode)
                    {
                        reservationFailed = true;
                        break;
                    }

                    // رزرو موفق بوده؛ زمان آخرین فعالیت را به‌روز می‌کنیم
                    order.LastSagaActivityAt = DateTime.UtcNow;

                    await _db.SaveChangesAsync();
                }
            }
            catch (HttpRequestException)
            {
                reservationFailed = true;
            }
            catch (TaskCanceledException)
            {
                reservationFailed = true;
            }

            // step4 => jobrani
            if (reservationFailed)
            {
                try
                {
                    var releaseResponse = await inventoryClient.PostAsJsonAsync(
                        "api/inventory/release",
                        new
                        {
                            OrderId = order.Id
                        });

                    if (releaseResponse.IsSuccessStatusCode)
                    {
                        order.Status = OrderStatus.Cancelled;
                        await _db.SaveChangesAsync();

                        return BadRequest(
                            $"Order cancelled. Stock reservations released. OrderId: {order.Id}");
                    }
                }
                catch (HttpRequestException)
                {
                    // Compensation failed
                }
                catch (TaskCanceledException)
                {
                    // Compensation timed out
                }

                // اگر Compensation موفق نشد، سفارش Pending باقی می‌ماند.
                return StatusCode(
                    503,
                    $"Stock reservation failed. Order {order.Id} requires recovery.");
            }

            

            // step 5 => confirm reserve
            order.Status = OrderStatus.Confirmed;

            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new OrderDto
            {
                Id = order.Id,
                CreatedAt = order.CreatedAt,
                TotalPrice = order.TotalPrice,
                Status = order.Status,
                Items = order.Items.Select(x => new OrderItemDto
                {
                    ProductId = x.ProductId,
                    ProductName = x.ProductName,
                    UnitPrice = x.UnitPrice,
                    Quantity = x.Quantity,
                    TotalPrice = x.TotalPrice
                }).ToList()
            });
        }
    }
}
