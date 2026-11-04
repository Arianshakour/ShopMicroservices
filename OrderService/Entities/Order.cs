namespace OrderService.Entities
{
    public class Order
    {
        public int Id { get; set; }

        public DateTime CreatedAt { get; set; }

        public decimal TotalPrice { get; set; }
        public OrderStatus Status { get; set; }
        public DateTime LastSagaActivityAt { get; set; }
        //in field zir baraye qofl kardan dar amaliat worker hast farz kon 2 ta worker dari har ki zoodtar gereft
        //lock mikone va status va IsRecoveryLocked ra update mikone ke worker dg in recordo nagire
        public bool IsRecoveryLocked { get; set; }
        //in field zir baraye ine vasat kar worker yeho worker qat shod dobare betoonim oon record baresi konim
        public DateTime? RecoveryLockedAt { get; set; }

        //Relaiton

        public List<OrderItem> Items { get; set; } = new();
    }
}
