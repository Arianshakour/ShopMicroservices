using InventoryService.Data;
using InventoryService.Dtos;
using InventoryService.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryController : ControllerBase
    {
        private readonly InventoryDbContext _db;

        public InventoryController(InventoryDbContext db)
        {
            _db = db;
        }

        // اضافه کردن موجودی
        [HttpPost("add-stock")]
        public IActionResult AddStock(AddStockRequest request)
        {
            if (request.Quantity <= 0)
                return BadRequest("Quantity must be greater than zero.");
            if (request.ProductId <= 0)
                return BadRequest("ProductId must be greater than zero.");

            var inventory = _db.Inventories
                .FirstOrDefault(x => x.ProductId == request.ProductId);

            if (inventory == null)
            {
                inventory = new Inventory
                {
                    ProductId = request.ProductId,
                    AvailableQuantity = request.Quantity,
                    ReservedQuantity = 0
                };

                _db.Inventories.Add(inventory);
            }
            else
            {
                inventory.AvailableQuantity += request.Quantity;
            }

            _db.SaveChanges();

            return Ok(inventory);
        }

        // رزرو موجودی
        [HttpPost("reserve")]
        public IActionResult ReserveStock(ReserveStockRequest request)
        {
            if (request.OrderId <= 0 ||
        request.ProductId <= 0 ||
        request.Quantity <= 0)
            {
                return BadRequest("Invalid request.");
            }

            var inventory = _db.Inventories
                .FirstOrDefault(x => x.ProductId == request.ProductId);

            if (inventory == null)
                return NotFound("Inventory not found.");

            if (inventory.AvailableQuantity < request.Quantity)
                return BadRequest("Insufficient stock.");

            inventory.AvailableQuantity -= request.Quantity;
            inventory.ReservedQuantity += request.Quantity;

            var reservation = new StockReservation
            {
                OrderId = request.OrderId,
                ProductId = request.ProductId,
                Quantity = request.Quantity,
                CreatedAt = DateTime.UtcNow
            };

            _db.StockReservations.Add(reservation);

            _db.SaveChanges();

            return Ok(reservation);

        }

        // آزادسازی موجودی رزروشده
        [HttpPost("release")]
        public IActionResult ReleaseStock(ReleaseStockRequest request)
        {
            var reservations = _db.StockReservations
                .Where(x => x.OrderId == request.OrderId)
                    .ToList();

            if (!reservations.Any())
                return Ok("No reservations to release.");

            foreach (var reservation in reservations)
            {
                var inventory = _db.Inventories
                    .FirstOrDefault(x => x.ProductId == reservation.ProductId);

                if (inventory == null)
                    return StatusCode(500, "Inventory record not found.");

                inventory.ReservedQuantity -= reservation.Quantity;
                inventory.AvailableQuantity += reservation.Quantity;
            }

            _db.StockReservations.RemoveRange(reservations);

            _db.SaveChanges();

            return Ok("Stock released successfully.");
        }
    }
}
