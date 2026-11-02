namespace InventoryService.Entities
{
    public class Inventory
    {
        public int Id { get; set; }

        public int ProductId { get; set; }

        public int AvailableQuantity { get; set; }

        public int ReservedQuantity { get; set; }
    }
}
