namespace OrderService.Entities
{
    public class OutboxMessage
    {
        public int Id { get; set; }

        public string Type { get; set; } = string.Empty;//نوع Event، مثلاً OrderCancelledEvent

        public string Payload { get; set; } = string.Empty;//	محتوای JSON پیام

        public DateTime CreatedAt { get; set; }

        public DateTime? ProcessedAt { get; set; }

        public int RetryCount { get; set; }

        public string? Error { get; set; }
    }
}
