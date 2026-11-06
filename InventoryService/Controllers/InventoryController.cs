using InventoryService.Data;
using InventoryService.Dtos;
using InventoryService.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;

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

            //Idempotency
            //chek kardan tekrari boodan sefaresh ba hamoon product
            var existingReservation = _db.StockReservations.FirstOrDefault(x =>
                x.OrderId == request.OrderId &&
                x.ProductId == request.ProductId);

            if (existingReservation != null)
            {
                if (existingReservation.Quantity != request.Quantity)
                    return Conflict("Reservation already exists with a different quantity.");

                return Ok(existingReservation);
            }

            //baresi Vaziat Sefaresh
            var orderState = _db.OrderInventoryStates
                .FirstOrDefault(x => x.OrderId == request.OrderId);

            if (orderState != null &&
                (orderState.Status == "Cancelling" || orderState.Status == "Cancelled"))
            {
                return Conflict("Order is cancelling or already cancelled.");
            }

            //Race Condition
            //baraye jelogiri az 2 request hamzaman va atomic kardane gereftan product va kam kardane mojodi
            using var transaction = _db.Database.BeginTransaction(IsolationLevel.Serializable);

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

            try
            {
                _db.SaveChanges();

                transaction.Commit();

                return Ok(reservation);
            }
            catch (DbUpdateException ex)//modoriate Unique Index ke gozashtam dar dbcontext
                when (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx
                      && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
            {

                transaction.Rollback();

                _db.Entry(reservation).State = EntityState.Detached;
                //خط Detached باعث می‌شه Entity رزروی که ذخیره‌سازی‌اش شکست خورده، در Change Tracker باقی نمونه

                existingReservation = _db.StockReservations
                    .AsNoTracking()
                    .FirstOrDefault(x =>
                        x.OrderId == request.OrderId &&
                        x.ProductId == request.ProductId);

                if (existingReservation != null)
                {
                    if (existingReservation.Quantity != request.Quantity)
                        return Conflict("Reservation already exists with a different quantity.");

                    return Ok(existingReservation);
                }

                throw;
            }

        }

        // آزادسازی موجودی رزروشده
        [HttpPost("release")]
        public IActionResult ReleaseStock(ReleaseStockRequest request)
        {
            if (request.OrderId <= 0)
                return BadRequest("Invalid OrderId.");

            //Race Condition
            using var transaction = _db.Database.BeginTransaction(IsolationLevel.Serializable);

            //chon momkene 2 ya chand nemone az Inventory service dashte bashim lock c# javab nemide
            //chon lock c# faqat roye hamon nemone kar mikone pas injori minevisim dar zemn sql injection ham nadare
            var reservations = _db.StockReservations
                .FromSqlInterpolated($@"
                SELECT *
                FROM StockReservations WITH (UPDLOCK, HOLDLOCK)
                WHERE OrderId = {request.OrderId}
                ")
                .ToList();

            if (!reservations.Any())
            {
                transaction.Commit();
                return Ok("No reservations to release.");
            }

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

            transaction.Commit();

            return Ok("Stock released successfully.");
        }
    }
}
