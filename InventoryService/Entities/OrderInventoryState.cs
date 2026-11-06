namespace InventoryService.Entities
{
    public class OrderInventoryState
    {
        public int Id { get; set; }

        public int OrderId { get; set; }

        public string Status { get; set; } = "Active";
    }
}
