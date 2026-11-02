namespace InventoryService.Dtos
{
    public class ReserveStockRequest
    {
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
}
