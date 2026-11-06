using InventoryService.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Data
{
    public class InventoryDbContext : DbContext
    {
        public InventoryDbContext(DbContextOptions<InventoryDbContext> options)
            : base(options)
        {
        }

        public DbSet<Inventory> Inventories { get; set; }
        public DbSet<StockReservation> StockReservations { get; set; }
        public DbSet<OrderInventoryState> OrderInventoryStates { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<StockReservation>()
                .HasIndex(x => new { x.OrderId, x.ProductId })
                .IsUnique();

            modelBuilder.Entity<OrderInventoryState>()
                .HasIndex(x => x.OrderId)
                .IsUnique();
        }
    }
}
