namespace OrderService.Entities
{
    public class Order
    {
        public int Id { get; set; }

        public DateTime CreatedAt { get; set; }

        public decimal TotalPrice { get; set; }
        public OrderStatus Status { get; set; }

        //Relaiton

        public List<OrderItem> Items { get; set; } = new();
    }
}
