namespace InventoryService.Entities
{
    public class StockReservation
    {
        //reserv haye 1 sefaresh(order) mibinim
        public int Id { get; set; }

        public int OrderId { get; set; }

        public int ProductId { get; set; }

        public int Quantity { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
