namespace InventoryService.Dtos
{
    public class AddStockRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
}
